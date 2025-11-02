# PriorityMaster Mod Compatibility

Full integration guide for using Priority Manager with [PriorityMaster](https://steamcommunity.com/sharedfiles/filedetails/?id=1994006442).

## What is PriorityMaster?

PriorityMaster is a RimWorld mod that extends the work priority system from the vanilla 1-4 scale to a much more granular 1-99 scale. This allows for more precise control over colonist work assignments.

**Key Features:**
- Customizable priority range (1-99)
- Configurable default priority
- Color customization with gradients
- Compatible with Fluffy's Work Tab, Multiplayer, and other work-related mods

**Mod Info:**
- Steam Workshop ID: `1994006442`
- Package ID: `Lauriichan.PriorityMaster`
- GitHub: https://github.com/Lauriichan/PriorityMaster

## ✅ Implementation Status

**ALL PHASES COMPLETE!** Priority Manager now has full PriorityMaster integration including:
- ✅ Automatic detection
- ✅ Dynamic priority scaling
- ✅ Custom mapping UI
- ✅ Distribution presets
- ✅ Composite role optimization
- ✅ Performance optimization with caching

## How It Works

### Automatic Detection

The mod automatically detects PriorityMaster by checking:
- Mod package ID: `Lauriichan.PriorityMaster`
- Running mods list

No manual configuration needed!

### Dynamic Priority Scaling

**Vanilla System (without PriorityMaster):**
- Priority 1: Primary job + critical tasks
- Priority 2: Top secondary jobs
- Priority 3: Additional jobs
- Priority 4: Backup jobs

**With PriorityMaster (Extended Range - max 99):**
- Priority 1 → **10** (10% of max) - Primary jobs + critical tasks
- Priority 2 → **30** (30% of max) - Secondary jobs
- Priority 3 → **60** (60% of max) - Tertiary jobs
- Priority 4 → **90** (90% of max) - Backup jobs

**The scaling is dynamic!** If you configure PriorityMaster to use a different max (e.g., 50), the priorities scale proportionally:
- Max 50: 1→5, 2→15, 3→30, 4→45
- Max 20: 1→2, 2→6, 3→12, 4→18

### Distribution Presets

Choose from three built-in presets or create your own custom mapping:

#### Tight Spacing
Best for: Small colonies or when you want colonists to switch tasks frequently
```
Priority 1 → 10
Priority 2 → 20
Priority 3 → 30
Priority 4 → 40
```

#### Balanced (Default)
Best for: General use, good separation between priorities
```
Priority 1 → 10
Priority 2 → 30
Priority 3 → 60
Priority 4 → 90
```

#### Wide Spread
Best for: Large colonies or when you want clear task separation
```
Priority 1 → 5
Priority 2 → 25
Priority 3 → 55
Priority 4 → 95
```

#### Custom
Create your own mapping! Use sliders to set exact values for each priority level.

### Composite Roles with PriorityMaster

Composite roles (Builder, Demolition, Medic, Industrialist) automatically spread their jobs across the extended range:

**Builder Example (max 99):**
- Construction: Priority **10** (highest)
- Repair: Priority **30** (medium)
- Deconstruct: Priority **60** (lower)
- Other jobs: Priority **90** (lowest)

This gives much better task prioritization compared to vanilla 1/2/3/4!

### Compatibility Considerations

**Works With:**
- ✅ Vanilla RimWorld (1-4 priorities)
- ✅ PriorityMaster (1-99 or user-configured max)
- ✅ Complex Jobs (specialized work types)
- ✅ Fluffy's Work Tab (PriorityMaster already compatible)

**Potential Conflicts:**
- None expected - PriorityMaster handles the UI and priority storage
- Priority Manager only sets the priority values
- Both mods can coexist peacefully

## User Guide

### Getting Started

1. **Install both mods:**
   - Priority Manager
   - PriorityMaster from [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1994006442)

2. **Enable both mods** in RimWorld mod menu

3. **Load or start a game**

4. **Open Priority Manager** (Press P or click button on Work tab)

5. **Go to PriorityMaster tab** to configure settings

### Recommended Settings

**For Small Colonies (1-5 colonists):**
- Use **Tight Spacing** preset
- Colonists will switch between tasks more frequently
- Good when everyone needs to multitask

**For Medium Colonies (6-15 colonists):**
- Use **Balanced** preset (default)
- Good separation between primary and backup jobs
- Colonists are more specialized but still flexible

**For Large Colonies (16+ colonists):**
- Use **Wide Spread** preset
- Maximum specialization
- Colonists focus heavily on their primary jobs
- Large priority gaps mean less task switching

### Configuration Options

**Enable PriorityMaster Integration** (checkbox)
- ON: Use extended priority range (default)
- OFF: Use vanilla 1-4 priorities even with PriorityMaster installed

**Priority Distribution Presets** (radio buttons)
- Choose from Tight, Balanced, Wide, or Custom
- Automatically applies the selected priority mapping
- Changes take effect on next recalculation

**Custom Mapping** (sliders, Custom preset only)
- Set exact priority values for 1, 2, 3, 4
- Range: 1 to PriorityMaster's max (default 99)
- Real-time preview of current mapping

**Recalculate All Priorities** (button)
- Applies new priority mapping to all colonists
- Click after changing presets or custom values

### User Benefits

1. **More Granular Control**: Take full advantage of PriorityMaster's 1-99 range
2. **Better Job Distribution**: More priority levels means less job conflicts
3. **Flexible Specialization**: Fine-tune exactly when colonists switch tasks
4. **Seamless Integration**: Works automatically when both mods are installed
5. **No Performance Impact**: Caching ensures priority lookups are fast

## Technical Implementation

### Files Modified

**New Files:**
- `Source/PriorityMasterCompat.cs` - Detection, scaling, and caching logic

**Modified Files:**
- `Source/PriorityAssigner.cs` - SetPriority method now uses scaling
- `Source/ConfigWindow.cs` - Added PriorityMaster tab with UI controls
- `Source/PriorityData.cs` - Added settings, presets, and preset application
- `Source/ColonistRole.cs` - Added GetCompositeRoleJobsScaled for composite roles

### Key Components

**Detection (PriorityMasterCompat.cs):**
```csharp
public static bool IsLoaded()
{
    return ModsConfig.IsActive("Lauriichan.PriorityMaster") ||
           LoadedModManager.RunningMods.Any(m => m.PackageId.ToLower() == "lauriichan.prioritymaster");
}
```

**Dynamic Scaling:**
```csharp
public static int ScalePriority(int vanillaPriority)
{
    int maxPriority = GetMaxPriority();
    switch (vanillaPriority)
    {
        case 1: return Mathf.Max(1, Mathf.RoundToInt(maxPriority * 0.1f));
        case 2: return Mathf.RoundToInt(maxPriority * 0.3f);
        case 3: return Mathf.RoundToInt(maxPriority * 0.6f);
        case 4: return Mathf.RoundToInt(maxPriority * 0.9f);
    }
}
```

**Reflection for Settings:**
- Reads PriorityMaster's max priority setting using HarmonyLib's AccessTools
- Cached for performance (only reads once per game session)
- Gracefully falls back to default (9) if reflection fails

**Composite Role Optimization:**
- Builder/Demolition/Medic/Industrialist roles use GetCompositeRoleJobsScaled
- Jobs are spread across the full priority range
- Example: Builder at max 99 uses priorities 10, 30, 60 instead of 1, 2, 3

## Usage Examples

### Example 1: Basic Setup with Balanced Preset

1. Load game with both mods enabled
2. Open Priority Manager (Press P)
3. Go to "PriorityMaster" tab
4. Verify it shows "PriorityMaster Detected - Max Priority: 99"
5. Select "Balanced Spread (10, 30, 60, 90)"
6. Click "Recalculate All Priorities"
7. Check Work tab - priorities now use 10, 30, 60, 90 instead of 1, 2, 3, 4

### Example 2: Builder with Extended Range

1. Select a colonist with Construction skill
2. Set role to "Builder (Construct→Repair→Demo)"
3. With PriorityMaster, they get:
   - Construction: Priority 10
   - Repair: Priority 30
   - Deconstruct: Priority 60
   - Other jobs: Priority 90
4. Much better task separation than vanilla 1/2/3/4!

### Example 3: Custom Mapping for Specialists

1. Go to PriorityMaster tab
2. Select "Custom Mapping"
3. Set custom values:
   - Priority 1 → 5 (very high priority, rarely switch)
   - Priority 2 → 15 (medium)
   - Priority 3 → 40 (lower)
   - Priority 4 → 80 (very low)
4. This creates specialists who strongly focus on their primary job

### Example 4: Wide Spread for Large Colony

1. Colony with 20+ colonists
2. Select "Wide Spread (5, 25, 55, 95)"
3. Each colonist becomes highly specialized
4. Primary jobs (5) are done almost exclusively
5. Backup jobs (95) are rarely touched
6. Perfect for large, well-organized colonies

## Troubleshooting

### PriorityMaster not detected

**Issue:** PriorityMaster tab shows "not detected"

**Solutions:**
- Ensure PriorityMaster is enabled in mod menu
- Restart RimWorld after enabling mods
- Check mod load order (shouldn't matter, but try loading PriorityMaster before Priority Manager)

### Priorities seem wrong

**Issue:** Colonists have unexpected priorities in Work tab

**Solutions:**
- Open PriorityMaster tab and check current mapping
- Click "Recalculate All Priorities"
- If you changed PriorityMaster's max priority setting, restart the game

### Custom mapping not saving

**Issue:** Custom priorities reset after reload

**Solutions:**
- Make sure you clicked "Recalculate All Priorities" after changing settings
- Settings are auto-saved when closing the window
- Check if PriorityManager settings file exists in save folder

## References

- [PriorityMaster Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1994006442)
- [PriorityMaster GitHub](https://github.com/Lauriichan/PriorityMaster)
- [Priority Manager GitHub](https://github.com/R3v3rs3-P4RAD0X/PriorityManager)

## Important Notes

- PriorityMaster's default max is **9**, not 99 (user-configurable up to 99)
- Priority Manager reads PriorityMaster's max setting automatically via reflection
- If PriorityMaster settings change mid-game, restart for Priority Manager to detect the change
- Composite roles work best with PriorityMaster's extended range
- Integration can be disabled if you prefer vanilla behavior

