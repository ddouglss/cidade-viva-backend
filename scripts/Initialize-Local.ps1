[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
Push-Location $repoPath
try {
    $envFile = Join-Path $repoPath '.env'
    if (-not (Test-Path -LiteralPath $envFile)) { Copy-Item -LiteralPath (Join-Path $repoPath '.env.example') -Destination $envFile }
    $lines = [Collections.Generic.List[string]]::new()
    Get-Content -LiteralPath $envFile | ForEach-Object { $lines.Add($_) }
    $variables = @{}
    foreach ($line in $lines) {
        if ($line -match '^\s*([^#=\s]+)=(.*)$') { $variables[$Matches[1]] = $Matches[2].Trim() }
    }
    function Set-LocalVariable([string]$name, [string]$value) {
        $variables[$name] = $value
        $index = -1
        for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match ('^' + [regex]::Escape($name) + '=')) { $index = $i; break } }
        if ($index -ge 0) { $lines[$index] = "$name=$value" } else { $lines.Add("$name=$value") }
    }
    foreach ($key in @('POSTGRES_PASSWORD','MQTT_BACKEND_PASSWORD','MQTT_SIMULATOR_PASSWORD','Bootstrap__viewer__Password','Bootstrap__operator__Password')) {
        if (-not $variables[$key] -or $variables[$key].StartsWith('CHANGE_ME')) {
            $generated = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_')
            Set-LocalVariable $key $generated
        }
    }
    Set-LocalVariable 'Database__Password' $variables['POSTGRES_PASSWORD']
    Set-LocalVariable 'Mqtt__Password' $variables['MQTT_BACKEND_PASSWORD']
    foreach ($role in @('viewer','operator')) {
        if (-not $variables["Bootstrap__${role}__Username"]) { Set-LocalVariable "Bootstrap__${role}__Username" $role }
    }
    Set-LocalVariable 'City__Enabled' 'true'
    [IO.File]::WriteAllLines($envFile, $lines, [Text.UTF8Encoding]::new($false))
    foreach ($key in $variables.Keys) { [Environment]::SetEnvironmentVariable($key, $variables[$key], 'Process') }
    # Preserve active broker credentials. Generate password file only on first initialization.
    if (-not (Test-Path -LiteralPath 'infra/mosquitto/secrets/passwd')) {
        & ./infra/mosquitto/generate-secrets.ps1
        if ($LASTEXITCODE -ne 0) { throw 'MQTT credential generation failed.' }
    }
    docker compose --env-file .env -f infra/compose.yaml up -d --wait
    if ($LASTEXITCODE -ne 0) { throw 'Local services unavailable.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'EF tool restore failed.' }
    dotnet build CidadeViva.sln --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    dotnet run --no-build --project src/CidadeViva.Api -- --initialize-database
    if ($LASTEXITCODE -ne 0) { throw 'Database initialization failed.' }
    Write-Output 'Local database initialized. Login usernames and passwords are in the ignored .env file (Bootstrap__*). No passwords were printed.'
} finally { Pop-Location }
