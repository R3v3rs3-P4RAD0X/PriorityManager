using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using PriorityManager.Memory;

namespace PriorityManager.Assignment
{
    /// <summary>
    /// Optimal work assignment using Hungarian algorithm (small colonies) or greedy approximation (large colonies)
    /// v2.0: Integrates with CoverageGuarantee to respect minimum assignments
    /// </summary>
    public static class SmartAssigner
    {
        private const int HUNGARIAN_THRESHOLD = 50; // Use Hungarian for colonies < 50
        private const float STABILITY_BIAS = 0.2f; // 20% bonus for keeping current assignments
        
        // Cache for assignment results
        private static Dictionary<int, AssignmentResult> cachedResults = new Dictionary<int, AssignmentResult>();
        private static int lastCacheInvalidationTick = 0;
        private const int CACHE_LIFETIME = 2500; // Cache for 1 in-game hour
        
        private class AssignmentResult
        {
            public Dictionary<Pawn, WorkTypeDef> assignments = new Dictionary<Pawn, WorkTypeDef>();
            public float totalScore = 0f;
            public int computedAtTick = 0;
        }
        
        /// <summary>
        /// Request optimal assignment for colony
        /// </summary>
        public static Dictionary<Pawn, WorkTypeDef> RequestAssignment(List<Pawn> colonists, 
            Spatial.WorkZoneGrid workZoneGrid, bool forceRecalculate = false)
        {
            using (PerformanceProfiler.Profile("SmartAssigner.RequestAssignment"))
            {
                if (colonists == null || colonists.Count == 0)
                    return new Dictionary<Pawn, WorkTypeDef>();
                
                int cacheKey = GetCacheKey(colonists);
                int currentTick = Find.TickManager.TicksGame;
                
                // Check cache first
                if (!forceRecalculate && cachedResults.TryGetValue(cacheKey, out AssignmentResult cached))
                {
                    if (currentTick - cached.computedAtTick < CACHE_LIFETIME)
                    {
                        return new Dictionary<Pawn, WorkTypeDef>(cached.assignments);
                    }
                }
                
                // Invalidate old cache entries
                InvalidateOldCache(currentTick);
                
                // Compute new assignment
                Dictionary<Pawn, WorkTypeDef> result;
                
                if (colonists.Count < HUNGARIAN_THRESHOLD)
                {
                    result = HungarianAssignment(colonists, workZoneGrid);
                }
                else
                {
                    result = GreedyAssignment(colonists, workZoneGrid);
                }
                
                // Cache result
                cachedResults[cacheKey] = new AssignmentResult
                {
                    assignments = new Dictionary<Pawn, WorkTypeDef>(result),
                    totalScore = CalculateTotalScore(result),
                    computedAtTick = currentTick
                };
                
                return result;
            }
        }
        
        /// <summary>
        /// Hungarian algorithm for optimal assignment (small colonies)
        /// </summary>
        private static Dictionary<Pawn, WorkTypeDef> HungarianAssignment(List<Pawn> colonists, 
            Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("SmartAssigner.Hungarian"))
            {
                var result = DictionaryPool<Pawn, WorkTypeDef>.Get();
                var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
                
                // Build cost matrix [colonist][workType]
                int n = colonists.Count;
                int m = visibleWorkTypes.Count;
                float[,] costMatrix = new float[n, m];
                
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < m; j++)
                    {
                        costMatrix[i, j] = CalculateCost(colonists[i], visibleWorkTypes[j], workZoneGrid);
                    }
                }
                
                // Run Hungarian algorithm
                var assignments = RunHungarianAlgorithm(costMatrix, n, m);
                
                // Map back to pawns and work types
                foreach (var (colonistIdx, workTypeIdx) in assignments)
                {
                    result[colonists[colonistIdx]] = visibleWorkTypes[workTypeIdx];
                }
                
