# Complex Jobs Mod Compatibility

Priority Manager has full compatibility with the **Complex Jobs** mod, which breaks down vanilla work types into more specialized tasks.

## What is Complex Jobs?

Complex Jobs is a RimWorld mod that splits broad work categories into more specific job types. For example, instead of one "Construction" work type, it might create:
- **Constructing** - Building new structures
- **Deconstructing** - Taking apart existing structures
- **Repairing** - Fixing damaged buildings

This gives you much finer control over what each colonist does.

## How Priority Manager Supports Complex Jobs

### Automatic Detection

Priority Manager automatically detects if Complex Jobs (or any similar mod) is installed by scanning the game's work type definitions. No manual configuration is needed!

### Additional Role Presets

When Complex Jobs is detected, Priority Manager adds specialized role presets to the role dropdown:

#### Single-Job Roles
Individual specialized roles that focus on one specific job type:
- **Nurse** - Care for the sick and injured
- **Surgeon** - Perform surgical operations
- **Repairer** - Repair damaged buildings
- **Deconstructor** - Deconstruct structures
- **Druggist** - Craft drugs
- **Machinist** - Work at machining tables
- **Fabricator** - Work at fabrication benches
- **Refiner** - Refine materials
- **Stonecutter** - Cut stone blocks
- **Smelter** - Smelt materials
- **Producer** - General production work
- **Animal Trainer** - Train and tame animals
- **Butcher** - Butcher meat and make kibble
- **Harvester** - Harvest crops
- **Driller** - Operate deep drills

#### Composite Roles (Multi-Job Priorities)
**New!** Composite roles assign multiple related jobs with different priority levels:

- **Builder** - Construction-focused workflow
  - Priority 1: Construction (building)
  - Priority 2: Repair
  - Priority 3: Deconstruct
  - Priority 4: Other jobs based on skills

- **Demolition** - Deconstruction-focused workflow
  - Priority 1: Deconstruct
  - Priority 2: Repair
  - Priority 3: Construction
  - Priority 4: Other jobs based on skills

- **Medic** - Medical workflow
  - Priority 1: Surgeon (Complex Jobs)
  - Priority 2: Nurse (Complex Jobs) + Doctor (vanilla)
  - Priority 4: Other jobs based on skills

- **Industrialist** - Production workflow
  - Priority 1: Machining + Fabrication
  - Priority 2: Smithing + Crafting
  - Priority 3: Smelt + Production
  - Priority 4: Other jobs based on skills

These presets appear **only if** the corresponding work types exist in your game.

### Full Job Settings Support

All Complex Jobs work types automatically appear in the **Job Settings** tab, where you can:
- Set job importance (Disabled, Very Low, Low, Normal, High, Critical)
- Configure min/max workers (absolute numbers or percentages)
- View relevant skills for each job
- Access detailed editing for each job type

### Smart Assignment

The auto-assignment system treats Complex Jobs work types like any other job:
- Considers colonist skills and passions
- Respects min/max worker limits
- Distributes jobs evenly across colonists
- Includes them in secondary job assignment (priorities 2-4)

## Technical Details

### Work Type Detection

Priority Manager looks for the following work type defNames (in order of preference):

**Repairing:**
- `Repair`
- `Repairing`

**Deconstructing:**
- `Deconstruct`
- `Deconstructing`

**Building/Constructing:**
- `Construct`
- `Building`
- `Constructing`

This flexible approach ensures compatibility even if Complex Jobs uses slightly different naming conventions.

### Graceful Fallback

If Complex Jobs is not installed or uses different defNames:
- The Complex Jobs role presets won't appear in the dropdown
- All other functionality works normally
- No errors or warnings are generated
- The vanilla "Constructor" role remains available

## Usage Examples

### Example 1: Builder Role (Composite)

**Scenario:** You want a colonist who primarily builds but can also repair and demo as needed.

