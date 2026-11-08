[CmdletBinding()]
param([string]$Url = 'http://127.0.0.1:5080')
$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
Push-Location $repoPath
try {
    if (-not (Test-Path -LiteralPath '.env')) { throw 'Execute scripts/Initialize-Local.ps1 first.' }
    foreach ($line in Get-Content -LiteralPath '.env') {
        if ($line -match '^\s*([^#=\s]+)=(.*)$') { [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2].Trim(), 'Process') }
    }
    dotnet run --no-build --project src/CidadeViva.Api --urls $Url
    exit $LASTEXITCODE
} finally { Pop-Location }
