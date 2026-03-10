param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "installer\AeKitToolsInstaller\AeKitToolsInstaller.csproj"
$outputPath = Join-Path $PSScriptRoot "dist\AeKitToolsInstaller"

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

dotnet publish `
    $projectPath `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:PublishTrimmed=false `
    /p:DebugType=none `
    -o $outputPath

Write-Host ""
Write-Host "Executavel gerado em:"
Write-Host "  $outputPath\AeKitToolsInstaller.exe"
