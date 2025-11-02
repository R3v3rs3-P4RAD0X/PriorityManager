# Priority Manager - Testing Checklist

## Build and Installation

### Build
- [ ] Run `./build.sh` successfully (or `dotnet build` in Source directory)
- [ ] `PriorityManager.dll` created in Assemblies folder
- [ ] `0Harmony.dll` copied to Assemblies folder
- [ ] No compilation errors or warnings

### Installation
- [ ] Copy mod folder to RimWorld/Mods directory
- [ ] Mod appears in RimWorld mod list
- [ ] Mod can be enabled without errors
- [ ] Game loads to main menu successfully
- [ ] Console shows "Priority Manager loaded successfully" message

## Basic Functionality Tests

### UI Access
- [ ] Work tab displays "Priority Manager" button
- [ ] Work tab displays "Recalculate All" button
- [ ] Clicking "Priority Manager" opens config window
- [ ] Pressing 'P' key opens/closes config window
- [ ] Config window is draggable and resizable

### Configuration Window
- [ ] Window title displays correctly
- [ ] Global settings section visible
- [ ] Auto-recalculation slider (0-24 hours) works
- [ ] "Enable auto-assignment globally" checkbox toggles
- [ ] "Reduce workload for ill/injured colonists" checkbox toggles
- [ ] "Recalculate All Priorities Now" button works
- [ ] "Reset All to Auto" button works
- [ ] Window can be closed with X button

## Single Colonist Scenario

### Test Setup
1. Start new game (any scenario)
2. Dev mode: kill all colonists except one
3. Enable Priority Manager

### Tests
- [ ] Solo colonist receives survival priorities automatically
- [ ] Firefighter = 1
- [ ] Patient/Bed Rest = 1
- [ ] Hunting, Cooking, Growing = 1-2
- [ ] Construction, Repair, Mining = 3
- [ ] Hauling, Cleaning = 4
- [ ] Research = 4 (if capable)
- [ ] Colonist can perform all essential survival tasks

### Edge Cases
- [ ] Works with starting colonist (Crashlanded scenario)
- [ ] Works with tribal start
- [ ] Works after other colonists die

## Multiple Colonists Scenario

### Test Setup
1. Start game with 3+ colonists
2. Enable Priority Manager
3. Set all to "Auto" role

### Tests
- [ ] Each colonist gets different primary job
- [ ] Primary jobs match colonists' best skills
- [ ] Secondary jobs (2-4) assigned based on remaining skills
- [ ] No colonist has all jobs enabled
- [ ] Critical jobs (Firefighter, Patient) always at priority 1
- [ ] System avoids assigning same primary job to all colonists

### Colonist Distribution
- [ ] With 3 colonists: 3 different primary roles
- [ ] With 6 colonists: reasonable distribution of roles
- [ ] High-skill colonist gets matching role (e.g., 15 Medicine → Doctor)
- [ ] Low-skill colonist assigned to appropriate role

## Role Preset System

### Auto Role
- [ ] Colonist with highest Medical skill becomes Doctor
- [ ] Colonist with highest Intellectual becomes Researcher
- [ ] Colonist with highest Plants becomes Farmer
- [ ] System picks best role for mixed-skill colonist
- [ ] Passions influence role selection (major > minor > none)

### Specific Role Presets
Test each role preset:

- [ ] **Researcher**: Research = 1, other jobs 2-4 by skill
- [ ] **Doctor**: Doctor = 1, other jobs 2-4 by skill
- [ ] **Crafter**: Crafting = 1, other jobs 2-4 by skill
- [ ] **Farmer**: Growing = 1, PlantCutting enabled, other jobs 2-4
- [ ] **Constructor**: Construction = 1, Repair enabled, other jobs 2-4
- [ ] **Miner**: Mining = 1, other jobs 2-4 by skill
- [ ] **Cook**: Cooking = 1, other jobs 2-4 by skill
- [ ] **Hunter**: Hunting = 1, other jobs 2-4 by skill
- [ ] **Hauler**: Hauling = 1, other jobs 2-4 by skill
- [ ] **Cleaner**: Cleaning = 1, other jobs 2-4 by skill
- [ ] **Warden**: Warden = 1, other jobs 2-4 by skill

### Manual Role
- [ ] Setting colonist to "Manual" disables auto-assignment
- [ ] Manual priorities are preserved
- [ ] "Recalculate All" does not affect manual colonists
- [ ] Per-colonist "Recalculate" does not work on manual colonists

## Illness/Injury Response

### Test Setup
1. Have healthy colonist with auto-assignment enabled
2. Note their current work priorities

### Injury Tests
- [ ] Colonist injured to <50% health → work reduced to Patient/Bed Rest
- [ ] Colonist with disease → work reduced
- [ ] Colonist with infection → work reduced
- [ ] Colonist recovers to >50% health → normal priorities restored

### Edge Cases
- [ ] Doctor colonist can still self-tend if Medicine ≥ 3
- [ ] Non-medical colonist has only Patient/Bed Rest when ill
- [ ] Multiple injuries/illnesses handled correctly
- [ ] Setting disabled → ill colonists keep normal priorities

## Auto-Recalculation

