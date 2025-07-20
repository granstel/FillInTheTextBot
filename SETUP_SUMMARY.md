# 🎭 FillInTheTextBot Dialogflow Emulator - Сводка настройки

## ✅ Что создано

### 1. Собственный Dialogflow Emulator
- **Node.js сервер** в `dialogflow-emulator/server.js`
- **HTTP API**, совместимый с Dialogflow V2
- **Автоматическая загрузка** интентов из файлов агента
- **105 интентов** успешно загружено из `Dialogflow/FillInTheTextBot-eu`

### 2. Docker интеграция
- **Dockerfile** для сборки эмулятора
- **docker-compose.yml** для запуска
- **Автоматическое монтирование** папки с агентом

### 3. C# HTTP клиенты
- **DialogflowEmulatorClient** - реализует SessionsClient
- **DialogflowEmulatorContextsClient** - реализует ContextsClient
- **Преобразование** gRPC вызовов в HTTP запросы

### 4. Интеграция с проектом
- **Расширенная DialogflowConfiguration** с EmulatorEndpoint
- **Автоматическое переключение** между эмулятором и Google Dialogflow
- **Минимальные изменения** существующего кода

### 5. Конфигурация
- **appsettings.Local.json** для локальной разработки
- **Скрипт start-local-dev.ps1** для быстрого запуска

## 🚀 Быстрый запуск

1. Запустите эмулятор:
   ```bash
   ./start-local-dev.ps1
   # или
   docker-compose up -d dialogflow-emulator
   ```

2. Запустите приложение:
   ```bash
   cd src/FillInTheTextBot.Api
   dotnet run --environment Local
   ```

## 📁 Структура файлов

```
FillInTheTextBot/
├── dialogflow-emulator/
│   ├── Dockerfile
│   ├── package.json
│   └── server.js
├── docker-compose.yml
├── src/
│   ├── FillInTheTextBot.Api/
│   │   └── appsettings.Local.json
│   └── FillInTheTextBot.Services/
│       ├── Configuration/
│       │   └── DialogflowConfiguration.cs (+ EmulatorEndpoint)
│       ├── DialogflowEmulatorClient.cs
│       └── DialogflowEmulatorContextsClient.cs
├── start-local-dev.ps1
├── DIALOGFLOW_EMULATOR.md
└── SETUP_SUMMARY.md
```

## ✨ Особенности решения

### Минимальные изменения
- Добавлено только 1 новое свойство: `EmulatorEndpoint`
- Новые клиенты наследуют от стандартных Google Cloud клиентов
- Логика переключения прозрачная для остального кода

### Совместимость
- ✅ Работает с существующими интентами и событиями
- ✅ Поддерживает русский язык
- ✅ Совместим с текущей архитектурой проекта
- ✅ Логирование и метрики работают как обычно

### Отладочные возможности
- HTTP endpoints для отладки (`/health`, `/debug/intents`)
- Подробное логирование запросов и ответов
- Возможность тестирования через curl/Postman

## 🧪 Проверка работы

1. **Health check**:
   ```bash
   curl http://localhost:3000/health
   ```

2. **Тест DetectIntent**:
   ```bash
   curl -X POST http://localhost:3000/v2/projects/fillinthetextbot-vyyaxp/agent/sessions/test:detectIntent \
   -H "Content-Type: application/json" \
   -d '{"queryInput":{"event":{"name":"WELCOME","languageCode":"ru"}}}'
   ```

3. **Список интентов**:
   ```bash
   curl http://localhost:3000/debug/intents
   ```

## 🎯 Результат

- ✅ **Нет готового образа matthew-trump/dialogflow-emulator** - проблема решена созданием собственного
- ✅ **Эмулятор работает** с реальными интентами проекта
- ✅ **Минимальные изменения** кода, как требовалось
- ✅ **Локальная изолированная отладка** полностью функциональна
- ✅ **105 интентов загружено** и готово к использованию

Теперь вы можете полноценно отлаживать проект локально без подключения к Google Dialogflow! 🎉