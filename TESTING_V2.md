# Priority Manager v2.0 - Testing Guide

## Quick Test Commands

### Dev Console (F12)

**Run all stress tests:**
```
PriorityManager.Testing.StressTester.RunAllTests();
```

**Run compatibility tests:**
```
PriorityManager.Testing.IntegrationTester.RunCompatibilityTests();
```

**Run performance benchmarks:**
```
PriorityManager.Benchmarks.RunAll();
```

**Quick performance check:**
```
PriorityManager.Benchmarks.QuickTest();
```

## Testing Phases

### Phase 1: Basic Functionality (30 minutes)

#### 1.1 Installation Test
- [ ] Mod loads without errors in log
- [ ] "Priority Manager v2" appears in mod list
- [ ] Can open settings (Options → Mod Settings)
- [ ] Can open ConfigWindow (N key or Work tab button)

#### 1.2 Single Colonist Test
- [ ] Start new colony (dev mode)
- [ ] Verify priorities assigned automatically
- [ ] Check all jobs have priority > 0 (coverage guarantee)
- [ ] Manually change priority → ML records it
- [ ] Toggle auto-assign on/off

#### 1.3 Small Colony Test (5-10 colonists)
- [ ] Add 5-10 colonists (dev mode spawn)
- [ ] Verify coverage: All jobs have 1+ workers
- [ ] Designate 20 construction frames
- [ ] Check: More builders assigned automatically
- [ ] Complete construction → builders scale down

#### 1.4 Role Assignment Test
- [ ] Assign different roles to colonists
- [ ] Verify role presets work (Researcher, Doctor, etc.)
- [ ] Create custom role
- [ ] Assign custom role to colonist
- [ ] Verify priorities match custom role

### Phase 2: Coverage Guarantee (20 minutes)

#### 2.1 All Jobs Covered
- [ ] Colony of 10 colonists with varied skills
- [ ] Force recalculate (ConfigWindow button)
- [ ] Check Work tab: Every job type should have green check for someone
- [ ] Even low-skill jobs (Art with passion: None) should be assigned

#### 2.2 Unskilled Assignment
- [ ] Remove skill XP from all colonists (dev mode)
- [ ] Force recalculate
- [ ] Verify: Still assigns workers to all jobs
- [ ] Workers chosen by "least bad" option

#### 2.3 Disabled Work Types
- [ ] Disable Growing for all colonists (incapable)
- [ ] Force recalculate
- [ ] Verify: Growing shows as "uncovered" in logs (expected)
- [ ] Other jobs still covered

### Phase 3: Demand Scaling (30 minutes)

#### 3.1 Construction Demand
- [ ] Designate 50 building frames
- [ ] Wait 500 ticks (or force recalc)
- [ ] Count builders: Should be 20-40% of colony
- [ ] Complete 25 frames → half done
- [ ] Wait 500 ticks
- [ ] Check: Should reduce builders to 10-20%

#### 3.2 Crafting Demand
- [ ] Queue 30 bills at crafting bench
- [ ] Wait 500 ticks
- [ ] Count crafters: Should scale to workload
- [ ] Compare to research (0 bills): Should have fewer workers

#### 3.3 Multiple High-Demand Jobs
- [ ] 30 construction frames + 30 crafting bills + 20 hauling
- [ ] Force recalculate
- [ ] Verify: Workers distributed proportionally
- [ ] Construction 30%, Crafting 30%, Hauling 20%, Others 20%

### Phase 4: Idle Handling (20 minutes)

#### 4.1 Idle Detection
- [ ] Builder finishes all construction
- [ ] Wait 500 ticks (idle threshold)
- [ ] Check logs: Should show "idle detected"
- [ ] Verify: Builder redirected to other work

#### 4.2 Idle with High Demand
- [ ] Complete all hauling
- [ ] Haulers become idle
- [ ] Designate 50 new constructions
- [ ] Wait 500 ticks
- [ ] Verify: Haulers redirected to construction

#### 4.3 Idle with No Work
- [ ] Complete ALL work (no designations, no bills)
- [ ] Colonists become idle
- [ ] Verify: No crashes, colonists maintain coverage assignments

### Phase 5: Performance Testing (45 minutes)

#### 5.1 Enable Performance Overlay
- [ ] Dev mode enabled
- [ ] Mod settings → Check "Show performance overlay"
- [ ] Verify: Overlay appears top-right
- [ ] Check colors: Green = good, Yellow = ok, Red = slow

#### 5.2 Small Colony Performance (20 colonists)
- [ ] Run benchmark: `Benchmarks.RunAll()`
- [ ] Check results:
  - [ ] MapComponentTick < 0.5ms
  - [ ] AssignAllPriorities < 5ms
  - [ ] WorkZoneGrid.Update < 1ms
