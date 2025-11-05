using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    public enum RolePreset
    {
        Auto,           // System determines best job
        Researcher,
        Doctor,
        Crafter,
        Farmer,
        Constructor,
        Miner,
        Cook,
        Hunter,
        Hauler,
        Cleaner,
        Warden,
        // Complex Jobs support - Medical
        Nurse,          // Care for sick (Complex Jobs)
        Surgeon,        // Perform surgeries (Complex Jobs)
        // Complex Jobs support - Construction
        Repairer,       // Repair buildings (Complex Jobs)
        Deconstructor,  // Deconstruct buildings (Complex Jobs)
        // Complex Jobs support - Production
        Druggist,       // Craft drugs (Complex Jobs)
        Machinist,      // Machining table (Complex Jobs)
        Fabricator,     // Fabrication bench (Complex Jobs)
        Refiner,        // Refinery work (Complex Jobs)
        Stonecutter,    // Cut stone (Complex Jobs)
        Smelter,        // Smelt materials (Complex Jobs)
        Producer,       // General production (Complex Jobs)
        // Complex Jobs support - Other
        Trainer,        // Train animals (Complex Jobs)
        ButcherWorker,  // Butcher & kibble (Complex Jobs)
        Harvester,      // Harvest crops (Complex Jobs)
        Driller,        // Deep drill (Complex Jobs)
        // Composite roles (Complex Jobs) - Multiple related jobs with priorities
        Builder,        // Construction-focused: Construct → Repair → Deconstruct
        Demolition,     // Deconstruction-focused: Deconstruct → Repair → Construct
        Medic,          // Medical-focused: Surgeon → Nurse → Doctor
        Industrialist,  // Production-focused: Machining → Fabrication → Smithing → Crafting
        Custom,         // User-defined custom role
        Manual          // User manually controls all priorities
    }

    public class ColonistRoleData : IExposable
    {
        public string pawnId;                    // Thingdef ID for the pawn
        public RolePreset assignedRole = RolePreset.Auto;
        public bool autoAssignEnabled = true;
        public int lastRecalculationTick = 0;
        public bool wasIllLastCheck = false;     // Track illness state
        public string customRoleId = null;       // ID of custom role when assignedRole == Custom

        public ColonistRoleData()
        {
        }

        public ColonistRoleData(Pawn pawn)
        {
            this.pawnId = pawn.ThingID;
            this.assignedRole = RolePreset.Auto;
            this.autoAssignEnabled = true;
            this.lastRecalculationTick = Find.TickManager.TicksGame;
            this.wasIllLastCheck = false;
            this.customRoleId = null;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref pawnId, "pawnId");
            Scribe_Values.Look(ref assignedRole, "assignedRole", RolePreset.Auto);
            Scribe_Values.Look(ref autoAssignEnabled, "autoAssignEnabled", true);
            Scribe_Values.Look(ref lastRecalculationTick, "lastRecalculationTick", 0);
            Scribe_Values.Look(ref wasIllLastCheck, "wasIllLastCheck", false);
            Scribe_Values.Look(ref customRoleId, "customRoleId", null);
        }
    }

    public static class RolePresetUtility
    {
        // Map role presets to their primary WorkTypeDef
        public static WorkTypeDef GetPrimaryWorkType(RolePreset preset)
        {
            switch (preset)
            {
                case RolePreset.Researcher:
                    return WorkTypeDefOf.Research;
                case RolePreset.Doctor:
                    return WorkTypeDefOf.Doctor;
                case RolePreset.Crafter:
                    return WorkTypeDefOf.Crafting;
                case RolePreset.Farmer:
                    return WorkTypeDefOf.Growing;
                case RolePreset.Constructor:
                    return WorkTypeDefOf.Construction;
                case RolePreset.Miner:
                    return WorkTypeDefOf.Mining;
                case RolePreset.Cook:
                    // Cook work type - using defName lookup since WorkTypeDefOf may not have it
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking");
                case RolePreset.Hunter:
                    return WorkTypeDefOf.Hunting;
                case RolePreset.Hauler:
                    return WorkTypeDefOf.Hauling;
                case RolePreset.Cleaner:
                    return WorkTypeDefOf.Cleaning;
                case RolePreset.Warden:
                    return WorkTypeDefOf.Warden;
                // Complex Jobs support - Medical
                case RolePreset.Nurse:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Nurse");
                case RolePreset.Surgeon:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Surgeon");
                // Complex Jobs support - Construction
                case RolePreset.Repairer:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Repair");
                case RolePreset.Deconstructor:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Deconstruct");
                // Complex Jobs support - Production
                case RolePreset.Druggist:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Drugs");
                case RolePreset.Machinist:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Machining");
                case RolePreset.Fabricator:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Fabrication");
                case RolePreset.Refiner:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Refine");
                case RolePreset.Stonecutter:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("CutStone");
                case RolePreset.Smelter:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Smelt");
                case RolePreset.Producer:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Production");
                // Complex Jobs support - Other
                case RolePreset.Trainer:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Train");
                case RolePreset.ButcherWorker:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Butcher");
                case RolePreset.Harvester:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Harvest");
                case RolePreset.Driller:
                    return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Drill");
                default:
                    return null;
            }
        }

        public static string GetRoleLabel(RolePreset preset)
        {
            switch (preset)
            {
                case RolePreset.Auto:
                    return "Auto (Best Skill)";
                case RolePreset.Manual:
                    return "Manual Control";
                // Complex Jobs - Medical
                case RolePreset.Nurse:
                    return "Nurse";
                case RolePreset.Surgeon:
                    return "Surgeon";
                // Complex Jobs - Construction
                case RolePreset.Repairer:
                    return "Repairer";
                case RolePreset.Deconstructor:
                    return "Deconstructor";
                // Complex Jobs - Production
                case RolePreset.Druggist:
                    return "Druggist";
                case RolePreset.Machinist:
                    return "Machinist";
                case RolePreset.Fabricator:
                    return "Fabricator";
                case RolePreset.Refiner:
                    return "Refiner";
                case RolePreset.Stonecutter:
                    return "Stonecutter";
                case RolePreset.Smelter:
                    return "Smelter";
                case RolePreset.Producer:
                    return "Producer";
                // Complex Jobs - Other
                case RolePreset.Trainer:
                    return "Animal Trainer";
                case RolePreset.ButcherWorker:
                    return "Butcher";
                case RolePreset.Harvester:
                    return "Harvester";
                case RolePreset.Driller:
                    return "Driller";
                // Composite Roles
                case RolePreset.Builder:
                    return "Builder (Construct→Repair→Demo)";
                case RolePreset.Demolition:
                    return "Demolition (Demo→Repair→Construct)";
                case RolePreset.Medic:
                    return "Medic (Surgery→Nursing→Doctor)";
                case RolePreset.Industrialist:
                    return "Industrialist (Multi-Production)";
                default:
                    return preset.ToString();
            }
        }
        
        // Check if a role is a composite role (multiple jobs with different priorities)
        public static bool IsCompositeRole(RolePreset preset)
        {
            return preset == RolePreset.Builder ||
                   preset == RolePreset.Demolition ||
                   preset == RolePreset.Medic ||
                   preset == RolePreset.Industrialist;
        }
        
        // Get the job hierarchy for composite roles
        // Returns list of (WorkTypeDef, priority) tuples
        // Returns null for non-composite roles
        public static List<(WorkTypeDef workType, int priority)> GetCompositeRoleJobs(RolePreset preset)
        {
            if (!IsCompositeRole(preset))
                return null;
                
            var jobs = new List<(WorkTypeDef workType, int priority)>();
            
            switch (preset)
            {
                case RolePreset.Builder:
                    // Construction → Repair → Deconstruct
                    AddIfExists(jobs, "Construction", 1);  // Vanilla construction
                    AddIfExists(jobs, "Repair", 2);
                    AddIfExists(jobs, "Deconstruct", 3);
                    break;
                    
                case RolePreset.Demolition:
                    // Deconstruct → Repair → Construction
                    AddIfExists(jobs, "Deconstruct", 1);
                    AddIfExists(jobs, "Repair", 2);
                    AddIfExists(jobs, "Construction", 1);  // Vanilla construction at same priority
                    break;
                    
                case RolePreset.Medic:
                    // Surgeon → Nurse → Doctor (vanilla)
                    AddIfExists(jobs, "Surgeon", 1);
                    AddIfExists(jobs, "Nurse", 2);
                    AddIfExists(jobs, "Doctor", 2);  // Vanilla doctor
                    break;
                    
                case RolePreset.Industrialist:
                    // Machining → Fabrication → Smithing → Crafting
                    AddIfExists(jobs, "Machining", 1);
                    AddIfExists(jobs, "Fabrication", 1);
                    AddIfExists(jobs, "Smithing", 2);
                    AddIfExists(jobs, "Crafting", 2);
                    AddIfExists(jobs, "Smelt", 3);
                    AddIfExists(jobs, "Production", 3);
                    break;
            }
            
            return jobs.Count > 0 ? jobs : null;
        }
        
        // Get composite role jobs with PriorityMaster scaling applied
        // This spreads jobs across the extended priority range for better granularity
        public static List<(WorkTypeDef workType, int priority)> GetCompositeRoleJobsScaled(RolePreset preset)
        {
            var jobs = GetCompositeRoleJobs(preset);
            if (jobs == null || jobs.Count == 0)
                return jobs;
            
            // If PriorityMaster is not loaded or integration is disabled, return unscaled
            if (!PriorityMasterCompat.IsLoaded())
                return jobs;
            
            // Spread across extended range for better granularity
            int maxPriority = PriorityMasterCompat.GetMaxPriority();
            var scaledJobs = new List<(WorkTypeDef workType, int priority)>();
            
            switch (preset)
            {
                case RolePreset.Builder:
                    // Construction → Repair → Deconstruct
                    // Spread: 10%, 30%, 60% of max
                    foreach (var job in jobs)
                    {
                        if (job.priority == 1 && job.workType.defName == "Construction")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.1f)));
                        else if (job.priority == 2 && job.workType.defName == "Repair")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.3f)));
                        else if (job.priority == 3 && job.workType.defName == "Deconstruct")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.6f)));
                        else
                            scaledJobs.Add((job.workType, PriorityMasterCompat.ScalePriority(job.priority)));
                    }
                    break;
                    
                case RolePreset.Demolition:
                    // Deconstruct → Repair → Construction
                    // Spread: 10%, 30%, 60% of max
                    foreach (var job in jobs)
                    {
                        if (job.workType.defName == "Deconstruct")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.1f)));
                        else if (job.workType.defName == "Repair")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.3f)));
                        else if (job.workType.defName == "Construction")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.6f)));
                        else
                            scaledJobs.Add((job.workType, PriorityMasterCompat.ScalePriority(job.priority)));
                    }
                    break;
                    
                case RolePreset.Medic:
                    // Surgeon → Nurse + Doctor
                    // Spread: 10%, 30% of max
                    foreach (var job in jobs)
                    {
                        if (job.workType.defName == "Surgeon")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.1f)));
                        else if (job.workType.defName == "Nurse" || job.workType.defName == "Doctor")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.3f)));
                        else
                            scaledJobs.Add((job.workType, PriorityMasterCompat.ScalePriority(job.priority)));
                    }
                    break;
                    
                case RolePreset.Industrialist:
                    // Machining + Fabrication → Smithing + Crafting → Smelt + Production
                    // Spread: 10%, 30%, 60% of max
                    foreach (var job in jobs)
                    {
                        if (job.workType.defName == "Machining" || job.workType.defName == "Fabrication")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.1f)));
                        else if (job.workType.defName == "Smithing" || job.workType.defName == "Crafting")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.3f)));
                        else if (job.workType.defName == "Smelt" || job.workType.defName == "Production")
                            scaledJobs.Add((job.workType, Mathf.RoundToInt(maxPriority * 0.6f)));
                        else
                            scaledJobs.Add((job.workType, PriorityMasterCompat.ScalePriority(job.priority)));
                    }
                    break;
                    
                default:
                    // For any other composite roles, use standard scaling
                    foreach (var job in jobs)
                    {
                        scaledJobs.Add((job.workType, PriorityMasterCompat.ScalePriority(job.priority)));
                    }
                    break;
            }
            
            return scaledJobs.Count > 0 ? scaledJobs : jobs;
        }
        
        // Helper to add work type if it exists
        private static void AddIfExists(List<(WorkTypeDef workType, int priority)> list, string defName, int priority)
        {
            WorkTypeDef workType = null;
            
            // Try exact match first
            workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(defName);
            
            // For Construction, also try WorkTypeDefOf
            if (workType == null && defName == "Construction")
            {
                workType = WorkTypeDefOf.Construction;
            }
            
            // For Doctor, use WorkTypeDefOf
            if (workType == null && defName == "Doctor")
            {
                workType = WorkTypeDefOf.Doctor;
            }
            
            // For Crafting, use WorkTypeDefOf
            if (workType == null && defName == "Crafting")
            {
                workType = WorkTypeDefOf.Crafting;
            }
            
            // For Smithing, use WorkTypeDefOf
            if (workType == null && defName == "Smithing")
            {
                workType = WorkTypeDefOf.Smithing;
            }
            
            if (workType != null)
            {
                list.Add((workType, priority));
            }
        }

        // Get all available role presets for display in UI
        // Only includes presets whose work types actually exist
        public static List<RolePreset> GetAllPresets()
        {
            var presets = new List<RolePreset>
            {
                RolePreset.Auto,
                RolePreset.Researcher,
                RolePreset.Doctor,
                RolePreset.Crafter,
                RolePreset.Farmer,
                RolePreset.Constructor,
                RolePreset.Miner,
                RolePreset.Cook,
                RolePreset.Hunter,
                RolePreset.Hauler,
                RolePreset.Cleaner,
                RolePreset.Warden
            };
            
            // Add Complex Jobs single-role presets if their work types exist
            if (GetPrimaryWorkType(RolePreset.Nurse) != null)
                presets.Add(RolePreset.Nurse);
            if (GetPrimaryWorkType(RolePreset.Surgeon) != null)
                presets.Add(RolePreset.Surgeon);
            if (GetPrimaryWorkType(RolePreset.Repairer) != null)
                presets.Add(RolePreset.Repairer);
            if (GetPrimaryWorkType(RolePreset.Deconstructor) != null)
                presets.Add(RolePreset.Deconstructor);
            if (GetPrimaryWorkType(RolePreset.Druggist) != null)
                presets.Add(RolePreset.Druggist);
            if (GetPrimaryWorkType(RolePreset.Machinist) != null)
                presets.Add(RolePreset.Machinist);
            if (GetPrimaryWorkType(RolePreset.Fabricator) != null)
                presets.Add(RolePreset.Fabricator);
            if (GetPrimaryWorkType(RolePreset.Refiner) != null)
                presets.Add(RolePreset.Refiner);
            if (GetPrimaryWorkType(RolePreset.Stonecutter) != null)
                presets.Add(RolePreset.Stonecutter);
            if (GetPrimaryWorkType(RolePreset.Smelter) != null)
                presets.Add(RolePreset.Smelter);
            if (GetPrimaryWorkType(RolePreset.Producer) != null)
                presets.Add(RolePreset.Producer);
            if (GetPrimaryWorkType(RolePreset.Trainer) != null)
                presets.Add(RolePreset.Trainer);
            if (GetPrimaryWorkType(RolePreset.ButcherWorker) != null)
                presets.Add(RolePreset.ButcherWorker);
            if (GetPrimaryWorkType(RolePreset.Harvester) != null)
                presets.Add(RolePreset.Harvester);
            if (GetPrimaryWorkType(RolePreset.Driller) != null)
                presets.Add(RolePreset.Driller);
            
            // Add composite roles if any of their work types exist
            if (GetCompositeRoleJobs(RolePreset.Builder) != null)
                presets.Add(RolePreset.Builder);
            if (GetCompositeRoleJobs(RolePreset.Demolition) != null)
                presets.Add(RolePreset.Demolition);
            if (GetCompositeRoleJobs(RolePreset.Medic) != null)
                presets.Add(RolePreset.Medic);
            if (GetCompositeRoleJobs(RolePreset.Industrialist) != null)
                presets.Add(RolePreset.Industrialist);
            
            presets.Add(RolePreset.Manual);
            
            return presets;
        }
        
        // Check if Complex Jobs mod is loaded by checking for its work types
        public static bool IsComplexJobsLoaded()
        {
            return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Repair") != null ||
                   DefDatabase<WorkTypeDef>.GetNamedSilentFail("Repairing") != null ||
                   DefDatabase<WorkTypeDef>.GetNamedSilentFail("Deconstruct") != null ||
                   DefDatabase<WorkTypeDef>.GetNamedSilentFail("Deconstructing") != null;
        }
    }
}

