using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PriorityManager
{
    /// <summary>
    /// v2.0: Caches WorkTypeDef queries to avoid repeated DefDatabase lookups
    /// </summary>
    public static class WorkTypeCache
    {
        private static List<WorkTypeDef> allWorkTypes = null;
        private static List<WorkTypeDef> visibleWorkTypes = null;
        private static Dictionary<string, WorkTypeDef> workTypesByDefName = null;
        private static Dictionary<WorkTypeDef, List<SkillDef>> relevantSkillsCache = null;
        private static bool initialized = false;
        
        /// <summary>
        /// Initialize or refresh cache. Call after defs are loaded or when mods change work types.
        /// </summary>
        public static void Initialize()
        {
            using (PerformanceProfiler.Profile("WorkTypeCache.Initialize"))
            {
                // Get all work types
                allWorkTypes = new List<WorkTypeDef>(DefDatabase<WorkTypeDef>.AllDefsListForReading);
                
                // Cache visible work types
                visibleWorkTypes = new List<WorkTypeDef>(allWorkTypes.Count);
                workTypesByDefName = new Dictionary<string, WorkTypeDef>(allWorkTypes.Count);
                relevantSkillsCache = new Dictionary<WorkTypeDef, List<SkillDef>>(allWorkTypes.Count);
                
                foreach (var workType in allWorkTypes)
                {
                    workTypesByDefName[workType.defName] = workType;
                    
                    if (workType.visible)
                    {
                        visibleWorkTypes.Add(workType);
                    }
                    
                    // Cache relevant skills
                    if (workType.relevantSkills != null && workType.relevantSkills.Count > 0)
                    {
                        relevantSkillsCache[workType] = new List<SkillDef>(workType.relevantSkills);
                    }
                    else
                    {
                        relevantSkillsCache[workType] = new List<SkillDef>();
                    }
                }
                
                initialized = true;
                Log.Message($"[PriorityManager] WorkTypeCache initialized: {allWorkTypes.Count} total, {visibleWorkTypes.Count} visible");
            }
        }
        
        /// <summary>
        /// Get all work types (cached)
        /// </summary>
        public static List<WorkTypeDef> AllWorkTypes
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }
                return allWorkTypes;
            }
        }
        
        /// <summary>
        /// Get visible work types (cached)
        /// </summary>
        public static List<WorkTypeDef> VisibleWorkTypes
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }
                return visibleWorkTypes;
            }
        }
        
        /// <summary>
        /// Get work type by def name (O(1) lookup)
        /// </summary>
        public static WorkTypeDef GetByDefName(string defName)
        {
            if (!initialized)
            {
                Initialize();
            }
            
            workTypesByDefName.TryGetValue(defName, out WorkTypeDef result);
            return result;
        }
        
        /// <summary>
        /// Get relevant skills for a work type (cached)
        /// </summary>
        public static List<SkillDef> GetRelevantSkills(WorkTypeDef workType)
        {
            if (!initialized)
            {
                Initialize();
            }
            
            if (relevantSkillsCache.TryGetValue(workType, out List<SkillDef> skills))
            {
                return skills;
            }
            
            return new List<SkillDef>();
        }
        
        /// <summary>
        /// Check if work type is visible
        /// </summary>
        public static bool IsVisible(WorkTypeDef workType)
        {
            if (!initialized)
            {
                Initialize();
            }
            
            return visibleWorkTypes.Contains(workType);
        }
        
        /// <summary>
        /// Get count of visible work types
        /// </summary>
        public static int VisibleCount
        {
            get
            {
                if (!initialized)
                {
                    Initialize();
                }
                return visibleWorkTypes.Count;
            }
        }
        
        /// <summary>
        /// Clear cache (call when mods are loaded/unloaded)
        /// </summary>
        public static void Clear()
        {
            allWorkTypes = null;
            visibleWorkTypes = null;
            workTypesByDefName = null;
            relevantSkillsCache = null;
            initialized = false;
        }
        
        /// <summary>
        /// Check if a pawn can do a work type (with caching potential)
        /// </summary>
        public static bool PawnCanDoWorkType(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn.WorkTypeIsDisabled(workType))
                return false;
            
            if (pawn.skills == null)
                return false;
            
            return true;
        }
    }
}

