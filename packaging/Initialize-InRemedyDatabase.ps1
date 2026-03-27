param(
    [string]$InstallRoot = $PSScriptRoot,
    [string]$PgBinDir = "C:\Program Files\PostgreSQL\17-inremedy\bin",
    [string]$AppUrl = "http://127.0.0.1:5180",
    [string]$DbHost = "127.0.0.1",
    [int]$Port = 5433,
    [string]$SuperUser = "postgres",
    [string]$SuperPassword = "InRemedyPG!2026",
    [string]$ServiceName = "postgresql-x64-17-inremedy",
    [string]$AppUser = "inremedy_app",
    [string]$AppPassword = "InRemedy!2026Local",
    [string]$DatabaseName = "inremedy"
)

$ErrorActionPreference = "Stop"

$configPath = Join-Path $InstallRoot "InRemedy.config.json"
$logPath = Join-Path $InstallRoot "bootstrap.log"
$psqlPath = Join-Path $PgBinDir "psql.exe"
$pgIsReadyPath = Join-Path $PgBinDir "pg_isready.exe"
$script:CurrentPassword = $SuperPassword
$script:CurrentUser = $SuperUser

function Write-Log {
    param([string]$Message)

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    Add-Content -Path $logPath -Value "[$timestamp] $Message"
}

function Assert-ValidIdentifier {
    param(
        [string]$Value,
        [string]$Name
    )

    if ($Value -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        throw "$Name contains an unsupported PostgreSQL identifier: $Value"
    }
}

function Invoke-ExternalChecked {
    param(
        [string]$FilePath,
        [string[]]$Arguments,
        [string]$InputText = ""
    )

    Write-Log ("Running: " + $FilePath + " " + ($Arguments -join " "))

    if ([string]::IsNullOrEmpty($InputText)) {
        $output = & $FilePath @Arguments 2>&1
    }
    else {
        $output = $InputText | & $FilePath @Arguments 2>&1
    }

    if ($null -ne $output) {
        foreach ($line in @($output)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$line)) {
                Write-Log ([string]$line)
            }
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE: $FilePath"
    }

    return (@($output) -join "`n").Trim()
}

function Invoke-PsqlCommand {
    param(
        [string]$UserName,
        [string]$Password,
        [string]$Database,
        [string]$Sql,
        [switch]$Quiet
    )

    $env:PGPASSWORD = $Password
    $script:CurrentPassword = $Password
    $script:CurrentUser = $UserName

    $arguments = @(
        "-X",
        "-h", $DbHost,
        "-p", "$Port",
        "-U", $UserName,
        "-d", $Database,
        "-v", "ON_ERROR_STOP=1"
    )

    if ($Quiet) {
        $arguments += @("-tA", "-c", $Sql)
        return Invoke-ExternalChecked -FilePath $psqlPath -Arguments $arguments
    }

    return Invoke-ExternalChecked -FilePath $psqlPath -Arguments ($arguments + @("-c", $Sql))
}

function Invoke-PgReady {
    $env:PGPASSWORD = $SuperPassword
    $script:CurrentPassword = $SuperPassword
    $script:CurrentUser = $SuperUser

    return Invoke-ExternalChecked -FilePath $pgIsReadyPath -Arguments @(
        "-h", $DbHost,
        "-p", "$Port",
        "-U", $SuperUser
    )
}

