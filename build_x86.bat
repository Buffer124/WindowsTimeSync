@echo off
echo ==============================================
echo [1/3] Tat tien trinh cu neu co...
taskkill /F /IM WindowsTimeSync.exe >nul 2>&1

echo [2/3] Bien dich 32-bit Single-File (Nen chuan boi .NET)...
dotnet publish -c Release -r win-x86

echo [3/3] Ky so Authenticode SHA-256...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -match 'Minh Duy Utility' } | Select-Object -First 1; Set-AuthenticodeSignature -FilePath 'bin\Release\net8.0-windows\win-x86\publish\WindowsTimeSync.exe' -Certificate $c -HashAlgorithm SHA256 -TimestampServer 'http://timestamp.digicert.com'"

echo ==============================================
echo HOAN TAT!
pause