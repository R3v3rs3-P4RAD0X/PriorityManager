# Priority Manager v2.0 - API Documentation

## Overview

This document describes the public API for Priority Manager v2.0 for mod developers who want to integrate with or extend the system.

## Breaking Changes from v1.x

| v1.x API | v2.0 API | Notes |
|----------|----------|-------|
| `PriorityAssigner.AssignPriorities()` | Still available | Now event-driven internally |
| `WorkScanner.ScoreWorkUrgency()` | `WorkZoneGrid.GetDemand()` | Spatial optimization |
| `PriorityManagerMapComponent` | `EventDispatcher` + `IncrementalUpdater` | Event-based architecture |

## Core Systems

### Event System

Subscribe to Priority Manager events:

```csharp
using PriorityManager.Events;

// Subscribe to colonist events
EventDispatcher.Instance.Subscribe<ColonistAddedEvent>(evt => {
    Log.Message($"Colonist added: {evt.pawn.Name}");
});

// Subscribe to health changes
EventDispatcher.Instance.Subscribe<HealthChangedEvent>(evt => {
    if (evt.becameIll) {
        Log.Message($"{evt.pawn.Name} became ill!");
    }
});

// Trigger recalculation
EventDispatcher.Instance.Dispatch(new RecalculateRequestEvent(force: true));
```

**Available Events:**
- `ColonistAddedEvent` - New colonist joins
- `ColonistRemovedEvent` - Colonist leaves/dies
- `HealthChangedEvent` - Health state changes
- `SkillChangedEvent` - Skill level changes
- `ColonistIdleEvent` - Colonist idle detected
- `JobDesignatedEvent` - Work designated
- `WorkCompletedEvent` - Work finished
- `RoleChangedEvent` - Role assignment changed
- `SettingsChangedEvent` - Settings modified
- `RecalculateRequestEvent` - Manual recalculation

### Priority Assignment

```csharp
using PriorityManager;

// Assign priorities for single colonist
PriorityAssigner.AssignPriorities(pawn, force: true);

// Assign priorities for all colonists
PriorityAssigner.AssignAllColonistPriorities(force: true);

// Set specific priority
PriorityAssigner.SetPriority(pawn, workType, priority: 1);

// Calculate work type score for colonist
float score = PriorityAssigner.CalculateWorkTypeScore(pawn, workType);
```

### Work Demand

```csharp
using PriorityManager.Spatial;
using PriorityManager.Assignment;

// Get work zone grid
var mapComp = map.GetComponent<PriorityManagerMapComponent>();
var workZoneGrid = mapComp.GetWorkZoneGrid();

// Get demand for specific work type
float demand = workZoneGrid.GetDemand(WorkTypeDefOf.Construction);

// Get all active work types
var activeWork = workZoneGrid.GetActiveWorkTypes();

// Calculate demand scores
var demands = DemandCalculator.CalculateAllDemands(colonists, workZoneGrid);
foreach (var kvp in demands) {
    Log.Message($"{kvp.Key.labelShort}: {kvp.Value.normalizedDemand:F1}");
}
```

### Coverage Guarantee

```csharp
using PriorityManager.Assignment;

// Ensure all jobs covered + scale by demand
CoverageGuarantee.EnsureCoverage(colonists, workZoneGrid);
```

### Idle Detection

```csharp
using PriorityManager.Assignment;

// Check if colonist is idle
bool idle = IdleRedirector.IsIdle(pawn);

// Get idle duration
int ticks = IdleRedirector.GetIdleDuration(pawn);

// Get all idle colonists
var idleColonists = IdleRedirector.GetIdleColonists();
```

### Performance Profiling

```csharp
using PriorityManager;

// Profile a code block
using (PerformanceProfiler.Profile("MyMethod")) {
    // Your code here
}

// Get profile data
var profileData = PerformanceProfiler.GetProfileData();
foreach (var kvp in profileData) {
    Log.Message($"{kvp.Key}: {kvp.Value.avgMs:F3}ms");
}

// Enable/disable profiler
PerformanceProfiler.Enabled = true/false;
```

