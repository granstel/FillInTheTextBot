#!/usr/bin/env bash
#
# Бесшовная раскатка новой версии сервиса.
#
#   ./rollout.sh <версия>
#
# Идея: не гасить старый контейнер до того, как новый готов принимать трафик.
#   1. Тянем образ нужной версии.
#   2. Поднимаем НОВЫЙ контейнер рядом со старым, с теми же метками Traefik —
#      Traefik добавляет его в пул балансировки как второй сервер.
#   3. Ждём, пока новый ответит здоровьем на /health.
#   4. Гасим старый: он по SIGTERM объявляет себя неготовым (health -> 503),
#      Traefik уводит с него трафик, приложение доживает текущие запросы и
#      разбирает очередь фоновых задач, и только потом выходит.
#
# В любой момент времени трафик обслуживает хотя бы один готовый экземпляр.

set -euo pipefail

VERSION="${1:?Использование: rollout.sh <версия>}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Параметры раскатки (пути, домен, имя образа) — из deploy/.env
if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi

IMAGE_REPO="${IMAGE_REPO:-granstel/fillinthetextbot}"
IMAGE="${IMAGE_REPO}:${VERSION}"
NETWORK="${DOCKER_NETWORK:-network}"
ALIAS="${SERVICE_ALIAS:-fitb}"           # стабильное сетевое имя для Prometheus
DOMAIN="${DOMAIN:?DOMAIN должен быть задан в deploy/.env}"
KEYS_DIR="${KEYS_DIR:-/docker/keys}"     # ключи сервис-аккаунтов Dialogflow
LOGS_DIR="${LOGS_DIR:-/docker/logs/fitb}"
APP_ENV_FILE="${APP_ENV_FILE:-${SCRIPT_DIR}/app.env}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-60}"   # сколько ждём готовности нового, сек
# Запас на слив трафика (drain) + завершение запросов и очереди фоновых задач.
# Должен быть больше, чем DrainDelaySeconds + Shutdown.TimeoutSeconds сервиса.
STOP_TIMEOUT="${STOP_TIMEOUT:-45}"

if [ ! -f "$APP_ENV_FILE" ]; then
  echo "!! Не найден файл окружения приложения: $APP_ENV_FILE" >&2
  echo "   Скопируйте app.env.example в app.env и заполните секреты." >&2
  exit 1
fi

# Уникальное имя: версия + метка времени, чтобы можно было переката́ть ту же версию
SAFE_VERSION="${VERSION//[^A-Za-z0-9_.-]/_}"
NEW_NAME="fitb_${SAFE_VERSION}_$(date +%s)"

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

# Ждём готовности нового экземпляра, стучась прямо в его IP из сети docker.
# Хост дотягивается до контейнера напрямую, curl внутри образа не нужен.
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

# Новый в пуле Traefik и принимает трафик — можно спокойно уводить старые.
for c in "${OLD[@]}"; do
  [ -z "$c" ] && continue
  echo "==> Сливаем и останавливаем старый ${c} (до ${STOP_TIMEOUT}s)"
  docker stop -t "$STOP_TIMEOUT" "$c" >/dev/null || true
  docker rm "$c" >/dev/null 2>&1 || true
done

echo "==> Чистим повисшие образы"
docker image prune -f >/dev/null 2>&1 || true

echo "==> Готово: активна версия ${VERSION} (${NEW_NAME})"
