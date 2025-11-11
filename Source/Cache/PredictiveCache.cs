using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager.Cache
{
    /// <summary>
    /// Learns common work patterns and pre-computes likely assignments
    /// v2.0: Predictive caching based on historical patterns
    /// </summary>
    public class PredictiveCache
    {
        private static PredictiveCache instance;
        
        // Historical data (sliding window)
        private List<WorkPattern> recentPatterns = new List<WorkPattern>();
        private const int MAX_HISTORY = 100; // Keep last 100 patterns
        
        // Learned patterns
        private Dictionary<int, WorkTypeDef> commonAssignments = new Dictionary<int, WorkTypeDef>(); // hour_of_day → common work
        private Dictionary<WorkTypeDef, float> jobFrequencies = new Dictionary<WorkTypeDef, float>();
        
        // Cached predictions
        private Dictionary<Pawn, WorkTypeDef> predictedAssignments = new Dictionary<Pawn, WorkTypeDef>();
        private int lastPredictionTick = 0;
        private const int PREDICTION_INTERVAL = 1000; // Update predictions every 1000 ticks
        
        private class WorkPattern
        {
            public int tick;
            public int hourOfDay;
            public Dictionary<WorkTypeDef, int> jobCounts = new Dictionary<WorkTypeDef, int>();
            public int totalColonists;
        }
        
        public static PredictiveCache Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new PredictiveCache();
                }
                return instance;
            }
        }
        
        private PredictiveCache()
        {
            Log.Message("[PriorityManager] PredictiveCache initialized");
        }
        
        /// <summary>
        /// Record current work pattern for learning
        /// </summary>
        public void RecordPattern()
        {
            using (PerformanceProfiler.Profile("PredictiveCache.RecordPattern"))
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                    return;
                
                int currentTick = Find.TickManager.TicksGame;
                int hourOfDay = GenDate.HourOfDay(currentTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
                
                var pattern = new WorkPattern
                {
                    tick = currentTick,
                    hourOfDay = hourOfDay,
                    totalColonists = gameComp.GetAllColonists().Count
                };
                
                // Count active jobs
                var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
                foreach (var workType in visibleWorkTypes)
                {
                    int workerCount = CountActiveWorkers(workType);
                    if (workerCount > 0)
                    {
                        pattern.jobCounts[workType] = workerCount;
                    }
                }
                
                // Add to history
                recentPatterns.Add(pattern);
                
                // Trim old patterns
                if (recentPatterns.Count > MAX_HISTORY)
                {
                    recentPatterns.RemoveAt(0);
                }
                
                // Update learned patterns
                LearnFromHistory();
            }
        }
        
        /// <summary>
        /// Get predicted assignment for a colonist
        /// </summary>
        public WorkTypeDef GetPrediction(Pawn pawn)
        {
            if (predictedAssignments.TryGetValue(pawn, out WorkTypeDef predicted))
            {
                return predicted;
            }
            return null;
        }
        
        /// <summary>
        /// Update predictions based on current state
        /// </summary>
        public void UpdatePredictions()
        {
            using (PerformanceProfiler.Profile("PredictiveCache.UpdatePredictions"))
            {
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastPredictionTick < PREDICTION_INTERVAL)
                    return;
                
                lastPredictionTick = currentTick;
                
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                    return;
                
                int hourOfDay = GenDate.HourOfDay(currentTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
                
                // Predict based on time of day patterns
                if (commonAssignments.TryGetValue(hourOfDay, out WorkTypeDef commonWork))
                {
                    var colonists = gameComp.GetAllColonists();
                    
                    foreach (var pawn in colonists)
                    {
                        if (!pawn.WorkTypeIsDisabled(commonWork))
                        {
                            predictedAssignments[pawn] = commonWork;
                        }
                    }
                }
            }
        }
        
        private void LearnFromHistory()
        {
            if (recentPatterns.Count < 10) // Need minimum data
                return;
            
            // Learn job frequencies
            jobFrequencies.Clear();
            var jobTotals = new Dictionary<WorkTypeDef, int>();
            int totalSamples = 0;
            
            foreach (var pattern in recentPatterns)
            {
                foreach (var kvp in pattern.jobCounts)
                {
                    if (!jobTotals.ContainsKey(kvp.Key))
                    {
                        jobTotals[kvp.Key] = 0;
                    }
                    jobTotals[kvp.Key] += kvp.Value;
                    totalSamples++;
                }
            }
            
            // Calculate frequencies
            foreach (var kvp in jobTotals)
            {
                jobFrequencies[kvp.Key] = kvp.Value / (float)totalSamples;
            }
            
            // Learn time-of-day patterns
            var hourlyPatterns = new Dictionary<int, Dictionary<WorkTypeDef, int>>();
            
            foreach (var pattern in recentPatterns)
            {
                int hour = pattern.hourOfDay;
                if (!hourlyPatterns.ContainsKey(hour))
                {
                    hourlyPatterns[hour] = new Dictionary<WorkTypeDef, int>();
                }
                
                foreach (var kvp in pattern.jobCounts)
                {
                    if (!hourlyPatterns[hour].ContainsKey(kvp.Key))
                    {
                        hourlyPatterns[hour][kvp.Key] = 0;
                    }
                    hourlyPatterns[hour][kvp.Key] += kvp.Value;
                }
            }
            
            // Find most common work per hour
            commonAssignments.Clear();
            foreach (var hourKvp in hourlyPatterns)
            {
                if (hourKvp.Value.Count > 0)
                {
                    var mostCommon = hourKvp.Value.OrderByDescending(kvp => kvp.Value).First();
                    commonAssignments[hourKvp.Key] = mostCommon.Key;
                }
            }
        }
        
        private int CountActiveWorkers(WorkTypeDef workType)
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return 0;
            
            int count = 0;
            var colonists = gameComp.GetAllColonists();
            foreach (var pawn in colonists)
            {
                if (pawn.workSettings != null && pawn.workSettings.GetPriority(workType) == 1)
                {
                    count++;
                }
            }
            return count;
        }
        
        /// <summary>
        /// Invalidate cache for specific event types
        /// </summary>
        public void Invalidate(InvalidationType type)
        {
            using (PerformanceProfiler.Profile("PredictiveCache.Invalidate"))
            {
                switch (type)
                {
                    case InvalidationType.ColonistAdded:
                    case InvalidationType.ColonistRemoved:
                        // Colony composition changed - invalidate all predictions
                        predictedAssignments.Clear();
                        break;
                        
                    case InvalidationType.SkillChanged:
                        // Skill changed - invalidate predictions but keep patterns
                        predictedAssignments.Clear();
                        break;
                        
                    case InvalidationType.SettingsChanged:
                        // Settings changed - clear everything
                        predictedAssignments.Clear();
                        recentPatterns.Clear();
                        commonAssignments.Clear();
                        jobFrequencies.Clear();
                        break;
                }
            }
        }
        
        /// <summary>
        /// Get most frequently needed jobs
        /// </summary>
        public List<WorkTypeDef> GetFrequentJobs(int count = 5)
        {
            return jobFrequencies
                .OrderByDescending(kvp => kvp.Value)
                .Take(count)
                .Select(kvp => kvp.Key)
                .ToList();
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            return $"Patterns: {recentPatterns.Count}/{MAX_HISTORY}, Predictions: {predictedAssignments.Count}, Learned hours: {commonAssignments.Count}";
        }
        
        /// <summary>
        /// Clear all cached data
        /// </summary>
        public void Clear()
        {
            recentPatterns.Clear();
            commonAssignments.Clear();
            jobFrequencies.Clear();
            predictedAssignments.Clear();
        }
    }
    
    /// <summary>
    /// Types of cache invalidation
    /// </summary>
    public enum InvalidationType
    {
        ColonistAdded,
        ColonistRemoved,
        SkillChanged,
        HealthChanged,
        SettingsChanged
    }
}

