# 🎉 FillInTheTextBot - Локальный эмулятор готов!

Ваш локальный эмулятор Dialogflow на базе Rasa успешно настроен и готов к использованию!

## ✅ Что работает

- 🔄 **Автоматическая конвертация** - Dialogflow агент автоматически конвертируется в формат Rasa при запуске
- 🤖 **Rasa NLU** - Распознавание интентов и сущностей
- 🎯 **Action Server** - Кастомные действия для бота
- 💾 **Redis** - Хранение сессий диалогов
- 🌐 **REST API** - Полноценный API для интеграции
- 🖥️ **Веб-интерфейс** - Удобный интерфейс для тестирования

## 🚀 Быстрый запуск

### Запуск эмулятора
```bash
# Простой запуск через bat-файл
start_emulator.bat

# Или через Docker Compose
docker-compose -f docker-compose.simple.yml up -d
```

### Остановка эмулятора  
```bash
# Остановка через bat-файл
stop_emulator.bat

# Или через Docker Compose
docker-compose -f docker-compose.simple.yml down
```

## 🧪 Тестирование

### Веб-интерфейс
1. Откройте `test_interface.html` в браузере
2. Убедитесь что статус показывает "🟢 Подключено к Rasa серверу"
3. Тестируйте бота через удобный интерфейс

### API тестирование
```bash
# Проверка статуса
curl -X GET "http://localhost:5005/status"

# Отправка сообщения (через PowerShell)
Invoke-RestMethod -Uri "http://localhost:5005/webhooks/rest/webhook" -Method POST -ContentType "application/json" -Body '{"sender": "test_user", "message": "привет"}'
```

### Python тестирование
```bash
python test_rasa.py
```

## 📊 Состояние конвертации

✅ **Интентов загружено:** 105  
✅ **Сущностей загружено:** 4  
✅ **Конвертация:** Успешно завершена  
✅ **Обучение модели:** Завершено  
✅ **Сервер:** Запущен и работает  

## 🔧 Архитектура

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Dialogflow    │───▶│   Конвертер     │───▶│   Rasa Bot      │
│   (исходный)    │    │   (Docker)      │    │   (обученный)   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                       │
                                                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Web UI        │◀───│   REST API      │◀───│   Rasa Server   │
│   (тестовый)    │    │   :5005         │    │   + Actions     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                                       │
                                                       ▼
                                               ┌─────────────────┐
                                               │     Redis       │
                                               │   (сессии)      │
                                               └─────────────────┘
```

## 📁 Структура проекта

```
FillInTheTextBot/
├── 🔧 start_emulator.bat          # Запуск эмулятора
├── 🔧 stop_emulator.bat           # Остановка эмулятора
├── 🌐 test_interface.html         # Веб-интерфейс для тестов
├── 🐍 test_rasa.py               # Python скрипт тестирования
├── 📋 docker-compose.simple.yml   # Docker Compose конфигурация
├── 📖 README_RASA_EMULATOR.md     # Подробная документация
│
├── Dialogflow/                    # Исходные данные
│   └── FillInTheTextBot-eu/       # Экспорт Dialogflow агента
│
├── converter/                     # Конвертер Dialogflow → Rasa
│   ├── Dockerfile                 # Docker образ конвертера
│   └── dialogflow_to_rasa_converter.py
│
├── rasa_base_files/              # Базовые файлы для Rasa
│   ├── endpoints.yml
│   └── actions/
│       ├── __init__.py
│       └── actions.py
│
└── rasa_bot/                     # Готовый Rasa бот (автоматически)
    ├── config.yml                # Конфигурация pipeline
    ├── domain.yml                # Домен бота  
    ├── endpoints.yml             # Настройки подключений
    ├── data/
    │   ├── nlu.yml              # NLU данные
    │   └── stories.yml          # Диалоговые сценарии
    └── actions/
        ├── __init__.py
        └── actions.py           # Кастомные действия
```

## 🎯 Возможности

### Интенты из вашего Dialogflow агента:
- Приветствие и базовые диалоги
- Запрос списка текстов
- Интенты для различных историй и гороскопов
- Обработка пользовательского ввода

### Кастомные действия:
- `action_welcome` - Приветствие пользователя
- `action_texts_list` - Отображение списка доступных текстов
- `action_hello_world` - Тестовое действие

## 🔗 API Endpoints

- `GET /status` - Статус сервера
- `POST /webhooks/rest/webhook` - Отправка сообщений
- `GET /model` - Информация о модели
- `GET /conversations/{sender_id}/tracker` - Состояние диалога

## 🔍 Мониторинг

```bash
# Просмотр логов
docker-compose -f docker-compose.simple.yml logs -f

# Логи конкретного сервиса
docker logs fillinthetextbot-rasa -f
docker logs fillinthetextbot-actions -f

# Статус контейнеров
docker-compose -f docker-compose.simple.yml ps
```

## 🛠️ Разработка

Для модификации бота:
1. Измените файлы в `rasa_bot/`
2. Перезапустите контейнеры:
   ```bash
   docker-compose -f docker-compose.simple.yml restart rasa-server
   ```

## 🎊 Поздравляем!

Ваш локальный эмулятор Dialogflow готов! 

- 🌐 **Тестируйте** через веб-интерфейс
- 🔧 **Интегрируйте** через REST API  
- 🚀 **Разрабатывайте** без зависимости от внешних сервисов
- 💰 **Экономьте** на API вызовах к Google Dialogflow

---
*Создано автоматически системой конвертации Dialogflow → Rasa*