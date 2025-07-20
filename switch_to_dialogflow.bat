@echo off
echo Switching to Dialogflow NLU provider...

REM Remove environment variable to use default Dialogflow configuration
set ASPNETCORE_ENVIRONMENT=

REM Stop Rasa emulator if running
echo Stopping Rasa emulator...
call stop_emulator.bat

REM Start the API with Dialogflow configuration
echo Starting FillInTheTextBot API with Dialogflow provider...
cd src\FillInTheTextBot.Api
dotnet run

pause