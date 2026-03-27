param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PostgresInstallerUrl = "https://get.enterprisedb.com/postgresql/postgresql-17.9-1-windows-x64.exe"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$distDir = Join-Path $root "dist"
$wwwrootDir = Join-Path $root "api\InRemedy.Api\wwwroot"
$publishDir = Join-Path $root "artifacts\publish\InRemedy"
$installerDir = Join-Path $root "artifacts\installer"
$cacheDir = Join-Path $root "artifacts\cache"
$apiProject = Join-Path $root "api\InRemedy.Api\InRemedy.Api.csproj"
$desktopProject = Join-Path $root "launcher\InRemedy.Desktop\InRemedy.Desktop.csproj"
$iconSource = Join-Path $root "image\favicon.png"
$iconTarget = Join-Path $publishDir "InRemedy.ico"
$innoCompiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
$postgresInstallerFileName = Split-Path $PostgresInstallerUrl -Leaf
$postgresInstallerPath = Join-Path $cacheDir $postgresInstallerFileName
$webViewInstallerUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
$webViewInstallerPath = Join-Path $cacheDir "MicrosoftEdgeWebView2Setup.exe"

Write-Host "Building frontend..."
Push-Location $root
try {
    npm run build
}
finally {
    Pop-Location
}

if (Test-Path $wwwrootDir) {
    Remove-Item $wwwrootDir -Recurse -Force
}
New-Item -ItemType Directory -Path $wwwrootDir | Out-Null
Copy-Item (Join-Path $distDir "*") $wwwrootDir -Recurse -Force

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
New-Item -ItemType Directory -Path $cacheDir -Force | Out-Null

Write-Host "Publishing backend..."
dotnet publish $apiProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

Write-Host "Publishing desktop host..."
dotnet publish $desktopProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    -o $publishDir

Copy-Item (Join-Path $PSScriptRoot "InRemedy.config.json") $publishDir -Force
Copy-Item (Join-Path $PSScriptRoot "Initialize-InRemedyDatabase.ps1") $publishDir -Force

Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap($iconSource)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$fileStream = [System.IO.File]::Create($iconTarget)
try {
    $icon.Save($fileStream)
}
finally {
    $fileStream.Dispose()
    $icon.Dispose()
    $bitmap.Dispose()
}

if (!(Test-Path $innoCompiler)) {
    throw "Inno Setup compiler not found at $innoCompiler"
}

if (!(Test-Path $postgresInstallerPath)) {
    Write-Host "Downloading PostgreSQL installer..."
    Invoke-WebRequest -Uri $PostgresInstallerUrl -OutFile $postgresInstallerPath
}

if (!(Test-Path $webViewInstallerPath)) {
    Write-Host "Downloading WebView2 bootstrapper..."
    Invoke-WebRequest -Uri $webViewInstallerUrl -OutFile $webViewInstallerPath
}

Write-Host "Building installer..."
& $innoCompiler `
    "/DMyAppVersion=0.1.0" `
    "/DSourceDir=$publishDir" `
    "/DOutputDir=$installerDir" `
    "/DPostgresInstaller=$postgresInstallerPath" `
    "/DWebViewInstaller=$webViewInstallerPath" `
    (Join-Path $PSScriptRoot "InRemedy.iss")

Write-Host "Installer created in $installerDir"
