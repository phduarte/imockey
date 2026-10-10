@echo off
setlocal

for %%p in (5087 7253) do call :liberar_porta %%p

start "imockey" cmd /c "dotnet run --project Imockey"
timeout /t 5 /nobreak > nul
start http://localhost:5087
exit /b 0

:liberar_porta
for /f "tokens=5" %%a in ('netstat -ano ^| findstr /R /C:":%1 .*LISTENING"') do (
    if not "%%a"=="0" (
        echo Porta %1 em uso pelo processo %%a. Encerrando...
        taskkill /F /PID %%a > nul 2>&1
    )
)
exit /b 0