- [ ] Check FPS: Should stay 60+

#### 5.3 Medium Colony Performance (50 colonists)
- [ ] Spawn 50 colonists (dev mode)
- [ ] Run benchmark
- [ ] Check results:
  - [ ] MapComponentTick < 1ms
  - [ ] AssignAllPriorities < 10ms (parallel should kick in)
- [ ] Check FPS: Should stay 60+

#### 5.4 Large Colony Performance (100+ colonists)
- [ ] Spawn 100 colonists
- [ ] Run benchmark
- [ ] Check results:
  - [ ] MapComponentTick < 1.5ms
  - [ ] Parallel processing active (check logs)
- [ ] Check FPS: Should stay 50-60

#### 5.5 Mega Colony Performance (200+ colonists)
- [ ] Spawn 200 colonists (dev mode: spawn 100 x2)
- [ ] Run benchmark
- [ ] Check results:
  - [ ] MapComponentTick < 2ms
  - [ ] AssignAllPriorities < 20ms
- [ ] Check FPS: Should stay 40-60

### Phase 6: UI Testing (20 minutes)

#### 6.1 Virtual Scrolling
- [ ] 100+ colonists
- [ ] Open ConfigWindow → Colonists tab
- [ ] Scroll list up/down smoothly
- [ ] Verify: No lag during scrolling
- [ ] Check FPS: Should stay 60

#### 6.2 Dashboard Performance
- [ ] Open ConfigWindow → Dashboard
- [ ] Verify: Metrics display correctly
- [ ] Check: Updates smoothly (not every frame)
- [ ] Expand skill matrix with 100+ colonists
- [ ] Verify: Smooth scrolling

#### 6.3 Job Settings Virtual Scroll
- [ ] ConfigWindow → Job Settings
- [ ] Scroll job list (should be smooth)
- [ ] Edit job settings
- [ ] Verify: Changes apply correctly

### Phase 7: Advanced Features (30 minutes)

#### 7.1 ML Predictor
- [ ] Manually adjust 20+ priorities
- [ ] Open Analytics tab
- [ ] Check: Training samples increasing
- [ ] Check: Model trains after 20 samples
- [ ] Export training data (button)
- [ ] Verify: CSV created in Config folder

#### 7.2 Shift Scheduling
- [ ] Analytics tab → Shift Scheduler section
- [ ] Check: Current hour and season displayed
- [ ] Wait for time change (6 AM or 10 PM)
- [ ] Verify: Priorities adjust for shift change

#### 7.3 Emergency Modes
- [ ] Analytics tab → Emergency Mode Controls
- [ ] Click "Activate Fire Emergency"
- [ ] Check: All firefighters priority 1
- [ ] Click "Deactivate Emergency Mode"
- [ ] Verify: Normal priorities restored

- [ ] Test each emergency type:
  - [ ] Fire - firefighters boosted
  - [ ] Raid - doctors on standby
  - [ ] Epidemic - medical focus
  - [ ] Famine - food production priority

#### 7.4 Performance Analytics
- [ ] Analytics tab → Performance Monitor
- [ ] Check: Real-time FPS and memory display
- [ ] Check: System timings color-coded
- [ ] Let run for 5 minutes
- [ ] Check: Trend detection works
- [ ] Export performance data (button)
- [ ] Verify: CSV created

### Phase 8: Integration Testing (40 minutes)

#### 8.1 Complex Jobs Compatibility
- [ ] Install Complex Jobs mod
- [ ] Start new colony
- [ ] Verify: Specialized work types detected
- [ ] Check: Coverage includes Complex Jobs work types
- [ ] Run: `IntegrationTester.RunCompatibilityTests()`

#### 8.2 PriorityMaster Compatibility
- [ ] Install PriorityMaster
- [ ] Enable PM integration in settings
- [ ] Verify: 1-99 priority range works
- [ ] Check: Scaling applied correctly

#### 8.3 Vanilla Expanded
- [ ] Install any VE mod with custom work
- [ ] Start new colony
- [ ] Verify: Custom work types handled
- [ ] Run compatibility tests

#### 8.4 Multiple Mods
- [ ] Complex Jobs + PriorityMaster + VE
- [ ] Start new colony
- [ ] Verify: All work types detected
- [ ] Run full benchmark
- [ ] Check: Performance still good

### Phase 9: Stress Testing (30 minutes)

#### 9.1 Rapid Events
- [ ] Spawn/despawn 20 colonists rapidly (dev mode)
- [ ] Check: No crashes
- [ ] Check event queue: `EventDispatcher.Instance.GetStatistics()`
- [ ] Verify: Queue processes smoothly

