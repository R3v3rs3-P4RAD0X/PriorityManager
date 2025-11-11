using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using PriorityManager.Memory;

namespace PriorityManager.Assignment
{
    /// <summary>
    /// Detects and redirects idle colonists to high-demand jobs
    /// v2.0: Automatic idle handling with dynamic priority boosting
    /// </summary>
    public static class IdleRedirector
    {
        private static Dictionary<Pawn, IdleState> idleStates = new Dictionary<Pawn, IdleState>();
        private const int IDLE_THRESHOLD = 500; // Ticks before considered "prolonged idle"
        
        private class IdleState
        {
            public int idleSinceTick = 0;
            public bool isIdle = false;
            public WorkTypeDef boostedJob = null;
            public int originalPriority = 0;
        }
        
        /// <summary>
        /// Monitor colonist states and redirect idle ones
        /// </summary>
        public static void MonitorAndRedirect(List<Pawn> colonists, Spatial.WorkZoneGrid workZoneGrid)
        {
            using (PerformanceProfiler.Profile("IdleRedirector.MonitorAndRedirect"))
            {
                if (colonists == null || colonists.Count == 0 || workZoneGrid == null)
                    return;
                
                int currentTick = Find.TickManager.TicksGame;
                
                foreach (var pawn in colonists)
                {
                    if (pawn == null || pawn.Dead || pawn.workSettings == null)
                        continue;
                    
                    UpdateIdleState(pawn, currentTick);
                    
                    // If idle for too long, redirect
                    if (IsIdle(pawn) && GetIdleDuration(pawn) > IDLE_THRESHOLD)
                    {
                        RedirectToHighDemandJob(pawn, workZoneGrid, currentTick);
                    }
                }
                
                // Clean up old states
                CleanupStates(colonists);
            }
        }
        
        /// <summary>
        /// Check if colonist is currently idle
        /// </summary>
        public static bool IsIdle(Pawn pawn)
        {
            if (pawn?.CurJob == null)
                return true;
            
            JobDef curJobDef = pawn.CurJob.def;
            
            // Check for idle job types
            return curJobDef == JobDefOf.Wait_Wander ||
                   curJobDef == JobDefOf.Wait_Combat ||
                   curJobDef == JobDefOf.Wait_MaintainPosture ||
                   curJobDef == JobDefOf.GotoWander ||
                   curJobDef.defName.Contains("Idle") ||
                   curJobDef.defName.Contains("Wander");
        }
        
        /// <summary>
        /// Get how long colonist has been idle
        /// </summary>
        public static int GetIdleDuration(Pawn pawn)
        {
            if (idleStates.TryGetValue(pawn, out IdleState state) && state.isIdle)
            {
                return Find.TickManager.TicksGame - state.idleSinceTick;
            }
            return 0;
        }
        
        private static void UpdateIdleState(Pawn pawn, int currentTick)
        {
            bool currentlyIdle = IsIdle(pawn);
            
            if (!idleStates.TryGetValue(pawn, out IdleState state))
            {
                state = new IdleState();
                idleStates[pawn] = state;
            }
            
            // Idle state transition
            if (currentlyIdle && !state.isIdle)
            {
                // Just became idle
                state.isIdle = true;
                state.idleSinceTick = currentTick;
            }
            else if (!currentlyIdle && state.isIdle)
            {
                // No longer idle
                state.isIdle = false;
                
                // Remove priority boost if we applied one
                if (state.boostedJob != null)
                {
                    RevertPriorityBoost(pawn, state);
                }
            }
        }
        
