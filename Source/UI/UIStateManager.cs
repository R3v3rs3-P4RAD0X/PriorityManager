using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PriorityManager.UI
{
    /// <summary>
    /// Manages UI state and dirty region tracking
    /// v2.0: Only redraws changed sections for better performance
    /// </summary>
    public class UIStateManager
    {
        private HashSet<UIRegion> dirtyRegions = new HashSet<UIRegion>();
        private Dictionary<string, object> cachedValues = new Dictionary<string, object>();
        private Dictionary<string, int> lastUpdateFrame = new Dictionary<string, int>();
        
        // Input debouncing
        private Dictionary<string, DebouncedInput> debouncedInputs = new Dictionary<string, DebouncedInput>();
        private const float DEFAULT_DEBOUNCE_MS = 300f;
        
        private class DebouncedInput
        {
            public float lastInputTime = 0f;
            public object pendingValue = null;
            public Action<object> callback = null;
            public float debounceTime = DEFAULT_DEBOUNCE_MS;
        }
        
        public enum UIRegion
        {
            ColonistList,
            Dashboard,
            JobSettings,
            GlobalSettings,
            CustomRoles,
            PerformanceMetrics,
            SkillMatrix,
            JobQueue
        }
        
        /// <summary>
        /// Mark a UI region as dirty (needs redraw)
        /// </summary>
        public void MarkDirty(UIRegion region)
        {
            dirtyRegions.Add(region);
        }
        
        /// <summary>
        /// Check if a region is dirty
        /// </summary>
        public bool IsDirty(UIRegion region)
        {
            return dirtyRegions.Contains(region);
        }
        
        /// <summary>
        /// Mark region as clean (after redraw)
        /// </summary>
        public void MarkClean(UIRegion region)
        {
            dirtyRegions.Remove(region);
        }
        
        /// <summary>
        /// Mark all regions as dirty
        /// </summary>
        public void MarkAllDirty()
        {
            foreach (UIRegion region in Enum.GetValues(typeof(UIRegion)))
            {
                dirtyRegions.Add(region);
            }
        }
        
        /// <summary>
        /// Cache a computed value (avoid recomputing on every frame)
        /// </summary>
        public void CacheValue<T>(string key, T value, int framesToLive = 60)
        {
            cachedValues[key] = value;
            lastUpdateFrame[key] = Time.frameCount;
        }
        
        /// <summary>
        /// Get cached value or compute if missing/expired
        /// </summary>
        public T GetOrCompute<T>(string key, Func<T> computeFunc, int framesToLive = 60)
        {
            using (PerformanceProfiler.Profile("UIStateManager.GetOrCompute"))
            {
                // Check if cached and not expired
                if (cachedValues.TryGetValue(key, out object cached) && 
                    lastUpdateFrame.TryGetValue(key, out int lastFrame))
                {
                    if (Time.frameCount - lastFrame < framesToLive)
                    {
                        return (T)cached;
                    }
                }
                
                // Compute and cache
                T value = computeFunc();
                CacheValue(key, value, framesToLive);
                return value;
            }
        }
        
        /// <summary>
        /// Invalidate specific cached value
        /// </summary>
        public void InvalidateCache(string key)
        {
            cachedValues.Remove(key);
            lastUpdateFrame.Remove(key);
        }
        
        /// <summary>
        /// Clear all cached values
        /// </summary>
        public void ClearCache()
        {
            cachedValues.Clear();
            lastUpdateFrame.Clear();
        }
        
        /// <summary>
        /// Register debounced input (e.g., sliders, text fields)
        /// </summary>
        public void RegisterDebouncedInput(string key, object value, Action<object> callback, float debounceMs = DEFAULT_DEBOUNCE_MS)
        {
            if (!debouncedInputs.TryGetValue(key, out DebouncedInput input))
            {
                input = new DebouncedInput
                {
                    callback = callback,
                    debounceTime = debounceMs
                };
                debouncedInputs[key] = input;
            }
            
            input.lastInputTime = Time.realtimeSinceStartup;
            input.pendingValue = value;
        }
        
        /// <summary>
        /// Process debounced inputs (call once per frame)
        /// </summary>
        public void ProcessDebouncedInputs()
        {
            using (PerformanceProfiler.Profile("UIStateManager.ProcessDebounced"))
            {
                float currentTime = Time.realtimeSinceStartup;
                
                foreach (var kvp in debouncedInputs)
                {
                    var input = kvp.Value;
                    
                    // Check if debounce time elapsed
                    if (input.pendingValue != null && 
                        (currentTime - input.lastInputTime) * 1000f >= input.debounceTime)
                    {
                        // Invoke callback
                        try
                        {
                            input.callback?.Invoke(input.pendingValue);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[UIStateManager] Error in debounced callback for {kvp.Key}: {ex}");
                        }
                        
                        input.pendingValue = null;
                    }
                }
            }
        }
        
        /// <summary>
        /// Check if value has changed since last check
        /// </summary>
        public bool HasChanged<T>(string key, T currentValue)
        {
            if (cachedValues.TryGetValue(key, out object cached))
            {
                return !EqualityComparer<T>.Default.Equals((T)cached, currentValue);
            }
            
            // First time seeing this value
            cachedValues[key] = currentValue;
            return true;
        }
        
        /// <summary>
        /// Batch update - mark multiple regions dirty
        /// </summary>
        public void BatchMarkDirty(params UIRegion[] regions)
        {
            foreach (var region in regions)
            {
                dirtyRegions.Add(region);
            }
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            return $"Dirty regions: {dirtyRegions.Count}, Cached values: {cachedValues.Count}, Debounced inputs: {debouncedInputs.Count}";
        }
        
        /// <summary>
        /// Clear all state
        /// </summary>
        public void Clear()
        {
            dirtyRegions.Clear();
            cachedValues.Clear();
            lastUpdateFrame.Clear();
            debouncedInputs.Clear();
        }
    }
    
    /// <summary>
    /// Deferred renderer - separates calculation from rendering
    /// </summary>
    public class DeferredRenderer
    {
        private Dictionary<string, CachedRenderData> renderCache = new Dictionary<string, CachedRenderData>();
        
        private class CachedRenderData
        {
            public object data;
            public int computedAtFrame;
            public bool isComputing;
        }
        
        /// <summary>
        /// Get data for rendering (uses cached or triggers compute)
        /// </summary>
        public T GetRenderData<T>(string key, Func<T> computeFunc, int cacheFrames = 60)
        {
            using (PerformanceProfiler.Profile("DeferredRenderer.GetRenderData"))
            {
                if (renderCache.TryGetValue(key, out CachedRenderData cached))
                {
                    // Return cached if not expired
                    if (Time.frameCount - cached.computedAtFrame < cacheFrames)
                    {
                        return (T)cached.data;
                    }
                }
                
                // Compute new data
                T data = computeFunc();
                
                renderCache[key] = new CachedRenderData
                {
                    data = data,
                    computedAtFrame = Time.frameCount,
                    isComputing = false
                };
                
                return data;
            }
        }
        
        /// <summary>
        /// Invalidate cached render data
        /// </summary>
        public void Invalidate(string key)
        {
            renderCache.Remove(key);
        }
        
        /// <summary>
        /// Clear all cached render data
        /// </summary>
        public void ClearCache()
        {
            renderCache.Clear();
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            return $"Cached renders: {renderCache.Count}";
        }
    }
    
    /// <summary>
    /// Frame rate limiter for non-critical UI updates
    /// </summary>
    public class FrameRateLimiter
    {
        private int lastUpdateFrame = 0;
        private int targetFrameInterval;
        
        public FrameRateLimiter(int fps = 30)
        {
            targetFrameInterval = Mathf.Max(1, 60 / fps); // Convert FPS to frame interval
        }
        
        /// <summary>
        /// Check if enough frames have passed for next update
        /// </summary>
        public bool ShouldUpdate()
        {
            int currentFrame = Time.frameCount;
            
            if (currentFrame - lastUpdateFrame >= targetFrameInterval)
            {
                lastUpdateFrame = currentFrame;
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Force next update
        /// </summary>
        public void ForceUpdate()
        {
            lastUpdateFrame = 0;
        }
    }
}