#### 9.2 Extreme Workload
- [ ] Designate 200 construction frames
- [ ] Queue 100 crafting bills
- [ ] Add 100 hauling items
- [ ] Force recalculate
- [ ] Verify: Workers scale appropriately
- [ ] Check: No performance degradation

#### 9.3 Memory Leak Test
- [ ] Run colony for 10 in-game days
- [ ] Check Analytics → Performance Monitor
- [ ] Memory: Should stay relatively stable
- [ ] If increasing >50MB: Possible leak

#### 9.4 Long-Running Test
- [ ] Let colony run for 1+ in-game year
- [ ] Periodically check performance
- [ ] Verify: No FPS decline over time
- [ ] Check: Memory stable
- [ ] Review: `PriorityManager_Performance.log`

### Phase 10: Edge Cases (20 minutes)

#### 10.1 All Auto-Assign Disabled
- [ ] Disable auto-assign for all colonists
- [ ] Force recalculate
- [ ] Verify: No crashes, graceful handling

#### 10.2 All Manual Role
- [ ] Set all colonists to "Manual" role
- [ ] Verify: PM doesn't override manual priorities
- [ ] Check: Min/max workers still counted

#### 10.3 Rapid Role Changes
- [ ] Change colonist role 10 times quickly
- [ ] Verify: No errors, handles gracefully

#### 10.4 Save/Load Test
- [ ] Configure priorities, roles, settings
- [ ] Save game
- [ ] Load game
- [ ] Verify: All data restored correctly
- [ ] Check: No duplicate assignments

## Performance Targets

### Must Meet

| Colony Size | Target Time | Critical Threshold |
|-------------|-------------|-------------------|
| 50 colonists | <0.5ms/tick | <1ms |
| 100 colonists | <1ms/tick | <2ms |
| 200 colonists | <2ms/tick | <5ms |
| 500 colonists | <5ms/tick | <10ms |

### Should Meet

- UI: 60 FPS with any colony size
- Memory: <100MB overhead for 500 colonists
- Event processing: <100μs per event
- Work scanning: <5ms regardless of map size

## Known Issues to Test

### From v1.x
- Min/max worker settings persistence
- Job importance dropdown interactions
- Custom role save/load
- Priority Master scaling edge cases

### v2.0 Specific
- Virtual scrolling with rapid list changes
- Event queue overflow (50+ events/tick)
- Parallel processing with modded work types
- Cache invalidation timing

## Test Reporting

### Format
```
Test: [Test Name]
Colony Size: [N colonists]
Result: PASS/FAIL
Time: [Xms]
Notes: [Any observations]
```

### Example
```
Test: Coverage Guarantee
Colony Size: 25 colonists
Result: PASS
Time: 2.3ms
Notes: All 28 work types covered, 3 uncoverable (all colonists incapable)
```

## Automated Testing

### Run Full Suite
```csharp
// Run everything
PriorityManager.Testing.StressTester.RunAllTests();
PriorityManager.Testing.IntegrationTester.RunCompatibilityTests();
PriorityManager.Benchmarks.RunAll();

// Check results
var stressResults = PriorityManager.Testing.StressTester.GetResults();
foreach (var result in stressResults) {
    Log.Message(result.ToString());
}
```

## Bug Reporting

### Critical Bugs (Report Immediately)
- Crashes / exceptions
- Save corruption
- Performance worse than v1.x
- Data loss
- Harmony conflicts

### High Priority
- Coverage guarantee failures
- Demand scaling not working
- Idle detection false positives
- UI lag with large colonies
- Memory leaks

### Medium Priority
- ML predictions inaccurate
- Emergency mode not activating
- Analytics display issues
- Minor performance issues

### Low Priority
- UI polish
- Tooltip improvements
- Documentation errors
- Feature requests

## Success Criteria

### Must Pass (v2.0 Release Blocker)
- ✅ All stress tests pass
- ✅ Performance targets met
- ✅ No crashes in normal gameplay
- ✅ Coverage guarantee works
- ✅ Demand scaling works
- ✅ Idle redirection works
- ✅ Compatible with Complex Jobs
- ✅ Compatible with PriorityMaster

### Should Pass (Post-Release Fix)
- UI 60 FPS with 500 colonists
- No memory leaks over 10 days
- ML achieves 70%+ accuracy after 50 samples
- All mod compatibility tests pass

### Nice to Have (Future Versions)
- Advanced analytics features
- More ML training data
- Additional emergency modes
- Custom shift definitions

---

*Last updated: November 11, 2025*  
*Version: 2.0.0*  
*Status: Phase 7 - Ready for Testing*

