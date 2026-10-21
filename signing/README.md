# Removing Windows warnings after moving the app

> **Security notice.** An earlier revision of this repository published `LGGZ-CodeSign.pfx`
> (commit `302a738`, "Add files via upload"), together with the password that used to be written
> in this document. Treat that key as **compromised**: generate a new certificate with
> `Make-Cert.ps1`, re-trust it with `Install-Trust.ps1`, re-sign with `sign.cmd`, and never commit
> a `.pfx` again (`.gitignore` covers `*.pfx`). Anything signed with the old key can be forged.

Copying `KeyboardOG.exe` to another machine can produce **three** different warnings. They have
different causes and different fixes. This folder implements the "own machine / own team" setup:
**self-signed certificate + unblocking**.

---

## 1. Identify which warning you are looking at

| What you see | Real cause | Can this toolkit fix it? |
|---|---|---|
| "This file came from another computer and might be blocked"<br>"Windows protected your PC" (for that file)<br>"missing xxx.dll" after extracting | The file carries a **Zone.Identifier** mark (a.k.a. Mark-of-the-Web). Anything transferred over a network path (download, chat, cloud) gets it | ✅ **Yes** — `Unblock-Files.ps1` (no administrator needed) |
| UAC dialog shows **"Unknown publisher"** | The exe is unsigned, or its signing certificate is not trusted on this machine | ✅ **Yes** — `Make-Cert.ps1` + `Sign-App.ps1` + `Install-Trust.ps1` (administrator) |
| SmartScreen: "Windows protected your PC"<br>(source: **unknown publisher / uncommon file**) | Microsoft's **reputation** system; signing alone does not help, and a self-signed certificate **never** builds reputation | ❌ **No**. Click "More info → Run anyway", or buy a CA-issued code-signing certificate (OV/EV) |
| Antivirus flags or blocks it | Heuristic false positive | ⚠️ Partly — `Install-Trust.ps1 -AddDefenderExclusion` adds a Defender exclusion for the folder |

> **Key point:** most "warnings after moving the app" are the first kind (Mark-of-the-Web), and one
> command removes them — no certificate purchase needed. Only the third kind requires a paid
> certificate.

---

## 2. Quickest fix (start here)

After copying the whole `KeyboardOG` folder to the target machine, run there:

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Unblock-Files.ps1 -Path "D:\KeyboardOG"
```

If you transfer a zip, a **better order** is: copy the zip → `Unblock-File .\KeyboardOG.zip` →
extract. Files extracted from an unblocked archive carry no mark.

---

## 3. Removing "Unknown publisher" (self-signed certificate)

### 3.1 Source machine: create the certificate (**once**)

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Make-Cert.ps1 -TrustLocal
```

Outputs:

| File | Purpose |
|---|---|
| `LGGZ-CodeSign.cer` | Public certificate — copy this to target machines |
| `LGGZ-CodeSign.pfx` | Contains the private key, used for signing. Password `123456`. **Never share it**: anyone who has it can sign as LGGZ |
| `cert-info.txt` | Thumbprint / validity / pfx password, read by the other scripts |

The subject is what UAC shows: **CN=LGGZ** (change it with `-Subject "CN=Something Else"`).
`-TrustLocal` also trusts it on the current machine, so local UAC shows LGGZ immediately.

### 3.2 Source machine: sign the binaries (**after every rebuild**)

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Sign-App.ps1
```

By default it signs `bin\Release\KeyboardOG.exe` and the dll beside it, with a timestamp (so the
signature stays valid after the certificate expires).

> ⚠️ **Rebuilding overwrites the exe and drops the signature** — sign again after every build.
> To sign a Debug build: `-Exe .\bin\Debug\KeyboardOG.exe`

### 3.3 Target machine: trust the certificate (**as administrator**)

Copy `LGGZ-CodeSign.cer` and the `signing` folder over, then **as administrator**:

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Install-Trust.ps1 -AppDir "D:\KeyboardOG"
```

It does three things:

1. installs the `.cer` into **Trusted Root Certification Authorities** and **Trusted Publishers**,
   so UAC shows the publisher as `LGGZ`;
2. recursively removes the Mark-of-the-Web from the app folder;
3. with `-AddDefenderExclusion`, adds a Defender exclusion for the folder.

If that machine will also sign builds, add `-InstallPfx` (imports the private key using the
password from `cert-info.txt`).

### 3.4 Verify

```powershell
powershell -ExecutionPolicy Bypass -File .\signing\Check-Sign.ps1
```

Or manually:

```powershell
Get-AuthenticodeSignature .\bin\Release\KeyboardOG.exe | Format-List Status, SignerCertificate
```

Success looks like `Status=Valid` and `SignerCertificate.Subject=CN=LGGZ`.

---

## 4. Distributing to other people

A self-signed certificate is useless on someone else's machine (they do not trust your root), and
SmartScreen still blocks. There are only two options:

1. **Buy a code-signing certificate** (DigiCert / Sectigo / GlobalSign; OV is roughly
   US$300–500/year, EV more). Sign with it by pointing `Sign-App.ps1` at the new certificate
   (`-Thumbprint`, or install it into `CurrentUser\My` and look it up by name).
   Note that a **new certificate has no reputation either** — SmartScreen turns green only after
   enough downloads over time; EV certificates take effect immediately.
2. Ship unsigned and let users click "More info → Run anyway", or use an installer
   (Inno Setup / MSI). SmartScreen blocks unsigned installers just the same.

---

## 5. Reminders

* **Keep the private key (.pfx) safe.** The default password `123456` only prevents accidents; it
  is not security. Never put it into a package you distribute.
* `Install-Trust.ps1` installs into the machine-wide trusted root — only run it on your own or
  trusted machines, never on a public computer.
* You can skip certificates entirely: `Unblock-Files.ps1` removes the Mark-of-the-Web class of
  warnings, and users just click "Run anyway" once for UAC and SmartScreen.
* To remove trust later: `certlm.msc` (admin) → Trusted Root Certification Authorities / Trusted
  Publishers → delete `LGGZ`; per-user entries live in `certmgr.msc` → Current User.
## 一键修复（推荐给使用者）

双击 **`一键修复.cmd`**（仓库根目录或本目录各有一份）即可完成：

1. 去掉 `bin\Release` 下所有文件的“来自 Internet”标记（不再弹安全警告）；
2. 把 `LGGZ-CodeSign.cer` 导入受信任的根证书与受信任的发布者
   （管理员权限装到本机，否则装到当前用户）；
3. 校验 `KeyboardOG.exe` / `HIDComm.dll` 的签名并给出结论。

实现是 `Fix-Signature-And-Unblock.ps1`，参数：

| 参数 | 说明 |
|---|---|
| `-NoPause` | 结束时不等待回车（脚本调用用） |
| `-Sign` | 签名无效时自动调用 `Sign-OneClick.ps1 -NoBuild` |
| `-Path <目录>` | 指定要处理的输出目录（默认 `bin\Release`） |

