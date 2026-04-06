Write-Host "Stopping Valuator" -ForegroundColor Green

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
    if ($nginxProcess) {
        $nginxProcess | Stop-Process -Force
    }
    Write-Host "OK" -ForegroundColor Green
    
} else {
    Write-Host "Nginx is not running" -ForegroundColor Yellow
}

# Stop application instances
Write-Host -NoNewline "Freeing ports (5001-5004): "
$ports = @(5001, 5002)
$stoppedCount = 0

foreach ($port in $ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        $processId = $connection.OwningProcess
        if ($processId -and $processId -ne 0) {
            $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
            if ($process) {
                try {
                    $process | Stop-Process -Force
                    $stoppedCount++
                    Write-Host -NoNewline "." 
                } catch {
                    Write-Host -NoNewline "!" 
                }
            }
        }
    }
}

Write-Host -NoNewline "Cleaning up log files: "
$logCount = 0
$logFiles = Get-ChildItem "$env:TEMP\valuator_*.log" -ErrorAction SilentlyContinue
$errFiles = Get-ChildItem "$env:TEMP\valuator_*.err" -ErrorAction SilentlyContinue
$allFiles = $logFiles + $errFiles

foreach ($file in $allFiles) {
    try {
        Remove-Item $file.FullName -Force -ErrorAction SilentlyContinue
        $logCount++
    } catch {

    }
}

if ($logCount -gt 0) {
    Write-Host "OK (removed $logCount files)" -ForegroundColor Green
} else {
    Write-Host "No log files found" -ForegroundColor Yellow
}

Write-Host "Stopped" -ForegroundColor Green