# Unblock-Files.ps1
# ---------------------------------------------------------------------------
# Does one thing: removes the Mark-of-the-Web (Zone.Identifier) from files.
#
# That mark is the source of the "warnings after moving the app":
#   · "This file came from another computer and might be blocked"
#   · "Windows protected your PC" / "Windows cannot access the specified device, path, or file"
#   · some apps silently lose their dlls after extraction and report "missing xxx.dll"
#
# Run it once after copying the app; **no administrator needed**:
#   powershell -ExecutionPolicy Bypass -File .\Unblock-Files.ps1
#   powershell -ExecutionPolicy Bypass -File .\Unblock-Files.ps1 -Path "D:\some\folder"
#
# If you transfer a zip: copy the zip -> Unblock-File it -> then extract,
# so the extracted files carry no mark (running this script afterwards works too).
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # Folder to process (default: parent of this script = the KeyboardOG folder)
    [string]$Path,

    # Recurse into subfolders
    [bool]$Recurse = $true,

    # Show every processed file (summary only by default)
    [switch]$Verbose2
)

$ErrorActionPreference = 'Stop'
if (-not $Path) {
    $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Path = Split-Path -Parent $here
}

if (-not (Test-Path $Path)) { throw "folder does not exist: $Path" }

Write-Host "=== remove Mark-of-the-Web ===" -ForegroundColor Cyan
Write-Host "  folder: $Path"

$files = Get-ChildItem -Path $Path -Recurse:$Recurse -File -ErrorAction SilentlyContinue
$blocked = 0
$total = 0

foreach ($f in $files) {
    $total++
    $has = $false
    try {
        $s = Get-Item -Path $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
        if ($s) { $has = $true }
    } catch { }

    if ($has) {
        $blocked++
        if ($Verbose2) { Write-Host "  unblocked: $($f.FullName)" }
    }

    # Unblock-File is a no-op for unmarked files, so just call it
    try { Unblock-File -Path $f.FullName -ErrorAction SilentlyContinue } catch { }

    # Fallback: delete the alternate data stream directly
    if ($has) {
        try { Remove-Item -Path $f.FullName -Stream 'Zone.Identifier' -Force -ErrorAction SilentlyContinue } catch { }
    }
}

Write-Host ""
Write-Host "  $total files, $blocked were marked, all cleared." -ForegroundColor Green
Write-Host ""
Write-Host 'Notes: to remove "Unknown publisher" too, sign the app and trust the certificate locally:' -ForegroundColor Yellow
Write-Host "      see README.md / Install-Trust.ps1 in this folder (the latter needs admin)."
