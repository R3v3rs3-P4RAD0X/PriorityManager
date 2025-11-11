using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PriorityManager.Data
{
    /// <summary>
    /// High-performance colonist data cache using array indexing instead of dictionaries
    /// v2.0: O(1) lookups, cache-friendly memory layout
    /// </summary>
    public class ColonistDataCache
    {
        // Array-indexed storage (ThingID → array index)
        private ColonistCacheEntry[] entries;
        private Dictionary<int, int> thingIdToIndex; // ThingID → array index mapping
        private int nextIndex = 0;
        private const int INITIAL_CAPACITY = 100;
        
        // Pre-computed data
        private float[][] workScores;           // [colonistIdx][workTypeIdx]
        private int[][] skillRankings;          // [colonistIdx][skillIdx] = level
        private bool[][] workCapabilities;      // [colonistIdx][workTypeIdx] = canDo
        
        // Work type indexing
        private int workTypeCount;
        private int skillCount;
        
        // Statistics
        private int cacheHits = 0;
        private int cacheMisses = 0;
        
        public ColonistDataCache()
        {
            entries = new ColonistCacheEntry[INITIAL_CAPACITY];
            thingIdToIndex = new Dictionary<int, int>(INITIAL_CAPACITY);
            
            // Initialize work type arrays
            workTypeCount = WorkTypeCache.VisibleCount;
            skillCount = DefDatabase<SkillDef>.DefCount;
            
            workScores = new float[INITIAL_CAPACITY][];
            skillRankings = new int[INITIAL_CAPACITY][];
            workCapabilities = new bool[INITIAL_CAPACITY][];
            
            for (int i = 0; i < INITIAL_CAPACITY; i++)
            {
                workScores[i] = new float[workTypeCount];
                skillRankings[i] = new int[skillCount];
                workCapabilities[i] = new bool[workTypeCount];
            }
        }
        
        /// <summary>
        /// Get or create cache entry for a colonist
        /// </summary>
        public ColonistCacheEntry GetOrCreate(Pawn pawn)
        {
            if (pawn == null)
                return null;
            
            int thingId = pawn.thingIDNumber;
            
            // O(1) lookup
            if (thingIdToIndex.TryGetValue(thingId, out int index))
            {
                cacheHits++;
                return entries[index];
            }
            
            cacheMisses++;
            return CreateEntry(pawn);
        }
        
        private ColonistCacheEntry CreateEntry(Pawn pawn)
        {
            // Expand arrays if needed
            if (nextIndex >= entries.Length)
            {
                ExpandCapacity();
            }
            
            int index = nextIndex++;
            int thingId = pawn.thingIDNumber;
            
            var entry = new ColonistCacheEntry
            {
                pawn = pawn,
                thingId = thingId,
                cacheIndex = index,
                lastUpdateTick = Find.TickManager.TicksGame
            };
            
            entries[index] = entry;
            thingIdToIndex[thingId] = index;
            
            // Pre-compute data for this colonist
            ComputeSkillRankings(pawn, index);
            ComputeWorkCapabilities(pawn, index);
            
            return entry;
        }
        
        /// <summary>
        /// Remove colonist from cache
        /// </summary>
        public void Remove(Pawn pawn)
        {
            if (pawn == null)
                return;
            
            int thingId = pawn.thingIDNumber;
            if (thingIdToIndex.TryGetValue(thingId, out int index))
            {
                entries[index] = null;
                thingIdToIndex.Remove(thingId);
            }
        }
        
        /// <summary>
        /// Get pre-computed work score
        /// </summary>
        public float GetWorkScore(Pawn pawn, int workTypeIndex)
        {
            if (thingIdToIndex.TryGetValue(pawn.thingIDNumber, out int colonistIndex))
            {
                return workScores[colonistIndex][workTypeIndex];
            }
            return 0f;
        }
        
        /// <summary>
        /// Set computed work score (for caching)
        /// </summary>
        public void SetWorkScore(Pawn pawn, int workTypeIndex, float score)
        {
            if (thingIdToIndex.TryGetValue(pawn.thingIDNumber, out int colonistIndex))
            {
                workScores[colonistIndex][workTypeIndex] = score;
            }
        }
        
        /// <summary>
        /// Check if colonist can do work type (cached)
        /// </summary>
        public bool CanDoWork(Pawn pawn, int workTypeIndex)
        {
            if (thingIdToIndex.TryGetValue(pawn.thingIDNumber, out int colonistIndex))
            {
                return workCapabilities[colonistIndex][workTypeIndex];
            }
            return false;
        }
        
        /// <summary>
        /// Get skill level (cached)
        /// </summary>
        public int GetSkillLevel(Pawn pawn, int skillIndex)
        {
            if (thingIdToIndex.TryGetValue(pawn.thingIDNumber, out int colonistIndex))
            {
                return skillRankings[colonistIndex][skillIndex];
            }
            return 0;
        }
        
        /// <summary>
        /// Invalidate cached data for a colonist (skill changed, etc.)
        /// </summary>
        public void Invalidate(Pawn pawn)
        {
            if (pawn == null)
                return;
            
            if (thingIdToIndex.TryGetValue(pawn.thingIDNumber, out int index))
            {
                ComputeSkillRankings(pawn, index);
                ComputeWorkCapabilities(pawn, index);
                
                // Clear work scores (will be recomputed on demand)
                for (int i = 0; i < workTypeCount; i++)
                {
                    workScores[index][i] = 0f;
                }
            }
        }
        
        private void ComputeSkillRankings(Pawn pawn, int colonistIndex)
        {
            if (pawn.skills == null)
                return;
            
            var allSkills = DefDatabase<SkillDef>.AllDefsListForReading;
            for (int i = 0; i < allSkills.Count; i++)
            {
                var skill = pawn.skills.GetSkill(allSkills[i]);
                skillRankings[colonistIndex][i] = skill != null ? skill.Level : 0;
            }
        }
        
        private void ComputeWorkCapabilities(Pawn pawn, int colonistIndex)
        {
            var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
            for (int i = 0; i < visibleWorkTypes.Count; i++)
            {
                workCapabilities[colonistIndex][i] = !pawn.WorkTypeIsDisabled(visibleWorkTypes[i]);
            }
        }
        
        private void ExpandCapacity()
        {
            int newCapacity = entries.Length * 2;
            
            // Expand main entries array
            Array.Resize(ref entries, newCapacity);
            
            // Expand pre-computed arrays
            Array.Resize(ref workScores, newCapacity);
            Array.Resize(ref skillRankings, newCapacity);
            Array.Resize(ref workCapabilities, newCapacity);
            
            // Initialize new slots
            for (int i = entries.Length / 2; i < newCapacity; i++)
            {
                workScores[i] = new float[workTypeCount];
                skillRankings[i] = new int[skillCount];
                workCapabilities[i] = new bool[workTypeCount];
            }
        }
        
        /// <summary>
        /// Get all cached colonists
        /// </summary>
        public List<ColonistCacheEntry> GetAll()
        {
            var result = new List<ColonistCacheEntry>(nextIndex);
            for (int i = 0; i < nextIndex; i++)
            {
                if (entries[i] != null && entries[i].pawn != null)
                {
                    result.Add(entries[i]);
                }
            }
            return result;
        }
        
        /// <summary>
        /// Clear entire cache
        /// </summary>
        public void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
            thingIdToIndex.Clear();
            nextIndex = 0;
            cacheHits = 0;
            cacheMisses = 0;
        }
        
        /// <summary>
        /// Get cache statistics
        /// </summary>
        public string GetStatistics()
        {
            int total = cacheHits + cacheMisses;
            float hitRate = total > 0 ? (cacheHits / (float)total) * 100f : 0f;
            return $"Entries: {nextIndex}, Hits: {cacheHits}, Misses: {cacheMisses}, Hit Rate: {hitRate:F1}%";
        }
    }
    
    /// <summary>
    /// Cache entry for a single colonist
    /// </summary>
    public class ColonistCacheEntry
    {
        public Pawn pawn;
        public int thingId;
        public int cacheIndex;
        public int lastUpdateTick;
        
        // Cached colonist-specific data (from PriorityData)
        public ColonistRoleData roleData;
        
        public bool IsValid()
        {
            return pawn != null && !pawn.Dead && !pawn.Destroyed;
        }
    }
}

