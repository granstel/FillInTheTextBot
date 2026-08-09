import http from 'k6/http';

export const options = { vus: 1, iterations: 1 };

const BASE = __ENV.BASE_URL || 'http://fillinthetextbot';
const PATH = '/yandex';
const SID = `probe-${Date.now()}`;

// Полный путь сочинения истории «29-late»: выбор истории + 6 слов
const STEPS = [
  ['welcome', '', true],
  ['история: опоздала в больницу', 'опоздала в больницу', false],
  ['слово 1 (number)', '3', false],
  ['слово 2 (character)', 'медведь', false],
  ['слово 3 (mult)', 'два', false],
  ['слово 4 (animals)', 'зайцы', false],
  ['слово 5 (place)', 'лес', false],
  ['слово 6 (speed)', '5', false],
];

function payload(cmd, isNew, i) {
  return JSON.stringify({
    meta: { locale: 'ru-RU', timezone: 'UTC', client_id: 'probe' },
    session: { message_id: i, session_id: SID, skill_id: 'probe', user_id: 'probe-user', new: isNew },
    request: {
      command: cmd, original_utterance: cmd, type: 'SimpleUtterance',
      markup: { dangerous_context: false }, nlu: { tokens: [], entities: [] },
    },
    state: { session: {}, user: {} }, version: '1.0',
  });
}

export default function () {
  for (let i = 0; i < STEPS.length; i++) {
    const [label, cmd, isNew] = STEPS[i];
    const res = http.post(`${BASE}${PATH}`, payload(cmd, isNew, i), {
      headers: { 'Content-Type': 'application/json' },
    });
    let text = '';
    try { text = res.json('response.text') || ''; } catch (e) { text = '<no json>'; }
    console.log(`[${res.status}] ${label} -> ${String(text).slice(0, 160)}`);
  }
}
