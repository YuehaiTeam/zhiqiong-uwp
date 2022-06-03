@echo off
set certfile=../zhiqiong_TemporaryKey.pfx
set /p certpass=<../zhiqiong_TemporaryKey.pfxpass
echo Signing %1
signtool sign /f %certfile% /p %certpass% /fd SHA256 /t http://timestamp.digicert.com %1