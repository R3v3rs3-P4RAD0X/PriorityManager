# Priority Manager - Installation Guide

## ✅ Build Status: COMPLETE

The mod has been successfully compiled! The DLLs are ready in the `Assemblies/` folder:
- `PriorityManager.dll` (25 KB) - Main mod code
- `0Harmony.dll` (2.1 MB) - Harmony library

## Quick Install

### Option 1: Symlink (Recommended for Development) ✅ DONE

Create a symlink so changes are immediately reflected:

```bash
ln -s "/home/p4rad0x/.projects/rimworld_mods/Priority Manager" \
      "/home/p4rad0x/Games/SteamLibrary/steamapps/common/RimWorld/Mods/Priority Manager"
```

**Status**: ✅ Symlink already created!

### Option 2: Copy to Mods Folder

Copy the entire mod folder to RimWorld's mods directory:

```bash
cp -r "/home/p4rad0x/.projects/rimworld_mods/Priority Manager" \
      "/home/p4rad0x/Games/SteamLibrary/steamapps/common/RimWorld/Mods/"
```

## Enable the Mod

1. Launch RimWorld
2. Go to **Mods** menu from main menu
3. Find "Priority Manager" in the list
4. Check the box to enable it
5. Click "Close" and restart RimWorld when prompted

## First Use

Once in-game:

1. **Open the Work tab** - You'll see two new buttons:
   - "Priority Manager" - Opens the configuration window
   - "Recalculate All" - Immediately recalculates all colonist priorities

2. **Or press 'P'** - Opens the Priority Manager window from anywhere

3. **Configure Settings**:
   - Set auto-recalculation interval (or 0 for manual only)
   - Enable/disable global auto-assignment
   - Enable/disable illness response

4. **Assign Roles**:
   - Each colonist defaults to "Auto" (best skill detection)
   - Click "Change Role" to pick specific roles (Doctor, Farmer, etc.)
   - Toggle auto-assign per colonist

5. **Click "Recalculate All"** to apply priorities

## Testing Your Installation

### Basic Checks
- [ ] Mod appears in RimWorld mod list
- [ ] No errors in log when loading game
- [ ] "Priority Manager" button visible in Work tab
- [ ] Can open Priority Manager window with 'P' key

### Functional Tests
- [ ] Start or load a game
- [ ] Open Priority Manager window
- [ ] Set a colonist to "Auto" role
- [ ] Click "Recalculate" for that colonist
- [ ] Check Work tab - priorities should be assigned (1-4)
- [ ] Try different role presets (Doctor, Researcher, etc.)

### Log File Location
If you encounter errors, check the log:
```
~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Player.log
```

Look for lines containing "PriorityManager" or errors related to the mod.

## Uninstallation

To remove the mod:

1. In RimWorld, disable the mod in the Mods menu
2. Remove the symlink or folder:
   ```bash
   rm "/home/p4rad0x/Games/SteamLibrary/steamapps/common/RimWorld/Mods/Priority Manager"
   ```

**Note**: Your save files will still load, but auto-assigned priorities will remain as they were last set.

## Troubleshooting

### Mod doesn't appear in list
- Verify the folder is in the correct location
- Check that `About/About.xml` exists
- Ensure the folder is named exactly "Priority Manager"

### Buttons don't appear in Work tab
- Make sure you're in a game (not main menu)
- Check that colonists exist
- Verify no errors in Player.log

### Priorities not updating
- Check that "Enable auto-assignment globally" is ON
- Verify the colonist's auto-assign toggle is enabled
- Ensure role is not set to "Manual"
- Try clicking "Recalculate Now" manually

### Build issues (for development)
- If you need to rebuild: `cd Source && dotnet build`
- For clean rebuild: `cd Source && dotnet clean && dotnet build`
- Check BUILD_INSTRUCTIONS.md for detailed build info

## Next Steps

- See `README.md` for feature documentation
- See `TESTING.md` for comprehensive testing checklist
- Provide feedback and report issues!

## File Structure Verification

Your mod should have this structure:
```
Priority Manager/
├── About/
│   └── About.xml          ✓
├── Assemblies/
│   ├── 0Harmony.dll      ✓
│   └── PriorityManager.dll ✓
├── Defs/
│   └── KeyBindings.xml   ✓
└── Source/                ✓ (not needed for distribution)
```

If any ✓ items are missing, the mod may not work correctly.

---

**Enjoy your automated priority management!** 🎮

