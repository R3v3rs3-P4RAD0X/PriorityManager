using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    public enum ConfigTab
    {
        Colonists,
        JobSettings,
        PriorityMaster
    }

    public class ConfigWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 jobScrollPosition = Vector2.zero;
        private const float ROW_HEIGHT = 35f;
        private PriorityManagerSettings settings;
        private ConfigTab currentTab = ConfigTab.Colonists;

        public override Vector2 InitialSize => new Vector2(1000f, 700f);

        public ConfigWindow()
        {
            doCloseButton = true;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            settings = PriorityManagerMod.settings;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            float yOffset = 0f;

            // Title - ABOVE tabs
            Rect titleRect = new Rect(0f, yOffset, inRect.width, 35f);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, "Priority Manager Settings");
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            yOffset += 40f;

            // Tab buttons
            Rect tabRect = new Rect(0f, yOffset, inRect.width, 40f);
            DrawTabs(tabRect);
            yOffset += 45f;

            // Content area based on selected tab
            Rect contentRect = new Rect(0f, yOffset, inRect.width, inRect.height - yOffset - 50f);
            
            if (currentTab == ConfigTab.Colonists)
            {
                DrawColonistsTab(contentRect);
            }
            else if (currentTab == ConfigTab.JobSettings)
            {
                DrawJobSettingsTab(contentRect);
            }
            else if (currentTab == ConfigTab.PriorityMaster)
            {
                DrawPriorityMasterTab(contentRect);
            }

            // Bottom buttons
            Rect bottomRect = new Rect(0f, inRect.height - 40f, inRect.width, 35f);
            DrawBottomButtons(bottomRect);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawTabs(Rect rect)
        {
            List<TabRecord> tabs = new List<TabRecord>();
            
            tabs.Add(new TabRecord("Colonists", delegate
            {
                currentTab = ConfigTab.Colonists;
            }, currentTab == ConfigTab.Colonists));
            
            tabs.Add(new TabRecord("Job Settings", delegate
            {
                currentTab = ConfigTab.JobSettings;
            }, currentTab == ConfigTab.JobSettings));
            
            tabs.Add(new TabRecord("PriorityMaster", delegate
            {
                currentTab = ConfigTab.PriorityMaster;
            }, currentTab == ConfigTab.PriorityMaster));

            TabDrawer.DrawTabs(rect, tabs);
        }

        private void DrawColonistsTab(Rect rect)
        {
            // Global Settings Section - fixed height
            float globalSettingsHeight = 180f;
            Rect globalSettingsRect = new Rect(rect.x, rect.y, rect.width, globalSettingsHeight);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(globalSettingsRect);
            DrawGlobalSettings(listing);
            
            // Info about manual control
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            listing.Label("Tip: Uncheck 'Auto-Assign' to manually control a colonist's priorities.");
            listing.Label("Manual colonists are counted for min/max worker limits but ignored for job distribution.");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            listing.End();

            // Colonist List Section - starts below global settings
            Rect colonistListRect = new Rect(rect.x, rect.y + globalSettingsHeight + 10f, rect.width, rect.height - globalSettingsHeight - 10f);
            DrawColonistList(colonistListRect);
        }

        private void DrawJobSettingsTab(Rect rect)
        {
            // Header section - fixed height
            float headerHeight = 80f;
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, headerHeight);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(headerRect);
            listing.Label("Job Type Priority Settings");
            listing.Gap();
            listing.Label("Configure which jobs should be prioritized, deprioritized, or disabled.");
            listing.End();

            // Job settings list - starts below header
            Rect jobListRect = new Rect(rect.x, rect.y + headerHeight + 5f, rect.width, rect.height - headerHeight - 5f);
            DrawJobSettingsList(jobListRect);
        }

        private void DrawGlobalSettings(Listing_Standard listing)
        {
            listing.Label("Global Settings:");
            
            // Auto-recalculation interval
            Rect intervalRect = listing.GetRect(30f);
            Rect labelRect = new Rect(intervalRect.x, intervalRect.y, 300f, 30f);
            Rect sliderRect = new Rect(intervalRect.x + 310f, intervalRect.y, 200f, 30f);
            Rect valueRect = new Rect(intervalRect.x + 520f, intervalRect.y, 80f, 30f);

            Widgets.Label(labelRect, $"Auto-recalculate interval:");
            float newValue = Widgets.HorizontalSlider(
                sliderRect,
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

            // Global auto-assign toggle
            listing.CheckboxLabeled("Enable auto-assignment globally", ref settings.globalAutoAssignEnabled);

            // Illness response toggle
            listing.CheckboxLabeled("Reduce workload for ill/injured colonists", ref settings.illnessResponseEnabled);

            // Recalculate now button
            if (listing.ButtonText("Recalculate All Priorities Now"))
            {
                PriorityAssigner.AssignAllColonistPriorities(true);
                Messages.Message("All colonist priorities have been recalculated.", MessageTypeDefOf.TaskCompletion);
            }
        }

        private void DrawColonistList(Rect rect)
        {
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp == null)
            {
                Widgets.Label(rect, "No game in progress.");
                return;
            }

            var colonists = gameComp.GetAllColonists();
            if (colonists.Count == 0)
            {
                Widgets.Label(rect, "No colonists found.");
                return;
            }

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            GUI.BeginGroup(headerRect);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            
            Widgets.Label(new Rect(10f, 0f, 150f, 30f), "Colonist");
            Widgets.Label(new Rect(170f, 0f, 120f, 30f), "Best Skill");
            Widgets.Label(new Rect(300f, 0f, 130f, 30f), "Current Role");
            Widgets.Label(new Rect(440f, 0f, 100f, 30f), "Auto-Assign");
            Widgets.Label(new Rect(550f, 0f, 250f, 30f), "Actions");
            
            GUI.color = Color.white;
            GUI.EndGroup();

            Widgets.DrawLineHorizontal(rect.x, rect.y + 30f, rect.width);

            // Scrollable list
            Rect scrollRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, colonists.Count * ROW_HEIGHT);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (var colonist in colonists)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, ROW_HEIGHT);
                DrawColonistRow(rowRect, colonist, gameComp);
                y += ROW_HEIGHT;
            }

            Widgets.EndScrollView();
        }

        private void DrawColonistRow(Rect rect, Pawn colonist, PriorityManagerGameComponent gameComp)
        {
            // Alternating background
            if (((int)(rect.y / ROW_HEIGHT)) % 2 == 0)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            }

            GUI.BeginGroup(rect);
            Text.Anchor = TextAnchor.MiddleLeft;

            var data = gameComp.GetOrCreateData(colonist);

            // Colonist name
            Widgets.Label(new Rect(10f, 0f, 150f, ROW_HEIGHT), colonist.Name.ToStringShort);

            // Best skill
            string bestSkillText = GetBestSkillText(colonist);
            Widgets.Label(new Rect(170f, 0f, 120f, ROW_HEIGHT), bestSkillText);

            // Current role
            string roleText = RolePresetUtility.GetRoleLabel(data.assignedRole);
            Widgets.Label(new Rect(300f, 0f, 130f, ROW_HEIGHT), roleText);

            // Auto-assign toggle
            bool autoAssign = data.autoAssignEnabled;
            Widgets.Checkbox(480f, 7f, ref autoAssign);
            if (autoAssign != data.autoAssignEnabled)
            {
                data.autoAssignEnabled = autoAssign;
                if (autoAssign)
                {
                    PriorityAssigner.AssignPriorities(colonist, true);
                }
            }

            // Change role button
            if (Widgets.ButtonText(new Rect(550f, 5f, 120f, 25f), "Change Role"))
            {
                ShowRoleSelectionMenu(colonist, data);
            }

            // Recalculate button
            if (Widgets.ButtonText(new Rect(680f, 5f, 100f, 25f), "Recalculate"))
            {
                PriorityAssigner.AssignPriorities(colonist, true);
                Messages.Message($"Priorities recalculated for {colonist.Name.ToStringShort}.", MessageTypeDefOf.TaskCompletion);
            }

            GUI.EndGroup();
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private string GetBestSkillText(Pawn colonist)
        {
            if (colonist.skills == null)
                return "-";

            SkillRecord bestSkill = null;
            float bestScore = -1f;

            foreach (var skill in colonist.skills.skills)
            {
                if (skill.TotallyDisabled)
                    continue;

                float score = skill.Level;
                
                // Factor in passion
                if (skill.passion == Passion.Major)
                    score += 8f;
                else if (skill.passion == Passion.Minor)
                    score += 4f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSkill = skill;
                }
            }

            if (bestSkill == null)
                return "-";

            string passionIcon = "";
            if (bestSkill.passion == Passion.Major)
                passionIcon = " 🔥🔥";
            else if (bestSkill.passion == Passion.Minor)
                passionIcon = " 🔥";

            return $"{bestSkill.def.skillLabel} {bestSkill.Level}{passionIcon}";
        }

        private void ShowRoleSelectionMenu(Pawn colonist, ColonistRoleData data)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (var preset in RolePresetUtility.GetAllPresets())
            {
                string label = RolePresetUtility.GetRoleLabel(preset);
                
                options.Add(new FloatMenuOption(label, () =>
                {
                    data.assignedRole = preset;
                    data.autoAssignEnabled = (preset != RolePreset.Manual);
                    
                    if (preset != RolePreset.Manual)
                    {
                        PriorityAssigner.AssignPriorities(colonist, true);
                        Messages.Message($"{colonist.Name.ToStringShort} assigned to {label} role.", MessageTypeDefOf.TaskCompletion);
                    }
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void DrawJobSettingsList(Rect rect)
        {
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible)
                .OrderBy(wt => wt.naturalPriority)
                .ToList();

            if (allWorkTypes.Count == 0)
            {
                Widgets.Label(rect, "No work types found.");
                return;
            }

            // Header
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            GUI.BeginGroup(headerRect);
            
            Text.Anchor = TextAnchor.MiddleLeft;
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            
            Widgets.Label(new Rect(10f, 0f, 180f, 30f), "Job Type");
            Widgets.Label(new Rect(200f, 0f, 130f, 30f), "Importance");
            Widgets.Label(new Rect(340f, 0f, 250f, 30f), "Description");
            Widgets.Label(new Rect(600f, 0f, 80f, 30f), "Workers");
            Widgets.Label(new Rect(690f, 0f, 80f, 30f), "Skills");
            Widgets.Label(new Rect(780f, 0f, 100f, 30f), "");
            
            GUI.color = Color.white;
            GUI.EndGroup();

            Widgets.DrawLineHorizontal(rect.x, rect.y + 30f, rect.width);

            // Scrollable list
            Rect scrollRect = new Rect(rect.x, rect.y + 35f, rect.width, rect.height - 35f);
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, allWorkTypes.Count * ROW_HEIGHT);

            Widgets.BeginScrollView(scrollRect, ref jobScrollPosition, viewRect);

            float y = 0f;
            foreach (var workType in allWorkTypes)
            {
                Rect rowRect = new Rect(0f, y, viewRect.width, ROW_HEIGHT);
                DrawJobSettingRow(rowRect, workType);
                y += ROW_HEIGHT;
            }

            Widgets.EndScrollView();
        }

        private void DrawJobSettingRow(Rect rect, WorkTypeDef workType)
        {
            // Alternating background
            if (((int)(rect.y / ROW_HEIGHT)) % 2 == 0)
            {
                Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.3f));
            }

            GUI.BeginGroup(rect);
            Text.Anchor = TextAnchor.MiddleLeft;

            // Job name
            Widgets.Label(new Rect(10f, 0f, 180f, ROW_HEIGHT), workType.labelShort ?? workType.defName);

            // Current importance setting
            JobImportance currentImportance = settings.GetJobImportance(workType);
            string importanceText = GetImportanceLabel(currentImportance);
            
            // Importance dropdown button
            if (Widgets.ButtonText(new Rect(200f, 5f, 130f, 25f), importanceText))
            {
                ShowImportanceMenu(workType, currentImportance);
            }

            // Description (truncated)
            string desc = workType.description ?? "";
            if (desc.Length > 30)
                desc = desc.Substring(0, 27) + "...";
            Widgets.Label(new Rect(340f, 0f, 250f, ROW_HEIGHT), desc);

            // Worker count
            int minWorkers = settings.GetRawMinWorkers(workType);
            int maxWorkers = settings.GetRawMaxWorkers(workType);
            bool usePercentage = settings.IsUsingPercentage(workType);
            string workerText = "";
            
            if (minWorkers > 0 || maxWorkers > 0)
            {
                string unit = usePercentage ? "%" : "";
                if (maxWorkers == 0)
                    workerText = $"{minWorkers}{unit}+";
                else if (minWorkers == maxWorkers)
                    workerText = $"{minWorkers}{unit}";
                else
                    workerText = $"{minWorkers}-{maxWorkers}{unit}";
            }
            else
            {
                workerText = "Auto";
            }
            Widgets.Label(new Rect(600f, 0f, 80f, ROW_HEIGHT), workerText);

            // Relevant skills
            string skillsText = GetRelevantSkillsText(workType);
            Widgets.Label(new Rect(690f, 0f, 80f, ROW_HEIGHT), skillsText);

            // Edit button
            if (Widgets.ButtonText(new Rect(780f, 5f, 60f, 25f), "Edit"))
            {
                Find.WindowStack.Add(new JobEditWindow(workType));
            }

            // Info button
            if (Widgets.ButtonText(new Rect(850f, 5f, 60f, 25f), "Info"))
            {
                ShowJobInfo(workType);
            }

            GUI.EndGroup();
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private string GetRelevantSkillsText(WorkTypeDef workType)
        {
            if (workType.relevantSkills == null || workType.relevantSkills.Count == 0)
                return "None";

            if (workType.relevantSkills.Count == 1)
                return workType.relevantSkills[0].skillLabel;

            return workType.relevantSkills[0].skillLabel + " +";
        }

        private string GetImportanceLabel(JobImportance importance)
        {
            switch (importance)
            {
                case JobImportance.Disabled: return "❌ Disabled";
                case JobImportance.VeryLow: return "⬇️ Very Low";
                case JobImportance.Low: return "⬇ Low";
                case JobImportance.Normal: return "➡ Normal";
                case JobImportance.High: return "⬆ High";
                case JobImportance.Critical: return "⚠️ Critical";
                default: return "Normal";
            }
        }

        private void ShowImportanceMenu(WorkTypeDef workType, JobImportance current)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (JobImportance importance in System.Enum.GetValues(typeof(JobImportance)))
            {
                string label = GetImportanceLabel(importance);
                string description = GetImportanceDescription(importance);
                
                options.Add(new FloatMenuOption($"{label} - {description}", () =>
                {
                    settings.SetJobImportance(workType, importance);
                    PriorityManagerMod.Instance.WriteSettings();
                    Messages.Message($"{workType.labelShort} importance set to {label}", MessageTypeDefOf.TaskCompletion);
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetImportanceDescription(JobImportance importance)
        {
            switch (importance)
            {
                case JobImportance.Disabled: return "Never assign this job";
                case JobImportance.VeryLow: return "Only as last resort (priority 4)";
                case JobImportance.Low: return "Backup job (priority 3-4)";
                case JobImportance.Normal: return "Standard assignment (priority 2-4)";
                case JobImportance.High: return "Preferred job (priority 1-2)";
                case JobImportance.Critical: return "Always assign (priority 1)";
                default: return "";
            }
        }

        private string GetJobImportance(WorkTypeDef workType)
        {
            // Critical jobs
            if (workType == WorkTypeDefOf.Firefighter)
                return "Critical";
            if (workType == WorkTypeDefOf.Doctor)
                return "Essential";

            // Survival jobs
            if (workType == DefDatabase<WorkTypeDef>.GetNamedSilentFail("Cooking") ||
                workType == WorkTypeDefOf.Hunting ||
                workType == WorkTypeDefOf.Growing)
                return "High";

            // Production/building
            if (workType == WorkTypeDefOf.Construction ||
                workType == WorkTypeDefOf.Mining ||
                workType == WorkTypeDefOf.Crafting)
                return "Medium";

            // Support jobs
            if (workType == WorkTypeDefOf.Hauling ||
                workType == WorkTypeDefOf.Cleaning)
                return "Low";

            return "Normal";
        }

        private void ShowJobInfo(WorkTypeDef workType)
        {
            string skillsText = "";
            if (workType.relevantSkills != null && workType.relevantSkills.Count > 0)
            {
                skillsText = "\n\nRelevant Skills:\n" + string.Join(", ", workType.relevantSkills.Select(s => s.skillLabel));
            }

            string message = $"{workType.labelShort}\n\n{workType.description ?? "No description"}{skillsText}\n\nNatural Priority: {workType.naturalPriority}";
            
            Find.WindowStack.Add(new Dialog_MessageBox(message));
        }

        private void DrawBottomButtons(Rect rect)
        {
            GUI.BeginGroup(rect);

            if (currentTab == ConfigTab.Colonists)
            {
                if (Widgets.ButtonText(new Rect(0f, 0f, 200f, 30f), "Reset All to Auto"))
                {
                    var gameComp = PriorityDataHelper.GetGameComponent();
                    if (gameComp != null)
                    {
                        gameComp.ResetAllToAuto();
                        PriorityAssigner.AssignAllColonistPriorities(true);
                        Messages.Message("All colonists reset to Auto mode and recalculated.", MessageTypeDefOf.TaskCompletion);
                    }
                }
            }

            GUI.EndGroup();
        }
        
        private void DrawPriorityMasterTab(Rect rect)
        {
            var listing = new Listing_Standard();
            listing.Begin(rect);
            
            // Detection status
            if (PriorityMasterCompat.IsLoaded())
            {
                listing.Label($"PriorityMaster Detected - Max Priority: {PriorityMasterCompat.GetMaxPriority()}");
                listing.Label(PriorityMasterCompat.GetMappingDescription());
            }
            else
            {
                listing.Label("PriorityMaster not detected (using vanilla 1-4 priorities)");
                listing.Gap();
                listing.Label("Install PriorityMaster to enable extended priority ranges (1-99).");
                listing.End();
                return;
            }
            
            listing.Gap();
            
            // Integration toggle
            listing.CheckboxLabeled("Enable PriorityMaster Integration", ref settings.enablePriorityMasterIntegration, 
                "When enabled, priorities will be scaled to PriorityMaster's extended range");
            
            if (!settings.enablePriorityMasterIntegration)
            {
                listing.Gap();
                listing.Label("Integration disabled. Using vanilla 1-4 priorities.");
                listing.End();
                return;
            }
            
            listing.Gap();
            listing.Gap();
            
            // Preset selection
            Text.Font = GameFont.Medium;
            listing.Label("Priority Distribution Preset:");
            Text.Font = GameFont.Small;
            listing.Gap();
            
            if (listing.RadioButton("Tight Spacing (10, 20, 30, 40)", settings.priorityPreset == PriorityPreset.Tight))
            {
                settings.ApplyPreset(PriorityPreset.Tight);
                settings.useCustomMapping = true;
            }
            
            if (listing.RadioButton("Balanced Spread (10, 30, 60, 90) - Default", settings.priorityPreset == PriorityPreset.Balanced))
            {
                settings.ApplyPreset(PriorityPreset.Balanced);
                settings.useCustomMapping = true;
            }
            
            if (listing.RadioButton("Wide Spread (5, 25, 55, 95)", settings.priorityPreset == PriorityPreset.Wide))
            {
                settings.ApplyPreset(PriorityPreset.Wide);
                settings.useCustomMapping = true;
            }
            
            if (listing.RadioButton("Custom Mapping", settings.priorityPreset == PriorityPreset.Custom))
            {
                settings.priorityPreset = PriorityPreset.Custom;
                settings.useCustomMapping = true;
            }
            
            listing.Gap();
            listing.Gap();
            
            // Custom mapping sliders (only for Custom preset)
            if (settings.priorityPreset == PriorityPreset.Custom)
            {
                Text.Font = GameFont.Medium;
                listing.Label("Custom Priority Mapping:");
                Text.Font = GameFont.Small;
                listing.Gap();
                
                int maxPriority = PriorityMasterCompat.GetMaxPriority();
                
                for (int i = 1; i <= 4; i++)
                {
                    int currentValue = settings.customPriorityMapping.TryGetValue(i, out int val) ? val : PriorityMasterCompat.ScalePriority(i);
                    
                    Rect sliderRect = listing.GetRect(30f);
                    Rect labelRect = new Rect(sliderRect.x, sliderRect.y, 150f, sliderRect.height);
                    Rect valueRect = new Rect(sliderRect.x + sliderRect.width - 50f, sliderRect.y, 50f, sliderRect.height);
                    Rect actualSliderRect = new Rect(labelRect.xMax + 5f, sliderRect.y, sliderRect.width - labelRect.width - valueRect.width - 15f, sliderRect.height);
                    
                    Widgets.Label(labelRect, $"Priority {i} →");
                    int newValue = Mathf.RoundToInt(Widgets.HorizontalSlider(actualSliderRect, currentValue, 1, maxPriority, true));
                    Widgets.Label(valueRect, newValue.ToString());
                    
                    settings.customPriorityMapping[i] = newValue;
                }
            }
            else if (settings.useCustomMapping)
            {
                // Show current mapping for non-custom presets
                Text.Font = GameFont.Medium;
                listing.Label("Current Mapping:");
                Text.Font = GameFont.Small;
                listing.Gap();
                
                for (int i = 1; i <= 4; i++)
                {
                    int value = settings.customPriorityMapping.TryGetValue(i, out int val) ? val : PriorityMasterCompat.ScalePriority(i);
                    listing.Label($"  Priority {i} → {value}");
                }
            }
            
            listing.Gap();
            listing.Gap();
            
            // Recalculate button
            if (listing.ButtonText("Recalculate All Priorities"))
            {
                PriorityAssigner.AssignAllColonistPriorities(true);
                Messages.Message("All priorities recalculated with PriorityMaster scaling.", MessageTypeDefOf.TaskCompletion);
            }
            
            listing.End();
        }

        public override void PreClose()
        {
            base.PreClose();
            // Save settings
            PriorityManagerMod.Instance.WriteSettings();
        }
    }
}

