FROM python:3.9-slim

# Устанавливаем зависимости
RUN pip install --no-cache-dir pyyaml

# Создаем рабочую директорию
WORKDIR /app

# Копируем скрипт конвертации
COPY dialogflow_to_rasa_converter.py /app/

# Создаем директорию для базовых файлов
RUN mkdir -p /app/base_files

# Устанавливаем права на выполнение
RUN chmod +x /app/dialogflow_to_rasa_converter.py

# Команда по умолчанию
ENTRYPOINT ["python", "/app/dialogflow_to_rasa_converter.py"]