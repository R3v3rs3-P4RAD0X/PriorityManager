# Priority Manager v2.0 - Architecture Documentation

## Overview

v2.0 is a complete rewrite focused on performance optimization for large colonies (100+ colonists). The architecture shifts from polling-based updates to an event-driven system with aggressive caching and spatial data structures.

## Design Principles

1. **Event-Driven Over Polling**: React to changes instead of checking for them
2. **Cache Aggressively**: Pre-compute and cache expensive calculations
3. **Spatial Optimization**: Use region-based scanning instead of full-map iterations
4. **Zero Allocation**: Pool objects in hot paths to reduce GC pressure
5. **Incremental Updates**: Spread work across multiple ticks to avoid lag spikes
6. **Parallel When Possible**: Use multi-threading for independent calculations

## Core Architecture

### Event System (`Source/Events/`)

**PriorityEvent.cs**: Base event types
- `ColonistAdded`: New colonist joined colony
- `ColonistRemoved`: Colonist died/left
- `HealthChanged`: Hediff added/removed
- `SkillChanged`: Skill level increased
- `WorkCompleted`: Job finished
- `JobDesignated`: New work designated (construction, mining, etc.)

**EventQueue**: Priority queue for batched event processing
- High priority: Health changes, colonist add/remove
- Medium priority: Skill changes, work completion
- Low priority: Job designation updates

**Event Dispatcher**: Subscriber registry and event distribution
- Subscribers register callbacks for event types
- Events processed in batches during tick handler
- Target: <100μs per event

### Data Layer (`Source/Data/`)

**ColonistDataCache.cs**: High-performance colonist data storage
```csharp
// Array-indexed instead of dictionary
private ColonistCacheEntry[] colonistCache;  // [ThingID → index]
private float[][] workScores;                // [colonistIdx][workTypeIdx]
private int[][] skillRankings;               // [colonistIdx][skillIdx]
private bool[][] workCapabilities;           // [colonistIdx][workTypeIdx]
```

Benefits:
- O(1) lookup instead of O(log n)
- Cache-friendly memory layout
- Batch updates without allocations

**WorkTypeCache.cs**: Cached work type metadata
```csharp
private WorkTypeDef[] allWorkTypes;          // Cached DefDatabase query
private SkillDef[][] relevantSkills;         // [workTypeIdx][skillIdx]
private int[] naturalPriorities;             // [workTypeIdx]
private bool[][] compatibility;              // [colonistIdx][workTypeIdx]
```

### Spatial Data (`Source/Spatial/`)

**WorkZoneGrid.cs**: 16x16 region-based work tracking
```
Map divided into regions:
┌────────┬────────┬────────┐
│  R00   │  R01   │  R02   │  Region = 16x16 cells
├────────┼────────┼────────┤
│  R10   │  R11   │  R12   │  Track per region:
├────────┼────────┼────────┤  - Construction frames
│  R20   │  R21   │  R22   │  - Active bills
└────────┴────────┴────────┘  - Hauling items
```

Benefits:
- Only scan regions with active work
- Update regions incrementally on designation changes
- 100x faster than full-map scans

### Assignment System (`Source/Assignment/`)

**CoverageGuarantee.cs**: Ensures all jobs covered
```
Phase 1: Coverage
- Assign 1 colonist to each job type
- Choose colonist with lowest skill penalty
- Result: Every job has someone who can do it

Phase 2: Scaling
- Calculate demand per job (from WorkZoneGrid)
- Add workers proportionally to demand
- Handle idle colonists by assigning to high-demand jobs
```

**DemandCalculator.cs**: Real-time work demand scoring
```csharp
float DemandScore = (pending_work / worker_capacity) * urgency_multiplier
```

Examples:
- 50 bills with 2 crafters: 50/2 * 1.0 = 25.0 (very high demand)
- 5 constructions with 3 builders: 5/3 * 1.0 = 1.67 (moderate)
- 0 research with 1 researcher: 0/1 * 1.0 = 0.0 (no demand)

**SmartAssigner.cs**: Optimal assignment algorithm
- Hungarian algorithm for perfect matching (small colonies <50)
- Greedy approximation for large colonies (100+)
- Stability bias: prefer keeping current assignments
- Cache results, only recalculate on significant changes

**IdleRedirector.cs**: Automatic idle handling
- Monitor job states (tracked via events, not polling)
- Detect idle > 500 ticks without meaningful work
- Temporarily boost priorities to overloaded jobs
- Revert after work queue clears

### UI Layer (`Source/UI/`)

**VirtualScrollView.cs**: Efficient list rendering
```
Viewport (visible area)
┌─────────────────────┐
│ Colonist 48  ← render
│ Colonist 49  ← render
│ Colonist 50  ← render
│ Colonist 51  ← render  Buffer zone
│ Colonist 52          (pre-render +5)
└─────────────────────┘
  Colonist 53...497    (not rendered)
```

Benefits:
- Only render ~10 rows instead of 500
- Reuse row GameObjects (object pooling)
- 60 FPS regardless of colony size

**UIStateManager.cs**: Dirty region tracking
- Track which UI sections changed
- Only redraw dirty regions
- Batch updates to once per frame
- Debounce input events (300ms delay)

### Memory Management (`Source/Memory/`)

