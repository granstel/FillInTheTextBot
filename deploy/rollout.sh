#!/usr/bin/env bash
#
# Раскатка версии без простоя: ./rollout.sh <версия>
#
# Новый контейнер поднимается рядом со старым и попадает в тот же пул Traefik.
# Старый гасится только после того, как прокси подтвердил, что балансирует на новый.

set -euo pipefail

VERSION="${1:?Использование: rollout.sh <версия>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Параметры — из .env рядом со скриптом
if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi

IMAGE_REPO="${IMAGE_REPO:-granstel/fillinthetextbot}"
IMAGE="${IMAGE_REPO}:${VERSION}"
NETWORK="${DOCKER_NETWORK:-network}"
ALIAS="${SERVICE_ALIAS:-fitb}"
DOMAIN="${DOMAIN:?задайте DOMAIN в .env}"
KEYS_DIR="${KEYS_DIR:?задайте KEYS_DIR в .env}"
LOGS_DIR="${LOGS_DIR:?задайте LOGS_DIR в .env}"
APP_ENV_FILE="${APP_ENV_FILE:-${SCRIPT_DIR}/app.env}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-60}"
# Должен быть больше, чем DrainDelaySeconds + Shutdown.TimeoutSeconds сервиса
STOP_TIMEOUT="${STOP_TIMEOUT:-45}"
TRAEFIK_CONTAINER="${TRAEFIK_CONTAINER:-traefik}"
TRAEFIK_SERVICE="${TRAEFIK_SERVICE:-fitb}"
ROTATION_TIMEOUT="${ROTATION_TIMEOUT:-30}"

if [ ! -f "$APP_ENV_FILE" ]; then
  echo "!! Не найден файл окружения приложения: $APP_ENV_FILE" >&2
  echo "   Скопируйте app-env.example в app.env и заполните секреты." >&2
  exit 1
fi

# Версия + метка времени, чтобы можно было перекатать ту же версию
SAFE_VERSION="${VERSION//[^A-Za-z0-9_.-]/_}"
NEW_NAME="fitb_${SAFE_VERSION}_$(date +%s)"

if ! docker network inspect "$NETWORK" >/dev/null 2>&1; then
  echo "==> Создаю docker-сеть ${NETWORK}"
  docker network create "$NETWORK" >/dev/null
fi

echo "==> Тянем образ ${IMAGE}"
docker pull "$IMAGE"

echo "==> Текущие контейнеры сервиса:"
mapfile -t OLD < <(docker ps --filter "label=app=fitb" --format '{{.Names}}')
if [ "${#OLD[@]}" -eq 0 ]; then
  echo "   (нет — первый запуск)"
else
  printf '   %s\n' "${OLD[@]}"
fi

echo "==> Поднимаем новый экземпляр ${NEW_NAME}"
docker run -d \
  --name "$NEW_NAME" \
  --restart unless-stopped \
  --network "$NETWORK" \
  --network-alias "$ALIAS" \
  --env-file "$APP_ENV_FILE" \
  -v "${KEYS_DIR}:/app/keys:ro" \
  -v "${LOGS_DIR}:/app/logs" \
  --label app=fitb \
  --label traefik.enable=true \
  --label "traefik.docker.network=${NETWORK}" \
  --label "traefik.http.routers.fitb.rule=Host(\`${DOMAIN}\`)" \
  --label traefik.http.routers.fitb.entrypoints=websecure \
  --label traefik.http.routers.fitb.tls=true \
  --label traefik.http.routers.fitb.tls.certresolver=le \
  --label traefik.http.services.fitb.loadbalancer.server.port=80 \
  --label traefik.http.services.fitb.loadbalancer.healthcheck.path=/health \
  --label traefik.http.services.fitb.loadbalancer.healthcheck.interval=3s \
  --label traefik.http.services.fitb.loadbalancer.healthcheck.timeout=2s \
  "$IMAGE" >/dev/null

# Стучимся прямо в IP контейнера — curl внутри образа не нужен
NEW_IP="$(docker inspect -f "{{ (index .NetworkSettings.Networks \"${NETWORK}\").IPAddress }}" "$NEW_NAME")"
echo "==> Ждём готовности ${NEW_NAME} (${NEW_IP}) на /health, до ${HEALTH_TIMEOUT}s"

deadline=$(( $(date +%s) + HEALTH_TIMEOUT ))
until curl -fsS -m 2 "http://${NEW_IP}/health" >/dev/null 2>&1; do
  if [ "$(date +%s)" -ge "$deadline" ]; then
    echo "!! Новый экземпляр не стал здоровым за ${HEALTH_TIMEOUT}s — откатываемся." >&2
    docker logs --tail 50 "$NEW_NAME" || true
    docker rm -f "$NEW_NAME" >/dev/null 2>&1 || true
    exit 1
  fi
  sleep 2
done
echo "   Новый экземпляр здоров."

# Готовность приложения ≠ готовность прокси: Traefik узнаёт о ней своей проверкой
# (healthcheck.interval, 3с). Погасить старый раньше — значит оставить пул без живых
# серверов, и клиент получит 503 от самого прокси. Замер во время раската: без этого
# ожидания 12 таких ответов на 338 запросов обычного трафика, с ним — 0 из 911 и 0 из
# 1342 в двух прогонах. В логе Traefik они отличимы: у ответа сливающегося экземпляра
# указан бэкенд, у ответа самого прокси вместо бэкенда прочерк.
TRAEFIK_IP="$(docker inspect -f "{{ (index .NetworkSettings.Networks \"${NETWORK}\").IPAddress }}" "$TRAEFIK_CONTAINER" 2>/dev/null || true)"

if [ -z "$TRAEFIK_IP" ]; then
  echo "!! Контейнер ${TRAEFIK_CONTAINER} не найден в сети ${NETWORK} — прерываюсь, старый работает." >&2
  docker rm -f "$NEW_NAME" >/dev/null 2>&1 || true
  exit 1
fi

echo "==> Ждём, пока Traefik возьмёт ${NEW_IP} в ротацию, до ${ROTATION_TIMEOUT}s"
deadline=$(( $(date +%s) + ROTATION_TIMEOUT ))
API_URL="http://${TRAEFIK_IP}:8080/api/http/services/${TRAEFIK_SERVICE}@docker"
until curl -fsS -m 2 "$API_URL" 2>/dev/null | grep -q "\"http://${NEW_IP}:80\":\"UP\""
do
  if [ "$(date +%s)" -ge "$deadline" ]; then
    echo "!! Traefik не взял новый экземпляр в ротацию за ${ROTATION_TIMEOUT}s — откатываемся." >&2
    docker rm -f "$NEW_NAME" >/dev/null 2>&1 || true
    exit 1
  fi
  sleep 1
done
echo "   Traefik балансирует на новый экземпляр."

for c in "${OLD[@]}"; do
  [ -z "$c" ] && continue
  echo "==> Сливаем и останавливаем старый ${c} (до ${STOP_TIMEOUT}s)"
  docker stop -t "$STOP_TIMEOUT" "$c" >/dev/null || true
  docker rm "$c" >/dev/null 2>&1 || true
done

echo "==> Чистим повисшие образы"
docker image prune -f >/dev/null 2>&1 || true

echo "==> Готово: активна версия ${VERSION} (${NEW_NAME})"
