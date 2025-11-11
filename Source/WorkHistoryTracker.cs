using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager
{
    public class WorkHistoryTracker : IExposable
    {
        private const int HISTORY_DAYS = 7;
        private const int TICKS_PER_SAMPLE = 250; // Sample every ~4 seconds
        private const int MAX_SAMPLES = HISTORY_DAYS * GenDate.TicksPerDay / TICKS_PER_SAMPLE; // ~42,000 samples for 7 days

        // Circular buffer of work samples
        private List<WorkSample> samples = new List<WorkSample>();
        private int nextSampleIndex = 0;
        private int lastSampleTick = 0;

        public WorkHistoryTracker()
        {
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref samples, "samples", LookMode.Deep);
            Scribe_Values.Look(ref nextSampleIndex, "nextSampleIndex", 0);
            Scribe_Values.Look(ref lastSampleTick, "lastSampleTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (samples == null)
                    samples = new List<WorkSample>();
            }
        }

        public void Update(int currentTick)
        {
            // Only sample periodically to save performance and memory
            if (currentTick - lastSampleTick < TICKS_PER_SAMPLE)
                return;

            lastSampleTick = currentTick;

            // Get all colonists
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;

            var colonists = gameComp.GetAllColonists();
            if (colonists.Count == 0)
                return;

            // Create new sample
            var sample = new WorkSample
            {
                tick = currentTick,
                colonistStates = new Dictionary<string, ColonistWorkState>()
            };

            foreach (var colonist in colonists)
            {
                if (colonist.ThingID == null)
                    continue;

                var state = new ColonistWorkState
                {
                    pawnId = colonist.ThingID,
                    isIdle = IsColonistIdle(colonist),
                    currentJob = colonist.CurJobDef?.defName,
                    currentWorkType = GetCurrentWorkType(colonist)
                };

                sample.colonistStates[colonist.ThingID] = state;
            }

            // Store sample in circular buffer
            if (samples.Count < MAX_SAMPLES)
            {
                samples.Add(sample);
            }
            else
            {
                // Overwrite oldest sample
                samples[nextSampleIndex] = sample;
            }

            nextSampleIndex = (nextSampleIndex + 1) % MAX_SAMPLES;
        }

        public float GetIdlePercentage(Pawn pawn, TimeWindow window)
        {
            if (pawn?.ThingID == null || samples.Count == 0)
                return 0f;

            int windowTicks = GetWindowTicks(window);
            int currentTick = Find.TickManager.TicksGame;
            int idleCount = 0;
            int totalCount = 0;

            foreach (var sample in samples)
            {
                // Only count samples within the time window
                if (currentTick - sample.tick > windowTicks)
                    continue;

                if (sample.colonistStates.TryGetValue(pawn.ThingID, out ColonistWorkState state))
                {
                    totalCount++;
                    if (state.isIdle)
                        idleCount++;
                }
            }

            if (totalCount == 0)
                return 0f;

            return (float)idleCount / totalCount * 100f;
        }

        public Dictionary<string, int> GetWorkTypeUsage(Pawn pawn, TimeWindow window)
        {
            var usage = new Dictionary<string, int>();
            
            if (pawn?.ThingID == null || samples.Count == 0)
                return usage;

            int windowTicks = GetWindowTicks(window);
            int currentTick = Find.TickManager.TicksGame;

            foreach (var sample in samples)
            {
                if (currentTick - sample.tick > windowTicks)
                    continue;

                if (sample.colonistStates.TryGetValue(pawn.ThingID, out ColonistWorkState state))
                {
                    if (!string.IsNullOrEmpty(state.currentWorkType))
                    {
                        usage.TryGetValue(state.currentWorkType, out int count);
                        usage[state.currentWorkType] = count + 1;
                    }
                }
            }

            return usage;
        }

        public List<WorkSample> GetSamplesForPawn(Pawn pawn, TimeWindow window)
        {
            if (pawn?.ThingID == null)
                return new List<WorkSample>();

            int windowTicks = GetWindowTicks(window);
            int currentTick = Find.TickManager.TicksGame;

            return samples
                .Where(s => currentTick - s.tick <= windowTicks && s.colonistStates.ContainsKey(pawn.ThingID))
                .OrderBy(s => s.tick)
                .ToList();
        }

        public void ClearOldData()
        {
            // Remove samples older than HISTORY_DAYS
            int currentTick = Find.TickManager.TicksGame;
            int maxAge = HISTORY_DAYS * GenDate.TicksPerDay;

            samples.RemoveAll(s => currentTick - s.tick > maxAge);
            
            if (nextSampleIndex >= samples.Count)
                nextSampleIndex = 0;
        }

        private bool IsColonistIdle(Pawn pawn)
        {
            if (pawn.CurJobDef == null)
                return true;

            // Consider these jobs as "idle"
            var idleJobs = new HashSet<string>
            {
                "Wait",
                "Wait_Downed",
                "Wait_MaintainPosture",
                "Wait_SafeTemperature",
                "Wait_Wander",
                "GotoWander",
                "Idle"
            };

            return idleJobs.Contains(pawn.CurJobDef.defName);
        }

        private string GetCurrentWorkType(Pawn pawn)
        {
            if (pawn.CurJobDef == null)
                return null;

            // Try to determine work type from current job
            var job = pawn.CurJob;
            if (job?.workGiverDef?.workType != null)
                return job.workGiverDef.workType.defName;

            // Fallback: try to match job def to work type
            var jobDefName = pawn.CurJobDef.defName;
            
            // Common job mappings
            if (jobDefName.Contains("Haul"))
                return "Hauling";
            if (jobDefName.Contains("Clean"))
                return "Cleaning";
            if (jobDefName.Contains("Research"))
                return "Research";
            if (jobDefName.Contains("Construct") || jobDefName.Contains("Build"))
                return "Construction";
            if (jobDefName.Contains("Repair") || jobDefName.Contains("FixBroken"))
                return "Repair";
            if (jobDefName.Contains("Plant") || jobDefName.Contains("Sow") || jobDefName.Contains("Harvest"))
                return "Growing";
            if (jobDefName.Contains("Hunt"))
                return "Hunting";
            if (jobDefName.Contains("Cook"))
                return "Cooking";
            if (jobDefName.Contains("Doctor") || jobDefName.Contains("Tend"))
                return "Doctor";
            if (jobDefName.Contains("Mine"))
                return "Mining";
            if (jobDefName.Contains("Warden"))
                return "Warden";

            return null;
        }

        private int GetWindowTicks(TimeWindow window)
        {
            switch (window)
            {
                case TimeWindow.Day:
                    return GenDate.TicksPerDay;
                case TimeWindow.ThreeDays:
                    return GenDate.TicksPerDay * 3;
                case TimeWindow.Week:
                    return GenDate.TicksPerDay * 7;
                default:
                    return GenDate.TicksPerDay;
            }
        }
    }

    public class WorkSample : IExposable
    {
        public int tick;
        public Dictionary<string, ColonistWorkState> colonistStates;

        public WorkSample()
        {
            colonistStates = new Dictionary<string, ColonistWorkState>();
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick", 0);
            Scribe_Collections.Look(ref colonistStates, "colonistStates", LookMode.Value, LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (colonistStates == null)
                    colonistStates = new Dictionary<string, ColonistWorkState>();
            }
        }
    }

    public class ColonistWorkState : IExposable
    {
        public string pawnId;
        public bool isIdle;
        public string currentJob;
        public string currentWorkType;

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref isIdle, "isIdle", false);
            Scribe_Values.Look(ref currentJob, "currentJob");
            Scribe_Values.Look(ref currentWorkType, "currentWorkType");
        }
    }
}

