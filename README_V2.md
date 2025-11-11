# Priority Manager v2.0 - Development Version

## ⚠️ Alpha Status - Development in Progress

This is the v2.0 rewrite of Priority Manager, focused on performance optimization for large colonies (100+ colonists).

**Current Status**: Phase 0 Complete - Phase 1 in progress

## Separate Installation

Priority Manager v2 is intentionally in a separate directory to allow side-by-side installation with v1.x.

### Directory Structure
```
rimworld_mods/
├── Priority Manager/        ← v1.3.2 (stable, main branch)
└── Priority Manager v2/     ← v2.0-alpha (dev, v2.0-dev branch)
```

### Git Worktree Setup
This directory is a git worktree linked to the `v2.0-dev` branch:
```bash
# From Priority Manager directory:
git worktree list
# Shows both main and v2.0-dev worktrees
```

### Installation for Testing

1. **Option A**: Symlink to RimWorld mods folder
```bash
ln -s "/home/p4rad0x/.projects/rimworld_mods/Priority Manager v2" \
      "~/.steam/steam/steamapps/common/RimWorld/Mods/Priority Manager v2"
```

2. **Option B**: Copy to mods folder
```bash
cp -r "Priority Manager v2" \
      "~/.steam/steam/steamapps/common/RimWorld/Mods/"
```

### Using Both Versions

You can have both v1.x and v2.0 in your mod list, but:
- **Enable only ONE at a time**
- v1.x: Use for stable gameplay
- v2.0: Use for testing and development

They have different packageIds:
- v1.x: `p4rad0x.prioritymanager`
- v2.0: `p4rad0x.prioritymanager.v2`

## What's Different in v2.0?

### Architecture Changes
- **Event-driven updates** instead of polling every 250 ticks
- **Spatial data structures** (WorkZoneGrid) for efficient work scanning
- **Array-indexed caching** instead of dictionary lookups
- **Virtual scrolling** for UI with 500+ colonists
- **Parallel processing** for large colonies
- **Zero-allocation design** in hot paths

### Performance Targets
| Colony Size | v1.x | v2.0 Target |
|-------------|------|-------------|
| 50 colonists | 2-3ms/tick | <0.5ms/tick |
| 100 colonists | 8-12ms/tick | <1ms/tick |
| 200 colonists | >20ms/tick | <2ms/tick |
| 500 colonists | Unplayable | <5ms/tick |

### New Features (Planned)
- **Coverage Guarantee**: Ensures all jobs have at least one worker
- **Demand-Based Scaling**: Adds workers based on actual workload
- **Smart Idle Handling**: Automatically redirects idle colonists
- **Predictive Caching**: Learns patterns and pre-computes assignments
- **Performance Dashboard**: Real-time profiling overlay

## Development Status

### Completed Phases
- ✅ Phase 0: Backup & Setup
  - v1.x-stable branch created
  - Separate v2 directory with git worktree
  - Architecture documentation written

### Current Phase
- ✅ Phase 1: Profiling & Baseline Optimization (COMPLETE)
  - ✅ Performance instrumentation (PerformanceProfiler.cs)
  - ✅ Benchmarking framework (Benchmarks.cs)
  - ✅ Quick wins (WorkTypeCache.cs, tick optimization, LINQ replacement)

- ✅ Phase 2: Event-Driven Architecture (COMPLETE)
  - ✅ Event system with 10 event types (PriorityEvent.cs)
  - ✅ Event dispatcher with priority queues (EventDispatcher.cs)
  - ✅ Incremental updater with dirty tracking (IncrementalUpdater.cs)
  - ✅ Harmony patches for reactive updates (EventHarmonyPatches.cs)

- ✅ Phase 3: Data Structure Redesign (COMPLETE)
  - ✅ Array-indexed colonist cache (ColonistDataCache.cs)
  - ✅ Spatial work grid 16x16 regions (WorkZoneGrid.cs)
  - ✅ Object pooling for zero allocations (ObjectPool.cs)
  - ✅ Pre-computed skill rankings and work capabilities

