# Sign-App.ps1
# ---------------------------------------------------------------------------
# Sign KeyboardOG with the self-signed certificate from Make-Cert.ps1 (run on the source machine).
#
# After signing:
#   · once the target machine trusts the certificate (Install-Trust.ps1), UAC shows the publisher,
#     no longer "Unknown publisher"; the Digital Signatures tab shows the signature too.
#   · machines without the certificate still show "Unknown publisher", and SmartScreen still blocks
#     (self-signed certificates never build SmartScreen reputation) - this is expected.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\Sign-App.ps1
#   powershell -ExecutionPolicy Bypass -File .\Sign-App.ps1 -Exe "D:\...\KeyboardOG.exe"
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # By default signs the Release output (including HIDComm.dll)
    [string]$Exe,

    # Use this thumbprint when given; otherwise look it up via cert-info.txt / subject
    [string]$Thumbprint,

    # Skip the timestamp (for offline use)
    [switch]$NoTimestamp
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$appDir = Split-Path -Parent $here  # the KeyboardOG folder

if (-not $Exe) { $Exe = Join-Path $appDir 'bin\Release\KeyboardOG.exe' }
if (-not (Test-Path $Exe)) { throw "nothing to sign: $Exe" }

# ---------- find the certificate ----------
if (-not $Thumbprint) {
    $infoPath = Join-Path $here 'cert-info.txt'
    if (Test-Path $infoPath) {
        $line = Select-String -Path $infoPath -Pattern '^Thumbprint=' | Select-Object -First 1
        if ($line) { $Thumbprint = ($line.Line -split '=', 2)[1].Trim() }
    }
}

$cert = $null
if ($Thumbprint) {
    $cert = Get-ChildItem 'Cert:\CurrentUser\My' |
            Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
}
if (-not $cert) {
    # Fallback: find a code-signing certificate with a private key by subject
    $cert = Get-ChildItem 'Cert:\CurrentUser\My' |
            Where-Object { $_.Subject -like '*LGGZ*' -and $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending | Select-Object -First 1
}
if (-not $cert) {
    throw "signing certificate not found. Run Make-Cert.ps1 first."
}

Write-Host "using certificate: $($cert.Subject)" -ForegroundColor Cyan
Write-Host "  thumbprint: $($cert.Thumbprint)"
Write-Host "  expires: $($cert.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host ""

# ---------- files to sign: the exe plus dlls beside it ----------
$targets = @($Exe)
$dir = Split-Path -Parent $Exe
Get-ChildItem $dir -Filter '*.dll' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch '^System\.' } |
    ForEach-Object { $targets += $_.FullName }

$ts = 'http://timestamp.digicert.com'
$ok = 0; $fail = 0

foreach ($t in $targets) {
    Write-Host "signing $([System.IO.Path]::GetFileName($t)) ..." -NoNewline

    $signed = $false
    if (-not $NoTimestamp) {
        try {
            Set-AuthenticodeSignature -FilePath $t -Certificate $cert -HashAlgorithm SHA256 `
                -TimestampServer $ts -ErrorAction Stop | Out-Null
            $signed = $true
        } catch {
            Write-Host "  timestamp failed ($($_.Exception.Message.Trim())), signing without timestamp" -ForegroundColor Yellow
        }
    }
    if (-not $signed) {
        try {
            Set-AuthenticodeSignature -FilePath $t -Certificate $cert -HashAlgorithm SHA256 -ErrorAction Stop | Out-Null
            $signed = $true
        } catch {
            Write-Host " failed: $($_.Exception.Message)" -ForegroundColor Red
            $fail++
            continue
        }
    }

    $st = Get-AuthenticodeSignature $t
    Write-Host "  status=$($st.Status)" -ForegroundColor Green
    $ok++
}

Write-Host ""
Write-Host "done: $ok signed, $fail failed." -ForegroundColor Cyan
Write-Host ""
Write-Host "Notes:" -ForegroundColor Yellow
Write-Host "  · Status=UnknownError/UntrustedRoot means this machine does not trust the self-signed certificate yet,"
Write-Host "    trust it locally as well: Make-Cert.ps1 -TrustLocal (or install the .cer into Trusted Root Certification Authorities)."
Write-Host "  · on another machine, run Install-Trust.ps1 there as administrator,"
Write-Host '    the "Unknown publisher" and "came from another computer" warnings disappear.'
