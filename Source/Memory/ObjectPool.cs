using System;
using System.Collections.Generic;

namespace PriorityManager.Memory
{
    /// <summary>
    /// Object pool for reducing GC pressure - reuse objects instead of allocating new ones
    /// v2.0: Target zero allocations in hot paths
    /// </summary>
    public static class ObjectPool
    {
        // Pool statistics
        private static int totalGets = 0;
        private static int totalReturns = 0;
        private static int totalCreations = 0;
        
        /// <summary>
        /// Get pooling statistics
        /// </summary>
        public static string GetStatistics()
        {
            int inUse = totalGets - totalReturns;
            return $"Gets: {totalGets}, Returns: {totalReturns}, In-use: {inUse}, Created: {totalCreations}";
        }
        
        /// <summary>
        /// Reset statistics
        /// </summary>
        public static void ResetStatistics()
        {
            totalGets = 0;
            totalReturns = 0;
            totalCreations = 0;
        }
    }
    
    /// <summary>
    /// Generic list pool
    /// </summary>
    public static class ListPool<T>
    {
        private static Stack<List<T>> pool = new Stack<List<T>>();
        private const int MAX_POOL_SIZE = 50;
        
        /// <summary>
        /// Get a list from the pool
        /// </summary>
        public static List<T> Get()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    var list = pool.Pop();
                    list.Clear(); // Ensure it's empty
                    return list;
                }
            }
            
            return new List<T>();
        }
        
        /// <summary>
        /// Return a list to the pool
        /// </summary>
        public static void Return(List<T> list)
        {
            if (list == null)
                return;
            
            lock (pool)
            {
                if (pool.Count < MAX_POOL_SIZE)
                {
                    list.Clear();
                    pool.Push(list);
                }
            }
        }
        
        /// <summary>
        /// Get pool size
        /// </summary>
        public static int GetPoolSize()
        {
            lock (pool)
            {
                return pool.Count;
            }
        }
        
        /// <summary>
        /// Clear the pool
        /// </summary>
        public static void Clear()
        {
            lock (pool)
            {
                pool.Clear();
            }
        }
    }
    
    /// <summary>
    /// Generic dictionary pool
    /// </summary>
    public static class DictionaryPool<TKey, TValue>
    {
        private static Stack<Dictionary<TKey, TValue>> pool = new Stack<Dictionary<TKey, TValue>>();
        private const int MAX_POOL_SIZE = 20;
        
        /// <summary>
        /// Get a dictionary from the pool
        /// </summary>
        public static Dictionary<TKey, TValue> Get()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    var dict = pool.Pop();
                    dict.Clear();
                    return dict;
                }
            }
            
            return new Dictionary<TKey, TValue>();
        }
        
        /// <summary>
        /// Return a dictionary to the pool
        /// </summary>
        public static void Return(Dictionary<TKey, TValue> dict)
        {
            if (dict == null)
                return;
            
            lock (pool)
            {
                if (pool.Count < MAX_POOL_SIZE)
                {
                    dict.Clear();
                    pool.Push(dict);
                }
            }
        }
        
        /// <summary>
        /// Get pool size
        /// </summary>
        public static int GetPoolSize()
        {
            lock (pool)
            {
                return pool.Count;
            }
        }
        
        /// <summary>
        /// Clear the pool
        /// </summary>
        public static void Clear()
        {
            lock (pool)
            {
                pool.Clear();
            }
        }
    }
    
    /// <summary>
    /// Generic HashSet pool
    /// </summary>
    public static class HashSetPool<T>
    {
        private static Stack<HashSet<T>> pool = new Stack<HashSet<T>>();
        private const int MAX_POOL_SIZE = 20;
        
        /// <summary>
        /// Get a HashSet from the pool
        /// </summary>
        public static HashSet<T> Get()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    var set = pool.Pop();
                    set.Clear();
                    return set;
                }
            }
            
            return new HashSet<T>();
        }
        
        /// <summary>
        /// Return a HashSet to the pool
        /// </summary>
        public static void Return(HashSet<T> set)
        {
            if (set == null)
                return;
            
            lock (pool)
            {
                if (pool.Count < MAX_POOL_SIZE)
                {
                    set.Clear();
                    pool.Push(set);
                }
            }
        }
        
        /// <summary>
        /// Get pool size
        /// </summary>
        public static int GetPoolSize()
        {
            lock (pool)
            {
                return pool.Count;
            }
        }
        
        /// <summary>
        /// Clear the pool
        /// </summary>
        public static void Clear()
        {
            lock (pool)
            {
                pool.Clear();
            }
        }
    }
    
    /// <summary>
    /// Array pool for temporary arrays
    /// </summary>
    public static class ArrayPool<T>
    {
        private static Dictionary<int, Stack<T[]>> pools = new Dictionary<int, Stack<T[]>>();
        private const int MAX_POOL_SIZE_PER_LENGTH = 10;
        
        /// <summary>
        /// Rent an array of at least the specified length
        /// </summary>
        public static T[] Rent(int minimumLength)
        {
            // Round up to nearest power of 2
            int length = RoundUpToPowerOf2(minimumLength);
            
            lock (pools)
            {
                if (pools.TryGetValue(length, out Stack<T[]> pool) && pool.Count > 0)
                {
                    return pool.Pop();
                }
            }
            
            return new T[length];
        }
        
        /// <summary>
        /// Return an array to the pool
        /// </summary>
        public static void Return(T[] array, bool clearArray = false)
        {
            if (array == null)
                return;
            
            if (clearArray)
            {
                Array.Clear(array, 0, array.Length);
            }
            
            int length = array.Length;
            
            lock (pools)
            {
                if (!pools.TryGetValue(length, out Stack<T[]> pool))
                {
                    pool = new Stack<T[]>();
                    pools[length] = pool;
                }
                
                if (pool.Count < MAX_POOL_SIZE_PER_LENGTH)
                {
                    pool.Push(array);
                }
            }
        }
        
        private static int RoundUpToPowerOf2(int value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;
            return value;
        }
        
        /// <summary>
        /// Get total pooled arrays count
        /// </summary>
        public static int GetPoolSize()
        {
            lock (pools)
            {
                int total = 0;
                foreach (var pool in pools.Values)
                {
                    total += pool.Count;
                }
                return total;
            }
        }
        
        /// <summary>
        /// Clear all pools
        /// </summary>
        public static void Clear()
        {
            lock (pools)
            {
                pools.Clear();
            }
        }
    }
    
    /// <summary>
    /// StringBuilder pool for string operations
    /// </summary>
    public static class StringBuilderPool
    {
        private static Stack<System.Text.StringBuilder> pool = new Stack<System.Text.StringBuilder>();
        private const int MAX_POOL_SIZE = 10;
        private const int MAX_CAPACITY = 4096; // Don't pool huge StringBuilders
        
        /// <summary>
        /// Get a StringBuilder from the pool
        /// </summary>
        public static System.Text.StringBuilder Get()
        {
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    var sb = pool.Pop();
                    sb.Clear();
                    return sb;
                }
            }
            
            return new System.Text.StringBuilder();
        }
        
        /// <summary>
        /// Return a StringBuilder to the pool
        /// </summary>
        public static void Return(System.Text.StringBuilder sb)
        {
            if (sb == null)
                return;
            
            // Don't pool if it's too large
            if (sb.Capacity > MAX_CAPACITY)
                return;
            
            lock (pool)
            {
                if (pool.Count < MAX_POOL_SIZE)
                {
                    sb.Clear();
                    pool.Push(sb);
                }
            }
        }
        
        /// <summary>
        /// Get pool size
        /// </summary>
        public static int GetPoolSize()
        {
            lock (pool)
            {
                return pool.Count;
            }
        }
        
        /// <summary>
        /// Clear the pool
        /// </summary>
        public static void Clear()
        {
            lock (pool)
            {
                pool.Clear();
            }
        }
    }
}