### Benchmarking

```csharp
using PriorityManager;

// Run full benchmark suite
Benchmarks.RunAll();

// Quick performance test
Benchmarks.QuickTest();

// Get results
var results = Benchmarks.GetResults();
```

### Analytics

```csharp
using PriorityManager.Analytics;

// Get performance snapshot
var snapshot = PerformanceMonitor.Instance.GetCurrentSnapshot();
Log.Message($"FPS: {snapshot.fps:F1}, Memory: {snapshot.memoryUsedMB}MB");

// Export analytics to CSV
PerformanceMonitor.Instance.ExportToCSV();
```

### Machine Learning

```csharp
using PriorityManager.ML;

// Record manual adjustment (automatic when player changes priorities)
AssignmentPredictor.Instance.RecordManualAdjustment(pawn, workType, oldPriority, newPriority);

// Predict optimal priority
int predicted = AssignmentPredictor.Instance.PredictPriority(pawn, workType);

// Suggest best work for colonist
WorkTypeDef suggested = AssignmentPredictor.Instance.SuggestWork(pawn);

// Export training data
string path = Path.Combine(GenFilePaths.ConfigFolderPath, "ml_training.csv");
AssignmentPredictor.Instance.ExportTrainingData(path);
```

### Shift Scheduling

```csharp
using PriorityManager.Scheduling;

// Assign shift to colonist
ShiftScheduler.Instance.AssignShift(pawn, "night");

// Get colonist's shift
string shift = ShiftScheduler.Instance.GetShift(pawn);

// Activate emergency mode
ShiftScheduler.Instance.ActivateEmergencyMode(ShiftScheduler.EmergencyType.Fire);

// Deactivate emergency mode
ShiftScheduler.Instance.DeactivateEmergencyMode();
```

### Work Type Cache

```csharp
using PriorityManager;

// Get all visible work types (cached)
var visibleWorkTypes = WorkTypeCache.VisibleWorkTypes;

// Get work type by name (O(1) lookup)
WorkTypeDef workType = WorkTypeCache.GetByDefName("Construction");

// Get relevant skills for work type
List<SkillDef> skills = WorkTypeCache.GetRelevantSkills(workType);

// Check if visible
bool visible = WorkTypeCache.IsVisible(workType);
```

### Object Pooling

```csharp
using PriorityManager.Memory;

// Get list from pool
var list = ListPool<Pawn>.Get();
try {
    // Use list
    list.Add(pawn);
} finally {
    // Always return to pool
    ListPool<Pawn>.Return(list);
}

// Dictionary pool
var dict = DictionaryPool<WorkTypeDef, float>.Get();
try {
    dict[workType] = score;
} finally {
    DictionaryPool<WorkTypeDef, float>.Return(dict);
}

// Array pool
var array = ArrayPool<int>.Rent(100);
try {
    array[0] = 42;
} finally {
    ArrayPool<int>.Return(array, clearArray: true);
}
```

## Harmony Patches

### Existing Patches (Don't Re-Patch)

v2.0 already patches these methods:

- `Pawn.SpawnSetup` - Colonist spawn detection
- `Pawn.Kill` - Colonist death detection
- `Pawn_HealthTracker.AddHediff` - Health changes
- `Pawn_HealthTracker.RemoveHediff` - Healing
- `SkillRecord.Level` setter - Skill changes
- `DesignationManager.AddDesignation` - Work designation
- `MainTabWindow_Work.DoWindowContents` - UI integration (v1.x)

### Safe to Patch

If you need to interact with Priority Manager, these are safe:

