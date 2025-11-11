using System;
using System.Collections.Generic;
using System.Diagnostics;
using RimWorld;
using Verse;

namespace PriorityManager.Testing
{
    /// <summary>
    /// Stress testing utilities for large colonies
    /// v2.0: Validates performance with 500+ colonists
    /// </summary>
    public static class StressTester
    {
        private static List<TestResult> testResults = new List<TestResult>();
        
        public class TestResult
        {
            public string testName;
            public bool passed;
            public string failureReason;
            public double executionTimeMs;
            public int colonistCount;
            
            public override string ToString()
            {
                string status = passed ? "✅ PASS" : "❌ FAIL";
                string reason = passed ? "" : $" - {failureReason}";
                return $"{status} {testName} ({colonistCount} colonists, {executionTimeMs:F2}ms){reason}";
            }
        }
        
        /// <summary>
        /// Run full stress test suite
        /// </summary>
        public static void RunAllTests()
        {
            Log.Message("═══════════════════════════════════════════════");
            Log.Message("  PRIORITY MANAGER V2.0 - STRESS TEST SUITE");
            Log.Message("═══════════════════════════════════════════════");
            
            testResults.Clear();
            
            // Phase 1: Basic functionality
            TestEventSystem();
            TestCoverageGuarantee();
            TestIdleRedirection();
            
            // Phase 2: Performance tests
            TestTickPerformance();
            TestAssignmentPerformance();
            TestWorkScanningPerformance();
            
            // Phase 3: Large colony tests
            TestLargeColony();
            TestMemoryUsage();
            
            // Phase 4: Edge cases
            TestSingleColonist();
            TestAllColonistsDisabled();
            TestRapidStateChanges();
            
            // Print results
            PrintResults();
        }
        
        private static void TestEventSystem()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var dispatcher = Events.EventDispatcher.Instance;
                
                // Test event dispatch and processing
                for (int i = 0; i < 100; i++)
                {
                    dispatcher.Dispatch(new Events.RecalculateRequestEvent());
                }
                
                int queueSize = dispatcher.GetQueuedEventCount();
                
                if (queueSize != 100)
                {
                    RecordResult("Event System", false, $"Expected 100 events, got {queueSize}", sw.Elapsed.TotalMilliseconds, 0);
                    return;
                }
                
                // Process events
                dispatcher.ProcessEvents();
                
