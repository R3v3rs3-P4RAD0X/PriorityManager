using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;

namespace PriorityManager
{
    /// <summary>
    /// Benchmark suite for testing performance at various colony sizes
    /// </summary>
    public static class Benchmarks
    {
        private static List<BenchmarkResult> results = new List<BenchmarkResult>();
        
        public class BenchmarkResult
        {
            public string testName;
            public int colonistCount;
            public double averageMs;
            public double minMs;
            public double maxMs;
            public double p95Ms;
            public int iterations;
            public DateTime timestamp;
            
            public override string ToString()
            {
                return $"{testName,-40} | {colonistCount,3} colonists | Avg: {averageMs,7:F3}ms | Min: {minMs,7:F3}ms | Max: {maxMs,7:F3}ms | P95: {p95Ms,7:F3}ms | {iterations} iterations";
            }
        }
        
        /// <summary>
        /// Run full benchmark suite
        /// </summary>
        public static void RunAll()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                Log.Error("[PriorityManager Benchmark] No active game or map. Load a save first.");
                return;
            }
            
            Log.Message("[PriorityManager Benchmark] Starting benchmark suite...");
            results.Clear();
            
            // Get current colony size
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
            {
                Log.Error("[PriorityManager Benchmark] Game component not found.");
                return;
            }
            
            int colonistCount = gameComp.GetAllColonists().Count;
            
            // Run benchmarks
            BenchmarkTickHandler(colonistCount);
            BenchmarkPriorityAssignment(colonistCount);
            BenchmarkWorkScanning(colonistCount);
            BenchmarkUIRendering(colonistCount);
            
