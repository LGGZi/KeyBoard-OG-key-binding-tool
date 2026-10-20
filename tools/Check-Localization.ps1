# Check that every Chinese literal in the UI has an English counterpart in Language.cs.
#
# Usage (from the KeyboardOG folder):
#     powershell -NoProfile -ExecutionPolicy Bypass -File tools\Check-Localization.ps1
# Exit code 0 = all translated; 1 = missing entries (file:line and text listed, report written to tools\localization-report.txt).
#
# Notes:
#   · trailing comments (// ...) are stripped first, so Chinese in comments is ignored;
#   · literals starting with '#' are skipped (ini comments, never shown in the UI);
#   · L.T("zh","en") / L.F("zh","en",...) carry English inline and need no table entry (multi-line calls included);
#   · placeholder numbers are ignored when comparing ("{0} entries" equals "{1} entries");
#   · Language.cs itself is skipped.

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$langPath = Join-Path $root 'Language.cs'
if (-not (Test-Path $langPath)) { Write-Host "$langPath not found" -ForegroundColor Red; exit 2 }

$lang = [System.IO.File]::ReadAllText($langPath, [System.Text.Encoding]::UTF8)

$keys = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($m in [regex]::Matches($lang, 'Add\(m,\s*"((?:[^"\\]|\\.)*)"')) {
    $k = [regex]::Replace($m.Groups[1].Value, '\{\d+', '{0}')
    [void]$keys.Add($k)
}

function Remove-Comment([string]$line) {
    $sb = New-Object System.Text.StringBuilder
    $inStr = $false; $esc = $false
    for ($i = 0; $i -lt $line.Length; $i++) {
        $ch = $line[$i]
        if ($inStr) {
            [void]$sb.Append($ch)
            if ($esc) { $esc = $false }
            elseif ($ch -eq '\') { $esc = $true }
            elseif ($ch -eq '"') { $inStr = $false }
        }
        elseif ($ch -eq '"') { $inStr = $true; [void]$sb.Append($ch) }
        elseif ($ch -eq '/' -and ($i + 1) -lt $line.Length -and $line[$i + 1] -eq '/') { break }
        else { [void]$sb.Append($ch) }
    }
    return $sb.ToString()
}

$files = Get-ChildItem $root -Recurse -Include *.cs -File |
    Where-Object { $_.Name -ne 'Language.cs' -and $_.FullName -notmatch '\\obj\\|\\bin\\|\\tools\\' }

$missing = New-Object 'System.Collections.Generic.List[object]'
$inlineTotal = 0

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)

    # Inline bilingual calls: matched over the whole text, both literals recorded as "line|column" and excluded by position
    $inline = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($c in [regex]::Matches($text, 'L\.(T|F)\(\s*"((?:[^"\\]|\\.)*)"\s*,\s*(?:\r?\n\s*)?"((?:[^"\\]|\\.)*)"')) {
        foreach ($g in @(2, 3)) {
            $pos = $c.Groups[$g].Index
            $upto = $text.Substring(0, $pos)
            $ln = ([regex]::Matches($upto, "\n")).Count + 1
            $col = $pos - ($upto.LastIndexOf("`n") + 1)
            [void]$inline.Add("$ln|$col")
            $inlineTotal++
        }
    }

    $lineNo = 0
    foreach ($rawLine in ($text -split "`n")) {
        $lineNo++
        $line = Remove-Comment $rawLine
        foreach ($m in [regex]::Matches($line, '"((?:[^"\\]|\\.)*)"')) {
            if ($inline.Contains("$lineNo|$($m.Groups[1].Index)")) { continue }
            $lit = $m.Groups[1].Value
            if ($lit -notmatch '[\u4e00-\u9fff]') { continue }
            if ($lit.StartsWith('#')) { continue }
            $norm = [regex]::Replace($lit, '\{\d+', '{0}')
            if (-not $keys.Contains($norm)) {
                $missing.Add([pscustomobject]@{ File = $f.Name; Line = $lineNo; Text = $lit })
            }
        }
    }
}

$report = Join-Path $PSScriptRoot 'localization-report.txt'
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("Language.cs entries: $($keys.Count)")
[void]$sb.AppendLine("missing English translations: $($missing.Count)")
[void]$sb.AppendLine('')
foreach ($x in $missing) { [void]$sb.AppendLine(("{0}:{1}  {2}" -f $x.File, $x.Line, $x.Text)) }
[System.IO.File]::WriteAllText($report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))

Write-Host "Language.cs entries: $($keys.Count)    inline bilingual literals: $inlineTotal    missing English: $($missing.Count)"
if ($missing.Count -eq 0) { Write-Host "OK: every UI string has an English translation" -ForegroundColor Green; exit 0 }
Write-Host "see $report" -ForegroundColor Yellow
exit 1
