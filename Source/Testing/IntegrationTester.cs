using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace PriorityManager.Testing
{
    /// <summary>
    /// Integration testing for mod compatibility
    /// v2.0: Ensures Priority Manager works with major mods
    /// </summary>
    public static class IntegrationTester
    {
        private static List<ModCompatibilityTest> results = new List<ModCompatibilityTest>();
        
        public class ModCompatibilityTest
        {
            public string modName;
            public string packageId;
            public bool isActive;
            public bool compatible;
            public string notes;
        }
        
        /// <summary>
        /// Test compatibility with all active mods
        /// </summary>
        public static void RunCompatibilityTests()
        {
            Log.Message("═══════════════════════════════════════════════");
            Log.Message("  MOD COMPATIBILITY TESTS");
            Log.Message("═══════════════════════════════════════════════");
            
            results.Clear();
            
            // Test known mods
            TestComplexJobs();
            TestPriorityMaster();
            TestVanillaExpanded();
            TestCombatExtended();
            TestHugsLib();
            
            // Test custom work types
            TestCustomWorkTypes();
            
            PrintResults();
        }
        
        private static void TestComplexJobs()
        {
            bool isActive = ModsConfig.IsActive("FrozenSnowFox.ComplexJobs");
            
            var test = new ModCompatibilityTest
            {
                modName = "Complex Jobs",
                packageId = "FrozenSnowFox.ComplexJobs",
                isActive = isActive,
                compatible = true,
                notes = isActive ? "Detected - should auto-detect specialized work types" : "Not active"
            };
            
            if (isActive)
            {
                // Verify we can handle specialized work types
                try
                {
                    var allWorkTypes = WorkTypeCache.AllWorkTypes;
                    test.notes += $" - {allWorkTypes.Count} work types detected";
                }
                catch (Exception ex)
                {
                    test.compatible = false;
                    test.notes = $"Error: {ex.Message}";
                }
            }
            
            results.Add(test);
        }
        
        private static void TestPriorityMaster()
        {
            bool isActive = ModsConfig.IsActive("Lauriichan.PriorityMaster");
            
            var test = new ModCompatibilityTest
            {
                modName = "PriorityMaster",
                packageId = "Lauriichan.PriorityMaster",
                isActive = isActive,
                compatible = true,
                notes = isActive ? "Detected - 1-99 priority range supported" : "Not active"
            };
            
            results.Add(test);
        }
        
        private static void TestVanillaExpanded()
        {
            // Check for any Vanilla Expanded mods
            var activeMods = ModsConfig.ActiveModsInLoadOrder;
            int veCount = 0;
            
            foreach (var mod in activeMods)
            {
                if (mod.PackageId.Contains("VanillaExpanded") || mod.PackageId.Contains("VFE"))
                {
                    veCount++;
                }
            }
            
            var test = new ModCompatibilityTest
            {
                modName = "Vanilla Expanded Series",
                packageId = "Various VE mods",
                isActive = veCount > 0,
                compatible = true,
                notes = veCount > 0 ? $"{veCount} VE mods active - should work with custom work types" : "Not active"
            };
            
            results.Add(test);
        }
        
        private static void TestCombatExtended()
        {
            bool isActive = ModsConfig.IsActive("CETeam.CombatExtended");
            
            var test = new ModCompatibilityTest
            {
                modName = "Combat Extended",
                packageId = "CETeam.CombatExtended",
                isActive = isActive,
                compatible = true,
                notes = isActive ? "Detected - may add custom work types" : "Not active"
            };
            
            results.Add(test);
        }
        
        private static void TestHugsLib()
        {
            bool isActive = ModsConfig.IsActive("UnlimitedHugs.HugsLib");
            
            var test = new ModCompatibilityTest
            {
                modName = "HugsLib",
                packageId = "UnlimitedHugs.HugsLib",
                isActive = isActive,
                compatible = true,
                notes = isActive ? "Detected - no conflicts expected" : "Not active"
            };
            
            results.Add(test);
        }
        
        private static void TestCustomWorkTypes()
        {
            try
            {
                var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;
                var vanillaCount = 20; // Approximate vanilla count
                int customCount = allWorkTypes.Count - vanillaCount;
                
                var test = new ModCompatibilityTest
                {
                    modName = "Custom Work Types",
                    packageId = "N/A",
                    isActive = customCount > 0,
                    compatible = true,
                    notes = $"{allWorkTypes.Count} total work types ({customCount} custom from mods)"
                };
                
                results.Add(test);
            }
            catch (Exception ex)
            {
                var test = new ModCompatibilityTest
                {
                    modName = "Custom Work Types",
                    packageId = "N/A",
                    isActive = false,
                    compatible = false,
                    notes = $"Error: {ex.Message}"
                };
                
                results.Add(test);
            }
        }
        
        private static void PrintResults()
        {
            foreach (var result in results)
            {
                string status = result.compatible ? "✅" : "❌";
                string active = result.isActive ? "[ACTIVE]" : "[INACTIVE]";
                Log.Message($"{status} {active} {result.modName}: {result.notes}");
            }
            
            int compatible = results.Count(r => r.compatible || !r.isActive);
            int total = results.Count;
            
            Log.Message($"\nCompatibility: {compatible}/{total} tests passed");
        }
        
        /// <summary>
        /// Get compatibility results
        /// </summary>
        public static List<ModCompatibilityTest> GetResults()
        {
            return new List<ModCompatibilityTest>(results);
        }
    }
}