            // Print results
            PrintResults();
        }
        
        private static void BenchmarkTickHandler(int colonistCount)
        {
            Log.Message("[Benchmark] Testing MapComponentTick...");
            
            var mapComp = Find.CurrentMap?.GetComponent<PriorityManagerMapComponent>();
            if (mapComp == null)
            {
                Log.Warning("[Benchmark] MapComponent not found, skipping.");
                return;
            }
            
            int iterations = 100;
            List<double> samples = new List<double>();
            
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                mapComp.MapComponentTick();
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            RecordResult("MapComponentTick", colonistCount, samples);
        }
        
        private static void BenchmarkPriorityAssignment(int colonistCount)
        {
            Log.Message("[Benchmark] Testing priority assignment...");
            
            var gameComp = PriorityDataHelper.GetGameComponent();
            var colonists = gameComp?.GetAllColonists();
            
            if (colonists == null || colonists.Count == 0)
            {
                Log.Warning("[Benchmark] No colonists found, skipping.");
                return;
            }
            
            int iterations = 50;
            List<double> samples = new List<double>();
            
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                PriorityAssigner.AssignAllColonistPriorities(force: true);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            RecordResult("AssignAllPriorities", colonistCount, samples);
            
            // Single colonist assignment
            samples.Clear();
            var testPawn = colonists.First();
            iterations = 200;
            
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                PriorityAssigner.AssignPriorities(testPawn, force: true);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            RecordResult("AssignSinglePriority", colonistCount, samples);
        }
        
        private static void BenchmarkWorkScanning(int colonistCount)
        {
            Log.Message("[Benchmark] Testing work scanning...");
            
            var map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[Benchmark] No map found, skipping.");
                return;
            }
            
            int iterations = 100;
            List<double> samples = new List<double>();
            
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                var urgency = WorkScanner.ScoreWorkUrgency(map);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            RecordResult("WorkScanner.ScoreUrgency", colonistCount, samples);
            
            // Job queue scanning
            samples.Clear();
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                var jobs = JobQueueScanner.ScanMap(map, maxJobs: 20);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            RecordResult("JobQueueScanner.ScanMap", colonistCount, samples);
        }
        
        private static void BenchmarkUIRendering(int colonistCount)
        {
            Log.Message("[Benchmark] Testing UI operations...");
            
            var map = Find.CurrentMap;
            if (map == null)
            {
                Log.Warning("[Benchmark] No map found, skipping.");
                return;
            }
            
            int iterations = 50;
            List<double> samples = new List<double>();
            
            for (int i = 0; i < iterations; i++)
            {
                Stopwatch sw = Stopwatch.StartNew();
                var metrics = new ColonyMetrics(map);
                sw.Stop();
                samples.Add(sw.Elapsed.TotalMilliseconds);
            }
            
            RecordResult("ColonyMetrics.Calculate", colonistCount, samples);
        }
        
        private static void RecordResult(string testName, int colonistCount, List<double> samples)
        {
            if (samples.Count == 0) return;
            
            var sorted = samples.OrderBy(x => x).ToList();
            int p95Index = (int)(sorted.Count * 0.95);
            
            var result = new BenchmarkResult
            {
                testName = testName,
                colonistCount = colonistCount,
                averageMs = samples.Average(),
                minMs = sorted.First(),
                maxMs = sorted.Last(),
                p95Ms = sorted[Math.Min(p95Index, sorted.Count - 1)],
                iterations = samples.Count,
                timestamp = DateTime.Now
            };
            
            results.Add(result);
        }
        
        private static void PrintResults()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("\n╔══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                                    PRIORITY MANAGER V2.0 - BENCHMARK RESULTS                                            ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine($"║ Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}                                                                                         ║");
            sb.AppendLine($"║ Game Tick: {Find.TickManager.TicksGame}                                                                                                    ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
            sb.AppendLine("║ Test Name                                | Count | Avg (ms)  | Min (ms)  | Max (ms)  | P95 (ms)  | Iterations            ║");
            sb.AppendLine("╠══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╣");
            
            foreach (var result in results)
            {
                sb.AppendLine($"║ {result.testName,-40} | {result.colonistCount,5} | {result.averageMs,9:F3} | {result.minMs,9:F3} | {result.maxMs,9:F3} | {result.p95Ms,9:F3} | {result.iterations,6} iter     ║");
            }
            
            sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════════╝");
            
            Log.Message(sb.ToString());
            
            // Also log to file
            LogToFile();
        }
        
        private static void LogToFile()
        {
            try
            {
                string path = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "PriorityManager_Benchmarks.csv");
                bool fileExists = System.IO.File.Exists(path);
                
                using (var writer = new System.IO.StreamWriter(path, append: true))
                {
                    // Write header if new file
                    if (!fileExists)
                    {
                        writer.WriteLine("Timestamp,TestName,ColonistCount,AvgMs,MinMs,MaxMs,P95Ms,Iterations");
                    }
                    
                    // Write results
                    foreach (var result in results)
                    {
                        writer.WriteLine($"{result.timestamp:yyyy-MM-dd HH:mm:ss},{result.testName},{result.colonistCount},{result.averageMs:F3},{result.minMs:F3},{result.maxMs:F3},{result.p95Ms:F3},{result.iterations}");
                    }
                }
                
                Log.Message($"[PriorityManager Benchmark] Results saved to: {path}");
            }
            catch (Exception ex)
            {
                Log.Error($"[PriorityManager Benchmark] Failed to save results: {ex}");
            }
        }
        
        /// <summary>
        /// Quick performance test - single iteration
        /// </summary>
        public static void QuickTest()
        {
            if (Current.Game == null || Find.CurrentMap == null)
            {
                Log.Error("[PriorityManager Benchmark] No active game or map.");
                return;
            }
            
            var gameComp = PriorityDataHelper.GetGameComponent();
            int colonistCount = gameComp?.GetAllColonists()?.Count ?? 0;
            
            Log.Message($"[Benchmark Quick Test] Colony size: {colonistCount} colonists");
            
            // Test tick handler
            var mapComp = Find.CurrentMap?.GetComponent<PriorityManagerMapComponent>();
            if (mapComp != null)
            {
                Stopwatch sw = Stopwatch.StartNew();
                mapComp.MapComponentTick();
                sw.Stop();
                Log.Message($"[Benchmark] MapComponentTick: {sw.Elapsed.TotalMilliseconds:F3}ms");
            }
            
            // Test priority assignment
            Stopwatch sw2 = Stopwatch.StartNew();
            PriorityAssigner.AssignAllColonistPriorities(force: true);
            sw2.Stop();
            Log.Message($"[Benchmark] AssignAllPriorities: {sw2.Elapsed.TotalMilliseconds:F3}ms");
            
            // Test work scanning
            Stopwatch sw3 = Stopwatch.StartNew();
            var urgency = WorkScanner.ScoreWorkUrgency(Find.CurrentMap);
            sw3.Stop();
            Log.Message($"[Benchmark] WorkScanner: {sw3.Elapsed.TotalMilliseconds:F3}ms");
        }
        
        /// <summary>
        /// Get most recent benchmark results
        /// </summary>
        public static List<BenchmarkResult> GetResults()
        {
            return new List<BenchmarkResult>(results);
        }
        
        /// <summary>
        /// Clear all benchmark results
        /// </summary>
        public static void ClearResults()
        {
            results.Clear();
        }
    }
}