- Postfix on `PriorityAssigner` methods (read data)
- Postfix on work completion methods (notify PM)
- Your own UI elements (don't overlap with PM windows)

### Unsafe to Patch

Avoid patching these (conflicts with v2.0):

- Pawn.workSettings.SetPriority (v2.0 manages this)
- Any tick methods (v2.0 already optimized)
- Event-related methods (v2.0 event system)

## Custom Work Types

v2.0 automatically handles custom work types from mods:

```csharp
// Your mod adds a new WorkTypeDef
WorkTypeDef myCustomWork = DefDatabase<WorkTypeDef>.GetNamed("MyModWork");

// Priority Manager will:
// 1. Detect it automatically (WorkTypeCache)
// 2. Include it in coverage guarantee
// 3. Scale workers based on demand
// 4. Show it in UI automatically

// No special integration needed!
```

## Data Access

### Colonist Role Data

```csharp
// Get colonist's role data
var gameComp = PriorityDataHelper.GetGameComponent();
var data = gameComp.GetData(pawn);

if (data != null) {
    RolePreset role = data.assignedRole;
    bool autoAssign = data.autoAssignEnabled;
    string customRoleId = data.customRoleId;
}
```

### Mod Settings

```csharp
// Access mod settings
var settings = PriorityManagerMod.settings;

// Check job importance
JobImportance importance = settings.GetJobImportance(workType);

// Check min/max workers
int minWorkers = settings.GetMinWorkersForJob(workType, colonistCount);
int maxWorkers = settings.GetMaxWorkersForJob(workType, colonistCount);

// Check always-enabled
bool alwaysEnabled = settings.IsJobAlwaysEnabled(workType);
```

## Best Practices

### Performance

1. **Use WorkTypeCache** instead of DefDatabase queries:
```csharp
// ❌ Slow
var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading;

// ✅ Fast (cached)
var allWorkTypes = WorkTypeCache.AllWorkTypes;
```

2. **Use Object Pooling** in hot paths:
```csharp
// ❌ Allocates
var list = new List<Pawn>();

// ✅ Pooled
var list = ListPool<Pawn>.Get();
// ... use ...
ListPool<Pawn>.Return(list);
```

3. **Profile your code**:
```csharp
using (PerformanceProfiler.Profile("MyMod.MyMethod")) {
    // Your code
}
```

### Events

1. **Subscribe early** (in StaticConstructorOnStartup or Mod constructor)
2. **Unsubscribe if needed** (though usually not necessary)
3. **Handle exceptions** in event handlers (don't crash the system)

### Thread Safety

1. **Don't modify shared state** in ParallelAssigner callbacks
2. **Use locks** if you must share data between threads
3. **Prefer immutable data** in parallel sections

## Examples

### Example 1: Notify PM of Custom Event

```csharp
// Your mod detects a significant event
public void OnMyCustomEvent(Pawn pawn) {
    // Notify Priority Manager to recalculate this colonist
    Events.EventDispatcher.Instance.Dispatch(
        new Events.RecalculateRequestEvent(force: false, specificPawn: pawn)
    );
}
```

### Example 2: Add Custom Job Scoring

```csharp
// Postfix on CalculateWorkTypeScore to boost your custom work
[HarmonyPatch(typeof(PriorityAssigner), nameof(PriorityAssigner.CalculateWorkTypeScore))]
public static class MyMod_WorkScorePatch {
    static void Postfix(Pawn pawn, WorkTypeDef workType, ref float __result) {
        if (workType.defName == "MyCustomWork") {
            // Boost score based on custom logic
            if (MyMod.IsSpecialist(pawn)) {
                __result *= 2f; // Double the score
            }
        }
    }
}
```

### Example 3: Monitor Performance

```csharp
// Check if Priority Manager is causing lag
public void CheckPMPerformance() {
    var profileData = PerformanceProfiler.GetProfileData();
    
    if (profileData.TryGetValue("MapComponentTick", out var timing)) {
        if (timing.avgMs > 5.0) {
            Log.Warning($"Priority Manager tick handler slow: {timing.avgMs:F2}ms");
        }
    }
}
```

### Example 4: Custom Emergency Mode

```csharp
// Your mod adds a custom emergency
public void OnSolarFlare() {
    // You can use PM's emergency system or implement your own
    var scheduler = Scheduling.ShiftScheduler.Instance;
    
    // Or manually adjust priorities
    foreach (var pawn in colonists) {
        // Disable outdoor work during solar flare
        PriorityAssigner.SetPriority(pawn, WorkTypeDefOf.Growing, 0);
        PriorityAssigner.SetPriority(pawn, WorkTypeDefOf.Mining, 0);
    }
}
```

## Testing Your Integration

### Checklist

- [ ] Test with v2.0 active
- [ ] Test with both v1.x and v2.0 installed (only one enabled)
- [ ] Test with 50, 100, 200 colonists
- [ ] Profile your integration (`PerformanceProfiler.Profile()`)
- [ ] Check for memory leaks (run `PerformanceMonitor`)
- [ ] Test event subscriptions don't leak
- [ ] Verify thread safety if using parallel features

### Debug Commands

```csharp
// In dev console

// Run all tests
PriorityManager.Testing.StressTester.RunAllTests();

// Run compatibility tests
PriorityManager.Testing.IntegrationTester.RunCompatibilityTests();

// Run benchmarks
PriorityManager.Benchmarks.RunAll();

// Check statistics
Log.Message(Events.EventDispatcher.Instance.GetStatistics());
Log.Message(Assignment.ParallelAssigner.GetStatistics());
```

## Support

### Reporting Integration Issues

1. Open issue on GitHub with `[v2.0-integration]` tag
2. Include:
   - Your mod's package ID
   - Priority Manager version
   - Error logs
   - Steps to reproduce
   - Colony size when issue occurs

### Getting Help

- GitHub Discussions: Q&A about integration
- Documentation: Check this file and architecture docs
- Source Code: v2.0 is open source - read the implementation!

## Migration from v1.x Integration

### If Your Mod Used v1.x API

**Priority Assignment:**
```csharp
// v1.x and v2.0 - same
PriorityAssigner.AssignPriorities(pawn, force);

// v1.x
WorkScanner.ScoreWorkUrgency(map);

// v2.0
var mapComp = map.GetComponent<PriorityManagerMapComponent>();
var workZoneGrid = mapComp.GetWorkZoneGrid();
float demand = workZoneGrid.GetDemand(workType);
```

**Settings Access:**
```csharp
// v1.x and v2.0 - same
var settings = PriorityManagerMod.settings;
```

**Data Access:**
```csharp
// v1.x and v2.0 - same
var gameComp = PriorityDataHelper.GetGameComponent();
var data = gameComp.GetData(pawn);
```

Most v1.x APIs still work in v2.0! Main change is WorkScanner → WorkZoneGrid.

## Performance Guidelines

### Do's
✅ Use WorkTypeCache for work type queries  
✅ Use Object Pooling in frequently called code  
✅ Subscribe to events instead of polling  
✅ Profile your integration with PerformanceProfiler  
✅ Test with large colonies (100+)  

### Don'ts
❌ Don't call DefDatabase in hot paths  
❌ Don't create new collections in tick handlers  
❌ Don't poll for state changes (use events!)  
❌ Don't modify priorities every frame  
❌ Don't block in event handlers (keep them fast)  

## Version Detection

```csharp
// Detect if Priority Manager v2.0 is active
bool hasV2 = ModsConfig.IsActive("p4rad0x.prioritymanager.v2");

// Detect if v1.x is active
bool hasV1 = ModsConfig.IsActive("p4rad0x.prioritymanager");

// Check version number
string version = PriorityManagerMod.Instance.Content.ModMetaData.ModVersion;
```

## License

Priority Manager v2.0 is open source. See repository for license details.

## Credits

If you integrate with Priority Manager v2.0, please credit:
- Priority Manager v2.0 by P4RAD0X
- Link to GitHub repository

---

*Last updated: November 11, 2025*  
*API Version: 2.0.0*  
*Document Version: 1.0*


