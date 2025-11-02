# Build Instructions for Priority Manager

## Prerequisites

You need one of the following build tools installed:

1. **.NET SDK** (recommended)
   ```bash
   # Check if installed
   dotnet --version
   ```

2. **Mono** (alternative)
   ```bash
   # Check if installed
   msbuild -version
   # or
   xbuild /version
   ```

## Installing Build Tools

### Option 1: .NET SDK (Recommended)

For Arch Linux:
```bash
sudo pacman -S dotnet-sdk
```

For other distributions, see: https://dotnet.microsoft.com/download

### Option 2: Mono

For Arch Linux:
```bash
sudo pacman -S mono mono-msbuild
```

For other distributions, see: https://www.mono-project.com/download/stable/

## Building the Mod

### Using the build script (easiest):
```bash
cd "/home/p4rad0x/.projects/rimworld_mods/Priority Manager"
./build.sh
```

### Manual build with dotnet:
```bash
cd "/home/p4rad0x/.projects/rimworld_mods/Priority Manager/Source"
dotnet build PriorityManagerMod.csproj -c Release
```

### Manual build with msbuild:
```bash
cd "/home/p4rad0x/.projects/rimworld_mods/Priority Manager/Source"
msbuild PriorityManagerMod.csproj /p:Configuration=Release
```

## Verifying the Build

After building, check that these files exist:

1. `Assemblies/PriorityManager.dll` - The main mod DLL
2. `Assemblies/0Harmony.dll` - Harmony library (auto-downloaded)

If these files exist, the build was successful!

## Installing the Mod

### Option 1: Symlink (for development)
```bash
ln -s "/home/p4rad0x/.projects/rimworld_mods/Priority Manager" \
      "$HOME/.steam/steam/steamapps/common/RimWorld/Mods/Priority Manager"
```

### Option 2: Copy (for distribution)
```bash
cp -r "/home/p4rad0x/.projects/rimworld_mods/Priority Manager" \
      "$HOME/.steam/steam/steamapps/common/RimWorld/Mods/"
```

## Testing

1. Launch RimWorld
2. Go to Mods menu
3. Enable "Priority Manager"
4. Restart the game
5. Start or load a game
6. Open the Work tab - you should see "Priority Manager" and "Recalculate All" buttons

## Troubleshooting

### Error: "dotnet: command not found"
Install .NET SDK (see Prerequisites above)

### Error: "Could not find file Assembly-CSharp.dll"
The RimWorld path in `PriorityManagerMod.csproj` needs to be updated.
Edit line 13 to match your RimWorld installation path.

### Build succeeds but mod doesn't appear in RimWorld
- Check that the mod folder is in the correct location
- Verify `About/About.xml` exists
- Check RimWorld logs for errors (`~/.config/unity3d/Ludeon Studios/RimWorld by Ludeon Studios/Player.log`)

### Build succeeds but mod crashes on load
- Check `Player.log` for specific errors
- Verify all source files were compiled
- Try building in Debug mode for more detailed error messages:
  ```bash
  dotnet build -c Debug
  ```

## Development Workflow

For active development:

1. Use symlink installation (see above)
2. Make code changes
3. Run `./build.sh`
4. Restart RimWorld (or use HugsLib's quick restart if installed)
5. Test changes

## Files Generated During Build

These files/folders are created during build and can be safely deleted:

- `Source/obj/` - Temporary build files
- `Source/bin/` - Build output (before copying to Assemblies)

The actual mod DLLs are in `Assemblies/` and should NOT be deleted.

## Clean Build

To force a complete rebuild:
```bash
cd Source
rm -rf obj/ bin/
dotnet clean
dotnet build
```

## Release Build

For final distribution:
```bash
cd Source
dotnet build -c Release
```

This will create optimized DLLs in the `Assemblies/` folder.

You can then create a zip file:
```bash
cd ..
zip -r "PriorityManager.zip" "Priority Manager/" -x "*.git*" "*/obj/*" "*/bin/*" "*/.vs/*"
```

