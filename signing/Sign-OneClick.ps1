# Sign-OneClick.ps1
# ---------------------------------------------------------------------------
# One step: build Release -> sign KeyboardOG.exe / HIDComm.dll -> verify.
#
# Double-click KeyboardOG\sign.cmd to run this, or call it from a shell.
# When releasing, just double-click sign.cmd: no MSBuild arguments to remember, no missed signature.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File .\signing\Sign-OneClick.ps1
#   powershell -ExecutionPolicy Bypass -File .\signing\Sign-OneClick.ps1 -NoBuild   # sign only, no build
#   powershell -ExecutionPolicy Bypass -File .\signing\Sign-OneClick.ps1 -MsBuild "C:\...\MSBuild.exe"
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [switch]$NoBuild,          # sign only, no rebuild (use the Visual Studio output)
    [string]$MsBuild,  # explicit MSBuild path
    [switch]$NoTimestamp  # skip the timestamp when offline
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$appDir = Split-Path -Parent $here
$proj = Join-Path $appDir 'KeyboardOG.csproj'
$outDir = Join-Path $appDir 'bin\Release'
$exe = Join-Path $outDir 'KeyboardOG.exe'

function Find-MSBuild {
    param([string]$Given)
    if ($Given -and (Test-Path $Given)) { return $Given }

    $fixed = 'D:\Microsoft VS\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path $fixed) { return $fixed }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $vs = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
        if ($vs) {
            $p = Join-Path $vs 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path $p) { return $p }
        }
    }
    return $null
}

Write-Host ''
Write-Host '=== 1/3 build Release ===' -ForegroundColor Cyan
if ($NoBuild) {
    Write-Host '  (-NoBuild: skip the build, sign the existing output)'
} else {
    $msb = Find-MSBuild $MsBuild
    if (-not $msb) { throw 'MSBuild not found. Pass -MsBuild, or use -NoBuild.' }
    Write-Host "  using $msb"

    # SignManifests=false: the ClickOnce manifest certificate in the csproj fails on ordinary machines with
    # MSB4018 / X509Store access denied; we only need the real Authenticode signature here.
    & $msb $proj /t:Rebuild /p:Configuration=Release /p:SignManifests=false /p:SignAssembly=false /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "build failed (exit $LASTEXITCODE)" }
}

if (-not (Test-Path $exe)) { throw "$exe not found; build once without -NoBuild and retry." }

Write-Host ''
Write-Host '=== 2/3 sign ===' -ForegroundColor Cyan
$signArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $here 'Sign-App.ps1'))
if ($NoTimestamp) { $signArgs += '-NoTimestamp' }
& powershell @signArgs
if ($LASTEXITCODE -ne 0) { throw "signing failed (exit $LASTEXITCODE)" }

Write-Host ''
Write-Host '=== 3/3 verify ===' -ForegroundColor Cyan
$bad = 0
foreach ($f in @('KeyboardOG.exe', 'HIDComm.dll')) {
    $p = Join-Path $outDir $f
    if (-not (Test-Path $p)) { Write-Host "  skipped (missing): $f" -ForegroundColor Yellow; continue }
    $sig = Get-AuthenticodeSignature $p
    $color = if ($sig.Status -eq 'Valid') { 'Green' } else { 'Red' }
    Write-Host ("  {0,-18} status={1}  signer={2}" -f $f, $sig.Status, $sig.SignerCertificate.Subject) -ForegroundColor $color
    if ($sig.Status -ne 'Valid') { $bad++ }
}

Write-Host ''
if ($bad -eq 0) {
    Write-Host "done: binaries in $outDir are signed and ready to use." -ForegroundColor Green
    exit 0
} else {
    Write-Host "$bad file(s) are not Valid, check the output above." -ForegroundColor Red
    exit 1
}
