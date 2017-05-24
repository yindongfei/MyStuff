namespace Database
{
    public class Cache<TKey, TValue>
    {
        private class CacheEntry
        {
            public TKey Key;
            public TValue Value;
        }

        private System.Collections.Generic.Dictionary<TKey, System.Collections.Generic.LinkedListNode<CacheEntry>> mDict = null;
        private System.Collections.Generic.LinkedList<CacheEntry> mList = null;
        int mSize = 0;

        public Cache(int size = 1024 * 32)
        {
            mSize = size;
            mDict = new System.Collections.Generic.Dictionary<TKey, System.Collections.Generic.LinkedListNode<CacheEntry>>(mSize);
            mList = new System.Collections.Generic.LinkedList<CacheEntry>();
        }

        public TValue Get(TKey key)
        {
            System.Collections.Generic.LinkedListNode<CacheEntry> node;
            if (mDict.TryGetValue(key, out node))
            {
                mList.Remove(node);
                mList.AddFirst(node);
                return node.Value.Value;
            }

            return default(TValue);
        }

        public void Set(TKey key, TValue value)
        {
            System.Collections.Generic.LinkedListNode<CacheEntry> node;
            if (mDict.TryGetValue(key, out node))
            {
                mList.Remove(node);
                mList.AddFirst(node);
                node.Value.Value = value;
                return;
            }
        
            node = mList.AddFirst(new CacheEntry {Key = key, Value = value});
            mDict[key] = node;

            while (mList.Count > mSize)
            {
                mList.RemoveLast();
            }
        }

        public void Delete(TKey key)
        {
            System.Collections.Generic.LinkedListNode<CacheEntry> node;
            if (mDict.TryGetValue(key, out node))
            {
                mList.Remove(node);
                mDict.Remove(key);
            }
        }
    }
}