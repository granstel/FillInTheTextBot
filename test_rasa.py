#!/usr/bin/env python3
"""
Скрипт для тестирования Rasa эмулятора
"""

import requests
import json
import time
import sys


def test_rasa_connection():
    """Тестирование подключения к Rasa"""
    print("🔍 Тестируем подключение к Rasa...")
    
    try:
        response = requests.get("http://localhost:5005/status", timeout=5)
        if response.status_code == 200:
            print("✅ Rasa сервер доступен")
            return True
        else:
            print(f"❌ Rasa сервер недоступен (код: {response.status_code})")
            return False
    except requests.exceptions.RequestException as e:
        print(f"❌ Ошибка подключения: {e}")
        return False


def send_message(message, sender_id="test_user"):
    """Отправка сообщения в Rasa"""
    url = "http://localhost:5005/webhooks/rest/webhook"
    payload = {
        "sender": sender_id,
        "message": message
    }
    
    try:
        response = requests.post(url, json=payload, timeout=10)
        if response.status_code == 200:
            return response.json()
        else:
            print(f"❌ Ошибка отправки сообщения (код: {response.status_code})")
            return None
    except requests.exceptions.RequestException as e:
        print(f"❌ Ошибка сети: {e}")
        return None


def run_tests():
    """Запуск тестов"""
    print("=" * 60)
    print("🤖 Тестирование FillInTheTextBot Rasa Emulator")
    print("=" * 60)
    
    # Тест подключения
    if not test_rasa_connection():
        print("\n❌ Тесты не могут быть выполнены - Rasa недоступен")
        print("Убедитесь что контейнеры запущены: docker-compose -f docker-compose.simple.yml up")
        return False
    
    # Тестовые сообщения
    test_messages = [
        "привет",
        "список текстов", 
        "помощь",
        "кто ты",
        "выход",
        "спасибо"
    ]
    
    print(f"\n📝 Тестируем {len(test_messages)} сообщений...\n")
    
    for i, message in enumerate(test_messages, 1):
        print(f"[{i}/{len(test_messages)}] Отправляем: '{message}'")
        
        responses = send_message(message)
        
        if responses:
            print("✅ Получены ответы:")
            for response in responses:
                if 'text' in response:
                    # Обрезаем длинные ответы для красивого вывода
                    text = response['text']
                    if len(text) > 100:
                        text = text[:97] + "..."
                    print(f"   🤖 {text}")
                if 'buttons' in response:
                    print(f"   🔘 Кнопки: {[btn['title'] for btn in response['buttons']]}")
        else:
            print("❌ Нет ответа от бота")
        
        print("-" * 40)
        time.sleep(1)  # Небольшая пауза между запросами
    
    print("✅ Тестирование завершено!")
    return True


def interactive_mode():
    """Интерактивный режим общения с ботом"""
    print("\n🎯 Интерактивный режим (введите 'выход' для завершения)")
    print("-" * 40)
    
    sender_id = f"interactive_user_{int(time.time())}"
    
    while True:
        try:
            message = input("\n👤 Вы: ").strip()
            
            if not message:
                continue
                
            if message.lower() in ['выход', 'exit', 'quit', 'q']:
                print("👋 До свидания!")
                break
            
            responses = send_message(message, sender_id)
            
            if responses:
                for response in responses:
                    if 'text' in response:
                        print(f"🤖 Бот: {response['text']}")
                    if 'buttons' in response:
                        buttons = [btn['title'] for btn in response['buttons']]
                        print(f"🔘 Варианты: {', '.join(buttons)}")
            else:
                print("❌ Бот не ответил")
                
        except KeyboardInterrupt:
            print("\n👋 До свидания!")
            break
        except Exception as e:
            print(f"❌ Ошибка: {e}")


def main():
    """Главная функция"""
    if len(sys.argv) > 1 and sys.argv[1] == "--interactive":
        if test_rasa_connection():
            interactive_mode()
        else:
            print("❌ Не удается подключиться к Rasa для интерактивного режима")
            return 1
    else:
        if run_tests():
            print("\n🎉 Все тесты прошли успешно!")
            print("💡 Для интерактивного режима используйте: python test_rasa.py --interactive")
            return 0
        else:
            return 1


if __name__ == "__main__":
    exit(main())