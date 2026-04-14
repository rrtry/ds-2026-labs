Write-Host "Starting Valuator and RankCalculator" -ForegroundColor Green

Write-Host "Stopping previous instances..." -ForegroundColor Yellow
# Останавливаем ранее запущенные экземпляры Valuator (по процессам dotnet с --urls)
Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*--urls*" } | Stop-Process -Force
# Останавливаем RankCalculator (по сохранённым PID)
$pidFile = Join-Path $env:TEMP "rankcalculator_pids.txt"
if (Test-Path $pidFile) {
    Get-Content $pidFile | ForEach-Object {
        try {
            $proc = Get-Process -Id $_ -ErrorAction SilentlyContinue
            if ($proc) { $proc | Stop-Process -Force }
        } catch {}
    }
    Remove-Item $pidFile -Force
}
# Останавливаем nginx
Get-Process -Name nginx -ErrorAction SilentlyContinue | Stop-Process -Force

Start-Sleep -Seconds 2

# Параметры запуска Valuator
$ports = @(5001, 5002)
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath
$appPath = Join-Path $projectRoot "Valuator"
$rankCalculatorPath = Join-Path $projectRoot "RankCalculator"

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
        if ($process) { Write-Host "OK" -ForegroundColor Green } else { Write-Host "Error" -ForegroundColor Red }
    }
    catch { Write-Host "Failed: $_" -ForegroundColor Red }
    Start-Sleep -Seconds 1
}

Write-Host "`nStarting RankCalculator instances" -ForegroundColor Yellow
$rankCalculatorInstances = 3   # количество экземпляров
$pids = @()

for ($i=1; $i -le $rankCalculatorInstances; $i++) {
    Write-Host -NoNewline "Starting RankCalculator instance #$i :"
    try {
        $proc = Start-Process -FilePath "dotnet" `
            -ArgumentList "run" `
            -WorkingDirectory $rankCalculatorPath `
            -PassThru `
            -NoNewWindow `
            -RedirectStandardOutput "$env:TEMP\rankcalculator_$i.log" `
            -RedirectStandardError "$env:TEMP\rankcalculator_$i.err"
        if ($proc) {
            Write-Host "OK (PID: $($proc.Id))" -ForegroundColor Green
            $pids += $proc.Id
        } else {
            Write-Host "Error" -ForegroundColor Red
        }
    }
    catch { Write-Host "Failed: $_" -ForegroundColor Red }
    Start-Sleep -Seconds 1
}

# Сохраняем PID'ы RankCalculator в файл для остановки
$pids | Out-File -FilePath $pidFile -Force

Write-Host "`nWaiting for all applications to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

Write-Host "`nStarting Nginx..." -ForegroundColor Yellow
$nginxPath = (Get-Command nginx.exe -ErrorAction SilentlyContinue).Path
if (-not $nginxPath) {
    Write-Host "Error: nginx.exe not found in PATH!" -ForegroundColor Red
    exit 1
}
$nginxDir = Split-Path $nginxPath -Parent
Write-Host "Found nginx at: $nginxPath" -ForegroundColor Gray

$currentLocation = Get-Location
Set-Location -Path $nginxDir

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

& $nginxPath -s stop 2>$null
Start-Sleep -Seconds 2
Write-Host -NoNewline "Starting nginx server: "

try {
    Start-Process -FilePath "cmd.exe" -ArgumentList "/c start /B nginx.exe" -WorkingDirectory $nginxDir -WindowStyle Hidden
    Start-Sleep -Seconds 3
    $nginxProcess = Get-Process -Name nginx -ErrorAction SilentlyContinue
    if ($nginxProcess) {
        Write-Host "OK (PID: $($nginxProcess[0].Id))" -ForegroundColor Green
        $listening = netstat -ano | Select-String ":8080"
        if ($listening) { Write-Host "  - Listening on port 8080: OK" -ForegroundColor Green }
        else { Write-Host "  - Warning: nginx not listening on port 8080" -ForegroundColor Yellow }
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

Set-Location -Path $currentLocation

Write-Host "`nStarted" -ForegroundColor Green
Write-Host "Access point: http://localhost:8080" -ForegroundColor Yellow
Write-Host "Valuator instances:"
foreach ($port in $ports) {
    Write-Host "  - http://localhost:$port (active)"
}
Write-Host "RankCalculator instances: $rankCalculatorInstances"
Write-Host "`nNginx status: http://localhost:8080/status" -ForegroundColor Gray
Write-Host "Log files: $env:TEMP\valuator_*.log, $env:TEMP\rankcalculator_*.log" -ForegroundColor Gray
Write-Host "`nTo stop the system run: .\scripts\stop.ps1" -ForegroundColor Yellow