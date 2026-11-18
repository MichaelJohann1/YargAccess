
param(
    [string] $Install = "$env:LOCALAPPDATA\YARC\YARG Installs\yarg-access-stable\installation",
    [string] $Donor   = "C:\Program Files (x86)\Steam\steamapps\common\PEAK"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Install)) { throw "YARG install not found: $Install" }
if (-not (Test-Path $Donor))   { throw "Donor install not found: $Donor" }

Write-Host "Install: $Install"
Write-Host "Donor:   $Donor"

if (-not (Test-Path "$Install\version.dll")) {
    Copy-Item "$Donor\version.dll" "$Install\version.dll"
    Write-Host "  + version.dll (MelonLoader bootstrap)"
}

robocopy "$Donor\MelonLoader" "$Install\MelonLoader" /E /NFL /NDL /NJH /NJS /NP /XD Logs /XF Latest.log | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy MelonLoader failed ($LASTEXITCODE)" }
Write-Host "  + MelonLoader\"

foreach ($dll in @("UniversalSpeech.dll", "nvdaControllerClient.dll", "ZDSRAPI.dll")) {
    Copy-Item "$Donor\$dll" "$Install\$dll" -Force
    Write-Host "  + $dll"
}
Copy-Item "$PSScriptRoot\..\lib\nvdaControllerClient64.dll" "$Install\nvdaControllerClient64.dll" -Force
Write-Host "  + nvdaControllerClient64.dll (NVDA SSML client, from lib\)"

foreach ($dir in @("Mods", "UserLibs", "UserData")) {
    if (-not (Test-Path "$Install\$dir")) {
        New-Item -ItemType Directory "$Install\$dir" | Out-Null
        Write-Host "  + $dir\"
    }
}

$cfg = "$Install\UserData\Loader.cfg"
if (-not (Test-Path $cfg)) {
    Copy-Item "$Donor\UserData\Loader.cfg" $cfg
}
$text = Get-Content $cfg -Raw
if ($text -match '(?m)^\s*hide_console\s*=\s*false') {
    $text = $text -replace '(?m)^\s*hide_console\s*=\s*false', 'hide_console = true'
    Set-Content $cfg $text -Encoding utf8
    Write-Host "  * Loader.cfg hide_console = true"
}

Write-Host ""
Write-Host "Done. Next: dotnet build src\YargAccess.csproj -c Release, then copy bin\Release\YargAccess.dll into the Mods folder."
