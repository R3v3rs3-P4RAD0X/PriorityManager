# Priority Manager v1.3.2 - Backup Information

## Version Details
- **Mod Version**: 1.3.2-final
- **RimWorld Version**: 1.5+
- **Backup Date**: November 11, 2025
- **Git Tag**: `v1.3.2-final`
- **Git Branch**: `v1.x-stable`

## Backup Locations

### Git Repository
- **Stable Branch**: `v1.x-stable` (remote: origin/v1.x-stable)
- **Main Branch**: `main` (commit: 6489444)
- **Tag**: `v1.3.2-final`

### File System
- **Archive**: `../backups/Priority Manager v1.3.2 - Backup.zip`
- **Contents**: Full source code, assemblies, About.xml, all documentation

## What's Included

### Core Features (v1.3.2)
- Automatic priority assignment based on colonist skills
- Role-based presets (Researcher, Doctor, Warrior, Builder, etc.)
- Custom role creation system
- Illness/injury response system
- Solo colonist survival mode
- Colony Efficiency Dashboard with real-time metrics
- Work demand scanning and idle colonist detection
- Job importance settings (Disabled, VeryLow, Low, Normal, High, Critical)
- Min/max worker constraints per job
- Always-enabled jobs configuration
- Work history tracking
- Skill matrix visualization
- Pending job queue display

### Files Backed Up
- All C# source files (14 files, ~8000 lines of code)
- Compiled assemblies (PriorityManager.dll, 0Harmony.dll)
- About.xml with mod metadata
- KeyBindings.xml (N key for config)
- Documentation (README, INSTALLATION, TESTING, BUILD_INSTRUCTIONS)
- Makefile for release automation
- All release packages (v1.0.0 through v1.3.2)

## Save Compatibility
- **v1.0.0 → v1.3.2**: Fully compatible, no data loss
- **v1.3.2 → v2.0.0**: **NOT COMPATIBLE** (v2.0 is complete rewrite)

## Restoration Instructions

### From Git
```bash
git checkout v1.x-stable
# or
git checkout v1.3.2-final
```

### From Backup Archive
```bash
cd /home/p4rad0x/.projects/rimworld_mods
unzip "backups/Priority Manager v1.3.2 - Backup.zip" -d "Priority Manager v1.3.2 Restored"
```

### Rebuild from Source
```bash
cd "Priority Manager/Source"
dotnet build -c Release
# or
cd "Priority Manager"
make build
```

## Why v2.0 is a Rewrite

v1.x architecture has performance limitations for large colonies (100+ colonists):
- Polling-based updates every 250 ticks (O(n²) complexity)
- Full map scans for work detection
- No caching or incremental updates
- UI redraws entire list every frame

v2.0 will use:
- Event-driven architecture (reactive, not polling)
- Spatial data structures for efficient work scanning
- Array-indexed caching instead of dictionary lookups
- Virtual scrolling for UI performance
- Parallel processing for large colonies
- Predictive caching based on learned patterns

## Support and Issues

If you need to revert to v1.3.2 after starting v2.0 development:
1. Checkout `v1.x-stable` branch
2. Your v1.x saves will work with v1.3.2
3. Do NOT load v2.0 saves with v1.3.2 (incompatible format)

## Developer Notes

v1.3.2 is feature-complete and stable. No further development planned on v1.x branch.

For bugs or issues with v1.3.2, open an issue on GitHub tagged `v1.x`.

For v2.0 development progress, see the `v2.0-dev` branch.

