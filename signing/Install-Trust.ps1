# Install-Trust.ps1
# ---------------------------------------------------------------------------
# 在【目标机器】上跑（把程序拷过去之后），作用是：
#   1. 把 LGGZ 的自签名证书装进"受信任的根"和"受信任的发布者"
#         -> UAC 里不再显示「未知发布者」，而是显示 LGGZ
#   2. 去掉文件上的"来自其他计算机"标记（Zone.Identifier）
#         -> 不再提示「此文件来自其他计算机，可能被阻止」，
#            SmartScreen 的「Windows 已保护你的电脑」也不会再对这个文件出现
#   3. （可选）给程序目录加一个 Windows Defender 排除项
#
# 需要【以管理员身份】运行：
#   powershell -ExecutionPolicy Bypass -File .\Install-Trust.ps1
#
# 如果只想做第 2 步（不需要管理员），用 Unblock-Files.ps1 就行。
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # 程序目录（默认：本脚本上一级 = KeyboardOG 目录）
    [string]$AppDir,

    # 证书文件（默认：本脚本同目录的 LGGZ-CodeSign.cer）
    [string]$CertFile,

    # 顺便把带私钥的 .pfx 也装进当前用户，这样这台机器以后也能签名
    [switch]$InstallPfx,

    # 给程序目录加 Defender 排除项
    [switch]$AddDefenderExclusion,

    # 不自动提权（脚本自己调用自己时用，或者你想手动右键管理员运行）
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

# 不是管理员就问一句，直接提权重开（比让人自己右键省事）
if (-not $isAdmin -and -not $NoElevate) {
    Write-Host "装证书需要管理员权限。" -ForegroundColor Yellow
    $ans = Read-Host "现在用管理员身份重新运行？(Y/n)"
    if ($ans -ne 'n' -and $ans -ne 'N') {
        $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                     '-AppDir', "`"$AppDir`"", '-CertFile', "`"$CertFile`"", '-NoElevate')
        if ($InstallPfx)           { $argList += '-InstallPfx' }
        if ($AddDefenderExclusion) { $argList += '-AddDefenderExclusion' }
        try {
            Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argList
            Write-Host "已用管理员身份重新打开，请看新窗口。" -ForegroundColor Green
        } catch {
            Write-Host "提权失败：$($_.Exception.Message)" -ForegroundColor Red
            Write-Host "请手动右键 -> 以管理员身份运行 PowerShell 再跑本脚本。"
        }
        exit
    }
}

Write-Host "=== LGGZ 自签名证书安装 / 解除阻止 ===" -ForegroundColor Cyan
Write-Host "  程序目录: $AppDir"
Write-Host "  证书文件: $CertFile"
Write-Host "  管理员  : $isAdmin"
Write-Host ""

# ---------------------------------------------------------------
# 1. 装证书（需要管理员）
# ---------------------------------------------------------------
if (-not (Test-Path $CertFile)) {
    Write-Host "找不到证书文件：$CertFile" -ForegroundColor Red
    Write-Host "（先在源机器上跑 Make-Cert.ps1，把生成的 .cer 一起拷过来）"
} elseif (-not $isAdmin) {
    Write-Host "不是管理员，跳过装证书。" -ForegroundColor Yellow
    Write-Host "  -> 想彻底去掉「未知发布者」，请右键以管理员身份重跑本脚本。"
} else {
    Write-Host "导入证书..." -ForegroundColor Cyan

    # 受信任的根证书颁发机构：让证书链可信
    Import-Certificate -FilePath $CertFile -CertStoreLocation 'Cert:\LocalMachine\Root' | Out-Null
    Write-Host "  已导入 受信任的根证书颁发机构" -ForegroundColor Green

    # 受信任的发布者：UAC 里才会认这个发布者
    Import-Certificate -FilePath $CertFile -CertStoreLocation 'Cert:\LocalMachine\TrustedPublisher' | Out-Null
    Write-Host "  已导入 受信任的发布者" -ForegroundColor Green

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
            Write-Host "  已导入私钥证书（这台机器以后也能签名）" -ForegroundColor Green
        } else {
            Write-Host "  没找到 $pfx，跳过私钥导入" -ForegroundColor Yellow
        }
    }
}

# ---------------------------------------------------------------
# 2. 去掉"来自其他计算机"的标记（不需要管理员）
# ---------------------------------------------------------------
Write-Host ""
Write-Host "去掉文件的「来自其他计算机」标记..." -ForegroundColor Cyan

$exts = @('*.exe', '*.dll', '*.config', '*.pdb', '*.ini', '*.csv')
$count = 0
if (Test-Path $AppDir) {
    foreach ($e in $exts) {
        Get-ChildItem -Path $AppDir -Filter $e -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
            $f = $_
            # 先试标准做法
            try { Unblock-File -Path $f.FullName -ErrorAction Stop } catch { }
            # 再直接删备用数据流（Unblock-File 个别情况下不生效时兜底）
            try {
                $streams = Get-Item -Path $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue
                if ($streams) { Remove-Item -Path $f.FullName -Stream 'Zone.Identifier' -ErrorAction SilentlyContinue }
            } catch { }
            $count++
        }
    }
}
Write-Host "  处理了 $count 个文件" -ForegroundColor Green

# ---------------------------------------------------------------
# 3. 可选：Defender 排除项
# ---------------------------------------------------------------
if ($AddDefenderExclusion) {
    Write-Host ""
    Write-Host "添加 Defender 排除项..." -ForegroundColor Cyan
    if (-not $isAdmin) {
        Write-Host "  需要管理员，跳过" -ForegroundColor Yellow
    } else {
        try {
            Add-MpPreference -ExclusionPath $AppDir -ErrorAction Stop
            Write-Host "  已排除 $AppDir" -ForegroundColor Green
        } catch {
            Write-Host "  添加失败：$($_.Exception.Message)" -ForegroundColor Yellow
        }
    }
}

# ---------------------------------------------------------------
# 4. 结果检查
# ---------------------------------------------------------------
Write-Host ""
Write-Host "=== 检查签名状态 ===" -ForegroundColor Cyan
$exe = Join-Path $AppDir 'bin\Release\KeyboardOG.exe'
if (-not (Test-Path $exe)) { $exe = Join-Path $AppDir 'KeyboardOG.exe' }

if (Test-Path $exe) {
    $sig = Get-AuthenticodeSignature $exe
    Write-Host "  文件: $exe"
    Write-Host "  状态: $($sig.Status)"
    if ($sig.SignerCertificate) {
        Write-Host "  发布者: $($sig.SignerCertificate.Subject)"
    }
    if ($sig.Status -eq 'Valid') {
        Write-Host "  -> 签名有效，UAC 里会显示发布者名字。" -ForegroundColor Green
    } else {
        Write-Host "  -> 还是 $($sig.Status)（目标机器上没装过证书时是正常的）。" -ForegroundColor Yellow
    }
} else {
    Write-Host "  没找到 KeyboardOG.exe，跳过检查。" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "完成。" -ForegroundColor Green
Write-Host "  · 「此文件来自其他计算机」/ 对该文件的 SmartScreen 拦截：已解除。"
Write-Host "  · 「未知发布者」：装过证书（管理员）之后会显示成 LGGZ。"
Write-Host "  · 首次运行仍然可能出现 SmartScreen（自签名证书没有信誉），点「更多信息 -> 仍要运行」即可；"
Write-Host "    要彻底没有这个提示，只能买 CA 签发的正式代码签名证书。"
