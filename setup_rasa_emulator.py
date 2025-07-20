#!/usr/bin/env python3
"""
Скрипт для настройки локального эмулятора Dialogflow на базе Rasa
"""

import os
import subprocess
import sys


def run_command(command, cwd=None):
    """Выполнение команды в shell"""
    print(f"Выполняем команду: {command}")
    try:
        result = subprocess.run(command, shell=True, cwd=cwd, check=True, 
                              capture_output=True, text=True)
        print(f"Успешно выполнено: {result.stdout}")
        return True
    except subprocess.CalledProcessError as e:
        print(f"Ошибка выполнения команды: {e}")
        print(f"Вывод ошибки: {e.stderr}")
        return False


def main():
    """Основная функция настройки"""
    print("=== Настройка локального эмулятора Dialogflow на базе Rasa ===")
    
    # Путь к проекту
    project_root = os.path.dirname(os.path.abspath(__file__))
    dialogflow_path = os.path.join(project_root, "Dialogflow", "FillInTheTextBot-eu")
    rasa_output_path = os.path.join(project_root, "rasa_bot")
    
    print(f"Корневая директория проекта: {project_root}")
    print(f"Путь к Dialogflow агенту: {dialogflow_path}")
    print(f"Путь для Rasa бота: {rasa_output_path}")
    
    # Проверяем существование Dialogflow агента
    if not os.path.exists(dialogflow_path):
        print(f"ОШИБКА: Путь {dialogflow_path} не существует!")
        return False
    
    # Устанавливаем зависимости Python
    print("\n1. Устанавливаем зависимости Python...")
    if not run_command("pip install pyyaml"):
        print("Не удалось установить зависимости Python")
        return False
    
    # Конвертируем Dialogflow в Rasa
    print("\n2. Конвертируем Dialogflow агента в формат Rasa...")
    converter_script = os.path.join(project_root, "dialogflow_to_rasa_converter.py")
    if not run_command(f'python "{converter_script}" "{dialogflow_path}" "{rasa_output_path}"'):
        print("Не удалось конвертировать Dialogflow агента")
        return False
    
    # Проверяем установку Docker
    print("\n3. Проверяем установку Docker...")
    if not run_command("docker --version"):
        print("Docker не установлен! Пожалуйста, установите Docker Desktop.")
        return False
    
    if not run_command("docker-compose --version"):
        print("Docker Compose не установлен!")
        return False
    
    # Останавливаем возможные предыдущие контейнеры
    print("\n4. Останавливаем предыдущие контейнеры...")
    run_command("docker-compose -f docker-compose.simple.yml down", cwd=project_root)
    
    # Запускаем Docker Compose
    print("\n5. Запускаем контейнеры Rasa...")
    if not run_command("docker-compose -f docker-compose.simple.yml up -d", cwd=project_root):
        print("Не удалось запустить контейнеры")
        return False
    
    print("\n=== Настройка завершена! ===")
    print("\nТеперь вы можете:")
    print("1. Обращаться к Rasa API по адресу: http://localhost:5005")
    print("2. Тестировать бота через curl:")
    print('   curl -X POST "http://localhost:5005/webhooks/rest/webhook" -H "Content-Type: application/json" -d \'{"sender": "test", "message": "привет"}\'')
    print("3. Просматривать логи контейнеров:")
    print("   docker-compose -f docker-compose.simple.yml logs -f")
    print("4. Остановить контейнеры:")
    print("   docker-compose -f docker-compose.simple.yml down")
    
    return True


if __name__ == "__main__":
    if main():
        sys.exit(0)
    else:
        sys.exit(1)