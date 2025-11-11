using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace PriorityManager.Assignment
{
    /// <summary>
    /// Parallel processing for large colonies using multi-threading
    /// v2.0: Enables 100+ colonist support with minimal performance impact
    /// </summary>
    public static class ParallelAssigner
    {
        private const int PARALLEL_THRESHOLD = 50; // Use parallel for 50+ colonists
        private const int CHUNK_SIZE = 25; // Process 25 colonists per thread
        
        private static bool parallelEnabled = true;
        private static int totalParallelUpdates = 0;
        private static int totalSingleThreadUpdates = 0;
        
        public static bool Enabled
        {
            get => parallelEnabled;
            set => parallelEnabled = value;
        }
        
        /// <summary>
        /// Assign priorities for multiple colonists (uses parallel processing if beneficial)
        /// </summary>
        public static void AssignMultiple(List<Pawn> colonists, bool force = false)
        {
            using (PerformanceProfiler.Profile("ParallelAssigner.AssignMultiple"))
            {
                if (colonists == null || colonists.Count == 0)
                    return;
                
                // Use parallel processing for large colonies
                if (parallelEnabled && colonists.Count >= PARALLEL_THRESHOLD)
                {
                    AssignParallel(colonists, force);
                    totalParallelUpdates++;
                }
                else
                {
                    AssignSequential(colonists, force);
                    totalSingleThreadUpdates++;
                }
            }
        }
        
        /// <summary>
        /// Parallel assignment using Task.Parallel
        /// </summary>
        private static void AssignParallel(List<Pawn> colonists, bool force)
        {
            using (PerformanceProfiler.Profile("ParallelAssigner.Parallel"))
            {
                // Split colonists into chunks
                int chunkCount = (colonists.Count + CHUNK_SIZE - 1) / CHUNK_SIZE;
                var chunks = new List<List<Pawn>>(chunkCount);
                
                for (int i = 0; i < colonists.Count; i += CHUNK_SIZE)
                {
                    int count = Math.Min(CHUNK_SIZE, colonists.Count - i);
                    var chunk = new List<Pawn>(count);
                    for (int j = 0; j < count; j++)
                    {
                        chunk.Add(colonists[i + j]);
                    }
                    chunks.Add(chunk);
                }
                
                // Process chunks in parallel
                Parallel.ForEach(chunks, chunk =>
                {
                    foreach (var pawn in chunk)
                    {
                        try
                        {
                            PriorityAssigner.AssignPriorities(pawn, force);
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[ParallelAssigner] Error assigning priorities for {pawn?.Name?.ToStringShort ?? "unknown"}: {ex}");
                        }
                    }
                });
                
                Log.Message($"[ParallelAssigner] Processed {colonists.Count} colonists in {chunkCount} parallel chunks");
            }
        }
        
        /// <summary>
        /// Sequential assignment (single-threaded)
        /// </summary>
        private static void AssignSequential(List<Pawn> colonists, bool force)
        {
            using (PerformanceProfiler.Profile("ParallelAssigner.Sequential"))
            {
                foreach (var pawn in colonists)
                {
                    PriorityAssigner.AssignPriorities(pawn, force);
                }
            }
        }
        
        /// <summary>
        /// Process work scoring in parallel for performance
        /// </summary>
        public static Dictionary<Pawn, Dictionary<WorkTypeDef, float>> ComputeWorkScoresParallel(
            List<Pawn> colonists, List<WorkTypeDef> workTypes)
        {
            using (PerformanceProfiler.Profile("ParallelAssigner.ComputeScores"))
            {
                var results = new Dictionary<Pawn, Dictionary<WorkTypeDef, float>>(colonists.Count);
                var lockObj = new object();
                
                if (parallelEnabled && colonists.Count >= PARALLEL_THRESHOLD)
                {
                    // Parallel computation
                    Parallel.ForEach(colonists, pawn =>
                    {
                        var scores = new Dictionary<WorkTypeDef, float>(workTypes.Count);
                        
                        foreach (var workType in workTypes)
                        {
                            scores[workType] = PriorityAssigner.CalculateWorkTypeScore(pawn, workType);
                        }
                        
                        lock (lockObj)
                        {
                            results[pawn] = scores;
                        }
                    });
                }
                else
                {
                    // Sequential computation
                    foreach (var pawn in colonists)
                    {
                        var scores = new Dictionary<WorkTypeDef, float>(workTypes.Count);
                        
                        foreach (var workType in workTypes)
                        {
                            scores[workType] = PriorityAssigner.CalculateWorkTypeScore(pawn, workType);
                        }
                        
                        results[pawn] = scores;
                    }
                }
                
                return results;
            }
        }
        
        /// <summary>
        /// Batch update multiple colonists with thread-safe operations
        /// </summary>
        public static void BatchUpdate(List<Pawn> colonists, Dictionary<Pawn, WorkTypeDef> primaryAssignments)
        {
            using (PerformanceProfiler.Profile("ParallelAssigner.BatchUpdate"))
            {
                if (colonists == null || colonists.Count == 0)
                    return;
                
                if (parallelEnabled && colonists.Count >= PARALLEL_THRESHOLD)
                {
                    // Parallel batch update
                    Parallel.ForEach(colonists, pawn =>
                    {
                        if (primaryAssignments.TryGetValue(pawn, out WorkTypeDef primary))
                        {
                            PriorityAssigner.SetPriority(pawn, primary, 1);
                        }
                    });
                }
                else
                {
                    // Sequential batch update
                    foreach (var pawn in colonists)
                    {
                        if (primaryAssignments.TryGetValue(pawn, out WorkTypeDef primary))
                        {
                            PriorityAssigner.SetPriority(pawn, primary, 1);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public static string GetStatistics()
        {
            return $"Parallel: {totalParallelUpdates}, Sequential: {totalSingleThreadUpdates}";
        }
        
        /// <summary>
        /// Reset statistics
        /// </summary>
        public static void ResetStatistics()
        {
            totalParallelUpdates = 0;
            totalSingleThreadUpdates = 0;
        }
    }
}

