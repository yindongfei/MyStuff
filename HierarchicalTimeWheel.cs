using System;
using System.Collections.Generic;
using System.Linq;

namespace Network
{
    /// <summary>
    /// Why a timing wheel? The critical feature of timing wheels is O(1) insertion and deletion. 
    /// Timeouts rarely expire in network server software; they're hedges by software for when other expected events fail to occur. 
    /// Timeouts are often installed and cancelled repeatedly for even the simplest of actions. 
    /// But the typical timeout implementation uses a red-black tree or priority queue, where insertion and deletion are O(log N) operations. 
    /// Timing wheels are considerably more efficient algorithmically, 
    /// while this implementation in particular tries to address potential fixed cost and latency issues, particularly for sparsely populated wheels.
    /// </summary>
    public class HierarchicalTimeWheel
    {
        /// <summary>
        /// intenal used data structure to hold time and callback
        /// </summary>
        class TimedItem
        {
            public long Duration;
            public Action Action;
            public bool Enable = true;
        }

        private const int MinTime = 10;
        private const int Step = 256;
        private const int MaxLevel = 5;

        private const long MaxDuration = (long)1 << (MaxLevel << 3);
        private readonly long[] Mask = new[] { 0xFF, 0xFF00, 0xFF0000, 0xFF000000, 0xFF00000000 };
        private readonly List<TimedItem>[,] items = new List<TimedItem>[MaxLevel, Step];
        private int[] currentIndex = new[] { 0, 0, 0, 0, 0 };

        public long elapsedTick = 0;
        private DateTime firstUpdateTime;

        /// <summary>
        /// initalize data structure and alloc memory for later useage.
        /// </summary>
        public HierarchicalTimeWheel()
        {
            for (int i = 0; i < MaxLevel; i++)
            {
                for (int j = 0; j < Step; j++)
                {
                    items[i, j] = new List<TimedItem>(128);
                }
            }

            firstUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// add a timer at given time.
        /// </summary>
        /// <param name="t">time point to call the call back</param>
        /// <param name="act">callback</param>
        /// <returns>the added timer, will be useful if the timer will canceled later.</returns>
        public object Add(DateTime t, Action act)
        {
            var tick = (long)((t - DateTime.Now).TotalMilliseconds) / MinTime + elapsedTick;
            return AddItem(tick, act);
        }

        /// <summary>
        /// add a timer will fire t millisecond later.
        /// </summary>
        /// <param name="t">time period in millisecond.</param>
        /// <param name="act">callback</param>
        /// <returns></returns>
        public object Add(long t, Action act)
        {
            var tick = (long)t / MinTime + elapsedTick;
            return AddItem(tick, act);
        }


        private object AddItem(long t, Action act)
        {
            if (t >= MaxDuration)
                throw new ArgumentOutOfRangeException("t");

            var level = MaxLevel - 1;

            if (t <= elapsedTick)
            {
                var item = new TimedItem { Duration = t, Action = act };
                items[0, currentIndex[0]].Add(item);
                return item;
            }

            var slot = t & Mask[level];
            while (slot <= currentIndex[level])
            {
                level--;
                slot = (t & Mask[level]) >> (level << 3);
            }

            {
                var item = new TimedItem
                {
                    Duration = t,
                    Action = act
                };
                items[level, slot].Add(item);
                return item;
            }
        }

        /// <summary>
        /// Update the Time wheel according to datetime, no matter when this is called, time wheel will advance to curruent tick.
        /// </summary>
        public void Update()
        {
            var now = DateTime.Now;
            var duration = (int)((now - firstUpdateTime).TotalMilliseconds) / MinTime - elapsedTick;
            //var duration = 1;
            for (int i = 0; i < duration; i++)
            {
                Advance();
            }
        }

        /// <summary>
        /// advance the timer at MinTime in millisecond, this method should be called every MinTime millisecond. 
        /// </summary>
        public void Advance()
        {
            var list = items[0, currentIndex[0]];
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                try
                {
                    if (item.Enable)
                        item.Action();
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    item.Enable = false;
                }
            }

            items[0, currentIndex[0]].Clear();
            currentIndex[0]++;
            elapsedTick++;

            if (currentIndex[0] >= Step)
            {
                Reshape(0);
            }
        }

        private void Reshape(int level)
        {
            if (currentIndex[level] >= Step)
            {
                currentIndex[level + 1]++;
                currentIndex[level] = 0;
                Reshape(level + 1);
            }

            if (level > 0)
                PutItemsDown(level);
        }
        public void UpdateWithoutExecute(List<Action> acts)
        {
            acts.AddRange(items[0, currentIndex[0]].Where(i => i.Enable).Select(i => i.Action));
            items[0, currentIndex[0]].Clear();
            currentIndex[0]++;
            elapsedTick++;

            if (currentIndex[0] >= Step)
            {
                Reshape(0);
            }
        }

        /// <summary>
        /// cancel a timer.
        /// </summary>
        /// <param name="t">timer which retured by Add function.</param>
        public void Cancel(object t)
        {
            var item = (t as TimedItem);
            if (item != null)
                item.Enable = false;
        }

        private void PutItemsDown(int fromLevel)
        {
            int fromIndex = currentIndex[fromLevel];
            int toLevel = fromLevel - 1;
            long mask = Mask[toLevel];
            int shift = (toLevel << 3);
            foreach (var timedItem in items[fromLevel, fromIndex])
            {
                var slot = (timedItem.Duration & mask) >> shift;
                items[toLevel, slot].Add(timedItem);
            }
            items[fromLevel, fromIndex].Clear();
        }
    }

}
