using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace PriorityManager
{
    /// <summary>
    /// Scans the map to determine what work is actually needed/available
    /// </summary>
    public static class WorkScanner
    {
        /// <summary>
        /// Get work types that currently have available work on the map
        /// </summary>
        public static HashSet<WorkTypeDef> GetActiveWorkTypes(Map map)
        {
            var activeWork = new HashSet<WorkTypeDef>();
            
            if (map == null)
                return activeWork;
            
            // Check for construction/repair work
            if (HasConstructionWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Construction);
            }
            
            // Check for growing/harvesting
            if (HasGrowingWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Growing);
            }
            
            // Check for crafting bills
            var craftingWork = GetCraftingWorkTypes(map);
            foreach (var workType in craftingWork)
            {
                activeWork.Add(workType);
            }
            
            // Check for hauling needs
            if (HasHaulingWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Hauling);
            }
            
            // Check for cleaning needs
            if (HasCleaningWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Cleaning);
            }
            
            // Check for doctoring needs
            if (HasDoctorWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Doctor);
            }
            
            // Check for hunting targets
            if (HasHuntingWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Hunting);
            }
            
            // Check for mining designated
            if (HasMiningWork(map))
            {
                activeWork.Add(WorkTypeDefOf.Mining);
            }
            
            return activeWork;
        }
        
        /// <summary>
        /// Score work types by their current urgency/amount
        /// </summary>
        public static Dictionary<WorkTypeDef, float> ScoreWorkUrgency(Map map)
        {
            var scores = new Dictionary<WorkTypeDef, float>();
            
            if (map == null)
                return scores;
            
            // Score construction (blueprints + repairs)
            float constructionScore = CountConstructionWork(map);
            if (constructionScore > 0)
                scores[WorkTypeDefOf.Construction] = constructionScore;
            
            // Score growing (plants to sow/harvest)
            float growingScore = CountGrowingWork(map);
            if (growingScore > 0)
                scores[WorkTypeDefOf.Growing] = growingScore;
            
            // Score crafting (bills waiting)
            var craftingScores = ScoreCraftingWork(map);
            foreach (var kvp in craftingScores)
            {
                scores[kvp.Key] = kvp.Value;
            }
            
            // Score hauling (items on ground)
            float haulingScore = CountHaulingWork(map);
            if (haulingScore > 0)
                scores[WorkTypeDefOf.Hauling] = haulingScore;
            
            // Score cleaning (filth)
            float cleaningScore = CountCleaningWork(map);
            if (cleaningScore > 0)
                scores[WorkTypeDefOf.Cleaning] = cleaningScore;
            
            // Score doctoring (injured colonists)
            float doctorScore = CountDoctorWork(map);
            if (doctorScore > 0)
                scores[WorkTypeDefOf.Doctor] = doctorScore * 2f; // Higher priority
            
            // Score hunting (designated animals)
            float huntingScore = CountHuntingWork(map);
            if (huntingScore > 0)
                scores[WorkTypeDefOf.Hunting] = huntingScore;
            
            // Score mining (designated cells)
            float miningScore = CountMiningWork(map);
            if (miningScore > 0)
                scores[WorkTypeDefOf.Mining] = miningScore;
            
            return scores;
        }
        
        private static bool HasConstructionWork(Map map)
        {
            return map.listerBuildings.allBuildingsNonColonist.Any(b => 
                b.def.useHitPoints && b.HitPoints < b.MaxHitPoints) ||
                map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer).Any() ||
                map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Any() ||
                map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Any();
        }
        
        private static float CountConstructionWork(Map map)
        {
            float score = 0f;
            score += map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint).Count * 2f;
            score += map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame).Count * 1.5f;
            score += map.listerBuildingsRepairable.RepairableBuildings(Faction.OfPlayer).Count() * 0.5f;
            return score;
        }
        
        private static bool HasGrowingWork(Map map)
        {
            var zones = map.zoneManager.AllZones.OfType<Zone_Growing>();
            foreach (var zone in zones)
            {
                // Check for cells that need sowing or harvesting
                foreach (var cell in zone.Cells)
                {
                    Plant plant = cell.GetPlant(map);
                    if (plant == null && zone.GetPlantDefToGrow() != null)
                        return true; // Needs sowing
                    if (plant != null && plant.HarvestableNow)
                        return true; // Needs harvesting
                }
            }
            return false;
        }
        
        private static float CountGrowingWork(Map map)
        {
            float score = 0f;
            var zones = map.zoneManager.AllZones.OfType<Zone_Growing>();
            foreach (var zone in zones)
            {
                foreach (var cell in zone.Cells)
                {
                    Plant plant = cell.GetPlant(map);
                    if (plant == null && zone.GetPlantDefToGrow() != null)
                        score += 0.1f; // Needs sowing
                    else if (plant != null && plant.HarvestableNow)
                        score += 1f; // Needs harvesting (more urgent)
                }
            }
            return score;
        }
        
        private static List<WorkTypeDef> GetCraftingWorkTypes(Map map)
        {
            var workTypes = new HashSet<WorkTypeDef>();
            
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is IBillGiver billGiver && billGiver.BillStack != null)
                {
                    foreach (var bill in billGiver.BillStack.Bills)
                    {
                        if (bill.ShouldDoNow() && bill.recipe?.workSkill != null)
                        {
                            // Find work type associated with this skill
                            var workType = DefDatabase<WorkTypeDef>.AllDefsListForReading
                                .FirstOrDefault(wt => wt.relevantSkills != null && 
                                               wt.relevantSkills.Contains(bill.recipe.workSkill));
                            
                            if (workType != null)
                                workTypes.Add(workType);
                        }
                    }
                }
            }
            
            return workTypes.ToList();
        }
        
        private static Dictionary<WorkTypeDef, float> ScoreCraftingWork(Map map)
        {
            var scores = new Dictionary<WorkTypeDef, float>();
            
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building is IBillGiver billGiver && billGiver.BillStack != null)
                {
                    foreach (var bill in billGiver.BillStack.Bills)
                    {
                        if (bill.ShouldDoNow() && bill.recipe?.workSkill != null)
                        {
                            var workType = DefDatabase<WorkTypeDef>.AllDefsListForReading
                                .FirstOrDefault(wt => wt.relevantSkills != null && 
                                               wt.relevantSkills.Contains(bill.recipe.workSkill));
                            
                            if (workType != null)
                            {
                                if (!scores.ContainsKey(workType))
                                    scores[workType] = 0f;
                                
                                // Score based on work amount
                                scores[workType] += bill.recipe.workAmount / 1000f;
                            }
                        }
                    }
                }
            }
            
            return scores;
        }
        
        private static bool HasHaulingWork(Map map)
        {
            return map.listerHaulables.ThingsPotentiallyNeedingHauling().Any();
        }
        
        private static float CountHaulingWork(Map map)
        {
            return map.listerHaulables.ThingsPotentiallyNeedingHauling().Count() * 0.5f;
        }
        
        private static bool HasCleaningWork(Map map)
        {
            return map.listerFilthInHomeArea.FilthInHomeArea.Any();
        }
        
        private static float CountCleaningWork(Map map)
        {
            return map.listerFilthInHomeArea.FilthInHomeArea.Count() * 0.3f;
        }
        
        private static bool HasDoctorWork(Map map)
        {
            return map.mapPawns.FreeColonistsSpawned.Any(p => 
                p.health?.HasHediffsNeedingTend() == true ||
                (p.health != null && p.health.summaryHealth.SummaryHealthPercent < 0.95f));
        }
        
        private static float CountDoctorWork(Map map)
        {
            float score = 0f;
            foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
            {
                if (pawn.health?.HasHediffsNeedingTend() == true)
                    score += 3f; // Urgent
                else if (pawn.health != null && pawn.health.summaryHealth.SummaryHealthPercent < 0.95f)
                    score += 1f;
            }
            return score;
        }
        
        private static bool HasHuntingWork(Map map)
        {
            return map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt).Any();
        }
        
        private static float CountHuntingWork(Map map)
        {
            return map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt).Count() * 1.5f;
        }
        
        private static bool HasMiningWork(Map map)
        {
            return map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).Any();
        }
        
        private static float CountMiningWork(Map map)
        {
            return map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine).Count() * 0.8f;
        }
    }
}

