# Check-Sign.ps1
# ---------------------------------------------------------------------------
# 看一眼程序有没有签名、签名有没有效、发布者是谁。
#
#   powershell -ExecutionPolicy Bypass -File .\Check-Sign.ps1
#   powershell -ExecutionPolicy Bypass -File .\Check-Sign.ps1 -Exe "D:\...\KeyboardOG.exe"
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [string]$Exe
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$appDir = Split-Path -Parent $here

if (-not $Exe) {
    $Exe = Join-Path $appDir 'bin\Release\KeyboardOG.exe'
    if (-not (Test-Path $Exe)) { $Exe = Join-Path $appDir 'bin\Debug\KeyboardOG.exe' }
}

if (-not (Test-Path $Exe)) { throw "找不到文件：$Exe" }

$dir = Split-Path -Parent $Exe
$files = @(Get-Item $Exe) + @(Get-ChildItem $dir -Filter '*.dll' -ErrorAction SilentlyContinue)

Write-Host "=== 签名状态 ===" -ForegroundColor Cyan
foreach ($f in $files) {
    $s = Get-AuthenticodeSignature $f.FullName
    $who = if ($s.SignerCertificate) { $s.SignerCertificate.Subject } else { '-' }
    $color = if ($s.Status -eq 'Valid') { 'Green' } else { 'Yellow' }

    Write-Host ("  {0,-22} {1,-14} {2}" -f $f.Name, $s.Status, $who) -ForegroundColor $color

    if ($s.TimeStamperCertificate) {
        Write-Host ("     时间戳: {0}" -f $s.TimeStamperCertificate.Subject) -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "=== 「来自其他计算机」标记 ===" -ForegroundColor Cyan
$blocked = 0
foreach ($f in $files) {
    $s = Get-Item $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
    if ($s) { $blocked++ }
}
if ($blocked -eq 0) {
    Write-Host "  没有被标记的文件" -ForegroundColor Green
} else {
    Write-Host "  有 $blocked 个文件带标记（跑 Unblock-Files.ps1 去掉）" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== 本机信任情况 ===" -ForegroundColor Cyan
$rootTrust = Get-ChildItem 'Cert:\CurrentUser\Root', 'Cert:\LocalMachine\Root' -ErrorAction SilentlyContinue |
             Where-Object { $_.Subject -like '*LGGZ*' }
$pubTrust  = Get-ChildItem 'Cert:\LocalMachine\TrustedPublisher' -ErrorAction SilentlyContinue |
             Where-Object { $_.Subject -like '*LGGZ*' }

Write-Host ("  受信任的根     : " + $(if ($rootTrust) { "已安装" } else { "未安装" }))
Write-Host ("  受信任的发布者 : " + $(if ($pubTrust) { "已安装" } else { "未安装（UAC 里会显示「未知发布者」）" }))
Write-Host ""
Write-Host "装证书：以管理员身份跑 Install-Trust.ps1" -ForegroundColor Yellow
