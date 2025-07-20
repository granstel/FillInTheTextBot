# Руководство по интеграции Rasa с существующим кодом

Данное руководство описывает, как переключаться между Dialogflow и Rasa с минимальными изменениями в существующем коде.

## Архитектура интеграции

Интеграция реализована с использованием паттерна **Adapter** и **Proxy**, что обеспечивает:

- ✅ **Полную обратную совместимость** - существующий код не требует изменений
- ✅ **Легкое переключение** между провайдерами через конфигурацию
- ✅ **Единый интерфейс** для работы с NLU
- ✅ **Изоляцию изменений** - новые возможности не влияют на старый код

## Компоненты интеграции

### 1. Новые интерфейсы
- `INluService` - общий интерфейс для всех NLU провайдеров
- `IDialogflowService` - расширен для наследования от `INluService`

### 2. Новые сервисы
- `RasaService` - адаптер для работы с Rasa API
- `NluServiceProxy` - прокси, автоматически выбирающий провайдера
- `NluServiceFactory` - фабрика для создания нужного сервиса

### 3. Конфигурация
- `NluConfiguration` - настройка выбора провайдера
- `RasaConfiguration` - специфичные настройки для Rasa
- Поддержка environment-specific конфигураций

## Способы переключения провайдеров

### 1. Через appsettings.json (рекомендуется)

```json
{
  "AppConfiguration": {
    "Nlu": {
      "Provider": "Rasa"  // или "Dialogflow"
    },
    "Rasa": [
      {
        "ScopeId": "default",
        "BaseUrl": "http://localhost:5005",
        "LanguageCode": "ru",
        "LogQuery": true
      }
    ]
  }
}
```

### 2. Через переменные окружения

```bash
# Для использования Rasa
set ASPNETCORE_ENVIRONMENT=Rasa

# Для возврата к Dialogflow
set ASPNETCORE_ENVIRONMENT=
```

### 3. Через bat-файлы (Windows)

```bash
# Переключение на Rasa
switch_to_rasa.bat

# Переключение на Dialogflow
switch_to_dialogflow.bat
```

### 4. Программно в Startup.cs

```csharp
// Принудительное использование Rasa
services.UseRasaAsNluProvider("http://localhost:5005");

// Принудительное использование Dialogflow
services.UseDialogflowAsNluProvider();
```

## Локальная отладка с Rasa

### Быстрый старт

1. **Запуск Rasa эмулятора:**
   ```bash
   start_emulator.bat
   ```

2. **Переключение на Rasa:**
   ```bash
   switch_to_rasa.bat
   ```

### Проверка работы

```bash
# Тест API
curl -X POST "http://localhost:5000/api/conversation" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "привет",
    "sessionId": "test123",
    "source": "Yandex"
  }'
```

## Миграция данных между провайдерами

### Из Dialogflow в Rasa
Используйте существующий конвертер:
```bash
python dialogflow_to_rasa_converter.py
```

### Обратная миграция
При необходимости вернуться к Dialogflow:
1. Экспортируйте обновленные данные из Rasa
2. Импортируйте их в Dialogflow через консоль

## Мониторинг и отладка

### Логирование
- Включите `LogQuery: true` в конфигурации для детального логирования
- Логи Rasa: `docker-compose logs rasa-server`
- Логи приложения покажут используемый провайдер

### Метрики
- `dialogflow_DetectIntent_scope` - для Dialogflow
- `rasa_webhook_scope` - для Rasa
- Метрики интентов остаются едиными

## Расширение функциональности

### Добавление нового NLU провайдера

1. Создайте новый сервис, реализующий `INluService`
2. Добавьте его в `NluServiceFactory`
3. Обновите enum `NluProvider`
4. Зарегистрируйте в DI

Пример:
```csharp
public class CustomNluService : INluService
{
    // Реализация интерфейса
}

// В фабрике
NluProvider.Custom => _serviceProvider.GetRequiredService<CustomNluService>()
```

## Устранение проблем

### Распространенные ошибки

1. **Rasa не отвечает**
   - Проверьте, что контейнер запущен: `docker-compose ps`
   - Проверьте доступность: `curl http://localhost:5005/status`

2. **Неправильный провайдер используется**
   - Проверьте конфигурацию в appsettings.json
   - Проверьте переменные окружения
   - Перезапустите приложение

3. **Ошибки маппинга**
   - Убедитесь, что Rasa возвращает ожидаемый формат
   - Проверьте настройки custom actions в Rasa

### Откат изменений

Если нужно полностью убрать Rasa интеграцию:

1. Верните в `InternalServicesRegistration.cs`:
   ```csharp
   services.AddScoped<IDialogflowService, DialogflowService>();
   ```

2. Удалите файлы:
   - `RasaService.cs`
   - `NluServiceProxy.cs` 
   - `NluServiceFactory.cs`
   - Папку `Rasa/`

3. Уберите из Startup.cs:
   ```csharp
   services.ConfigureNluProvider(_configuration.GetSection("AppConfiguration"));
   ```

## Производительность

- **Rasa**: Быстрее на локальных тестах, не требует сетевых запросов к Google
- **Dialogflow**: Медленнее из-за API вызовов, но более стабильный в продакшене
- **Переключение**: Практически без накладных расходов благодаря DI

## Лицензирование

- Rasa: Apache 2.0 (открытый исходный код)
- Dialogflow: Проприетарный (Google Cloud)
- Интеграция: MIT (часть проекта)