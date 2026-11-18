# Lists every line of the mod's text that goes through Loc.T or Loc.F (src\Core\Loc.cs) and writes them,
# sorted and without repeats, to tools\lang-keys.json: the keys every src\Lang\<code>.json translates.
#
#   tools\extract-strings.ps1
#
# The first argument of Loc.T / Loc.F must be a string literal, or literals joined with +.
# Anything else (a variable, a call) is reported, since it can't be listed for translators.
$ErrorActionPreference = "Stop"
$src = Join-Path $PSScriptRoot "..\src"
$out = Join-Path $PSScriptRoot "lang-keys.json"

function Read-Literal([string]$t, [int]$i) {
    # Returns @(value, nextIndex) for a regular or verbatim C# string literal starting at $i, or $null.
    $sb = New-Object System.Text.StringBuilder
    if ($t[$i] -eq '@' -and $t[$i + 1] -eq '"') {
        $i += 2
        while ($i -lt $t.Length) {
            if ($t[$i] -eq '"') { if ($t[$i + 1] -eq '"') { [void]$sb.Append('"'); $i += 2; continue }; return @($sb.ToString(), ($i + 1)) }
            [void]$sb.Append($t[$i]); $i++
        }
        return $null
    }
    if ($t[$i] -ne '"') { return $null }
    $i++
    while ($i -lt $t.Length) {
        $c = $t[$i]
        if ($c -eq '"') { return @($sb.ToString(), ($i + 1)) }
        if ($c -eq '\') {
            $n = $t[$i + 1]
            switch ($n) {
                'n' { [void]$sb.Append("`n") } 't' { [void]$sb.Append("`t") } 'r' { [void]$sb.Append("`r") }
                '0' { [void]$sb.Append([char]0) }
                'u' { [void]$sb.Append([char][Convert]::ToInt32($t.Substring($i + 2, 4), 16)); $i += 4 }
                default { [void]$sb.Append($n) }
            }
            $i += 2; continue
        }
        [void]$sb.Append($c); $i++
    }
    return $null
}

function Skip-Space([string]$t, [int]$i) {
    while ($i -lt $t.Length) {
        if ([char]::IsWhiteSpace($t[$i])) { $i++; continue }
        if ($t[$i] -eq '/' -and $t[$i + 1] -eq '/') { while ($i -lt $t.Length -and $t[$i] -ne "`n") { $i++ }; continue }
        break
    }
    return $i
}

$keys = New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal)
# Labels passed through a variable (Tutorials: main = "Next" / "Done", then Text(main)).
foreach ($k in "Next", "Done") { [void]$keys.Add($k) }
$bad = @()
foreach ($f in Get-ChildItem $src -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\obj\\' }) {
    $t = [IO.File]::ReadAllText($f.FullName)
    # Dialog button labels become YargAccess.Text.<label> keys through Tutorials.Text and EntryActions.T;
    # ModSettings.TryLocalize translates them with Loc.T, so their labels are keys too.
    $pattern = '\bLoc\.(T|F)\s*\('
    if ($f.Name -eq "Tutorials.cs") { $pattern = '(\bLoc\.(T|F)|(?<![\w.])Text)\s*\(' }
    if ($f.Name -eq "EntryActions.cs") { $pattern = '(\bLoc\.(T|F)|(?<![\w.])T)\s*\(' }
    foreach ($m in [regex]::Matches($t, $pattern)) {
        if ($f.Name -eq "Loc.cs") { continue }
        if ($t.Substring([Math]::Max(0, $m.Index - 14), [Math]::Min(14, $m.Index)) -match 'static string\s*$') { continue }
        $i = Skip-Space $t ($m.Index + $m.Length)
        $val = ""; $ok = $false
        while ($true) {
            $lit = Read-Literal $t $i
            if ($lit -eq $null) { break }
            $val += $lit[0]; $ok = $true
            $i = Skip-Space $t $lit[1]
            if ($t[$i] -eq '+') {
                $j = Skip-Space $t ($i + 1)
                if ($t[$j] -eq '"' -or ($t[$j] -eq '@' -and $t[$j + 1] -eq '"')) { $i = $j; continue }
                $ok = $false
            }
            break
        }
        $end = $t[$i]
        if ($ok -and ($end -eq ')' -or $end -eq ',')) { [void]$keys.Add($val) }
        else {
            $line = ($t.Substring(0, $m.Index) -split "`n").Count
            $bad += "$($f.Name):$line"
        }
    }
}
$json = $keys | ConvertTo-Json -Depth 2
if ($keys.Count -eq 1) { $json = "[" + $json + "]" }
[IO.File]::WriteAllText($out, $json, (New-Object Text.UTF8Encoding $false))
Write-Host "$($keys.Count) keys written to $out"
if ($bad.Count) { Write-Host "Not a literal (fix these): $($bad -join ', ')" }
