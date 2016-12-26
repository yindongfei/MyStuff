

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

        private static readonly long[] Duration = new[]
        {
            (long) MinTime, 
            (long) MinTime*Step, 
            (long) MinTime*Step*Step, 
            (long) MinTime*Step*Step*Step, 
            (long) MinTime*Step*Step*Step*Step,
            (long) MinTime*Step*Step*Step*Step*Step
        };

        private readonly List<TimedItem>[,] items = new List<TimedItem>[MaxLevel, Step];
        private int[] currentIndex = new[] { 0, 0, 0, 0, 0 };

        private DateTime lastUpdateTime;

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

            lastUpdateTime = DateTime.Now;
        }

        /// <summary>
        /// add a timer at given time.
        /// </summary>
        /// <param name="t">time point to call the call back</param>
        /// <param name="act">callback</param>
        /// <returns>the added timer, will be useful if the timer will canceled later.</returns>
        public object Add(DateTime t, Action act)
        {
            var ms = (long)(t - DateTime.Now).TotalMilliseconds;
            return Add(ms, act);
        }

        /// <summary>
        /// add a timer will fire t millisecond later.
        /// </summary>
        /// <param name="t">time period in millisecond.</param>
        /// <param name="act">callback</param>
        /// <returns></returns>
        public object Add(long t, Action act)
        {
            if(t >= Duration[MaxLevel])
                throw new ArgumentOutOfRangeException("t");

            var level = 0;

            if (t < Duration[0])
            {
                var item = new TimedItem {Duration = t, Action = act};
                items[0, currentIndex[0]].Add(item);
                return item;
            }

            for (int i = 0; i < MaxLevel; i++)
            {
                if (t >= Duration[i] && t < Duration[i + 1])
                {
                    level = i;
                    break;
                }
            }

            {
                var item = new TimedItem
                {
                    Duration = t,
                    Action = act
                };
                items[level, currentIndex[level] + (int) (t/Duration[level])%Step].Add(item);
                return item;
            }
        }

        /// <summary>
        /// Update the Time wheel according to datetime, no matter when this is called, time wheel will advance to curruent tick.
        /// </summary>
        public void Update()
        {
            var now = DateTime.Now;
            var duration = (now - lastUpdateTime).TotalMilliseconds/MinTime;
            for (int i = 0; i < duration; i++)
            {
                Advance();
            }

            lastUpdateTime = lastUpdateTime.AddMilliseconds(duration*10);
        }

        /// <summary>
        /// advance the timer at MinTime in millisecond, this method should be called every MinTime millisecond. 
        /// </summary>
        public void Advance()
        {
            foreach (var item in items[0, currentIndex[0]])
            {
                //try
                //{
                if (item.Enable)
                    item.Action();
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine(ex.ToString());
                //}
            }

            items[0, currentIndex[0]].Clear();
            currentIndex[0]++;

            for (int i = 1; i < MaxLevel; i++)
            {
                if (currentIndex[i - 1] >= Step)
                {
                    currentIndex[i - 1] = i == 1 ? 0 : 1;
                    currentIndex[i] ++;
                    currentIndex[i] %= Step;
                    PutItemsDown(i);
                }
                else
                {
                    break;
                }
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
            foreach (var timedItem in items[fromLevel, fromIndex])
            {
                items[toLevel, (timedItem.Duration/Duration[toLevel])%Step].Add(timedItem);
            }
            items[fromLevel, fromIndex].Clear();
        }
    }