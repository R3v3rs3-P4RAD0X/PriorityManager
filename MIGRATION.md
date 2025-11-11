# Migration Guide: Priority Manager v1.x → v2.0

## ⚠️ CRITICAL: Save Incompatibility

**Priority Manager v2.0 is NOT compatible with v1.x save files.**

If you load a v1.x save with v2.0 installed, your colonist priority data will be lost.

## Why the Incompatibility?

v2.0 is a **complete architectural rewrite** focused on performance for large colonies (100+ colonists). The data structures, save format, and internal systems are entirely different.

### What Changed

| Aspect | v1.x | v2.0 |
|--------|------|------|
| **Update System** | Polling every 250 ticks | Event-driven (reactive) |
| **Data Storage** | Dictionary<Pawn, Data> | Array-indexed cache |
| **Work Detection** | Full map scan | Spatial grid (16x16 regions) |
| **Caching** | None | Multi-layer with prediction |
| **UI Rendering** | Full redraw per frame | Virtual scrolling |
| **Parallel Processing** | None | Thread pools for large colonies |
| **Save Format** | XML with nested dictionaries | Compact binary format |

## Migration Paths

### Option 1: Start Fresh (Recommended)
1. Finish your current v1.x playthrough
2. Update to v2.0
3. Start a new colony with v2.0 installed

**Pros**: Clean start, best performance
**Cons**: Can't continue existing colony

### Option 2: Keep v1.x for Old Saves
1. Keep Priority Manager v1.3.2 for existing saves
2. Install v2.0 as a separate mod folder for new games
3. Disable one or the other before loading

**Pros**: Can play both old and new colonies
**Cons**: Need to manage two mod versions

### Option 3: Manual Conversion (Advanced)
There is no automated converter, but you can manually rebuild your colony's priorities:

1. Before updating, take screenshots of your v1.x settings:
   - Each colonist's role assignment
   - Custom roles (if created)
   - Job importance settings
   - Min/max worker settings

2. Update to v2.0

3. Load your save (priorities will be lost)

4. Reconfigure priorities using your screenshots

**Estimated time**: 5-15 minutes for medium colony (20 colonists)

## What Data is Lost?

When loading a v1.x save with v2.0:
- ❌ Colonist role assignments (Auto/Researcher/Doctor/etc.)
- ❌ Custom role definitions
- ❌ Job importance overrides
- ❌ Min/max worker settings per job
- ❌ Always-enabled jobs configuration
- ❌ Work history data

## What Data is Preserved?

RimWorld's native data is unaffected:
- ✅ Colonist skills and XP
- ✅ Work priorities (if manually set before)
- ✅ All pawns, buildings, items
- ✅ Story progression and quests
- ✅ Other mods' data

## Downgrade Path (v2.0 → v1.x)

If you want to go back to v1.x:

### Step 1: Install v1.3.2
```bash
git checkout v1.x-stable
# or restore from backup archive
```

### Step 2: Remove v2.0 Save Data
Your save file will have v2.0 data in it. When loaded with v1.x, it will be ignored (safe, no corruption).

### Step 3: Reconfigure
v1.x will see your colonists but won't have any role assignments. You'll need to set them up again.

## Version Detection

To check which version you're running:

1. Open Priority Manager settings (Mod Settings → Priority Manager)
2. Look at the version number at the top:
   - **v1.x**: "Priority Manager v1.3.2"
   - **v2.0**: "Priority Manager v2.0.0-alpha" (or higher)

Or check About.xml:
```xml
<modVersion>1.3.2</modVersion>  <!-- v1.x -->
<modVersion>2.0.0-alpha</modVersion>  <!-- v2.0 -->
```

## Performance Differences

### v1.x Performance (Baseline)
- 50 colonists: ~2-3ms per tick
- 100 colonists: ~8-12ms per tick (noticeable lag)
- 200+ colonists: Not recommended (>20ms per tick)

### v2.0 Target Performance
- 50 colonists: <0.5ms per tick
- 100 colonists: <1ms per tick
- 200 colonists: <2ms per tick
- 500 colonists: <5ms per tick

## Frequently Asked Questions

### Can I run both v1.x and v2.0?
Not simultaneously in the same mod list. You can have both installed in separate folders and enable only one.

### Will my v1.x saves corrupt if I load them with v2.0?
No, they won't corrupt. v2.0 will ignore v1.x data (and vice versa). However, all priority assignments will be lost and need to be reconfigured.

### When will v2.0 be stable?
v2.0 development plan:
- Week 1-3: Core architecture and optimization
- Week 4-6: Features and UI
- Week 7: Testing and polish
- **Estimated stable release**: ~2 months after start

### Should I update to v2.0 immediately?
**Only if**:
- Starting a new colony
- Have a large colony (100+ colonists) and experiencing lag
- Want to test alpha/beta features

**Stay on v1.x if**:
- Mid-playthrough with a colony you care about
- Want stability over new features
- Don't want to reconfigure priorities

### Where can I get v1.3.2 after v2.0 releases?
- Git branch: `v1.x-stable`
- Git tag: `v1.3.2-final`
- GitHub Releases: "v1.3.2 - Final Stable"
- Backup archive: Check releases page

## Support

### v1.x Issues
Open issue on GitHub with `[v1.x]` tag or checkout `v1.x-stable` branch.

### v2.0 Issues
Open issue on GitHub with `[v2.0]` tag or checkout `v2.0-dev` branch.

### Migration Help
If you have trouble with migration, open a discussion on GitHub with:
- Current version (v1.x or v2.0)
- Save file age/size
- Screenshots of settings (if available)

## Developer API Changes

If you're a mod developer integrating with Priority Manager:

### Removed in v2.0
- `PriorityAssigner.AssignPriorities()` (replaced with event system)
- `PriorityManagerMapComponent` (replaced with `EventDispatcher`)
- `WorkScanner.ScoreWorkUrgency()` (replaced with `WorkZoneGrid`)

### Added in v2.0
- `PriorityEventDispatcher.Subscribe()`
- `ColonistDataCache.GetCachedData()`
- `WorkZoneGrid.GetDemandScore()`
- `SmartAssigner.RequestAssignment()`

See `docs/v2-api.md` for full API documentation (coming in Phase 7).

