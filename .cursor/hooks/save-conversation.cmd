@echo off
setlocal
cd /d "%~dp0\..\.."
if exist "C:\Program Files\nodejs\node.exe" (
  "C:\Program Files\nodejs\node.exe" "%~dp0save-conversation.js"
) else (
  node "%~dp0save-conversation.js"
)
