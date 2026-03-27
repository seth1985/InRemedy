param()

$ErrorActionPreference = "Stop"

$configPath = Join-Path $PSScriptRoot "InRemedy.config.json"
$exePath = Join-Path $PSScriptRoot "InRemedy.Api.exe"
$logDir = Join-Path $PSScriptRoot "logs"

if (!(Test-Path $exePath)) {
    throw "InRemedy.Api.exe was not found in $PSScriptRoot"
}

$config = @{
    AppUrl = "http://127.0.0.1:5180"
    ConnectionString = "Host=localhost;Port=5432;Database=inremedy;Username=postgres;Password=postgres"
}

if (Test-Path $configPath) {
    $loadedConfig = Get-Content $configPath -Raw | ConvertFrom-Json
    if ($loadedConfig.AppUrl) {
        $config.AppUrl = $loadedConfig.AppUrl
    }
    if ($loadedConfig.ConnectionString) {
        $config.ConnectionString = $loadedConfig.ConnectionString
    }
}

$healthUrl = "$($config.AppUrl.TrimEnd('/'))/api/imports"

function Test-AppReady {
    param([string]$Url)

    try {
        $null = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
        return $true
    }
    catch {
        return $false
    }
}

if (!(Test-AppReady -Url $healthUrl)) {
    if (!(Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir | Out-Null
    }

    $env:INREMEDY_CONNECTION_STRING = $config.ConnectionString
    $env:ASPNETCORE_ENVIRONMENT = "Production"

    Start-Process -FilePath $exePath `
        -ArgumentList @("--urls", $config.AppUrl) `
        -WorkingDirectory $PSScriptRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $logDir "app.out.log") `
        -RedirectStandardError (Join-Path $logDir "app.err.log")

    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 500
        if (Test-AppReady -Url $healthUrl) {
            break
        }
    }
}

Start-Process $config.AppUrl
