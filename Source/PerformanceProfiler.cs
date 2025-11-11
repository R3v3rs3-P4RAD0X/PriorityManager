using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    /// <summary>
    /// Lightweight performance profiler for tracking method execution times.
    /// Tracks key systems and generates performance reports.
    /// </summary>
    public static class PerformanceProfiler
    {
        private static Dictionary<string, ProfileEntry> profiles = new Dictionary<string, ProfileEntry>();
        private static bool enabled = true;
        private static StreamWriter logWriter = null;
        private static int frameCount = 0;
        private static readonly int REPORT_INTERVAL = 600; // Report every 10 seconds at 60fps
        
        private class ProfileEntry
        {
            public long totalTicks = 0;
            public int callCount = 0;
            public long minTicks = long.MaxValue;
            public long maxTicks = 0;
            public List<long> recentSamples = new List<long>(100);
            
            public double AverageMs => callCount > 0 ? (totalTicks / callCount) / (double)Stopwatch.Frequency * 1000.0 : 0;
            public double TotalMs => totalTicks / (double)Stopwatch.Frequency * 1000.0;
            public double MinMs => minTicks != long.MaxValue ? minTicks / (double)Stopwatch.Frequency * 1000.0 : 0;
            public double MaxMs => maxTicks / (double)Stopwatch.Frequency * 1000.0;
            
            public double PercentileMs(double percentile)
            {
                if (recentSamples.Count == 0) return 0;
                var sorted = recentSamples.OrderBy(x => x).ToList();
                int index = (int)(sorted.Count * percentile);
                return sorted[Math.Min(index, sorted.Count - 1)] / (double)Stopwatch.Frequency * 1000.0;
            }
            
            public void AddSample(long ticks)
            {
                totalTicks += ticks;
                callCount++;
                minTicks = Math.Min(minTicks, ticks);
                maxTicks = Math.Max(maxTicks, ticks);
                
                recentSamples.Add(ticks);
                if (recentSamples.Count > 100)
                {
                    recentSamples.RemoveAt(0);
                }
            }
            
            public void Reset()
            {
                totalTicks = 0;
                callCount = 0;
                minTicks = long.MaxValue;
                maxTicks = 0;
                recentSamples.Clear();
            }
        }
        
        public static bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }
        
        /// <summary>
        /// Profile a method execution. Usage: using (PerformanceProfiler.Profile("MethodName")) { ... }
        /// </summary>
        public static ProfileScope Profile(string methodName)
        {
            return new ProfileScope(methodName);
        }
        
        public struct ProfileScope : IDisposable
        {
            private string methodName;
            private Stopwatch stopwatch;
            private bool disposed;
            
            public ProfileScope(string methodName)
            {
                this.methodName = methodName;
                this.stopwatch = Stopwatch.StartNew();
                this.disposed = false;
            }
            
            public void Dispose()
            {
                if (!disposed && enabled)
                {
                    stopwatch.Stop();
                    RecordSample(methodName, stopwatch.ElapsedTicks);
                    disposed = true;
                }
            }
        }
        
        private static void RecordSample(string methodName, long ticks)
        {
            if (!profiles.TryGetValue(methodName, out ProfileEntry entry))
            {
                entry = new ProfileEntry();
                profiles[methodName] = entry;
            }
            
            entry.AddSample(ticks);
        }
        
        /// <summary>
        /// Call once per frame to update profiler state
        /// </summary>
        public static void OnGUI()
        {
            if (!enabled) return;
            
            frameCount++;
            if (frameCount >= REPORT_INTERVAL)
            {
                frameCount = 0;
                LogReport();
            }
            
            // Draw overlay if debug mode
            if (Prefs.DevMode && PriorityManagerMod.settings?.showPerformanceOverlay == true)
            {
                DrawOverlay();
            }
        }
        
        private static void DrawOverlay()
        {
            float width = 400f;
            float height = 500f;
            Rect overlayRect = new Rect(Screen.width - width - 10, 10, width, height);
            
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            Widgets.DrawBoxSolid(overlayRect, Color.black);
            GUI.color = Color.white;
            Widgets.DrawBox(overlayRect, 2);
            
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            
            float curY = 15f;
            float lineHeight = 18f;
            
            // Title
            Rect titleRect = new Rect(overlayRect.x + 10, overlayRect.y + curY, width - 20, lineHeight);
            Widgets.Label(titleRect, "PRIORITY MANAGER V2.0 - PERFORMANCE");
            curY += lineHeight + 5;
            
            // Get top 15 methods by total time
            var topMethods = profiles.OrderByDescending(kvp => kvp.Value.TotalMs).Take(15).ToList();
            
            foreach (var kvp in topMethods)
            {
                string name = kvp.Key;
                ProfileEntry entry = kvp.Value;
                
                // Truncate long names
                if (name.Length > 25)
                {
                    name = name.Substring(0, 22) + "...";
                }
                
                string stats = $"{name,-25} {entry.AverageMs,6:F2}ms avg  {entry.MaxMs,6:F2}ms max  {entry.callCount,5} calls";
                
                Rect statRect = new Rect(overlayRect.x + 10, overlayRect.y + curY, width - 20, lineHeight);
                
                // Color code by performance
                if (entry.AverageMs > 5.0)
                {
                    GUI.color = Color.red;
                }
                else if (entry.AverageMs > 2.0)
                {
                    GUI.color = Color.yellow;
                }
                else
                {
                    GUI.color = Color.green;
                }
                
                Widgets.Label(statRect, stats);
                GUI.color = Color.white;
                
                curY += lineHeight;
            }
            
            // Summary stats
            curY += 5;
            double totalTimeMs = profiles.Sum(kvp => kvp.Value.TotalMs);
            int totalCalls = profiles.Sum(kvp => kvp.Value.callCount);
            
            Rect summaryRect = new Rect(overlayRect.x + 10, overlayRect.y + curY, width - 20, lineHeight);
            Widgets.Label(summaryRect, $"Total: {totalTimeMs:F2}ms across {totalCalls} calls");
            curY += lineHeight;
            
            Rect fpsRect = new Rect(overlayRect.x + 10, overlayRect.y + curY, width - 20, lineHeight);
            float fps = 1f / Time.deltaTime;
            GUI.color = fps > 50 ? Color.green : fps > 30 ? Color.yellow : Color.red;
            Widgets.Label(fpsRect, $"FPS: {fps:F1}");
            GUI.color = Color.white;
            
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        private static void LogReport()
        {
            if (profiles.Count == 0) return;
            
            EnsureLogWriter();
            
            if (logWriter == null) return;
            
            try
            {
                logWriter.WriteLine($"\n=== Performance Report - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                logWriter.WriteLine($"Tick: {Find.TickManager.TicksGame}");
                logWriter.WriteLine($"Colony Size: {GetColonySize()} colonists");
                logWriter.WriteLine();
                
                // Sort by total time
                var sortedProfiles = profiles.OrderByDescending(kvp => kvp.Value.TotalMs).ToList();
                
                logWriter.WriteLine($"{"Method",-40} {"Avg(ms)",10} {"Min(ms)",10} {"Max(ms)",10} {"P95(ms)",10} {"Calls",8} {"Total(ms)",12}");
                logWriter.WriteLine(new string('-', 120));
                
                foreach (var kvp in sortedProfiles)
                {
                    ProfileEntry entry = kvp.Value;
                    logWriter.WriteLine(
                        $"{kvp.Key,-40} {entry.AverageMs,10:F3} {entry.MinMs,10:F3} {entry.MaxMs,10:F3} {entry.PercentileMs(0.95),10:F3} {entry.callCount,8} {entry.TotalMs,12:F3}"
                    );
                }
                
                logWriter.WriteLine();
                double totalMs = sortedProfiles.Sum(kvp => kvp.Value.TotalMs);
                int totalCalls = sortedProfiles.Sum(kvp => kvp.Value.callCount);
                logWriter.WriteLine($"Total: {totalMs:F3}ms across {totalCalls} calls");
                logWriter.WriteLine();
                
                logWriter.Flush();
            }
            catch (Exception ex)
            {
                Log.Error($"[PriorityManager] Failed to write performance log: {ex}");
            }
        }
        
        private static void EnsureLogWriter()
        {
            if (logWriter != null) return;
            
            try
            {
                string logPath = Path.Combine(GenFilePaths.ConfigFolderPath, "PriorityManager_Performance.log");
                logWriter = new StreamWriter(logPath, append: true);
                logWriter.WriteLine($"\n\n=== New Session Started - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                logWriter.Flush();
            }
            catch (Exception ex)
            {
                Log.Error($"[PriorityManager] Failed to open performance log: {ex}");
            }
        }
        
        private static int GetColonySize()
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            return gameComp?.GetAllColonists()?.Count ?? 0;
        }
        
        /// <summary>
        /// Reset all profiling data
        /// </summary>
        public static void Reset()
        {
            foreach (var entry in profiles.Values)
            {
                entry.Reset();
            }
        }
        
        /// <summary>
        /// Clear all profiles and start fresh
        /// </summary>
        public static void Clear()
        {
            profiles.Clear();
            frameCount = 0;
        }
        
        /// <summary>
        /// Get current profile data for external analysis
        /// </summary>
        public static Dictionary<string, (double avgMs, double maxMs, int calls)> GetProfileData()
        {
            var result = new Dictionary<string, (double, double, int)>();
            foreach (var kvp in profiles)
            {
                result[kvp.Key] = (kvp.Value.AverageMs, kvp.Value.MaxMs, kvp.Value.callCount);
            }
            return result;
        }
        
        /// <summary>
        /// Close log writer on cleanup
        /// </summary>
        public static void Cleanup()
        {
            if (logWriter != null)
            {
                try
                {
                    logWriter.WriteLine($"\n=== Session Ended - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    logWriter.Flush();
                    logWriter.Close();
                    logWriter = null;
                }
                catch (Exception ex)
                {
                    Log.Error($"[PriorityManager] Failed to close performance log: {ex}");
                }
            }
        }
    }
}

