# PriorityMaster Mod Compatibility

This document outlines the compatibility plan for integrating Priority Manager with [PriorityMaster](https://steamcommunity.com/sharedfiles/filedetails/?id=1994006442).

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

## Integration Plan

### Detection

Detect PriorityMaster installation by checking for its package ID or mod assembly:
```csharp
public static bool IsPriorityMasterLoaded()
{
    return ModsConfig.IsActive("Lauriichan.PriorityMaster") ||
           LoadedModManager.RunningMods.Any(m => m.PackageId == "Lauriichan.PriorityMaster");
}
```

### Priority Range Adaptation

**Current System (Vanilla):**
- Priority 1: Primary job + critical tasks
- Priority 2: Top secondary jobs
- Priority 3: Additional jobs
- Priority 4: Backup jobs

**With PriorityMaster (Extended Range):**
- Use a wider spread of priorities to take advantage of the 1-99 range
- Map our internal logic to appropriate ranges:
  - Priority 1-10: Critical jobs (firefighting, patient, etc.)
  - Priority 11-30: Primary jobs (role-based assignments)
  - Priority 31-60: Secondary jobs (skill-based backups)
  - Priority 61-90: Tertiary jobs (low-skill fallbacks)
  - Priority 91-99: Least important tasks

### Implementation Approach

1. **Priority Scaling Function**
   ```csharp
   public static int ScalePriority(int vanillaPriority, int maxPriority = 4)
   {
       if (!IsPriorityMasterLoaded()) return vanillaPriority;
       
       // Get PriorityMaster's max priority setting
       int pmMaxPriority = GetPriorityMasterMaxPriority();
       
       // Scale vanilla 1-4 to PriorityMaster range
       switch (vanillaPriority)
       {
           case 1: return Mathf.RoundToInt(pmMaxPriority * 0.1f);  // 10% (e.g., 10 if max is 99)
           case 2: return Mathf.RoundToInt(pmMaxPriority * 0.3f);  // 30% (e.g., 30 if max is 99)
           case 3: return Mathf.RoundToInt(pmMaxPriority * 0.6f);  // 60% (e.g., 60 if max is 99)
           case 4: return Mathf.RoundToInt(pmMaxPriority * 0.9f);  // 90% (e.g., 90 if max is 99)
           default: return vanillaPriority;
       }
   }
   ```

2. **Configuration Options**
   - Add toggle: "Enable PriorityMaster Integration"
   - Add option: "Use Extended Priority Range"
   - Add sliders for custom priority mappings
   - Display current priority range in UI

3. **Composite Roles Adjustment**
   For composite roles (Builder, Demolition, etc.), spread priorities across the range:
   ```csharp
   Builder (with PriorityMaster):
   - Construction: Priority 10
   - Repair: Priority 30
   - Deconstruct: Priority 60
   - Other jobs: Priority 80-90
   ```

4. **UI Updates**
   - Display actual priority numbers in tooltips
   - Show "(PriorityMaster)" indicator when active
   - Add explanation of priority ranges in settings

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

### Testing Checklist

- [ ] Mod loads without errors with PriorityMaster enabled
- [ ] Mod loads without errors with PriorityMaster disabled
- [ ] Priority scaling works correctly (1→10, 2→30, 3→60, 4→90)
- [ ] Composite roles spread across extended range
- [ ] Settings UI shows correct priority ranges
- [ ] Auto-assignment respects PriorityMaster's max priority setting
- [ ] Manual priority changes work correctly
- [ ] Save/load preserves priority settings
- [ ] Works with Complex Jobs + PriorityMaster simultaneously

### User Benefits

1. **More Granular Control**: Take full advantage of PriorityMaster's 1-99 range
2. **Better Job Distribution**: More priority levels means less job conflicts
3. **Flexible Specialization**: Fine-tune exactly when colonists switch tasks
4. **Seamless Integration**: Works automatically when both mods are installed

### Implementation Priority

**Phase 1: Basic Compatibility**
- Detect PriorityMaster
- Scale priorities to extended range
- Test basic functionality

**Phase 2: Enhanced Integration**
- Custom priority mapping in settings
- UI improvements
- Advanced configuration options

**Phase 3: Optimization**
- Performance tuning
- User presets for priority distributions
- Documentation and examples

## Code Structure

### New Files
- `Source/PriorityMasterCompat.cs` - Detection and integration logic
- `Source/PriorityScaling.cs` - Priority scaling algorithms

### Modified Files
- `Source/PriorityAssigner.cs` - Add priority scaling to SetPriority calls
- `Source/ConfigWindow.cs` - Add PriorityMaster settings section
- `Source/PriorityManagerMod.cs` - Initialize PriorityMaster detection
- `Source/ColonistRole.cs` - Update composite role priority assignments

## References

- [PriorityMaster Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=1994006442)
- [PriorityMaster GitHub](https://github.com/Lauriichan/PriorityMaster)
- [RimWorld Modding Documentation](https://rimworldwiki.com/wiki/Modding_Tutorials)

## Notes

- PriorityMaster is highly customizable by users - we need to read their settings
- Default max priority in PriorityMaster is 9, not 99 (configurable up to 99)
- Must handle cases where user changes PriorityMaster settings mid-game
- Our priority assignments should adapt dynamically to PriorityMaster config changes

