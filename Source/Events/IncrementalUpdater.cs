using System;
using System.Collections.Generic;
using Verse;

namespace PriorityManager.Events
{
    /// <summary>
    /// Manages incremental updates - only recalculates colonists marked as dirty
    /// </summary>
    public class IncrementalUpdater
    {
        private static IncrementalUpdater instance;
        
        // Dirty tracking
        private HashSet<Pawn> dirtyColonists = new HashSet<Pawn>();
        private HashSet<Pawn> criticalColonists = new HashSet<Pawn>(); // Need immediate update
        
        // Batch processing
        private const int MAX_UPDATES_PER_TICK = 10; // Spread updates across multiple ticks
        private int updatesThisTick = 0;
        
        // Statistics
        private int totalUpdates = 0;
        private int batchedUpdates = 0;
        private int immediateUpdates = 0;
        
        public static IncrementalUpdater Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new IncrementalUpdater();
                }
                return instance;
            }
        }
        
        private IncrementalUpdater()
        {
            // Subscribe to relevant events
            EventDispatcher.Instance.Subscribe<ColonistAddedEvent>(OnColonistAdded);
            EventDispatcher.Instance.Subscribe<ColonistRemovedEvent>(OnColonistRemoved);
            EventDispatcher.Instance.Subscribe<HealthChangedEvent>(OnHealthChanged);
            EventDispatcher.Instance.Subscribe<SkillChangedEvent>(OnSkillChanged);
            EventDispatcher.Instance.Subscribe<ColonistIdleEvent>(OnColonistIdle);
            EventDispatcher.Instance.Subscribe<RoleChangedEvent>(OnRoleChanged);
            EventDispatcher.Instance.Subscribe<RecalculateRequestEvent>(OnRecalculateRequest);
            
            Log.Message("[PriorityManager] IncrementalUpdater initialized");
        }
        
        /// <summary>
        /// Mark a colonist as needing update
        /// </summary>
        public void MarkDirty(Pawn pawn, bool critical = false)
        {
            if (pawn == null)
                return;
            
            if (critical)
            {
                criticalColonists.Add(pawn);
            }
            else
            {
                dirtyColonists.Add(pawn);
            }
        }
        
        /// <summary>
        /// Process dirty colonists (call once per tick)
        /// </summary>
        public void ProcessDirtyColonists()
        {
            using (PerformanceProfiler.Profile("IncrementalUpdater.Process"))
            {
                updatesThisTick = 0;
                
                // Process critical colonists first (always immediate)
                while (criticalColonists.Count > 0 && updatesThisTick < MAX_UPDATES_PER_TICK)
                {
                    var pawn = GetNextPawn(criticalColonists);
                    if (pawn != null)
                    {
                        UpdateColonist(pawn, immediate: true);
                        immediateUpdates++;
                    }
                }
                
                // Process dirty colonists (batched)
                while (dirtyColonists.Count > 0 && updatesThisTick < MAX_UPDATES_PER_TICK)
                {
                    var pawn = GetNextPawn(dirtyColonists);
                    if (pawn != null)
                    {
                        UpdateColonist(pawn, immediate: false);
                        batchedUpdates++;
                    }
                }
            }
        }
        
        private Pawn GetNextPawn(HashSet<Pawn> set)
        {
            foreach (var pawn in set)
            {
                set.Remove(pawn);
                return pawn;
            }
            return null;
        }
        
        private void UpdateColonist(Pawn pawn, bool immediate)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed)
                return;
            
            updatesThisTick++;
            totalUpdates++;
            
            // Perform actual priority assignment
            PriorityAssigner.AssignPriorities(pawn, force: immediate);
        }
        
        // ============================================================================
        // EVENT HANDLERS
        // ============================================================================
        
        private void OnColonistAdded(ColonistAddedEvent evt)
        {
            // New colonist - immediate update
            MarkDirty(evt.pawn, critical: true);
        }
        
        private void OnColonistRemoved(ColonistRemovedEvent evt)
        {
            // Remove from dirty sets if present
            dirtyColonists.Remove(evt.pawn);
            criticalColonists.Remove(evt.pawn);
            
            // Trigger colony-wide recalculation (distribution may change)
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp != null)
            {
                var allColonists = gameComp.GetAllColonists();
                foreach (var pawn in allColonists)
                {
                    MarkDirty(pawn, critical: false);
                }
            }
        }
        
        private void OnHealthChanged(HealthChangedEvent evt)
        {
            // Health change - immediate if became ill, batched if recovered
            bool critical = evt.becameIll;
            MarkDirty(evt.pawn, critical: critical);
        }
        
        private void OnSkillChanged(SkillChangedEvent evt)
        {
            // Skill change - batched update (not urgent)
            MarkDirty(evt.pawn, critical: false);
        }
        
        private void OnColonistIdle(ColonistIdleEvent evt)
        {
            // Idle colonist - batched update
            MarkDirty(evt.pawn, critical: false);
        }
        
        private void OnRoleChanged(RoleChangedEvent evt)
        {
            // Role change - immediate update
            MarkDirty(evt.pawn, critical: true);
        }
        
        private void OnRecalculateRequest(RecalculateRequestEvent evt)
        {
            if (evt.specificPawn != null)
            {
                // Single colonist recalculate
                MarkDirty(evt.specificPawn, critical: evt.force);
            }
            else
            {
                // All colonists recalculate
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp != null)
                {
                    var allColonists = gameComp.GetAllColonists();
                    foreach (var pawn in allColonists)
                    {
                        MarkDirty(pawn, critical: evt.force);
                    }
                }
            }
        }
        
        /// <summary>
        /// Get current dirty colonist count
        /// </summary>
        public int GetDirtyCount()
        {
            return dirtyColonists.Count + criticalColonists.Count;
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            return $"Total: {totalUpdates}, Batched: {batchedUpdates}, Immediate: {immediateUpdates}, Pending: {GetDirtyCount()}";
        }
        
        /// <summary>
        /// Clear all dirty flags
        /// </summary>
        public void ClearDirty()
        {
            dirtyColonists.Clear();
            criticalColonists.Clear();
        }
        
        /// <summary>
        /// Reset statistics
        /// </summary>
        public void ResetStatistics()
        {
            totalUpdates = 0;
            batchedUpdates = 0;
            immediateUpdates = 0;
        }
    }
}

