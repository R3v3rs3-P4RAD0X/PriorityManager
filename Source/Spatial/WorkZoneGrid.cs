using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PriorityManager.Spatial
{
    /// <summary>
    /// Spatial grid for efficient work scanning - divides map into 16x16 regions
    /// v2.0: Only scan regions with active work instead of full map
    /// </summary>
    public class WorkZoneGrid
    {
        private Map map;
        private WorkZone[,] zones;
        private int gridWidth;
        private int gridHeight;
        
        private const int ZONE_SIZE = 16; // 16x16 cells per zone
        private int lastUpdateTick = 0;
        private const int UPDATE_INTERVAL = 250; // Update every 250 ticks
        
        public WorkZoneGrid(Map map)
        {
            this.map = map;
            
            // Calculate grid dimensions
            gridWidth = (map.Size.x + ZONE_SIZE - 1) / ZONE_SIZE;
            gridHeight = (map.Size.z + ZONE_SIZE - 1) / ZONE_SIZE;
            
            zones = new WorkZone[gridWidth, gridHeight];
            
            // Initialize zones
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    zones[x, z] = new WorkZone(x, z);
                }
            }
            
            Log.Message($"[PriorityManager] WorkZoneGrid initialized: {gridWidth}x{gridHeight} zones ({gridWidth * gridHeight} total)");
        }
        
        /// <summary>
        /// Update work zones (call periodically)
        /// </summary>
        public void Update()
        {
            using (PerformanceProfiler.Profile("WorkZoneGrid.Update"))
            {
                int currentTick = Find.TickManager.TicksGame;
                if (currentTick - lastUpdateTick < UPDATE_INTERVAL)
                    return;
                
                lastUpdateTick = currentTick;
                
                // Clear all zones
                for (int x = 0; x < gridWidth; x++)
                {
                    for (int z = 0; z < gridHeight; z++)
                    {
                        zones[x, z].Clear();
                    }
                }
                
                // Scan for active work
                ScanConstructionWork();
                ScanMiningWork();
                ScanBillWork();
                ScanPlantWork();
                ScanHaulingWork();
            }
        }
        
        /// <summary>
        /// Get work demand for a specific work type
        /// </summary>
        public float GetDemand(WorkTypeDef workType)
        {
            float totalDemand = 0f;
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    totalDemand += zones[x, z].GetDemand(workType);
                }
            }
            
            return totalDemand;
        }
        
        /// <summary>
        /// Get all work types with active work
        /// </summary>
        public HashSet<WorkTypeDef> GetActiveWorkTypes()
        {
            var active = new HashSet<WorkTypeDef>();
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    foreach (var workType in zones[x, z].activeWorkTypes)
                    {
                        active.Add(workType);
                    }
                }
            }
            
            return active;
        }
        
        /// <summary>
        /// Get zones with active work (for targeted scanning)
        /// </summary>
        public List<WorkZone> GetActiveZones()
        {
            var activeZones = new List<WorkZone>();
            
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    if (zones[x, z].HasActiveWork())
                    {
                        activeZones.Add(zones[x, z]);
                    }
                }
            }
            
            return activeZones;
        }
        
        private void ScanConstructionWork()
        {
            // Scan building frames
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingFrame))
            {
                AddWorkToZone(thing.Position, WorkTypeDefOf.Construction, urgency: 4f);
            }
            
            // Scan blueprints
            foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Blueprint))
            {
                AddWorkToZone(thing.Position, WorkTypeDefOf.Construction, urgency: 3f);
            }
            
            // Scan damaged buildings (repair) - use allBuildingsColonist instead
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                if (building != null && building.def.useHitPoints && building.HitPoints < building.MaxHitPoints)
                {
                    float damage = 1f - (building.HitPoints / (float)building.MaxHitPoints);
                    if (damage > 0.3f) // Only significant damage
                    {
                        AddWorkToZone(building.Position, WorkTypeDefOf.Construction, urgency: damage * 10f);
                    }
                }
            }
        }
        
        private void ScanMiningWork()
        {
            if (map.designationManager?.AllDesignations == null)
                return;
            
            foreach (var des in map.designationManager.AllDesignations)
            {
                if (des.def == DesignationDefOf.Mine)
                {
                    AddWorkToZone(des.target.Cell, WorkTypeDefOf.Mining, urgency: 5f);
                }
            }
        }
        
        private void ScanBillWork()
        {
            foreach (var building in map.listerBuildings.allBuildingsColonist)
            {
                var billGiver = building as IBillGiver;
                if (billGiver?.BillStack != null)
                {
                    int activeBills = 0;
                    foreach (var bill in billGiver.BillStack.Bills)
                    {
                        if (bill.ShouldDoNow() && !bill.suspended)
                        {
                            activeBills++;
                        }
                    }
                    
                    if (activeBills > 0)
                    {
                        // Map to appropriate work type
                        WorkTypeDef workType = GetWorkTypeForBills(building);
                        AddWorkToZone(building.Position, workType, urgency: activeBills * 3f);
                    }
                }
            }
        }
        
        private void ScanPlantWork()
        {
            if (map.designationManager?.AllDesignations == null)
                return;
            
            foreach (var des in map.designationManager.AllDesignations)
            {
                if (des.def == DesignationDefOf.HarvestPlant)
                {
                    AddWorkToZone(des.target.Cell, WorkTypeDefOf.Growing, urgency: 4f);
                }
                else if (des.def == DesignationDefOf.CutPlant)
                {
                    var plantCutting = DefDatabase<WorkTypeDef>.GetNamedSilentFail("PlantCutting");
                    if (plantCutting != null)
                    {
                        AddWorkToZone(des.target.Cell, plantCutting, urgency: 2f);
                    }
                }
            }
        }
        
        private void ScanHaulingWork()
        {
            var haulables = map.listerHaulables?.ThingsPotentiallyNeedingHauling();
            if (haulables == null)
                return;
            
            int count = 0;
            foreach (var thing in haulables)
            {
                AddWorkToZone(thing.Position, WorkTypeDefOf.Hauling, urgency: 1f);
                count++;
                if (count >= 200) break; // Cap for performance
            }
        }
        
        private void AddWorkToZone(IntVec3 position, WorkTypeDef workType, float urgency)
        {
            if (workType == null)
                return;
            
            // Convert position to zone coordinates
            int zoneX = position.x / ZONE_SIZE;
            int zoneZ = position.z / ZONE_SIZE;
            
            if (zoneX >= 0 && zoneX < gridWidth && zoneZ >= 0 && zoneZ < gridHeight)
            {
                zones[zoneX, zoneZ].AddWork(workType, urgency);
            }
        }
        
        private WorkTypeDef GetWorkTypeForBills(Building building)
        {
            // Try to determine work type from building
            if (building.def.defName.Contains("Stove") || building.def.defName.Contains("Kitchen"))
                return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking") ?? WorkTypeDefOf.Crafting;
            if (building.def.defName.Contains("Tailor") || building.def.defName.Contains("Sewing"))
                return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Tailoring") ?? WorkTypeDefOf.Crafting;
            if (building.def.defName.Contains("Smith") || building.def.defName.Contains("Forge"))
                return WorkTypeDefOf.Smithing;
            if (building.def.defName.Contains("Butcher"))
                return DefDatabase<WorkTypeDef>.GetNamedSilentFail("Butchering") ?? WorkTypeDefOf.Crafting;
            
            return WorkTypeDefOf.Crafting; // Default
        }
        
        /// <summary>
        /// Get total active work count
        /// </summary>
        public int GetTotalWorkCount()
        {
            int total = 0;
            for (int x = 0; x < gridWidth; x++)
            {
                for (int z = 0; z < gridHeight; z++)
                {
                    total += zones[x, z].workCount;
                }
            }
            return total;
        }
        
        /// <summary>
        /// Get statistics
        /// </summary>
        public string GetStatistics()
        {
            int activeZones = GetActiveZones().Count;
            int totalZones = gridWidth * gridHeight;
            int workCount = GetTotalWorkCount();
            return $"Zones: {activeZones}/{totalZones} active, Work items: {workCount}";
        }
    }
    
    /// <summary>
    /// Single work zone (16x16 region)
    /// </summary>
    public class WorkZone
    {
        public int gridX;
        public int gridZ;
        public Dictionary<WorkTypeDef, float> workDemands = new Dictionary<WorkTypeDef, float>();
        public HashSet<WorkTypeDef> activeWorkTypes = new HashSet<WorkTypeDef>();
        public int workCount = 0;
        
        public WorkZone(int x, int z)
        {
            this.gridX = x;
            this.gridZ = z;
        }
        
        public void AddWork(WorkTypeDef workType, float urgency)
        {
            if (!workDemands.ContainsKey(workType))
            {
                workDemands[workType] = 0f;
            }
            
            workDemands[workType] += urgency;
            activeWorkTypes.Add(workType);
            workCount++;
        }
        
        public float GetDemand(WorkTypeDef workType)
        {
            return workDemands.TryGetValue(workType, out float demand) ? demand : 0f;
        }
        
        public bool HasActiveWork()
        {
            return workCount > 0;
        }
        
        public void Clear()
        {
            workDemands.Clear();
            activeWorkTypes.Clear();
            workCount = 0;
        }
    }
}

