# Install-Trust.ps1
# ---------------------------------------------------------------------------
# Run on the TARGET machine after copying the app. It:
#   1. installs the LGGZ self-signed certificate into Trusted Root and Trusted Publishers
#         -> UAC shows LGGZ instead of "Unknown publisher"
#   2. removes the Mark-of-the-Web (Zone.Identifier) from the files
#         -> no more "file came from another computer" prompt, and SmartScreen
#            no longer blocks this file
#   3. (optional) adds a Windows Defender exclusion for the app folder
#
# Requires administrator rights:
#   powershell -ExecutionPolicy Bypass -File .\Install-Trust.ps1
#
# If you only need step 2 (no admin), use Unblock-Files.ps1.
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # App folder (default: parent of this script = the KeyboardOG folder)
    [string]$AppDir,

    # Certificate file (default: LGGZ-CodeSign.cer beside this script)
    [string]$CertFile,

    # Also import the .pfx into the current user so this machine can sign later
    [switch]$InstallPfx,

    # Add a Defender exclusion for the app folder
    [switch]$AddDefenderExclusion,

    # Do not self-elevate (used when the script re-invokes itself, or for manual admin runs)
    [switch]$NoElevate
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $AppDir) { $AppDir = Split-Path -Parent $here }
if (-not $CertFile) { $CertFile = Join-Path $here 'LGGZ-CodeSign.cer' }

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal($id)).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)
}

$isAdmin = Test-Admin

# If not admin, ask once and self-elevate (simpler than asking the user to right-click)
if (-not $isAdmin -and -not $NoElevate) {
    Write-Host "installing the certificate requires administrator rights." -ForegroundColor Yellow
    $ans = Read-Host "restart as administrator now? (Y/n)"
    if ($ans -ne 'n' -and $ans -ne 'N') {
        $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                     '-AppDir', "`"$AppDir`"", '-CertFile', "`"$CertFile`"", '-NoElevate')
        if ($InstallPfx)           { $argList += '-InstallPfx' }
        if ($AddDefenderExclusion) { $argList += '-AddDefenderExclusion' }
        try {
            Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argList
            Write-Host "reopened as administrator, check the new window." -ForegroundColor Green
        } catch {
            Write-Host "elevation failed: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "right-click -> Run as administrator, then run this script again."
        }
        exit
    }
}

Write-Host "=== LGGZ self-signed certificate install / unblock ===" -ForegroundColor Cyan
Write-Host "  app folder: $AppDir"
Write-Host "  certificate: $CertFile"
Write-Host "  admin   : $isAdmin"
Write-Host ""

# ---------------------------------------------------------------
# 1. Install the certificate (requires admin)
# ---------------------------------------------------------------
if (-not (Test-Path $CertFile)) {
    Write-Host "certificate file not found: $CertFile" -ForegroundColor Red
    Write-Host " (run Make-Cert.ps1 on the source machine and copy the generated .cer here)"
} elseif (-not $isAdmin) {
    Write-Host "not running as administrator, skipping certificate install." -ForegroundColor Yellow
    Write-Host '  -> to remove "Unknown publisher" completely, rerun this script as administrator.'
} else {
    Write-Host "importing certificate..." -ForegroundColor Cyan

    # Trusted Root Certification Authorities: makes the chain trusted
    Import-Certificate -FilePath $CertFile -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Write-Host "  imported into Trusted Root Certification Authorities" -ForegroundColor Green

    # Trusted Publishers: makes UAC accept this publisher
    Import-Certificate -FilePath $CertFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Write-Host "  imported into Trusted Publishers" -ForegroundColor Green

    if ($InstallPfx) {
        $pfx = Join-Path $here 'LGGZ-CodeSign.pfx'
        $infoPath = Join-Path $here 'cert-info.txt'
        $pwd = '123456'
        if (Test-Path $infoPath) {
            $l = Select-String -Path $infoPath -Pattern '^PfxPassword=' | Select-Object -First 1
            if ($l) { $pwd = ($l.Line -split '=', 2)[1].Trim() }
        }
        if (Test-Path $pfx) {
            $sec = ConvertTo-SecureString -String $pwd -AsPlainText -Force
            Import-PfxCertificate -FilePath $pfx -CertStoreLocation 'Cert:\CurrentUser\My' -Password $sec | Out-Null
            Write-Host "  private key imported (this machine can sign too)" -ForegroundColor Green
        } else {
            Write-Host "  $pfx not found, skipping private key import" -ForegroundColor Yellow
        }
    }
}

# ---------------------------------------------------------------
# 2. Remove the Mark-of-the-Web (no admin needed)
# ---------------------------------------------------------------
Write-Host ""
Write-Host "removing Mark-of-the-Web..." -ForegroundColor Cyan

$exts = @('*.exe', '*.dll', '*.config', '*.pdb', '*.ini', '*.csv')
$count = 0
if (Test-Path $AppDir) {
    foreach ($e in $exts) {
        Get-ChildItem -Path $AppDir -Filter $e -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $f = $_
            # Try the standard approach first
            try { Unblock-File -Path $f.FullName -ErrorAction Stop } catch { }
            # Fallback: delete the alternate data stream when Unblock-File does not work
            try {
                $streams = Get-Item -Path $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
                if ($streams) { Remove-Item -Path $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue }
            } catch { }
            $count++
        }
    }
}
Write-Host "  processed $count files" -ForegroundColor Green

# ---------------------------------------------------------------
# 3. Optional: Defender exclusion
# ---------------------------------------------------------------
if ($AddDefenderExclusion) {
    Write-Host ""
    Write-Host "adding Defender exclusion..." -ForegroundColor Cyan
    if (-not $isAdmin) {
        Write-Host "  administrator required, skipped" -ForegroundColor Yellow
    } else {
        try {
            Add-MpPreference -ExclusionPath $AppDir -ErrorAction Stop
            Write-Host "  Defender exclusion added: $AppDir" -ForegroundColor Green
        } catch {
            Write-Host "  add failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# ---------------------------------------------------------------
# 4. Result check
# ---------------------------------------------------------------
Write-Host ""
Write-Host "=== signature check ===" -ForegroundColor Cyan
$exe = Join-Path $AppDir 'bin\Release\KeyboardOG.exe'
if (-not (Test-Path $exe)) { $exe = Join-Path $AppDir 'KeyboardOG.exe' }

if (Test-Path $exe) {
    $sig = Get-AuthenticodeSignature $exe
    Write-Host "  file: $exe"
    Write-Host "  status: $($sig.Status)"
    if ($sig.SignerCertificate) {
        Write-Host "  signer: $($sig.SignerCertificate.Subject)"
    }
    if ($sig.Status -eq 'Valid') {
        Write-Host "  -> signature valid, UAC shows the publisher name." -ForegroundColor Green
    } else {
        Write-Host "  -> still $($sig.Status) (expected when the certificate is not installed on the target machine)." -ForegroundColor Yellow
    }
} else {
    Write-Host "  KeyboardOG.exe not found, skipping check." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "done." -ForegroundColor Green
Write-Host '  · "came from another computer" / SmartScreen blocking for this file: cleared.'
Write-Host '  · "Unknown publisher": after installing the certificate (admin) it shows LGGZ.'
Write-Host '  · the first run may still hit SmartScreen (self-signed certificates have no reputation): use "More info -> Run anyway";'
Write-Host "    removing this prompt entirely requires a CA-issued code-signing certificate."
