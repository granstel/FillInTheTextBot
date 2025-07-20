FROM node:18-alpine

WORKDIR /app

# Устанавливаем необходимые пакеты
RUN apk add --no-cache bash

# Копируем package.json для установки зависимостей
COPY dialogflow-emulator/package*.json ./

# Устанавливаем зависимости
RUN npm install

# Копируем исходный код
COPY dialogflow-emulator/ ./

# Создаем папку для агента
RUN mkdir -p /app/agent

# Открываем порт
EXPOSE 3000

# Запускаем сервер
CMD ["node", "server.js"]