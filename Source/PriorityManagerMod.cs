using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    public class PriorityManagerMod : Mod
    {
        public static Harmony harmony;
        public static PriorityManagerSettings settings;
        public static PriorityManagerMod Instance;
        
        private int currentTab = 0;
        private CustomRole selectedRole = null;
        private Vector2 roleListScrollPos = Vector2.zero;
        private Vector2 roleEditorScrollPos = Vector2.zero;

        public PriorityManagerMod(ModContentPack content) : base(content)
        {
            try
            {
                Log.Message("PriorityManager: Constructor starting...");
                
                Instance = this;
                settings = GetSettings<PriorityManagerSettings>();
                
                Log.Message("PriorityManager: Settings loaded, applying Harmony patches...");
                
                harmony = new Harmony("P4RAD0X.PriorityManager");
                harmony.PatchAll();
                
                Log.Message("Priority Manager v2.0 loaded successfully. Press 'N' or open the Work tab to access Priority Manager settings.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"PriorityManager: FAILED TO LOAD! Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            
            // Draw tabs
            Rect tabRect = new Rect(inRect.x, inRect.y, inRect.width, 40f);
            float tabWidth = inRect.width / 2f;
            
            Rect tab1Rect = new Rect(tabRect.x, tabRect.y, tabWidth, 32f);
            Rect tab2Rect = new Rect(tabRect.x + tabWidth, tabRect.y, tabWidth, 32f);
            
            if (Widgets.ButtonText(tab1Rect, "Global Settings", currentTab == 0))
            {
                currentTab = 0;
            }
            
            if (Widgets.ButtonText(tab2Rect, "Custom Roles", currentTab == 1))
            {
                currentTab = 1;
            }
            
            // Content area
            Rect contentRect = new Rect(inRect.x, inRect.y + 45f, inRect.width, inRect.height - 45f);
            
            if (currentTab == 0)
            {
                DrawGlobalSettings(contentRect);
            }
            else if (currentTab == 1)
            {
                DrawCustomRolesTab(contentRect);
            }
        }
        
        private void DrawGlobalSettings(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("Global Settings");
            Text.Font = GameFont.Small;
            listing.Gap();

            listing.Label("Auto-recalculation interval (0 = Manual only):");
            Rect sliderRect = listing.GetRect(30f);
            Rect labelRect = new Rect(sliderRect.x, sliderRect.y, sliderRect.width - 100f, 30f);
            Rect valueRect = new Rect(sliderRect.x + sliderRect.width - 90f, sliderRect.y, 90f, 30f);
            
            float newValue = Widgets.HorizontalSlider(
                labelRect,
                settings.autoRecalculateIntervalHours,
                0f,
                24f
            );
            settings.autoRecalculateIntervalHours = (int)newValue;

            if (settings.autoRecalculateIntervalHours == 0)
            {
                Widgets.Label(valueRect, "Manual Only");
            }
            else
            {
                Widgets.Label(valueRect, $"{settings.autoRecalculateIntervalHours} hours");
            }

            listing.Gap();
            listing.CheckboxLabeled("Enable auto-assignment globally", ref settings.globalAutoAssignEnabled, 
                "When enabled, the mod will automatically assign priorities to colonists based on their skills and roles.");
            
            listing.CheckboxLabeled("Reduce workload for ill/injured colonists", ref settings.illnessResponseEnabled,
                "When enabled, colonists with low health or serious injuries will have their work assignments reduced to essential self-care tasks.");
            
            // v2.0: Injury severity dropdown
            if (settings.illnessResponseEnabled)
            {
                listing.Gap(4f);
                Rect injuryLevelRect = listing.GetRect(30f);
                Rect injuryLabelRect = new Rect(injuryLevelRect.x + 30f, injuryLevelRect.y, injuryLevelRect.width * 0.5f - 40f, 30f);
                Rect injuryDropdownRect = new Rect(injuryLabelRect.xMax + 10f, injuryLevelRect.y, injuryLevelRect.width * 0.5f - 10f, 30f);
                
                Widgets.Label(injuryLabelRect, "    Injury severity threshold:");
                
                if (Widgets.ButtonText(injuryDropdownRect, GetInjurySeverityLabel(settings.injurySeverityThreshold)))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (InjurySeverityLevel level in System.Enum.GetValues(typeof(InjurySeverityLevel)))
                    {
                        options.Add(new FloatMenuOption(GetInjurySeverityLabel(level), () =>
                        {
                            settings.injurySeverityThreshold = level;
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                // Show description of current setting
                Rect descRect = listing.GetRect(Text.CalcHeight(GetInjurySeverityDescription(settings.injurySeverityThreshold), injuryLevelRect.width - 30f));
                Text.Font = GameFont.Tiny;
                GUI.color = Color.grey;
                Widgets.Label(new Rect(descRect.x + 30f, descRect.y, descRect.width - 30f, descRect.height), GetInjurySeverityDescription(settings.injurySeverityThreshold));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
            
            listing.CheckboxLabeled("Enable solo survival mode", ref settings.enableSoloSurvivalMode,
                "When enabled, a single colonist will use survival mode (all essential tasks enabled). Disable to use normal role-based assignment even with one colonist.");
            
            if (Prefs.DevMode)
            {
                listing.CheckboxLabeled("[Dev] Show performance overlay", ref settings.showPerformanceOverlay,
                    "v2.0: Shows real-time performance profiler overlay with method timing breakdowns. Only visible in dev mode.");
            }

            listing.Gap();
            listing.Gap();
            
            // Always Enabled Jobs Section
            Text.Font = GameFont.Medium;
            listing.Label("Always Enabled Jobs");
            Text.Font = GameFont.Small;
            listing.Gap();
            
            listing.Label("These jobs will be enabled at priority 1 for all colonists, regardless of their assigned role:");
            listing.Gap();
            
            // Get critical work types
            var criticalWorkTypes = new List<WorkTypeDef>();
            if (WorkTypeDefOf.Firefighter != null)
                criticalWorkTypes.Add(WorkTypeDefOf.Firefighter);
            if (WorkTypeDefOf.Doctor != null)
                criticalWorkTypes.Add(WorkTypeDefOf.Doctor);
            
            var patientWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("Patient");
            if (patientWork != null)
                criticalWorkTypes.Add(patientWork);
            
            var patientBedRestWork = DefDatabase<WorkTypeDef>.GetNamedSilentFail("PatientBedRest");
            if (patientBedRestWork != null)
                criticalWorkTypes.Add(patientBedRestWork);
            
            // Draw checkboxes for always-enabled jobs
            foreach (var workType in criticalWorkTypes)
            {
                bool isEnabled = settings.IsJobAlwaysEnabled(workType);
                bool newEnabled = isEnabled;
                
                listing.CheckboxLabeled(
                    workType.labelShort ?? workType.defName,
                    ref newEnabled,
                    workType.description
                );
                
                if (newEnabled != isEnabled)
                {
                    settings.SetJobAlwaysEnabled(workType, newEnabled);
                }
            }
            
            listing.Gap();
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Note: Always-enabled jobs will not count towards role-specific job assignments.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            listing.Gap();
            listing.Label("For more detailed colonist-specific settings, open the Priority Manager window from the Work tab or press 'N'.");

            listing.End();
        }
        
        private void DrawCustomRolesTab(Rect inRect)
        {
            // Split into left panel (role list) and right panel (role editor)
            float listWidth = inRect.width * 0.35f;
            Rect listRect = new Rect(inRect.x, inRect.y, listWidth - 5f, inRect.height);
            Rect editorRect = new Rect(inRect.x + listWidth + 5f, inRect.y, inRect.width - listWidth - 5f, inRect.height);
            
            DrawRoleList(listRect);
            Widgets.DrawLineVertical(listRect.xMax + 2.5f, listRect.y, listRect.height);
            DrawRoleEditor(editorRect);
        }
        
        private void DrawRoleList(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Custom Roles");
            Text.Font = GameFont.Small;
            
            // Create new role button
            Rect createButtonRect = new Rect(inRect.x, inRect.yMax - 35f, inRect.width, 30f);
            if (Widgets.ButtonText(createButtonRect, "Create New Role"))
            {
                CustomRole newRole = new CustomRole("New Role");
                settings.AddCustomRole(newRole);
                selectedRole = newRole;
                WriteSettings();
            }
            
            // Scrollable list of roles
            Rect listViewRect = new Rect(inRect.x, inRect.y + 35f, inRect.width, inRect.height - 75f);
            Rect listContentRect = new Rect(0f, 0f, listViewRect.width - 20f, settings.customRoles.Count * 60f);
            
            Widgets.BeginScrollView(listViewRect, ref roleListScrollPos, listContentRect);
            
            float curY = 0f;
            foreach (var role in settings.GetAllCustomRoles())
            {
                Rect roleRect = new Rect(0f, curY, listContentRect.width, 55f);
                
                if (selectedRole == role)
                {
                    Widgets.DrawHighlight(roleRect);
                }
                
                if (Widgets.ButtonInvisible(roleRect))
                {
                    selectedRole = role;
                }
                
                Rect nameRect = new Rect(roleRect.x + 5f, roleRect.y + 5f, roleRect.width - 10f, 25f);
                Rect summaryRect = new Rect(roleRect.x + 5f, roleRect.y + 30f, roleRect.width - 10f, 20f);
                
                Text.Font = GameFont.Small;
                Widgets.Label(nameRect, role.roleName);
                Text.Font = GameFont.Tiny;
                GUI.color = Color.gray;
                Widgets.Label(summaryRect, role.GetSummary());
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                
                curY += 60f;
            }
            
            Widgets.EndScrollView();
        }
        
        private void DrawRoleEditor(Rect inRect)
        {
            if (selectedRole == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inRect, "Select a role to edit or create a new one");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width - 80f, 30f), "Role Editor");
            Text.Font = GameFont.Small;
            
            // Delete role button
            Rect deleteRect = new Rect(inRect.xMax - 75f, inRect.y, 70f, 25f);
            GUI.color = new Color(1f, 0.3f, 0.3f);
            if (Widgets.ButtonText(deleteRect, "Delete"))
            {
                if (selectedRole != null)
                {
                    settings.RemoveCustomRole(selectedRole.roleId);
                    selectedRole = null;
                    WriteSettings();
                }
            }
            GUI.color = Color.white;
            
            if (selectedRole == null)
                return;
            
            // Role name
            Rect nameLabelRect = new Rect(inRect.x, inRect.y + 35f, 100f, 25f);
            Rect nameInputRect = new Rect(inRect.x + 105f, inRect.y + 35f, inRect.width - 105f, 25f);
            Widgets.Label(nameLabelRect, "Role Name:");
            selectedRole.roleName = Widgets.TextField(nameInputRect, selectedRole.roleName);
            
            // Add job button
            Rect addJobRect = new Rect(inRect.x, inRect.y + 70f, 120f, 30f);
            if (Widgets.ButtonText(addJobRect, "Add Job"))
            {
                Find.WindowStack.Add(new JobSelectorDialog(selectedRole, () => WriteSettings()));
            }
            
            // Save button
            Rect saveRect = new Rect(inRect.xMax - 125f, inRect.y + 70f, 120f, 30f);
            if (Widgets.ButtonText(saveRect, "Save Changes"))
            {
                settings.UpdateCustomRole(selectedRole);
                WriteSettings();
                Messages.Message($"Saved custom role: {selectedRole.roleName}", MessageTypeDefOf.TaskCompletion);
            }
            
            // Job list
            Rect jobListRect = new Rect(inRect.x, inRect.y + 110f, inRect.width, inRect.height - 115f);
            DrawJobList(jobListRect);
        }
        
        private void DrawJobList(Rect inRect)
        {
            if (selectedRole == null || selectedRole.jobs.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inRect, "No jobs assigned to this role. Click 'Add Job' to begin.");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }
            
            float rowHeight = 40f;
            Rect contentRect = new Rect(0f, 0f, inRect.width - 20f, selectedRole.jobs.Count * rowHeight);
            
            Widgets.BeginScrollView(inRect, ref roleEditorScrollPos, contentRect);
            
            float curY = 0f;
            var sortedJobs = selectedRole.GetSortedJobs();
            
            for (int i = 0; i < sortedJobs.Count; i++)
            {
                var job = sortedJobs[i];
                var workType = job.GetWorkTypeDef();
                
                if (workType == null)
                    continue;
                
                Rect rowRect = new Rect(0f, curY, contentRect.width, rowHeight - 2f);
                
                // Drag handle area (left side)
                Rect dragRect = new Rect(rowRect.x, rowRect.y, 30f, rowHeight);
                Widgets.DrawHighlightIfMouseover(dragRect);
                Widgets.Label(new Rect(dragRect.x + 5f, dragRect.y + 10f, 20f, 20f), ":::");
                
                // Job name
                Rect nameRect = new Rect(rowRect.x + 35f, rowRect.y + 5f, 200f, 30f);
                Widgets.Label(nameRect, workType.labelShort);
                
                // Importance dropdown
                Rect importanceRect = new Rect(rowRect.x + 245f, rowRect.y + 5f, 150f, 30f);
                if (Widgets.ButtonText(importanceRect, job.importance.ToString()))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (JobImportance importance in System.Enum.GetValues(typeof(JobImportance)))
                    {
                        options.Add(new FloatMenuOption(importance.ToString(), () => {
                            job.importance = importance;
                            WriteSettings();
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
                
                // Remove button
                Rect removeRect = new Rect(rowRect.xMax - 30f, rowRect.y + 5f, 25f, 25f);
                GUI.color = new Color(1f, 0.3f, 0.3f);
                if (Widgets.ButtonText(removeRect, "X"))
                {
                    selectedRole.RemoveJob(job.workTypeDefName);
                    WriteSettings();
                }
                GUI.color = Color.white;
                
                Widgets.DrawLineHorizontal(rowRect.x, curY + rowHeight - 1f, rowRect.width);
                curY += rowHeight;
            }
            
            Widgets.EndScrollView();
        }

        public override string SettingsCategory()
        {
            return "Priority Manager";
        }
        
        // v2.0: Helper methods for injury severity dropdown
        private string GetInjurySeverityLabel(InjurySeverityLevel level)
        {
            switch (level)
            {
                case InjurySeverityLevel.Disabled:
                    return "Disabled";
                case InjurySeverityLevel.SevereOnly:
                    return "Severe Injuries Only (<30% health)";
                case InjurySeverityLevel.MajorInjuries:
                    return "Major Injuries (<50% health)";
                case InjurySeverityLevel.AnyInjury:
                    return "Any Injury (<80% health)";
                case InjurySeverityLevel.MinorInjuries:
                    return "Minor Injuries (<95% health)";
                default:
                    return "Unknown";
            }
        }
        
        private string GetInjurySeverityDescription(InjurySeverityLevel level)
        {
            switch (level)
            {
                case InjurySeverityLevel.Disabled:
                    return "Injuries will not affect work assignments.";
                case InjurySeverityLevel.SevereOnly:
                    return "Only life-threatening injuries (<30% health) will reduce workload to Firefighter, Doctor, and Patient/Bedrest.";
                case InjurySeverityLevel.MajorInjuries:
                    return "Significant injuries or illnesses (<50% health) will reduce workload to Firefighter, Doctor, and Patient/Bedrest.";
                case InjurySeverityLevel.AnyInjury:
                    return "Any injury or illness that affects capabilities (<80% health) will reduce workload to Firefighter, Doctor, and Patient/Bedrest.";
                case InjurySeverityLevel.MinorInjuries:
                    return "Even minor injuries (<95% health) will reduce workload to Firefighter, Doctor, and Patient/Bedrest.";
                default:
                    return "";
            }
        }
    }
}