**ObjectPool.cs**: Pre-allocated object pools
```csharp
// Instead of:
var list = new List<Pawn>();  // GC allocation

// Use:
var list = ListPool<Pawn>.Get();  // Pooled, zero allocation
// ... use list ...
ListPool<Pawn>.Return(list);      // Return to pool
```

Pooled objects:
- `List<T>`, `Dictionary<K,V>`, `HashSet<T>`
- Result structures (WorkScore, Assignment, etc.)
- UI intermediate buffers

Target: Zero allocations in tick handler

## Performance Targets

### Tick Handler
| Colony Size | v1.x | v2.0 Target | Improvement |
|-------------|------|-------------|-------------|
| 50 colonists | 2-3ms | <0.5ms | 4-6x faster |
| 100 colonists | 8-12ms | <1ms | 8-12x faster |
| 200 colonists | >20ms | <2ms | 10x faster |
| 500 colonists | N/A (unplayable) | <5ms | Newly viable |

### UI Frame Time
| Operation | v1.x | v2.0 Target |
|-----------|------|-------------|
| Scroll colonist list | 5-10ms | <1ms |
| Open dashboard | 15-20ms | <2ms |
| Update metrics | 10ms | <1ms (worker thread) |

### Memory Overhead
| Colony Size | v1.x | v2.0 Target |
|-------------|------|-------------|
| 50 colonists | ~5MB | ~5MB |
| 100 colonists | ~10MB | ~12MB |
| 500 colonists | N/A | ~50MB |

## Implementation Phases

### Phase 0: Backup & Setup ✓
- v1.x-stable branch created
- v1.3.2-final tagged
- Backup archive created
- Migration docs written
- v2.0-dev branch created

### Phase 1: Profiling & Quick Wins (Week 1)
- Add performance instrumentation
- Benchmark current v1.x performance
- Apply immediate optimizations (caching, bailouts)
- Document baseline metrics

### Phase 2: Event System (Week 2)
- Implement event types and dispatcher
- Replace polling with reactive updates
- Add incremental updater with dirty tracking

### Phase 3: Data Structures (Week 3)
- Redesign ColonistDataCache with arrays
- Implement WorkTypeCache with lookup tables
- Add WorkZoneGrid for spatial optimization
- Create ObjectPool for memory management

### Phase 4: Algorithms (Week 4)
- Implement CoverageGuarantee system
- Add DemandCalculator for work scoring
- Create SmartAssigner with Hungarian algorithm
- Add IdleRedirector for idle handling
- Implement parallel processing

### Phase 5: UI (Week 5)
- Virtual scrolling for colonist list
- Deferred rendering with worker threads
- UIStateManager for dirty regions

### Phase 6: Advanced Features (Week 6)
- Machine learning predictions
- Performance analytics dashboard
- Shift scheduling

### Phase 7: Testing & Polish (Week 7)
- Performance testing with 500+ colonists
- Integration testing with other mods
- Documentation and API guide

## Key Differences from v1.x

| Aspect | v1.x | v2.0 |
|--------|------|------|
| **Update Frequency** | Every 250 ticks | Event-driven (on-demand) |
| **Colonist Iteration** | O(n²) nested loops | O(n) with cached scores |
| **Work Detection** | Full map scan | Spatial grid (16x16 regions) |
| **Skill Calculations** | Recalculated each time | Pre-computed and cached |
| **UI Rendering** | Full redraw per frame | Virtual scrolling + dirty regions |
| **Memory Allocations** | ~100/tick | Target: 0/tick |
| **Parallelization** | None | Multi-threaded for 50+ colonists |
| **Caching Strategy** | None | Multi-layer with prediction |

## Breaking Changes

### Save Format
v1.x uses XML with nested dictionaries. v2.0 uses compact binary format with array indices.

### API Changes
- `PriorityAssigner.AssignPriorities()` → `SmartAssigner.RequestAssignment()`
- `WorkScanner.ScoreWorkUrgency()` → `WorkZoneGrid.GetDemandScore()`
- `PriorityManagerMapComponent` → `EventDispatcher`

### Settings Structure
v1.x settings are preserved where possible, but custom roles may need recreation.

## Development Guidelines

### Code Style
- Use array indexing over dictionary lookups in hot paths
- Avoid LINQ in methods called every tick
- Pre-allocate collections with known sizes
- Return pooled objects to pool after use
- Add performance comments for non-obvious optimizations

### Testing
- Profile every optimization with Stopwatch
- Test with 500+ colonist scenarios
- Verify no memory leaks (GC.GetTotalMemory)
- Check compatibility with Complex Jobs, PriorityMaster

### Documentation
- Update this file as architecture evolves
- Document breaking changes in MIGRATION.md
- Add XML comments to public APIs
- Include performance notes in code comments

## Future Considerations

### v2.1+ Features
- Colonist mood-based priority adjustments
- Work schedule templates (day/night shifts)
- Priority presets per season (harvest mode)
- Integration with colonist needs (recreation, sleep)
- Multi-map support for Multiplayer mod

### Performance Targets
- <1ms tick time for 1000 colonists
- Real-time optimization during gameplay
- Predictive assignment before work appears
- Zero perceivable lag in UI

## References

- Hungarian Algorithm: https://en.wikipedia.org/wiki/Hungarian_algorithm
- Object Pooling: https://source.dot.net/#System.Buffers.ArrayPool%601
- Spatial Partitioning: https://gameprogrammingpatterns.com/spatial-partition.html
- Dirty Regions: https://www.redblobgames.com/pathfinding/tower-defense/

