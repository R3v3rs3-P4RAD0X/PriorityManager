using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager.Scheduling
{
    /// <summary>
    /// Time-of-day and seasonal priority adjustments
    /// v2.0: Shift scheduling, seasonal work priorities, emergency modes
    /// </summary>
    public class ShiftScheduler
    {
        private static ShiftScheduler instance;
        
        // Shift definitions
        private Dictionary<string, Shift> shifts = new Dictionary<string, Shift>();
        private Dictionary<Pawn, string> colonistShifts = new Dictionary<Pawn, string>();
        
        // Emergency mode
        private bool emergencyMode = false;
        private EmergencyType currentEmergency = EmergencyType.None;
        private int emergencyStartTick = 0;
        
        // Season tracking
        private Season currentSeason = Season.Undefined;
        private Dictionary<Season, List<WorkTypeDef>> seasonalPriorities = new Dictionary<Season, List<WorkTypeDef>>();
        
        public enum EmergencyType
        {
            None,
            Raid,
            Fire,
            Epidemic,
            Famine,
            ToxicFallout
        }
        
        public class Shift
        {
            public string name;
            public int startHour;
            public int endHour;
            public Dictionary<WorkTypeDef, int> priorityAdjustments = new Dictionary<WorkTypeDef, int>();
            
            public bool IsActive(int currentHour)
            {
                if (startHour < endHour)
                {
                    return currentHour >= startHour && currentHour < endHour;
                }
                else // Wraps midnight
                {
                    return currentHour >= startHour || currentHour < endHour;
                }
            }
        }
        
        public static ShiftScheduler Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ShiftScheduler();
                }
                return instance;
            }
        }
        
        private ShiftScheduler()
        {
            InitializeDefaultShifts();
            InitializeSeasonalPriorities();
            Log.Message("[PriorityManager] ShiftScheduler initialized");
        }
        
        private void InitializeDefaultShifts()
        {
            // Day shift (6 AM - 10 PM)
            var dayShift = new Shift
            {
                name = "Day",
                startHour = 6,
                endHour = 22
            };
            dayShift.priorityAdjustments[WorkTypeDefOf.Construction] = -1; // Boost construction
            dayShift.priorityAdjustments[WorkTypeDefOf.Growing] = -1; // Boost farming
            dayShift.priorityAdjustments[WorkTypeDefOf.Mining] = -1; // Boost mining
            shifts["day"] = dayShift;
            
            // Night shift (10 PM - 6 AM)
            var nightShift = new Shift
            {
                name = "Night",
                startHour = 22,
                endHour = 6
            };
            nightShift.priorityAdjustments[WorkTypeDefOf.Doctor] = -1; // Boost medical
            nightShift.priorityAdjustments[WorkTypeDefOf.Research] = -1; // Boost research
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            if (cooking != null)
                nightShift.priorityAdjustments[cooking] = -1; // Boost cooking
            shifts["night"] = nightShift;
        }
        
        private void InitializeSeasonalPriorities()
        {
            // Spring: Growing focus
            seasonalPriorities[Season.Spring] = new List<WorkTypeDef>
            {
                WorkTypeDefOf.Growing,
                WorkTypeDefOf.Construction
            };
            
            // Summer: Harvest and hauling
            seasonalPriorities[Season.Summer] = new List<WorkTypeDef>
            {
                WorkTypeDefOf.Growing,
                WorkTypeDefOf.Hauling
            };
            
            // Fall: Heavy harvest
            seasonalPriorities[Season.Fall] = new List<WorkTypeDef>
            {
                WorkTypeDefOf.Growing,
                WorkTypeDefOf.Hauling,
                DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking")
            };
            
            // Winter: Indoor work
            seasonalPriorities[Season.Winter] = new List<WorkTypeDef>
            {
                WorkTypeDefOf.Crafting,
                WorkTypeDefOf.Research,
                WorkTypeDefOf.Smithing
            };
        }
        
        /// <summary>
        /// Apply time-based priority adjustments
        /// </summary>
        public void ApplyTimeBasedAdjustments()
        {
            using (PerformanceProfiler.Profile("ShiftScheduler.ApplyAdjustments"))
            {
                int currentHour = GetCurrentHour();
                Season season = GetCurrentSeason();
                
                // Check for season change
                if (season != currentSeason)
                {
                    currentSeason = season;
                    OnSeasonChanged(season);
                }
                
                // Apply shift-based adjustments
                foreach (var kvp in colonistShifts)
                {
                    Pawn pawn = kvp.Key;
                    string shiftName = kvp.Value;
                    
                    if (shifts.TryGetValue(shiftName, out Shift shift))
                    {
                        if (shift.IsActive(currentHour))
                        {
                            ApplyShiftPriorities(pawn, shift);
                        }
                    }
                }
                
                // Apply emergency mode if active
                if (emergencyMode)
                {
                    ApplyEmergencyPriorities();
                }
            }
        }
        
        private void ApplyShiftPriorities(Pawn pawn, Shift shift)
        {
            if (pawn.workSettings == null)
                return;
            
            foreach (var kvp in shift.priorityAdjustments)
            {
                WorkTypeDef workType = kvp.Key;
                int adjustment = kvp.Value;
                
                if (workType == null || pawn.WorkTypeIsDisabled(workType))
                    continue;
                
                int currentPriority = pawn.workSettings.GetPriority(workType);
                if (currentPriority > 0)
                {
                    int newPriority = Mathf.Clamp(currentPriority + adjustment, 1, 4);
                    pawn.workSettings.SetPriority(workType, newPriority);
                }
            }
        }
        
        /// <summary>
        /// Activate emergency mode (all colonists focus on crisis)
        /// </summary>
        public void ActivateEmergencyMode(EmergencyType type)
        {
            emergencyMode = true;
            currentEmergency = type;
            emergencyStartTick = Find.TickManager.TicksGame;
            
            Log.Message($"[ShiftScheduler] Emergency mode activated: {type}");
            Messages.Message($"Emergency: {type} - All colonists redirected to crisis response!", MessageTypeDefOf.ThreatBig);
        }
        
        /// <summary>
        /// Deactivate emergency mode
        /// </summary>
        public void DeactivateEmergencyMode()
        {
            if (!emergencyMode)
                return;
            
            emergencyMode = false;
            currentEmergency = EmergencyType.None;
            
            Log.Message("[ShiftScheduler] Emergency mode deactivated");
            Messages.Message("Emergency resolved - Resuming normal operations", MessageTypeDefOf.PositiveEvent);
            
            // Trigger recalculation
            Events.EventDispatcher.Instance.Dispatch(new Events.RecalculateRequestEvent(force: true));
        }
        
        private void ApplyEmergencyPriorities()
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;
            
            var colonists = gameComp.GetAllColonists();
            
            switch (currentEmergency)
            {
                case EmergencyType.Fire:
                    ApplyFireEmergency(colonists);
                    break;
                    
                case EmergencyType.Raid:
                    ApplyRaidEmergency(colonists);
                    break;
                    
                case EmergencyType.Epidemic:
                    ApplyEpidemicEmergency(colonists);
                    break;
                    
                case EmergencyType.Famine:
                    ApplyFamineEmergency(colonists);
                    break;
            }
        }
        
        private void ApplyFireEmergency(List<Pawn> colonists)
        {
            var firefighter = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Firefighter");
            if (firefighter == null)
                return;
            
            foreach (var pawn in colonists)
            {
                if (!pawn.WorkTypeIsDisabled(firefighter))
                {
                    pawn.workSettings.SetPriority(firefighter, 1);
                }
            }
        }
        
        private void ApplyRaidEmergency(List<Pawn> colonists)
        {
            // All combat-capable to defense positions
            // Doctors on standby
            foreach (var pawn in colonists)
            {
                if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                {
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
                }
            }
        }
        
        private void ApplyEpidemicEmergency(List<Pawn> colonists)
        {
            // All doctors working, reduce other work
            foreach (var pawn in colonists)
            {
                if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                {
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
                }
            }
        }
        
        private void ApplyFamineEmergency(List<Pawn> colonists)
        {
            // Focus on food production
            var cooking = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
            
            foreach (var pawn in colonists)
            {
                if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Growing))
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Growing, 1);
                
                if (!pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hunting))
                    pawn.workSettings.SetPriority(WorkTypeDefOf.Hunting, 1);
                
                if (cooking != null && !pawn.WorkTypeIsDisabled(cooking))
                    pawn.workSettings.SetPriority(cooking, 1);
            }
        }
        
        private void OnSeasonChanged(Season newSeason)
        {
            Log.Message($"[ShiftScheduler] Season changed to {newSeason}");
            
            if (seasonalPriorities.TryGetValue(newSeason, out List<WorkTypeDef> priorities))
            {
                Messages.Message($"Season: {newSeason} - Prioritizing seasonal work", MessageTypeDefOf.NeutralEvent);
            }
        }
        
        /// <summary>
        /// Assign colonist to a shift
        /// </summary>
        public void AssignShift(Pawn pawn, string shiftName)
        {
            if (shifts.ContainsKey(shiftName))
            {
                colonistShifts[pawn] = shiftName;
            }
        }
        
        /// <summary>
        /// Get colonist's current shift
        /// </summary>
        public string GetShift(Pawn pawn)
        {
            if (colonistShifts.TryGetValue(pawn, out string shift))
                return shift;
            return "none";
        }
        
        private int GetCurrentHour()
        {
            if (Find.CurrentMap == null)
                return 12;
            
            return GenDate.HourOfDay(Find.TickManager.TicksGame, 
                Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
        }
        
        private Season GetCurrentSeason()
        {
            if (Find.CurrentMap == null)
                return Season.Undefined;
            
            return GenDate.Season(Find.TickManager.TicksGame, 
                Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            int assignedShifts = colonistShifts.Count;
            string emergencyStatus = emergencyMode ? $"Active: {currentEmergency}" : "None";
            return $"Shifts: {shifts.Count}, Assigned: {assignedShifts}, Emergency: {emergencyStatus}, Season: {currentSeason}";
        }
        
        /// <summary>
        /// Clear all shift assignments
        /// </summary>
        public void Clear()
        {
            colonistShifts.Clear();
            emergencyMode = false;
            currentEmergency = EmergencyType.None;
        }
    }
}

