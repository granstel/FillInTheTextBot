#!/usr/bin/env bash
#
# Раскатка версии без простоя: ./rollout.sh <версия>
#
# Два проекта compose из одного файла: новая версия поднимается в свободном цвете,
# прежний гасится после подтверждения ротации от прокси.

set -euo pipefail

VERSION="${1:?Использование: rollout.sh <версия>}"
export VERSION

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Параметры — из .env рядом со скриптом
if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi

COMPOSE_FILE="${COMPOSE_FILE:-service-compose.yml}"
NETWORK="${DOCKER_NETWORK:-network}"
APP_ENV_FILE="${APP_ENV_FILE:-${SCRIPT_DIR}/app.env}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-60}"
TRAEFIK_CONTAINER="${TRAEFIK_CONTAINER:-traefik}"
TRAEFIK_SERVICE="${TRAEFIK_SERVICE:-fitb}"
ROTATION_TIMEOUT="${ROTATION_TIMEOUT:-30}"
PROJECT_BLUE="${PROJECT_BLUE:-fitb-blue}"
PROJECT_GREEN="${PROJECT_GREEN:-fitb-green}"

if [ ! -f "$APP_ENV_FILE" ]; then
  echo "!! Не найден файл окружения приложения: $APP_ENV_FILE" >&2
  echo "   Скопируйте app-env.example в app.env и заполните секреты." >&2
  exit 1
fi

# Проверяем до образа и контейнера, чтобы не тратить цикл ради отказа в конце.
TRAEFIK_IP="$(docker inspect -f "{{ (index .NetworkSettings.Networks \"${NETWORK}\").IPAddress }}" "$TRAEFIK_CONTAINER" 2>/dev/null || true)"

if [ -z "$TRAEFIK_IP" ]; then
  echo "!! Контейнер ${TRAEFIK_CONTAINER} не найден в сети ${NETWORK}." >&2
  echo "   Поднимите прокси: docker compose -f docker-compose.yml up -d" >&2
  exit 1
fi

compose() { docker compose -p "$1" -f "$COMPOSE_FILE" "${@:2}"; }

running() {
  [ -n "$(docker ps -q --filter "label=com.docker.compose.project=$1" \
                      --filter "label=com.docker.compose.service=fitb")" ]
}

if running "$PROJECT_BLUE"; then
  ACTIVE="$PROJECT_BLUE"; TARGET="$PROJECT_GREEN"
elif running "$PROJECT_GREEN"; then
  ACTIVE="$PROJECT_GREEN"; TARGET="$PROJECT_BLUE"
else
  ACTIVE=""; TARGET="$PROJECT_BLUE"
fi

echo "==> Активен: ${ACTIVE:-(нет — первый запуск)}; поднимаем в ${TARGET}"

echo "==> Тянем образ версии ${VERSION}"
compose "$TARGET" pull

# --wait ждёт HEALTHCHECK образа
echo "==> Поднимаем ${TARGET} и ждём готовности, до ${HEALTH_TIMEOUT}s"
if ! compose "$TARGET" up -d --wait --wait-timeout "$HEALTH_TIMEOUT"; then
  echo "!! Новый экземпляр не стал здоровым за ${HEALTH_TIMEOUT}s — откатываемся." >&2
  compose "$TARGET" logs --tail 50 || true
  compose "$TARGET" down >/dev/null 2>&1 || true
  exit 1
fi
echo "   Новый экземпляр здоров."

NEW_ID="$(compose "$TARGET" ps -q fitb)"
NEW_IP="$(docker inspect -f "{{ (index .NetworkSettings.Networks \"${NETWORK}\").IPAddress }}" "$NEW_ID")"

# Гасить старый до подтверждения ротации — оставить пул без живых серверов.
echo "==> Ждём, пока Traefik возьмёт ${NEW_IP} в ротацию, до ${ROTATION_TIMEOUT}s"
deadline=$(( $(date +%s) + ROTATION_TIMEOUT ))
API_URL="http://${TRAEFIK_IP}:8080/api/http/services/${TRAEFIK_SERVICE}@docker"
until curl -fsS -m 2 "$API_URL" 2>/dev/null | grep -q "\"http://${NEW_IP}:80\":\"UP\""
do
  if [ "$(date +%s)" -ge "$deadline" ]; then
    echo "!! Traefik не взял новый экземпляр в ротацию за ${ROTATION_TIMEOUT}s — откатываемся." >&2
    compose "$TARGET" down >/dev/null 2>&1 || true
    exit 1
  fi
  sleep 1
done
echo "   Traefik балансирует на новый экземпляр."

if [ -n "$ACTIVE" ]; then
  echo "==> Сливаем и останавливаем ${ACTIVE}"
  compose "$ACTIVE" down || true
fi

echo "==> Чистим повисшие образы"
docker image prune -f >/dev/null 2>&1 || true

echo "==> Готово: активна версия ${VERSION} в ${TARGET}"
