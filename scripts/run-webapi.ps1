Param(
    [string]$Urls = "http://localhost:5000;https://localhost:5001"
)

Write-Host "Starting AuthManager WebApi sample (project: samples/AuthManagerSample.WebApi)"
Write-Host "Listening on: $Urls"

$proj = "samples/AuthManagerSample.WebApi"
Start-Process -FilePath dotnet -ArgumentList "run --project $proj --urls $Urls" -NoNewWindow

Start-Sleep -Seconds 2
try {
    Start-Process "https://localhost:5001/swagger"
} catch {
    Write-Host "Open https://localhost:5001/swagger in your browser"
}
