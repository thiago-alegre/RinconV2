param(
    [string]$Version = (Get-Date -Format 'yyyyMMdd-HHmm')
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artifacts = Join-Path $repo 'artifacts'
$packageName = "rinconv2-$Version"
$staging = Join-Path $artifacts $packageName
$app = Join-Path $staging 'app'
$zip = Join-Path $artifacts "$packageName.zip"

if ($Version -notmatch '^[A-Za-z0-9._-]+$') {
    throw 'Version solo puede contener letras, números, punto, guion y guion bajo.'
}

foreach ($target in @($staging, $zip)) {
    if (Test-Path -LiteralPath $target) {
        $resolved = (Resolve-Path -LiteralPath $target).Path
        if (-not $resolved.StartsWith($artifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Destino inesperado: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $app -Force | Out-Null

Push-Location $repo
try {
    dotnet build .\Rincon.sln -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación Release.' }

    dotnet ef migrations has-pending-model-changes `
        --project .\Rincon.DataAccess `
        --startup-project .\Rincon `
        --configuration Release `
        --no-build
    if ($LASTEXITCODE -ne 0) { throw 'El modelo tiene cambios sin migración.' }

    dotnet publish .\Rincon\Rincon.csproj `
        -c Release `
        --no-build `
        --no-restore `
        -o $app
    if ($LASTEXITCODE -ne 0) { throw 'Falló la publicación.' }

    dotnet ef migrations script 0 `
        --project .\Rincon.DataAccess `
        --startup-project .\Rincon `
        --configuration Release `
        --no-build `
        --output (Join-Path $staging 'migrate-empty-database.sql')
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el esquema para la base vacía.' }

    @(
        "Version=$Version"
        "GeneratedUtc=$([DateTime]::UtcNow.ToString('O'))"
        'Framework=net8.0'
        'Deployment=framework-dependent'
        'Database=PostgreSQL'
        'DatabaseSource=empty database plus migrate-empty-database.sql'
        'ProductImages=/var/lib/rinconv2/uploads/products (not included in releases)'
    ) | Set-Content -LiteralPath (Join-Path $staging 'manifest.txt') -Encoding UTF8

    Get-ChildItem -LiteralPath $staging -Recurse -File |
        Sort-Object FullName |
        ForEach-Object {
            $relative = [IO.Path]::GetRelativePath($staging, $_.FullName).Replace('\', '/')
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            "$hash  $relative"
        } | Set-Content -LiteralPath (Join-Path $staging 'SHA256SUMS') -Encoding ASCII

    Compress-Archive -LiteralPath $staging -DestinationPath $zip -CompressionLevel Optimal
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Host "Paquete: $zip"
    Write-Host "SHA256: $zipHash"
}
finally {
    Pop-Location
}
