# This files contains your custom actions which can be used to run
# custom Python code.
#
# See this guide on how to implement these action:
# https://rasa.com/docs/rasa/custom-actions

from typing import Any, Text, Dict, List
from rasa_sdk import Action, Tracker
from rasa_sdk.executor import CollectingDispatcher


class ActionWelcome(Action):
    """Действие приветствия"""

    def name(self) -> Text:
        return "action_welcome"

    def run(self, dispatcher: CollectingDispatcher,
            tracker: Tracker,
            domain: Dict[Text, Any]) -> List[Dict[Text, Any]]:

        welcome_text = ("Добро пожаловать! Давай вместе сочиним занимательные истории! "
                       "Я буду просить называть слова, а ты отвечай первое, что придёт в голову. "
                       "Потом я прочитаю историю, которая у нас получилась. "
                       "Если что-то непонятно, или нужен список текстов — спрашивай. "
                       "Захочешь выйти — так и скажи. Начнём?")

        dispatcher.utter_message(text=welcome_text)
        return []


class ActionTextsList(Action):
    """Действие для отображения списка текстов"""

    def name(self) -> Text:
        return "action_texts_list"

    def run(self, dispatcher: CollectingDispatcher,
            tracker: Tracker,
            domain: Dict[Text, Any]) -> List[Dict[Text, Any]]:

        texts_list = [
            "1. Карантин",
            "2. Парк развлечений", 
            "3. Сложная рыба",
            "4. Больница",
            "5. Цирк",
            "6. Объяснительная",
            "7. Новая булочка",
            "8-23. Гороскопы для всех знаков зодиака",
            "24. 23 февраля",
            "25. 8 марта",
            "26. Борис кот и шоколад",
            "27. Почему мы пропустили",
            "28. Король пиратов", 
            "29. Опоздание",
            "30. Дерево горит",
            "31. Старые часы",
            "32. Летние каникулы"
        ]

        response = "Доступные тексты:\n" + "\n".join(texts_list)
        dispatcher.utter_message(text=response)
        return []


class ActionHelloWorld(Action):
    """Тестовое действие"""

    def name(self) -> Text:
        return "action_hello_world"

    def run(self, dispatcher: CollectingDispatcher,
            tracker: Tracker,
            domain: Dict[Text, Any]) -> List[Dict[Text, Any]]:

        dispatcher.utter_message(text="Hello World!")
        return []