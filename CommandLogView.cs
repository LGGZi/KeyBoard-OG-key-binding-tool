using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace KeyboardOG
{
    /// <summary>
    /// 指令日志视图：显示“正在执行 / 已完成”的指令、收到的回应和错误。
    /// 主窗体的底部面板和独立的日志窗口都复用这个控件。
    /// </summary>
    public class CommandLogView : UserControl
    {
        private readonly ListView _list;
        private readonly Dictionary<int, ListViewItem> _items = new Dictionary<int, ListViewItem>();
        private CommandLogStore _store;

        /// <summary>是否自动滚动到最新一行。</summary>
        public bool AutoScrollToEnd = true;

        /// <summary>暂停刷新（日志仍然记录，只是不再更新视图）。</summary>
        public bool Paused;

        public CommandLogView()
        {
            var columns = new[]
            {
                new ColumnHeader { Text = "#",    Width = 44  },
                new ColumnHeader { Text = "时间",  Width = 84  },
                new ColumnHeader { Text = "类型",  Width = 52  },
                new ColumnHeader { Text = "状态",  Width = 60  },
                new ColumnHeader { Text = "指令",  Width = 130 },
                new ColumnHeader { Text = "详情",  Width = 560 },
                new ColumnHeader { Text = "数据 (8 字节)", Width = 190 },
            };

            _list = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HideSelection = false,
                MultiSelect = false,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                Font = new Font("Consolas", 8.5f),
            };
            _list.Columns.AddRange(columns);

            Controls.Add(_list);
            BackColor = Color.White;
        }

        public ListView List { get { return _list; } }

        public void Attach(CommandLogStore store)
        {
            Detach();
            _store = store;
            if (_store == null) return;

            _store.Added += OnAdded;
            _store.Updated += OnUpdated;
            _store.Removed += OnRemoved;
            _store.Cleared += OnCleared;

            foreach (var entry in _store.Snapshot()) AddItem(entry);
            ScrollToEnd();
        }

        public void Detach()
        {
            if (_store == null) return;
            _store.Added -= OnAdded;
            _store.Updated -= OnUpdated;
            _store.Removed -= OnRemoved;
            _store.Cleared -= OnCleared;
            _store = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Detach();
            base.Dispose(disposing);
        }

        public void ClearView()
        {
            _list.Items.Clear();
            _items.Clear();
        }

        private void OnAdded(LogEntry e)
        {
            if (Paused) return;
            AddItem(e);
            ScrollToEnd();
        }

        private void OnUpdated(LogEntry e)
        {
            if (Paused) return;
            ListViewItem item;
            if (_items.TryGetValue(e.Id, out item)) Fill(item, e);
        }

        private void OnRemoved(LogEntry e)
        {
            ListViewItem item;
            if (_items.TryGetValue(e.Id, out item))
            {
                _list.Items.Remove(item);
                _items.Remove(e.Id);
            }
        }

        private void OnCleared()
        {
            ClearView();
        }

        private void ScrollToEnd()
        {
            if (!AutoScrollToEnd || _list.Items.Count == 0) return;
            _list.EnsureVisible(_list.Items.Count - 1);
        }

        /// <summary>语言切换后把整张表按新语言重画一遍。</summary>
        public void Reload()
        {
            if (_store == null) return;
            _items.Clear();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var entry in _store.Snapshot()) AddItem(entry);
            _list.EndUpdate();
        }

        private void AddItem(LogEntry e)
        {
            var item = new ListViewItem(new string[7]);
            item.Tag = e;
            _list.Items.Add(item);
            _items[e.Id] = item;
            Fill(item, e);
        }

        private static void Fill(ListViewItem item, LogEntry e)
        {
            // 固定 7 列，按下标刷新（不要用 SubItems.Clear()，item 在 ListView 里时它会破坏列结构）
            while (item.SubItems.Count < 7) item.SubItems.Add(string.Empty);

            string detail = L.T(e.Detail);
            if (!string.IsNullOrEmpty(e.Result) && e.State != LogState.Pending)
                detail = detail + "  →  " + L.T(e.Result);

            item.SubItems[0].Text = e.Id.ToString();
            item.SubItems[1].Text = e.TimeText;
            item.SubItems[2].Text = L.T(LogText.Kind(e.Kind));
            item.SubItems[3].Text = L.T(LogText.State(e.State));
            item.SubItems[4].Text = L.T(e.Command);
            item.SubItems[5].Text = detail;
            item.SubItems[6].Text = e.PayloadHex;

            item.ForeColor = ColorFor(e.Kind, e.State);
            item.BackColor = Color.White;
        }

        private static Color ColorFor(LogKind kind, LogState state)
        {
            if (state == LogState.Failed) return Color.FromArgb(190, 30, 30);
            if (state == LogState.Timeout) return Color.FromArgb(180, 120, 20);

            switch (kind)
            {
                case LogKind.Tx:    return Color.FromArgb(20, 60, 160);
                case LogKind.Rx:    return Color.FromArgb(20, 120, 60);
                case LogKind.Error: return Color.FromArgb(190, 30, 30);
                case LogKind.Warn:  return Color.FromArgb(180, 120, 20);
                default:            return Color.FromArgb(90, 90, 90);
            }
        }

        /// <summary>把当前日志导出成 CSV（UTF-8 带 BOM，Excel 可直接打开）。</summary>
        public void ExportCsv(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#,时间,类型,状态,指令,详情,数据");

            foreach (ListViewItem item in _list.Items)
            {
                var e = item.Tag as LogEntry;
                if (e == null) continue;
                sb.Append(e.Id).Append(',')
                  .Append(Csv(e.TimeText)).Append(',')
                  .Append(Csv(L.T(LogText.Kind(e.Kind)))).Append(',')
                  .Append(Csv(L.T(LogText.State(e.State)))).Append(',')
                  .Append(Csv(L.T(e.Command))).Append(',')
                  .Append(Csv(L.T(e.Detail) + (string.IsNullOrEmpty(e.Result) ? "" : " -> " + L.T(e.Result)))).Append(',')
                  .Append(Csv(e.PayloadHex)).AppendLine();
            }

            WriteAllText(path, sb.ToString());
        }

        /// <summary>
        /// 写文件。
        ///
        /// 导出最常见的失败就是"没有权限"，两种典型情况：
        ///   · 目标文件是**只读**的（比如上次用 Excel 打开过、或从别处复制来的） -> 去掉只读属性再写一次；
        ///   · 目标目录本身不可写 -> 直接抛出，让调用方换目录或提示用户。
        /// </summary>
        public static void WriteAllText(string path, string text)
        {
            try
            {
                File.WriteAllText(path, text, new UTF8Encoding(true));
                return;
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var attrs = File.GetAttributes(path);
                        if ((attrs & FileAttributes.ReadOnly) != 0)
                        {
                            File.SetAttributes(path, attrs & ~FileAttributes.ReadOnly);
                            File.WriteAllText(path, text, new UTF8Encoding(true));
                            return;
                        }
                    }
                }
                catch
                {
                    //还是不行就把原始异常抛出去
                }
                throw;
            }
        }

        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }
    }
}
