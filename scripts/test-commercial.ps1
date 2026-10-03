param([switch]$SkipRestore)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    Write-Host 'Requiere PostgreSQL descartable en 127.0.0.1:55439, usuario audit, DB rinconv2_hardening.'
    Write-Host 'La suite migra y agrega datos sintéticos solo en esa base; no usar una base de trabajo.'
    if (!$SkipRestore) {
        dotnet restore tests/AuditChecks/AuditChecks.csproj
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    }
    dotnet run --project tests/AuditChecks/AuditChecks.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Commercial regression failed' }
    dotnet publish Rincon/Rincon.csproj -c Release --no-restore -o artifacts/commercial-check
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
}
finally { Pop-Location }
