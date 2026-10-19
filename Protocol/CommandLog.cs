using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace KeyboardOG
{
    public enum LogKind
    {
        Info,
        Tx,      // 上位机 -> 键盘
        Rx,      // 键盘 -> 上位机
        Warn,
        Error,
    }

    public enum LogState
    {
        None,
        Pending,   // 已进入发送队列
        Sent,      // 已发出，未等待回应
        Done,      // 已完成（收到回应 / 发送成功）
        Timeout,   // 无回应
        Failed,    // 发送失败
    }

    public static class LogText
    {
        public static string Kind(LogKind k)
        {
            switch (k)
            {
                case LogKind.Info:  return "信息";
                case LogKind.Tx:    return "发送";
                case LogKind.Rx:    return "接收";
                case LogKind.Warn:  return "警告";
                case LogKind.Error: return "错误";
                default: return k.ToString();
            }
        }

        public static string State(LogState s)
        {
            switch (s)
            {
                case LogState.None:    return "-";
                case LogState.Pending: return "执行中";
                case LogState.Sent:    return "已发送";
                case LogState.Done:    return "已完成";
                case LogState.Timeout: return "超时";
                case LogState.Failed:  return "失败";
                default: return s.ToString();
            }
        }
    }

    /// <summary>一条指令 / 回应 / 提示日志。</summary>
    public sealed class LogEntry
    {
        public int Id;
        public DateTime Time;
        public LogKind Kind;
        public string Command;
        public string Detail;
        public byte[] Payload;
        public LogState State = LogState.None;
        public string Result = string.Empty;

        public string PayloadHex
        {
            get { return Payload == null ? string.Empty : CommandBuilder.Hex(Payload); }
        }

        public string TimeText
        {
            get { return Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture); }
        }
    }

    /// <summary>
    /// 指令日志仓库。所有事件都在创建时传入的 SynchronizationContext（UI 线程）上触发，
    /// 因此订阅者可以直接操作控件。
    /// </summary>
    public sealed class CommandLogStore
    {
        private readonly List<LogEntry> _entries = new List<LogEntry>();
        private readonly SynchronizationContext _ui;
        private readonly object _gate = new object();
        private int _nextId = 1;

        /// <summary>超过该数量后从最旧的开始丢弃。</summary>
        public int MaxEntries = 3000;

        public event Action<LogEntry> Added;
        public event Action<LogEntry> Updated;
        public event Action<LogEntry> Removed;
        public event Action Cleared;

        public CommandLogStore(SynchronizationContext ui)
        {
            _ui = ui ?? new SynchronizationContext();
        }

        private void OnUi(Action a)
        {
            if (_ui == null) { a(); return; }
            _ui.Post(_ => a(), null);
        }

        public LogEntry Add(LogKind kind, string command, string detail, byte[] payload, LogState state)
        {
            var e = new LogEntry
            {
                Time = DateTime.Now,
                Kind = kind,
                Command = command ?? string.Empty,
                Detail = detail ?? string.Empty,
                Payload = payload,
                State = state,
            };

            LogEntry dropped = null;
            lock (_gate)
            {
                e.Id = _nextId++;
                _entries.Add(e);
                if (_entries.Count > MaxEntries)
                {
                    dropped = _entries[0];
                    _entries.RemoveAt(0);
                }
            }

            OnUi(() =>
            {
                if (dropped != null && Removed != null) Removed(dropped);
                if (Added != null) Added(e);
            });
            return e;
        }

        /// <summary>更新某条指令的状态（执行中 → 已完成 / 超时 / 失败）。</summary>
        public void Complete(LogEntry entry, LogState state, string result)
        {
            if (entry == null) return;
            entry.State = state;
            entry.Result = result ?? string.Empty;
            OnUi(() => { if (Updated != null) Updated(entry); });
        }

        /// <summary>仅更新显示文本（不改变状态）。</summary>
        public void Touch(LogEntry entry, string result)
        {
            if (entry == null) return;
            if (result != null) entry.Result = result;
            OnUi(() => { if (Updated != null) Updated(entry); });
        }

        public List<LogEntry> Snapshot()
        {
            lock (_gate) { return _entries.ToList(); }
        }

        public void Clear()
        {
            lock (_gate) { _entries.Clear(); }
            OnUi(() => { if (Cleared != null) Cleared(); });
        }
    }
}
