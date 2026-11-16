# Локальный StoryDialog через вебхук голосового помощника

Нужны Docker с Linux containers и Compose v2.17+ с `additional_contexts`.
Checkout `StoryDialog` по умолчанию находится рядом с `FillInTheTextBot`.
Команды выполняются из корня FillInTheTextBot; Go, .NET и Python на хосте не нужны.

Обучающие фразы настраиваются в StoryDialog (`nlu/train.example.json`).
После обучения задайте `STORYDIALOG_UNDERSTANDING_FILE=/app/nlu/model-encoder.json`
и `STORYDIALOG_ENCODER_ENDPOINT=http://encoder:8080` в окружении Compose,
затем поднимите стенд с `--profile learned`. Профиль применяется к новым разговорам;
без настройки сохраняются встроенные правила. Команда подготовки описана в
`StoryDialog/docs/understanding.md`.

При включённом обученном профиле проверка новых формулировок:
`docker compose -f local/storydialog/compose.yaml --profile checks run --build --rm --no-deps smoke learned`.
Обычный `smoke` проверяет режим встроенных правил.

```sh
docker compose -f local/storydialog/compose.yaml up --build -d
docker compose -f local/storydialog/compose.yaml --profile checks run --build --rm smoke
```

Первый запуск собирает образы и скачивает зависимости. Бот, StoryDialog, MongoDB,
Redis и эмулятор находятся только во внутренней Docker-сети без выхода к Google
и GitHub. Ротация выключена. Отдельный nginx gateway подключён к этой сети и
входной bridge-сети; он направляет HTTP только на `fitb:80`.
Проект `fitb-storydialog-local`, сеть и Mongo volume отдельные; существующий стенд
StoryDialog не используется. MongoDB, Redis и gRPC не публикуют порты.
Gateway публикует вебхук голосового помощника на `http://127.0.0.1:18082/yandex`, без реальных
токенов. Журналы запросов gateway отключены, тела запросов и ответов в приложении
не журналируются; вводите только синтетические данные.

Ручной диалог через тот же HTTP endpoint, с переносом `session_state`:

```sh
docker compose -f local/storydialog/compose.yaml --profile checks run --build --rm --no-deps smoke chat
```

Команды и ответы передаются в StoryDialog исходным текстом; распознавание не
использует NLU голосового помощника. В начале сессии адаптер выбирает историю по умолчанию
или по явному `/story ID`. Во время истории выбор следующей истории,
команды и уточнения выполняет StoryDialog.

Smoke обращается к основному боту через gateway и проверяет простую и многочастную
историю, команды, числа, перенос ответов,
повтор одного HTTP сообщения, конфликт содержимого, устаревшую версию, `ping`,
сохранение legacy state и ограничения формата ответа. Дополнительные экземпляры бота
профиля `checks` проверяют недоступный backend и смену провайдера активной сессии.
Счётчик вызовов Dialogflow у StoryDialog-экземпляров должен оставаться неизменным.
Экспорт публичного proto сравнивается с исходником владельца при сборке и запуске.

## Переключение провайдера

По умолчанию только этот стенд использует StoryDialog. Общий default приложения —
Dialogflow. Чтобы проверить старый провайдер, создайте `local/storydialog/.env`
с одной строкой:

```dotenv
CONVERSATION_PROVIDER=Dialogflow
```

```sh
docker compose --env-file local/storydialog/.env -f local/storydialog/compose.yaml up --build -d
docker compose --env-file local/storydialog/.env -f local/storydialog/compose.yaml --profile checks run --build --rm smoke emulator
```

Dialogflow направлен исключительно в локальный эмулятор с синтетическими интентами.
Ответ `LOCAL_DIALOGFLOW_EMULATOR` подтверждает этот маршрут. Для полного smoke
верните `CONVERSATION_PROVIDER=StoryDialog` и повторите запуск.
Смена настройки требует новой сессии: активная сессия не переносится между движками.

Локальный адаптер StoryDialog поддерживает до четырёх историй с ID до 64 ASCII-символов.
При превышении лимита длины ответа голосового помощника он сообщает об ограничении,
а не обрезает историю. Кампании и ротация Dialogflow в этом режиме не исполняются;
сохранённые legacy-индексы и состояние ротации не изменяются.

## Тесты адаптера

```sh
docker compose -f local/storydialog/compose.yaml --profile unit run --build --rm unit
```

Запускаются unit-тесты и несетевые проверки регистрации провайдеров. Полные проверки
HTTP/gRPC/MongoDB выполняет `smoke` из инструкции выше.

Если соседний checkout расположен иначе, в том же `.env` задайте
`STORYDIALOG_SOURCE` относительно `local/storydialog`, например
`STORYDIALOG_SOURCE=../../../StoryDialog`. Конфигурация провайдера передаётся через
`AppConfiguration__Conversation__Provider`; настройки gRPC — через
`AppConfiguration__StoryDialog__Endpoint`, `ApiKey`, `DefaultStoryId` и
`TimeoutMilliseconds`. В этом стенде API-ключ пустой.

## Остановка

```sh
docker compose -f local/storydialog/compose.yaml --profile checks down
```

Данные локального StoryDialog сохраняются. Для удаления именно данных этого
стенда выполните ту же команду с `--volumes`. Redis здесь временный.
