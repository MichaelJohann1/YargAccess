# Checks src\Lang\<code>.json against tools\lang-keys.json (run tools\extract-strings.ps1 first):
# keys missing or no longer used, empty translations, and placeholders ({0}, {move}) that differ from
# the English line. Exit code 1 when anything is missing or a placeholder differs.
#
#   tools\check-lang.ps1            every language
#   tools\check-lang.ps1 es-ES      one
param([string]$Code = "")
$ErrorActionPreference = "Stop"
# ConvertFrom-Json in Windows PowerShell 5.1 rejects keys that differ only in case ("Star Power" and
# "Star power" are both keys), so the files are read with a case-sensitive .NET parser.
Add-Type -AssemblyName System.Web.Extensions
$json = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$json.MaxJsonLength = [int]::MaxValue
$keys = [string[]]$json.DeserializeObject((Get-Content (Join-Path $PSScriptRoot "lang-keys.json") -Raw -Encoding UTF8))
$dir = Join-Path $PSScriptRoot "..\src\Lang"
$codes = if ($Code) { @($Code) } else { Get-ChildItem $dir -Filter *.json | ForEach-Object { $_.BaseName } }
$failed = $false

function Holders([string]$s) { ([regex]::Matches($s, '\{[A-Za-z0-9_]+\}') | ForEach-Object Value | Sort-Object) -join ' ' }

foreach ($c in $codes) {
    $path = Join-Path $dir "$c.json"
    if (-not (Test-Path $path)) { Write-Host "${c}: no file"; $failed = $true; continue }
    $obj = $json.DeserializeObject((Get-Content $path -Raw -Encoding UTF8))
    $map = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::Ordinal)
    foreach ($k in $obj.Keys) { $map[$k] = [string]$obj[$k] }
    $missing = @($keys | Where-Object { -not $map.ContainsKey($_) -or [string]::IsNullOrWhiteSpace($map[$_]) })
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($k in $keys) { [void]$set.Add($k) }
    $extra = @($map.Keys | Where-Object { -not $set.Contains($_) })
    $holders = @($keys | Where-Object { $map.ContainsKey($_) -and $map[$_] -and (Holders $_) -ne (Holders $map[$_]) })
    $same = @($keys | Where-Object { $map.ContainsKey($_) -and $map[$_] -ceq $_ -and $_ -match '[a-z]{3,} [a-z]{3,}' })
    Write-Host ("{0}: {1} of {2} translated, {3} missing, {4} unused, {5} placeholder mismatches, {6} left in English" -f $c, ($keys.Count - $missing.Count), $keys.Count, $missing.Count, $extra.Count, $holders.Count, $same.Count)
    foreach ($k in $missing | Select-Object -First 10) { Write-Host "  missing: $k" }
    foreach ($k in $holders | Select-Object -First 10) { Write-Host "  placeholders: $k  =>  $($map[$k])" }
    foreach ($k in $same | Select-Object -First 5) { Write-Host "  still English: $k" }
    if ($missing.Count -or $holders.Count) { $failed = $true }
}
if ($failed) { exit 1 }
