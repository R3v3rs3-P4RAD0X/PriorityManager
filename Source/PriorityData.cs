using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager
{
    public enum JobImportance
    {
        Disabled,      // Never assign this job
        VeryLow,       // Only as last resort (priority 4)
        Low,           // Backup job (priority 3-4)
        Normal,        // Standard (priority 2-4)
        High,          // Preferred (priority 1-2)
        Critical       // Always assign (priority 1)
    }
    
    // PriorityMaster priority distribution presets
    public enum PriorityPreset
    {
        Tight,      // Close spacing: 10, 20, 30, 40
        Balanced,   // Balanced spread: 10, 30, 60, 90 (default)
        Wide,       // Maximum spread: 5, 25, 55, 95
        Custom      // User-defined custom mapping
    }

    public class JobPrioritySetting : IExposable
    {
        public string workTypeDefName;
        public JobImportance importance = JobImportance.Normal;

        public JobPrioritySetting()
        {
        }

        public JobPrioritySetting(string defName)
        {
            this.workTypeDefName = defName;
            this.importance = JobImportance.Normal;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref workTypeDefName, "workTypeDefName");
            Scribe_Values.Look(ref importance, "importance", JobImportance.Normal);
        }
    }

    public class PriorityManagerSettings : ModSettings
    {
        public int autoRecalculateIntervalHours = 12;  // Hours between auto-recalculations
        public bool globalAutoAssignEnabled = true;
        public bool illnessResponseEnabled = true;
        public Dictionary<string, JobImportance> jobImportanceSettings = new Dictionary<string, JobImportance>();
        public Dictionary<string, int> jobMinWorkers = new Dictionary<string, int>();
        public Dictionary<string, int> jobMaxWorkers = new Dictionary<string, int>();
        public Dictionary<string, bool> jobUsePercentage = new Dictionary<string, bool>(); // True = use percentage, False = use absolute numbers
        
        // PriorityMaster integration settings
        public bool enablePriorityMasterIntegration = true;
        public bool useCustomMapping = false;
        public Dictionary<int, int> customPriorityMapping = new Dictionary<int, int>(); // vanilla → PM priority
        public PriorityPreset priorityPreset = PriorityPreset.Balanced;
        
        // Custom roles
        public List<CustomRole> customRoles = new List<CustomRole>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoRecalculateIntervalHours, "autoRecalculateIntervalHours", 12);
            Scribe_Values.Look(ref globalAutoAssignEnabled, "globalAutoAssignEnabled", true);
            Scribe_Values.Look(ref illnessResponseEnabled, "illnessResponseEnabled", true);
            Scribe_Collections.Look(ref jobImportanceSettings, "jobImportanceSettings", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref jobMinWorkers, "jobMinWorkers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref jobMaxWorkers, "jobMaxWorkers", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref jobUsePercentage, "jobUsePercentage", LookMode.Value, LookMode.Value);
            
            // PriorityMaster integration settings
            Scribe_Values.Look(ref enablePriorityMasterIntegration, "enablePriorityMasterIntegration", true);
            Scribe_Values.Look(ref useCustomMapping, "useCustomMapping", false);
            Scribe_Collections.Look(ref customPriorityMapping, "customPriorityMapping", LookMode.Value, LookMode.Value);
            Scribe_Values.Look(ref priorityPreset, "priorityPreset", PriorityPreset.Balanced);
            
            // Custom roles
            Scribe_Collections.Look(ref customRoles, "customRoles", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (jobImportanceSettings == null)
                    jobImportanceSettings = new Dictionary<string, JobImportance>();
                if (jobMinWorkers == null)
                    jobMinWorkers = new Dictionary<string, int>();
                if (jobMaxWorkers == null)
                    jobMaxWorkers = new Dictionary<string, int>();
                if (jobUsePercentage == null)
                    jobUsePercentage = new Dictionary<string, bool>();
                if (customPriorityMapping == null)
                    customPriorityMapping = new Dictionary<int, int>();
                if (customRoles == null)
                    customRoles = new List<CustomRole>();
            }
        }

        public JobImportance GetJobImportance(WorkTypeDef workType)
        {
            if (workType == null)
                return JobImportance.Normal;

            if (jobImportanceSettings.TryGetValue(workType.defName, out JobImportance importance))
                return importance;

            // Set defaults for critical jobs
            if (workType == WorkTypeDefOf.Firefighter)
                return JobImportance.Critical;

            return JobImportance.Normal;
        }

        public void SetJobImportance(WorkTypeDef workType, JobImportance importance)
        {
            if (workType == null)
                return;

            jobImportanceSettings[workType.defName] = importance;
        }

        public int GetMinWorkersForJob(WorkTypeDef workType, int totalColonists = 0)
        {
            if (workType == null)
                return 0;

            jobMinWorkers.TryGetValue(workType.defName, out int min);
            
            // Check if using percentage
            if (IsUsingPercentage(workType) && totalColonists > 0)
            {
                return (int)System.Math.Ceiling(totalColonists * (min / 100f));
            }
            
            return min;
        }

        public int GetMaxWorkersForJob(WorkTypeDef workType, int totalColonists = 0)
        {
            if (workType == null)
                return 0;

            jobMaxWorkers.TryGetValue(workType.defName, out int max);
            
            // Check if using percentage
            if (IsUsingPercentage(workType) && totalColonists > 0 && max > 0)
            {
                return (int)System.Math.Ceiling(totalColonists * (max / 100f));
            }
            
            return max; // 0 = unlimited
        }

        public bool IsUsingPercentage(WorkTypeDef workType)
        {
            if (workType == null)
                return false;

            jobUsePercentage.TryGetValue(workType.defName, out bool usePercentage);
            return usePercentage;
        }

        public void SetWorkerCounts(WorkTypeDef workType, int minWorkers, int maxWorkers, bool usePercentage)
        {
            if (workType == null)
                return;

            jobMinWorkers[workType.defName] = minWorkers;
            jobMaxWorkers[workType.defName] = maxWorkers;
            jobUsePercentage[workType.defName] = usePercentage;
        }

        public int GetRawMinWorkers(WorkTypeDef workType)
        {
            if (workType == null)
                return 0;
            jobMinWorkers.TryGetValue(workType.defName, out int min);
            return min;
        }

        public int GetRawMaxWorkers(WorkTypeDef workType)
        {
            if (workType == null)
                return 0;
            jobMaxWorkers.TryGetValue(workType.defName, out int max);
            return max;
        }
        
        // PriorityMaster integration methods
        
        public int GetMappedPriority(int vanillaPriority)
        {
            if (useCustomMapping && customPriorityMapping.TryGetValue(vanillaPriority, out int mapped))
                return mapped;
            return PriorityMasterCompat.ScalePriority(vanillaPriority);
        }
        
        public void ApplyPreset(PriorityPreset preset)
        {
            if (!PriorityMasterCompat.IsLoaded())
                return;
                
            int max = PriorityMasterCompat.GetMaxPriority();
            customPriorityMapping.Clear();
            
            switch (preset)
            {
                case PriorityPreset.Tight:
                    customPriorityMapping[1] = UnityEngine.Mathf.RoundToInt(max * 0.1f);
                    customPriorityMapping[2] = UnityEngine.Mathf.RoundToInt(max * 0.2f);
                    customPriorityMapping[3] = UnityEngine.Mathf.RoundToInt(max * 0.3f);
                    customPriorityMapping[4] = UnityEngine.Mathf.RoundToInt(max * 0.4f);
                    break;
                case PriorityPreset.Wide:
                    customPriorityMapping[1] = UnityEngine.Mathf.RoundToInt(max * 0.05f);
                    customPriorityMapping[2] = UnityEngine.Mathf.RoundToInt(max * 0.25f);
                    customPriorityMapping[3] = UnityEngine.Mathf.RoundToInt(max * 0.55f);
                    customPriorityMapping[4] = UnityEngine.Mathf.RoundToInt(max * 0.95f);
                    break;
                case PriorityPreset.Balanced:
                    // Use default scaling from PriorityMasterCompat
                    customPriorityMapping[1] = PriorityMasterCompat.ScalePriority(1);
                    customPriorityMapping[2] = PriorityMasterCompat.ScalePriority(2);
                    customPriorityMapping[3] = PriorityMasterCompat.ScalePriority(3);
                    customPriorityMapping[4] = PriorityMasterCompat.ScalePriority(4);
                    break;
                case PriorityPreset.Custom:
                    // Don't change custom mapping
                    break;
            }
            
            priorityPreset = preset;
        }
        
        // Custom role management methods
        
        public CustomRole GetCustomRole(string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
                return null;
            
            return customRoles.FirstOrDefault(r => r.roleId == roleId);
        }
        
        public void AddCustomRole(CustomRole role)
        {
            if (role == null || !role.IsValid())
                return;
            
            // Ensure unique ID
            if (customRoles.Any(r => r.roleId == role.roleId))
            {
                role.roleId = Guid.NewGuid().ToString();
            }
            
            customRoles.Add(role);
        }
        
        public void RemoveCustomRole(string roleId)
        {
            if (string.IsNullOrEmpty(roleId))
                return;
            
            customRoles.RemoveAll(r => r.roleId == roleId);
        }
        
        public void UpdateCustomRole(CustomRole role)
        {
            if (role == null || string.IsNullOrEmpty(role.roleId))
                return;
            
            var existing = GetCustomRole(role.roleId);
            if (existing != null)
            {
                customRoles.Remove(existing);
                customRoles.Add(role);
            }
        }
        
        public List<CustomRole> GetAllCustomRoles()
        {
            return new List<CustomRole>(customRoles);
        }
    }

    public class PriorityManagerGameComponent : GameComponent
    {
        private Dictionary<string, ColonistRoleData> colonistData = new Dictionary<string, ColonistRoleData>();
        private int lastGlobalRecalculationTick = 0;

        public PriorityManagerGameComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref colonistData, "colonistData", LookMode.Value, LookMode.Deep);
            Scribe_Values.Look(ref lastGlobalRecalculationTick, "lastGlobalRecalculationTick", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (colonistData == null)
                {
                    colonistData = new Dictionary<string, ColonistRoleData>();
                }
            }
        }

        public ColonistRoleData GetOrCreateData(Pawn pawn)
        {
            if (pawn == null || pawn.ThingID == null)
                return null;

            if (!colonistData.ContainsKey(pawn.ThingID))
            {
                colonistData[pawn.ThingID] = new ColonistRoleData(pawn);
            }

            return colonistData[pawn.ThingID];
        }

        public ColonistRoleData GetData(Pawn pawn)
        {
            if (pawn == null || pawn.ThingID == null)
                return null;

            colonistData.TryGetValue(pawn.ThingID, out ColonistRoleData data);
            return data;
        }

        public void SetRolePreset(Pawn pawn, RolePreset preset)
        {
            var data = GetOrCreateData(pawn);
            if (data != null)
            {
                data.assignedRole = preset;
                data.autoAssignEnabled = (preset != RolePreset.Manual);
            }
        }

        public void ResetAllToAuto()
        {
            foreach (var data in colonistData.Values)
            {
                data.assignedRole = RolePreset.Auto;
                data.autoAssignEnabled = true;
            }
        }

        public int GetLastGlobalRecalculationTick()
        {
            return lastGlobalRecalculationTick;
        }

        public void SetLastGlobalRecalculationTick(int tick)
        {
            lastGlobalRecalculationTick = tick;
        }

        public List<Pawn> GetAllManagedColonists()
        {
            if (Current.Game == null || Current.Game.CurrentMap == null)
                return new List<Pawn>();

            return Current.Game.CurrentMap.mapPawns.FreeColonists
                .Where(p => p.workSettings != null && GetData(p)?.autoAssignEnabled == true)
                .ToList();
        }

        public List<Pawn> GetAllColonists()
        {
            if (Current.Game == null || Current.Game.CurrentMap == null)
                return new List<Pawn>();

            return Current.Game.CurrentMap.mapPawns.FreeColonists
                .Where(p => p.workSettings != null)
                .ToList();
        }
    }

    public static class PriorityDataHelper
    {
        public static PriorityManagerGameComponent GetGameComponent()
        {
            if (Current.Game == null)
                return null;

            return Current.Game.GetComponent<PriorityManagerGameComponent>();
        }
    }
}

