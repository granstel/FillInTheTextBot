@echo off
echo ===============================================
echo   Останавливаем FillInTheTextBot Emulator
echo ===============================================
echo.

echo Останавливаем контейнеры...
docker-compose -f docker-compose.simple.yml down

echo.
echo Контейнеры остановлены ✓
echo.
pause