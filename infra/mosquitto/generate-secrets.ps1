[CmdletBinding()]
param(
    [string]$EnvFile = (Join-Path $PSScriptRoot "..\..\.env"),
    [string]$OutputPath = (Join-Path $PSScriptRoot "secrets\passwd"),
    [string]$MosquittoImage = "eclipse-mosquitto:2.0.22-openssl"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $EnvFile -PathType Leaf)) {
    throw "Arquivo .env nao encontrado em '$EnvFile'. Copie '.env.example' para '.env' e defina senhas locais antes de gerar os secrets do Mosquitto."
}

$envVars = @{}
foreach ($line in Get-Content -LiteralPath $EnvFile) {
    $trimmed = $line.Trim()
    if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith("#")) {
        continue
    }
    $parts = $trimmed -split "=", 2
    if ($parts.Count -eq 2) {
        $envVars[$parts[0].Trim()] = $parts[1].Trim()
    }
}

$requiredKeys = @(
    "MQTT_BACKEND_USER",
    "MQTT_BACKEND_PASSWORD",
    "MQTT_SIMULATOR_USER",
    "MQTT_SIMULATOR_PASSWORD"
)

foreach ($key in $requiredKeys) {
    if (-not $envVars.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($envVars[$key]) -or $envVars[$key].StartsWith("CHANGE_ME")) {
        throw "Variavel obrigatoria '$key' ausente ou ainda com valor placeholder em '.env'."
    }
}

$backendUser = $envVars["MQTT_BACKEND_USER"]
$simulatorUser = $envVars["MQTT_SIMULATOR_USER"]

if ($backendUser -eq $simulatorUser) {
    throw "MQTT_BACKEND_USER e MQTT_SIMULATOR_USER devem ser usuarios distintos."
}

foreach ($u in @($backendUser, $simulatorUser)) {
    if ($u -match "[:\s]") {
        throw "Nome de usuario MQTT invalido (nao pode conter ':' ou espacos)."
    }
}

$resolvedOutputPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
$secretsDir = Split-Path -Parent $resolvedOutputPath
if ([string]::IsNullOrWhiteSpace($secretsDir)) {
    throw "Caminho de saida invalido para o arquivo de senhas."
}

New-Item -ItemType Directory -Force -Path $secretsDir | Out-Null

$tempFileName = ".passwd.tmp.$([Guid]::NewGuid().ToString('N'))"
$tempFilePath = Join-Path $secretsDir $tempFileName

try {
    New-Item -ItemType File -Path $tempFilePath -Force | Out-Null

    # Gera hashes PBKDF2-SHA512 em arquivo temporario usando a imagem oficial fixada do Mosquitto sem expor senhas
    docker run --rm `
        -v "${secretsDir}:/mosquitto/secrets" `
        -e "BACKEND_USER=$backendUser" `
        -e "BACKEND_PASS=$($envVars['MQTT_BACKEND_PASSWORD'])" `
        -e "SIM_USER=$simulatorUser" `
        -e "SIM_PASS=$($envVars['MQTT_SIMULATOR_PASSWORD'])" `
        -e "TEMP_FILE=/mosquitto/secrets/$tempFileName" `
        $MosquittoImage `
        sh -c 'mosquitto_passwd -b -c "$TEMP_FILE" "$BACKEND_USER" "$BACKEND_PASS" && mosquitto_passwd -b "$TEMP_FILE" "$SIM_USER" "$SIM_PASS" && chmod 0644 "$TEMP_FILE"'

    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar mosquitto_passwd no container (exit code $LASTEXITCODE)."
    }

    if (-not (Test-Path -LiteralPath $tempFilePath -PathType Leaf)) {
        throw "Arquivo temporario de senhas nao foi gerado."
    }

    $lines = @(Get-Content -LiteralPath $tempFilePath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -ne 2) {
        throw "Validacao do arquivo temporario falhou: esperadas 2 entradas de usuario, encontradas $($lines.Count)."
    }

    $hasBackendHash = @(
        $lines | Where-Object {
            ($_.StartsWith("${backendUser}:`$7`$", [System.StringComparison]::Ordinal) -or
             $_.StartsWith("${backendUser}:`$6`$", [System.StringComparison]::Ordinal)) -and
            $_.Length -gt ($backendUser.Length + 20)
        }
    ).Count -eq 1

    $hasSimHash = @(
        $lines | Where-Object {
            ($_.StartsWith("${simulatorUser}:`$7`$", [System.StringComparison]::Ordinal) -or
             $_.StartsWith("${simulatorUser}:`$6`$", [System.StringComparison]::Ordinal)) -and
            $_.Length -gt ($simulatorUser.Length + 20)
        }
    ).Count -eq 1

    if (-not ($hasBackendHash -and $hasSimHash)) {
        throw "Validacao do arquivo temporario falhou: hashes de usuario em formato inesperado."
    }

    Move-Item -LiteralPath $tempFilePath -Destination $resolvedOutputPath -Force
    Write-Output "Arquivo de senhas do Mosquitto gerado e validado com sucesso em '$resolvedOutputPath' (ignorado pelo Git)."
}
finally {
    if (Test-Path -LiteralPath $tempFilePath) {
        Remove-Item -LiteralPath $tempFilePath -Force -ErrorAction SilentlyContinue
    }
}
