# Fix-Signature-And-Unblock.ps1
# ---------------------------------------------------------------------------
# 一键处理三件事（可以放在 signing\ 下，也可以直接放到 bin\Release\ 里）：
#   1. 去掉“来自 Internet”标记（Zone.Identifier）—— 否则每次启动都弹
#      “Open File - Security Warning / 是否运行此文件”；
#   2. 把 LGGZ 自签名证书导入受信任的根证书 + 受信任的发布者
#      （有管理员权限时装到本机，否则装到当前用户）；
#   3. 校验 KeyboardOG.exe / HIDComm.dll 的 Authenticode 签名并给出结论。
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File .\Fix-Signature-And-Unblock.ps1
#   powershell -ExecutionPolicy Bypass -File .\Fix-Signature-And-Unblock.ps1 -NoPause
#   powershell -ExecutionPolicy Bypass -File .\Fix-Signature-And-Unblock.ps1 -Sign   # 签名无效时自动跑签名脚本
#
# 这个脚本只读取证书、写证书库和文件属性，不删除任何文件。
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    [switch]$NoPause,
    [switch]$Sign,
    [string]$Path
)

$ErrorActionPreference = 'Continue'

try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding = New-Object System.Text.UTF8Encoding($false)
} catch { }

function Step($t) { Write-Host ""; Write-Host "== $t" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "   $t" -ForegroundColor Green }
function Warn($t) { Write-Host "   $t" -ForegroundColor Yellow }
function Fail($t) { Write-Host "   $t" -ForegroundColor Red }

function Test-Admin {
    try {
        return ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { return $false }
}

# ---------------------------------------------------------------- 路径
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$isAdmin = Test-Admin

# 允许放在 bin\Release 里直接运行
if ($Path) {
    $outDir = [System.IO.Path]::GetFullPath($Path)
    $appDir = Split-Path -Parent $outDir
} elseif (Test-Path (Join-Path $here 'KeyboardOG.exe')) {
    $outDir = $here
    $appDir = Split-Path -Parent $here
} else {
    $appDir = Split-Path -Parent $here
    $outDir = Join-Path $appDir 'bin\Release'
}

Write-Host ""
Write-Host "=== KeyboardOG 一键修复 ===" -ForegroundColor Cyan
Write-Host "  应用目录 : $appDir"
Write-Host "  输出目录 : $outDir"
Write-Host "  管理员   : $isAdmin"
Write-Host "  证书库   : $(if ($isAdmin) { '本机（LocalMachine）' } else { '当前用户（CurrentUser）' })"

if (-not (Test-Path $outDir)) {
    Fail "输出目录不存在：$outDir"
    if (-not $NoPause) { Read-Host "按回车退出" | Out-Null }
    exit 1
}

# ---------------------------------------------------------------- 1. 去掉 MOTW
Step "1/3 去掉[来自 Internet]标记"

$removed = 0
$scanned = 0
$targets = @($outDir)
if ($Path -eq $null -and (Test-Path (Join-Path $appDir 'signing'))) {
    # 放在 signing\ 下时顺带处理整个 exe 目录即可；需要全仓库可用 -Path
}

foreach ($dir in $targets) {
    Get-ChildItem $dir -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $scanned++
        $zone = Get-Item -LiteralPath $_.FullName -Stream Zone.Identifier -ErrorAction SilentlyContinue
        if ($zone) {
            try {
                Unblock-File -LiteralPath $_.FullName -ErrorAction Stop
                $removed++
                Write-Host "   unblocked: $($_.Name)" -ForegroundColor DarkGray
            } catch {
                Warn "无法解除：$($_.Name)"
            }
        }
    }
}
if ($removed -gt 0) { Ok "已清除 $removed 个文件的标记（共检查 $scanned 个）" }
else { Ok "没有文件带标记（共检查 $scanned 个）" }

# ---------------------------------------------------------------- 2. 证书
Step "2/3 信任签名证书"

$cerCandidates = @(
    (Join-Path $here 'LGGZ-CodeSign.cer'),
    (Join-Path $appDir 'signing\LGGZ-CodeSign.cer'),
    (Join-Path $appDir 'LGGZ-CodeSign.cer')
)
$cerPath = $null
foreach ($c in $cerCandidates) { if (Test-Path $c) { $cerPath = $c; break } }

