Set-StrictMode -Version Latest

function Import-IumpLocalEnvironment {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepoRoot)

    $allowed = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@(
            'IUMP_DB_HOST',
            'IUMP_DB_PORT',
            'IUMP_DB_NAME',
            'IUMP_DB_USER',
            'IUMP_DB_PASSWORD',
            'IUMP_MIGRATION_PASSWORD',
            'IUMP_APP_PASSWORD',
            'IUMP_READONLY_PASSWORD'
        ),
        [StringComparer]::Ordinal
    )

    foreach ($name in @('.env.local', '.env')) {
        $path = Join-Path $RepoRoot $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        foreach ($line in Get-Content -LiteralPath $path) {
            $trimmed = $line.Trim()
            if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) { continue }
            $separator = $trimmed.IndexOf('=')
            if ($separator -le 0) { continue }
            $key = $trimmed.Substring(0, $separator).Trim()
            if (-not $allowed.Contains($key) -or
                -not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($key))) {
                continue
            }
            $value = $trimmed.Substring($separator + 1).Trim().Trim('"')
            [Environment]::SetEnvironmentVariable(
                $key, $value, [EnvironmentVariableTarget]::Process)
        }
    }
}

function Get-IumpEnvironmentValue {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Name)

    $value = [Environment]::GetEnvironmentVariable(
        $Name, [EnvironmentVariableTarget]::Process)
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = [Environment]::GetEnvironmentVariable(
            $Name, [EnvironmentVariableTarget]::User)
    }
    return $value
}

function Resolve-IumpPostgresCliRuntime {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [ValidateSet('Bootstrap', 'Migration', 'Application', 'Readonly')]
        [string]$Purpose = 'Application'
    )

    Import-IumpLocalEnvironment -RepoRoot $RepoRoot
    $psqlPath = 'C:\Program Files\PostgreSQL\18\bin\psql.exe'
    if (-not (Test-Path -LiteralPath $psqlPath -PathType Leaf)) {
        return [pscustomobject]@{
            Available = $false
            Classification = 'BLOCKED_BY_MISSING_TOOL'
            Evidence = 'The approved PostgreSQL 18 psql executable is unavailable.'
        }
    }

    $hostName = Get-IumpEnvironmentValue -Name 'IUMP_DB_HOST'
    $portText = Get-IumpEnvironmentValue -Name 'IUMP_DB_PORT'
    $database = Get-IumpEnvironmentValue -Name 'IUMP_DB_NAME'
    $bootstrapUser = Get-IumpEnvironmentValue -Name 'IUMP_DB_USER'
    $role = switch ($Purpose) {
        'Bootstrap' { $bootstrapUser }
        'Migration' { 'iump_migration' }
        'Application' { 'iump_app' }
        'Readonly' { 'iump_readonly' }
    }
    $passwordKey = switch ($Purpose) {
        'Bootstrap' { 'IUMP_DB_PASSWORD' }
        'Migration' { 'IUMP_MIGRATION_PASSWORD' }
        'Application' { 'IUMP_APP_PASSWORD' }
        'Readonly' { 'IUMP_READONLY_PASSWORD' }
    }
    $password = Get-IumpEnvironmentValue -Name $passwordKey

    if ($hostName -ne '127.0.0.1' -or $portText -ne '5433' -or
        $database -ne 'iump_dev' -or [string]::IsNullOrWhiteSpace($role) -or
        [string]::IsNullOrWhiteSpace($password)) {
        return [pscustomobject]@{
            Available = $false
            Classification = 'DATABASE_CONNECTION_RUNTIME_FAILURE'
            Evidence = 'The approved local PostgreSQL target or role credential is unavailable.'
        }
    }

    return [pscustomobject]@{
        Available = $true
        Classification = 'PASS'
        Evidence = "Approved PostgreSQL runtime resolved for $Purpose."
        PsqlPath = $psqlPath
        Host = $hostName
        Port = 5433
        Database = $database
        Username = $role
        Password = $password
    }
}

function Invoke-IumpPsql {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][object]$Runtime,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    if (-not $Runtime.Available) {
        throw 'DATABASE_CONNECTION_RUNTIME_FAILURE'
    }
    $previous = [Environment]::GetEnvironmentVariable(
        'PGPASSWORD', [EnvironmentVariableTarget]::Process)
    try {
        [Environment]::SetEnvironmentVariable(
            'PGPASSWORD', $Runtime.Password, [EnvironmentVariableTarget]::Process)
        & $Runtime.PsqlPath `
            -h $Runtime.Host `
            -p $Runtime.Port `
            -U $Runtime.Username `
            -d $Runtime.Database `
            --no-psqlrc `
            @Arguments
        $exitCode = [int]$LASTEXITCODE
    }
    finally {
        [Environment]::SetEnvironmentVariable(
            'PGPASSWORD', $previous, [EnvironmentVariableTarget]::Process)
    }
    $global:LASTEXITCODE = $exitCode
}
