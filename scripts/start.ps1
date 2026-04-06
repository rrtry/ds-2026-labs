# start.ps1
Write-Host "Starting Valuator" -ForegroundColor Green

Write-Host "Stopping previous instances..." -ForegroundColor Yellow
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*--urls*" } | Stop-Process -Force
Get-Process -Name nginx -ErrorAction SilentlyContinue | Stop-Process -Force

Start-Sleep -Seconds 2
$ports = @(5001, 5002)

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$appPath = Join-Path $projectRoot "Valuator"

Write-Host "`nStarting Valuator application instances from: $appPath" -ForegroundColor Yellow

foreach ($port in $ports) {
    Write-Host -NoNewline "Starting on port $port :"
    try {
        $process = Start-Process -FilePath "dotnet" `
            -ArgumentList "run --urls `"http://0.0.0.0:$port`"" `
            -WorkingDirectory $appPath `
            -PassThru `
            -NoNewWindow `
            -RedirectStandardOutput "$env:TEMP\valuator_$port.log" `
            -RedirectStandardError "$env:TEMP\valuator_$port.err"
        
        if ($process) {
            Write-Host "OK" -ForegroundColor Green
        } else {
            Write-Host "Error" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "Failed: $_" -ForegroundColor Red
    }
    
    Start-Sleep -Seconds 1
}

Write-Host "`nWaiting for all applications to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5
Write-Host "`nStarting Nginx..." -ForegroundColor Yellow

# Получаем путь к nginx
$nginxPath = (Get-Command nginx.exe -ErrorAction SilentlyContinue).Path
if (-not $nginxPath) {
    Write-Host "Error: nginx.exe not found in PATH!" -ForegroundColor Red
    exit 1
}

$nginxDir = Split-Path $nginxPath -Parent
Write-Host "Found nginx at: $nginxPath" -ForegroundColor Gray

# Сохраняем текущую директорию
$currentLocation = Get-Location

# Переходим в директорию nginx
Set-Location -Path $nginxDir

# Проверяем конфигурацию
Write-Host -NoNewline "Testing nginx configuration: "
$testResult = & $nginxPath -t -c conf/nginx.conf -p . 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "Error" -ForegroundColor Red
    Write-Host $testResult -ForegroundColor Red
    Set-Location -Path $currentLocation
    exit 1
}

# Останавливаем nginx если он уже запущен
& $nginxPath -s stop 2>$null
Start-Sleep -Seconds 2
Write-Host -NoNewline "Starting nginx server: "

try {
    # Запускаем nginx как отдельный процесс
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c start /B nginx.exe" -WorkingDirectory $nginxDir -WindowStyle Hidden
    Start-Sleep -Seconds 3
    $nginxProcess = Get-Process -Name nginx -ErrorAction SilentlyContinue
    if ($nginxProcess) {
        Write-Host "OK (PID: $($nginxProcess[0].Id))" -ForegroundColor Green
        
        # Проверяем, что nginx слушает порт 8080
        $listening = netstat -ano | Select-String ":8080"
        if ($listening) {
            Write-Host "  - Listening on port 8080: OK" -ForegroundColor Green
        } else {
            Write-Host "  - Warning: nginx not listening on port 8080" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Failed - process not found" -ForegroundColor Red
        Set-Location -Path $currentLocation
        exit 1
    }
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    Set-Location -Path $currentLocation
    exit 1
}

# Возвращаемся в исходную директорию
Set-Location -Path $currentLocation

Write-Host "`nStarted" -ForegroundColor Green
Write-Host "Access point: http://localhost:8080" -ForegroundColor Yellow
Write-Host "Application instances:"
foreach ($port in $ports) {
    $status = if ($port -le 5002) { "active" } else { "backup" }
    Write-Host "  - http://localhost:$port ($status)"
}
Write-Host "`nNginx status: http://localhost:8080/status" -ForegroundColor Gray
Write-Host "Log files: $env:TEMP\valuator_*.log" -ForegroundColor Gray
Write-Host "`nTo stop the system run: .\scripts\stop.ps1" -ForegroundColor Yellow