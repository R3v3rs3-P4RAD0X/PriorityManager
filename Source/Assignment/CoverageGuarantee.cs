using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using PriorityManager.Memory;

namespace PriorityManager.Assignment
{
    /// <summary>
    /// Ensures every job type has at least one worker, even if unskilled
    /// Then scales workers based on actual work demand
    /// v2.0: User requirement - all jobs covered + demand-based scaling
    /// </summary>
    public static class CoverageGuarantee
    {
        /// <summary>
        /// Assign workers to ensure all jobs are covered
        /// Phase 1: Coverage (1 worker per job minimum)
        /// Phase 2: Scaling (add workers based on demand)
        /// </summary>
        public static void EnsureCoverage(List<Pawn> colonists, Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("CoverageGuarantee.EnsureCoverage"))
            {
                if (colonists == null || colonists.Count == 0)
                    return;
                
                var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
                var settings = PriorityManagerMod.settings;
                
                // Use pooled collections for zero allocation
                var assignments = DictionaryPool<WorkTypeDef, List<Pawn>>.Get();
                var unassignedWork = ListPool<WorkTypeDef>.Get();
                
                try
                {
                    // Initialize assignments
                    foreach (var workType in visibleWorkTypes)
                    {
                        assignments[workType] = ListPool<Pawn>.Get();
                    }
                    
                    // Phase 1: Coverage - assign 1 colonist to each job
                    AssignCoverage(colonists, visibleWorkTypes, assignments, unassignedWork);
                    
                    // Phase 2: Scaling - add workers based on demand
                    if (workZoneGrid != null)
                    {
                        ScaleByDemand(colonists, visibleWorkTypes, assignments, workZoneGrid);
                    }
                    
                    // Phase 3: Handle idle colonists - assign to high-demand jobs
                    AssignIdleColonists(colonists, assignments, workZoneGrid);
                    
                    Log.Message($"[CoverageGuarantee] Coverage complete: {assignments.Count} jobs covered, {unassignedWork.Count} uncoverable");
                }
                finally
                {
                    // Return all pooled objects
                    foreach (var list in assignments.Values)
                    {
                        ListPool<Pawn>.Return(list);
                    }
                    DictionaryPool<WorkTypeDef, List<Pawn>>.Return(assignments);
                    ListPool<WorkTypeDef>.Return(unassignedWork);
                }
            }
        }
        
