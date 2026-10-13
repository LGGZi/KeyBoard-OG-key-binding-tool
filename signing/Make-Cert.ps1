# Make-Cert.ps1
# ---------------------------------------------------------------------------
# 生成一张"代码签名"用的自签名证书（在【源机器】上跑一次就够了）。
#
# 生成的东西：
#   LGGZ-CodeSign.cer        —— 公钥证书，拷到别的机器上"信任"用
#   LGGZ-CodeSign.pfx        —— 带私钥，用来给 exe 签名（**要保管好**）
#   cert-info.txt            —— 指纹等信息，后面两个脚本会读它
#
# 说明：自签名证书能去掉"未知发布者"，但**不能**去掉微信/浏览器下载后的
#       SmartScreen「Windows 已保护你的电脑」——那个只能靠 CA 签发的正式证书。
#       详见同目录 README.md。
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # 证书名字：就是 UAC / 文件属性里显示的"发布者"
    [string]$Subject = "CN=LGGZ",

    # 导出 .pfx 用的密码（按你的要求默认 123456；只是防止误传，强度本身不强）
    [string]$PfxPassword = "123456",

    # 有效期（年）
    [int]$Years = 10,

    # 顺便把证书装进"当前用户 -> 受信任的根证书颁发机构"，这样本机也认它
    [switch]$TrustLocal
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "=== 1/3 生成自签名代码签名证书 ===" -ForegroundColor Cyan
Write-Host "  主题: $Subject"
Write-Host "  有效期: $Years 年"

# 1.3.6.1.5.5.7.3.3 = Code Signing 的增强型密钥用法
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy Exportable `
    -KeyLength 2048 `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears($Years) `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3')

Write-Host "  指纹: $($cert.Thumbprint)" -ForegroundColor Green

# ---------- 导出 .cer（公钥）----------
$cerPath = Join-Path $here 'LGGZ-CodeSign.cer'
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT -Force | Out-Null
Write-Host "=== 2/3 已导出公钥证书 ===" -ForegroundColor Cyan
Write-Host "  $cerPath"

# ---------- 导出 .pfx（含私钥）----------
$pfxPath = Join-Path $here 'LGGZ-CodeSign.pfx'
$pfxPwd = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPwd -Force | Out-Null
Write-Host "  $pfxPath  （**别外传**，谁拿到都能冒充你签名）" -ForegroundColor Yellow

# ---------- 记录信息 ----------
$infoPath = Join-Path $here 'cert-info.txt'
@(
    "# 由 Make-Cert.ps1 生成，Sign-App.ps1 / Install-Trust.ps1 会读这里"
    "Thumbprint=$($cert.Thumbprint)"
    "Subject=$($cert.Subject)"
    "NotAfter=$($cert.NotAfter.ToString('yyyy-MM-dd'))"
    "PfxPassword=$PfxPassword"
) | Set-Content -Path $infoPath -Encoding UTF8
Write-Host "=== 3/3 已记录证书信息 ===" -ForegroundColor Cyan
Write-Host "  $infoPath"

if ($TrustLocal) {
    Write-Host ""
    Write-Host "=== 把证书装进当前用户的受信任根（本机也认它）===" -ForegroundColor Cyan
    Import-Certificate -FilePath $cerPath -CertStoreLocation 'Cert:\CurrentUser\Root' | Out-Null
    Write-Host "  已导入 Cert:\CurrentUser\Root" -ForegroundColor Green
}

Write-Host ""
Write-Host "下一步：" -ForegroundColor Green
Write-Host "  1) 给程序签名：  powershell -ExecutionPolicy Bypass -File .\Sign-App.ps1"
Write-Host "  2) 拷到别的机器后，在那台机器上【以管理员身份】跑："
Write-Host "     powershell -ExecutionPolicy Bypass -File .\Install-Trust.ps1"
