# Unblock-Files.ps1
# ---------------------------------------------------------------------------
# 只做一件事：去掉文件的「来自其他计算机」标记（Zone.Identifier）。
#
# 这个标记就是"软件转移之后各种警告"的来源：
#   · 「此文件来自其他计算机，可能被阻止」
#   · 打开时「Windows 已保护你的电脑」/「Windows 无法访问指定的设备、路径或文件」
#   · 有些程序解压后 dll 被静默拦掉，报"缺少 xxx.dll"
#
# 拷贝完程序之后跑一次就行，**不需要管理员**：
#   powershell -ExecutionPolicy Bypass -File .\Unblock-Files.ps1
#   powershell -ExecutionPolicy Bypass -File .\Unblock-Files.ps1 -Path "D:\某个目录"
#
# 如果是压缩包传输的：先把 zip 拷过去 -> Unblock-File 那个 zip -> 再解压，
# 这样解压出来的文件不会带标记（否则解压后再跑一次本脚本也一样）。
# ---------------------------------------------------------------------------

[CmdletBinding()]
param(
    # 要处理的目录（默认：本脚本上一级 = KeyboardOG 目录）
    [string]$Path,

    # 是否递归子目录
    [bool]$Recurse = $true,

    # 显示每个被处理的文件（默认只显示汇总）
    [switch]$Verbose2
)

$ErrorActionPreference = 'Stop'
if (-not $Path) {
    $here = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Path = Split-Path -Parent $here
}

if (-not (Test-Path $Path)) { throw "目录不存在：$Path" }

Write-Host "=== 去掉「来自其他计算机」标记 ===" -ForegroundColor Cyan
Write-Host "  目录: $Path"

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
        if ($Verbose2) { Write-Host "  解除阻止: $($f.FullName)" }
    }

    # Unblock-File 对没有标记的文件是空操作，直接调就行
    try { Unblock-File -Path $f.FullName -ErrorAction SilentlyContinue } catch { }

    # 兜底：直接删备用数据流
    if ($has) {
        try { Remove-Item -Path $f.FullName -Stream 'Zone.Identifier' -Force -ErrorAction SilentlyContinue } catch { }
    }
}

Write-Host ""
Write-Host "  共 $total 个文件，其中带标记的 $blocked 个，已全部解除。" -ForegroundColor Green
Write-Host ""
Write-Host "提示：想让「未知发布者」也消失，需要给程序签名并在本机信任证书 ——" -ForegroundColor Yellow
Write-Host "      见同目录 README.md / Install-Trust.ps1（那个需要管理员）。"