        /// <summary>
        /// Phase 1: Assign at least one colonist to each job type
        /// </summary>
        private static void AssignCoverage(List<Pawn> colonists, List<WorkTypeDef> workTypes, 
            Dictionary<WorkTypeDef, List<Pawn>> assignments, List<WorkTypeDef> unassignedWork)
        {
            using (PerformanceProfiler.Profile("CoverageGuarantee.AssignCoverage"))
            {
                var settings = PriorityManagerMod.settings;
                
                // For each work type, find best available colonist
                foreach (var workType in workTypes)
                {
                    // Skip always-enabled jobs (handled separately)
                    if (settings.IsJobAlwaysEnabled(workType))
                        continue;
                    
                    // Find colonists who can do this work
                    Pawn bestColonist = null;
                    float bestScore = float.MinValue;
                    
                    foreach (var pawn in colonists)
                    {
                        if (pawn.WorkTypeIsDisabled(workType))
                            continue;
                        
                        // Calculate "coverage penalty" - lower is better for balanced distribution
                        int currentJobs = CountAssignedJobs(pawn, assignments);
                        float score = PriorityAssigner.CalculateWorkTypeScore(pawn, workType);
                        
                        // Penalize colonists who already have many jobs
                        float penalty = currentJobs * 5f;
                        score -= penalty;
                        
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestColonist = pawn;
                        }
                    }
                    
                    // Assign best colonist (or track as uncoverable)
                    if (bestColonist != null)
                    {
                        assignments[workType].Add(bestColonist);
                        PriorityAssigner.SetPriority(bestColonist, workType, 3); // Priority 3 for coverage
                    }
                    else
                    {
                        unassignedWork.Add(workType);
                        Log.Warning($"[CoverageGuarantee] No colonist can do {workType.labelShort} - job uncovered!");
                    }
                }
            }
        }
        
        /// <summary>
        /// Phase 2: Scale worker count based on actual work demand
        /// </summary>
        private static void ScaleByDemand(List<Pawn> colonists, List<WorkTypeDef> workTypes,
            Dictionary<WorkTypeDef, List<Pawn>> assignments, Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("CoverageGuarantee.ScaleByDemand"))
            {
                // Calculate demand for each job type
                var demands = DictionaryPool<WorkTypeDef, float>.Get();
                
                try
                {
                    float totalDemand = 0f;
                    foreach (var workType in workTypes)
                    {
                        float demand = workZoneGrid.GetDemand(workType);
                        demands[workType] = demand;
                        totalDemand += demand;
                    }
                    
                    if (totalDemand == 0)
                        return; // No work to do
                    
                    // Calculate optimal worker count per job based on demand
                    foreach (var workType in workTypes)
                    {
                        float demand = demands[workType];
                        if (demand <= 0)
                            continue;
                        
                        // Calculate target workers: proportional to demand
                        float demandRatio = demand / totalDemand;
                        int targetWorkers = Math.Max(1, (int)Math.Ceiling(demandRatio * colonists.Count));
                        
                        // Current workers assigned to this job
                        int currentWorkers = assignments[workType].Count;
                        
                        // Add more workers if needed
                        if (currentWorkers < targetWorkers)
                        {
                            int workersNeeded = targetWorkers - currentWorkers;
                            AddWorkers(colonists, workType, workersNeeded, assignments);
                        }
                    }
                }
                finally
                {
                    DictionaryPool<WorkTypeDef, float>.Return(demands);
                }
            }
        }
        
        /// <summary>
        /// Add additional workers to a job based on demand
        /// </summary>
        private static void AddWorkers(List<Pawn> colonists, WorkTypeDef workType, int count,
            Dictionary<WorkTypeDef, List<Pawn>> assignments)
        {
            var alreadyAssigned = new HashSet<Pawn>(assignments[workType]);
            
            // Find best available colonists not yet assigned to this job
            var candidates = ListPool<(Pawn pawn, float score)>.Get();
            
            try
            {
                foreach (var pawn in colonists)
                {
                    if (alreadyAssigned.Contains(pawn))
                        continue;
                    
                    if (pawn.WorkTypeIsDisabled(workType))
                        continue;
                    
                    float score = PriorityAssigner.CalculateWorkTypeScore(pawn, workType);
                    candidates.Add((pawn, score));
                }
                
                // Sort by score descending
                candidates.Sort((a, b) => b.score.CompareTo(a.score));
                
                // Assign top candidates
                int assigned = 0;
                foreach (var (pawn, score) in candidates)
                {
                    if (assigned >= count)
                        break;
                    
                    assignments[workType].Add(pawn);
                    PriorityAssigner.SetPriority(pawn, workType, 2); // Priority 2 for demand-based
                    assigned++;
                }
            }
            finally
            {
                ListPool<(Pawn, float)>.Return(candidates);
            }
        }
        
        /// <summary>
        /// Phase 3: Assign idle colonists to high-demand jobs
        /// </summary>
        private static void AssignIdleColonists(List<Pawn> colonists, 
            Dictionary<WorkTypeDef, List<Pawn>> assignments, Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("CoverageGuarantee.AssignIdleColonists"))
            {
                if (workZoneGrid == null)
                    return;
                
                // Find idle colonists (those with very few job assignments)
                var idleColonists = ListPool<Pawn>.Get();
                
                try
                {
                    foreach (var pawn in colonists)
                    {
                        int assignedJobs = CountAssignedJobs(pawn, assignments);
                        int totalJobs = WorkTypeCache.VisibleCount;
                        
                        // Consider idle if assigned to less than 30% of jobs
                        if (assignedJobs < totalJobs * 0.3f)
                        {
                            idleColonists.Add(pawn);
                        }
                    }
                    
                    if (idleColonists.Count == 0)
                        return;
                    
                    // Find high-demand jobs
                    var activeWorkTypes = workZoneGrid.GetActiveWorkTypes();
                    var highDemandJobs = ListPool<(WorkTypeDef, float)>.Get();
                    
                    try
                    {
                        foreach (var workType in activeWorkTypes)
                        {
                            float demand = workZoneGrid.GetDemand(workType);
                            if (demand > 5f) // Threshold for "high demand"
                            {
                                highDemandJobs.Add((workType, demand));
                            }
                        }
                        
                        // Sort by demand descending
                        highDemandJobs.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                        
                        // Assign idle colonists to high-demand jobs
                        foreach (var pawn in idleColonists)
                        {
                            foreach (var (workType, demand) in highDemandJobs)
                            {
                                if (!pawn.WorkTypeIsDisabled(workType) && !assignments[workType].Contains(pawn))
                                {
                                    assignments[workType].Add(pawn);
                                    PriorityAssigner.SetPriority(pawn, workType, 3); // Priority 3 for idle assignment
                                    break; // One job per idle colonist
                                }
                            }
                        }
                    }
                    finally
                    {
                        ListPool<(WorkTypeDef, float)>.Return(highDemandJobs);
                    }
                }
                finally
                {
                    ListPool<Pawn>.Return(idleColonists);
                }
            }
        }
        
        private static int CountAssignedJobs(Pawn pawn, Dictionary<WorkTypeDef, List<Pawn>> assignments)
        {
            int count = 0;
            foreach (var list in assignments.Values)
            {
                if (list.Contains(pawn))
                    count++;
            }
            return count;
        }
    }
}