if (-not $cerPath) {
    Warn "找不到 LGGZ-CodeSign.cer，跳过证书安装"
    Warn '（把证书和这个脚本放在同一目录，或用 -Path 指定 bin\Release）'
} else {
    Ok "证书文件：$cerPath"
    $certObj = $null
    try { $certObj = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $cerPath } catch { }
    if ($certObj) { Write-Host "   主题 $($certObj.Subject)   指纹 $($certObj.Thumbprint)" }

    $stores = if ($isAdmin) {
        @('Cert:\LocalMachine\Root', 'Cert:\LocalMachine\TrustedPublisher')
    } else {
        @('Cert:\CurrentUser\Root', 'Cert:\CurrentUser\TrustedPublisher')
    }

    foreach ($store in $stores) {
        $existing = $null
        if ($certObj) {
            $existing = Get-ChildItem $store -ErrorAction SilentlyContinue |
                        Where-Object { $_.Thumbprint -eq $certObj.Thumbprint } | Select-Object -First 1
        }
        if ($existing) {
            Ok "已在 $store"
            continue
        }

        $ok = $false
        try {
            Import-Certificate -FilePath $cerPath -CertStoreLocation $store -ErrorAction Stop | Out-Null
            $ok = $true
        } catch {
            # Import-Certificate 不可用或权限不足时退回 certutil
            $userArg = if ($isAdmin) { @() } else { @('-user') }
            $storeName = if ($store -match 'TrustedPublisher') { 'TrustedPublisher' } else { 'Root' }
            & certutil @userArg -addstore $storeName $cerPath 2>&1 | Out-Null
            $ok = ($LASTEXITCODE -eq 0)
        }

        if ($ok) { Ok "已导入 $store" }
        else {
            Warn "导入失败：$store"
            if (-not $isAdmin) { Warn "  以管理员身份运行可以装到本机证书库（推荐）" }
        }
    }
}

# ---------------------------------------------------------------- 3. 验签
Step "3/3 校验签名"

$files = @('KeyboardOG.exe', 'HIDComm.dll')
$bad = 0
$anyMissing = $false
foreach ($name in $files) {
    $f = Join-Path $outDir $name
    if (-not (Test-Path $f)) { Warn "缺少 $name"; $anyMissing = $true; continue }
    $sig = Get-AuthenticodeSignature -LiteralPath $f
    $color = if ($sig.Status -eq 'Valid') { 'Green' } else { 'Red' }
    Write-Host ("   {0,-18} {1}  {2}" -f $name, $sig.Status,
        $(if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { '' })) -ForegroundColor $color
    if ($sig.Status -ne 'Valid') { $bad++ }
}

if ($bad -gt 0 -and $Sign) {
    $signScript = Join-Path $appDir 'signing\Sign-OneClick.ps1'
    if (Test-Path $signScript) {
        Step "3b 重新签名"
        & powershell -NoProfile -ExecutionPolicy Bypass -File $signScript -NoBuild
    } else {
        Warn "找不到 signing\\Sign-OneClick.ps1，无法自动签名"
    }
} elseif ($bad -gt 0) {
    Warn "有 $bad 个文件签名无效；加 -Sign 可以自动调用签名脚本"
}

# ---------------------------------------------------------------- 小结
Write-Host ""
Write-Host "=== 完成 ===" -ForegroundColor Cyan
Write-Host "   标记清除 : $removed 个文件"
Write-Host "   证书库   : $(if ($isAdmin) { '本机（重启后依然有效）' } else { '当前用户' })"
Write-Host "   签名     : $(if ($bad -eq 0 -and -not $anyMissing) { '全部有效' } else { "有 $bad 个无效 / 缺失" })"
Write-Host ""
Write-Host "   现在直接双击 bin\Release\KeyboardOG.exe 应该不会再弹安全警告。" -ForegroundColor Green

if (-not $NoPause) { Read-Host "按回车退出" | Out-Null }
exit $(if ($bad -eq 0) { 0 } else { 1 })
