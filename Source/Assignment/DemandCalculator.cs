using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using PriorityManager.Memory;

namespace PriorityManager.Assignment
{
    /// <summary>
    /// Calculates real-time work demand per job type
    /// v2.0: Demand-based worker scaling - more workers for busy jobs
    /// </summary>
    public static class DemandCalculator
    {
        /// <summary>
        /// Calculate demand score for each work type
        /// Score = (pending_work / worker_capacity) * urgency_multiplier
        /// </summary>
        public static Dictionary<WorkTypeDef, DemandScore> CalculateAllDemands(
            List<Pawn> colonists, Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("DemandCalculator.CalculateAll"))
            {
                var results = DictionaryPool<WorkTypeDef, DemandScore>.Get();
                
                if (workZoneGrid == null)
                    return results; // Empty
                
                var activeWorkTypes = workZoneGrid.GetActiveWorkTypes();
                
                foreach (var workType in activeWorkTypes)
                {
                    float rawDemand = workZoneGrid.GetDemand(workType);
                    int capableWorkers = CountCapableWorkers(colonists, workType);
                    int currentWorkers = CountCurrentWorkers(colonists, workType);
                    
                    var score = new DemandScore
                    {
                        workType = workType,
                        rawDemand = rawDemand,
                        capableWorkers = capableWorkers,
                        currentWorkers = currentWorkers,
                        urgency = GetUrgencyMultiplier(workType),
                        normalizedDemand = CalculateNormalizedDemand(rawDemand, currentWorkers, capableWorkers),
                        recommendedWorkers = CalculateRecommendedWorkers(rawDemand, capableWorkers, colonists.Count)
                    };
                    
                    results[workType] = score;
                }
                
                return results;
            }
        }
        
        /// <summary>
        /// Get demand score for a specific work type
        /// </summary>
        public static DemandScore GetDemand(WorkTypeDef workType, List<Pawn> colonists, 
            Spatial.WorkZoneGrid workZoneGrid)
        {
            if (workZoneGrid == null)
                return new DemandScore { workType = workType };
            
            float rawDemand = workZoneGrid.GetDemand(workType);
            int capableWorkers = CountCapableWorkers(colonists, workType);
            int currentWorkers = CountCurrentWorkers(colonists, workType);
            
            return new DemandScore
            {
                workType = workType,
                rawDemand = rawDemand,
                capableWorkers = capableWorkers,
                currentWorkers = currentWorkers,
                urgency = GetUrgencyMultiplier(workType),
                normalizedDemand = CalculateNormalizedDemand(rawDemand, currentWorkers, capableWorkers),
                recommendedWorkers = CalculateRecommendedWorkers(rawDemand, capableWorkers, colonists.Count)
            };
        }
        
        private static float CalculateNormalizedDemand(float rawDemand, int currentWorkers, int capableWorkers)
        {
            if (capableWorkers == 0)
                return 0f;
            
            // Normalize by current workers (higher = more overloaded)
            float workersToUse = currentWorkers > 0 ? currentWorkers : 1;
            return rawDemand / workersToUse;
        }
        
        private static int CalculateRecommendedWorkers(float rawDemand, int capableWorkers, int colonySize)
        {
            // Base recommendation on demand level
            if (rawDemand == 0)
                return 1; // Always at least 1 for coverage
            
            if (rawDemand < 5f)
                return 1; // Low demand
            
            if (rawDemand < 20f)
                return Math.Min(2, capableWorkers); // Medium demand
            
            if (rawDemand < 50f)
                return Math.Min(3, capableWorkers); // High demand
            
            // Very high demand - scale with colony size
            int recommended = (int)Math.Ceiling(rawDemand / 20f);
            return Math.Min(recommended, Math.Max(capableWorkers, colonySize / 3));
        }
        
        private static float GetUrgencyMultiplier(WorkTypeDef workType)
        {
            // Critical jobs get higher multiplier
            if (workType == WorkTypeDefOf.Doctor)
                return 3.0f;
            if (workType == DefDatabase<WorkTypeDef>.GetNamedSilentFail("Firefighter"))
                return 3.0f;
            if (workType == WorkTypeDefOf.Construction || workType == DefDatabase<WorkTypeDef>.GetNamedSilentFail("Repair"))
                return 1.5f;
            if (workType == WorkTypeDefOf.Growing || workType == WorkTypeDefOf.Hauling)
                return 1.3f;
            
            return 1.0f; // Default
        }
        
        private static int CountCapableWorkers(List<Pawn> colonists, WorkTypeDef workType)
        {
            int count = 0;
            foreach (var pawn in colonists)
            {
                if (!pawn.WorkTypeIsDisabled(workType))
                    count++;
            }
            return count;
        }
        
        private static int CountCurrentWorkers(List<Pawn> colonists, WorkTypeDef workType)
        {
            int count = 0;
            foreach (var pawn in colonists)
            {
                if (pawn.workSettings != null && pawn.workSettings.GetPriority(workType) > 0)
                    count++;
            }
            return count;
        }
        
        /// <summary>
        /// Get jobs sorted by demand (highest first)
        /// </summary>
        public static List<WorkTypeDef> GetJobsByDemand(Dictionary<WorkTypeDef, DemandScore> demands)
        {
            var sorted = demands.OrderByDescending(kvp => kvp.Value.normalizedDemand).ToList();
            return sorted.Select(kvp => kvp.Key).ToList();
        }
        
        /// <summary>
        /// Check if a job is overloaded (demand > capacity)
        /// </summary>
        public static bool IsOverloaded(DemandScore score)
        {
            return score.normalizedDemand > 10f; // Threshold for overloaded
        }
        
        /// <summary>
        /// Check if a job is underutilized (no demand but has workers)
        /// </summary>
        public static bool IsUnderutilized(DemandScore score)
        {
            return score.rawDemand == 0 && score.currentWorkers > 1;
        }
    }
    
    /// <summary>
    /// Demand score data structure
    /// </summary>
    public struct DemandScore
    {
        public WorkTypeDef workType;
        public float rawDemand;             // Raw urgency from WorkZoneGrid
        public int capableWorkers;          // Colonists who CAN do this work
        public int currentWorkers;          // Colonists currently assigned
        public float urgency;               // Multiplier for critical jobs
        public float normalizedDemand;      // demand / current_workers (overload indicator)
        public int recommendedWorkers;      // Suggested worker count
        
        public override string ToString()
        {
            return $"{workType?.labelShort ?? "Unknown"}: demand={rawDemand:F1}, workers={currentWorkers}/{capableWorkers}, norm={normalizedDemand:F1}, rec={recommendedWorkers}";
        }
    }
}