### Interval Tests
- [ ] Setting interval to 0 → no auto-recalculation
- [ ] Setting interval to 1 hour → recalculates every game hour
- [ ] Setting interval to 12 hours → recalculates every 12 game hours
- [ ] Setting interval to 24 hours → recalculates every game day

### Trigger Events
- [ ] Manual "Recalculate Now" works immediately
- [ ] Per-colonist "Recalculate" works immediately
- [ ] Auto-recalculation respects interval setting
- [ ] Global toggle off → no recalculation

### Performance
- [ ] Auto-recalculation doesn't cause lag (10+ colonists)
- [ ] Auto-recalculation doesn't cause lag (20+ colonists)
- [ ] No error messages during recalculation

## New Colonist Events

### Tests
- [ ] New colonist joins (quest reward) → auto-assigned
- [ ] Refugee joins → auto-assigned
- [ ] Prisoner recruited → auto-assigned
- [ ] New colonist gets "Auto" role by default
- [ ] First colonist gets survival priorities
- [ ] Second colonist gets complementary role to first

## Save/Load Persistence

### Settings Persistence
- [ ] Save game with custom interval setting
- [ ] Load game → interval setting preserved
- [ ] Save game with global auto-assign disabled
- [ ] Load game → setting preserved
- [ ] Mod options settings persist across game sessions

### Colonist Data Persistence
- [ ] Save game with colonist roles assigned
- [ ] Load game → roles preserved
- [ ] Save game with mix of Auto/Manual colonists
- [ ] Load game → auto-assignment states preserved
- [ ] Save during illness → load → illness response still active

### Migration Tests
- [ ] Load existing save (without mod) → colonists get default Auto role
- [ ] Add mod to existing save → no errors
- [ ] Remove mod from save → no corruption (priorities remain as-is)

## Edge Cases and Error Handling

### Disabled Work Types
- [ ] Colonist with disability (e.g., can't do Social) → skill excluded
- [ ] Trait prevents work (e.g., "Too Smart" → no Dumb Labor) → handled
- [ ] Backstory prevents work → handled gracefully

### Skills and Passions
- [ ] Colonist with all passions gets appropriate priorities
- [ ] Colonist with no passions gets reasonable priorities
- [ ] Colonist with burning passion → job prioritized over higher skill without passion
- [ ] Low-skill colonist with major passion vs high-skill without passion

### Extreme Scenarios
- [ ] 1 colonist → survival mode
- [ ] 20+ colonists → all get appropriate roles
- [ ] All colonists set to Manual → no auto-assignment
- [ ] All colonists incapacitated → system doesn't crash
- [ ] Remove all work priorities → system can reassign

### Mod Compatibility
- [ ] Works with other work-related mods (if any installed)
- [ ] Work tab mods don't conflict
- [ ] No errors in log related to Priority Manager

## UI/UX Tests

### Colonist List Display
- [ ] All colonists shown in scrollable list
- [ ] Colonist names display correctly
- [ ] Current role displays correctly
- [ ] Auto-assign checkbox state correct
- [ ] Alternating row backgrounds visible

### Interaction Tests
- [ ] "Change Role" button opens dropdown menu
- [ ] Dropdown shows all role presets
- [ ] Selecting role updates colonist immediately
- [ ] Checkbox toggles auto-assignment correctly
- [ ] "Recalculate" button triggers immediate update

### Visual Feedback
- [ ] Messages appear when recalculating ("X recalculated")
- [ ] Messages appear when changing roles
- [ ] No visual glitches or overlapping text
- [ ] Window scrolls smoothly with many colonists
- [ ] Buttons are clickable and responsive

## Performance Tests

### Small Colony (1-5 colonists)
- [ ] No noticeable lag
- [ ] Priorities update quickly
- [ ] UI responsive

### Medium Colony (6-15 colonists)
- [ ] No noticeable lag
- [ ] Priorities update within 1 second
- [ ] UI remains responsive

### Large Colony (15+ colonists)
- [ ] Acceptable lag (< 2 seconds for full recalculation)
- [ ] UI doesn't freeze
- [ ] No error spam in console

### Long-term Tests
- [ ] Run colony for 1 in-game year with auto-recalculation
- [ ] No memory leaks
- [ ] No increasing lag over time
- [ ] No corruption of save file

## Regression Tests

After any code changes, verify:
- [ ] All previous tests still pass
- [ ] No new errors in console
- [ ] Existing saves still load
- [ ] Settings still save/load correctly

## User Experience Tests

### First-time User
- [ ] Keybind suggestion appears on mod load
- [ ] Default settings are reasonable
- [ ] UI is intuitive without reading docs
- [ ] Error states are informative (if any)

### Power User
- [ ] Can quickly assign roles to many colonists
- [ ] Manual override works as expected
- [ ] Can disable mod without breaking game
- [ ] Advanced settings accessible

---

## Test Results Log

**Date**: _____________  
**Tester**: _____________  
**RimWorld Version**: _____________  
**Mod Version**: _____________  

### Critical Issues Found
1. 
2. 
3. 

### Minor Issues Found
1. 
2. 
3. 

### Suggestions
1. 
2. 
3. 

### Overall Assessment
- [ ] Ready for release
- [ ] Needs minor fixes
- [ ] Needs major fixes
- [ ] Not ready for testing

**Notes**:


