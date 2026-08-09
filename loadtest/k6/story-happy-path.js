import http from 'k6/http';
import { check, sleep } from 'k6';

// Нагрузочный тест happy path «сочинения истории» через Yandex-эндпоинт.
//
// ВАЖНО про эмулятор: Dialogflow-эмулятор — стаб. Он матчит интент по имени
// события или ТОЧНОЙ обучающей фразе, без разбора языка, БЕЗ слот-филлинга и
// контекстов. Поэтому полноценно «сочинить историю от начала до конца» (заполнить
// слоты словами и получить итоговый текст) на нём нельзя — многошаговый диалог
// со слотами тут не воспроизводится. Скрипт гоняет достижимую последовательность
// реплик story-бота, каждая из которых попадает в реальный интент (не Fallback)
// и возвращает осмысленный ответ, прогоняя весь конвейр: HTTP → контроллер →
// ConversationService → gRPC в Dialogflow-эмулятор → Redis (кэш сессии) → ответ.

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const YANDEX_PATH = __ENV.YANDEX_PATH || '/yandex';

const PEAK_RPS = Number(__ENV.PEAK_RPS || 18);
const RAMP_DURATION = __ENV.RAMP_DURATION || '1m';   // плавный подъём/спуск
const PEAK_DURATION = __ENV.PEAK_DURATION || '1m';   // длительность пика

// Один проход = одна пользовательская сессия. Реплики идут по кругу; при step===0
// открывается новая сессия. Все шаги проверены на живом стенде (200 + не Fallback).
const STEPS = [
  { command: '', newSession: true },                 // пустая команда в новой сессии → событие Welcome
  { command: 'что ты умеешь', newSession: false },   // What can you do
  { command: 'покажи список историй', newSession: false }, // TextsList — вход в выбор истории
  { command: 'помоги мне', newSession: false },      // Help
  { command: 'выйти', newSession: false },            // Exit
];

export const options = {
  scenarios: {
    story_happy_path: {
      executor: 'ramping-arrival-rate',
      timeUnit: '1s',
      startRate: 0,
      preAllocatedVUs: 20,
      maxVUs: 60,
      stages: [
        { target: PEAK_RPS, duration: RAMP_DURATION }, // плавный подъём до пика
        { target: PEAK_RPS, duration: PEAK_DURATION }, // пик
        { target: 0, duration: RAMP_DURATION },        // плавный спуск
      ],
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],       // почти нет сетевых ошибок / не-2xx
    http_req_duration: ['p(95)<800'],     // p95 задержки
    checks: ['rate>0.99'],                // почти все смысловые проверки прошли
  },
};

// Состояние на VU: модуль инстанцируется отдельно в каждом VU, поэтому эти
// переменные — персессионные для конкретного VU и переживают итерации.
let step = 0;
let sessionId = null;
let sessionCounter = 0;

function newSessionId() {
  sessionCounter += 1;
  return `k6-${__VU}-${sessionCounter}-${Date.now()}`;
}

function payload(command, newSession, session, messageId) {
  return JSON.stringify({
    meta: { locale: 'ru-RU', timezone: 'UTC', client_id: 'k6-loadtest' },
    session: {
      message_id: messageId,
      session_id: session,
      skill_id: 'k6-loadtest',
      user_id: `user-${session}`,
      new: newSession,
    },
    request: {
      command,
      original_utterance: command,
      type: 'SimpleUtterance',
      markup: { dangerous_context: false },
      nlu: { tokens: [], entities: [] },
    },
    state: { session: {}, user: {} },
    version: '1.0',
  });
}

// Дожидаемся готовности всей цепочки (приложение + эмулятор + Redis) перед
// нагрузкой: при холодном старте одной командой k6 может обогнать поднятие сервисов.
export function setup() {
  const deadlineMs = Number(__ENV.WARMUP_TIMEOUT_MS || 90000);
  const deadline = Date.now() + deadlineMs;
  let lastStatus = 0;

  while (Date.now() < deadline) {
    const res = http.post(
      `${BASE_URL}${YANDEX_PATH}`,
      payload('', true, `warmup-${Date.now()}`, 0),
      { headers: { 'Content-Type': 'application/json' }, timeout: '5s' },
    );
    lastStatus = res.status;
    if (res.status === 200) return;
    sleep(2);
  }

  throw new Error(`Стенд не готов за ${deadlineMs} мс, последний статус: ${lastStatus}`);
}

export default function () {
  if (step === 0) {
    sessionId = newSessionId();
  }

  const current = STEPS[step];

  const res = http.post(`${BASE_URL}${YANDEX_PATH}`, payload(current.command, current.newSession, sessionId, step), {
    headers: { 'Content-Type': 'application/json' },
    tags: { step: String(step) },
  });

  check(res, {
    'статус 200': (r) => r.status === 200,
    'есть текст ответа': (r) => {
      try { return typeof r.json('response.text') === 'string' && r.json('response.text').length > 0; }
      catch (e) { return false; }
    },
    'не Fallback': (r) => {
      try { return !String(r.json('response.text')).startsWith('Не совсем понимаю'); }
      catch (e) { return false; }
    },
  });

  step = (step + 1) % STEPS.length;
}
