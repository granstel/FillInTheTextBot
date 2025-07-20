#!/usr/bin/env python3
"""
Скрипт для конвертации Dialogflow агента в формат Rasa
"""

import json
import os
import yaml
from typing import Dict, List, Any
import sys


class DialogflowToRasaConverter:
    def __init__(self, dialogflow_path: str, output_path: str):
        self.dialogflow_path = dialogflow_path
        self.output_path = output_path
        self.intents_data = []
        self.entities_data = {}
        self.stories_data = []
        
    def load_dialogflow_data(self):
        """Загрузка данных из Dialogflow экспорта"""
        print(f"Загрузка данных из {self.dialogflow_path}")
        
        # Загрузка интентов
        intents_path = os.path.join(self.dialogflow_path, 'intents')
        if os.path.exists(intents_path):
            for intent_file in os.listdir(intents_path):
                if intent_file.endswith('.json') and not intent_file.endswith('_usersays_ru.json'):
                    intent_path = os.path.join(intents_path, intent_file)
                    usersays_path = os.path.join(intents_path, intent_file.replace('.json', '_usersays_ru.json'))
                    
                    try:
                        with open(intent_path, 'r', encoding='utf-8') as f:
                            intent_data = json.load(f)
                        
                        # Загрузка пользовательских фраз если есть
                        user_examples = []
                        if os.path.exists(usersays_path):
                            with open(usersays_path, 'r', encoding='utf-8') as f:
                                usersays_data = json.load(f)
                                for example in usersays_data:
                                    if 'data' in example:
                                        text_parts = []
                                        for part in example['data']:
                                            text_parts.append(part.get('text', ''))
                                        user_examples.append(''.join(text_parts))
                        
                        intent_data['user_examples'] = user_examples
                        self.intents_data.append(intent_data)
                        print(f"Загружен интент: {intent_data.get('name', 'unknown')}")
                    except Exception as e:
                        print(f"Ошибка загрузки интента {intent_file}: {e}")
        
        # Загрузка сущностей
        entities_path = os.path.join(self.dialogflow_path, 'entities')
        if os.path.exists(entities_path):
            for entity_file in os.listdir(entities_path):
                if entity_file.endswith('.json') and not entity_file.endswith('_entries_ru.json'):
                    entity_path = os.path.join(entities_path, entity_file)
                    entries_path = os.path.join(entities_path, entity_file.replace('.json', '_entries_ru.json'))
                    
                    try:
                        with open(entity_path, 'r', encoding='utf-8') as f:
                            entity_data = json.load(f)
                        
                        # Загрузка значений сущности
                        entity_values = []
                        if os.path.exists(entries_path):
                            with open(entries_path, 'r', encoding='utf-8') as f:
                                entries_data = json.load(f)
                                for entry in entries_data:
                                    if 'value' in entry:
                                        synonyms = entry.get('synonyms', [])
                                        entity_values.append({
                                            'value': entry['value'],
                                            'synonyms': synonyms
                                        })
                        
                        entity_data['values'] = entity_values
                        self.entities_data[entity_data['name']] = entity_data
                        print(f"Загружена сущность: {entity_data.get('name', 'unknown')}")
                    except Exception as e:
                        print(f"Ошибка загрузки сущности {entity_file}: {e}")

    def convert_to_rasa_nlu(self) -> Dict:
        """Конвертация в формат Rasa NLU"""
        rasa_nlu = {
            'version': '3.1',
            'nlu': []
        }
        
        for intent in self.intents_data:
            intent_name = intent['name'].replace(' ', '_').replace('-', '_').lower()
            
            # Пропускаем системные интенты Dialogflow
            if intent_name in ['default_welcome_intent', 'default_fallback_intent']:
                if intent_name == 'default_welcome_intent':
                    intent_name = 'greet'  # Переименовываем в стандартный Rasa интент
                else:
                    continue  # Пропускаем fallback
                
            rasa_intent = {
                'intent': intent_name,
                'examples': []
            }
            
            # Добавляем примеры пользовательских фраз
            examples_text = ""
            for example in intent.get('user_examples', []):
                if example.strip():
                    examples_text += f"- {example.strip()}\n"
            
            # Если нет примеров, добавляем дефолтные на основе имени интента
            if not examples_text:
                default_examples = self.generate_default_examples(intent_name)
                examples_text = "\n".join([f"- {ex}" for ex in default_examples])
            
            rasa_intent['examples'] = examples_text.strip()
            rasa_nlu['nlu'].append(rasa_intent)
        
        return rasa_nlu

    def generate_default_examples(self, intent_name: str) -> List[str]:
        """Генерация дефолтных примеров для интентов без пользовательских фраз"""
        defaults = {
            'greet': ['привет', 'здравствуйте', 'добрый день', 'hi'],
            'goodbye': ['пока', 'до свидания', 'увидимся'],
            'textslist': ['список текстов', 'покажи тексты', 'какие есть тексты'],
            'help': ['помощь', 'что ты умеешь', 'как пользоваться'],
            'thanks': ['спасибо', 'благодарю'],
            'yes': ['да', 'конечно', 'согласен'],
            'no': ['нет', 'не согласен', 'отказываюсь'],
            'exit': ['выход', 'выйти', 'закончить']
        }
        
        return defaults.get(intent_name, [intent_name])

    def convert_to_rasa_stories(self) -> Dict:
        """Создание базовых историй для Rasa"""
        stories = {
            'version': '3.1',
            'stories': []
        }
        
        # Создаем базовые истории
        base_stories = [
            {
                'story': 'greet and start',
                'steps': [
                    {'intent': 'greet'},
                    {'action': 'utter_greet'},
                    {'action': 'action_welcome'}
                ]
            },
            {
                'story': 'goodbye',
                'steps': [
                    {'intent': 'goodbye'},
                    {'action': 'utter_goodbye'}
                ]
            }
        ]
        
        stories['stories'].extend(base_stories)
        
        # Создаем истории для остальных интентов
        for intent in self.intents_data:
            intent_name = intent['name'].replace(' ', '_').replace('-', '_').lower()
            
            if intent_name in ['default_welcome_intent', 'default_fallback_intent', 'greet', 'goodbye']:
                continue
                
            story = {
                'story': f'{intent_name} story',
                'steps': [
                    {'intent': intent_name},
                    {'action': f'utter_{intent_name}'}
                ]
            }
            stories['stories'].append(story)
        
        return stories

    def convert_to_rasa_domain(self) -> Dict:
        """Создание домена Rasa"""
        intents = ['greet', 'goodbye']
        actions = ['action_welcome', 'utter_greet', 'utter_goodbye']
        responses = {}
        
        # Базовые ответы
        responses['utter_greet'] = [
            {'text': 'Привет! Как дела?'},
            {'text': 'Здравствуйте!'},
            {'text': 'Добро пожаловать!'}
        ]
        
        responses['utter_goodbye'] = [
            {'text': 'До свидания!'},
            {'text': 'Увидимся!'},
            {'text': 'Пока!'}
        ]
        
        for intent in self.intents_data:
            intent_name = intent['name'].replace(' ', '_').replace('-', '_').lower()
            
            if intent_name in ['default_welcome_intent', 'default_fallback_intent']:
                continue
            
            intents.append(intent_name)
            actions.append(f'utter_{intent_name}')
            
            # Извлекаем ответы из Dialogflow
            intent_responses = []
            if 'responses' in intent and intent['responses']:
                for response in intent['responses']:
                    if 'messages' in response:
                        for message in response['messages']:
                            if message.get('type') == '0' and 'speech' in message:
                                for speech in message['speech']:
                                    if speech.strip():
                                        intent_responses.append({'text': speech.strip()})
            
            # Если нет ответов, добавляем дефолтный
            if not intent_responses:
                intent_responses.append({'text': f'Получен интент {intent_name}'})
            
            responses[f'utter_{intent_name}'] = intent_responses

        return {
            'version': '3.1',
            'intents': intents,
            'actions': actions,
            'responses': responses,
            'session_config': {
                'session_expiration_time': 60,
                'carry_over_slots_to_new_session': True
            }
        }

    def create_rasa_config(self) -> Dict:
        """Создание конфигурации Rasa"""
        return {
            'version': '3.1',
            'assistant_id': 'fillinthetextbot',
            'language': 'ru',
            'pipeline': [
                {'name': 'WhitespaceTokenizer'},
                {'name': 'RegexFeaturizer'},
                {'name': 'LexicalSyntacticFeaturizer'},
                {'name': 'CountVectorsFeaturizer'},
                {'name': 'CountVectorsFeaturizer',
                 'analyzer': 'char_wb',
                 'min_ngram': 1,
                 'max_ngram': 4},
                {'name': 'DIETClassifier', 'epochs': 50, 'constrain_similarities': True},
                {'name': 'EntitySynonymMapper'},
                {'name': 'ResponseSelector', 'epochs': 50}
            ],
            'policies': [
                {'name': 'MemoizationPolicy'},
                {'name': 'UnexpecTEDIntentPolicy', 'max_history': 5, 'epochs': 50},
                {'name': 'TEDPolicy', 'max_history': 5, 'epochs': 50, 'constrain_similarities': True}
            ]
        }

    def copy_base_files(self):
        """Копирование базовых файлов Rasa"""
        import shutil
        
        base_files_path = "/app/base_files"
        if os.path.exists(base_files_path):
            print("Копируем базовые файлы...")
            
            # Копируем endpoints.yml
            if os.path.exists(os.path.join(base_files_path, 'endpoints.yml')):
                shutil.copy2(
                    os.path.join(base_files_path, 'endpoints.yml'),
                    os.path.join(self.output_path, 'endpoints.yml')
                )
                print("✓ Скопирован endpoints.yml")
            
            # Копируем папку actions
            actions_src = os.path.join(base_files_path, 'actions')
            actions_dst = os.path.join(self.output_path, 'actions')
            if os.path.exists(actions_src):
                if os.path.exists(actions_dst):
                    shutil.rmtree(actions_dst)
                shutil.copytree(actions_src, actions_dst)
                print("✓ Скопирована папка actions")

    def convert(self):
        """Основной метод конвертации"""
        print("=" * 50)
        print("Начинаем конвертацию Dialogflow -> Rasa")
        print("=" * 50)
        
        # Создаем выходную директорию
        os.makedirs(self.output_path, exist_ok=True)
        os.makedirs(os.path.join(self.output_path, 'data'), exist_ok=True)
        
        # Копируем базовые файлы
        self.copy_base_files()
        
        # Загружаем данные Dialogflow
        self.load_dialogflow_data()
        print(f"Загружено интентов: {len(self.intents_data)}")
        print(f"Загружено сущностей: {len(self.entities_data)}")
        
        # Конвертируем в формат Rasa
        print("\nКонвертируем NLU данные...")
        nlu_data = self.convert_to_rasa_nlu()
        
        print("Создаем истории...")
        stories_data = self.convert_to_rasa_stories()
        
        print("Создаем конфигурацию...")
        config_data = self.create_rasa_config()
        
        print("Создаем домен...")
        domain_data = self.convert_to_rasa_domain()
        
        # Сохраняем файлы
        files_to_save = [
            ('data/nlu.yml', nlu_data),
            ('data/stories.yml', stories_data),
            ('config.yml', config_data),
            ('domain.yml', domain_data)
        ]
        
        for file_path, data in files_to_save:
            full_path = os.path.join(self.output_path, file_path)
            os.makedirs(os.path.dirname(full_path), exist_ok=True)
            
            with open(full_path, 'w', encoding='utf-8') as f:
                yaml.dump(data, f, allow_unicode=True, default_flow_style=False, sort_keys=False)
            
            print(f"✓ Сохранен файл: {file_path}")
        
        print("\n" + "=" * 50)
        print(f"🎉 Конвертация завершена! Файлы сохранены в {self.output_path}")
        print("=" * 50)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Использование: python dialogflow_to_rasa_converter.py <путь_к_dialogflow> <путь_вывода_rasa>")
        print("Пример: python dialogflow_to_rasa_converter.py /data/dialogflow /data/rasa")
        sys.exit(1)
    
    dialogflow_path = sys.argv[1]
    output_path = sys.argv[2]
    
    if not os.path.exists(dialogflow_path):
        print(f"ОШИБКА: Путь {dialogflow_path} не существует!")
        sys.exit(1)
    
    converter = DialogflowToRasaConverter(dialogflow_path, output_path)
    converter.convert()