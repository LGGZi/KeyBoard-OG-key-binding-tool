# 软件转移后的警告，怎么消掉

程序（KeyboardOG.exe）拷到别的机器上会遇到的警告有 **三种**，来源不同、解法也不同。
这里给的是"自己的机器 / 自己人用"的方案：**自签名证书 + 解除阻止**。

---

## 一、先搞清楚是哪种警告

| 你看到的提示 | 真正原因 | 这份工具能不能消掉 |
|---|---|---|
| 「此文件来自其他计算机，可能被阻止」<br>「Windows 已保护你的电脑」（对该文件）<br>解压后报"缺少 xxx.dll" | 文件带了 **Zone.Identifier** 标记（俗称"网络标记 / MOTW"），只要通过下载、U 盘以外的网络方式传过就会带上 | ✅ **能**，`Unblock-Files.ps1`（不用管理员） |
| UAC 弹窗里写着 **「未知发布者」** | exe 没有数字签名，或签名证书在这台机器上不受信任 | ✅ **能**，`Make-Cert.ps1` + `Sign-App.ps1` + `Install-Trust.ps1`（管理员） |
| SmartScreen：「Windows 已保护你的电脑」<br>（信息来源：**未知发布者 / 不常见的文件**） | 微软的**信誉**机制，跟文件是不是签了名无关；自签名证书**不会**积累信誉 | ❌ **不能**。只能点"更多信息 → 仍要运行"，或者买 CA 签发的正式代码签名证书（OV/EV） |
| 杀毒软件报毒 / 拦截 | 启发式误报 | ⚠️ 部分：`Install-Trust.ps1 -AddDefenderExclusion` 可以给目录加 Defender 排除项 |

> **重点**：你之前说的"软件转移后的警告"，绝大多数是第一种（网络标记）。那种**一条命令就没了**，根本不用买证书。真正需要花钱买证书的只有第三种。

---

## 二、最简单的用法（推荐先做这个）

把整个 `KeyboardOG` 文件夹拷到目标机器后，在那台机器上：

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Unblock-Files.ps1 -Path "D:\KeyboardOG"
```

如果传的是压缩包，**更好的做法**是：先拷 zip → `Unblock-File .\KeyboardOG.zip` → 再解压，
这样解压出来的文件天生不带标记。

---

## 三、去掉「未知发布者」（自签名证书）

### 1) 源机器：生成证书（**只需要做一次**）

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Make-Cert.ps1 -TrustLocal
```

会生成：

| 文件 | 用途 |
|---|---|
| `LGGZ-CodeSign.cer` | 公钥证书，**拷到目标机器**上信任用 |
| `LGGZ-CodeSign.pfx` | 带私钥，用来签名。密码 `123456`。**不要外传**，谁拿到都能冒充 LGGZ 签名 |
| `cert-info.txt` | 指纹 / 有效期 / pfx 密码，后面两个脚本会读 |

证书名字就是 UAC 里显示的发布者：**CN=LGGZ**（想改就 `-Subject "CN=别的名字"`）。
`-TrustLocal` 会顺便让**本机**也信任它，这样本机 UAC 就会显示 LGGZ。

### 2) 源机器：给程序签名（**每次重新编译之后都要跑**）

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Sign-App.ps1
```

默认签 `bin\Release\KeyboardOG.exe` 和同目录的 dll；带时间戳（这样证书过期后签名依然有效）。

> ⚠️ **重新编译会覆盖 exe，签名一起没了**，所以每次 Build 完都要再签一次。
> 想签 Debug 的产物：`-Exe .\bin\Debug\KeyboardOG.exe`

### 3) 目标机器：信任证书（**以管理员身份**）

把 `LGGZ-CodeSign.cer` 和 `signing` 目录一起拷过去，然后**以管理员身份**：

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Install-Trust.ps1 -AppDir "D:\KeyboardOG"
```

它做三件事：
1. 把 `.cer` 装进 **受信任的根证书颁发机构** + **受信任的发布者** → UAC 显示发布者 `LGGZ`；
2. 递归去掉程序目录里所有文件的"来自其他计算机"标记；
3. `-AddDefenderExclusion` 时给目录加 Defender 排除项。

如果这台机器以后也要签名，加 `-InstallPfx`（会用 `cert-info.txt` 里的密码导入私钥）。

### 4) 验证

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Check-Sign.ps1
```

或者手动看：

```powershell
Get-AuthenticodeSignature .\bin\Release\KeyboardOG.exe | Format-List Status, SignerCertificate
```

`Status=Valid` + `SignerCertificate.Subject=CN=LGGZ` 就是成功了。

---

## 四、要发给**别人**用怎么办

自签名证书在别人机器上没用（他们不信任你的自签名根），SmartScreen 也照样拦。这时候只有两条路：

1. **买正式代码签名证书**（DigiCert / Sectigo / GlobalSign 等，OV 大约 ¥1000+/年，EV 更贵）。
   用 `Sign-App.ps1` 换成那张证书签就行（改 `-Thumbprint`，或把证书装进 `CurrentUser\My` 后按名字找）。
   注意：**新证书一开始也没有信誉**，SmartScreen 要跑一段时间、够多下载量才会转绿；EV 证书是立即生效的。
2. 不签名，让用户自己点"更多信息 → 仍要运行"，或者用安装包（Inno Setup / MSI）安装 ——
   但 SmartScreen 对未签名的安装包一样会拦。

---

## 五、几个提醒

* **私钥（.pfx）要收好**。密码是 `123456`，只够防误传，不算安全。别放进要发出去的包。
* `Install-Trust.ps1` 往"受信任的根"里装证书是全机生效的 ——
  只在**你自己的机器 / 信得过的机器**上做，别在公共电脑上跑。
* 不想装证书也行：只用 `Unblock-Files.ps1`，能把"来自其他计算机"那一类警告消掉，
  剩下 UAC 的"未知发布者"和第一次运行的 SmartScreen 点一下"仍要运行"即可。
* 万一以后不想信任了：`certlm.msc`（管理员）→ 受信任的根证书颁发机构 / 受信任的发布者里删掉 `LGGZ`；
  本机的用 `certmgr.msc` → 当前用户 → 受信任的根证书颁发机构里删。