1. Install both Priority Manager and Complex Jobs
2. Open Priority Manager (press P or click button on Work tab)
3. Select a colonist with high Construction skill
4. Set their role to "Builder (Construct→Repair→Demo)"
5. Result:
   - Construction work is Priority 1 (top priority)
   - Repair work is Priority 2 (secondary)
   - Deconstruction is Priority 3 (tertiary)
   - Other jobs at Priority 4 based on their skills

### Example 2: Demolition Specialist

**Scenario:** You're clearing out an area and need dedicated demolition workers.

1. Select a colonist
2. Set their role to "Demolition (Demo→Repair→Construct)"
3. Result:
   - Deconstruction is Priority 1 (they'll prioritize demo work)
   - Repair is Priority 2 (fix things when not demo'ing)
   - Construction is Priority 3 (build when nothing to demo/repair)

### Example 3: Balanced Construction Team

**Scenario:** You want 2-3 colonists always available for construction.

1. Go to "Job Settings" tab in Priority Manager
2. Find "Construction" in the list
3. Click "Edit"
4. Set Min Workers: 2, Max Workers: 3
5. Click Save
6. Result: The system ensures 2-3 colonists are always assigned to construction

### Example 4: Medical Team (Composite Medic Role)

**Scenario:** You need versatile medical staff.

1. Select colonists with Medical skills
2. Set their roles to "Medic (Surgery→Nursing→Doctor)"
3. Result:
   - Surgery is Priority 1 (operations first)
   - Nursing + Doctor work at Priority 2 (care & tending)
   - They handle all medical tasks efficiently

### Example 5: Production Specialist (Industrialist)

**Scenario:** You have a skilled crafter who should focus on high-tech production.

1. Select a colonist with high Crafting skill
2. Set their role to "Industrialist (Multi-Production)"
3. Result:
   - Machining & Fabrication at Priority 1
   - Smithing & Crafting at Priority 2
   - Smelting & Production at Priority 3
   - Covers entire production pipeline efficiently

## Compatibility with Other Mods

Priority Manager is designed to work with **any mod** that adds custom work types through the standard `WorkTypeDef` system. This includes:

- Complex Jobs
- Custom work type mods
- Overhaul mods that add new professions
- Any mod using RimWorld's work type framework

The system automatically:
- Detects all work types from the game's definition database
- Makes them available for assignment and configuration
- Respects their skill requirements and restrictions
- Includes them in all auto-assignment logic

## Troubleshooting

### Complex Jobs presets don't appear

**Cause:** Complex Jobs may not be loaded, or uses different defNames than expected.

**Solution:**
1. Verify Complex Jobs is enabled in your mod list
2. Check that Complex Jobs loads before Priority Manager
3. Look in the Job Settings tab - Complex Jobs work types should still appear there even if presets don't
4. You can still use these jobs; just set them manually in Job Settings

### Work priorities seem wrong with Complex Jobs

**Cause:** The system might need to recalculate priorities after enabling Complex Jobs.

**Solution:**
1. Open Priority Manager window
2. Click "Recalculate All" button
3. This will reassign all colonists with the new work types

### Both vanilla and Complex Jobs work types appear

**Cause:** Complex Jobs doesn't always remove vanilla work types, it just adds more specific ones.

**Solution:**
- This is normal behavior
- You can use Job Settings to disable the vanilla work types
- Or use min/max workers to control how many colonists use each type
- The system will handle both types appropriately

## Future Enhancements

Potential improvements for Complex Jobs support:
- Auto-detect when Complex Jobs is removed and revert to vanilla presets
- Suggest role presets based on detected mod work types
- Custom priority templates for Complex Jobs setups
- Integration with Complex Jobs' own priority systems (if applicable)

## Credits

- Complex Jobs mod by its original author
- Priority Manager integration by P4RAD0X

## Support

For issues specific to Complex Jobs compatibility:
1. Ensure both mods are up to date
2. Check the Priority Manager logs for errors
3. Try recalculating all priorities
4. If issues persist, report with both mod versions and error logs

