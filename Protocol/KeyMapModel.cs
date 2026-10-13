using System;
using System.Collections.Generic;
using System.Linq;

namespace KeyboardOG
{
    /// <summary>
    /// 上位机侧的按键映射模型。
    ///
    /// 为什么需要本地维护一份：
    ///   固件只提供三种写操作 —— APPEND（追加到 EEPROM 末尾）、
    ///   OVERRIDE（写到指定 storageIndex）、CLEAR（清空整张表），
    ///   没有“读回当前映射”和“清除单个键”的能力。
    ///   所以界面上的映射列表只能由上位机自己记住，
    ///   需要时用 CLEAR + 全部 APPEND 的方式整表重写。
    ///
    /// 每个按键最多 DeviceLimits.MaxBindingsPerKey(12) 条。
    /// </summary>
    public sealed class KeyMapModel
    {
        private readonly List<Binding>[] _slots;

        public KeyMapModel()
        {
            _slots = new List<Binding>[DeviceLimits.KeyCount];
            for (int i = 0; i < _slots.Length; i++) _slots[i] = new List<Binding>();
        }

        public List<Binding> this[int key]
        {
            get { return _slots[key]; }
        }

        public int Count(int key) { return _slots[key].Count; }

        public bool IsFull(int key) { return _slots[key].Count >= DeviceLimits.MaxBindingsPerKey; }

        public int TotalCount { get { return _slots.Sum(s => s.Count); } }

        /// <summary>追加一条映射；已满或重复时返回 false。</summary>
        public bool Add(int key, Binding binding)
        {
            var list = _slots[key];
            if (list.Count >= DeviceLimits.MaxBindingsPerKey) return false;
            if (list.Any(b => b.Device == binding.Device && b.Offset == binding.Offset)) return false;

            list.Add(binding.Clone());
            return true;
        }

        public void RemoveAt(int key, int index)
        {
            var list = _slots[key];
            if (index < 0 || index >= list.Count) return;
            list.RemoveAt(index);
        }

        /// <summary>
        /// 按 (device, offset) 删除一条映射 —— 给选键控件的“再点一下取消选中”用。
        /// 返回是否真的删掉了。
        /// </summary>
        public bool Remove(int key, MapDevice device, byte offset)
        {
            var list = _slots[key];
            int index = list.FindIndex(b => b.Device == device && b.Offset == offset);
            if (index < 0) return false;

            list.RemoveAt(index);
            return true;
        }

        /// <summary>找某个 (device, offset) 在列表里的位置，没有返回 -1。</summary>
        public int IndexOf(int key, MapDevice device, byte offset)
        {
            return _slots[key].FindIndex(b => b.Device == device && b.Offset == offset);
        }

        /// <summary>
        /// 替换第 index 条映射（给"左键点选键控件 = 替换选中项"用）。
        /// 越界时返回 false。
        /// </summary>
        public bool ReplaceAt(int key, int index, Binding binding)
        {
            var list = _slots[key];
            if (index < 0 || index >= list.Count) return false;

            list[index] = binding.Clone();
            return true;
        }

        /// <summary>全部清空（连接后从键盘同步之前调用）。</summary>
        public void ClearAll()
        {
            foreach (var list in _slots) list.Clear();
        }

        public void Move(int key, int index, int delta)
        {
            var list = _slots[key];
            int target = index + delta;
            if (index < 0 || index >= list.Count) return;
            if (target < 0 || target >= list.Count) return;

            var b = list[index];
            list.RemoveAt(index);
            list.Insert(target, b);
        }

        public void ClearKey(int key) { _slots[key].Clear(); }

        public void Clear() { foreach (var s in _slots) s.Clear(); }

        public IEnumerable<KeyValuePair<int, List<Binding>>> All
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                    yield return new KeyValuePair<int, List<Binding>>(i, _slots[i]);
            }
        }

        /// <summary>
        /// 某个按键上已经选中的偏移量 —— 换算成「选键控件用的」偏移量，用于给控件做高亮。
        /// 键盘修饰键下发的是 104~111，控件上要显示成 224~231。
        /// </summary>
        public List<byte> PickerOffsetsOf(int key, MapDevice device)
        {
            return _slots[key]
                .Where(b => b.Device == device)
                .Select(b => BindingCatalog.ToPickerOffset(device, b.Offset))
                .ToList();
        }
    }
}
