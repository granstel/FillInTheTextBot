const express = require('express');
const cors = require('cors');
const bodyParser = require('body-parser');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;
const PROJECT_ID = process.env.PROJECT_ID || 'fillinthetextbot-vyyaxp';
const LANGUAGE_CODE = process.env.LANGUAGE_CODE || 'ru';
const AGENT_PATH = process.env.AGENT_PATH || '/app/agent';

// Middleware
app.use(cors());
app.use(bodyParser.json());

// Загрузка интентов при запуске
let intents = {};
let agent = {};

function loadAgentData() {
    console.log(`Loading agent from: ${AGENT_PATH}`);
    
    try {
        // Загружаем agent.json
        const agentPath = path.join(AGENT_PATH, 'agent.json');
        if (fs.existsSync(agentPath)) {
            agent = JSON.parse(fs.readFileSync(agentPath, 'utf8'));
            console.log(`Loaded agent: ${agent.displayName}`);
        }
        
        // Загружаем интенты
        const intentsPath = path.join(AGENT_PATH, 'intents');
        if (fs.existsSync(intentsPath)) {
            const intentFiles = fs.readdirSync(intentsPath)
                .filter(file => file.endsWith('.json') && !file.includes('_usersays_'));
            
            intentFiles.forEach(file => {
                try {
                    const intentPath = path.join(intentsPath, file);
                    const intent = JSON.parse(fs.readFileSync(intentPath, 'utf8'));
                    intents[intent.name] = intent;
                    console.log(`Loaded intent: ${intent.name}`);
                } catch (err) {
                    console.error(`Error loading intent ${file}:`, err.message);
                }
            });
            
            console.log(`Total intents loaded: ${Object.keys(intents).length}`);
        }
    } catch (err) {
        console.error('Error loading agent data:', err.message);
        // Создаем базовые интенты для работы
        createDefaultIntents();
    }
}

function createDefaultIntents() {
    console.log('Creating default intents for testing...');
    
    intents['Default Welcome Intent'] = {
        name: 'Default Welcome Intent',
        events: [{ name: 'WELCOME' }],
        responses: [{
            messages: [{
                type: '0',
                speech: ['Добро пожаловать! Давай вместе сочиним занимательные истории!']
            }]
        }]
    };
    
    intents['EasyWelcome'] = {
        name: 'EasyWelcome',
        events: [{ name: 'EasyWelcome' }],
        responses: [{
            messages: [{
                type: '0',
                speech: ['Настало время занимательных историй! Давай сочиним что-нибудь?']
            }]
        }]
    };
    
    intents['Default Fallback Intent'] = {
        name: 'Default Fallback Intent',
        fallbackIntent: true,
        responses: [{
            messages: [{
                type: '0',
                speech: ['Извините, я не понял. Можете повторить?']
            }]
        }]
    };
}

function findIntentByEvent(eventName) {
    return Object.values(intents).find(intent => 
        intent.events && intent.events.some(event => event.name === eventName)
    );
}

function findIntentByText(text) {
    // Простая логика поиска интента по тексту
    // В реальном Dialogflow это сложный ML процесс
    
    if (!text) return null;
    
    const lowerText = text.toLowerCase().trim();
    
    // Ключевые слова для интентов
    const keywordMap = {
        'Default Welcome Intent': ['привет', 'начать', 'hello', '/start'],
        'EasyWelcome': ['да', 'конечно', 'давай'],
        'Exit': ['выход', 'выйти', 'стоп', 'пока'],
        'Help': ['помощь', 'что ты умеешь', 'справка'],
        'TextsList': ['список текстов', 'список историй', 'тексты'],
        'Yes': ['да', 'ага', 'конечно', 'угу'],
        'No': ['нет', 'не хочу', 'не буду']
    };
    
    for (const [intentName, keywords] of Object.entries(keywordMap)) {
        if (keywords.some(keyword => lowerText.includes(keyword))) {
            return intents[intentName] || null;
        }
    }
    
    return null;
}

function getFallbackIntent() {
    return intents['Default Fallback Intent'] || {
        name: 'Default Fallback Intent',
        responses: [{
            messages: [{
                type: '0',
                speech: ['Извините, я не понял. Можете повторить?']
            }]
        }]
    };
}