- ✅ Phase 4: Algorithm Optimization (COMPLETE)
  - ✅ Coverage guarantee system (CoverageGuarantee.cs)
  - ✅ Demand calculator with scaling (DemandCalculator.cs)
  - ✅ Idle redirector with auto-assignment (IdleRedirector.cs)
  - ✅ Smart assigner with Hungarian/Greedy algorithms (SmartAssigner.cs)
  - ✅ Parallel processing for 50+ colonists (ParallelAssigner.cs)
  - ✅ Predictive caching with pattern learning (PredictiveCache.cs)

- ✅ Phase 5: UI Performance Overhaul (COMPLETE)
  - ✅ Virtual scrolling for colonist and job lists (VirtualScrollView.cs)
  - ✅ Deferred rendering with caching (DeferredRenderer)
  - ✅ UIStateManager with dirty tracking and debouncing
  - ✅ Frame rate limiting for non-critical updates
  - ✅ Integrated into ConfigWindow

### Upcoming Phases
- Phase 6: Advanced Features (Week 6)
- Phase 7: Testing & Polish (Week 7)

## Building

```bash
cd "/home/p4rad0x/.projects/rimworld_mods/Priority Manager v2"
make build
# or
cd Source && dotnet build
```

## Testing

### Performance Testing
```bash
# Run benchmarks (coming in Phase 1)
make benchmark
```

### Integration Testing
Test with these mods enabled:
- Complex Jobs
- PriorityMaster
- Vanilla Expanded series

## Save Compatibility

⚠️ **NOT COMPATIBLE WITH V1.X SAVES**

v2.0 uses a completely different data format. Loading a v1.x save with v2.0 will lose all priority data (but won't corrupt the save).

## Documentation

- **Architecture**: `docs/v2-architecture.md`
- **Migration Guide**: `MIGRATION.md` (in v1.x directory)
- **Backup Info**: `BACKUP_INFO.md` (in v1.x directory)

## Development Guidelines

### Code Style
- Avoid LINQ in hot paths (methods called every tick)
- Use array indexing over dictionary lookups
- Return pooled objects to ObjectPool
- Profile optimizations with Stopwatch

### Git Workflow
```bash
# Work in v2 directory
cd "Priority Manager v2"
git add .
git commit -m "Your message"
git push origin v2.0-dev

# Switch to v1.x
cd "../Priority Manager"
git checkout main
```

### Branching
- `main`: v1.x stable (frozen, no new features)
- `v1.x-stable`: v1.3.2 backup
- `v2.0-dev`: v2.0 development (current)
- Feature branches: `v2-feature-xyz` off `v2.0-dev`

## Troubleshooting

### "Mod conflict detected"
If RimWorld complains about duplicate mods:
- Check that v1.x and v2.0 have different packageIds
- Ensure only one is enabled in mod list

### Build errors
```bash
# Clean and rebuild
make clean
make build
```

### Git worktree issues
```bash
# List worktrees
cd "Priority Manager"
git worktree list

# Remove worktree if needed
git worktree remove "../Priority Manager v2"

# Re-add
git worktree add "../Priority Manager v2" v2.0-dev
```

## Support

For v2.0 issues:
- Open GitHub issue with `[v2.0]` tag
- Include: Phase number, error logs, colony size
- Mention if issue occurs with specific mods

For v1.x issues:
- Use v1.x-stable branch
- Open issue with `[v1.x]` tag

## Contributing

Want to help with v2.0 development?

1. Fork the repository
2. Create feature branch from `v2.0-dev`
3. Implement feature with performance profiling
4. Write tests for new functionality
5. Submit PR with benchmark results

## License

Same as v1.x - see LICENSE file in repository root.

## Credits

- Original Priority Manager: P4RAD0X
- v2.0 Rewrite: P4RAD0X
- Architecture inspired by: Source Engine, Unity ECS, RimWorld performance mods

