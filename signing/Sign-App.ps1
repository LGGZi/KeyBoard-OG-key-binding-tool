# Sign-App.ps1
# ---------------------------------------------------------------------------
# 用 Make-Cert.ps1 生成的自签名证书给 KeyboardOG 签名（在【源机器】上跑）。
#
# 签名后：
#   · 目标机器装过证书（Install-Trust.ps1）之后，UAC 里会显示发布者名字，
#     不再是「未知发布者」；文件属性里的"数字签名"页也能看到签名。
#   · 没装证书的机器上依旧是「未知发布者」，而且 SmartScreen 仍然会拦
#     （自签名证书不会积累 SmartScreen 信誉）——这是正常现象。
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File .\Sign-App.ps1
#   powershell -ExecutionPolicy Bypass -File .\Sign-App.ps1 -Exe "D:\...\KeyboardOG.exe"
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # 默认签 KeyboardOG 的 Release 产物（包含 HIDComm.dll）
    [string]$Exe,

    # 指定指纹时优先用它；不填就按 cert-info.txt / 主题去找
    [string]$Thumbprint,

    # 不做时间戳（没网时用）
    [switch]$NoTimestamp
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$appDir = Split-Path -Parent $here      # KeyboardOG 目录

if (-not $Exe) { $Exe = Join-Path $appDir 'bin\Release\KeyboardOG.exe' }
if (-not (Test-Path $Exe)) { throw "找不到要签名的文件：$Exe" }

# ---------- 找证书 ----------
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
    #退而求其次：按名字找一张带私钥的代码签名证书
    $cert = Get-ChildItem 'Cert:\CurrentUser\My' |
            Where-Object { $_.Subject -like '*LGGZ*' -and $_.HasPrivateKey } |
            Sort-Object NotAfter -Descending | Select-Object -First 1
}
if (-not $cert) {
    throw "没找到签名证书。先跑一次 Make-Cert.ps1。"
}

Write-Host "使用证书：$($cert.Subject)" -ForegroundColor Cyan
Write-Host "  指纹  ：$($cert.Thumbprint)"
Write-Host "  有效期：$($cert.NotAfter.ToString('yyyy-MM-dd'))"
Write-Host ""

# ---------- 要签的文件：exe + 同目录的 dll ----------
$targets = @($Exe)
$dir = Split-Path -Parent $Exe
Get-ChildItem $dir -Filter '*.dll' -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notmatch '^System\.' } |
    ForEach-Object { $targets += $_.FullName }

$ts = 'http://timestamp.digicert.com'
$ok = 0; $fail = 0

foreach ($t in $targets) {
    Write-Host "签名 $([System.IO.Path]::GetFileName($t)) ..." -NoNewline

    $signed = $false
    if (-not $NoTimestamp) {
        try {
            Set-AuthenticodeSignature -FilePath $t -Certificate $cert -HashAlgorithm SHA256 `
                -TimestampServer $ts -ErrorAction Stop | Out-Null
            $signed = $true
        } catch {
            Write-Host " 时间戳失败($($_.Exception.Message.Trim()))，改成本地签名" -ForegroundColor Yellow
        }
    }
    if (-not $signed) {
        try {
            Set-AuthenticodeSignature -FilePath $t -Certificate $cert -HashAlgorithm SHA256 -ErrorAction Stop | Out-Null
            $signed = $true
        } catch {
            Write-Host " 失败: $($_.Exception.Message)" -ForegroundColor Red
            $fail++
            continue
        }
    }

    $st = Get-AuthenticodeSignature $t
    Write-Host " 状态=$($st.Status)" -ForegroundColor Green
    $ok++
}

Write-Host ""
Write-Host "完成：$ok 个签名成功，$fail 个失败。" -ForegroundColor Cyan
Write-Host ""
Write-Host "提示：" -ForegroundColor Yellow
Write-Host "  · Status=UnknownError/UntrustedRoot 表示本机还没信任这张自签名证书，"
Write-Host "    在本机也信任一次：Make-Cert.ps1 -TrustLocal（或把 .cer 装进 受信任的根证书颁发机构）。"
Write-Host "  · 拷到别的机器后，在那台机器上以管理员身份跑 Install-Trust.ps1，"
Write-Host "    「未知发布者」和「文件来自其他计算机」的警告就会消失。"