                RecordResult("Event System", true, null, sw.Elapsed.TotalMilliseconds, 0);
            }
            catch (Exception ex)
            {
                RecordResult("Event System", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestCoverageGuarantee()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                {
                    RecordResult("Coverage Guarantee", false, "No game component", sw.Elapsed.TotalMilliseconds, 0);
                    return;
                }
                
                var colonists = gameComp.GetAllColonists();
                if (colonists.Count == 0)
                {
                    RecordResult("Coverage Guarantee", false, "No colonists", sw.Elapsed.TotalMilliseconds, 0);
                    return;
                }
                
                var mapComp = Find.CurrentMap?.GetComponent<PriorityManagerMapComponent>();
                var workZoneGrid = mapComp?.GetWorkZoneGrid();
                
                // Run coverage guarantee
                Assignment.CoverageGuarantee.EnsureCoverage(colonists, workZoneGrid);
                
                // Verify all visible work types have at least 1 worker
                var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;
                int uncoveredJobs = 0;
                
                foreach (var workType in visibleWorkTypes)
                {
                    bool hasWorker = false;
                    
                    foreach (var pawn in colonists)
                    {
                        if (!pawn.WorkTypeIsDisabled(workType) && pawn.workSettings.GetPriority(workType) > 0)
                        {
                            hasWorker = true;
                            break;
                        }
                    }
                    
                    if (!hasWorker)
                    {
                        uncoveredJobs++;
                    }
                }
                
                bool passed = uncoveredJobs == 0;
                string reason = passed ? null : $"{uncoveredJobs} jobs uncovered";
                
                RecordResult("Coverage Guarantee", passed, reason, sw.Elapsed.TotalMilliseconds, colonists.Count);
            }
            catch (Exception ex)
            {
                RecordResult("Coverage Guarantee", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestIdleRedirection()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                {
                    RecordResult("Idle Redirection", false, "No game component", sw.Elapsed.TotalMilliseconds, 0);
                    return;
                }
                
                var colonists = gameComp.GetAllColonists();
                var mapComp = Find.CurrentMap?.GetComponent<PriorityManagerMapComponent>();
                var workZoneGrid = mapComp?.GetWorkZoneGrid();
                
                // Run idle redirector
                Assignment.IdleRedirector.MonitorAndRedirect(colonists, workZoneGrid);
                
                RecordResult("Idle Redirection", true, null, sw.Elapsed.TotalMilliseconds, colonists.Count);
            }
            catch (Exception ex)
            {
                RecordResult("Idle Redirection", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestTickPerformance()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var mapComp = Find.CurrentMap?.GetComponent<PriorityManagerMapComponent>();
                if (mapComp == null)
                {
                    RecordResult("Tick Performance", false, "No map component", 0, 0);
                    return;
                }
                
                var gameComp = PriorityDataHelper.GetGameComponent();
                int colonistCount = gameComp?.GetAllColonists()?.Count ?? 0;
                
                // Run tick handler 100 times
                for (int i = 0; i < 100; i++)
                {
                    mapComp.MapComponentTick();
                }
                
                sw.Stop();
                double avgTime = sw.Elapsed.TotalMilliseconds / 100.0;
                
                // Performance targets
                bool passed = true;
                string reason = null;
                
                if (colonistCount <= 50 && avgTime > 0.5)
                {
                    passed = false;
                    reason = $"Too slow for {colonistCount} colonists: {avgTime:F3}ms (target: <0.5ms)";
                }
                else if (colonistCount <= 100 && avgTime > 1.0)
                {
                    passed = false;
                    reason = $"Too slow for {colonistCount} colonists: {avgTime:F3}ms (target: <1ms)";
                }
                else if (colonistCount <= 200 && avgTime > 2.0)
                {
                    passed = false;
                    reason = $"Too slow for {colonistCount} colonists: {avgTime:F3}ms (target: <2ms)";
                }
                
                RecordResult("Tick Performance", passed, reason, avgTime, colonistCount);
            }
            catch (Exception ex)
            {
                RecordResult("Tick Performance", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestAssignmentPerformance()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                {
                    RecordResult("Assignment Performance", false, "No game component", 0, 0);
                    return;
                }
                
                var colonists = gameComp.GetAllColonists();
                int colonistCount = colonists.Count;
                
                // Run full assignment
                PriorityAssigner.AssignAllColonistPriorities(true);
                
                sw.Stop();
                
                // Performance targets
                bool passed = true;
                string reason = null;
                
                if (colonistCount <= 100 && sw.Elapsed.TotalMilliseconds > 10)
                {
                    passed = false;
                    reason = $"Too slow: {sw.Elapsed.TotalMilliseconds:F2}ms (target: <10ms)";
                }
                else if (colonistCount <= 200 && sw.Elapsed.TotalMilliseconds > 20)
                {
                    passed = false;
                    reason = $"Too slow: {sw.Elapsed.TotalMilliseconds:F2}ms (target: <20ms)";
                }
                
                RecordResult("Assignment Performance", passed, reason, sw.Elapsed.TotalMilliseconds, colonistCount);
            }
            catch (Exception ex)
            {
                RecordResult("Assignment Performance", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestWorkScanningPerformance()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var mapComp = Find.CurrentMap?.GetComponent<PriorityManagerMapComponent>();
                var workZoneGrid = mapComp?.GetWorkZoneGrid();
                
                if (workZoneGrid == null)
                {
                    RecordResult("Work Scanning", false, "No work zone grid", 0, 0);
                    return;
                }
                
                // Run work zone update
                workZoneGrid.Update();
                
                sw.Stop();
                
                // Should be very fast with spatial optimization
                bool passed = sw.Elapsed.TotalMilliseconds < 5.0;
                string reason = passed ? null : $"Too slow: {sw.Elapsed.TotalMilliseconds:F2}ms (target: <5ms)";
                
                RecordResult("Work Scanning", passed, reason, sw.Elapsed.TotalMilliseconds, 0);
            }
            catch (Exception ex)
            {
                RecordResult("Work Scanning", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestLargeColony()
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
            {
                Log.Warning("[StressTester] Skipping large colony test - no game");
                return;
            }
            
            int colonistCount = gameComp.GetAllColonists().Count;
            
            if (colonistCount < 100)
            {
                Log.Warning($"[StressTester] Skipping large colony test - only {colonistCount} colonists (need 100+)");
                return;
            }
            
            var sw = Stopwatch.StartNew();
            
            try
            {
                // Full system test with large colony
                PriorityAssigner.AssignAllColonistPriorities(true);
                
                sw.Stop();
                
                bool passed = sw.Elapsed.TotalMilliseconds < 50; // 50ms for 100+
                string reason = passed ? null : $"Too slow for large colony: {sw.Elapsed.TotalMilliseconds:F2}ms";
                
                RecordResult("Large Colony Test", passed, reason, sw.Elapsed.TotalMilliseconds, colonistCount);
            }
            catch (Exception ex)
            {
                RecordResult("Large Colony Test", false, ex.Message, sw.Elapsed.TotalMilliseconds, colonistCount);
            }
        }
        
        private static void TestMemoryUsage()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                long startMemory = GC.GetTotalMemory(true);
                
                // Run assignment 10 times
                for (int i = 0; i < 10; i++)
                {
                    PriorityAssigner.AssignAllColonistPriorities(true);
                }
                
                long endMemory = GC.GetTotalMemory(false);
                long increase = (endMemory - startMemory) / (1024 * 1024); // Convert to MB
                
                // Should have minimal memory increase (object pooling)
                bool passed = increase < 5; // Less than 5MB increase for 10 runs
                string reason = passed ? null : $"Memory increased by {increase}MB (target: <5MB)";
                
                var gameComp = PriorityDataHelper.GetGameComponent();
                int colonistCount = gameComp?.GetAllColonists()?.Count ?? 0;
                
                RecordResult("Memory Usage", passed, reason, sw.Elapsed.TotalMilliseconds, colonistCount);
            }
            catch (Exception ex)
            {
                RecordResult("Memory Usage", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestSingleColonist()
        {
            // This test only runs if there's exactly 1 colonist
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null || gameComp.GetAllColonists().Count != 1)
            {
                Log.Warning("[StressTester] Skipping single colonist test - not applicable");
                return;
            }
            
            var sw = Stopwatch.StartNew();
            
            try
            {
                var colonists = gameComp.GetAllColonists();
                PriorityAssigner.AssignPriorities(colonists[0], true);
                
                bool passed = true;
                RecordResult("Single Colonist", passed, null, sw.Elapsed.TotalMilliseconds, 1);
            }
            catch (Exception ex)
            {
                RecordResult("Single Colonist", false, ex.Message, sw.Elapsed.TotalMilliseconds, 1);
            }
        }
        
        private static void TestAllColonistsDisabled()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var gameComp = PriorityDataHelper.GetGameComponent();
                if (gameComp == null)
                {
                    RecordResult("All Disabled", false, "No game component", 0, 0);
                    return;
                }
                
                // Disable auto-assign for all, should handle gracefully
                var colonists = gameComp.GetAllColonists();
                foreach (var pawn in colonists)
                {
                    var data = gameComp.GetOrCreateData(pawn);
                    if (data != null)
                    {
                        data.autoAssignEnabled = false;
                    }
                }
                
                // Should not crash when all disabled
                PriorityAssigner.AssignAllColonistPriorities(true);
                
                RecordResult("All Disabled", true, null, sw.Elapsed.TotalMilliseconds, colonists.Count);
                
                // Re-enable
                foreach (var pawn in colonists)
                {
                    var data = gameComp.GetData(pawn);
                    if (data != null)
                    {
                        data.autoAssignEnabled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                RecordResult("All Disabled", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void TestRapidStateChanges()
        {
            var sw = Stopwatch.StartNew();
            
            try
            {
                var dispatcher = Events.EventDispatcher.Instance;
                var gameComp = PriorityDataHelper.GetGameComponent();
                
                if (gameComp == null)
                {
                    RecordResult("Rapid State Changes", false, "No game component", 0, 0);
                    return;
                }
                
                var colonists = gameComp.GetAllColonists();
                if (colonists.Count == 0)
                {
                    RecordResult("Rapid State Changes", false, "No colonists", 0, 0);
                    return;
                }
                
                // Simulate rapid events
                for (int i = 0; i < 50; i++)
                {
                    dispatcher.Dispatch(new Events.RecalculateRequestEvent(force: false, colonists[0]));
                }
                
                // Process events
                dispatcher.ProcessEvents();
                
                bool passed = true;
                RecordResult("Rapid State Changes", passed, null, sw.Elapsed.TotalMilliseconds, colonists.Count);
            }
            catch (Exception ex)
            {
                RecordResult("Rapid State Changes", false, ex.Message, sw.Elapsed.TotalMilliseconds, 0);
            }
        }
        
        private static void RecordResult(string testName, bool passed, string failureReason, double timeMs, int colonistCount)
        {
            testResults.Add(new TestResult
            {
                testName = testName,
                passed = passed,
                failureReason = failureReason,
                executionTimeMs = timeMs,
                colonistCount = colonistCount
            });
        }
        
        private static void PrintResults()
        {
            Log.Message("\n═══════════════════════════════════════════════");
            Log.Message("  TEST RESULTS");
            Log.Message("═══════════════════════════════════════════════");
            
            int passed = 0;
            int failed = 0;
            
            foreach (var result in testResults)
            {
                Log.Message(result.ToString());
                
                if (result.passed)
                    passed++;
                else
                    failed++;
            }
            
            Log.Message("═══════════════════════════════════════════════");
            Log.Message($"  SUMMARY: {passed} passed, {failed} failed");
            Log.Message("═══════════════════════════════════════════════\n");
            
            if (failed == 0)
            {
                Messages.Message("All stress tests passed! ✅", MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message($"Stress tests complete: {failed} failures detected", MessageTypeDefOf.NegativeEvent);
            }
        }
        
        /// <summary>
        /// Get test results
        /// </summary>
        public static List<TestResult> GetResults()
        {
            return new List<TestResult>(testResults);
        }
        
        /// <summary>
        /// Clear results
        /// </summary>
        public static void ClearResults()
        {
            testResults.Clear();
        }
    }
}