function Test-DatabaseReady {
    try {
        Invoke-PgReady | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

Set-Content -Path $logPath -Value ""
Write-Log "Starting In-Remedy database bootstrap."

Assert-ValidIdentifier -Value $AppUser -Name "AppUser"
Assert-ValidIdentifier -Value $DatabaseName -Name "DatabaseName"

if (!(Test-Path $psqlPath)) {
    throw "psql.exe was not found in $PgBinDir"
}

if (!(Test-Path $pgIsReadyPath)) {
    throw "pg_isready.exe was not found in $PgBinDir"
}

if (![string]::IsNullOrWhiteSpace($ServiceName)) {
    $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($null -ne $service) {
        Write-Log "Found PostgreSQL service '$ServiceName' with status '$($service.Status)'."
        if ($service.Status -ne "Running") {
            Write-Log "Starting PostgreSQL service '$ServiceName'."
            Start-Service -Name $ServiceName
        }
    }
    else {
        Write-Log "Service '$ServiceName' was not found. Continuing without service start."
    }
}

$databaseReady = $false
for ($attempt = 1; $attempt -le 90; $attempt++) {
    if (Test-DatabaseReady) {
        $databaseReady = $true
        Write-Log "PostgreSQL became ready on attempt $attempt."
        break
    }

    Start-Sleep -Seconds 1
}

if (-not $databaseReady) {
    throw "PostgreSQL did not become ready on $DbHost:$Port within 90 seconds."
}

$quotedAppUser = '"' + $AppUser.Replace('"', '""') + '"'
$quotedDatabase = '"' + $DatabaseName.Replace('"', '""') + '"'
$escapedAppUser = $AppUser.Replace("'", "''")
$escapedDatabaseName = $DatabaseName.Replace("'", "''")
$escapedAppPassword = $AppPassword.Replace("'", "''")

$roleSql = @"
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '$escapedAppUser') THEN
        CREATE ROLE $quotedAppUser LOGIN PASSWORD '$escapedAppPassword';
    ELSE
        ALTER ROLE $quotedAppUser WITH LOGIN PASSWORD '$escapedAppPassword';
    END IF;
END
$$;
"@

Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database "postgres" -Sql $roleSql | Out-Null

$databaseExists = Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database "postgres" -Sql "SELECT 1 FROM pg_database WHERE datname = '$escapedDatabaseName';" -Quiet
if ($databaseExists -ne "1") {
    Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database "postgres" -Sql "CREATE DATABASE $quotedDatabase OWNER $quotedAppUser;" | Out-Null
}
else {
    Write-Log "Database '$DatabaseName' already exists."
}

Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database "postgres" -Sql "ALTER DATABASE $quotedDatabase OWNER TO $quotedAppUser;" | Out-Null

$permissionsSql = @"
ALTER SCHEMA public OWNER TO $quotedAppUser;
GRANT ALL ON SCHEMA public TO $quotedAppUser;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO $quotedAppUser;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO $quotedAppUser;
GRANT ALL PRIVILEGES ON ALL FUNCTIONS IN SCHEMA public TO $quotedAppUser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO $quotedAppUser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO $quotedAppUser;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON FUNCTIONS TO $quotedAppUser;
"@

Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database $DatabaseName -Sql $permissionsSql | Out-Null

$roleValidation = Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database "postgres" -Sql "SELECT 1 FROM pg_roles WHERE rolname = '$escapedAppUser';" -Quiet
if ($roleValidation -ne "1") {
    throw "Role '$AppUser' was not created successfully."
}

$databaseValidation = Invoke-PsqlCommand -UserName $SuperUser -Password $SuperPassword -Database "postgres" -Sql "SELECT 1 FROM pg_database WHERE datname = '$escapedDatabaseName';" -Quiet
if ($databaseValidation -ne "1") {
    throw "Database '$DatabaseName' was not created successfully."
}

$loginValidation = Invoke-PsqlCommand -UserName $AppUser -Password $AppPassword -Database $DatabaseName -Sql "SELECT current_user;" -Quiet
if ($loginValidation -ne $AppUser) {
    throw "Application login validation failed for '$AppUser'."
}

$config = @{
    AppUrl = $AppUrl
    ConnectionString = "Host=$DbHost;Port=$Port;Database=$DatabaseName;Username=$AppUser;Password=$AppPassword"
}

$config | ConvertTo-Json | Set-Content -Path $configPath -Encoding UTF8
Write-Log "Wrote application config to '$configPath'."
Write-Log "Bootstrap completed successfully."
