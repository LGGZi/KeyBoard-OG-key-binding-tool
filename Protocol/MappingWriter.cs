using System.Collections.Generic;

namespace KeyboardOG
{
    /// <summary>一条"最少指令"计划项；真正发送时再用 CommandBuilder.SetMap 变成 8 字节。</summary>
    public sealed class MappingPlanItem
    {
        public MapOperation Operation;
        public byte Key;
        public byte Device;     // APPEND / OVERRIDE 用
        public byte Offset;     // APPEND / OVERRIDE 用
        public byte Index;      // OVERRIDE = 第几个槽；CLEAR = 从第几槽开始清（0 = 整个键清空）
        public string Detail;
    }

    /// <summary>
    /// 把"本地列表"变成"键盘上的内容"，并且**指令越少越好**。
    ///
    /// 下位机（HIDcmdHandler.h / EEPROMManager.h）现在有三种操作：
    ///   MAP_DO_APPEND   ：往该按键的第一个空槽追加一条（Byte6 不用给）
    ///   MAP_DO_OVERRIDE ：写该按键的第 slot 个槽（**不再顺手清后面的槽**）
    ///   MAP_DO_CLEAR    ：Byte6 = 从第几槽开始清到该按键末尾；0 = 整个键清空；
    ///                     Byte6 = BUTTONCOUNT 时清空全部按键
    ///
    /// 所以上位机不用再"每个槽都发一遍"，只发真正有变化的那几条就够了。
    /// </summary>
    public static class MappingWriter
    {
        private static bool Same(Binding a, Binding b)
        {
            return a.Device == b.Device && a.Offset == b.Offset && a.Behavior == b.Behavior;
        }

        private static string Describe(Binding b)
        {
            return b == null ? "?" : b.Text;
        }

        private static MappingPlanItem Item(MapOperation op, int key, Binding b, byte index, string detail)
        {
            return new MappingPlanItem
            {
                Operation = op,
                Key = (byte)key,
                Device = b == null ? (byte)0 : b.WireDevice,
                Offset = b == null ? (byte)0 : b.Offset,
                Index = index,
                Detail = detail,
            };
        }

        private static MappingPlanItem Clear(int key, byte fromSlot, string detail)
        {
            return new MappingPlanItem
            {
                Operation = MapOperation.Clear,
                Key = (byte)key,
                Device = 0,
                Offset = 0,
                Index = fromSlot,
                Detail = detail,
            };
        }

        /// <summary>desired 是不是"current 后面纯粹追加了若干条"。</summary>
        private static bool IsAppendOnly(IList<Binding> current, IList<Binding> desired)
        {
            if (desired.Count <= current.Count) return false;
            for (int i = 0; i < current.Count; i++)
                if (!Same(current[i], desired[i])) return false;
            return true;
        }

        /// <summary>
        /// 算出"把键盘上的 current 改成 desired"最少要发哪几条指令。
        /// current 传 null 表示"键盘上是什么还不知道"，这时只能整片重写（安全优先）。
        /// </summary>
        public static List<MappingPlanItem> BuildPlan(IList<Binding> current, IList<Binding> desired, int key)
        {
            var plan = new List<MappingPlanItem>();
            int desCount = desired == null ? 0 : desired.Count;

            // 未知（没同步过）：OVERRIDE 每个槽 + 清一次尾巴，保证结果一定对
            if (current == null)
            {
                for (int i = 0; i < desCount; i++)
                    plan.Add(Item(MapOperation.Override, key, desired[i], (byte)i,
                        string.Format("按键#{0} 槽{1} ← {2}", key, i, Describe(desired[i]))));

                if (desCount < DeviceLimits.MaxBindingsPerKey)
                    plan.Add(Clear(key, (byte)desCount,
                        string.Format("按键#{0} 清掉槽{1}以后的旧映射", key, desCount)));
                return plan;
            }

            int curCount = current.Count;

            // 1) 一模一样：一条都不用发
            if (curCount == desCount)
            {
                bool same = true;
                for (int i = 0; i < curCount; i++)
                    if (!Same(current[i], desired[i])) { same = false; break; }
                if (same) return plan;
            }

            // 2) 只是在后面追加：新增的每条一个 APPEND
            if (IsAppendOnly(current, desired))
            {
                for (int i = curCount; i < desCount; i++)
                    plan.Add(Item(MapOperation.Append, key, desired[i], 0,
                        string.Format("按键#{0} 追加 {1}", key, Describe(desired[i]))));
                return plan;
            }

            // 3) 本地清空了：一条 CLEAR（Index = 0 → 整个键清空）
            if (desCount == 0)
            {
                plan.Add(Clear(key, 0, string.Format("按键#{0} 清空全部映射", key)));
                return plan;
            }

            // 4) 其它：只改有变化的槽；本地比键盘短时补一条 CLEAR 清尾巴
            for (int i = 0; i < desCount; i++)
            {
                if (i < curCount && Same(current[i], desired[i])) continue;
                plan.Add(Item(MapOperation.Override, key, desired[i], (byte)i,
                    string.Format("按键#{0} 槽{1} ← {2}", key, i, Describe(desired[i]))));
            }

            if (desCount < curCount)
                plan.Add(Clear(key, (byte)desCount,
                    string.Format("按键#{0} 清掉槽{1}以后的旧映射", key, desCount)));

            return plan;
        }
    }
}
