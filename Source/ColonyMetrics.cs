using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager
{
    public class ColonyMetrics
    {
        private Map map;
        private List<Pawn> colonists;
        private PriorityManagerSettings settings;

        public ColonyMetrics(Map map)
        {
            this.map = map;
            this.settings = PriorityManagerMod.settings;
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp != null)
            {
                this.colonists = gameComp.GetAllColonists();
            }
            else
            {
                this.colonists = new List<Pawn>();
            }
        }

        // Performance Metrics

        public Dictionary<Pawn, float> GetIdleTimePercentages(TimeWindow window)
        {
            var result = new Dictionary<Pawn, float>();
            var tracker = GetHistoryTracker();
            
            if (tracker == null)
                return result;

            foreach (var colonist in colonists)
            {
                float idlePercent = tracker.GetIdlePercentage(colonist, window);
                result[colonist] = idlePercent;
            }

            return result;
        }

        public float GetAverageIdleTime(TimeWindow window)
        {
            var idleTimes = GetIdleTimePercentages(window);
            if (idleTimes.Count == 0)
                return 0f;
            return idleTimes.Values.Average();
        }

        public List<JobBottleneck> GetJobBottlenecks()
        {
            var bottlenecks = new List<JobBottleneck>();
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(wt => wt.visible).ToList();
            
            foreach (var workType in allWorkTypes)
            {
                int minWorkers = settings.GetMinWorkersForJob(workType, colonists.Count);
                int maxWorkers = settings.GetMaxWorkersForJob(workType, colonists.Count);
                int currentWorkers = CountWorkersForJob(workType);
                
                // Check if below minimum
                if (minWorkers > 0 && currentWorkers < minWorkers)
                {
                    bottlenecks.Add(new JobBottleneck
                    {
                        workType = workType,
                        currentWorkers = currentWorkers,
                        targetWorkers = minWorkers,
                        severity = BottleneckSeverity.BelowMinimum,
                        description = $"Below minimum: {currentWorkers}/{minWorkers} workers"
                    });
                }
                // Check if above maximum
                else if (maxWorkers > 0 && currentWorkers > maxWorkers)
                {
                    bottlenecks.Add(new JobBottleneck
                    {
                        workType = workType,
                        currentWorkers = currentWorkers,
                        targetWorkers = maxWorkers,
                        severity = BottleneckSeverity.AboveMaximum,
                        description = $"Above maximum: {currentWorkers}/{maxWorkers} workers"
                    });
                }
                
                // Check for high workload with low capacity
                if (map != null)
                {
                    var workUrgency = WorkScanner.ScoreWorkUrgency(map);
                    if (workUrgency.TryGetValue(workType, out float urgency) && urgency > 50f && currentWorkers < 2)
                    {
                        bottlenecks.Add(new JobBottleneck
                        {
                            workType = workType,
                            currentWorkers = currentWorkers,
                            targetWorkers = currentWorkers + 1,
                            severity = BottleneckSeverity.HighDemand,
                            description = $"High demand, low capacity: {currentWorkers} workers"
                        });
                    }
                }
            }

            return bottlenecks.OrderByDescending(b => (int)b.severity).ToList();
        }

        public float GetColonyEfficiencyScore()
        {
            float score = 100f;
            
            // Penalize for idle time (max -30 points)
            float avgIdle = GetAverageIdleTime(TimeWindow.Day);
            score -= avgIdle * 0.3f;
            
            // Penalize for bottlenecks (max -40 points)
            var bottlenecks = GetJobBottlenecks();
            int criticalBottlenecks = bottlenecks.Count(b => b.severity == BottleneckSeverity.BelowMinimum);
            score -= criticalBottlenecks * 10f;
            score -= (bottlenecks.Count - criticalBottlenecks) * 5f;
            
            // Penalize for skill gaps (max -30 points)
            var skillGaps = GetSkillCoverageGaps();
            int criticalGaps = skillGaps.Count(g => g.severity == GapSeverity.NoSkill);
            score -= criticalGaps * 15f;
            score -= (skillGaps.Count - criticalGaps) * 5f;
            
            return Math.Max(0f, Math.Min(100f, score));
        }

        // Staffing Overview

        public Dictionary<WorkTypeDef, List<Pawn>> GetCurrentAssignments()
        {
            var assignments = new Dictionary<WorkTypeDef, List<Pawn>>();
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(wt => wt.visible).ToList();
            
            foreach (var workType in allWorkTypes)
            {
                assignments[workType] = new List<Pawn>();
                
                foreach (var colonist in colonists)
                {
                    if (colonist.workSettings != null && colonist.workSettings.GetPriority(workType) > 0)
                    {
                        assignments[workType].Add(colonist);
                    }
                }
            }

            return assignments;
        }

        public Dictionary<WorkTypeDef, StaffingStatus> GetStaffingStatus()
        {
            var status = new Dictionary<WorkTypeDef, StaffingStatus>();
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(wt => wt.visible).ToList();
            
            foreach (var workType in allWorkTypes)
            {
                int minWorkers = settings.GetMinWorkersForJob(workType, colonists.Count);
                int maxWorkers = settings.GetMaxWorkersForJob(workType, colonists.Count);
                int currentWorkers = CountWorkersForJob(workType);
                
                StaffingLevel level = StaffingLevel.Optimal;
                
                if (minWorkers > 0 && currentWorkers < minWorkers)
                    level = StaffingLevel.Understaffed;
                else if (maxWorkers > 0 && currentWorkers > maxWorkers)
                    level = StaffingLevel.Overstaffed;
                
                status[workType] = new StaffingStatus
                {
                    workType = workType,
                    currentWorkers = currentWorkers,
                    minWorkers = minWorkers,
                    maxWorkers = maxWorkers,
                    level = level
                };
            }

            return status;
        }

        public List<SkillCoverageGap> GetSkillCoverageGaps()
        {
            var gaps = new List<SkillCoverageGap>();
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading.Where(wt => wt.visible).ToList();
            
            foreach (var workType in allWorkTypes)
            {
                if (workType.relevantSkills == null || workType.relevantSkills.Count == 0)
                    continue;

                var skilledColonists = new List<Pawn>();
                float totalSkill = 0f;
                int skillCount = 0;
                
                foreach (var colonist in colonists)
                {
                    foreach (var skillDef in workType.relevantSkills)
                    {
                        var skill = colonist.skills?.GetSkill(skillDef);
                        if (skill != null && skill.Level > 0)
                        {
                            totalSkill += skill.Level;
                            skillCount++;
                            if (!skilledColonists.Contains(colonist))
                                skilledColonists.Add(colonist);
                        }
                    }
                }
                
                float avgSkill = skillCount > 0 ? totalSkill / skillCount : 0f;
                
                GapSeverity severity = GapSeverity.Adequate;
                
                if (skilledColonists.Count == 0)
                    severity = GapSeverity.NoSkill;
                else if (avgSkill < 3f)
                    severity = GapSeverity.LowSkill;
                else if (skilledColonists.Count < 2)
                    severity = GapSeverity.SinglePoint;
                
                if (severity != GapSeverity.Adequate)
                {
                    gaps.Add(new SkillCoverageGap
                    {
                        workType = workType,
                        skilledColonists = skilledColonists.Count,
                        averageSkillLevel = avgSkill,
                        severity = severity
                    });
                }
            }

            return gaps.OrderByDescending(g => (int)g.severity).ToList();
        }

        public Dictionary<RolePreset, int> GetRoleDistribution()
        {
            var distribution = new Dictionary<RolePreset, int>();
            var gameComp = PriorityDataHelper.GetGameComponent();
            
            if (gameComp == null)
                return distribution;

            foreach (var colonist in colonists)
            {
                var data = gameComp.GetData(colonist);
                if (data != null)
                {
                    var role = data.assignedRole;
                    distribution.TryGetValue(role, out int count);
                    distribution[role] = count + 1;
                }
            }

            return distribution;
        }

        // Helper Methods

        private int CountWorkersForJob(WorkTypeDef workType)
        {
            int count = 0;
            foreach (var colonist in colonists)
            {
                if (colonist.workSettings != null && colonist.workSettings.GetPriority(workType) > 0)
                    count++;
            }
            return count;
        }

        private WorkHistoryTracker GetHistoryTracker()
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            return gameComp?.GetWorkHistoryTracker();
        }

        // Workload Assessment

        public WorkloadLevel GetColonistWorkload(Pawn pawn)
        {
            if (pawn == null || pawn.workSettings == null)
                return WorkloadLevel.Idle;

            // Count assigned jobs (priority > 0)
            int assignedJobs = 0;
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType.visible && pawn.workSettings.GetPriority(workType) > 0)
                    assignedJobs++;
            }

            // Get idle time from history
            var tracker = GetHistoryTracker();
            float idlePercent = tracker != null ? tracker.GetIdlePercentage(pawn, TimeWindow.Day) : 0f;

            // Check health status
            float healthPercent = pawn.health?.summaryHealth.SummaryHealthPercent ?? 1f;
            
            // Determine workload level
            if (idlePercent > 80f || assignedJobs == 0)
                return WorkloadLevel.Idle;
            
            if (healthPercent < 0.5f || assignedJobs <= 3)
                return WorkloadLevel.Light;
            
            if (assignedJobs >= 10 || (assignedJobs >= 7 && idlePercent < 5f))
                return WorkloadLevel.Heavy;
            
            if (assignedJobs >= 7 || idlePercent < 10f)
                return WorkloadLevel.Moderate;
            
            // Check for overworked conditions
            if ((assignedJobs >= 12) || (healthPercent < 0.6f && assignedJobs >= 8))
                return WorkloadLevel.Overworked;

            return WorkloadLevel.Light;
        }

        public string GetWorkloadTooltip(Pawn pawn)
        {
            if (pawn == null || pawn.workSettings == null)
                return "No data available";

            int assignedJobs = 0;
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (workType.visible && pawn.workSettings.GetPriority(workType) > 0)
                    assignedJobs++;
            }

            var tracker = GetHistoryTracker();
            float idlePercent = tracker != null ? tracker.GetIdlePercentage(pawn, TimeWindow.Day) : 0f;
            float healthPercent = pawn.health?.summaryHealth.SummaryHealthPercent ?? 1f;

            WorkloadLevel level = GetColonistWorkload(pawn);
            string levelText = level.ToString();

            return $"Workload: {levelText}\n" +
                   $"Assigned Jobs: {assignedJobs}\n" +
                   $"Idle Time (24h): {idlePercent:F1}%\n" +
                   $"Health: {healthPercent:F0}%";
        }

        // Skill Matrix

        public SkillMatrixData GetSkillMatrix()
        {
            var matrix = new SkillMatrixData();
            matrix.colonists = new List<Pawn>(colonists);
            matrix.workTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible && wt.relevantSkills != null && wt.relevantSkills.Count > 0)
                .OrderBy(wt => wt.labelShort)
                .ToList();
            
            matrix.cells = new Dictionary<WorkTypeDef, Dictionary<Pawn, SkillCell>>();
            
            foreach (var workType in matrix.workTypes)
            {
                matrix.cells[workType] = new Dictionary<Pawn, SkillCell>();
                
                foreach (var colonist in colonists)
                {
                    var cell = new SkillCell();
                    cell.canDo = !colonist.WorkTypeIsDisabled(workType);
                    cell.isAssigned = colonist.workSettings != null && colonist.workSettings.GetPriority(workType) > 0;
                    
                    // Calculate average skill level for this work type
                    if (cell.canDo && workType.relevantSkills != null && workType.relevantSkills.Count > 0)
                    {
                        int totalSkill = 0;
                        Passion highestPassion = Passion.None;
                        
                        foreach (var skillDef in workType.relevantSkills)
                        {
                            var skill = colonist.skills?.GetSkill(skillDef);
                            if (skill != null)
                            {
                                totalSkill += skill.Level;
                                if ((int)skill.passion > (int)highestPassion)
                                    highestPassion = skill.passion;
                            }
                        }
                        
                        cell.skillLevel = totalSkill / workType.relevantSkills.Count;
                        cell.passion = highestPassion;
                    }
                    else
                    {
                        cell.skillLevel = 0;
                        cell.passion = Passion.None;
                    }
                    
                    matrix.cells[workType][colonist] = cell;
                }
            }
            
            return matrix;
        }
    }

    // Data Structures

    public enum WorkloadLevel
    {
        Idle,
        Light,
        Moderate,
        Heavy,
        Overworked
    }

    public enum TimeWindow
    {
        Day,        // Last 24 hours
        ThreeDays,  // Last 3 days
        Week        // Last 7 days
    }

    public enum BottleneckSeverity
    {
        HighDemand = 0,
        AboveMaximum = 1,
        BelowMinimum = 2
    }

    public class JobBottleneck
    {
        public WorkTypeDef workType;
        public int currentWorkers;
        public int targetWorkers;
        public BottleneckSeverity severity;
        public string description;
    }

    public enum StaffingLevel
    {
        Understaffed,
        Optimal,
        Overstaffed
    }

    public class StaffingStatus
    {
        public WorkTypeDef workType;
        public int currentWorkers;
        public int minWorkers;
        public int maxWorkers;
        public StaffingLevel level;
    }

    public enum GapSeverity
    {
        Adequate = 0,
        SinglePoint = 1,
        LowSkill = 2,
        NoSkill = 3
    }

    public class SkillCoverageGap
    {
        public WorkTypeDef workType;
        public int skilledColonists;
        public float averageSkillLevel;
        public GapSeverity severity;
    }

    public class SkillMatrixData
    {
        public List<Pawn> colonists;
        public List<WorkTypeDef> workTypes;
        public Dictionary<WorkTypeDef, Dictionary<Pawn, SkillCell>> cells;
    }

    public class SkillCell
    {
        public int skillLevel;
        public Passion passion;
        public bool canDo;
        public bool isAssigned;
    }
}

