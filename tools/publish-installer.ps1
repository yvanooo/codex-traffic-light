param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\CodexTrafficLight.App\CodexTrafficLight.App.csproj"
$publishDir = Join-Path $repoRoot "dist\CodexTrafficLight-installer-files"
$installerScript = Join-Path $repoRoot "installer\CodexTrafficLight.iss"

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    /p:PublishSingleFile=false `
    -o $publishDir

$isccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)

$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        $iscc = $command.Source
    }
}

if (-not $iscc) {
    Write-Host "Installer files published to: $publishDir"
    Write-Host "Inno Setup 6 is not installed, so the setup EXE was not built."
    Write-Host "Install Inno Setup 6, then run this script again."
    exit 2
}

& $iscc $installerScript
