using HarmonyLib;
using RimWorld;
using Verse;
using System.Collections.Generic;

namespace PriorityManager.Events
{
    /// <summary>
    /// Harmony patches that fire events instead of polling
    /// </summary>
    [StaticConstructorOnStartup]
    public static class EventHarmonyPatches
    {
        static EventHarmonyPatches()
        {
            Log.Message("[PriorityManager] Applying event-driven Harmony patches...");
        }
        
        // ============================================================================
        // COLONIST SPAWNING / DESPAWNING
        // ============================================================================
        
        /// <summary>
        /// Detect colonist added to map
        /// </summary>
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
        public static class Pawn_SpawnSetup_Patch
        {
            static void Postfix(Pawn __instance, Map map, bool respawningAfterLoad)
            {
                // Only track colonists
                if (__instance == null || !__instance.IsColonist || __instance.Faction != Faction.OfPlayer)
                    return;
                
                // Don't fire event on load
                if (respawningAfterLoad)
                    return;
                
                EventDispatcher.Instance.Dispatch(new ColonistAddedEvent(__instance));
            }
        }
        
        /// <summary>
        /// Detect colonist death
        /// </summary>
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
        public static class Pawn_Kill_Patch
        {
            static void Prefix(Pawn __instance, DamageInfo? dinfo)
            {
                if (__instance == null || !__instance.IsColonist || __instance.Faction != Faction.OfPlayer)
                    return;
                
                EventDispatcher.Instance.Dispatch(new ColonistRemovedEvent(__instance, "death"));
            }
        }
        
        // ============================================================================
        // HEALTH TRACKING
        // ============================================================================
        
        /// <summary>
        /// Track health changes via hediff addition
        /// </summary>
        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff), new System.Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
        public static class HealthTracker_AddHediff_Patch
        {
            static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn == null || !pawn.IsColonist || pawn.Faction != Faction.OfPlayer)
                    return;
                
                // Check if this is a significant health change
                if (hediff == null)
                    return;
                
                // Fire event if it's a major illness or injury
                if (hediff.def.makesSickThought || hediff.def.tendable || hediff.def.lethalSeverity > 0)
                {
                    float healthPercent = __instance.summaryHealth.SummaryHealthPercent;
                    bool becameIll = healthPercent < 0.8f; // Threshold for "ill"
                    
                    EventDispatcher.Instance.Dispatch(new HealthChangedEvent(
                        pawn, 
                        oldHealth: 1.0f, // We don't track previous easily, approximate
                        newHealth: healthPercent,
                        becameIll: becameIll
                    ));
                }
            }
        }
        
        /// <summary>
        /// Track health changes via hediff removal (healing)
        /// </summary>
        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.RemoveHediff))]
        public static class HealthTracker_RemoveHediff_Patch
        {
            static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
            {
                Pawn pawn = Traverse.Create(__instance).Field("pawn").GetValue<Pawn>();
                if (pawn == null || !pawn.IsColonist || pawn.Faction != Faction.OfPlayer)
                    return;
                
                if (hediff == null)
                    return;
                
                // Fire event if recovering from significant hediff
                if (hediff.def.makesSickThought || hediff.def.tendable)
                {
                    float healthPercent = __instance.summaryHealth.SummaryHealthPercent;
                    bool recovered = healthPercent > 0.8f;
                    
                    EventDispatcher.Instance.Dispatch(new HealthChangedEvent(
                        pawn,
                        oldHealth: healthPercent - 0.1f, // Approximate
                        newHealth: healthPercent,
                        recovered: recovered
                    ));
                }
            }
        }
        
        // ============================================================================
        // SKILL TRACKING
        // ============================================================================
        
        /// <summary>
        /// Track skill level changes
        /// </summary>
        [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Level), MethodType.Setter)]
        public static class SkillRecord_Level_Patch
        {
            static void Prefix(SkillRecord __instance, int value, out int __state)
            {
                // Store old level
                __state = __instance.Level;
            }
            
            static void Postfix(SkillRecord __instance, int __state)
            {
                int oldLevel = __state;
                int newLevel = __instance.Level;
                
                // Only fire if level actually changed
                if (oldLevel == newLevel)
                    return;
                
                Pawn pawn = __instance.Pawn;
                if (pawn == null || !pawn.IsColonist || pawn.Faction != Faction.OfPlayer)
                    return;
                
                EventDispatcher.Instance.Dispatch(new SkillChangedEvent(
                    pawn,
                    __instance.def,
                    oldLevel,
                    newLevel
                ));
            }
        }
        
        // ============================================================================
        // WORK DESIGNATION TRACKING
        // ============================================================================
        
        /// <summary>
        /// Track new designations (construction, mining, etc.)
        /// </summary>
        [HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.AddDesignation))]
        public static class DesignationManager_AddDesignation_Patch
        {
            static void Postfix(DesignationManager __instance, Designation newDes)
            {
                if (newDes == null || newDes.target == null)
                    return;
                
                // Get map from designation manager
                Map map = Traverse.Create(__instance).Field("map").GetValue<Map>();
                if (map == null)
                    map = Find.CurrentMap; // Fallback
                
                // Map designation def to work type
                WorkTypeDef workType = GetWorkTypeForDesignation(newDes.def);
                if (workType != null)
                {
                    EventDispatcher.Instance.Dispatch(new JobDesignatedEvent(
                        workType,
                        newDes.target.Cell,
                        map
                    ));
                }
            }
            
            private static WorkTypeDef GetWorkTypeForDesignation(DesignationDef def)
            {
                if (def == DesignationDefOf.Mine)
                    return WorkTypeDefOf.Mining;
                if (def == DesignationDefOf.Deconstruct || def == DesignationDefOf.Uninstall)
                    return WorkTypeDefOf.Construction;
                if (def == DesignationDefOf.HarvestPlant)
                    return WorkTypeDefOf.Growing;
                if (def == DesignationDefOf.Hunt)
                    return WorkTypeDefOf.Hunting;
                if (def == DesignationDefOf.CutPlant)
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("PlantCutting");
                
                return null;
            }
        }
    }
}

