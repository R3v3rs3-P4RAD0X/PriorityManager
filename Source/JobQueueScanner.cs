using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace PriorityManager
{
    public static class JobQueueScanner
    {
        public static List<PendingJob> ScanMap(Map map, int maxJobs = 50)
        {
            if (map == null)
                return new List<PendingJob>();

            var pendingJobs = new List<PendingJob>();

            // Scan construction designations
            pendingJobs.AddRange(ScanConstruction(map));

            // Scan deconstruction/mining
            pendingJobs.AddRange(ScanMining(map));

            // Scan bills
            pendingJobs.AddRange(ScanBills(map));

            // Scan plant work
            pendingJobs.AddRange(ScanPlants(map));

            // Scan hauling
            pendingJobs.AddRange(ScanHauling(map));

            // Sort by urgency/priority
            pendingJobs = pendingJobs.OrderByDescending(j => j.urgency).Take(maxJobs).ToList();

            return pendingJobs;
        }

        private static List<PendingJob> ScanConstruction(Map map)
        {
            var jobs = new List<PendingJob>();

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                var blueprint = thing as Blueprint;
                if (blueprint != null)
                {
                    var job = new PendingJob
                    {
                        type = JobType.Construction,
                        description = $"Build {blueprint.def.entityDefToBuild?.label ?? "structure"}",
                        location = thing.Position,
                        workType = "Construction",
                        urgency = 5f
                    };

                    jobs.Add(job);
                }
            }

            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                var frame = thing as Frame;
                if (frame != null)
                {
                    var job = new PendingJob
                    {
                        type = JobType.Construction,
                        description = $"Build {frame.def.entityDefToBuild?.label ?? "structure"}",
                        location = thing.Position,
                        workType = "Construction",
                        urgency = 4f
                    };

                    jobs.Add(job);
                }
            }

            return jobs;
        }

        private static List<PendingJob> ScanMining(Map map)
        {
            var jobs = new List<PendingJob>();

            if (map.designationManager?.AllDesignations == null)
                return jobs;

            foreach (var des in map.designationManager.AllDesignations)
            {
                if (des.def == DesignationDefOf.Mine)
                {
                    var job = new PendingJob
                    {
                        type = JobType.Mining,
                        description = $"Mine {des.target.Cell.GetFirstMineable(map)?.def.label ?? "rock"}",
                        location = des.target.Cell,
                        workType = "Mining",
                        urgency = 3f
                    };
                    jobs.Add(job);
                }
                else if (des.def == DesignationDefOf.Deconstruct)
                {
                    var job = new PendingJob
                    {
                        type = JobType.Deconstruction,
                        description = $"Deconstruct {des.target.Thing?.Label ?? "structure"}",
                        location = des.target.Cell,
                        workType = "Construction",
                        urgency = 4f
                    };
                    jobs.Add(job);
                }
            }

            return jobs;
        }

        private static List<PendingJob> ScanBills(Map map)
        {
            var jobs = new List<PendingJob>();

            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var billGiver = building as IBillGiver;
                if (billGiver?.BillStack != null)
                {
                    foreach (var bill in billGiver.BillStack.Bills)
                    {
                        if (bill.ShouldDoNow() && !bill.suspended)
                        {
                            var workTable = building as Building_WorkTable;
                            var job = new PendingJob
                            {
                                type = JobType.Crafting,
                                description = $"{bill.LabelCap}",
                                location = building.Position,
                                workType = workTable != null ? GetWorkTypeForBill(bill, workTable) : "Crafting",
                                urgency = bill is Bill_Production ? 3f : 5f
                            };
                            jobs.Add(job);
                        }
                    }
                }
            }

            return jobs;
        }

        private static List<PendingJob> ScanPlants(Map map)
        {
            var jobs = new List<PendingJob>();

            if (map.designationManager?.AllDesignations == null)
                return jobs;

            int harvestCount = 0;
            int sowCount = 0;
            int cutCount = 0;

            foreach (var des in map.designationManager.AllDesignations)
            {
                if (des.def == DesignationDefOf.HarvestPlant)
                    harvestCount++;
                else if (des.def.defName == "Sow")
                    sowCount++;
                else if (des.def == DesignationDefOf.CutPlant)
                    cutCount++;
            }

            if (harvestCount > 0)
            {
                jobs.Add(new PendingJob
                {
                    type = JobType.Harvesting,
                    description = $"Harvest plants x{harvestCount}",
                    workType = "Growing",
                    urgency = 6f
                });
            }

            if (sowCount > 0)
            {
                jobs.Add(new PendingJob
                {
                    type = JobType.Sowing,
                    description = $"Sow plants x{sowCount}",
                    workType = "Growing",
                    urgency = 5f
                });
            }

            if (cutCount > 0)
            {
                jobs.Add(new PendingJob
                {
                    type = JobType.PlantCutting,
                    description = $"Cut plants x{cutCount}",
                    workType = "PlantCutting",
                    urgency = 2f
                });
            }

            return jobs;
        }

        private static List<PendingJob> ScanHauling(Map map)
        {
            var jobs = new List<PendingJob>();

            // Count items needing hauling
            int haulableCount = 0;
            var haulables = map.listerHaulables?.ThingsPotentiallyNeedingHauling();
            if (haulables != null)
            {
                foreach (var thing in haulables)
                {
                    haulableCount++;

                    if (haulableCount >= 100) // Cap at 100 to avoid performance issues
                        break;
                }
            }

            if (haulableCount > 0)
            {
                jobs.Add(new PendingJob
                {
                    type = JobType.Hauling,
                    description = $"Haul items x{haulableCount}",
                    workType = "Hauling",
                    urgency = haulableCount > 50 ? 7f : 4f
                });
            }

            return jobs;
        }

        private static string GetWorkTypeForBill(Bill bill, Building_WorkTable workTable)
        {
            var workType = workTable.def.AllRecipes?.FirstOrDefault(r => r == bill.recipe)?.workSkill;
            if (workType != null)
            {
                // Map skill to work type
                if (workType == SkillDefOf.Cooking)
                    return "Cooking";
                if (workType == SkillDefOf.Crafting)
                    return "Crafting";
                if (workType == SkillDefOf.Construction)
                    return "Construction";
                if (workType == SkillDefOf.Plants)
                    return "Growing";
                if (workType == SkillDefOf.Mining)
                    return "Mining";
                if (workType == SkillDefOf.Artistic)
                    return "Art";
                if (workType == SkillDefOf.Medicine)
                    return "Doctor";
                if (workType == SkillDefOf.Intellectual)
                    return "Research";
            }

            return "Crafting";
        }

        public static int CountCapableColonists(Map map, string workTypeDefName)
        {
            var workTypeDef = DefDatabase<WorkTypeDef>.GetNamedSilentFail(workTypeDefName);
            if (workTypeDef == null)
                return 0;

            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
                return 0;

            var colonists = gameComp.GetAllColonists();
            int count = 0;

            foreach (var colonist in colonists)
            {
                if (!colonist.WorkTypeIsDisabled(workTypeDef) && colonist.workSettings.GetPriority(workTypeDef) > 0)
                    count++;
            }

            return count;
        }
    }

    public enum JobType
    {
        Construction,
        Deconstruction,
        Mining,
        Crafting,
        Harvesting,
        Sowing,
        PlantCutting,
        Hauling,
        Repair,
        Research
    }

    public class PendingJob
    {
        public JobType type;
        public string description;
        public IntVec3 location;
        public string workType;
        public float urgency;
        public Pawn assignedColonist;
        public int capableColonists;
    }
}

