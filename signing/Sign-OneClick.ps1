# Sign-OneClick.ps1
# ---------------------------------------------------------------------------
# 一键：编译 Release -> 给 KeyboardOG.exe / HIDComm.dll 签名 -> 验证签名。
#
# 双击 KeyboardOG\sign.cmd 就会跑这个脚本（也可以用命令行跑）。
# 改完代码想发布时，直接双击 sign.cmd 即可：不用记 MSBuild 参数，也不会漏签名。
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File .\signing\Sign-OneClick.ps1
#   powershell -ExecutionPolicy Bypass -File .\signing\Sign-OneClick.ps1 -NoBuild   # 只签名，不编译
#   powershell -ExecutionPolicy Bypass -File .\signing\Sign-OneClick.ps1 -MsBuild "C:\...\MSBuild.exe"
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [switch]$NoBuild,          # 只签名，不重新编译（用 VS 编好的产物）
    [string]$MsBuild,          # 手动指定 MSBuild 路径
    [switch]$NoTimestamp       # 没有网络时不做时间戳
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
Write-Host '=== 1/3 编译 Release ===' -ForegroundColor Cyan
if ($NoBuild) {
    Write-Host '  （-NoBuild：跳过编译，直接签现有产物）'
} else {
    $msb = Find-MSBuild $MsBuild
    if (-not $msb) { throw '找不到 MSBuild。请用 -MsBuild 指定路径，或先改用 -NoBuild。' }
    Write-Host "  使用 $msb"

    # SignManifests=false：csproj 里那个 ClickOnce 清单签名证书在普通机器上会报
    # MSB4018 / X509Store 拒绝访问；这里只做真正的 Authenticode 签名，用不到它。
    & $msb $proj /t:Rebuild /p:Configuration=Release /p:SignManifests=false /p:SignAssembly=false /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw "编译失败（exit $LASTEXITCODE）" }
}

if (-not (Test-Path $exe)) { throw "找不到 $exe，先编译一次（不加 -NoBuild）再试。" }

Write-Host ''
Write-Host '=== 2/3 签名 ===' -ForegroundColor Cyan
$signArgs = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $here 'Sign-App.ps1'))
if ($NoTimestamp) { $signArgs += '-NoTimestamp' }
& powershell @signArgs
if ($LASTEXITCODE -ne 0) { throw "签名失败（exit $LASTEXITCODE）" }

Write-Host ''
Write-Host '=== 3/3 验证 ===' -ForegroundColor Cyan
$bad = 0
foreach ($f in @('KeyboardOG.exe', 'HIDComm.dll')) {
    $p = Join-Path $outDir $f
    if (-not (Test-Path $p)) { Write-Host "  跳过（不存在）: $f" -ForegroundColor Yellow; continue }
    $sig = Get-AuthenticodeSignature $p
    $color = if ($sig.Status -eq 'Valid') { 'Green' } else { 'Red' }
    Write-Host ("  {0,-18} 状态={1}  签名者={2}" -f $f, $sig.Status, $sig.SignerCertificate.Subject) -ForegroundColor $color
    if ($sig.Status -ne 'Valid') { $bad++ }
}

Write-Host ''
if ($bad -eq 0) {
    Write-Host "完成：$outDir 里的程序已签名，可以直接拷去用。" -ForegroundColor Green
    exit 0
} else {
    Write-Host "有 $bad 个文件的签名状态不是 Valid，请检查上面的输出。" -ForegroundColor Red
    exit 1
}
