using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager
{
    public static class PriorityAssigner
    {
        // Survival priority jobs for solo colonist or initial setup
        private static readonly Dictionary<string, int> SurvivalPriorities = new Dictionary<string, int>
        {
            { "Firefighter", 1 },
            { "Doctor", 2 },
            { "Hunting", 1 },
            { "Cook", 2 },
            { "Growing", 2 },
            { "Construction", 3 },
            { "Repair", 3 },
            { "Mining", 3 },
            { "PlantCutting", 3 },
            { "Hauling", 4 },
            { "Cleaning", 4 },
            { "Crafting", 4 },
            { "Research", 4 }
        };

        public static void AssignPriorities(Pawn pawn, bool force = false)
        {
            using (PerformanceProfiler.Profile("AssignPriorities"))
            {
                if (pawn == null || pawn.workSettings == null || pawn.skills == null)
                    return;

                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                    return;

                var data = gameComp.GetOrCreateData(pawn);
                if (data == null)
                    return;

            // Don't assign if auto-assign disabled or manual mode
            // force only bypasses timing checks, not the disabled status
            if (!data.autoAssignEnabled || data.assignedRole == RolePreset.Manual)
                return;

            // Check if colonist is ill/injured
            bool isIll = IsColonistIll(pawn);
            
            if (isIll)
            {
                AssignIllnessPriorities(pawn);
                data.wasIllLastCheck = true;
                data.lastRecalculationTick = Find.TickManager.TicksGame;
                return;
            }

            // If they were ill but recovered, restore normal priorities
            if (data.wasIllLastCheck && !isIll)
            {
                data.wasIllLastCheck = false;
            }

            // Check if this is a solo colonist scenario
            var allColonists = gameComp.GetAllColonists();
            bool isSoloColonist = allColonists.Count == 1;
            var settings = PriorityManagerMod.settings;

            if (isSoloColonist && settings != null && settings.enableSoloSurvivalMode)
            {
                AssignSurvivalPriorities(pawn);
            }
            else
            {
                AssignRoleBasedPriorities(pawn, data, allColonists);
            }

            data.lastRecalculationTick = Find.TickManager.TicksGame;
            }
        }

        public static void AssignAllColonistPriorities(bool force = false)
        {
            using (PerformanceProfiler.Profile("AssignAllColonistPriorities"))
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                    return;

                var colonists = gameComp.GetAllColonists();
                
                // v2.0: Early bailout if no colonists
                if (colonists == null || colonists.Count == 0)
                    return;
                
                // v2.0: Use parallel processing for large colonies
                if (colonists.Count >= 50)
                {
                    Assignment.ParallelAssigner.AssignMultiple(colonists, force);
                }
                else if (colonists.Count > 1)
                {
                    // Use colony-wide distribution for small/medium colonies
                    AssignColonyWidePriorities(colonists, force);
                }
                else
                {
                    // Solo colonist - use individual assignment
                    AssignPriorities(colonists[0], force);
                }

                gameComp.SetLastGlobalRecalculationTick(Find.TickManager.TicksGame);
            }
        }

        private static bool IsColonistIll(Pawn pawn)
        {
            if (pawn.health == null || pawn.health.hediffSet == null)
                return false;

            var settings = PriorityManagerMod.settings;
            if (settings == null || settings.injurySeverityThreshold == InjurySeverityLevel.Disabled)
                return false;

            // Check for actual injuries/illnesses first (hediffs), then use health percentage as backup
            switch (settings.injurySeverityThreshold)
            {
                case InjurySeverityLevel.SevereOnly:
                    // Only life-threatening conditions
                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        // Lethal diseases/conditions at dangerous levels
                        if (hediff.def.lethalSeverity > 0 && hediff.Severity > hediff.def.lethalSeverity * 0.5f)
                            return true;
                        
                        // Severe diseases
                        if (hediff.def.makesSickThought && hediff.Severity > 0.7f)
                            return true;
                        
                        // Critical injuries
                        if (hediff.def.tendable && hediff.Severity > 0.8f)
                            return true;
                    }
                    
                    // Backup: very low health
                    if (pawn.health.summaryHealth.SummaryHealthPercent < 0.3f)
                        return true;
                    break;
                    
                case InjurySeverityLevel.MajorInjuries:
                    // Significant injuries or illnesses (DEFAULT)
                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        // Any lethal condition
                        if (hediff.def.lethalSeverity > 0)
                            return true;
                        
                        // Moderate to severe diseases (flu, plague, etc.)
                        if (hediff.def.makesSickThought && hediff.Severity > 0.4f)
                            return true;
                        
                        // Significant injuries
                        if (hediff.def.tendable && hediff.Severity > 0.5f)
                            return true;
                        
                        // High pain
                        if (hediff.PainOffset > 0.3f)
                            return true;
                    }
                    
                    // Backup: moderately low health
                    if (pawn.health.summaryHealth.SummaryHealthPercent < 0.5f)
                        return true;
                    break;
                    
                case InjurySeverityLevel.AnyInjury:
                    // Any injury or illness that affects capabilities
                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        // Any disease
                        if (hediff.def.makesSickThought && hediff.Severity > 0.1f)
                            return true;
                        
                        // Any injury
                        if (hediff.def.tendable && hediff.Severity > 0.2f)
                            return true;
                        
                        // Any pain
                        if (hediff.PainOffset > 0.1f)
                            return true;
                        
                        // Conditions affecting work capabilities
                        if (hediff.def.lethalSeverity > 0 ||
                            hediff.CurStage?.capMods?.Any(cm => 
                                cm.capacity == PawnCapacityDefOf.Consciousness ||
                                cm.capacity == PawnCapacityDefOf.Moving ||
                                cm.capacity == PawnCapacityDefOf.Manipulation) == true)
                            return true;
                    }
                    
                    // Backup: somewhat reduced health
                    if (pawn.health.summaryHealth.SummaryHealthPercent < 0.8f)
                        return true;
                    break;
                    
                case InjurySeverityLevel.MinorInjuries:
                    // Even minor injuries or illnesses
                    foreach (var hediff in pawn.health.hediffSet.hediffs)
                    {
                        // Any disease at any level
                        if (hediff.def.makesSickThought)
                            return true;
                        
                        // Any injury at any level
                        if (hediff.def.tendable && hediff.Severity > 0.01f)
                            return true;
                        
                        // Any pain at all
                        if (hediff.PainOffset > 0.01f)
                            return true;
                    }
                    
                    // Backup: any health reduction
                    if (pawn.health.summaryHealth.SummaryHealthPercent < 0.99f)
                        return true;
                    break;
            }

            return false;
        }

        private static void AssignIllnessPriorities(Pawn pawn)
        {
            // v2.0: When injured/ill, only assign critical survival jobs
            ClearAllPriorities(pawn);

            // Priority 1: Firefighter (always critical - prevents base destruction)
            if (CanDoWork(pawn, WorkTypeDefOf.Firefighter))
            {
                SetPriority(pawn, WorkTypeDefOf.Firefighter, 1);
            }
            
            // Priority 1: Doctor (for self-tending if capable)
            if (pawn.skills.GetSkill(SkillDefOf.Medicine)?.Level >= 3 && CanDoWork(pawn, WorkTypeDefOf.Doctor))
            {
                SetPriority(pawn, WorkTypeDefOf.Doctor, 1);
            }
            
            // Priority 1: Patient/bed rest (these are handled by game automatically when colonist is downed/resting)
            var patientBedRestWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("PatientBedRest");
            if (patientBedRestWork != null && CanDoWork(pawn, patientBedRestWork))
            {
                SetPriority(pawn, patientBedRestWork, 1);
            }
            
            var patientWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Patient");
            if (patientWork != null && CanDoWork(pawn, patientWork))
            {
                SetPriority(pawn, patientWork, 1);
            }

            // Note: All other jobs are disabled - injured pawns should rest and recover
            // They will automatically prioritize medical treatment and rest
        }

        private static void AssignSurvivalPriorities(Pawn pawn)
        {
            ClearAllPriorities(pawn);

            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType.visible && CanDoWork(pawn, workType))
                {
                    if (SurvivalPriorities.TryGetValue(workType.defName, out int priority))
                    {
                        SetPriority(pawn, workType, priority);
                    }
                }
            }
        }

        private static void AssignRoleBasedPriorities(Pawn pawn, ColonistRoleData data, List<Pawn> allColonists)
        {
            ClearAllPriorities(pawn);

            // Always enable jobs marked as "always enabled" in settings at priority 1
            ApplyAlwaysEnabledJobs(pawn);

            // Check if this is a custom role
            if (data.assignedRole == RolePreset.Custom && !string.IsNullOrEmpty(data.customRoleId))
            {
                ApplyCustomRole(pawn, data.customRoleId);
                return; // Custom roles handle all job assignments
            }

            // Check if this is a composite role (multiple jobs with priorities)
            if (RolePresetUtility.IsCompositeRole(data.assignedRole))
            {
                // Composite role - assign multiple jobs with different priorities
                ApplyCompositeRole(pawn, data.assignedRole);
                return; // Composite roles handle all job assignments
            }

            // Determine primary work type for single-job roles
            WorkTypeDef primaryWork = null;

            if (data.assignedRole == RolePreset.Auto)
            {
                // Auto-determine best job based on highest skill
                primaryWork = GetBestWorkTypeForPawn(pawn, allColonists);
            }
            else if (data.assignedRole != RolePreset.Manual)
            {
                // Use preset role
                primaryWork = RolePresetUtility.GetPrimaryWorkType(data.assignedRole);
            }

            // Set primary work to priority 1
            if (primaryWork != null && CanDoWork(pawn, primaryWork))
            {
                SetPriority(pawn, primaryWork, 1);
            }

            // Assign secondary jobs based on skills (individual mode)
            AssignSecondaryJobsIndividual(pawn, primaryWork);
        }

        private static WorkTypeDef GetBestWorkTypeForPawn(Pawn pawn, List<Pawn> allColonists)
        {
            // Get all work types the pawn can do
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible && CanDoWork(pawn, wt))
                .ToList();

            // Exclude always-enabled jobs (handled separately)
            var settings = PriorityManagerMod.settings;
            workTypes.RemoveAll(wt => settings != null && settings.IsJobAlwaysEnabled(wt));

            // Calculate scores for each work type
            var scores = new Dictionary<WorkTypeDef, float>();
            foreach (var workType in workTypes)
            {
                float score = CalculateWorkTypeScore(pawn, workType);
                scores[workType] = score;
            }

            // Get counts of how many colonists have each work type as primary
            var primaryCounts = GetPrimaryWorkTypeCounts(allColonists);

            // Prefer work types that are underrepresented
            foreach (var workType in scores.Keys.ToList())
            {
                primaryCounts.TryGetValue(workType, out int count);
                // Reduce score if many colonists already have this as primary
                scores[workType] = scores[workType] / (1 + count * 0.5f);
            }

            // Return the work type with the highest score
            return scores.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;
        }

        private static Dictionary<WorkTypeDef, int> GetPrimaryWorkTypeCounts(List<Pawn> allColonists)
        {
            var counts = new Dictionary<WorkTypeDef, int>();
            var gameComp = PriorityDataHelper.GetGameComponent();

            foreach (var colonist in allColonists)
            {
                var data = gameComp?.GetData(colonist);
                if (data != null && data.assignedRole != RolePreset.Auto && data.assignedRole != RolePreset.Manual)
                {
                    var workType = RolePresetUtility.GetPrimaryWorkType(data.assignedRole);
                    if (workType != null)
                    {
                        counts.TryGetValue(workType, out int currentCount);
                        counts[workType] = currentCount + 1;
                    }
                }
            }

            return counts;
        }

        // Apply a custom role (user-defined jobs with importance levels)
        private static void ApplyCustomRole(Pawn pawn, string roleId)
        {
            var settings = PriorityManagerMod.settings;
            var customRole = settings.GetCustomRole(roleId);
            
            if (customRole == null || !customRole.IsValid())
            {
                Log.Warning($"PriorityManager: Custom role {roleId} not found or invalid for {pawn.Name.ToStringShort}. Falling back to Auto.");
                return;
            }

            var sortedJobs = customRole.GetSortedJobs();
            var assignedWorkTypes = new HashSet<WorkTypeDef>();
            
            // Group jobs by importance level
            var jobsByImportance = new Dictionary<JobImportance, List<CustomRoleJobEntry>>();
            foreach (var job in sortedJobs)
            {
                if (!jobsByImportance.ContainsKey(job.importance))
                    jobsByImportance[job.importance] = new List<CustomRoleJobEntry>();
                jobsByImportance[job.importance].Add(job);
            }
            
            // Apply jobs in importance order (Critical -> High -> Normal -> Low -> VeryLow)
            var importanceOrder = new[] { JobImportance.Critical, JobImportance.High, JobImportance.Normal, JobImportance.Low, JobImportance.VeryLow };
            
            foreach (var importance in importanceOrder)
            {
                if (!jobsByImportance.ContainsKey(importance))
                    continue;
                
                var jobs = jobsByImportance[importance];
                int basePriority = GetBasePriorityForImportance(importance);
                
                for (int i = 0; i < jobs.Count; i++)
                {
                    var job = jobs[i];
                    var workType = job.GetWorkTypeDef();
                    
                    if (workType == null || !CanDoWork(pawn, workType))
                        continue;
                    
                    // Calculate priority within the importance tier
                    int priority = CalculatePriorityForCustomJob(importance, i, jobs.Count);
                    SetPriority(pawn, workType, priority);
                    assignedWorkTypes.Add(workType);
                }
            }
            
            // Fill remaining slots with best available jobs at priority 4 (optional backup jobs)
            var availableWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible && !assignedWorkTypes.Contains(wt) && CanDoWork(pawn, wt) && 
                       (settings == null || !settings.IsJobAlwaysEnabled(wt)))
                .ToList();

            if (availableWorkTypes.Count > 0)
            {
                var scores = new Dictionary<WorkTypeDef, float>();
                foreach (var workType in availableWorkTypes)
                {
                    scores[workType] = CalculateWorkTypeScore(pawn, workType);
                }

                // Assign top 3 remaining jobs at priority 4 as backup
                var topJobs = scores.OrderByDescending(kvp => kvp.Value).Take(3).ToList();
                foreach (var kvp in topJobs)
                {
                    SetPriority(pawn, kvp.Key, 4);
                }
            }
        }
        
        // Get base priority for importance level
        private static int GetBasePriorityForImportance(JobImportance importance)
        {
            switch (importance)
            {
                case JobImportance.Critical:
                    return 1;
                case JobImportance.High:
                    return 1;
                case JobImportance.Normal:
                    return 2;
                case JobImportance.Low:
                    return 3;
                case JobImportance.VeryLow:
                    return 4;
                case JobImportance.Disabled:
                    return 0; // Should not be assigned
                default:
                    return 3;
            }
        }
        
        // Calculate specific priority for a job within its importance tier
        private static int CalculatePriorityForCustomJob(JobImportance importance, int indexInTier, int totalInTier)
        {
            switch (importance)
            {
                case JobImportance.Critical:
                    return 1; // All critical jobs get priority 1
                    
                case JobImportance.High:
                    // High importance: first half get 1, rest get 2
                    return (indexInTier < totalInTier / 2f) ? 1 : 2;
                    
                case JobImportance.Normal:
                    // Normal importance: spread across 2-3
                    if (indexInTier < totalInTier * 0.5f)
                        return 2;
                    else
                        return 3;
                    
                case JobImportance.Low:
                    // Low importance: spread across 3-4
                    if (indexInTier < totalInTier * 0.3f)
                        return 3;
                    else
                        return 4;
                    
                case JobImportance.VeryLow:
                    return 4; // All very low jobs get priority 4
                    
                default:
                    return 3;
            }
        }

        // Apply a composite role (multiple jobs with specific priorities)
        private static void ApplyCompositeRole(Pawn pawn, RolePreset role)
        {
            var compositeJobs = RolePresetUtility.GetCompositeRoleJobsScaled(role);
            if (compositeJobs == null || compositeJobs.Count == 0)
                return;

            // Apply each job in the composite role (priorities already scaled by GetCompositeRoleJobsScaled)
            foreach (var (workType, priority) in compositeJobs)
            {
                if (CanDoWork(pawn, workType))
                {
                    // Don't scale again - already scaled in GetCompositeRoleJobsScaled
                    pawn.workSettings.SetPriority(workType, priority);
                }
            }

            // Fill remaining slots with best available jobs at priority 4
            var assignedWorkTypes = new HashSet<WorkTypeDef>(compositeJobs.Select(j => j.workType));
            var settings = PriorityManagerMod.settings;
            var availableWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible && !assignedWorkTypes.Contains(wt) && CanDoWork(pawn, wt) && 
                       (settings == null || !settings.IsJobAlwaysEnabled(wt)))
                .ToList();

            var scores = new Dictionary<WorkTypeDef, float>();
            foreach (var workType in availableWorkTypes)
            {
                scores[workType] = CalculateWorkTypeScore(pawn, workType);
            }

            // Assign top 3-5 remaining jobs at priority 4
            var topJobs = scores.OrderByDescending(kvp => kvp.Value).Take(5).ToList();
            foreach (var kvp in topJobs)
            {
                SetPriority(pawn, kvp.Key, 4);
            }
        }

        private static void AssignColonyWidePriorities(List<Pawn> colonists, bool force)
        {
            using (PerformanceProfiler.Profile("AssignColonyWidePriorities"))
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                    return;

                var settings = PriorityManagerMod.settings;

                // Split colonists into managed and manual
                // force only bypasses timing checks, not the disabled status
                var managedColonists = new List<Pawn>(colonists.Count);
                var manualColonists = new List<Pawn>(colonists.Count);

                foreach (var p in colonists)
                {
                    var data = gameComp.GetOrCreateData(p);
                    if (data != null && data.autoAssignEnabled && data.assignedRole != RolePreset.Manual)
                    {
                        managedColonists.Add(p);
                    }
                    else
                    {
                        manualColonists.Add(p);
                    }
                }

                if (managedColonists.Count == 0)
                    return;

            int totalColonists = colonists.Count; // Include manual colonists in total

            // Scan map for active work needs
            Map map = colonists.FirstOrDefault()?.Map;
            Dictionary<WorkTypeDef, float> workUrgency = new Dictionary<WorkTypeDef, float>();
            HashSet<WorkTypeDef> activeWorkTypes = new HashSet<WorkTypeDef>();
            
            if (map != null)
            {
                workUrgency = WorkScanner.ScoreWorkUrgency(map);
                activeWorkTypes = WorkScanner.GetActiveWorkTypes(map);
                
                if (workUrgency.Count > 0)
                {
                    Log.Message($"PriorityManager: Found {workUrgency.Count} work types with active work:");
                    foreach (var kvp in workUrgency.OrderByDescending(x => x.Value).Take(5))
                    {
                        Log.Message($"  - {kvp.Key.labelShort}: urgency score {kvp.Value:F1}");
                    }
                }
            }

            // Clear all priorities first and apply always-enabled jobs
            foreach (var pawn in managedColonists)
            {
                ClearAllPriorities(pawn);
                // Apply always-enabled jobs for all colonists
                ApplyAlwaysEnabledJobs(pawn);
            }

            // v2.0: Get all work types from cache (excluding always-enabled jobs)
            var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
            var allWorkTypes = new List<WorkTypeDef>(visibleWorkTypes.Count);
            for (int i = 0; i < visibleWorkTypes.Count; i++)
            {
                if (!settings.IsJobAlwaysEnabled(visibleWorkTypes[i]))
                {
                    allWorkTypes.Add(visibleWorkTypes[i]);
                }
            }

            // Build skill matrix: for each work type, rank colonists by skill
            var workTypeRankings = new Dictionary<WorkTypeDef, List<(Pawn pawn, float score)>>();
            foreach (var workType in allWorkTypes)
            {
                var rankings = new List<(Pawn pawn, float score)>();
                foreach (var pawn in managedColonists)
                {
                    if (CanDoWork(pawn, workType))
                    {
                        float score = CalculateWorkTypeScore(pawn, workType);
                        
                        // Boost score if this work type has active work
                        if (activeWorkTypes.Contains(workType))
                        {
                            score *= 1.5f; // 50% boost for jobs with active work
                        }
                        
                        // Further boost based on urgency score
                        if (workUrgency.TryGetValue(workType, out float urgency))
                        {
                            // Add urgency bonus (normalized to reasonable range)
                            score += urgency * 10f;
                        }
                        
                        rankings.Add((pawn, score));
                    }
                }
                rankings.Sort((a, b) => b.score.CompareTo(a.score)); // Sort descending by score
                workTypeRankings[workType] = rankings;
            }

            // Assign primary jobs (priority 1) with distribution
            var assignedPrimaryJobs = new Dictionary<Pawn, WorkTypeDef>();
            var coveredWorkTypes = new HashSet<WorkTypeDef>();

            // First pass: assign preset roles (including custom and composite roles)
            foreach (var pawn in managedColonists)
            {
                var data = gameComp.GetData(pawn);
                if (data != null && data.assignedRole != RolePreset.Auto && data.assignedRole != RolePreset.Manual)
                {
                    // Check if this is a custom role
                    if (data.assignedRole == RolePreset.Custom && !string.IsNullOrEmpty(data.customRoleId))
                    {
                        // Custom role - assign using ApplyCustomRole
                        ApplyCustomRole(pawn, data.customRoleId);
                        
                        // Track first job as primary for distribution purposes
                        var customRole = settings.GetCustomRole(data.customRoleId);
                        if (customRole != null && customRole.jobs.Count > 0)
                        {
                            var firstJob = customRole.GetSortedJobs().FirstOrDefault();
                            if (firstJob != null)
                            {
                                var workType = firstJob.GetWorkTypeDef();
                                if (workType != null && CanDoWork(pawn, workType))
                                {
                                    assignedPrimaryJobs[pawn] = workType;
                                    coveredWorkTypes.Add(workType);
                                }
                            }
                        }
                    }
                    // Check if this is a composite role
                    else if (RolePresetUtility.IsCompositeRole(data.assignedRole))
                    {
                        // Composite role - assign multiple jobs with priorities (using scaled priorities)
                        var compositeJobs = RolePresetUtility.GetCompositeRoleJobsScaled(data.assignedRole);
                        if (compositeJobs != null && compositeJobs.Count > 0)
                        {
                            // Track the first job as the primary for distribution purposes
                            var firstJob = compositeJobs.First();
                            if (CanDoWork(pawn, firstJob.workType))
                            {
                                assignedPrimaryJobs[pawn] = firstJob.workType;
                                coveredWorkTypes.Add(firstJob.workType);
                            }
                            
                            // Apply all composite role jobs (priorities already scaled)
                            foreach (var (workType, priority) in compositeJobs)
                            {
                                if (CanDoWork(pawn, workType))
                                {
                                    // Don't scale again - already scaled in GetCompositeRoleJobsScaled
                                    pawn.workSettings.SetPriority(workType, priority);
                                    coveredWorkTypes.Add(workType);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Single-job preset
                        var presetWork = RolePresetUtility.GetPrimaryWorkType(data.assignedRole);
                        if (presetWork != null && CanDoWork(pawn, presetWork))
                        {
                            assignedPrimaryJobs[pawn] = presetWork;
                            coveredWorkTypes.Add(presetWork);
                            SetPriority(pawn, presetWork, 1);
                        }
                    }
                }
            }

            // Count jobs held by manual colonists (for min/max worker calculations)
            var manualColonistJobs = new Dictionary<WorkTypeDef, int>();
            foreach (var manualPawn in manualColonists)
            {
                if (manualPawn.workSettings == null)
                    continue;
                    
                foreach (var workType in allWorkTypes)
                {
                    if (manualPawn.workSettings.GetPriority(workType) > 0)
                    {
                        manualColonistJobs.TryGetValue(workType, out int count);
                        manualColonistJobs[workType] = count + 1;
                    }
                }
            }

            // Second pass: assign remaining colonists to uncovered jobs first
            var unassignedColonists = managedColonists.Where(p => !assignedPrimaryJobs.ContainsKey(p)).ToList();
            
            foreach (var colonist in unassignedColonists)
            {
                WorkTypeDef bestJob = null;
                float bestScore = -1f;

                foreach (var workType in allWorkTypes)
                {
                    if (!CanDoWork(colonist, workType))
                        continue;

                    // Check max workers limit (include manual colonists in count)
                    int maxWorkers = settings.GetMaxWorkersForJob(workType, totalColonists);
                    if (maxWorkers > 0)
                    {
                        int currentAutoWorkers = assignedPrimaryJobs.Count(kvp => kvp.Value == workType);
                        int currentManualWorkers = manualColonistJobs.TryGetValue(workType, 0);
                        int totalWorkers = currentAutoWorkers + currentManualWorkers;
                        
                        if (totalWorkers >= maxWorkers)
                        {
                            Log.Message($"PriorityManager: Skipping {workType.labelShort} for {colonist.Name.ToStringShort} - max workers ({maxWorkers}) reached");
                            continue; // Skip if max workers reached
                        }
                    }

                    float score = CalculateWorkTypeScore(colonist, workType);
                    
                    // Bonus for uncovered work types (only count auto-managed for coverage)
                    if (!coveredWorkTypes.Contains(workType))
                    {
                        score *= 2.0f;
                    }
                    
                    // Bonus for jobs that need minimum workers (include manual colonists in count)
                    int minWorkers = settings.GetMinWorkersForJob(workType, totalColonists);
                    if (minWorkers > 0)
                    {
                        int currentAutoWorkers = assignedPrimaryJobs.Count(kvp => kvp.Value == workType);
                        int currentManualWorkers = manualColonistJobs.TryGetValue(workType, 0);
                        int totalWorkers = currentAutoWorkers + currentManualWorkers;
                        
                        if (totalWorkers < minWorkers)
                        {
                            score *= 2.5f; // Strong boost to meet minimum
                        }
                    }
                    
                    // Penalty if another colonist is already primary on this (only auto-managed)
                    if (assignedPrimaryJobs.ContainsValue(workType))
                    {
                        score *= 0.3f;
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestJob = workType;
                    }
                }

                if (bestJob != null)
                {
                    assignedPrimaryJobs[colonist] = bestJob;
                    coveredWorkTypes.Add(bestJob);
                    SetPriority(colonist, bestJob, 1);
                }
            }

            // Third pass: ensure ALL jobs are covered by at least one colonist (respecting max workers)
            // Track which jobs still need assignment
            var uncoveredJobs = allWorkTypes.Where(wt => !coveredWorkTypes.Contains(wt)).ToList();
            
            // Assign uncovered jobs to the best available colonists (even at lower priorities)
            foreach (var workType in uncoveredJobs)
            {
                // Check max workers limit before assigning
                int maxWorkers = settings.GetMaxWorkersForJob(workType, totalColonists);
                if (maxWorkers > 0)
                {
                    int currentAutoWorkers = assignedPrimaryJobs.Count(kvp => kvp.Value == workType);
                    int currentManualWorkers = manualColonistJobs.TryGetValue(workType, 0);
                    int totalWorkers = currentAutoWorkers + currentManualWorkers;
                    
                    if (totalWorkers >= maxWorkers)
                        continue; // Skip if max workers reached
                }
                
                Pawn bestColonist = null;
                float bestScore = -1f;

                foreach (var colonist in managedColonists)
                {
                    if (CanDoWork(colonist, workType))
                    {
                        float score = CalculateWorkTypeScore(colonist, workType);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestColonist = colonist;
                        }
                    }
                }

                // Assign to best colonist at priority 2 (backup job)
                if (bestColonist != null)
                {
                    SetPriority(bestColonist, workType, 2);
                    coveredWorkTypes.Add(workType);
                }
            }
            
            // Fourth pass: assign remaining secondary jobs (priorities 2-4) for complete coverage
            foreach (var colonist in managedColonists)
            {
                assignedPrimaryJobs.TryGetValue(colonist, out WorkTypeDef primaryJob);
                AssignSecondaryJobsComplete(colonist, primaryJob, assignedPrimaryJobs, allWorkTypes, manualColonistJobs, totalColonists, settings);
            }
            
            // Fifth pass: Enforce minimum workers AFTER secondary assignment to ensure requirements are met
            EnforceMinimumWorkers(managedColonists, allWorkTypes, assignedPrimaryJobs, manualColonistJobs, totalColonists, settings);
            
            // Log final worker counts for jobs with min/max settings
            LogFinalWorkerCounts(allWorkTypes, colonists, settings, totalColonists);
            }
        }

        private static void LogFinalWorkerCounts(List<WorkTypeDef> allWorkTypes, List<Pawn> allColonists, PriorityManagerSettings settings, int totalColonists)
        {
            var jobsWithSettings = allWorkTypes.Where(wt => 
                settings.GetMinWorkersForJob(wt, totalColonists) > 0 || 
                settings.GetMaxWorkersForJob(wt, totalColonists) > 0).ToList();
            
            if (jobsWithSettings.Count == 0)
                return;
            
            Log.Message("PriorityManager: === Final Worker Counts ===");
            foreach (var workType in jobsWithSettings)
            {
                int minWorkers = settings.GetMinWorkersForJob(workType, totalColonists);
                int maxWorkers = settings.GetMaxWorkersForJob(workType, totalColonists);
                int currentWorkers = 0;
                
                foreach (var colonist in allColonists)
                {
                    if (colonist.workSettings != null && colonist.workSettings.GetPriority(workType) > 0)
                        currentWorkers++;
                }
                
                string status = "";
                if (minWorkers > 0 && currentWorkers < minWorkers)
                    status = " [BELOW MIN]";
                else if (maxWorkers > 0 && currentWorkers > maxWorkers)
                    status = " [ABOVE MAX]";
                else if (minWorkers > 0 && currentWorkers >= minWorkers)
                    status = " [OK]";
                
                string minStr = minWorkers > 0 ? minWorkers.ToString() : "-";
                string maxStr = maxWorkers > 0 ? maxWorkers.ToString() : "∞";
                
                Log.Message($"PriorityManager: {workType.labelShort}: {currentWorkers} workers (min: {minStr}, max: {maxStr}){status}");
            }
        }
        
        private static void EnforceMinimumWorkers(
            List<Pawn> managedColonists,
            List<WorkTypeDef> allWorkTypes,
            Dictionary<Pawn, WorkTypeDef> assignedPrimaryJobs,
            Dictionary<WorkTypeDef, int> manualColonistJobs,
            int totalColonists,
            PriorityManagerSettings settings)
        {
            Log.Message("PriorityManager: Enforcing minimum worker requirements...");
            int enforcementCount = 0;
            
            // For each job type, check if it meets the minimum worker requirement
            foreach (var workType in allWorkTypes)
            {
                int minWorkers = settings.GetMinWorkersForJob(workType, totalColonists);
                int maxWorkers = settings.GetMaxWorkersForJob(workType, totalColonists);
                if (minWorkers <= 0)
                    continue; // No minimum set
                
                // Count current workers (both auto-assigned at priority 1 and manual colonists)
                int currentAutoWorkers = assignedPrimaryJobs.Count(kvp => kvp.Value == workType);
                int currentManualWorkers = manualColonistJobs.TryGetValue(workType, 0);
                int totalWorkers = currentAutoWorkers + currentManualWorkers;
                
                // Check if we need more workers
                int needed = minWorkers - totalWorkers;
                if (needed <= 0)
                    continue; // Minimum already met
                
                Log.Message($"PriorityManager: {workType.labelShort} needs {needed} more workers (current: {totalWorkers}, min: {minWorkers}, max: {maxWorkers})");
                enforcementCount++;
                
                // Find best available colonists who aren't already assigned this as primary
                var candidates = new List<(Pawn pawn, float score)>();
                foreach (var colonist in managedColonists)
                {
                    // Skip if already assigned this job as primary
                    if (assignedPrimaryJobs.TryGetValue(colonist, out WorkTypeDef existingPrimary) && existingPrimary == workType)
                        continue;
                    
                    if (CanDoWork(colonist, workType))
                    {
                        float score = CalculateWorkTypeScore(colonist, workType);
                        candidates.Add((colonist, score));
                    }
                }
                
                // Sort by score and assign top candidates
                var sortedCandidates = candidates.OrderByDescending(c => c.score).Take(needed).ToList();
                int assigned = 0;
                foreach (var (colonist, score) in sortedCandidates)
                {
                    // Check max workers limit before assigning
                    if (maxWorkers > 0)
                    {
                        int newAutoWorkers = assignedPrimaryJobs.Count(kvp => kvp.Value == workType);
                        int newTotalWorkers = newAutoWorkers + currentManualWorkers;
                        if (newTotalWorkers >= maxWorkers)
                        {
                            Log.Message($"PriorityManager: Cannot assign more to {workType.labelShort} - max workers ({maxWorkers}) reached");
                            break;
                        }
                    }
                    
                    // Check if this colonist already has a primary job
                    if (assignedPrimaryJobs.ContainsKey(colonist))
                    {
                        // Assign as secondary job (priority 2)
                        SetPriority(colonist, workType, 2);
                        Log.Message($"PriorityManager: Assigned {colonist.Name.ToStringShort} to {workType.labelShort} as secondary (priority 2)");
                    }
                    else
                    {
                        // No primary job yet, make this their primary
                        assignedPrimaryJobs[colonist] = workType;
                        SetPriority(colonist, workType, 1);
                        Log.Message($"PriorityManager: Assigned {colonist.Name.ToStringShort} to {workType.labelShort} as primary (priority 1)");
                    }
                    assigned++;
                }
                
                if (assigned < needed)
                {
                    Log.Warning($"PriorityManager: Could only assign {assigned}/{needed} workers for {workType.labelShort} (not enough capable colonists)");
                }
            }
            
            if (enforcementCount > 0)
            {
                Log.Message($"PriorityManager: Minimum worker enforcement completed - processed {enforcementCount} jobs");
            }
        }

        private static void AssignSecondaryJobsIndividual(Pawn pawn, WorkTypeDef primaryWork)
        {
            // Individual mode - just assign based on skills
            var settings = PriorityManagerMod.settings;
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible && CanDoWork(pawn, wt))
                .Where(wt => wt != primaryWork)
                .Where(wt => settings == null || !settings.IsJobAlwaysEnabled(wt))
                .ToList();

            var scores = new Dictionary<WorkTypeDef, float>();
            foreach (var workType in workTypes)
            {
                scores[workType] = CalculateWorkTypeScore(pawn, workType);
            }

            var sortedWorks = scores.OrderByDescending(kvp => kvp.Value).Take(9).ToList();

            for (int i = 0; i < sortedWorks.Count; i++)
            {
                int priority;
                if (i < 3)
                    priority = 2;
                else if (i < 6)
                    priority = 3;
                else
                    priority = 4;

                SetPriority(pawn, sortedWorks[i].Key, priority);
            }
        }

        private static void AssignSecondaryJobsComplete(Pawn pawn, WorkTypeDef primaryWork, Dictionary<Pawn, WorkTypeDef> assignedPrimaries, List<WorkTypeDef> allWorkTypes, Dictionary<WorkTypeDef, int> manualColonistJobs, int totalColonists, PriorityManagerSettings settings)
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return;

            int colonySize = gameComp.GetAllColonists().Count;

            // Get current priorities already set
            var alreadySet = new HashSet<WorkTypeDef>();
            if (primaryWork != null)
                alreadySet.Add(primaryWork);
            
            // Add always-enabled jobs to already set
            if (settings != null)
            {
                foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                {
                    if (settings.IsJobAlwaysEnabled(workType))
                        alreadySet.Add(workType);
                }
            }

            // Get work urgency for prioritization
            Map map = pawn.Map;
            Dictionary<WorkTypeDef, float> workUrgency = new Dictionary<WorkTypeDef, float>();
            HashSet<WorkTypeDef> activeWorkTypes = new HashSet<WorkTypeDef>();
            
            if (map != null)
            {
                workUrgency = WorkScanner.ScoreWorkUrgency(map);
                activeWorkTypes = WorkScanner.GetActiveWorkTypes(map);
            }

            // Get all work types this colonist can do
            var workTypes = allWorkTypes
                .Where(wt => CanDoWork(pawn, wt))
                .Where(wt => !alreadySet.Contains(wt))
                .ToList();

            var scores = new Dictionary<WorkTypeDef, float>();
            foreach (var workType in workTypes)
            {
                // Check if job is disabled in settings
                JobImportance importance = settings.GetJobImportance(workType);
                if (importance == JobImportance.Disabled)
                    continue;

                // Check max workers limit before considering this job
                int maxWorkers = settings.GetMaxWorkersForJob(workType, totalColonists);
                if (maxWorkers > 0)
                {
                    int currentWorkers = CountCurrentWorkers(workType, gameComp.GetAllColonists());
                    if (currentWorkers >= maxWorkers)
                    {
                        Log.Message($"PriorityManager: Skipping {workType.labelShort} for {pawn.Name.ToStringShort} in secondary assignment - max workers ({maxWorkers}) reached");
                        continue; // Skip jobs that are at max capacity
                    }
                }

                float score = CalculateWorkTypeScore(pawn, workType);
                
                // Apply importance modifiers
                score = ApplyImportanceModifier(score, importance);
                
                // Boost jobs that are someone else's primary (for backup coverage)
                if (assignedPrimaries.ContainsValue(workType))
                {
                    score *= 1.3f;
                }
                
                // Boost jobs with active work
                if (activeWorkTypes.Contains(workType))
                {
                    score *= 1.4f;
                }
                
                // Add urgency bonus
                if (workUrgency.TryGetValue(workType, out float urgency))
                {
                    score += urgency * 5f; // Smaller bonus for secondary jobs
                }
                
                scores[workType] = score;
            }

            // Sort by score
            var sortedWorks = scores.OrderByDescending(kvp => kvp.Value).ToList();

            // Scale job assignments based on colony size
            int maxJobsToAssign = CalculateMaxJobsForColonist(colonySize, sortedWorks.Count);
            
            // Limit how many jobs to assign (unless colonist is idle - handled separately)
            var jobsToAssign = sortedWorks.Take(maxJobsToAssign).ToList();
            
            for (int i = 0; i < jobsToAssign.Count; i++)
            {
                // Check if already assigned (from uncovered job pass)
                if (pawn.workSettings.GetPriority(jobsToAssign[i].Key) > 0)
                    continue;

                // Double-check max workers before assignment (in case it changed during iteration)
                int maxWorkers = settings.GetMaxWorkersForJob(jobsToAssign[i].Key, totalColonists);
                if (maxWorkers > 0)
                {
                    int currentWorkers = CountCurrentWorkers(jobsToAssign[i].Key, gameComp.GetAllColonists());
                    if (currentWorkers >= maxWorkers)
                        continue; // Skip if max reached
                }

                int priority = CalculatePriorityLevel(i, jobsToAssign.Count, settings.GetJobImportance(jobsToAssign[i].Key));
                SetPriority(pawn, jobsToAssign[i].Key, priority);
            }
        }
        
        private static int CountCurrentWorkers(WorkTypeDef workType, List<Pawn> allColonists)
        {
            int count = 0;
            foreach (var colonist in allColonists)
            {
                if (colonist.workSettings != null && colonist.workSettings.GetPriority(workType) > 0)
                    count++;
            }
            return count;
        }

        private static int CalculateMaxJobsForColonist(int colonySize, int totalAvailableJobs)
        {
            // Scale down jobs per colonist as colony grows
            if (colonySize <= 1)
                return totalAvailableJobs; // Solo: do everything
            else if (colonySize <= 3)
                return Math.Min(12, totalAvailableJobs); // Small: ~12 jobs each
            else if (colonySize <= 6)
                return Math.Min(9, totalAvailableJobs);  // Medium: ~9 jobs each
            else if (colonySize <= 10)
                return Math.Min(7, totalAvailableJobs);  // Large: ~7 jobs each
            else
                return Math.Min(5, totalAvailableJobs);  // Very large: ~5 jobs each (stay focused)
        }

        private static int CalculatePriorityLevel(int index, int totalJobs, JobImportance importance)
        {
            // Critical importance always gets priority 1
            if (importance == JobImportance.Critical)
                return 1;
            
            // High importance gets priority 1-2
            if (importance == JobImportance.High)
                return index < totalJobs * 0.5f ? 1 : 2;
            
            // Very Low importance gets priority 4
            if (importance == JobImportance.VeryLow)
                return 4;
            
            // Low importance gets priority 3-4
            if (importance == JobImportance.Low)
                return index < totalJobs * 0.3f ? 3 : 4;

            // Normal importance: distribute across 2-4 based on skill score
            if (index < totalJobs * 0.3f)
                return 2;
            else if (index < totalJobs * 0.7f)
                return 3;
            else
                return 4;
        }

        private static float ApplyImportanceModifier(float score, JobImportance importance)
        {
            switch (importance)
            {
                case JobImportance.Critical:
                    return score * 3.0f;
                case JobImportance.High:
                    return score * 1.8f;
                case JobImportance.Low:
                    return score * 0.6f;
                case JobImportance.VeryLow:
                    return score * 0.3f;
                case JobImportance.Disabled:
                    return 0f;
                default:
                    return score;
            }
        }

        private static void AssignSecondaryJobs(Pawn pawn, WorkTypeDef primaryWork, Dictionary<Pawn, WorkTypeDef> assignedPrimaries)
        {
            // Legacy method - kept for compatibility
            var settings = PriorityManagerMod.settings;
            var workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible && CanDoWork(pawn, wt))
                .Where(wt => wt != primaryWork)
                .Where(wt => settings == null || !settings.IsJobAlwaysEnabled(wt))
                .ToList();

            var scores = new Dictionary<WorkTypeDef, float>();
            foreach (var workType in workTypes)
            {
                float score = CalculateWorkTypeScore(pawn, workType);
                
                // Boost jobs that are someone else's primary (for backup coverage)
                if (assignedPrimaries.ContainsValue(workType))
                {
                    score *= 1.2f;
                }
                
                scores[workType] = score;
            }

            // Sort by score and assign priorities
            var sortedWorks = scores.OrderByDescending(kvp => kvp.Value).Take(9).ToList();

            // Assign priorities 2-4
            for (int i = 0; i < sortedWorks.Count; i++)
            {
                int priority;
                if (i < 3)
                    priority = 2;
                else if (i < 6)
                    priority = 3;
                else
                    priority = 4;

                SetPriority(pawn, sortedWorks[i].Key, priority);
            }
        }

        public static float CalculateWorkTypeScore(Pawn pawn, WorkTypeDef workType)
        {
            float score = 0f;
            int skillCount = 0;

            // Get relevant skills for this work type
            if (workType.relevantSkills != null && workType.relevantSkills.Count > 0)
            {
                foreach (var skillDef in workType.relevantSkills)
                {
                    var skill = pawn.skills.GetSkill(skillDef);
                    if (skill != null)
                    {
                        skillCount++;
                        
                        // Base score from skill level
                        float skillScore = skill.Level;

                        // Passion multiplier - significantly boost passionate colonists
                        if (skill.passion == Passion.Major)
                        {
                            skillScore *= 2.0f; // Double for burning passion
                        }
                        else if (skill.passion == Passion.Minor)
                        {
                            skillScore *= 1.5f; // 50% boost for minor passion
                        }

                        // Learning rate consideration (higher is better for long-term)
                        if (skill.passion == Passion.Major)
                        {
                            skillScore += 5f; // Extra bonus for growth potential
                        }
                        else if (skill.passion == Passion.Minor)
                        {
                            skillScore += 2f;
                        }
                        
                        score += skillScore;
                    }
                }

                // Average by number of skills to normalize
                if (skillCount > 0)
                    score /= skillCount;
            }
            else
            {
                // No relevant skills - give base score of 1 so job can still be assigned
                score = 1f;
            }

            // Ensure minimum score so jobs without skills can still be distributed
            if (score < 1f)
                score = 1f;

            return score;
        }

        private static bool CanDoWork(Pawn pawn, WorkTypeDef workType)
        {
            if (pawn.WorkTypeIsDisabled(workType))
                return false;

            return true;
        }

        public static void SetPriority(Pawn pawn, WorkTypeDef workType, int priority)
        {
            if (workType == null || !CanDoWork(pawn, workType))
                return;

            // Apply PriorityMaster scaling if enabled
            int scaledPriority = PriorityMasterCompat.ScalePriority(priority);

            try
            {
                pawn.workSettings.SetPriority(workType, scaledPriority);
            }
            catch (Exception ex)
            {
                Log.Warning($"PriorityManager: Failed to set priority for {pawn.Name} on work {workType.defName}: {ex.Message}");
            }
        }

        private static void ClearAllPriorities(Pawn pawn)
        {
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType.visible)
                {
                    try
                    {
                        pawn.workSettings.SetPriority(workType, 0);
                    }
                    catch { }
                }
            }
        }
        
        private static void ApplyAlwaysEnabledJobs(Pawn pawn)
        {
            var settings = PriorityManagerMod.settings;
            if (settings == null)
                return;
            
            // Get all work types and check if they should always be enabled
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType.visible && settings.IsJobAlwaysEnabled(workType) && CanDoWork(pawn, workType))
                {
                    SetPriority(pawn, workType, 1);
                }
            }
        }
    }
}

