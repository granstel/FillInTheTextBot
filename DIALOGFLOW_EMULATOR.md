# Локальная отладка с Dialogflow Emulator

Этот документ описывает, как настроить и использовать собственный Dialogflow Emulator для локальной изолированной отладки проекта FillInTheTextBot.

## Что добавлено

1. **Собственный Dialogflow Emulator** на Node.js с HTTP API
2. **Docker Compose конфигурация** для запуска эмулятора
3. **HTTP клиенты** для интеграции с эмулятором (DialogflowEmulatorClient, DialogflowEmulatorContextsClient)
4. **Расширенная конфигурация** DialogflowConfiguration с поддержкой EmulatorEndpoint
5. **Локальные настройки** appsettings.Local.json для разработки
6. **Автоматическое переключение** между эмулятором и реальным Dialogflow

## Быстрый запуск

### 1. Запуск эмулятора

```bash
docker-compose up -d dialogflow-emulator
```

Эмулятор будет доступен по адресу http://localhost:3000

### 2. Запуск приложения с локальными настройками

```bash
cd src/FillInTheTextBot.Api
dotnet run --environment Local
```

Или в Visual Studio/Rider установите переменную окружения:
```
ASPNETCORE_ENVIRONMENT=Local
```

## Как это работает

### Архитектура эмулятора

Эмулятор состоит из:
- **Node.js сервера** (`dialogflow-emulator/server.js`) - HTTP API, совместимый с Dialogflow V2
- **HTTP клиентов** - DialogflowEmulatorClient и DialogflowEmulatorContextsClient для C#
- **Docker контейнера** - для изоляции и простого развертывания

### Docker Compose

Эмулятор собирается из исходников и использует агент из папки `Dialogflow/FillInTheTextBot-eu`:

```yaml
services:
  dialogflow-emulator:
    build:
      context: .
      dockerfile: dialogflow-emulator/Dockerfile
    ports:
      - "3000:3000"
    volumes:
      - ./Dialogflow/FillInTheTextBot-eu:/app/agent:ro
    environment:
      - PROJECT_ID=fillinthetextbot-vyyaxp
      - LANGUAGE_CODE=ru
```

### Конфигурация приложения

В `appsettings.Local.json` указан endpoint эмулятора:

```json
"Dialogflow": [
  {
    "ScopeId": "local-emulator",
    "ProjectId": "fillinthetextbot-vyyaxp",
    "JsonPath": "",
    "Region": "",
    "LogQuery": true,
    "DoNotUseForNewSessions": false,
    "EmulatorEndpoint": "localhost:3000"
  }
]
```

### Автоматическое переключение

Код автоматически определяет наличие `EmulatorEndpoint` и:
- Если указан - создается DialogflowEmulatorClient, который делает HTTP запросы к эмулятору
- Если не указан - создается стандартный SessionsClient для работы с Google Dialogflow

### Интеграция эмулятора

1. **DialogflowEmulatorClient** - наследует SessionsClient и преобразует gRPC вызовы в HTTP запросы
2. **DialogflowEmulatorContextsClient** - наследует ContextsClient для работы с контекстами
3. **Автоматический выбор** в ExternalServicesRegistration.cs based on EmulatorEndpoint

## Структура агента

Эмулятор использует файлы агента из папки `Dialogflow/FillInTheTextBot-eu/`:
- `agent.json` - основная конфигурация агента
- `intents/` - папка с интентами
- `entities/` - папка с сущностями

## Полезные команды

### Просмотр логов эмулятора
```bash
docker-compose logs -f dialogflow-emulator
```

### Перезапуск эмулятора
```bash
docker-compose restart dialogflow-emulator
```

### Остановка эмулятора
```bash
docker-compose down
```

### Проверка статуса
```bash
curl http://localhost:3000/health
```

### Отладочные endpoints
```bash
# Список всех интентов
curl http://localhost:3000/debug/intents

# Просмотр конкретного интента
curl http://localhost:3000/debug/intents/EasyWelcome
```

### Тестирование напрямую с эмулятором
```bash
# POST запрос для тестирования DetectIntent
curl -X POST http://localhost:3000/v2/projects/fillinthetextbot-vyyaxp/agent/sessions/test-session:detectIntent \
-H "Content-Type: application/json" \
-d '{
  "queryInput": {
    "text": {
      "text": "привет",
      "languageCode": "ru"
    }
  }
}'
```

## Отладка

1. В локальной конфигурации включено расширенное логирование (`LogQuery: true`)
2. Все запросы и ответы Dialogflow будут записываться в лог
3. Можно тестировать через Postman/curl напрямую с эмулятором

## Переключение между средами

Для работы с разными средами достаточно изменить переменную окружения:

- `ASPNETCORE_ENVIRONMENT=Local` - локальный эмулятор
- `ASPNETCORE_ENVIRONMENT=Development` - обычные настройки разработки
- `ASPNETCORE_ENVIRONMENT=Production` - продакшен

## Минимальные изменения кода

Как и требовалось, изменения в коде минимальны:
1. Добавлено свойство `EmulatorEndpoint` в `DialogflowConfiguration`
2. Расширена логика создания клиентов в `ExternalServicesRegistration`
3. Добавлен файл конфигурации `appsettings.Local.json`

Остальной код остается без изменений и продолжает работать как с эмулятором, так и с реальным Dialogflow.