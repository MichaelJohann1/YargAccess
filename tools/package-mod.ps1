
param(
    [string] $Version = "dev",
    [string] $ModDll = "$PSScriptRoot\..\bin\Release\YargAccess.dll",
    [string] $SpeechDir = "$env:LOCALAPPDATA\YARC\YARG Installs\yarg-access-stable\installation",
    [string] $OutDir = "$PSScriptRoot\..\dist"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $ModDll)) { throw "Mod DLL not found: $ModDll (build the mod first)" }

$stage = Join-Path $env:TEMP "yarg-access-package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory "$stage\Mods" | Out-Null
Copy-Item $ModDll "$stage\Mods\YargAccess.dll"
New-Item -ItemType Directory "$stage\Mods\YargAccess" | Out-Null
Copy-Item "$PSScriptRoot\..\LICENSE" "$stage\Mods\YargAccess\LICENSE.txt"
Copy-Item "$PSScriptRoot\..\THIRD-PARTY-LICENSES.md" "$stage\Mods\YargAccess\THIRD-PARTY-LICENSES.md"
foreach ($dll in @("UniversalSpeech.dll", "nvdaControllerClient.dll", "ZDSRAPI.dll")) {
    $src = Join-Path $SpeechDir $dll
    if (-not (Test-Path $src)) { throw "Speech DLL not found: $src" }
    Copy-Item $src "$stage\$dll"
}
Copy-Item "$PSScriptRoot\..\lib\nvdaControllerClient64.dll" "$stage\nvdaControllerClient64.dll"

New-Item -ItemType Directory -Force $OutDir | Out-Null
$zip = Join-Path $OutDir "YargAccess-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path "$stage\*" -DestinationPath $zip
Remove-Item $stage -Recurse -Force
Write-Host "Wrote $zip"
