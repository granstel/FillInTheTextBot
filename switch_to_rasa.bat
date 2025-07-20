@echo off
echo Switching to Rasa NLU provider...

REM Set environment variable for ASP.NET Core to use Rasa configuration
set ASPNETCORE_ENVIRONMENT=Rasa

REM Start Rasa emulator if not already running
echo Starting Rasa emulator...
call start_emulator.bat

REM Wait a bit for Rasa to start
timeout /t 10 /nobreak

REM Start the API with Rasa configuration
echo Starting FillInTheTextBot API with Rasa provider...
cd src\FillInTheTextBot.Api
dotnet run

pause