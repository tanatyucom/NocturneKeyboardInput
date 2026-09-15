param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\smt3hd",
    [string]$Version = "0.18.3"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$outputRoot = Join-Path $projectRoot "dist\NocturneKeyboardInput"
$zipPath = Join-Path $projectRoot "dist\NocturneKeyboardInput-v$Version.zip"
$modsOutput = Join-Path $outputRoot "Mods"
$helperOutput = Join-Path $outputRoot "UserData\NocturneKeyboardInput"

if (Test-Path -LiteralPath $outputRoot) {
    Remove-Item -LiteralPath $outputRoot -Recurse -Force
}

dotnet build (Join-Path $projectRoot "src\NocturneKeyboardInput.csproj") -c Release -p:GameDir="$GameDir"
dotnet build (Join-Path $projectRoot "src\NameInputHelper\NameInputHelper.csproj") -c Release

New-Item -ItemType Directory -Force -Path $modsOutput | Out-Null
New-Item -ItemType Directory -Force -Path $helperOutput | Out-Null

Copy-Item -LiteralPath (Join-Path $projectRoot "src\bin\Release\net6.0\NocturneKeyboardInput.dll") -Destination $modsOutput -Force

$helperBuild = Join-Path $projectRoot "src\NameInputHelper\bin\Release\net6.0-windows"
foreach ($name in @("NameInputHelper.exe", "NameInputHelper.dll", "NameInputHelper.deps.json", "NameInputHelper.runtimeconfig.json")) {
    Copy-Item -LiteralPath (Join-Path $helperBuild $name) -Destination $helperOutput -Force
}

Copy-Item -LiteralPath (Join-Path $projectRoot "README.md") -Destination $outputRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot "LICENSE") -Destination $outputRoot -Force

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
$releaseItems = Get-ChildItem -LiteralPath $outputRoot
Compress-Archive -Path $releaseItems.FullName -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Release files created at: $outputRoot"
Write-Host "Release archive created at: $zipPath"
