# Локальный эмулятор Dialogflow на базе Rasa

Этот проект предоставляет локальную замену для Google Dialogflow с использованием Rasa, которая позволяет вам разрабатывать и тестировать ваш чат-бот без подключения к внешним сервисам.

## Быстрый старт

### Предварительные требования

1. **Docker Desktop** - скачайте и установите с [официального сайта](https://www.docker.com/products/docker-desktop)
2. **Python 3.7+** - для скриптов конвертации
3. **Git** - для работы с репозиторием

### Установка и запуск

**Простой запуск через bat-файл (Windows):**
```bash
# Запуск эмулятора
start_emulator.bat

# Остановка эмулятора
stop_emulator.bat
```

**Ручной запуск через Docker Compose:**
```bash
# Запуск с автоматической конвертацией
docker-compose -f docker-compose.simple.yml up --build

# Остановка
docker-compose -f docker-compose.simple.yml down
```

**Что происходит при запуске:**
1. 🔄 Автоматически собирается Docker образ конвертера
2. 📝 Dialogflow агент конвертируется в формат Rasa
3. 🚀 Запускается Rasa сервер с обученной моделью
4. ⚡ Запускается Action Server для кастомных действий
5. 🔴 Запускается Redis для хранения сессий

## Структура проекта

```
FillInTheTextBot/
├── Dialogflow/                     # Исходные данные Dialogflow
│   └── FillInTheTextBot-eu/
│       ├── agent.json
│       ├── intents/
│       └── entities/
├── rasa_bot/                       # Сконвертированный Rasa бот
│   ├── config.yml                  # Конфигурация pipeline
│   ├── domain.yml                  # Домен бота
│   ├── endpoints.yml               # Настройки подключений
│   ├── data/
│   │   ├── nlu.yml                 # NLU данные
│   │   ├── stories.yml             # Диалоговые истории
│   │   └── responses.yml           # Ответы бота
│   └── actions/
│       └── actions.py              # Кастомные действия
├── docker-compose.simple.yml      # Docker Compose для быстрого старта
├── docker-compose.yml             # Полная конфигурация с Rasa X
├── dialogflow_to_rasa_converter.py # Скрипт конвертации
└── setup_rasa_emulator.py         # Скрипт автоматической настройки
```

## Использование

### API тестирование

После запуска контейнеров, Rasa API будет доступен по адресу `http://localhost:5005`.

**Тестирование через curl:**
```bash
curl -X POST "http://localhost:5005/webhooks/rest/webhook" \
  -H "Content-Type: application/json" \
  -d '{"sender": "test_user", "message": "привет"}'
```

**Тестирование через Python:**
```python
import requests

response = requests.post(
    "http://localhost:5005/webhooks/rest/webhook",
    json={"sender": "test_user", "message": "привет"}
)
print(response.json())
```

### Основные endpoints

- `POST /webhooks/rest/webhook` - отправка сообщений боту
- `GET /model` - информация о модели
- `POST /model/train` - переобучение модели
- `GET /conversations/{sender_id}/tracker` - получение состояния диалога

### Мониторинг и отладка

**Просмотр логов:**
```bash
# Все сервисы
docker-compose -f docker-compose.simple.yml logs -f

# Конкретный сервис
docker-compose -f docker-compose.simple.yml logs -f rasa-server
```

**Состояние контейнеров:**
```bash
docker-compose -f docker-compose.simple.yml ps
```

## Конфигурация

### Изменение поведения бота

1. **Добавление новых интентов**: Отредактируйте `rasa_bot/data/nlu.yml`
2. **Изменение ответов**: Обновите `rasa_bot/data/responses.yml`
3. **Создание диалоговых сценариев**: Добавьте истории в `rasa_bot/data/stories.yml`
4. **Кастомные действия**: Реализуйте в `rasa_bot/actions/actions.py`

### Переобучение модели

После изменения конфигурации переобучите модель:

```bash
# Перезапуск с переобучением
docker-compose -f docker-compose.simple.yml restart rasa-server

# Или ручное обучение внутри контейнера
docker exec -it fillinthetextbot-rasa rasa train --force
```

## Расширенные возможности

### Полная конфигурация с Rasa X

Для веб-интерфейса управления ботом используйте полную конфигурацию:

```bash
docker-compose up -d
```

Rasa X будет доступен по адресу `http://localhost:8080`.

### Интеграция с внешними сервисами

Для интеграции с Telegram, Slack или другими платформами, добавьте соответствующие каналы в `rasa_bot/credentials.yml`:

```yaml
telegram:
  access_token: "YOUR_ACCESS_TOKEN"
  verify: "YOUR_VERIFY_TOKEN"
  webhook_url: "YOUR_WEBHOOK_URL"
```

## Миграция обратно в Dialogflow

При необходимости вы можете экспортировать данные из Rasa обратно в формат Dialogflow, используя обратную конвертацию или API Dialogflow.

## Управление

### Запуск
```bash
docker-compose -f docker-compose.simple.yml up -d
```

### Остановка
```bash
docker-compose -f docker-compose.simple.yml down
```

### Перезапуск
```bash
docker-compose -f docker-compose.simple.yml restart
```

### Полная очистка
```bash
docker-compose -f docker-compose.simple.yml down -v
docker system prune -f
```

## Поддержка и отладка

### Частые проблемы

1. **Порты заняты**: Измените порты в docker-compose.yml
2. **Модель не обучается**: Проверьте формат данных в data/
3. **Действия не работают**: Убедитесь, что action server запущен

### Логи и диагностика

- Логи Rasa: `docker-compose logs rasa-server`
- Логи Action Server: `docker-compose logs rasa-actions`
- Состояние Redis: `docker-compose logs redis`

### Производительность

Для улучшения производительности:
- Увеличьте memory limit в docker-compose.yml
- Оптимизируйте pipeline в config.yml
- Используйте GPU для обучения (требует nvidia-docker)

## Лицензия

Этот проект использует открытые решения и предназначен для разработки и тестирования.