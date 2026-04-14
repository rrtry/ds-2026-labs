Write-Host "Stopping Valuator and RankCalculator" -ForegroundColor Green

# Остановка Nginx
Write-Host -NoNewline "Stopping Nginx: "
$nginxProcess = Get-Process -Name nginx -ErrorAction SilentlyContinue
if ($nginxProcess) {
    $scriptPath  = Split-Path -Parent $MyInvocation.MyCommand.Path
    $projectRoot = Split-Path -Parent $scriptPath
    $nginxPath   = Join-Path $projectRoot "nginx"
    Push-Location $nginxPath
    & nginx -s stop 2>$null
    Pop-Location
    $nginxProcess = Get-Process -Name nginx -ErrorAction SilentlyContinue
    if ($nginxProcess) { $nginxProcess | Stop-Process -Force }
    Write-Host "OK" -ForegroundColor Green
} else {
    Write-Host "Nginx is not running" -ForegroundColor Yellow
}

# Остановка Valuator (процессы dotnet с --urls)
Write-Host -NoNewline "Stopping Valuator instances: "
$valuatorStopped = 0
$dotnetProcesses = Get-Process -Name dotnet -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*--urls*" }
foreach ($proc in $dotnetProcesses) {
    try {
        $proc | Stop-Process -Force
        $valuatorStopped++
    } catch {}
}
Write-Host "$valuatorStopped stopped" -ForegroundColor Green

# Остановка RankCalculator (по сохранённым PID)
Write-Host -NoNewline "Stopping RankCalculator instances: "
$pidFile = Join-Path $env:TEMP "rankcalculator_pids.txt"
$rankStopped = 0
if (Test-Path $pidFile) {
    $pids = Get-Content $pidFile
    foreach ($procId in $pids) {   # ← ИСПРАВЛЕНО: не $pid, а $procId
        try {
            $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
            if ($proc) {
                $proc | Stop-Process -Force
                $rankStopped++
            }
        } catch {}
    }
    Remove-Item $pidFile -Force
}
Write-Host "$rankStopped stopped" -ForegroundColor Green

# Освобождение портов (только для Valuator)
Write-Host -NoNewline "Freeing ports (5001-5002): "
$ports = @(5001, 5002)
foreach ($port in $ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        $processId = $connection.OwningProcess
        if ($processId -and $processId -ne 0) {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($process) { try { $process | Stop-Process -Force } catch {} }
        }
    }
}
Write-Host "OK" -ForegroundColor Green

# Очистка логов
Write-Host -NoNewline "Cleaning up log files: "
$logCount = 0
$logFiles = Get-ChildItem "$env:TEMP\valuator_*.log","$env:TEMP\valuator_*.err","$env:TEMP\rankcalculator_*.log","$env:TEMP\rankcalculator_*.err" -ErrorAction SilentlyContinue
foreach ($file in $logFiles) {
    try { Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue; $logCount++ } catch {}
}
if ($logCount -gt 0) { Write-Host "OK (removed $logCount files)" -ForegroundColor Green }
else { Write-Host "No log files found" -ForegroundColor Yellow }

Write-Host "Stopped" -ForegroundColor Green