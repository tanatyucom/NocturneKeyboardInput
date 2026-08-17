param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\smt3hd"
)

$ErrorActionPreference = "Stop"
$projectRoot = $PSScriptRoot
$outputRoot = Join-Path $projectRoot "dist\NocturneKeyboardInput"
$modsOutput = Join-Path $outputRoot "Mods"
$helperOutput = Join-Path $outputRoot "UserData\NocturneKeyboardInput"

dotnet build (Join-Path $projectRoot "src\NocturneKeyboardInput.csproj") -c Release -p:GameDir="$GameDir"
dotnet build (Join-Path $projectRoot "src\NameInputHelper\NameInputHelper.csproj") -c Release

New-Item -ItemType Directory -Force -Path $modsOutput | Out-Null
New-Item -ItemType Directory -Force -Path $helperOutput | Out-Null

Copy-Item -LiteralPath (Join-Path $projectRoot "src\bin\Release\net6.0\NocturneKeyboardInput.dll") -Destination $modsOutput -Force

$helperBuild = Join-Path $projectRoot "src\NameInputHelper\bin\Release\net6.0-windows"
foreach ($name in @("NameInputHelper.exe", "NameInputHelper.dll", "NameInputHelper.deps.json", "NameInputHelper.runtimeconfig.json")) {
    Copy-Item -LiteralPath (Join-Path $helperBuild $name) -Destination $helperOutput -Force
}

Write-Host "Release files created at: $outputRoot"
