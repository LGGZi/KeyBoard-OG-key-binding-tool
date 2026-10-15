# 检查界面里所有中文字面量在 Language.cs 里有没有对应的英文。
#
# 用法（在 KeyboardOG 目录下）：
#     powershell -NoProfile -ExecutionPolicy Bypass -File tools\Check-Localization.ps1
# 退出码 0 = 全部有翻译；1 = 有遗漏（列出 文件:行号 和文本，并写 tools\localization-report.txt）。
#
# 说明：
#   · 先剥掉行尾注释（// ...），注释里的中文不会误报；
#   · 以 '#' 开头的字面量跳过（那是写进 .ini 的注释，不显示在界面上）；
#   · L.T("中文","English") / L.F("中文","English",...) 自带英文，不要求进表（跨行写法也认）；
#   · 比对时忽略占位符编号（"{0} 条" 与 "{1} 条" 视为同一条），因为界面句子是拼出来的；
#   · Language.cs 自己不算。

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$langPath = Join-Path $root 'Language.cs'
if (-not (Test-Path $langPath)) { Write-Host "找不到 $langPath" -ForegroundColor Red; exit 2 }

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
    Where-Object { $_.Name -ne 'Language.cs' -and $_.FullName -notmatch '\\obj\\|\\bin\\' }

$missing = New-Object 'System.Collections.Generic.List[object]'
$inlineTotal = 0

foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)

    # 内联双语调用：在整段文本上匹配，记下两个字面量的"行号|列号"，按位置排除
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
[void]$sb.AppendLine("Language.cs 表项: $($keys.Count) 条")
[void]$sb.AppendLine("缺少英文翻译: $($missing.Count) 条")
[void]$sb.AppendLine('')
foreach ($x in $missing) { [void]$sb.AppendLine(("{0}:{1}  {2}" -f $x.File, $x.Line, $x.Text)) }
[System.IO.File]::WriteAllText($report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($true)))

Write-Host "Language.cs 表项: $($keys.Count)    内联双语字面量: $inlineTotal    缺少英文翻译: $($missing.Count)"
if ($missing.Count -eq 0) { Write-Host "OK: 所有界面中文都有英文翻译" -ForegroundColor Green; exit 0 }
Write-Host "详见 $report" -ForegroundColor Yellow
exit 1
