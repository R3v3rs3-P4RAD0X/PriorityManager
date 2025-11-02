# Priority Manager - RimWorld Mod

![Priority Manager](About/Preview.png)

Auto-assigns work priorities to colonists based on their skills, user-defined roles, and health status.

## Features

- **Automatic Priority Assignment**: Assigns work priorities (1-4) based on colonist skills and passions
- **Role Presets**: Choose from predefined roles (Researcher, Doctor, Farmer, etc.) or let the system auto-detect best jobs
- **Smart Secondary Jobs**: Automatically assigns backup jobs based on skill levels
- **Illness Response**: Reduces workload for sick/injured colonists
- **Solo Colonist Mode**: Special survival priorities when you only have one colonist
- **Periodic Recalculation**: Auto-updates priorities every X hours (configurable)
- **Manual Override**: Set colonists to manual control if needed
- **Complex Jobs Support**: Automatically detects and supports Complex Jobs mod's specialized work types (Repairing, Deconstructing, Building)
- **PriorityMaster Support**: Full integration with PriorityMaster (1-99 priorities) - automatic scaling, presets, and custom mapping
- **Min/Max Worker Control**: Set minimum and maximum workers per job type (absolute or percentage-based)

## Download

### For Users

**Quick Install:** Download the latest compiled version from the [Releases](https://github.com/R3v3rs3-P4RAD0X/PriorityManager/releases) page.

**Installation:**
1. Download `PriorityManager-v1.0.0.zip` (or latest version) from Releases
2. Extract the **entire** "Priority Manager" folder to your RimWorld Mods directory:
   - **Windows**: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\`
   - **Linux**: `~/.steam/steam/steamapps/common/RimWorld/Mods/` or `~/Games/SteamLibrary/steamapps/common/RimWorld/Mods/`
   - **Mac**: `~/Library/Application Support/Steam/steamapps/common/RimWorld/Mods/`
3. Launch RimWorld and enable "Priority Manager" in the mod menu
4. Start a new game or load an existing save

For detailed installation instructions, see [INSTALLATION.md](INSTALLATION.md)

### For Developers

If you want to build from source, see the build instructions below.

## How to Build

### Requirements
- .NET Framework 4.7.2 SDK or Mono
- RimWorld installed at `/home/p4rad0x/Games/SteamLibrary/steamapps/common/RimWorld`

### Build Commands

Using dotnet CLI:
```bash
cd "Source"
dotnet build
```

Using Mono (if dotnet is not available):
```bash
cd "Source"
msbuild PriorityManagerMod.csproj
```

The compiled DLL will be placed in the `Assemblies/` directory.

## Installation

1. Build the mod (see above)
2. Copy the entire "Priority Manager" folder to your RimWorld mods directory:
   - Linux: `~/.steam/steam/steamapps/common/RimWorld/Mods/`
   - Or use the game's mod folder: `~/Games/SteamLibrary/steamapps/common/RimWorld/Mods/`
3. Launch RimWorld and enable the mod in the mod menu

## Usage

### Quick Start
1. Open the Work tab in-game
2. Click "Priority Manager" button at the top
3. Configure your settings and assign roles to colonists
4. Click "Recalculate All" to apply priorities

### Role Presets

#### Single-Job Roles
- **Auto (Default)**: System picks the best primary job based on highest skill
- **Specific Roles**: Choose Researcher, Doctor, Cook, Constructor, etc. to set their primary focus
- **Manual**: Disable auto-assignment for full manual control

#### Complex Jobs Roles (when mod is loaded)
Individual specialized roles for Complex Jobs work types:
- Nurse, Surgeon, Repairer, Deconstructor, Druggist, Machinist, Fabricator, etc.

#### Composite Roles (Multiple Jobs with Priorities)
**New!** Advanced roles that assign multiple related jobs with different priority levels:
- **Builder**: Construction (1st) → Repair (2nd) → Deconstruct (3rd)
- **Demolition**: Deconstruct (1st) → Repair (2nd) → Construction (3rd)
- **Medic**: Surgeon (1st) → Nurse + Doctor (2nd)
- **Industrialist**: Machining + Fabrication (1st) → Smithing + Crafting (2nd) → Smelting (3rd)

Perfect for Complex Jobs mod! These roles create specialized workflows for your colonists.

### Settings

#### Global Settings
- **Auto-recalculation interval**: How often to automatically update priorities (0 = manual only)
- **Global auto-assign**: Enable/disable the entire system
- **Illness response**: Automatically reduce workload for sick colonists

#### Job Settings Tab
Configure individual job types in the "Job Settings" tab:
- **Job Importance**: Set priority level (Disabled, Very Low, Low, Normal, High, Critical)
- **Min/Max Workers**: Set minimum and maximum number of colonists for each job
  - Can be set as **absolute number** (e.g., "exactly 2 cooks")
  - Or as **percentage** (e.g., "25% of colonists should be builders")
  - Min workers are **enforced** - the system will assign at least this many colonists
  - Max workers are **respected** - no more than this many colonists will be assigned
- **Edit Button**: Opens detailed settings for each job type

### Keybinds
- Press **P** to open the Priority Manager window (configurable in Options > Key Bindings)

## How It Works

### Priority System
- **Priority 1**: Primary job (from role) + critical jobs (Firefighter)
- **Priority 2**: Top secondary jobs based on skills
- **Priority 3**: Additional jobs based on skills
- **Priority 4**: Backup jobs
- **Disabled**: All other jobs

### Illness Handling
When a colonist becomes ill or injured (health below 50% or serious conditions):
- **Priority 1**: Patient/Bed Rest (if available), Firefighter
- **Priority 2**: Doctor (if Medical skill ≥ 3 for self-tending)
- **Priority 4**: Hauling (basic tasks only)
- All other work is disabled to allow recovery

### Solo Colonist
When you only have one colonist, the mod uses survival-focused priorities:
- Hunting, Cooking, Growing = highest priority
- Construction, Repair, Mining = medium priority
- Hauling, Cleaning, Research = lower priority

## Mod Compatibility

### Complex Jobs
Priority Manager automatically detects and supports the **Complex Jobs** mod, which splits vanilla work types into more specialized jobs:

- **Repairing**: Separate from Construction (if Complex Jobs splits it)
- **Deconstructing**: Separate from Construction (if Complex Jobs splits it)
- **Building/Constructing**: The actual construction work (if Complex Jobs splits it)

**How it works:**
1. The mod automatically detects if Complex Jobs is loaded
2. If Complex Jobs work types are found, additional role presets appear in the dropdown:
   - "Repairer (Complex Jobs)"
   - "Deconstructor (Complex Jobs)"
   - "Builder (Complex Jobs)"
3. All Complex Jobs work types are automatically included in:
   - Job priority calculations
   - Min/Max worker assignments
   - Job Settings tab for customization
4. Works seamlessly with or without Complex Jobs - no manual configuration needed

**Note**: The mod uses dynamic detection, so it will work with any mod that adds custom `WorkTypeDef` entries.

### PriorityMaster

Priority Manager now supports [PriorityMaster](https://steamcommunity.com/sharedfiles/filedetails/?id=1994006442), which extends work priorities from 1-4 to 1-99!

**Features:**
- **Automatic Detection**: Mod automatically detects if PriorityMaster is installed
- **Dynamic Scaling**: Priorities are automatically scaled to PriorityMaster's extended range
  - Priority 1 → 10% of max (default: 10 for max 99)
  - Priority 2 → 30% of max (default: 30 for max 99)
  - Priority 3 → 60% of max (default: 60 for max 99)
  - Priority 4 → 90% of max (default: 90 for max 99)
- **Custom Mapping**: Configure exact priority values in the PriorityMaster tab
- **Distribution Presets**:
  - **Tight**: Close spacing (10, 20, 30, 40)
  - **Balanced**: Default spread (10, 30, 60, 90)
  - **Wide**: Maximum spread (5, 25, 55, 95)
  - **Custom**: Define your own mapping
- **Composite Roles**: Builder/Demolition/Medic roles spread across full range for maximum granularity
- **Seamless Integration**: Works with or without PriorityMaster - no manual configuration needed

**How to use:**
1. Install both Priority Manager and PriorityMaster
2. Open Priority Manager settings (Press P)
3. Go to the "PriorityMaster" tab
4. Choose your preferred preset or customize mapping
5. Click "Recalculate All Priorities"

Your colonists now have much finer control over work priorities!

### Other Mods
Priority Manager is designed to work with any mod that adds custom work types. The system automatically:
- Detects all work types from `DefDatabase<WorkTypeDef>`
- Includes them in priority calculations
- Makes them available in the Job Settings tab
- Respects their skill requirements and restrictions

## Testing Checklist

### Basic Functionality
- [ ] Mod loads without errors in RimWorld
- [ ] Priority Manager button appears on Work tab
- [ ] Can open Priority Manager window with 'P' key
- [ ] Can open Priority Manager window from Work tab button

### Single Colonist Scenario
- [ ] New game with 1 colonist gets survival priorities
- [ ] Colonist has Hunting, Cooking, Growing at high priority
- [ ] Essential jobs are enabled appropriately

### Multiple Colonists
- [ ] Each colonist with "Auto" role gets different primary jobs
- [ ] Secondary jobs (priority 2-4) are assigned based on skills
- [ ] Colonists with preset roles get correct primary job

### Role Assignment
- [ ] Can change colonist role from dropdown
- [ ] "Auto" role selects best job automatically
- [ ] Specific roles (Doctor, Researcher, etc.) set correct primary job
- [ ] "Manual" role disables auto-assignment

### Illness Response
- [ ] When colonist health < 50%, work is reduced to Patient/Bed Rest
- [ ] When colonist recovers, normal priorities are restored
- [ ] Setting can be toggled on/off

### Auto-Recalculation
- [ ] Setting interval to 0 disables auto-recalculation
- [ ] Setting interval to 12h recalculates every 12 in-game hours
- [ ] Manual "Recalculate Now" button works

### New Colonists
- [ ] New colonist joining gets auto-assigned role
- [ ] New colonist gets appropriate priorities based on skills

### Persistence
- [ ] Save game and reload - settings are preserved
- [ ] Save game and reload - colonist roles are preserved
- [ ] Settings in mod options are saved

### UI/UX
- [ ] Colonist list shows all colonists
- [ ] Can toggle auto-assign per colonist
- [ ] "Reset All to Auto" button works
- [ ] "Recalculate" button works per colonist

### Complex Jobs Compatibility (if mod is loaded)
- [ ] Complex Jobs work types appear in Job Settings tab
- [ ] Can set min/max workers for Complex Jobs work types
- [ ] Role presets for Repairer, Deconstructor, Builder appear in dropdown
- [ ] Assigning a Complex Jobs role sets the correct work type priority
- [ ] Auto-assignment distributes Complex Jobs work types appropriately
- [ ] Job Settings "Edit" window works for Complex Jobs work types

### Min/Max Worker Controls
- [ ] Can set minimum workers for a job type (absolute number)
- [ ] Can set maximum workers for a job type (absolute number)
- [ ] Can toggle between absolute and percentage mode
- [ ] Minimum workers are enforced (at least N colonists assigned)
- [ ] Maximum workers are respected (no more than N colonists assigned)
- [ ] Percentage mode correctly calculates based on colony size
- [ ] Disabled colonists are excluded from auto-assignment but counted for min/max limits

### PriorityMaster Compatibility (if mod is loaded)
- [ ] PriorityMaster is detected and shown in settings
- [ ] Priority scaling works correctly (1→10, 2→30, 3→60, 4→90 by default)
- [ ] Can toggle PriorityMaster integration on/off
- [ ] Tight preset works (10, 20, 30, 40)
- [ ] Balanced preset works (10, 30, 60, 90)
- [ ] Wide preset works (5, 25, 55, 95)
- [ ] Custom mapping sliders work and save correctly
- [ ] Composite roles spread across extended range properly
- [ ] Settings persist after save/load
- [ ] Works with Complex Jobs + PriorityMaster simultaneously
- [ ] Disabling integration reverts to vanilla 1-4 priorities

## Known Limitations

- The mod uses priorities 1-4. If you prefer a different range, you'll need to manually adjust
- Auto-detection of "best job" is based on raw skill levels and passions, not current colony needs
- The mod doesn't account for job urgency (e.g., won't prioritize cooking if low on food)

## Troubleshooting

### Mod doesn't appear in mod list
- Ensure the folder is named exactly "Priority Manager"
- Check that About/About.xml exists and is valid
- Verify the mod is in the correct mods directory

### Compilation errors
- Verify RimWorld path in .csproj matches your installation
- Ensure .NET 4.7.2 SDK is installed
- Check that all RimWorld DLLs exist in the specified path

### Priorities not updating
- Check that "Enable auto-assignment globally" is checked
- Verify colonist's auto-assign toggle is enabled
- Check that colonist's role is not set to "Manual"
- Try clicking "Recalculate Now" manually

## Credits

Created by P4RAD0X for RimWorld 1.6

## License

Feel free to modify and redistribute. If you improve it, please share!

