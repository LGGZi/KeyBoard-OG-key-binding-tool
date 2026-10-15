using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>界面语言。</summary>
    public enum UiLanguage
    {
        Chinese = 0,
        English = 1,
    }

    /// <summary>
    /// 界面本地化。
    ///
    /// 做法：整份程序的中文原文都写死在源码里（和以前一样，方便维护），
    /// 这里维护一张"中文 → 英文"的对照表，运行时按需翻译：
    ///
    ///   · 静态文本（按钮、标签、分组框…）：L.Apply(控件) 递归把 .Text 换掉，
    ///     英文能翻回中文（表是双向用的），所以语言可以来回切、不需要缓存原值。
    ///   · 带参数的文本：表里写成 "{0}" 形式的模板，
    ///     翻译时自动编成正则去匹配**已经格式化好的**那句话，
    ///     所以调用方不用改（还是 string.Format(...) 那套）。
    ///   · 只有两个控件永远保持双语：主界面的"设置/config"按钮，
    ///     以及设置对话框里的"语言/Language"标签 —— 否则切错语言就找不回来了。
    ///
    /// 注意：英文尽量写短，避免超出原来中文占的宽度。
    /// </summary>
    public static class L
    {
        public static UiLanguage Current = UiLanguage.Chinese;
        public static bool IsEn { get { return Current == UiLanguage.English; } }

        /// <summary>永远保持"中/英"双语的控件名（否则切错语言找不回来）。</summary>
        private static readonly HashSet<string> AlwaysBilingual =
            new HashSet<string> { "btnSettings", "lblLanguage" };

        // ============================================================
        //  对照表：中文原文 -> 英文
        //  含 {0} {1} 的当作模板（自动编成正则，可以翻译格式化之后的整句）
        // ============================================================
        private static readonly Dictionary<string, string> Map = BuildMap();

        /// <summary>
        /// 建表。重复的键只保留第一次 —— 手滑写重复不至于让程序直接崩在启动阶段
        /// （Dictionary 的集合初始化器遇到重复键会抛异常）。
        /// </summary>
        private static Dictionary<string, string> BuildMap()
        {
            var m = new Dictionary<string, string>(StringComparer.Ordinal);
            // ---------- 主界面：顶部 / 状态 ----------
            Add(m, "KeyboardOG 改键 / 改参数工具", "KeyboardOG Key & Param Editor");
            Add(m, "设备：", "Device:");
            Add(m, "刷新设备", "Refresh");
            Add(m, "连接", "Connect");
            Add(m, "断开", "Disconnect");
            Add(m, "未连接", "Not connected");
            Add(m, "● 设备已连接", "● Connected");
            Add(m, "● 设备未连接", "● Not connected");
            Add(m, "队列：", "Queue: ");
            Add(m, "队列：0", "Queue: 0");
            Add(m, "设置…", "Settings...");
            Add(m, "指令日志…", "Command log...");
            Add(m, "[{0}] {1} 已完成", "[{0}] {1} done");
            Add(m, "[{0}] {1} —— 超时无回应（键盘可能没回 ACK，或不支持该指令）", "[{0}] {1} - no response (no ACK)");
            Add(m, "[{0}] {1} —— 发送失败：{2}", "[{0}] {1} - send failed: {2}");

            // ---------- 主界面：左侧按键 / 映射列表 ----------
            Add(m, "1. 选择要修改的按键", "1. Pick a key");
            Add(m, "蓝底 = 选中　绿字 = 与键盘一致　红字 = 本地有改动（参数面板同一套）", "Blue = selected  green = in sync  red = edited");
            Add(m, "（下方预留：后续可换成其他样式的按键选择器）", "(reserved for other key pickers)");
            Add(m, "2. 按键映射列表", "2. Mappings of this key");
            Add(m, "映射内容", "Mapping");
            Add(m, "按键 #0：0/12 条", "Key #0: 0/12");
            Add(m, "按键 #{0}：{1}/{2} 条", "Key #{0}: {1}/{2}");
            Add(m, "↑ 上移", "↑ Up");
            Add(m, "↓ 下移", "↓ Down");
            Add(m, "删除", "Delete");
            Add(m, "清空该键", "Clear key");
            Add(m, "清空全部按键映射", "Clear all mappings");
            Add(m, "再次点击以确定清除", "Click again to confirm");
            Add(m, "重新读取映射（放弃改动）", "Re-read mappings (discard)");
            Add(m, "重新读取磁轴参数", "Re-read parameters");

            // ---------- 主界面：右侧选键页 ----------
            Add(m, "键盘", "Keyboard");
            Add(m, "鼠标", "Mouse");
            Add(m, "手柄", "Gamepad");
            Add(m, "多媒体", "Consumer");
            Add(m, "当前按键 #0 · 已选 0/12 · 在上方页面里点击要映射到的按键", "Key #0 - 0/12 - click a key above to map");

            // ---------- 主界面：下方操作区 ----------
            Add(m, "① 写入当前按键映射（主操作）", "1) Write this key (main)");
            Add(m, "② 全部映射操作", "2) All-keys operations");
            Add(m, "③ ⚠ 危险操作", "3) ⚠ Dangerous");
            Add(m, "写入当前按键映射（全部 12 个槽）", "Write this key (all 12 slots)");
            Add(m, "写入全部按键映射（0 ~ 13）", "Write all keys (0 ~ 13)");
            Add(m, "写入全部按键映射（0 ~ {0}）", "Write all keys (0 ~ {0})");
            Add(m, "清空全部映射 (CLEAR)", "Clear all (CLEAR)");
            Add(m, "主操作 = 把左侧列表整片写进当前按键，只动这一个键；后面的空槽由键盘自动清零，\r\n所以只发本地有的那几条映射。写完自动发 CMD_REFRESH，不用重新插拔键", "Main = write the list into the current key only; the keyboard clears the tail,\r\nso only your mappings are sent. CMD_REFRESH follows - no replug needed.");
            Add(m, "盘。", " ");

            // ---------- 主界面：状态栏 / 提示 ----------
            Add(m, "就绪。请先点“刷新设备”，选中你的键盘，再点“连接”。", "Ready. Click Refresh, pick your keyboard, then Connect.");
            Add(m, "就绪。请先点“刷新设备”，选中你的键盘后点“连接”。", "Ready. Click Refresh, pick your keyboard, then Connect.");
            Add(m, "正在枚举 HID 设备…", "Enumerating HID devices...");
            Add(m, "没有发现任何 HID 设备。请确认键盘已插好。", "No HID device found. Check the keyboard.");
            Add(m, "没有找到带指令接口（UsagePage=0xFF00, Usage=0x01）的设备，先列出全部 {0} 个 HID 设备。", "No command interface (UsagePage=0xFF00, Usage=0x01); listing all {0} HID devices.");
            Add(m, "请自行核对 VID/PID。", "Check VID/PID yourself.");
            Add(m, "⚠ 有 {0} 个设备都带指令接口（例如键盘 + 触摸板），", "⚠ {0} devices expose a command interface,");
            Add(m, "只有“#实例号”不同，连接前请确认是你自己的键盘。", "only the #instance differs - make sure it is your keyboard.");
            Add(m, "连接前请核对 VID/PID 是不是你的键盘。", "Check that the VID/PID is your keyboard.");
            Add(m, "设备列表已过滤：共发现 {0} 个 HID 设备，其中 {1} 个是命令接口，已只列出这 {1} 个。{2}", "Filtered: {0} HID devices, {1} command interface(s), showing those {1}. {2}");
            Add(m, "枚举失败：", "Enumeration failed: ");
            Add(m, "请先刷新并选择一个设备。", "Refresh and pick a device first.");
            Add(m, "打开设备失败：\r\n", "Open device failed:\r\n");
            Add(m, "提示", "Notice");
            Add(m, "警告", "Warning");
            Add(m, "错误", "Error");
            Add(m, "打开设备", "Open device");
            Add(m, "打开失败", "Open failed");
            Add(m, "已断开", "Disconnected");
            Add(m, "已断开（重新枚举）", "Disconnected (re-enumerating)");
            Add(m, "关闭设备", "Close device");
            Add(m, "已关闭指令接口", "Command interface closed");
            Add(m, "没有连接到键盘的指令接口。请先“刷新设备”→“连接”。", "Not connected. Use Refresh, then Connect.");

            // ---------- 主界面：映射增删改提示 ----------
            Add(m, "按键 #{0} 移除映射：{1}（第 {2} 条）", "Key #{0}: removed {1} (slot {2})");
            Add(m, "按键 #{0} 第 {1} 条替换为：{2}", "Key #{0} slot {1} -> {2}");
            Add(m, "按键 #{0} 已经有 {1} 条映射，达到上限。请先删除一条。", "Key #{0} already has {1} mappings (max). Delete one first.");
            Add(m, "按键 #{0} 上已经有 {1} 了。", "Key #{0} already has {1}.");
            Add(m, "按键 #{0} 加入映射：{1}（左键=替换选中项，右键/Shift+左键=追加，点已有的=删除）", "Key #{0}: added {1} (L=replace, R/Shift+L=add, click existing=remove)");
            Add(m, "已从本地列表删除一条映射（还没有写入键盘）。", "Removed one mapping locally (not written yet).");
            Add(m, "已清空按键 #{0} 的本地映射列表（还没写入键盘）。", "Key #{0}: local list cleared (not written yet).");
            Add(m, "再点一次「再次点击以确定清除」就会清空本地全部按键的映射列表（不影响键盘）。", "Click again to clear ALL local mappings (keyboard untouched).");
            Add(m, "已清空本地全部按键的映射列表（共 {0} 条，还没写入键盘）。", "Cleared all local mappings ({0} total, not written yet).");

            // ---------- 写入映射的确认框 / 结果 ----------
            Add(m, "把按键 #{0} 的映射写进键盘：\r\n\r\n", "Write key #{0} mappings to the keyboard:\r\n\r\n");
            Add(m, "  共 {1} 条映射{2}\r\n\r\n", "  {1} mapping(s){2}\r\n\r\n");
            Add(m, "只会改写按键 #{0} 自己的 EEPROM 区域，不影响其它按键；\r\n", "Only this key's EEPROM area is changed;\r\n");
            Add(m, "后面的空槽由键盘自动清零，所以只发 {3} 条指令。\r\n", "the keyboard clears the tail, so only {3} command(s) are sent.\r\n");
            Add(m, "写完会自动发一条 CMD_REFRESH，不用重新插拔键盘。\r\n\r\n继续？", "CMD_REFRESH is sent afterwards - no replug needed.\r\n\r\nContinue?");
            Add(m, "（即清掉这个键的全部映射）", " (clears all mappings of this key)");
            Add(m, "写入当前按键映射", "Write this key");
            Add(m, "写入按键 #{0}", "Write key #{0}");
            Add(m, "按键 #{0} 的 {1} 条映射已写入并已刷新（这个按键已变绿，别的按键不受影响）。", "Key #{0}: {1} mapping(s) written and refreshed (this key is green now).");
            Add(m, "把本地列表里的全部 {0} 条映射写进键盘（按键 0 ~ {1}）。\r\n\r\n", "Write all {0} local mappings (keys 0 ~ {1}).\r\n\r\n");
            Add(m, "每个按键只发本地有的那几条（后面的空槽键盘会自动清零）。\r\n", "Each key sends only its own mappings (the tail is cleared by the keyboard).\r\n");
            Add(m, "本地列表里没有的按键，键盘上对应的映射会被清掉。\r\n", "Keys missing from the local list will be cleared on the keyboard.\r\n");
            Add(m, "写入全部按键映射", "Write all keys");
            Add(m, "全部按键已写入并已刷新，所有按键已变绿，可以直接用了。", "All keys written and refreshed; everything is green.");
            Add(m, "全部按键写入过程中有指令没成功，请看日志和状态栏。", "Some commands failed while writing all keys - see log/status.");
            Add(m, "按键#{0} 槽{1} ← {2}", "key#{0} slot{1} <- {2}");
            Add(m, "按键#{0} 槽0 ← 空槽（清空该键）", "key#{0} slot0 <- empty (clear key)");
            Add(m, "这是危险操作：将清空键盘上【全部按键】的映射（EEPROM 里的整片映射区）。\r\n\r\n", "Dangerous: this clears ALL mappings on the keyboard.\r\n\r\n");
            Add(m, "本地列表不会被修改，之后可以用“写入全部按键映射”把本地列表重新写回去。\r\n\r\n继续？", "The local list is kept; you can write it back with Write all keys.\r\n\r\nContinue?");
            Add(m, "⚠ 清空全部映射", "⚠ Clear all mappings");
            Add(m, "清空全部映射（整片映射区）", "Clear all mappings (whole area)");
            Add(m, "已下发清空指令，本地列表也一并清空了（键盘上现在没有任何映射），按键按钮已全部变绿。", "Clear command sent; the local list was cleared too. All keys are green.");

            // ---------- 设备信息 / 同步 ----------
            Add(m, "正在读取键盘的设备信息…", "Reading device info...");
            Add(m, "读取设备信息", "Read device info");
            Add(m, "设备信息", "Device info");
            Add(m, "没有收到设备信息报文，按本地配置显示。", "No device-info frame; using local config.");
            Add(m, "没有收到键盘的设备信息，暂按本地配置显示（按键数 ", "No device info; using local config (keys ");
            Add(m, "）。", ").");
            Add(m, "键盘报告有 {0} 个按键，超过本地配置的上限 {1}，只显示前 {1} 个（需要改 DeviceLimits.KeyCount 并重新编译上位机）。", "Keyboard reports {0} keys, above the local limit {1}; showing the first {1}.");
            Add(m, "键盘报告：{0} 键 · 磁轴键盘（已显示磁轴参数面板）· 编码器 {1} 个。", "Keyboard: {0} keys · hall-effect (parameter panel shown) · {1} encoder(s).");
            Add(m, "键盘报告：{0} 键 · 非磁轴键盘（已隐藏磁轴参数面板，只做改键）· 编码器 {1} 个。", "Keyboard: {0} keys · not hall-effect (parameter panel hidden) · {1} encoder(s).");
            Add(m, "正在从键盘读取全部按键映射…", "Reading all mappings...");
            Add(m, "读取全部按键映射（0~{0}）", "Read all mappings (0~{0})");
            Add(m, "同步失败：查询指令没能发出去。", "Sync failed: query not sent.");
            Add(m, "正在从键盘读取磁轴参数…", "Reading parameters...");
            Add(m, "读取全部按键参数（0~{0}）", "Read all parameters (0~{0})");
            Add(m, "（有查询没收到 FIN，可能是固件版本较旧或是超时，结果可能不完整）", " (some queries had no FIN - results may be incomplete)");
            Add(m, "已从键盘同步：{0} 条映射（跳过 {1} 条），{2} 项参数。{3}{4}", "Synced: {0} mappings (skipped {1}), {2} parameters. {3}{4}");
            Add(m, "键盘上目前没有任何映射。", "The keyboard has no mappings.");
            Add(m, "同步完成", "Sync done");
            Add(m, "从键盘读取到 {0} 条映射、{2} 项参数，跳过 {1} 条。", "Read {0} mappings and {2} parameters (skipped {1}).");
            Add(m, "同步过程中出错：", "Sync error: ");
            Add(m, "同步失败", "Sync failed");
            Add(m, "写入当前按键映射（全部 {0} 个槽）", "Write this key (all {0} slots)");
            Add(m, "有指令正在执行，等它结束再同步。", "A command is running; wait for it.");
            Add(m, "正在重新读取全部按键映射…", "Re-reading all mappings...");
            Add(m, "重新读取全部按键映射（0~{0}）", "Re-read all mappings (0~{0})");
            Add(m, "重新读取映射失败：查询指令没能发出去。", "Re-read failed: query not sent.");
            Add(m, "已重新读取映射：本地恢复成键盘上的 {0} 条（丢弃本地改动），按键按钮全部变绿。", "Re-read: local list restored to {0} mapping(s); local edits discarded.");
            Add(m, "重新读取映射出错：", "Re-read error: ");
            Add(m, "这台键盘没有磁轴，没有参数可以读取。", "This keyboard has no hall sensors.");
            Add(m, "正在重新读取磁轴参数…", "Re-reading parameters...");
            Add(m, "重新读取全部按键参数（0~{0}）", "Re-read all parameters (0~{0})");
            Add(m, "重新读取参数失败：查询指令没能发出去。", "Re-read failed: query not sent.");
            Add(m, "已重新读取磁轴参数：{0} 项，值框和滑块已经换成键盘上的真实值，按键标记也复位了。", "Re-read {0} parameters; boxes/sliders now show the keyboard values.");
            Add(m, "重新读取参数出错：", "Re-read error: ");

            // ---------- 磁轴参数写入 / 校准 ----------
            Add(m, "提示：数值 = 满行程的千分比（11~979，40 = 4.0%）；RT 触发行程 / 上死区的滑块是「上=小、下=大」。", "Values are per-mille of full travel (11~979; 40 = 4.0%). RT trigger / upper deadzone sliders: top = min.");
            Add(m, "「写入参数」会把四个参数一起发给范围内的按键，写完自动刷新，不用重新插拔。", "Write sends all four parameters to the selected keys and refreshes.");
            Add(m, "有指令正在执行，等它结束再写参数。", "A command is running; wait for it.");
            Add(m, "当前按键 #{0}", "current key #{0}");
            Add(m, "选中的 {0} 个按键（{1}）", "{0} selected keys ({1})");
            Add(m, "把四个参数写入 {0}？\r\n\r\n{1}\r\n\r\n每条指令都会等键盘回 FIN，写完全局刷新一次，不用重新插拔。\r\n\r\n继续？", "Write the four parameters to {0}?\r\n\r\n{1}\r\n\r\nEach command waits for FIN; a refresh follows.\r\n\r\nContinue?");
            Add(m, "  {0} = {1}（{2}）", "  {0} = {1} ({2})");
            Add(m, "写入磁轴参数", "Write parameters");
            Add(m, "参数值 {0} 超出固件接受范围（{1}~{2}），已跳过。", "Value {0} out of range ({1}~{2}); skipped.");
            Add(m, "按键#{0} {1} = {2}（{3:0.0}% 行程）", "key#{0} {1} = {2} ({3:0.0}% travel)");
            Add(m, "正在写入参数：{0}/{1}  {2}", "Writing parameters {0}/{1}  {2}");
            Add(m, "参数已写入 {0}，这些按键的数字已经恢复成绿色。", "Parameters written to {0}; those keys are green again.");
            Add(m, "参数已写入 {0} 并已刷新，可以直接用了。", "Parameters written to {0} and refreshed.");
            Add(m, "⚠ 有 {0} 条参数没有写成功（没收到正确的 FIN），对应按键的数字仍然是红色。请检查连接和设备是否选对。", "⚠ {0} parameter(s) failed (no valid FIN); those keys stay red.");
            Add(m, "参数写入完成，但有 {0} 条没成功，对应按键仍标红（见日志）。", "Write finished with {0} failure(s); those keys stay red.");
            Add(m, "校准全部按键：\r\n\r\n", "Calibrate all keys:\r\n\r\n");
            Add(m, "固件会从按键 0 开始逐个校准，每个键都需要你【先按住再松开】。\r\n", "The firmware calibrates key by key; press and release each one.\r\n");
            Add(m, "整个过程中固件的主循环被占用，其它指令不会被处理。\r\n", "The firmware is busy during calibration; other commands wait.\r\n");
            Add(m, "请预留大约 ", "Allow about ");
            Add(m, " × 每个键 2 秒 的时间，中途不要拔线。\r\n\r\n开始校准？", " x 2 s per key; do not unplug.\r\n\r\nStart?");
            Add(m, "校准当前按键 #{0}：\r\n\r\n请【先按住再松开】这个键，固件会记录行程的上下限。\r\n\r\n开始校准？", "Calibrate key #{0}:\r\n\r\npress and release it; the firmware records the travel limits.\r\n\r\nStart?");
            Add(m, "磁轴校准", "Hall calibration");
            Add(m, "校准全部按键", "Calibrate all keys");
            Add(m, "校准按键 #{0}", "Calibrate key #{0}");
            Add(m, "校准中：请从按键 #0 开始，依次「按住 → 松开」每一个键（键盘会逐个回报进度）…", "Calibrating: press and release each key from #0 (progress is reported)...");
            Add(m, "校准中：请对按键 #{0}「按住 → 松开」…", "Calibrating: press and release key #{0}...");
            Add(m, "校准中：请从按键 #0 开始，依次“按住 → 松开”每一个键（键盘会逐个回报进度）…", "Calibrating: press and release each key from #0 (progress is reported)...");
            Add(m, "校准中：请对按键 #{0}“按住 → 松开”…", "Calibrating: press and release key #{0}...");
            Add(m, "校准中：按键 #{0} 已完成（{1}/{2}），请继续按下一个键…", "Calibrating: key #{0} done ({1}/{2}), keep going...");
            Add(m, "校准中：按键 #{0} 已完成（{1}/{2}）…", "Calibrating: key #{0} done ({1}/{2})...");
            Add(m, "校准失败：指令没能发出去。", "Calibration failed: command not sent.");
            Add(m, "校准指令没能发出去。", "Calibration command not sent.");
            Add(m, "校准等待超时：没有收到键盘的完成回报（FIN）。", "Calibration timeout: no FIN.");
            Add(m, "校准等待超时：没有收到键盘的完成回报（FIN）。可能是没按到位，或者固件版本不支持 FIN。", "Calibration timeout: no FIN received (key not pressed, or old firmware).");
            Add(m, "校准完成。", "Calibration done.");
            Add(m, "按键 #{0} 校准完成。", "Key #{0} calibrated.");
            Add(m, "全部校准完成：最后完成的是按键 #{0}。建议重新插拔键盘一次。", "All calibrated (last key #{0}). Replug the keyboard.");
            Add(m, "按键 #{0} 校准完成。建议重新插拔键盘一次。", "Key #{0} calibrated. Replug the keyboard.");
            Add(m, "校准完成", "Calibration done");
            Add(m, "全部按键校准完成", "All keys calibrated");
            Add(m, "按键 #{0} 校准完成", "Key #{0} calibrated");
            Add(m, "有批量指令正在执行，请等它结束。", "A batch is running; wait for it.");
            Add(m, "正在{0}：{1}/{2}  {3}", "{0}: {1}/{2}  {3}");
            Add(m, "{0} 完成，但有 {1} 条指令没有收到正确的 FIN（见日志）。", "{0} finished with {1} missing FIN(s) - see the log.");
            Add(m, "刷新按键映射（EEPROM → 内存）", "refresh key mappings (EEPROM -> RAM)");
            Add(m, "刷新磁轴参数（EEPROM → 内存）", "refresh parameters (EEPROM -> RAM)");
            Add(m, "刷新映射", "Refresh mappings");
            Add(m, "刷新参数", "Refresh parameters");
            Add(m, "正在写入映射", "Writing mappings");

            // ---------- 设置对话框 ----------
            Add(m, "参数设置 - KeyboardOG", "Settings - KeyboardOG");
            Add(m, "发送与等待", "Sending & waiting");
            Add(m, "发送频率：", "Send rate:");
            Add(m, "回应超时：", "Response timeout:");
            Add(m, "每条指令等键盘回 FIN 之后再发下一条（建议开启）", "Wait for FIN before sending the next command");
            Add(m, "说明：\r\n", "Notes:\r\n");
            Add(m, "· 发送频率 = 批量写入时两条指令之间的最小间隔，建议 500Hz（约 2ms 一条）。\r\n", "· Send rate = minimum gap between commands when writing in bulk (500 Hz is fine).\r\n");
            Add(m, "· 回应超时 = 等 ACK / FIN 的最长时间，超时会在日志里标成橙色“超时”。\r\n", "· Response timeout = max wait for ACK/FIN (shown orange in the log).\r\n");
            Add(m, "· 关掉“等 FIN”会连续快发，容易丢指令，建议保持开启。", "· Turning \"wait for FIN\" off may drop commands; keep it on.");
            Add(m, "保存位置：", "Saved at:");
            Add(m, "确定", "OK");
            Add(m, "取消", "Cancel");
            Add(m, "恢复默认", "Defaults");
            Add(m, "≈ {0} ms / 条（约 {1} 条/秒）", "≈ {0} ms per command (~{1}/s)");
            Add(m, "设置文件保存失败：\r\n", "Could not save the settings file:\r\n");
            Add(m, "\r\n\r\n本次设置只在这次运行里有效。", "\r\n\r\nThey apply to this run only.");
            Add(m, "设置已生效并保存：", "Settings saved: ");
            Add(m, "    （", "    (");
            Add(m, "设置", "Settings");

            // ---------- 日志窗口 ----------
            Add(m, "指令日志 - KeyboardOG", "Command log - KeyboardOG");
            Add(m, "指令日志 - KeyboardOG   （发送 {0} / 接收 {1}）", "Command log - KeyboardOG   (sent {0} / received {1})");
            Add(m, "暂停刷新", "Pause");
            Add(m, "自动滚动", "Auto scroll");
            Add(m, "清空日志", "Clear log");
            Add(m, "导出 CSV", "Export CSV");
            Add(m, "关闭本窗口只是隐藏，不会影响主窗口。", "Closing this window only hides it.");
            Add(m, "共 {0} 条   ·   发送 {1}   接收确认 {2}   已完成 {3}   超时 {4}   失败 {5}", "{0} entries · sent {1}  ack {2}  done {3}  timeout {4}  failed {5}");
            Add(m, "时间", "Time");
            Add(m, "类型", "Kind");
            Add(m, "状态", "State");
            Add(m, "指令", "Command");
            Add(m, "详情", "Detail");
            Add(m, "数据 (8 字节)", "Data (8 bytes)");
            Add(m, "#,时间,类型,状态,指令,详情,数据", "#,time,kind,state,command,detail,data");
            Add(m, "已导出：\r\n", "Exported:\r\n");
            Add(m, "导出成功", "Export done");
            Add(m, "那个位置没有写入权限：\r\n", "No write permission at:\r\n");
            Add(m, "\r\n\r\n已经改存到「文档」里了：\r\n", "\r\n\r\nSaved to Documents instead:\r\n");
            Add(m, "导出成功（换了位置）", "Export done (new location)");
            Add(m, "导出失败：没有写入权限。\r\n\r\n原路径：", "Export failed: no write permission.\r\n\r\nPath: ");
            Add(m, "\r\n\r\n换个有权限的目录（例如「文档」）再试。\r\n\r\n", "\r\n\r\nTry a writable folder (e.g. Documents).\r\n\r\n");
            Add(m, "导出失败", "Export failed");
            Add(m, "导出失败：", "Export failed: ");
            Add(m, "CSV 文件 (*.csv)|*.csv|所有文件 (*.*)|*.*", "CSV file (*.csv)|*.csv|All files (*.*)|*.*");
            Add(m, "未知", "Unknown");

            // ---------- 日志内容：状态 / 种类 ----------
            Add(m, "等待", "Wait");
            Add(m, "发送", "Send");
            Add(m, "接收", "Recv");
            Add(m, "完成", "Done");
            Add(m, "超时", "Timeout");
            Add(m, "失败", "Failed");
            Add(m, "信息", "Info");
            Add(m, "警告", "Warning");
            Add(m, "错误", "Error");
            Add(m, "启动", "Startup");
            Add(m, "发送中…", "Sending...");

            // ---------- 选键画布 ----------
            Add(m, "左键", "Left");
            Add(m, "右键", "Right");
            Add(m, "中键", "Middle");
            Add(m, "鼠标按键映射：offset 0/1/2 → mouseButtons bit0/1/2", "Mouse: offset 0/1/2 -> mouseButtons bit0/1/2");
            Add(m, "（固件未实现鼠标 X/Y 轴与滚轮量映射）", "(mouse X/Y and wheel are not implemented)");
            Add(m, "LT 未实现", "LT n/a");
            Add(m, "RT 未实现", "RT n/a");
            Add(m, "手柄映射：offset 0~15 → xboxButtons bit0~bit15（描述符里的 B1~B16）", "Gamepad: offset 0~15 -> xboxButtons bit0~bit15 (B1~B16)");
            Add(m, "静音", "Mute");
            Add(m, "音量 -", "Vol -");
            Add(m, "音量 +", "Vol +");
            Add(m, "播放 / 暂停", "Play/Pause");
            Add(m, "下一曲", "Next");
            Add(m, "上一曲", "Prev");
            Add(m, "多媒体映射：offset 0~5 按描述符顺序 → mediaButtons bit0~bit5", "Consumer: offset 0~5 -> mediaButtons bit0~bit5");

            // ---------- 协议 / 目录里的显示名 ----------
            Add(m, "磁轴", "hall");
            Add(m, "非磁轴", "non-hall");
            Add(m, "未知(0x{0:X2})", "unknown(0x{0:X2})");
            Add(m, "{0} 键 · {1} · 编码器 {2} 个 · 设备类型 0x{3:X2}", "{0} keys · {1} · {2} encoder(s) · type 0x{3:X2}");
            Add(m, "中键/滚轮按下", "middle / wheel click");
            Add(m, "B16 (保留)", "B16 (reserved)");
            Add(m, "播放/暂停", "play/pause");
            Add(m, "修饰键 HID 0x{0:X2} → keyboardKeys[13] bit{1}（下发 0x{2:X2}）", "modifier HID 0x{0:X2} -> keyboardKeys[13] bit{1} (send 0x{2:X2})");
            Add(m, "(空)", "(empty)");
            Add(m, "RT 触发行程", "RT trigger");
            Add(m, "按下时触发所需的行程", "travel needed to trigger");
            Add(m, "RT 释放行程", "RT release");
            Add(m, "抬起时释放所需的行程", "travel needed to release");
            Add(m, "上死区", "Upper deadzone");
            Add(m, "接近底部时的死区", "deadzone near the bottom");
            Add(m, "下死区", "Lower deadzone");
            Add(m, "接近顶部时的死区", "deadzone near the top");
            Add(m, "= {0} / 1000（满行程的 {1:0.0}%）", "= {0} / 1000 ({1:0.0}% of travel)");
            Add(m, "{0} Hz（约 {1} ms/条）· 超时 {2} ms · {3}", "{0} Hz (~{1} ms/cmd) · timeout {2} ms · {3}");
            Add(m, "等 FIN 再发下一条", "wait for FIN");
            Add(m, "不等 FIN", "no FIN wait");
            Add(m, "内部错误", "Internal error");
            Add(m, "枚举设备", "Enumerate");
            Add(m, "发现 {0} 个 HID 设备，其中 {1} 个是指令接口 (UsagePage=0xFF00, Usage=0x01)", "Found {0} HID devices, {1} with the command interface");
            Add(m, "已打开 [{0}] VID_{1:X4}&PID_{2:X4} {3}{4}", "Opened [{0}] VID_{1:X4}&PID_{2:X4} {3}{4}");
            Add(m, "  (指令接口)", "  (command interface)");   // 已不再出现在连接详情里，留着以防别处用到
            Add(m, "  (非指令接口，指令可能无效)", "  (not a command interface)");
            Add(m, "已按 VID_{0:X4}&PID_{1:X4} 选择指令接口", "Selected command interface VID_{0:X4}&PID_{1:X4}");
            Add(m, "发送失败: ", "Send failed: ");
            Add(m, "已发送（未等待回应）", "Sent (no wait)");
            Add(m, "已发送（不收取回传）", "Sent (no response expected)");
            Add(m, "接收确认：键盘原样回传了本条指令", "Ack: the keyboard echoed this command");
            Add(m, "已收到键盘回应", "Response received");
            Add(m, "等待 {0} ms 无回应", "No response in {0} ms");
            Add(m, "回应长度异常", "Bad response length");
            Add(m, "ACK：键盘已收到 {0}（指令 0x{1:X2}，上位机序号 0x{2:X2}）", "ACK: {0} received (cmd 0x{1:X2}, seq 0x{2:X2})");
            Add(m, "FIN：{0} 执行完毕（按键 #{1}，上位机序号 0x{2:X2}）", "FIN: {0} done (key #{1}, seq 0x{2:X2})");
            Add(m, "查询结果·键值：{0} [device={1}, offset=0x{2:X2}, slot={3}]", "Mapping: {0} [device={1}, offset=0x{2:X2}, slot={3}]");
            Add(m, "查询结果·参数：{0} = {1}（{2}）", "Parameter: {0} = {1} ({2})");
            Add(m, "设备信息（解析失败）", "Device info (parse failed)");
            Add(m, "设备信息：", "Device info: ");
            Add(m, "设备回显了指令本身（可能是数据上报）", "Device echoed the command (data report?)");
            Add(m, "未识别的回应，原始数据见“数据”列", "Unrecognized response - see the Data column");
            Add(m, "回应对不上（{0}）：收到 序号0x{1:X2}，期望 序号0x{2:X2}", "Response mismatch ({0}): got seq 0x{1:X2}, expected 0x{2:X2}");
            Add(m, "已完成，共收到 {0} 条数据", "Done, {0} data item(s)");
            Add(m, "（常见原因：发太快 / 上一条的回应没读走）", "(usually: too fast / stale response)");
            Add(m, "等待 {0} ms 没有收到 FIN，已收到 {1} 条数据", "No FIN in {0} ms, {1} data item(s)");
            Add(m, "等待 {0} ms 没有收到 FIN：{1}（键盘可能没在处理指令）", "No FIN in {0} ms: {1}");
            Add(m, "回应错位", "Response mismatch");
            Add(m, "收到的第一条回应不是本条指令的，管道里有残留：", "First response was not for this command: ");
            Add(m, "指令发送失败：", "Send failed: ");
            Add(m, "还有指令正在下发，现在退出可能会让键盘停在中间状态。仍然退出？", "Commands are still being sent; exit anyway?");
            Add(m, "确认退出", "Confirm exit");
            Add(m, "UI 线程异常", "UI thread exception");
            Add(m, "未处理异常（进程即将退出）", "Unhandled exception (process will exit)");
            Add(m, "启动失败", "Startup failed");
            Add(m, "时间: ", "Time: ");
            Add(m, "类型: ", "Type: ");
            Add(m, "：\r\n\r\n", ": \r\n\r\n");
            Add(m, "\r\n\r\n详细信息已写入：\r\n", "\r\n\r\nDetails written to:\r\n");

            // ---------- 设置项的下拉 / 单位 ----------
            // ---------- 补充：磁轴面板 / 主界面里代码拼出来的 ----------
            Add(m, "校准：", "Calibrate:");
            Add(m, "校准当前按键", "Calibrate this key");
            Add(m, "全选 / 全不选", "All / none");
            Add(m, "写入范围（左键单选/右键多选）：", "Scope (L=single, R=multi):");
            Add(m, "将四个参数\r\n发给选中按键", "Send all four\r\nto the selected keys");
            Add(m, "⚠ 校准是阻塞操作：需要「按住 → 松开」，\r\n期间键盘不处理其它指令。\r\n校准完会自动刷新参数。",
                "⚠ Calibration blocks the firmware: press and release,\r\nother commands wait.\r\nParameters refresh afterwards.");
            Add(m, "主操作 = 把左侧列表整片写进当前按键，只动这一个键；后面的空槽由键盘自动清零，\r\n所以只发本地有的那几条映射。写完自动发 CMD_REFRESH，不用重新插拔键\r\n盘。",
                "Main = write this key's list to the keyboard only; the keyboard clears the tail,\r\nso only your mappings are sent. CMD_REFRESH follows - no replug needed.");
            Add(m, "提示：数值 = 满行程的千分比（11~979，40 = 4.0%）；RT 触发行程 / 上死区的滑块是「上=小、下=大」。「写入参数」会把四个参数一起发给范围内的按键，写完自动刷新，不用重新插拔。",
                "Values are per-mille of full travel (11~979; 40 = 4.0%). RT trigger / upper deadzone sliders: top = min. Write sends all four parameters to the selected keys and refreshes.");
            Add(m, "按键报告：{0} 键 · {1} · 编码器 {2} 个", "Keyboard: {0} keys · {1} · {2} encoder(s)");
            Add(m, "刷新按键映射", "Refresh mappings");
            Add(m, "键", "keys");
            Add(m, "主操作 = 把左侧列表整片写进当前按键，只动这一个键；后面的空槽由键盘自动清零，\r\n所以只发本地有的那几条映射。写完自动发 CMD_REFRESH，不用重新插拔键盘。",
                "Main = write this key's list to the keyboard only; the keyboard clears the tail,\r\nso only your mappings are sent. CMD_REFRESH follows - no replug needed.");            Add(m, "键盘已经拔掉，已自动断开连接。插好后点“刷新设备”再连接。",
                "Keyboard unplugged - disconnected. Click Refresh to reconnect.");
            Add(m, "检测到键盘已拔出，自动断开连接", "Keyboard removed: auto-disconnected");
            Add(m, "按键#{0} 追加 {1}", "key#{0} append {1}");
            Add(m, "按键#{0} 清空全部映射", "key#{0} clear all mappings");
            Add(m, "按键#{0} 清掉槽{1}以后的旧映射", "key#{0} clear slots from {1}");
            Add(m, "写入按键 #{0}", "Write key #{0}");
            Add(m, "写入按键 #{0}（{1} 条指令）", "Write key #{0} ({1} commands)");
            Add(m, "写入全部按键映射（{0} 条指令）", "Write all keys ({0} commands)");
            Add(m, "执行中", "Running");
            Add(m, "已发送", "Sent");
            Add(m, "已完成", "Done");
            Add(m, "(无产品名)", "(no product name)");
            Add(m, "探测设备信息", "probe device info");
            Add(m, "探测", "Probe");
            Add(m, "这个设备没有回应设备信息查询（不是键盘）", "This device did not answer the device-info query (not a keyboard)");
            Add(m, "连接自检", "link check");
            Add(m, "KeyboardOG 改键工具已启动。协议：Report ID 0x04，8 字节指令。", "KeyboardOG started. Protocol: Report ID 0x04, 8-byte commands.");
            Add(m, "⚠ 有 {0} 个设备都带指令接口且都能回应探测（例如两块键盘），", "⚠ {0} devices expose a command interface and answer the probe,");
            Add(m, "还没有连接到键盘的指令接口。请先“刷新设备”→“连接”。", "Not connected to the keyboard. Use Refresh, then Connect.");
            Add(m, "按键 #{0} 的本地内容和键盘完全一致，没有需要发送的指令。", "Key #{0} already matches the keyboard; nothing to send.");
            Add(m, "按键 #{0} 的 {1} 条映射已写入（只发了 {2} 条指令），别的按键不受影响。", "Key #{0}: {1} mappings written ({2} commands sent); other keys untouched.");
            Add(m, "所有按键的本地内容都和键盘一致，没有需要发送的指令。", "Every key already matches the keyboard; nothing to send.");
            Add(m, "全部按键已写入（共 {0} 条指令，{1} 个按键与键盘一致已跳过），可以直接用了。", "All keys written ({0} commands; {1} keys already matched).");
            Add(m, "已从键盘同步：{0} 条映射（跳过 {1} 条），{2} 项参数。", "Synced: {0} mappings (skipped {1}), {2} parameters.");
            Add(m, "磁轴参数 / 校准（当前 #0）", "Hall parameters / calibration (key #0)");
            Add(m, "键盘 —", "kbd —");
            Add(m, "版本检查：固件 {0} / 软件 {1}，兼容", "Version check: firmware {0} / software {1}, compatible");
            Add(m, "版本检查：固件 {0} / 软件 {1}，固件版本过老", "Version check: firmware {0} / software {1}, firmware too old");
            Add(m, "版本检查：固件 {0} / 软件 {1}，固件版本过新", "Version check: firmware {0} / software {1}, firmware too new");
            Add(m, "版本检查", "Version check");
            Add(m, "支持固件 {0} ~ {1}", "supports firmware {0} ~ {1}");
            Add(m, "固件版本过老，请更新固件", "Firmware is too old\nPlease update the firmware");
            Add(m, "固件版本过新，请更新软件", "Firmware is too new\nPlease update the software");
            Add(m, "软件版本与固件版本不匹配可能会导致意料之外的问题",
                "Version mismatch may cause unexpected problems");
            Add(m, "中文", "中文");
            Add(m, "语言/Language", "语言/Language");
            Add(m, "设置/config", "设置/config");
            return m;
        }

        private static void Add(Dictionary<string, string> m, string zh, string en)
        {
            if (!m.ContainsKey(zh)) m[zh] = en;
        }


        // ============================================================
        //  内部：由模板自动编出来的正则规则
        // ============================================================
        private sealed class Rule
        {
            public Regex Rx;
            public string ToFormat;      // 目标语言的格式串
            public int[] GroupOf;        // 第 n 个占位符用第几个捕获组（0 基）
        }

        private static readonly object Sync = new object();
        private static List<Rule> _rulesToEn;
        private static List<Rule> _rulesToZh;
        private static Dictionary<string, string> _enToZh;

        private static readonly Regex Placeholder =
            new Regex(@"\{(\d+)(?::[^}]*)?\}", RegexOptions.Compiled);

        /// <summary>把 "{0} ... {1}" 这种模板编成"匹配已格式化整句"的正则。</summary>
        private static Rule BuildRule(string fromFormat, string toFormat)
        {
            // 同一个占位符可能在模板里出现多次（"{1} ... {1}"），
            // 正则里每个"出现"都要能吃字符，但捕获组只编一套（按首次出现的顺序）：
            // 这样 args 的下标就是占位符编号，string.Format 直接能用。
            var pieces = new List<KeyValuePair<string, int>>();   // 文本 / 后面跟的占位符编号（-1 = 没占位符）
            var slots = new List<int>();                          // 出现过的占位符（按首次出现顺序）
            int pos = 0;

            foreach (Match m in Placeholder.Matches(fromFormat))
            {
                pieces.Add(new KeyValuePair<string, int>(fromFormat.Substring(pos, m.Index - pos), -1));
                int idx = int.Parse(m.Groups[1].Value);
                if (!slots.Contains(idx)) slots.Add(idx);
                pieces.Add(new KeyValuePair<string, int>(string.Empty, idx));
                pos = m.Index + m.Length;
            }
            pieces.Add(new KeyValuePair<string, int>(fromFormat.Substring(pos), -1));

            var sb = new StringBuilder("^");
            foreach (var piece in pieces)
            {
                if (piece.Value < 0) sb.Append(Regex.Escape(piece.Key).Replace("\\ ", " "));
                else sb.Append("(.+?)");                          // 每次出现都吃字符
            }
            sb.Append("$");

            // 捕获组的编号是按"出现顺序"排的（重复的占位符会占多个组），
            // 而 string.Format 的参数是按"占位符编号"给的 ——
            // 所以每个占位符要用它**第一次出现**时的那个组号。
            var groupOf = new int[slots.Count == 0 ? 0 : (MaxSlot(slots) + 1)];
            int groupNo = 0;
            foreach (var piece in pieces)
            {
                if (piece.Value < 0) continue;
                groupNo++;
                if (groupOf[piece.Value] == 0) groupOf[piece.Value] = groupNo;
            }

            return new Rule
            {
                Rx = new Regex(sb.ToString(), RegexOptions.Singleline),
                ToFormat = toFormat,
                GroupOf = groupOf,
            };
        }

        private static int MaxSlot(List<int> slots)
        {
            int max = 0;
            foreach (int s in slots) if (s > max) max = s;
            return max;
        }

        private static void EnsureTables()
        {
            if (_rulesToEn != null) return;
            lock (Sync)
            {
                if (_rulesToEn != null) return;

                var toEn = new List<Rule>();
                var toZh = new List<Rule>();
                var rev = new Dictionary<string, string>();

                foreach (var kv in Map)
                {
                    if (!rev.ContainsKey(kv.Value)) rev[kv.Value] = kv.Key;

                    if (Placeholder.IsMatch(kv.Key) && Placeholder.IsMatch(kv.Value))
                    {
                        toEn.Add(BuildRule(kv.Key, kv.Value));
                        toZh.Add(BuildRule(kv.Value, kv.Key));
                    }
                }

                // 长的模板优先匹配，避免被短模板抢先
                Comparison<Rule> byLen = (a, b) => b.ToFormat.Length.CompareTo(a.ToFormat.Length);
                toEn.Sort(byLen);
                toZh.Sort(byLen);

                _enToZh = rev;
                _rulesToZh = toZh;
                _rulesToEn = toEn;
            }
        }

        /// <summary>把一句话翻成当前语言（翻不动就原样返回）。</summary>
        public static string T(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            string one = (Current == UiLanguage.Chinese) ? ToChinese(text) : ToEnglish(text);
            if (one != text) return one;

            //整段没命中：可能是多行拼出来的（比如设置窗口下面那段"说明"），
            //按行再试一次（每行通常就是表里的一条）。
            if (text.IndexOf('\n') >= 0) return TParts(text);

            return text;
        }

        /// <summary>显式给中英两版（新代码用这个更清楚）。</summary>
        public static string T(string zh, string en)
        {
            return IsEn ? en : zh;
        }

        /// <summary>带参数的句子：中英各给一个格式串。</summary>
        public static string F(string zhFormat, string enFormat, params object[] args)
        {
            return string.Format(IsEn ? enFormat : zhFormat, args);
        }

        /// <summary>
        /// 由多段拼出来的整段文字（对话框正文那种）：先整段查表，
        /// 查不到就按行拆开逐行翻（每段通常都是表里的一条）。
        /// </summary>
        public static string TParts(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;


            // 注意：这里直接调 ToChinese/ToEnglish，**不能**再调 T() ——
            // T() 翻不动时又会回头调 TParts()，那就死递归了（会直接栈溢出崩掉）。
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append("\r\n");

                // 表里有些条目是带行尾 "\r\n" 写的（源码里就是那么拼的），
                // 拆行之后行尾没了，所以两种都试一下。
                string line = lines[i];
                string done = (Current == UiLanguage.Chinese) ? ToChinese(line) : ToEnglish(line);
                if (done == line)
                {
                    string withCrLf = line + "\r\n";
                    string done2 = (Current == UiLanguage.Chinese) ? ToChinese(withCrLf) : ToEnglish(withCrLf);
                    if (done2 != withCrLf) done = done2.Substring(0, done2.Length - 2);
                }
                sb.Append(done);
            }
            return sb.ToString();
        }

        private static string ToEnglish(string text)
        {
            EnsureTables();

            string en;
            if (Map.TryGetValue(text, out en)) return en;

            foreach (var r in _rulesToEn)
            {
                var m = r.Rx.Match(text);
                if (!m.Success) continue;
                return ApplyRule(r, m);
            }
            return text;
        }

        private static string ToChinese(string text)
        {
            EnsureTables();

            string zh;
            if (_enToZh.TryGetValue(text, out zh)) return zh;

            foreach (var r in _rulesToZh)
            {
                var m = r.Rx.Match(text);
                if (!m.Success) continue;
                return ApplyRule(r, m);
            }
            return text;
        }

        private static string ApplyRule(Rule r, Match m)
        {
            var args = new object[r.GroupOf.Length];
            for (int i = 0; i < args.Length; i++)
            {
                int g = r.GroupOf[i];
                // 捕获到的内容本身可能也是一句中文（比如状态栏里嵌了 warn 文本），
                // 所以再翻一遍；捕获到的是数字时 T() 会原样返回，不会有副作用。
                args[i] = (g > 0 && g < m.Groups.Count) ? T(m.Groups[g].Value) : string.Empty;
            }
            try { return string.Format(r.ToFormat, args); }
            catch (FormatException) { return m.Value; }
        }

        /// <summary>
        /// 递归把控件树上的静态文本换成当前语言。
        /// 表是双向的，所以可以反复调用（切英文、切回中文都靠它）。
        /// </summary>
        /// <summary>
        /// 这些控件的文本**不翻译**：
        ///   keyboard1 = 可视化键盘画布，里面的键名是 HID 键名（Esc / F1 / Pause…），
        ///   翻译了反而看不懂（而且它内部是一堆 Button）。
        /// </summary>
        private static readonly HashSet<string> NeverTranslate =
            new HashSet<string> { "keyboard1" };

        public static void Apply(Control root)
        {
            if (root == null) return;
            if (NeverTranslate.Contains(root.Name)) return;      // 连子树一起跳过

            if (!AlwaysBilingual.Contains(root.Name))
            {
                string now = root.Text;
                if (!string.IsNullOrEmpty(now))
                {
                    string want = T(now);
                    if (want != now) root.Text = want;
                }
            }

            // ListView 的列头不算 Control.Text，单独处理
            var lv = root as ListView;
            if (lv != null)
            {
                foreach (ColumnHeader col in lv.Columns)
                {
                    if (string.IsNullOrEmpty(col.Text)) continue;
                    col.Text = T(col.Text);
                }
            }

            // 语言下拉框的选项固定不动（"中文" / "English"）
            var cb = root as ComboBox;
            if (cb != null && cb.Name != "cboLanguage")
            {
                for (int i = 0; i < cb.Items.Count; i++)
                {
                    var s = cb.Items[i] as string;
                    if (s == null) continue;
                    string want = T(s);
                    if (want != s) cb.Items[i] = want;
                }
            }

            foreach (Control c in root.Controls) Apply(c);
        }
    }
}
