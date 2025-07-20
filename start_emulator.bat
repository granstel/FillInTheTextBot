@echo off
echo ===============================================
echo   FillInTheTextBot - Rasa Emulator Setup
echo ===============================================
echo.

echo Проверяем Docker...
docker --version >nul 2>&1
if %errorlevel% neq 0 (
    echo ОШИБКА: Docker не установлен или не запущен!
    echo Пожалуйста, установите Docker Desktop и запустите его.
    pause
    exit /b 1
)

echo Docker найден ✓
echo.

echo Останавливаем предыдущие контейнеры...
docker-compose -f docker-compose.simple.yml down >nul 2>&1

echo Создаем директорию для Rasa бота...
if not exist "rasa_bot" mkdir rasa_bot

echo.
echo Запускаем конвертацию и Rasa сервер...
echo Это может занять несколько минут при первом запуске...
echo.

docker-compose -f docker-compose.simple.yml up --build

echo.
echo ===============================================
echo   Для остановки нажмите Ctrl+C
echo ===============================================
pause