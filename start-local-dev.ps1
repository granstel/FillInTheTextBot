# Скрипт для запуска локального окружения разработки
Write-Host "🚀 Запуск локального окружения для разработки FillInTheTextBot" -ForegroundColor Green

# Проверяем, что Docker запущен
Write-Host "📦 Проверка Docker..." -ForegroundColor Yellow
$dockerRunning = docker info 2>$null
if (-not $dockerRunning) {
    Write-Host "❌ Docker не запущен или недоступен. Запустите Docker Desktop и повторите попытку." -ForegroundColor Red
    exit 1
}

# Запуск Dialogflow эмулятора
Write-Host "🎭 Запуск Dialogflow Emulator..." -ForegroundColor Yellow
docker-compose build dialogflow-emulator
docker-compose up -d dialogflow-emulator

# Ждем запуска эмулятора
Write-Host "⏳ Ожидание запуска эмулятора..." -ForegroundColor Yellow
$timeout = 30
$elapsed = 0

do {
    Start-Sleep -Seconds 2
    $elapsed += 2
    $response = $null
    
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:3000" -TimeoutSec 5 -UseBasicParsing -ErrorAction SilentlyContinue
    } catch {
        # Игнорируем ошибки соединения
    }
    
    if ($response -and $response.StatusCode -eq 200) {
        Write-Host "✅ Dialogflow Emulator запущен на http://localhost:3000" -ForegroundColor Green
        break
    }
    
    if ($elapsed -ge $timeout) {
        Write-Host "⚠️ Эмулятор не отвечает, но контейнер может все еще запускаться. Проверьте логи:" -ForegroundColor Yellow
        Write-Host "   docker-compose logs dialogflow-emulator" -ForegroundColor Cyan
        break
    }
    
    Write-Host "   Ждем... ($elapsed/$timeout сек)" -ForegroundColor Gray
} while ($true)

Write-Host ""
Write-Host "🎯 Окружение готово!" -ForegroundColor Green
Write-Host ""
Write-Host "Следующие шаги:" -ForegroundColor Yellow
Write-Host "1. Запустите API с локальными настройками:" -ForegroundColor White
Write-Host "   cd src/FillInTheTextBot.Api" -ForegroundColor Cyan
Write-Host "   dotnet run --environment Local" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Или в IDE установите переменную окружения:" -ForegroundColor White
Write-Host "   ASPNETCORE_ENVIRONMENT=Local" -ForegroundColor Cyan
Write-Host ""
Write-Host "Полезные команды:" -ForegroundColor Yellow
Write-Host "• Логи эмулятора:    docker-compose logs -f dialogflow-emulator" -ForegroundColor White
Write-Host "• Остановка:         docker-compose down" -ForegroundColor White
Write-Host "• Перезапуск:        docker-compose restart dialogflow-emulator" -ForegroundColor White
Write-Host ""
Write-Host "📚 Подробная документация: DIALOGFLOW_EMULATOR.md" -ForegroundColor Green