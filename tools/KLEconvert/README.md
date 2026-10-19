# KLEconvert —— KLE 配列查看 / 编号 / 导出

可视化处理 keyboard-layout-editor（KLE）导出配列的小工具，用来：

* 拖入 KLE 的 **Raw data JSON**，按真实配列渲染按键；
* 一键按「从上到下、从左到右」给按键编号；
* 点选某个键，直接在键盘上敲数字改它的编号（Enter 跳到下一个键）；
* 编辑类型、键帽文字、坐标尺寸；
* 导出**带编号的 JSON**（回到 KLE 继续画）和固件用的 **.h 数组**（贴进 `LayoutManager.h`）。

## 运行

双击 **`运行.cmd`**（没有编译过会自动编译），或直接用 Visual Studio 打开 `KLEconvert.sln` 后 F5。

也可以拖 json 到窗口里，或用命令行：

```bat
KLEconvert.exe path\to\layout.json          :: 打开
KLEconvert.exe --export-h  in.json out.h    :: 原样导出 .h
KLEconvert.exe --export-json in.json out.json
KLEconvert.exe --number-h  in.json out.h    :: 先按位置自动编号再导出
KLEconvert.exe --number-json in.json out.json
KLEconvert.exe --shot out.png in.json --numbered   :: 自绘截图（自检用）
```

## 操作流程

1. **拖入** `.json`（或「打开 JSON…」）；
2. 点 **「一键编号」**，按位置得到 0…N-1；
3. 需要调整时，**点画布上的按键**：左侧「编号」框自动获得焦点并全选；
4. **直接敲数字**（如 `12`），画布与列表立即更新；
   - 按 **Enter** 应用到当前键并跳到下一个键，适合连续微调；
   - 勾 **「无编号」** 可把某个键从编号序列里去掉；
5. 「类型」可选 普通 / 修饰键 / 编码器 / 装饰（写进数组的 flags 低 6 位）；
6. **「导出 JSON…」**：把编号以 `#N` 行写进键帽文字（KLE 里能直接看到）；
7. **「导出 .h…」**：生成 `const KeyLayoutItem layout_custom[] PROGMEM`，
   全部键都有编号时按编号排序，否则按位置排序（与 `kle2c` 的位置模式一致）。

## 左侧字段

| 字段 | 说明 |
|---|---|
| 编号 | 0…255；对应固件 `key_map` 的下标 |
| 无编号 | 勾选后该键不参与编号（导出 .h 时按位置排序） |
| 类型 | 0 普通 / 1 修饰键 / 2 编码器 / 3 装饰 |
| 标签 | 键帽文字（不含 `#N`），只影响显示与注释，不占固件空间 |
| x / y / w / h | 0.25u 为单位（4 = 1u），可直接改坐标尺寸 |

## 关于编号与错序

KLE 的 Raw data 不含按键身份，编号默认由位置决定；**在 KLE 里拖动按键会改变编号**。
本工具的做法是把编号写成键帽里的 `#N` 行：

* 编号跟着按键走，拖动后不会错号；
* `tools/kle2c.py` 默认 `auto`：标签全带 `#N` 就按标签编号，全没有就按位置；
* 编号必须覆盖 `0…N-1`，重复或缺失都会报错，避免贴错表。

## 目录

```
KLEconvert\
├─ KLEconvert.sln / KLEconvert\   VS 工程（Form1 即主界面，UI 由代码构建）
├─ 运行.cmd                        编译（如需）+ 启动
└─ tools\                          批量转换工具（kle2c.py、快速转换.bat、README.md）
```

## 备注

* 界面由代码构建，不是 WinForms 设计器拖的控件；在 VS 里改界面请改 `Form1.cs` 的 `BuildLeftPanel` / `LayoutCanvas.cs`。
* 画布只读：不支持拖动改位置，坐标请用左侧 x/y/w/h 精确修改。
* 旋转键（KLE 里 `r != 0`）会显示 ↻ 并写 `0xC0` 标志，旋转参数留给二期协议。
