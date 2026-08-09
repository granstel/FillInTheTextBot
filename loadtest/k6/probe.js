import http from 'k6/http';

export const options = { vus: 1, iterations: 1 };

const BASE = __ENV.BASE_URL || 'http://fillinthetextbot';
const PATH = '/yandex';

// Единый session_id на всю последовательность — как в реальном диалоге
const SID = 'probe-session-1';

const STEPS = [
  ['welcome (пусто, new)', '', true],
  ['покажи список историй', 'покажи список историй', false],
  ['новый', 'новый', false],
  ['начать', 'начать', false],
  ['дальше', 'дальше', false],
  ['ещё', 'ещё', false],
  ['повтори', 'повтори', false],
  ['выйти', 'выйти', false],
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
    console.log(`[${res.status}] ${label} -> ${String(text).slice(0, 110)}`);
  }
}
