#!/usr/bin/env python3
"""
Скрипт для тестирования интеграции между Dialogflow и Rasa
"""

import requests
import json
import time
from typing import Dict, Any

class IntegrationTester:
    def __init__(self, api_base_url: str = "http://localhost:5000"):
        self.api_base_url = api_base_url
        self.test_messages = [
            "привет",
            "помоги мне",
            "что ты умеешь",
            "расскажи историю",
            "пока"
        ]
    
    def test_api_endpoint(self, message: str, session_id: str = "test123") -> Dict[str, Any]:
        """Тестирует API endpoint приложения"""
        url = f"{self.api_base_url}/api/conversation"  # Предполагаемый endpoint
        
        payload = {
            "text": message,
            "sessionId": session_id,
            "source": "Yandex",
            "scopeKey": "default"
        }
        
        try:
            response = requests.post(url, json=payload, timeout=10)
            response.raise_for_status()
            return {
                "success": True,
                "status_code": response.status_code,
                "response": response.json(),
                "message": message
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "message": message
            }
    
    def test_rasa_direct(self, message: str, session_id: str = "test123") -> Dict[str, Any]:
        """Тестирует Rasa напрямую"""
        url = "http://localhost:5005/webhooks/rest/webhook"
        
        payload = {
            "sender": session_id,
            "message": message
        }
        
        try:
            response = requests.post(url, json=payload, timeout=10)
            response.raise_for_status()
            return {
                "success": True,
                "status_code": response.status_code,
                "response": response.json(),
                "message": message
            }
        except Exception as e:
            return {
                "success": False,
                "error": str(e),
                "message": message
            }
    
    def check_rasa_status(self) -> bool:
        """Проверяет доступность Rasa"""
        try:
            response = requests.get("http://localhost:5005/status", timeout=5)
            return response.status_code == 200
        except:
            return False
    
    def check_api_status(self) -> bool:
        """Проверяет доступность API приложения"""
        try:
            # Попробуем использовать health check endpoint или любой GET endpoint
            response = requests.get(f"{self.api_base_url}/health", timeout=5)
            return response.status_code in [200, 404]  # 404 тоже означает что сервер доступен
        except:
            return False
    
    def run_integration_test(self):
        """Запускает полный тест интеграции"""
        print("🧪 Запуск тестов интеграции...")
        print("=" * 50)
        
        # Проверка доступности сервисов
        print("🔍 Проверка доступности сервисов...")
        rasa_available = self.check_rasa_status()
        api_available = self.check_api_status()
        
        print(f"   Rasa: {'✅ Доступен' if rasa_available else '❌ Недоступен'}")
        print(f"   API:  {'✅ Доступен' if api_available else '❌ Недоступен'}")
        print()
        
        if not rasa_available:
            print("⚠️  Rasa недоступен. Запустите эмулятор командой: start_emulator.bat")
            return
        
        if not api_available:
            print("⚠️  API недоступен. Запустите приложение.")
            return
        
        # Тест Rasa напрямую
        print("🎯 Тестирование Rasa напрямую...")
        for message in self.test_messages:
            result = self.test_rasa_direct(message)
            status = "✅" if result["success"] else "❌"
            print(f"   {status} '{message}' -> {self.format_response(result)}")
        
        print()
        
        # Тест через API приложения
        print("🎯 Тестирование через API приложения...")
        for message in self.test_messages:
            result = self.test_api_endpoint(message)
            status = "✅" if result["success"] else "❌"
            print(f"   {status} '{message}' -> {self.format_response(result)}")
        
        print()
        print("✨ Тестирование завершено!")
    
    def format_response(self, result: Dict[str, Any]) -> str:
        """Форматирует ответ для вывода"""
        if not result["success"]:
            return f"Ошибка: {result['error']}"
        
        response = result["response"]
        if isinstance(response, list) and response:
            # Rasa format
            return response[0].get("text", "Нет текста")
        elif isinstance(response, dict):
            # API format
            return response.get("response", response.get("text", "Нет ответа"))
        else:
            return str(response)
    
    def performance_test(self, iterations: int = 10):
        """Тестирует производительность"""
        print(f"⚡ Тест производительности ({iterations} запросов)...")
        
        message = "привет"
        
        # Тест Rasa
        start_time = time.time()
        for i in range(iterations):
            self.test_rasa_direct(message, f"perf_test_{i}")
        rasa_time = time.time() - start_time
        
        # Тест API
        start_time = time.time()
        for i in range(iterations):
            self.test_api_endpoint(message, f"perf_test_api_{i}")
        api_time = time.time() - start_time
        
        print(f"   Rasa напрямую: {rasa_time:.2f}с ({rasa_time/iterations*1000:.1f}мс на запрос)")
        print(f"   Через API:     {api_time:.2f}с ({api_time/iterations*1000:.1f}мс на запрос)")
        print(f"   Накладные расходы API: {((api_time - rasa_time) / rasa_time * 100):.1f}%")

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description="Тестирование интеграции Rasa")
    parser.add_argument("--url", default="http://localhost:5000", help="Base URL API")
    parser.add_argument("--performance", action="store_true", help="Запустить тест производительности")
    parser.add_argument("--iterations", type=int, default=10, help="Количество итераций для теста производительности")
    
    args = parser.parse_args()
    
    tester = IntegrationTester(args.url)
    
    if args.performance:
        tester.performance_test(args.iterations)
    else:
        tester.run_integration_test()

if __name__ == "__main__":
    main()