function createDialogflowResponse(intent, queryText) {
    const response = intent.responses && intent.responses.length > 0 ? intent.responses[0] : {};
    const messages = response.messages || [];
    
    // Находим текстовое сообщение
    const textMessage = messages.find(msg => msg.type === '0' || msg.type === 0);
    let fulfillmentText = 'Ответ не найден';
    
    if (textMessage && textMessage.speech && textMessage.speech.length > 0) {
        // Выбираем случайный ответ из доступных
        const randomIndex = Math.floor(Math.random() * textMessage.speech.length);
        fulfillmentText = textMessage.speech[randomIndex];
    }
    
    // Создаем ответ в формате Dialogflow V2 API
    return {
        responseId: `emulator-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`,
        queryResult: {
            queryText: queryText || '',
            parameters: response.parameters || {},
            allRequiredParamsPresent: true,
            fulfillmentText: fulfillmentText,
            fulfillmentMessages: [
                {
                    text: {
                        text: [fulfillmentText]
                    }
                }
            ],
            outputContexts: [],
            intent: {
                name: `projects/${PROJECT_ID}/agent/intents/${intent.id || 'emulator-intent'}`,
                displayName: intent.name || 'Unknown Intent'
            },
            intentDetectionConfidence: 0.85,
            languageCode: LANGUAGE_CODE
        }
    };
}

// Основной endpoint для DetectIntent
app.post('/v2/projects/:projectId/agent/sessions/:sessionId:detectIntent', (req, res) => {
    const { projectId, sessionId } = req.params;
    const { queryInput } = req.body;
    
    console.log(`\n--- DetectIntent Request ---`);
    console.log(`Project: ${projectId}, Session: ${sessionId}`);
    console.log(`Query Input:`, JSON.stringify(queryInput, null, 2));
    
    let intent = null;
    let queryText = '';
    
    try {
        // Обработка события
        if (queryInput.event) {
            queryText = `event:${queryInput.event.name}`;
            intent = findIntentByEvent(queryInput.event.name);
            console.log(`Looking for event: ${queryInput.event.name}`);
        }
        // Обработка текста
        else if (queryInput.text) {
            queryText = queryInput.text.text;
            intent = findIntentByText(queryText);
            console.log(`Looking for text: "${queryText}"`);
        }
        
        // Если интент не найден, используем fallback
        if (!intent) {
            intent = getFallbackIntent();
            console.log('Using fallback intent');
        } else {
            console.log(`Found intent: ${intent.name}`);
        }
        
        const response = createDialogflowResponse(intent, queryText);
        console.log(`Response:`, JSON.stringify(response, null, 2));
        
        res.json(response);
        
    } catch (error) {
        console.error('Error processing request:', error);
        res.status(500).json({
            error: 'Internal server error',
            message: error.message
        });
    }
});

// Health check endpoint
app.get('/health', (req, res) => {
    res.json({
        status: 'healthy',
        timestamp: new Date().toISOString(),
        intentsLoaded: Object.keys(intents).length,
        agent: agent.displayName || 'Unknown'
    });
});

// Endpoint для получения списка интентов
app.get('/debug/intents', (req, res) => {
    res.json({
        intents: Object.keys(intents),
        total: Object.keys(intents).length
    });
});

// Endpoint для получения конкретного интента
app.get('/debug/intents/:intentName', (req, res) => {
    const intent = intents[req.params.intentName];
    if (intent) {
        res.json(intent);
    } else {
        res.status(404).json({ error: 'Intent not found' });
    }
});

// Обработка создания контекстов (заглушка)
app.post('/v2/projects/:projectId/agent/sessions/:sessionId/contexts', (req, res) => {
    console.log(`\n--- Create Context Request ---`);
    console.log(`Project: ${req.params.projectId}, Session: ${req.params.sessionId}`);
    console.log(`Context:`, JSON.stringify(req.body, null, 2));
    
    // Просто возвращаем созданный контекст
    res.json(req.body);
});

// Загрузка данных агента
loadAgentData();

// Запуск сервера
app.listen(PORT, '0.0.0.0', () => {
    console.log(`\n🎭 Dialogflow Emulator Server is running!`);
    console.log(`📍 Port: ${PORT}`);
    console.log(`🏷️  Project ID: ${PROJECT_ID}`);
    console.log(`🌍 Language: ${LANGUAGE_CODE}`);
    console.log(`📁 Agent Path: ${AGENT_PATH}`);
    console.log(`✅ Health check: http://localhost:${PORT}/health`);
    console.log(`🔍 Debug intents: http://localhost:${PORT}/debug/intents`);
    console.log(`\n🚀 Ready to handle Dialogflow API requests!`);
});

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n👋 Shutting down Dialogflow Emulator...');
    process.exit(0);
});

process.on('SIGTERM', () => {
    console.log('\n👋 Shutting down Dialogflow Emulator...');
    process.exit(0);
});