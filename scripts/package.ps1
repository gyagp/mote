param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseRoot = Join-Path $projectRoot '.release'
$packageName = "Mote-v$Version-win-x64"
$stage = Join-Path $releaseRoot ("staging-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null

Push-Location $projectRoot
try {
    dotnet run --project tests/Mote.Placement.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Layout tests failed.' }

    dotnet publish Mote.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None -p:DebugSymbols=false "-p:Version=$Version" -o $stage --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }

    Copy-Item -LiteralPath 'README.md', 'CHANGELOG.md' -Destination $stage
    $previewDirectory = Join-Path $stage 'artifacts'
    New-Item -ItemType Directory -Path $previewDirectory | Out-Null
    Copy-Item -LiteralPath 'artifacts/mote-design.png' -Destination $previewDirectory

    $archive = Join-Path $releaseRoot "$packageName.zip"
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $archive -CompressionLevel Optimal -Force
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $packageName.zip" | Set-Content -LiteralPath (Join-Path $releaseRoot "$packageName.sha256") -Encoding ascii
    Write-Output "Package: $archive"
    Write-Output "Staging: $stage"
    Write-Output "SHA256:  $hash"
}
finally {
    Pop-Location
}
