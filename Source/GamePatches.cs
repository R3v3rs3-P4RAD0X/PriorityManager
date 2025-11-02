using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PriorityManager
{
    // MapComponent for periodic updates
    public class PriorityManagerMapComponent : MapComponent
    {
        private int tickCounter = 0;
        private const int CHECK_INTERVAL = 250; // Check every 250 ticks (~4 seconds)

        public PriorityManagerMapComponent(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            base.MapComponentTick();

            tickCounter++;
            if (tickCounter >= CHECK_INTERVAL)
            {
                tickCounter = 0;
                CheckAndRecalculate();
                CheckHealthChanges();
                CheckIdleColonists();
            }
        }

        private void CheckAndRecalculate()
        {
            var settings = PriorityManagerMod.settings;
            if (!settings.globalAutoAssignEnabled || settings.autoRecalculateIntervalHours <= 0)
                return;

            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;

            int currentTick = Find.TickManager.TicksGame;
            int lastTick = gameComp.GetLastGlobalRecalculationTick();
            int intervalTicks = settings.autoRecalculateIntervalHours * 2500; // 2500 ticks per hour

            if (currentTick - lastTick >= intervalTicks)
            {
                PriorityAssigner.AssignAllColonistPriorities(false);
            }
        }

        private void CheckHealthChanges()
        {
            var settings = PriorityManagerMod.settings;
            if (!settings.illnessResponseEnabled)
                return;

            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;

            var colonists = gameComp.GetAllManagedColonists();
            foreach (var pawn in colonists)
            {
                var data = gameComp.GetData(pawn);
                if (data == null)
                    continue;

                bool isCurrentlyIll = IsColonistIll(pawn);
                
                // State changed - trigger recalculation
                if (isCurrentlyIll != data.wasIllLastCheck)
                {
                    PriorityAssigner.AssignPriorities(pawn, false);
                }
            }
        }

        private bool IsColonistIll(Pawn pawn)
        {
            if (pawn.health == null)
                return false;

            float healthPercent = pawn.health.summaryHealth.SummaryHealthPercent;
            if (healthPercent < 0.5f)
                return true;

            if (pawn.health.hediffSet != null)
            {
                foreach (var hediff in pawn.health.hediffSet.hediffs)
                {
                    if (hediff.def.makesSickThought || hediff.def.tendable)
                    {
                        if (hediff.Severity > 0.3f || hediff.def.lethalSeverity > 0)
                            return true;
                    }
                }
            }

            return false;
        }

        private int idleCheckCounter = 0;
        private const int IDLE_CHECK_INTERVAL = 2500; // Check every ~4 seconds

        private void CheckIdleColonists()
        {
            idleCheckCounter++;
            if (idleCheckCounter < IDLE_CHECK_INTERVAL)
                return;

            idleCheckCounter = 0;

            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;

            var colonists = gameComp.GetAllManagedColonists();
            foreach (var pawn in colonists)
            {
                // Check if colonist is idle (not doing anything)
                if (pawn.mindState != null && pawn.CurJob != null)
                {
                    // If they're wandering or idle, they might need more jobs
                    string jobDefName = pawn.CurJob.def?.defName ?? "";
                    if (jobDefName.Contains("Wait") || jobDefName.Contains("Wander") || jobDefName.Contains("Idle"))
                    {
                        // Check how many jobs they have assigned
                        int assignedJobCount = CountAssignedJobs(pawn);
                        int totalJobs = DefDatabase<WorkTypeDef>.AllDefsListForReading.Count(wt => wt.visible);
                        
                        // If they have less than half the jobs available, give them more
                        if (assignedJobCount < totalJobs * 0.5f)
                        {
                            ExpandIdleColonistJobs(pawn);
                        }
                    }
                }
            }
        }

        private int CountAssignedJobs(Pawn pawn)
        {
            if (pawn.workSettings == null)
                return 0;

            int count = 0;
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType.visible && pawn.workSettings.GetPriority(workType) > 0)
                    count++;
            }
            return count;
        }

        private void ExpandIdleColonistJobs(Pawn pawn)
        {
            var settings = PriorityManagerMod.settings;
            
            // Get all work types not currently assigned
            var unassignedJobs = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible)
                .Where(wt => pawn.workSettings.GetPriority(wt) == 0)
                .Where(wt => !pawn.WorkTypeIsDisabled(wt))
                .Where(wt => settings.GetJobImportance(wt) != JobImportance.Disabled)
                .ToList();

            if (unassignedJobs.Count == 0)
                return;

            // Assign top unassigned jobs at priority 4 (as backup/filler work)
            foreach (var workType in unassignedJobs.Take(5))
            {
                pawn.workSettings.SetPriority(workType, 4);
            }

            Log.Message($"PriorityManager: {pawn.Name.ToStringShort} was idle, assigned {Math.Min(5, unassignedJobs.Count)} additional jobs.");
        }
    }

    // Patch to add buttons to the Work tab
    [HarmonyPatch(typeof(MainTabWindow_Work), "DoWindowContents")]
    public static class MainTabWindow_Work_Patch
    {
        private static bool hasLoggedPatchStatus = false;

        static void Postfix(MainTabWindow_Work __instance, Rect rect)
        {
            try
            {
                if (!hasLoggedPatchStatus)
                {
                    Log.Message("PriorityManager: Work tab patch is running successfully.");
                    hasLoggedPatchStatus = true;
                }

                // Add buttons at the top right of the work tab
                float buttonWidth = 150f;
                float buttonHeight = 30f;
                float spacing = 10f;
                float topMargin = 5f;
                float rightMargin = 5f;
                
                // Position from right edge
                Rect recalcButtonRect = new Rect(rect.xMax - buttonWidth - rightMargin, rect.y + topMargin, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(recalcButtonRect, "Recalculate All"))
                {
                    PriorityAssigner.AssignAllColonistPriorities(true);
                    Messages.Message("All colonist priorities have been recalculated.", MessageTypeDefOf.TaskCompletion);
                }

                // Priority Manager button to the left of Recalculate All
                Rect buttonRect = new Rect(recalcButtonRect.x - buttonWidth - spacing, rect.y + topMargin, buttonWidth, buttonHeight);
                if (Widgets.ButtonText(buttonRect, "Priority Manager"))
                {
                    Find.WindowStack.Add(new ConfigWindow());
                }

                // Show indicators for auto-managed colonists
                DrawAutoManagedIndicators(__instance);
            }
            catch (Exception ex)
            {
                Log.Error($"PriorityManager: Error in Work tab patch: {ex.Message}\n{ex.StackTrace}");
            }
        }

        static void DrawAutoManagedIndicators(MainTabWindow_Work window)
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;

            // This would require access to the pawn list being displayed
            // We'll add a simple tooltip/indicator system
            // For now, we'll skip this as it requires more complex reflection/patching
        }
    }

    // Patch to handle new colonists joining
    [HarmonyPatch(typeof(Pawn), "SetFaction")]
    public static class Pawn_SetFaction_Patch
    {
        static void Postfix(Pawn __instance, Faction newFaction)
        {
            // Check if this pawn just joined the player's faction
            if (newFaction != null && newFaction.IsPlayer && __instance.RaceProps.Humanlike)
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp != null && __instance.workSettings != null)
                {
                    // Small delay to ensure pawn is fully initialized
                    var data = gameComp.GetOrCreateData(__instance);
                    if (data != null)
                    {
                        // Auto-assign priorities for new colonist
                        PriorityAssigner.AssignPriorities(__instance, true);
                    }
                }
            }
        }
    }

    // Patch to handle keybind
    [HarmonyPatch(typeof(UIRoot_Play), "UIRootOnGUI")]
    public static class UIRoot_Play_Patch
    {
        private static bool hasLoggedKeybind = false;

        static void Postfix()
        {
            if (Current.ProgramState != ProgramState.Playing)
                return;

            KeyBindingDef keyBinding = DefDatabase<KeyBindingDef>.GetNamedSilentFail("OpenPriorityManager");
            if (keyBinding != null && keyBinding.KeyDownEvent)
            {
                if (!hasLoggedKeybind)
                {
                    Log.Message("PriorityManager: Keybind 'P' detected and working!");
                    hasLoggedKeybind = true;
                }

                if (Find.WindowStack.IsOpen<ConfigWindow>())
                {
                    Find.WindowStack.TryRemove(typeof(ConfigWindow));
                }
                else
                {
                    Find.WindowStack.Add(new ConfigWindow());
                }
                Event.current.Use();
            }
        }
    }

    // Patch to initialize colonist data when loading a game
    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class Game_InitNewGame_Patch
    {
        static void Postfix()
        {
            // Initialize any colonists present at game start
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp != null)
            {
                var colonists = gameComp.GetAllColonists();
                foreach (var colonist in colonists)
                {
                    gameComp.GetOrCreateData(colonist);
                }
            }
        }
    }

    // Patch to handle loading saved games
    [HarmonyPatch(typeof(Game), "LoadGame")]
    public static class Game_LoadGame_Patch
    {
        static void Postfix()
        {
            // Ensure all colonists have data entries
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp != null)
            {
                var colonists = gameComp.GetAllColonists();
                foreach (var colonist in colonists)
                {
                    gameComp.GetOrCreateData(colonist);
                }
            }
        }
    }
}

