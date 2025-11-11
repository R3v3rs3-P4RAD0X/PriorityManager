using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager.Analytics
{
    /// <summary>
    /// Real-time performance monitoring and analytics dashboard
    /// v2.0: Visualizes system performance, bottlenecks, and trends
    /// </summary>
    public class PerformanceMonitor
    {
        private static PerformanceMonitor instance;
        
        // Historical data
        private List<PerformanceSnapshot> history = new List<PerformanceSnapshot>();
        private const int MAX_HISTORY = 300; // 5 minutes at 1 sample/second
        
        // Current metrics
        private PerformanceSnapshot currentSnapshot;
        private int lastSnapshotTick = 0;
        private const int SNAPSHOT_INTERVAL = 60; // 1 snapshot per second at 60fps
        
        public class PerformanceSnapshot
        {
            public int tick;
            public float fps;
            public long memoryUsedMB;
            public Dictionary<string, float> systemTimings = new Dictionary<string, float>();
            public int eventQueueSize;
            public int dirtyColonistCount;
            public int colonistCount;
        }
        
        public static PerformanceMonitor Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PerformanceMonitor();
                }
                return instance;
            }
        }
        
        private PerformanceMonitor()
        {
            currentSnapshot = new PerformanceSnapshot();
            Log.Message("[PriorityManager] PerformanceMonitor initialized");
        }
        
        /// <summary>
        /// Update performance metrics (call once per frame)
        /// </summary>
        public void Update()
        {
            using (PerformanceProfiler.Profile("PerformanceMonitor.Update"))
            {
                int currentTick = Find.TickManager.TicksGame;
                
                if (currentTick - lastSnapshotTick >= SNAPSHOT_INTERVAL)
                {
                    lastSnapshotTick = currentTick;
                    CaptureSnapshot();
                }
            }
        }
        
        private void CaptureSnapshot()
        {
            var snapshot = new PerformanceSnapshot
            {
                tick = Find.TickManager.TicksGame,
                fps = 1f / Time.deltaTime,
                memoryUsedMB = GC.GetTotalMemory(false) / (1024 * 1024),
                systemTimings = new Dictionary<string, float>(),
                colonistCount = GetColonistCount()
            };
            
            // Get timing data from profiler
            var profileData = PerformanceProfiler.GetProfileData();
            foreach (var kvp in profileData)
            {
                snapshot.systemTimings[kvp.Key] = (float)kvp.Value.avgMs;
            }
            
            // Get event system stats
            snapshot.eventQueueSize = Events.EventDispatcher.Instance.GetQueuedEventCount();
            snapshot.dirtyColonistCount = Events.IncrementalUpdater.Instance.GetDirtyCount();
            
            history.Add(snapshot);
            currentSnapshot = snapshot;
            
            // Trim old history
            if (history.Count > MAX_HISTORY)
            {
                history.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Draw performance analytics dashboard
        /// </summary>
        public void DrawDashboard(Rect rect)
        {
            using (PerformanceProfiler.Profile("PerformanceMonitor.DrawDashboard"))
            {
                Listing_Standard listing = new Listing_Standard();
                listing.Begin(rect);
                
                // Title
                Text.Font = GameFont.Medium;
                listing.Label("Performance Analytics");
                Text.Font = GameFont.Small;
                listing.Gap();
                
                // Current metrics
                if (currentSnapshot != null)
                {
                    listing.Label($"FPS: {currentSnapshot.fps:F1}");
                    listing.Label($"Memory: {currentSnapshot.memoryUsedMB} MB");
                    listing.Label($"Colonists: {currentSnapshot.colonistCount}");
                    listing.Label($"Event Queue: {currentSnapshot.eventQueueSize}");
                    listing.Label($"Dirty Colonists: {currentSnapshot.dirtyColonistCount}");
                    listing.Gap();
                }
                
                // System timings
                Text.Font = GameFont.Small;
                listing.Label("System Timings (average ms):");
                
                if (currentSnapshot?.systemTimings != null)
                {
                    var topSystems = currentSnapshot.systemTimings
                        .OrderByDescending(kvp => kvp.Value)
                        .Take(10);
                    
                    foreach (var kvp in topSystems)
                    {
                        Color color = GetColorForTiming(kvp.Value);
                        GUI.color = color;
                        listing.Label($"  {kvp.Key}: {kvp.Value:F3}ms");
                        GUI.color = Color.white;
                    }
                }
                
                listing.Gap();
                
                // Historical trends
                if (history.Count > 10)
                {
                    listing.Label("Trends (last 5 minutes):");
                    
                    float avgFps = history.Average(s => s.fps);
                    float minFps = history.Min(s => s.fps);
                    long avgMemory = (long)history.Average(s => s.memoryUsedMB);
                    long maxMemory = history.Max(s => s.memoryUsedMB);
                    
                    listing.Label($"  FPS: {avgFps:F1} avg, {minFps:F1} min");
                    listing.Label($"  Memory: {avgMemory} MB avg, {maxMemory} MB peak");
                    
                    // Detect trends
                    if (DetectFPSDecline())
                    {
                        GUI.color = Color.yellow;
                        listing.Label("  ⚠ FPS declining over time");
                        GUI.color = Color.white;
                    }
                    
                    if (DetectMemoryLeak())
                    {
                        GUI.color = Color.red;
                        listing.Label("  ⚠ Possible memory leak detected!");
                        GUI.color = Color.white;
                    }
                }
                
                listing.Gap();
                
                // Export buttons
                if (listing.ButtonText("Export Performance Data (CSV)"))
                {
                    ExportToCSV();
                }
                
                if (listing.ButtonText("Reset Statistics"))
                {
                    Clear();
                    PerformanceProfiler.Reset();
                }
                
                listing.End();
            }
        }
        
        private Color GetColorForTiming(float ms)
        {
            if (ms > 5f) return Color.red;
            if (ms > 2f) return Color.yellow;
            return Color.green;
        }
        
        private bool DetectFPSDecline()
        {
            if (history.Count < 100)
                return false;
            
            // Compare first quarter vs last quarter
            int quarterSize = history.Count / 4;
            float firstQuarterAvg = history.Take(quarterSize).Average(s => s.fps);
            float lastQuarterAvg = history.Skip(history.Count - quarterSize).Average(s => s.fps);
            
            return lastQuarterAvg < firstQuarterAvg * 0.8f; // 20% decline
        }
        
        private bool DetectMemoryLeak()
        {
            if (history.Count < 100)
                return false;
            
            // Check if memory consistently increasing
            long startMemory = history.First().memoryUsedMB;
            long endMemory = history.Last().memoryUsedMB;
            long increase = endMemory - startMemory;
            
            return increase > 50; // 50MB increase = potential leak
        }
        
        private int GetColonistCount()
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            return gameComp?.GetAllColonists()?.Count ?? 0;
        }
        
        /// <summary>
        /// Export performance data to CSV
        /// </summary>
        public void ExportToCSV()
        {
            try
            {
                string path = Path.Combine(GenFilePaths.ConfigFolderPath, 
                    $"PriorityManager_Analytics_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                
                using (var writer = new StreamWriter(path))
                {
                    // Header
                    writer.WriteLine("Tick,FPS,MemoryMB,ColonistCount,EventQueue,DirtyColonists,MapComponentTick,AssignPriorities");
                    
                    // Data
                    foreach (var snapshot in history)
                    {
                        snapshot.systemTimings.TryGetValue("MapComponentTick", out float mapTick);
                        snapshot.systemTimings.TryGetValue("AssignPriorities", out float assignPriorities);
                        
                        writer.WriteLine($"{snapshot.tick},{snapshot.fps:F2},{snapshot.memoryUsedMB}," +
                            $"{snapshot.colonistCount},{snapshot.eventQueueSize},{snapshot.dirtyColonistCount}," +
                            $"{mapTick:F3},{assignPriorities:F3}");
                    }
                }
                
                Messages.Message($"Performance data exported to: {path}", MessageTypeDefOf.TaskCompletion);
                Log.Message($"[PerformanceMonitor] Exported {history.Count} snapshots to {path}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PerformanceMonitor] Failed to export: {ex}");
            }
        }
        
        /// <summary>
        /// Get current snapshot
        /// </summary>
        public PerformanceSnapshot GetCurrentSnapshot()
        {
            return currentSnapshot;
        }
        
        /// <summary>
        /// Get historical data
        /// </summary>
        public List<PerformanceSnapshot> GetHistory()
        {
            return new List<PerformanceSnapshot>(history);
        }
        
        /// <summary>
        /// Clear all data
        /// </summary>
        public void Clear()
        {
            history.Clear();
            currentSnapshot = new PerformanceSnapshot();
            lastSnapshotTick = 0;
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            if (history.Count == 0)
                return "No data collected yet";
            
            float avgFps = history.Average(s => s.fps);
            long avgMemory = (long)history.Average(s => s.memoryUsedMB);
            
            return $"Snapshots: {history.Count}/{MAX_HISTORY}, Avg FPS: {avgFps:F1}, Avg Memory: {avgMemory}MB";
        }
    }
}

