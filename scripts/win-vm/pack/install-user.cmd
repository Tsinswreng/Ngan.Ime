@echo off
rem 把 Ngan IME 加進「目前使用者」的輸入法清單。
rem 必須以本人身分執行（不要用管理員）：清單是每個使用者自己的。
setlocal
cd /d "%~dp0"
"Ngan.Ime.Tsf.Registration.exe" --install-user
echo.
echo 若顯示成功，請登出再登入，然後用 Win+Space 切換。
pause
