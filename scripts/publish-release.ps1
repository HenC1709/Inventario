param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$uiProject = Join-Path $repoRoot "src\TiendaBodegas.UI\TiendaBodegas.UI.csproj"
$publishDir = Join-Path $repoRoot "artifacts\publish\$Runtime"
$releaseDir = Join-Path $repoRoot "artifacts\release"
$zipPath = Join-Path $releaseDir "StockFlow-$Version-$Runtime-portable.zip"
$issPath = Join-Path $repoRoot "installer\StockFlow.iss"

if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

$selfContainedFlag = if ($SelfContained) { "true" } else { "false" }

Write-Host "Publicando StockFlow ($Configuration | $Runtime | SelfContained=$selfContainedFlag | SingleFile=true)..."
dotnet publish $uiProject `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedFlag `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -m:1 -nr:false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "La publicación de dotnet publish falló con código $LASTEXITCODE."
}

$publishedExe = Join-Path $publishDir "StockFlow.exe"
$standaloneExe = Join-Path $releaseDir "StockFlow-$Version-$Runtime.exe"
if (Test-Path $publishedExe) {
    Copy-Item $publishedExe $standaloneExe -Force
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Write-Host "Empaquetando archivo portable ZIP en $zipPath..."
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$isccCandidates = @(
    (Get-Command "iscc" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { $_ -and (Test-Path $_) }

$isccExe = $isccCandidates | Select-Object -First 1
if ($isccExe) {
    Write-Host "Compilando instalador de Windows con Inno Setup ($isccExe)..."
    & $isccExe $issPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup falló con código $LASTEXITCODE."
    }
} else {
    Write-Host "ISCC.exe (Inno Setup 6) no detectado. Se omitió la compilación del instalador .exe; el script está listo en $issPath."
}

Write-Host "Release generado en $releaseDir"