        private static void RedirectToHighDemandJob(Pawn pawn, Spatial.WorkZoneGrid workZoneGrid, int currentTick)
        {
            if (!idleStates.TryGetValue(pawn, out IdleState state))
                return;
            
            // Already redirected recently?
            if (state.boostedJob != null)
                return;
            
            // Find jobs with high demand that colonist can do
            var activeWorkTypes = workZoneGrid.GetActiveWorkTypes();
            var candidates = ListPool<(WorkTypeDef, float)>.Get();
            
            try
            {
                foreach (var workType in activeWorkTypes)
                {
                    if (pawn.WorkTypeIsDisabled(workType))
                        continue;
                    
                    float demand = workZoneGrid.GetDemand(workType);
                    
                    // Calculate demand score adjusted for capability
                    float score = demand;
                    
                    // Boost if no current workers
                    int currentWorkers = CountCurrentWorkers(pawn.Map, workType);
                    if (currentWorkers == 0)
                    {
                        score *= 5f; // Huge boost for uncovered jobs
                    }
                    
                    // Boost if overloaded
                    if (currentWorkers > 0)
                    {
                        float workPerWorker = demand / currentWorkers;
                        if (workPerWorker > 10f)
                        {
                            score *= 2f; // Boost for overloaded jobs
                        }
                    }
                    
                    candidates.Add((workType, score));
                }
                
                if (candidates.Count == 0)
                    return; // No suitable work
                
                // Sort by score and pick best
                candidates.Sort((a, b) => b.Item2.CompareTo(a.Item2));
                var (bestJob, bestScore) = candidates[0];
                
                // Apply priority boost
                ApplyPriorityBoost(pawn, bestJob, state);
                
                Log.Message($"[IdleRedirector] {pawn.Name.ToStringShort} idle for {GetIdleDuration(pawn)} ticks, redirected to {bestJob.labelShort} (demand: {bestScore:F1})");
            }
            finally
            {
                ListPool<(WorkTypeDef, float)>.Return(candidates);
            }
        }
        
        private static void ApplyPriorityBoost(Pawn pawn, WorkTypeDef workType, IdleState state)
        {
            if (pawn.workSettings == null)
                return;
            
            // Store original priority
            state.originalPriority = pawn.workSettings.GetPriority(workType);
            state.boostedJob = workType;
            
            // Boost to priority 1 temporarily
            pawn.workSettings.SetPriority(workType, 1);
        }
        
        private static void RevertPriorityBoost(Pawn pawn, IdleState state)
        {
            if (pawn.workSettings == null || state.boostedJob == null)
                return;
            
            // Restore original priority
            pawn.workSettings.SetPriority(state.boostedJob, state.originalPriority);
            
            state.boostedJob = null;
            state.originalPriority = 0;
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
        
        private static int CountCurrentWorkers(Map map, WorkTypeDef workType)
        {
            int count = 0;
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return 0;
            
            var colonists = gameComp.GetAllColonists();
            foreach (var pawn in colonists)
            {
                if (pawn.workSettings != null && pawn.workSettings.GetPriority(workType) > 0)
                    count++;
            }
            return count;
        }
        
        private static void CleanupStates(List<Pawn> colonists)
        {
            // Remove states for pawns no longer in colony
            var toRemove = ListPool<Pawn>.Get();
            
            try
            {
                foreach (var pawn in idleStates.Keys)
                {
                    if (pawn == null || pawn.Dead || !colonists.Contains(pawn))
                    {
                        toRemove.Add(pawn);
                    }
                }
                
                foreach (var pawn in toRemove)
                {
                    idleStates.Remove(pawn);
                }
            }
            finally
            {
                ListPool<Pawn>.Return(toRemove);
            }
        }
        
        /// <summary>
        /// Get all currently idle colonists
        /// </summary>
        public static List<Pawn> GetIdleColonists()
        {
            var idle = ListPool<Pawn>.Get();
            
            foreach (var kvp in idleStates)
            {
                if (kvp.Value.isIdle && GetIdleDuration(kvp.Key) > IDLE_THRESHOLD)
                {
                    idle.Add(kvp.Key);
                }
            }
            
            return idle;
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public static string GetStatistics()
        {
            int totalTracked = idleStates.Count;
            int currentlyIdle = 0;
            int redirected = 0;
            
            foreach (var state in idleStates.Values)
            {
                if (state.isIdle)
                    currentlyIdle++;
                if (state.boostedJob != null)
                    redirected++;
            }
            
            return $"Tracked: {totalTracked}, Idle: {currentlyIdle}, Redirected: {redirected}";
        }
        
        /// <summary>
        /// Clear all idle states
        /// </summary>
        public static void Clear()
        {
            idleStates.Clear();
        }
    }
}

