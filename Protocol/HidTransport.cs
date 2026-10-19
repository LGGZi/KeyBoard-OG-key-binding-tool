using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KeyboardOG
{
    /// <summary>一条待发送的 8 字节指令。</summary>
    public sealed class CommandRequest
    {
        /// <summary>指令名，例如 "SET_MAP"。</summary>
        public string Name;

        /// <summary>可读说明，会显示在日志的“详情”列。</summary>
        public string Detail;

        /// <summary>8 字节负载。</summary>
        public byte[] Payload;

        /// <summary>等待回应的超时（毫秒）。&lt;= 0 表示只发送、不等待回应。</summary>
        public int ResponseTimeoutMs;

        public static CommandRequest Of(string name, string detail, byte[] payload, int responseTimeoutMs = 0)
        {
            return new CommandRequest
            {
                Name = name,
                Detail = detail,
                Payload = payload,
                ResponseTimeoutMs = responseTimeoutMs,
            };
        }
    }

    /// <summary>
    /// HID 通信层。
    ///
    /// HIDComm.dll 内部使用全局状态（选中的句柄、设备表、错误缓冲），
    /// 且 Hid_ReadResponse 是阻塞调用，所以这里用**一条专用工作线程 + 队列**
    /// 把全部原生调用串行化；界面线程永远不做阻塞 IO，也不会跨线程访问控件。
    /// </summary>
    public sealed class HidTransport : IDisposable
    {
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        private readonly SynchronizationContext _ui;
        private Thread _thread;
        private volatile bool _disposed;

        /// <summary>
        /// 上一条指令是不是"没收到对得上的 FIN"（超时/错位）？
        /// 是的话管道里可能留着它晚到的回应，下一条发之前要先清掉。
        /// 只在出过问题时才清 —— 正常情况下管道是空的，没必要每次都去读一个超时。
        /// </summary>
        private volatile bool _mayHaveStaleFrames;

        private volatile bool _connected;

        public CommandLogStore Log { get; private set; }

        /// <summary>当前是否已打开指令接口。</summary>
        public bool IsConnected { get { return _connected; } }

        /// <summary>最近一次枚举到的设备（仅在 UI 线程读）。</summary>
        public List<HidNative.HidDeviceInfo> Devices { get; private set; }

        /// <summary>连接状态变化（在 UI 线程触发）。</summary>
        public event Action<bool, string> ConnectionChanged;

        /// <summary>队列长度变化（在 UI 线程触发），方便界面显示“正在执行的指令”。</summary>
        public event Action<int> QueueLengthChanged;

        public HidTransport(CommandLogStore log, SynchronizationContext ui)
        {
            Log = log;
            _ui = ui ?? new SynchronizationContext();
            Devices = new List<HidNative.HidDeviceInfo>();
        }

        // ------------------------------------------------------------
        // 生命周期
        // ------------------------------------------------------------
        public void Start()
        {
            if (_thread != null) return;
            _thread = new Thread(WorkerLoop);
            _thread.IsBackground = true;
            _thread.Name = "HID-Transport";
            _thread.Start();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 先让工作线程把队列里剩下的作业做完再退出。
            try { _queue.CompleteAdding(); } catch { }

            bool joined = true;
            if (_thread != null) joined = _thread.Join(3000);

            // 只有线程真的退出了才能销毁队列：否则它可能还在 GetConsumingEnumerable 上，
            // 会抛 ObjectDisposedException 到后台线程，退出时弹一个没必要的错误框。
            if (joined)
            {
                try { _queue.Dispose(); } catch { }
            }
        }

        private void WorkerLoop()
        {
            foreach (var job in _queue.GetConsumingEnumerable())
            {
                try { job(); }
                catch (Exception ex)
                {
                    Log.Add(LogKind.Error, "内部错误", ex.Message, null, LogState.Failed);
                }
            }
        }

        /// <summary>把一个作业排到 HID 线程上执行。</summary>
        private void Post(Action job)
        {
            if (_disposed) return;
            try
            {
                _queue.Add(job);
                RaiseOnUi(() => { if (QueueLengthChanged != null) QueueLengthChanged(_queue.Count); });
            }
            catch (InvalidOperationException) { /* 已关闭 */ }
        }

        private void RaiseOnUi(Action a)
        {
            if (_ui == null) { a(); return; }
            _ui.Post(_ => a(), null);
        }

        private void SetConnected(bool value, string message)
        {
            if (_connected == value && string.IsNullOrEmpty(message)) return;
            _connected = value;
            RaiseOnUi(() => { if (ConnectionChanged != null) ConnectionChanged(value, message); });
        }

        // ------------------------------------------------------------
        // 异步包装
        // ------------------------------------------------------------
        private static TaskCompletionSource<T> NewTcs<T>()
        {
            return new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>枚举全部 HID 设备（会关闭当前已选设备）。</summary>
        public Task<List<HidNative.HidDeviceInfo>> EnumerateAsync()
        {
            var tcs = NewTcs<List<HidNative.HidDeviceInfo>>();
            Post(() =>
            {
                var list = new List<HidNative.HidDeviceInfo>();
                try
                {
                    var buffer = new HidNative.HidDeviceInfo[HidNative.MaxDevices];
                    int count = HidNative.Hid_EnumerateDevices(buffer, HidNative.MaxDevices);
                    if (count < 0)
                        throw new InvalidOperationException(HidNative.LastError);

                    for (int i = 0; i < count; i++) list.Add(buffer[i]);
                    Devices = list;
                    _connected = false;

                    int cmdIf = list.Count(d => d.IsCommandInterface != 0);
                    Log.Add(LogKind.Info, "枚举设备",
                        string.Format("发现 {0} 个 HID 设备，其中 {1} 个是指令接口 (UsagePage=0xFF00, Usage=0x01)",
                            count, cmdIf), null, LogState.None);

                    RaiseOnUi(() => { if (ConnectionChanged != null) ConnectionChanged(false, "已断开（重新枚举）"); });
                    tcs.TrySetResult(list);
                }
                catch (Exception ex)
                {
                    Log.Add(LogKind.Error, "枚举设备", ex.Message, null, LogState.Failed);
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        /// <summary>按枚举索引打开设备。</summary>
        public Task<string> ConnectAsync(int index)
        {
            var tcs = NewTcs<string>();
            Post(() =>
            {
                try
                {
                    int rc = HidNative.Hid_SelectDevice(index);
                    if (rc != 0)
                    {
                        string err = HidNative.LastError;
                        SetConnected(false, "打开失败");
                        Log.Add(LogKind.Error, "打开设备", err, null, LogState.Failed);
                        tcs.TrySetResult(err);
                        return;
                    }

                    var info = (Devices != null && index >= 0 && index < Devices.Count) ? Devices[index] : default(HidNative.HidDeviceInfo);
                    //列表里过滤后只剩指令接口了，正常情况不用再标"（指令接口）"；
                    //万一真选到非指令接口的设备，还是要提醒一句。
                    string desc = string.Format("已打开 [{0}] VID_{1:X4}&PID_{2:X4} {3}{4}",
                        index, info.VendorId, info.ProductId, info.Name,
                        info.IsCommandInterface != 0 ? string.Empty : "  (非指令接口，指令可能无效)");

                    SetConnected(true, desc);
                    Log.Add(LogKind.Info, "打开设备", desc, null, LogState.Done);
                    tcs.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    Log.Add(LogKind.Error, "打开设备", ex.Message, null, LogState.Failed);
                    tcs.TrySetResult(ex.Message);
                }
            });
            return tcs.Task;
        }

        /// <summary>
        /// 快速探测某个设备是不是"真的键盘"：打开它、问一次设备信息，
        /// 能回一帧 0xFF 开头的报文才算数。
        ///
        /// 为什么需要这个：触摸板之类的设备也会暴露一个
        /// UsagePage=0xFF00 / Usage=0x01 的厂商集合，光看描述符和键盘一模一样，
        /// 只能真的问一句才分得出来（之前下拉框里就混进过触摸板）。
        /// 探测完会把设备关掉，不影响后面正常连接。
        /// </summary>
        /// <summary>
        /// 一次把候选设备全探完（**在同一个作业里**）。
        ///
        /// 为什么要合成一个作业（踩过的坑）：
        ///   Hid_SelectDevice 内部第一步就是 Hid_CloseDevice，而原生库只有**一份全局句柄**。
        ///   先前是"每个候选设备各发一个作业"，用户在这中间点了"连接"的话，
        ///   探测作业就会把刚打开的**已连接句柄**关掉 —— 之后每条指令都立刻失败
        ///   （Hid_ReadResponse 直接返回 -1 "No device selected"），
        ///   表现就是"等不到回应、而且等待时间明显不到半秒"。
        ///   合成一个作业之后，连接要么排在它前面、要么排在它后面，队列是 FIFO，不会再交叉。
        ///
        /// 另外：已经连上了就直接跳过探测，绝不去碰句柄。
        /// </summary>
        public Task<List<int>> ProbeAllAsync(int[] indices, int timeoutMs = ProbeTimeoutMs)
        {
            var tcs = NewTcs<List<int>>();
            Post(() =>
            {
                var alive = new List<int>();
                try
                {
                    if (!_connected && indices != null)
                    {
                        foreach (int idx in indices)
                        {
                            if (ProbeOne(idx, timeoutMs)) alive.Add(idx);
                        }
                    }
                }
                catch { }

                tcs.TrySetResult(alive);
            });
            return tcs.Task;
        }

        /// <summary>探测单个设备是不是真键盘（只允许在 HID 工作线程里调用）。</summary>
        private bool ProbeOne(int nativeIndex, int timeoutMs)
        {
            bool ok = false;
            try
            {
                if (HidNative.Hid_SelectDevice(nativeIndex) == 0)
                {
                    var req = CommandRequest.Of("QUERY/DEVICE_INFO(probe)", "探测设备信息",
                        CommandBuilder.Query(QueryTarget.DeviceInfo, QueryMode.All, 0), timeoutMs);

                    var resp = new byte[HidNative.CommandSize];
                    if (HidNative.Hid_SendCommand(req.Payload) == 0)
                    {
                        // 一条指令回两帧：先 ACK，再设备信息（设备信息后面没有 FIN）。
                        // 读到 0xFF 那一帧才算真键盘。
                        for (int i = 0; i < 3; i++)
                        {
                            if (HidNative.Hid_ReadResponse(resp, timeoutMs) != 0) break;
                            if (resp[0] == DeviceInfo.FrameId) { ok = true; break; }
                        }
                    }

                    DrainInput();   //问完清一下，别把晚到的报文留给下一条指令
                    HidNative.Hid_CloseDevice();
                }
            }
            catch { ok = false; }

            if (!ok) Log.Add(LogKind.Info, "探测", "这个设备没有回应设备信息查询（不是键盘）", null, LogState.Timeout);
            return ok;
        }

        public Task<bool> ProbeDeviceInfoAsync(int nativeIndex, int timeoutMs = ProbeTimeoutMs)
        {
            var tcs = NewTcs<bool>();
            Post(() =>
            {
                //已连接时不要动句柄（Hid_SelectDevice 会先关掉当前设备）
                bool ok = !_connected && ProbeOne(nativeIndex, timeoutMs);
                tcs.TrySetResult(ok);
            });
            return tcs.Task;
        }

        /// <summary>
        /// 探活：当前连着的设备还在不在？发一条设备信息查询，超时没回应就当掉线。
        ///
        /// 为什么不用"重新枚举"来判断：Hid_EnumerateDevices 会把已打开的设备先关掉
        /// （原生库的行为），一插拔就会把正常连接也弄断。发一条查询没这个副作用。
        /// </summary>
        public Task<bool> PingAsync(int timeoutMs = PingTimeoutMs)
        {
            var tcs = NewTcs<bool>();
            Post(() =>
            {
                bool alive = false;
                try
                {
                    if (!_connected) { tcs.TrySetResult(false); return; }

                    var req = CommandRequest.Of("QUERY/DEVICE_INFO(ping)", "连接自检",
                        CommandBuilder.Query(QueryTarget.DeviceInfo, QueryMode.All, 0), timeoutMs);
                    var resp = new byte[HidNative.CommandSize];

                    if (HidNative.Hid_SendCommand(req.Payload) == 0)
                    {
                        // 键盘对一条指令是"先 ACK 再设备信息"，而且设备信息后面**没有 FIN**，
                        // 所以不能只读一帧（读到 ACK 就以为不是键盘，真键盘会被筛掉）。
                        // 这里最多读 3 帧，直到看见 0xFF 开头那一帧。
                        for (int i = 0; i < 3; i++)
                        {
                            if (HidNative.Hid_ReadResponse(resp, timeoutMs) != 0) break;
                            if (resp[0] == DeviceInfo.FrameId) { alive = true; break; }
                        }

                        DrainInput();   //同上：探完清管道
                    }
                }
                catch { alive = false; }

                tcs.TrySetResult(alive);
            });
            return tcs.Task;
        }

        // ============================================================
        //  探测 / 探活的等待时间
        //
        //  这两个动作只是"问一句看有没有人应"，但等待时间不能短：
        //  板子上如果开着 DEBUG，Serial.print 在没有串口监视器时每次最多阻塞 250ms，
        //  ACK 可能要等好几百毫秒才出来。之前写 300/400ms，慢一点的板子就被判成
        //  "不是键盘"或者"已掉线"（表现就是收发异常 / 莫名其妙断开）。
        // ============================================================
        private const int ProbeTimeoutMs = 1500;
        private const int PingTimeoutMs = 1500;

        /// <summary>
        /// 把设备里还没读走的报文丢干净。
        ///
        /// 探测/探活用的是"读几帧就停"的策略，万一设备的回应晚到，
        /// 残留的报文会被**下一条指令**当成自己的 ACK/FIN（"回应对不上"的一个来源），
        /// 所以每次问完都清一下。每帧只等很短的时间，不会拖慢流程。
        /// </summary>
        private static void DrainInput(int perFrameTimeoutMs = 15)
        {
            var junk = new byte[HidNative.CommandSize];
            for (int i = 0; i < 4; i++)
            {
                if (HidNative.Hid_ReadResponse(junk, perFrameTimeoutMs) != 0) break;
            }
        }

        /// <summary>尝试按 VID/PID 自动选择指令接口。</summary>
        public Task<string> ConnectCommandInterfaceAsync(ushort vid, ushort pid)
        {
            var tcs = NewTcs<string>();
            Post(() =>
            {
                int rc = HidNative.Hid_SelectCommandInterface(vid, pid);
                if (rc != 0)
                {
                    string err = HidNative.LastError;
                    SetConnected(false, "打开失败");
                    Log.Add(LogKind.Error, "打开设备", err, null, LogState.Failed);
                    tcs.TrySetResult(err);
                    return;
                }
                string desc = string.Format("已按 VID_{0:X4}&PID_{1:X4} 选择指令接口", vid, pid);
                SetConnected(true, desc);
                Log.Add(LogKind.Info, "打开设备", desc, null, LogState.Done);
                tcs.TrySetResult(null);
            });
            return tcs.Task;
        }

        public Task DisconnectAsync()
        {
            var tcs = NewTcs<bool>();
            Post(() =>
            {
                HidNative.Hid_CloseDevice();
                SetConnected(false, "已断开");
                Log.Add(LogKind.Info, "关闭设备", "已关闭指令接口", null, LogState.Done);
                tcs.TrySetResult(true);
            });
            return tcs.Task;
        }

        // ------------------------------------------------------------
        // 发送
        // ------------------------------------------------------------
        private void ExecuteOne(CommandRequest req)
        {
            var entry = Log.Add(LogKind.Tx, req.Name, req.Detail, req.Payload, LogState.Pending);

            int rc = HidNative.Hid_SendCommand(req.Payload);
            if (rc != 0)
            {
                Log.Complete(entry, LogState.Failed, "发送失败: " + HidNative.LastError);
                return;
            }

            if (req.ResponseTimeoutMs <= 0)
            {
                Log.Complete(entry, LogState.Sent, "已发送（未等待回应）");
                return;
            }

            var resp = new byte[HidNative.CommandSize];
            if (HidNative.Hid_ReadResponse(resp, req.ResponseTimeoutMs) == 0)
            {
                Log.Add(LogKind.Rx, "RESPONSE", DescribeResponse(req, resp), resp, LogState.Done);

                // 接收确认：固件按计划会把指令「原样回传」当 ACK，
                // 所以只要回传内容与发出去的一致，就直接判为已完成。
                if (IsEcho(req.Payload, resp))
                    Log.Complete(entry, LogState.Done, "接收确认：键盘原样回传了本条指令");
                else
                    Log.Complete(entry, LogState.Done, "已收到键盘回应");
            }
            else
            {
                Log.Complete(entry, LogState.Timeout,
                    string.Format("等待 {0} ms 无回应", req.ResponseTimeoutMs));
            }
        }

        /// <summary>回传内容是否与发出去的 8 字节完全一致（固件约定的 ACK 方式）。</summary>
        private static bool IsEcho(byte[] request, byte[] response)
        {
            if (request == null || response == null) return false;
            if (request.Length != response.Length) return false;
            for (int i = 0; i < request.Length; i++)
                if (request[i] != response[i]) return false;
            return true;
        }

        /// <summary>
        /// 判断一条 ACK / FIN 是不是我们这条指令的回应。
        ///
        /// · ACK 是 {CMD_ACK, 指令码, 序号, 0...}，所以 [1]=指令码、[2]=序号；
        /// · FIN 是**把整条指令原样回传、只把 [0] 换成 CMD_FIN**，所以 [1] 是
        ///   device / 参数编号 / mode，**不是指令码** ——
        ///   原来拿 resp[1] 去比指令码，会把所有 SET_PARAM、CALIBRATE、QUERY 的回应
        ///   全误判成"对不上"。这里改成逐字节比对 [1..7]。
        ///   另外"校准全部按键"时固件会把 [2] 换成逐个按键号，那种情况只比 [1] 和 [7]。
        /// </summary>
        private static bool MatchesRequest(byte[] req, byte[] resp, ResponseKind kind)
        {
            if (req == null || req.Length < 8 || resp == null || resp.Length < 8) return false;

            if (kind == ResponseKind.Ack)
                return resp[1] == req[0] && resp[2] == req[7];

            if (resp[7] != req[7]) return false;                 //序号必须一致

            if (req[0] == (byte)HidCmd.Calibrate)                //校准：[2] 是按键号，不比
                return resp[1] == req[1];

            for (int i = 1; i < 8; i++)
                if (resp[i] != req[i]) return false;
            return true;
        }

        /// <summary>解析回应内容。固件的回传格式与下发指令一致（见 ProtocolDefs 的 ResponseParser）。</summary>
        private static string DescribeResponse(CommandRequest req, byte[] response)
        {
            if (response == null || response.Length < 8) return "回应长度异常";

            switch (ResponseParser.KindOf(response))
            {
                case ResponseKind.Ack:
                    return string.Format("ACK：键盘已收到 {0}（指令 0x{1:X2}，上位机序号 0x{2:X2}）",
                        req != null ? req.Name : "指令",
                        ResponseParser.ReferenceCmd(response), ResponseParser.EchoSequence(response));

                case ResponseKind.Fin:
                    //FIN 是整条指令原样回传，[1] 不是指令码，所以这里用请求本身的名字
                    return string.Format("FIN：{0} 执行完毕（按键 #{1}，上位机序号 0x{2:X2}）",
                        req != null ? req.Name : "指令", ResponseParser.FinKey(response),
                        ResponseParser.EchoSequence(response));

                case ResponseKind.MapItem:
                    return string.Format("查询结果·键值：{0} [device={1}, offset=0x{2:X2}, slot={3}]",
                        ResponseParser.ToBinding(response).Text, ResponseParser.MapDeviceByte(response),
                        ResponseParser.MapOffset(response), ResponseParser.MapSlot(response));

                case ResponseKind.ParamItem:
                    return string.Format("查询结果·参数：{0} = {1}（{2}）",
                        BindingCatalog.Parameter(ResponseParser.ParamCode(response)).Name,
                        ResponseParser.ParamValue(response),
                        BindingCatalog.FormatParameterValue(ResponseParser.ParamValue(response)));

                case ResponseKind.DeviceInfo:
                    var dev = DeviceInfo.FromFrame(response);
                    return dev == null ? "设备信息（解析失败）" : "设备信息：" + dev.ToString();
            }

            if (req != null && req.Payload != null && response[0] == req.Payload[0])
                return "设备回显了指令本身（可能是数据上报）";

            return "未识别的回应，原始数据见“数据”列";
        }

        // ------------------------------------------------------------
        // 查询 / 校准：一条指令对应多条回传
        // ------------------------------------------------------------
        /// <summary>一条指令的多条回传结果。</summary>
        public sealed class CollectOutcome
        {
            /// <summary>收到的全部回传（含 ACK / FIN）。</summary>
            public readonly List<byte[]> Frames = new List<byte[]>();

            /// <summary>是否收到了 FIN（说明下位机执行完了）。</summary>
            public bool SawFin;

            /// <summary>发送是否成功。</summary>
            public bool Sent;

            /// <summary>收到的 ACK/FIN 是不是这条指令的（指令码 + 序号都对得上）。</summary>
            public bool Matched;

            /// <summary>收到的第一条回应就不是这条指令的 —— 管道里有上一条的残留。</summary>
            public bool StaleFirst;

            /// <summary>对不上的具体说明；都对得上时为 null。</summary>
            public string Mismatch;

            /// <summary>
            /// FIN 里的按键号。校准全部按键时下位机每校准完一个键回一个 FIN，
            /// 所以这里记录最后一个，等于 KeyCount-1 才说明全部校准完了。
            /// </summary>
            public int LastFinKey = -1;

            /// <summary>数据条数（不含 ACK / FIN）。</summary>
            public int ItemCount
            {
                get
                {
                    int n = 0;
                    foreach (var f in Frames)
                    {
                        var k = ResponseParser.KindOf(f);
                        if (k == ResponseKind.MapItem || k == ResponseKind.ParamItem) n++;
                    }
                    return n;
                }
            }
        }

        /// <summary>
        /// 发送一条指令并持续收取回传，直到收到 FIN、或者超过 overallTimeoutMs。
        ///
        /// 用于查询（下位机把结果逐条发回来）和校准（下位机每校准完一个键回一个 FIN）。
        /// perFrameTimeoutMs 是"两帧之间最多等多久"：
        ///   · 查询时下位机是连着发的，短一点没关系；
        ///   · 校准时每帧之间要等用户按键，必须给足（否则会被误判成没有下文了）。
        /// 每收到一条就回调 onFrame（在工作线程上，调用方自己切回 UI 线程）。
        /// </summary>
        public Task<CollectOutcome> SendAndCollectAsync(CommandRequest req, int overallTimeoutMs,
                                                        Action<byte[]> onFrame,
                                                        int perFrameTimeoutMs = 800,
                                                        Func<byte[], bool> isFinalFrame = null)
        {
            var tcs = NewTcs<CollectOutcome>();
            var outcome = new CollectOutcome();

            Post(() =>
            {
                try { ExecuteCollect(req, overallTimeoutMs, onFrame, perFrameTimeoutMs, isFinalFrame, outcome); }
                catch (Exception ex)
                {
                    Log.Add(LogKind.Error, req.Name, ex.Message, req.Payload, LogState.Failed);
                }
                tcs.TrySetResult(outcome);
            });
            return tcs.Task;
        }

        private void ExecuteCollect(CommandRequest req, int overallTimeoutMs,
                                    Action<byte[]> onFrame, int perFrameTimeoutMs,
                                    Func<byte[], bool> isFinalFrame,
                                    CollectOutcome outcome)
        {
            var entry = Log.Add(LogKind.Tx, req.Name, req.Detail, req.Payload, LogState.Pending);

            // 上一条如果超时了，它的 ACK/FIN 可能过一会儿才到；不清掉的话，
            // 本条指令会读到**上一条的**回应，序号对不上（"回应错位"就是这么来的）。
            // 注意只在"上一条出过问题"时才清：正常情况管道是空的，
            // 每次都去读一个超时会白白走一遍原生库的取消分支。
            if (_mayHaveStaleFrames)
            {
                DrainInput();
                _mayHaveStaleFrames = false;
            }

            if (HidNative.Hid_SendCommand(req.Payload) != 0)
            {
                Log.Complete(entry, LogState.Failed, "发送失败: " + HidNative.LastError);
                return;
            }
            outcome.Sent = true;

            if (overallTimeoutMs <= 0)
            {
                Log.Complete(entry, LogState.Sent, "已发送（不收取回传）");
                return;
            }

            int deadline = Environment.TickCount + overallTimeoutMs;
            bool firstFrame = true;
            while (!_disposed)
            {
                int remain = deadline - Environment.TickCount;
                if (remain <= 0) break;

                var resp = new byte[HidNative.CommandSize];
                //第一帧（ACK）应该立刻到；后面的帧按 perFrameTimeoutMs 等
                int wait = outcome.Frames.Count == 0 ? Math.Min(remain, 1000) : Math.Min(remain, perFrameTimeoutMs);
                if (HidNative.Hid_ReadResponse(resp, Math.Max(wait, 1)) != 0)
                    break;      //这一段没等到，认为回传结束

                outcome.Frames.Add(resp);
                var kind = ResponseParser.KindOf(resp);

                //配对校验：ACK / FIN 都是回应我们这条指令的，序号必须对得上。
                //对不上的帧**不立刻判失败**：它很可能是上一条（超时那条）晚到的回应，
                //而本条指令真正的回应还在后面 —— 继续读到 deadline 为止。
                bool paired = true;                  //数据帧（查询结果）不参与配对
                if (kind == ResponseKind.Ack || kind == ResponseKind.Fin)
                {
                    paired = MatchesRequest(req.Payload, resp, kind);
                    if (paired)
                    {
                        outcome.Matched = true;
                        outcome.Mismatch = null;     //后面收到对得上的帧，之前的错位就不算数了
                    }
                    else
                    {
                        if (firstFrame) outcome.StaleFirst = true;
                        if (outcome.Mismatch == null)
                        {
                            outcome.Mismatch = string.Format(
                                "回应对不上（{0}）：收到 序号0x{1:X2}，期望 序号0x{2:X2}",
                                kind == ResponseKind.Ack ? "ACK" : "FIN",
                                ResponseParser.EchoSequence(resp), req.Payload[7]);
                        }
                    }
                }
                firstFrame = false;

                //只有"对得上"的 FIN 才算本条指令的 FIN
                //（残留的 FIN 如果也算，就会提前认为成功/提前收手）
                if (kind == ResponseKind.Fin && paired)
                {
                    outcome.SawFin = true;
                    outcome.LastFinKey = ResponseParser.FinKey(resp);
                }

                Log.Add(LogKind.Rx,
                    kind == ResponseKind.Ack || kind == ResponseKind.Fin ? "RESPONSE" : "QUERY DATA",
                    DescribeResponse(req, resp), resp, LogState.Done);

                if (onFrame != null) onFrame(resp);

                //只有"最后那个 FIN"才结束收取：
                //全部按键校准时下位机会每个键回一个 FIN，收到第一个不能就收手
                bool last = isFinalFrame != null
                    ? (paired && isFinalFrame(resp))
                    : (kind == ResponseKind.Fin && paired);
                if (last) break;
            }

            string done;
            if (outcome.SawFin && outcome.Mismatch == null)
                done = string.Format("已完成，共收到 {0} 条数据", outcome.ItemCount);
            else if (outcome.Mismatch != null)
                done = "⚠ " + outcome.Mismatch + "（常见原因：发太快 / 上一条的回应没读走）";
            else
                done = string.Format("等待 {0} ms 没有收到 FIN，已收到 {1} 条数据", overallTimeoutMs, outcome.ItemCount);

            bool clean = outcome.SawFin && outcome.Mismatch == null;
            if (!clean) _mayHaveStaleFrames = true;      //这条没干净收尾，下一条发之前先清管道
            else _mayHaveStaleFrames = false;

            Log.Complete(entry, clean ? LogState.Done : LogState.Timeout, done);
        }

        /// <summary>排入一条指令（不等待）。</summary>
        public void Send(CommandRequest req)
        {
            Post(() => ExecuteOne(req));
        }

        /// <summary>发送一条指令并等待它执行完毕（发送 + 可选等待回应）。</summary>
        public Task SendAsync(CommandRequest req)
        {
            var tcs = NewTcs<bool>();
            Post(() =>
            {
                try { ExecuteOne(req); }
                catch (Exception ex) { Log.Add(LogKind.Error, req.Name, ex.Message, req.Payload, LogState.Failed); }
                tcs.TrySetResult(true);
            });
            return tcs.Task;
        }

        /// <summary>
        /// 批量发送。用于“清空映射 + 重新写入全部映射”这类需要连续下发很多条指令的场景：
        /// 单条指令的发送间隔由 gapMs 控制，避免超过固件主循环的处理速度。
        /// </summary>
        public async Task RunBatchAsync(IList<CommandRequest> requests, int gapMs,
                                        Action<int, int, string> progress)
        {
            if (requests == null || requests.Count == 0) return;

            for (int i = 0; i < requests.Count; i++)
            {
                await SendAsync(requests[i]).ConfigureAwait(true);
                if (progress != null) progress(i + 1, requests.Count, requests[i].Detail);
                if (gapMs > 0 && i < requests.Count - 1)
                    await Task.Delay(gapMs).ConfigureAwait(true);
            }
        }
    }
}
