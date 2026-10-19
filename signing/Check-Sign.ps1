# Check-Sign.ps1
# ---------------------------------------------------------------------------
# Check whether the binaries are signed, whether the signature is valid, and who the publisher is.
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

if (-not (Test-Path $Exe)) { throw "file not found: $Exe" }

$dir = Split-Path -Parent $Exe
$files = @(Get-Item $Exe) + @(Get-ChildItem $dir -Filter '*.dll' -ErrorAction SilentlyContinue)

Write-Host "=== signature status ===" -ForegroundColor Cyan
foreach ($f in $files) {
    $s = Get-AuthenticodeSignature $f.FullName
    $who = if ($s.SignerCertificate) { $s.SignerCertificate.Subject } else { '-' }
    $color = if ($s.Status -eq 'Valid') { 'Green' } else { 'Yellow' }

    Write-Host ("  {0,-22} {1,-14} {2}" -f $f.Name, $s.Status, $who) -ForegroundColor $color

    if ($s.TimeStamperCertificate) {
        Write-Host ("      timestamp: {0}" -f $s.TimeStamperCertificate.Subject) -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "=== Mark-of-the-Web ===" -ForegroundColor Cyan
$blocked = 0
foreach ($f in $files) {
    $s = Get-Item $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
    if ($s) { $blocked++ }
}
if ($blocked -eq 0) {
    Write-Host "  no files were marked" -ForegroundColor Green
} else {
    Write-Host "  $blocked file(s) are marked (run Unblock-Files.ps1 to clear)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== local trust status ===" -ForegroundColor Cyan
$rootTrust = Get-ChildItem 'Cert:\CurrentUser\Root', 'Cert:\LocalMachine\Root' -ErrorAction SilentlyContinue |
             Where-Object { $_.Subject -like '*LGGZ*' }
$pubTrust  = Get-ChildItem 'Cert:\LocalMachine\TrustedPublisher' -ErrorAction SilentlyContinue |
             Where-Object { $_.Subject -like '*LGGZ*' }

Write-Host ("  Trusted Root       : " + $(if ($rootTrust) { "installed" } else { "not installed" }))
Write-Host ("  Trusted Publishers : " + $(if ($pubTrust) { "installed" } else { "not installed (UAC shows 'Unknown publisher')" }))
Write-Host ""
Write-Host "certificate: run Install-Trust.ps1 as administrator" -ForegroundColor Yellow