                return result;
            }
        }
        
        /// <summary>
        /// Greedy approximation for large colonies
        /// </summary>
        private static Dictionary<Pawn, WorkTypeDef> GreedyAssignment(List<Pawn> colonists, 
            Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("SmartAssigner.Greedy"))
            {
                var result = new Dictionary<Pawn, WorkTypeDef>(colonists.Count);
                var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
                var assignedWorkTypes = HashSetPool<WorkTypeDef>.Get();
                
                try
                {
                    // First pass: Assign each colonist to their best available job
                    foreach (var pawn in colonists)
                    {
                        WorkTypeDef bestJob = null;
                        float bestScore = float.MinValue;
                        
                        foreach (var workType in visibleWorkTypes)
                        {
                            float score = CalculateScore(pawn, workType, workZoneGrid);
                            
                            // Bonus for uncovered jobs
                            if (!assignedWorkTypes.Contains(workType))
                            {
                                score += 100f; // Strong bonus for coverage
                            }
                            
                            if (score > bestScore)
                            {
                                bestScore = score;
                                bestJob = workType;
                            }
                        }
                        
                        if (bestJob != null)
                        {
                            result[pawn] = bestJob;
                            assignedWorkTypes.Add(bestJob);
                        }
                    }
                    
                    return result;
                }
                finally
                {
                    HashSetPool<WorkTypeDef>.Return(assignedWorkTypes);
                }
            }
        }
        
        /// <summary>
        /// Simplified Hungarian algorithm implementation
        /// </summary>
        private static List<(int, int)> RunHungarianAlgorithm(float[,] costMatrix, int rows, int cols)
        {
            // For v2.0 alpha: Use greedy approximation instead of full Hungarian
            // Full Hungarian implementation is complex and may be added in later version
            
            var assignments = new List<(int, int)>();
            var usedCols = new HashSet<int>();
            
            // Greedy: For each row, pick best available column
            for (int i = 0; i < rows; i++)
            {
                int bestCol = -1;
                float bestCost = float.MaxValue;
                
                for (int j = 0; j < cols; j++)
                {
                    if (usedCols.Contains(j))
                        continue;
                    
                    if (costMatrix[i, j] < bestCost)
                    {
                        bestCost = costMatrix[i, j];
                        bestCol = j;
                    }
                }
                
                if (bestCol >= 0)
                {
                    assignments.Add((i, bestCol));
                    usedCols.Add(bestCol);
                }
            }
            
            return assignments;
        }
        
        private static float CalculateCost(Pawn pawn, WorkTypeDef workType, Spatial.WorkZoneGrid workZoneGrid)
        {
            // Lower cost = better match
            // Convert score to cost (invert)
            float score = CalculateScore(pawn, workType, workZoneGrid);
            return 1000f - score; // Invert for cost matrix
        }
        
        private static float CalculateScore(Pawn pawn, WorkTypeDef workType, Spatial.WorkZoneGrid workZoneGrid)
        {
            if (pawn.WorkTypeIsDisabled(workType))
                return 0f;
            
            float score = PriorityAssigner.CalculateWorkTypeScore(pawn, workType);
            
            // Bonus for jobs with active work
            if (workZoneGrid != null)
            {
                float demand = workZoneGrid.GetDemand(workType);
                if (demand > 0)
                {
                    score += demand * 0.5f; // Demand bonus
                }
            }
            
            // Stability bias: bonus for current assignment
            if (pawn.workSettings != null && pawn.workSettings.GetPriority(workType) == 1)
            {
                score += score * STABILITY_BIAS; // 20% bonus
            }
            
            return score;
        }
        
        private static float CalculateTotalScore(Dictionary<Pawn, WorkTypeDef> assignments)
        {
            float total = 0f;
            foreach (var kvp in assignments)
            {
                total += PriorityAssigner.CalculateWorkTypeScore(kvp.Key, kvp.Value);
            }
            return total;
        }
        
        private static int GetCacheKey(List<Pawn> colonists)
        {
            // Simple hash based on colonist count and first/last IDs
            int hash = colonists.Count;
            if (colonists.Count > 0)
            {
                hash = hash * 31 + colonists[0].thingIDNumber;
                hash = hash * 31 + colonists[colonists.Count - 1].thingIDNumber;
            }
            return hash;
        }
        
        private static void InvalidateOldCache(int currentTick)
        {
            if (currentTick - lastCacheInvalidationTick < CACHE_LIFETIME)
                return;
            
            lastCacheInvalidationTick = currentTick;
            
            // Remove old entries
            var keysToRemove = ListPool<int>.Get();
            
            try
            {
                foreach (var kvp in cachedResults)
                {
                    if (currentTick - kvp.Value.computedAtTick >= CACHE_LIFETIME)
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                }
                
                foreach (var key in keysToRemove)
                {
                    cachedResults.Remove(key);
                }
            }
            finally
            {
                ListPool<int>.Return(keysToRemove);
            }
        }
        
        /// <summary>
        /// Clear assignment cache
        /// </summary>
        public static void ClearCache()
        {
            cachedResults.Clear();
        }
        
        /// <summary>
        /// Get cache statistics
        /// </summary>
        public static string GetStatistics()
        {
            return $"Cached assignments: {cachedResults.Count}";
        }
    }
}

