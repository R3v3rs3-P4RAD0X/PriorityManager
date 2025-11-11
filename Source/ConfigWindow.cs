using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    public enum ConfigTab
    {
        Dashboard,
        Colonists,
        JobSettings,
        PriorityMaster
    }

    public class ConfigWindow : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private Vector2 jobScrollPosition = Vector2.zero;
        private Vector2 dashboardScrollPosition = Vector2.zero;
        private Vector2 skillMatrixScrollPosition = Vector2.zero;
        private const float ROW_HEIGHT = 35f;
        private PriorityManagerSettings settings;
        private ConfigTab currentTab = ConfigTab.Dashboard;
        private bool showSkillMatrix = false;
        private bool showOnlyGaps = false;

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

            // Title
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, yOffset, inRect.width, 35f), "Priority Manager Settings");
            Text.Font = GameFont.Small;
            yOffset += 40f;

            // Tab buttons
            Rect tabRect = new Rect(0f, yOffset, inRect.width, 40f);
            DrawTabs(tabRect);
            yOffset += 50f;

            // Content area based on selected tab
            Rect contentRect = new Rect(0f, yOffset, inRect.width, inRect.height - yOffset - 50f);
            
            if (currentTab == ConfigTab.Dashboard)
            {
                DrawDashboardTab(contentRect);
            }
            else if (currentTab == ConfigTab.Colonists)
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
            
            tabs.Add(new TabRecord("Dashboard", delegate
            {
                currentTab = ConfigTab.Dashboard;
            }, currentTab == ConfigTab.Dashboard));
            
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
            Widgets.Label(new Rect(10f, 0f, 130f, ROW_HEIGHT), colonist.Name.ToStringShort);

            // Workload indicator
            var map = Find.CurrentMap;
            if (map != null)
            {
                var metrics = new ColonyMetrics(map);
                WorkloadLevel workload = metrics.GetColonistWorkload(colonist);
                Color indicatorColor = GetWorkloadColor(workload);
                
                Rect indicatorRect = new Rect(145f, (ROW_HEIGHT - 12f) / 2f, 12f, 12f);
                Widgets.DrawBoxSolid(indicatorRect, indicatorColor);
                Widgets.DrawBox(indicatorRect, 1);
                
                // Tooltip
                if (Mouse.IsOver(indicatorRect))
                {
                    string tooltip = metrics.GetWorkloadTooltip(colonist);
                    TooltipHandler.TipRegion(indicatorRect, tooltip);
                }
            }

            // Best skill
            string bestSkillText = GetBestSkillText(colonist);
            Widgets.Label(new Rect(170f, 0f, 120f, ROW_HEIGHT), bestSkillText);

            // Current role
            string roleText;
            if (data.assignedRole == RolePreset.Custom && !string.IsNullOrEmpty(data.customRoleId))
            {
                var customRole = PriorityManagerMod.settings.GetCustomRole(data.customRoleId);
                roleText = customRole != null ? customRole.roleName : "Custom (Invalid)";
            }
            else
            {
                roleText = RolePresetUtility.GetRoleLabel(data.assignedRole);
            }
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

        private Color GetWorkloadColor(WorkloadLevel level)
        {
            switch (level)
            {
                case WorkloadLevel.Idle:
                    return new Color(0.5f, 0.5f, 0.5f); // Gray
                case WorkloadLevel.Light:
                    return new Color(0.4f, 1f, 0.4f); // Green
                case WorkloadLevel.Moderate:
                    return new Color(1f, 1f, 0.3f); // Yellow
                case WorkloadLevel.Heavy:
                    return new Color(1f, 0.6f, 0.2f); // Orange
                case WorkloadLevel.Overworked:
                    return new Color(1f, 0.2f, 0.2f); // Red
                default:
                    return Color.white;
            }
        }

        private void ShowRoleSelectionMenu(Pawn colonist, ColonistRoleData data)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Add built-in presets (excluding Custom)
            foreach (var preset in RolePresetUtility.GetAllPresets())
            {
                if (preset == RolePreset.Custom)
                    continue; // Skip Custom - we'll handle custom roles separately
                
                string label = RolePresetUtility.GetRoleLabel(preset);
                
                options.Add(new FloatMenuOption(label, () =>
                {
                    data.assignedRole = preset;
                    data.customRoleId = null; // Clear custom role ID when switching to preset
                    data.autoAssignEnabled = (preset != RolePreset.Manual);
                    
                    if (preset != RolePreset.Manual)
                    {
                        PriorityAssigner.AssignPriorities(colonist, true);
                        Messages.Message($"{colonist.Name.ToStringShort} assigned to {label} role.", MessageTypeDefOf.TaskCompletion);
                    }
                }));
            }

            // Add separator and custom roles
            var customRoles = PriorityManagerMod.settings.GetAllCustomRoles();
            if (customRoles.Count > 0)
            {
                options.Add(new FloatMenuOption("--- Custom Roles ---", null));
                
                foreach (var customRole in customRoles)
                {
                    options.Add(new FloatMenuOption(customRole.roleName, () =>
                    {
                        data.assignedRole = RolePreset.Custom;
                        data.customRoleId = customRole.roleId;
                        data.autoAssignEnabled = true;
                        
                        PriorityAssigner.AssignPriorities(colonist, true);
                        Messages.Message($"{colonist.Name.ToStringShort} assigned to custom role: {customRole.roleName}.", MessageTypeDefOf.TaskCompletion);
                    }));
                }
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

        private void DrawDashboardTab(Rect rect)
        {
            var map = Find.CurrentMap;
            if (map == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "No active map");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            var metrics = new ColonyMetrics(map);
            
            Widgets.BeginScrollView(rect, ref dashboardScrollPosition, new Rect(0f, 0f, rect.width - 20f, 2000f));
            
            float curY = 0f;
            float panelWidth = rect.width - 20f;
            
            // Performance Metrics Section
            curY = DrawPerformanceMetrics(metrics, curY, panelWidth);
            curY += 20f;
            
            // Job Queue Section
            curY = DrawJobQueueSection(map, curY, panelWidth);
            curY += 20f;
            
            // Staffing Overview Section
            curY = DrawStaffingOverview(metrics, curY, panelWidth);
            curY += 20f;
            
            // Skill Matrix Section
            curY = DrawSkillMatrixSection(metrics, curY, panelWidth);
            
            Widgets.EndScrollView();
        }

        private float DrawJobQueueSection(Map map, float startY, float width)
        {
            float curY = startY;
            
            // Section Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 30f), "Pending Work Queue");
            Text.Font = GameFont.Small;
            curY += 35f;
            
            Widgets.DrawLineHorizontal(0f, curY, width);
            curY += 10f;
            
            // Scan for pending jobs
            var pendingJobs = JobQueueScanner.ScanMap(map, 20);
            
            if (pendingJobs.Count == 0)
            {
                GUI.color = Color.green;
                Widgets.Label(new Rect(0f, curY, width, 25f), "No pending work - all jobs complete!");
                GUI.color = Color.white;
                curY += 30f;
                return curY;
            }
            
            Widgets.Label(new Rect(0f, curY, width, 25f), $"Pending Jobs: {pendingJobs.Count}");
            curY += 30f;
            
            // Draw job list
            foreach (var job in pendingJobs.Take(15))
            {
                // Calculate capable colonists
                job.capableColonists = JobQueueScanner.CountCapableColonists(map, job.workType);
                
                // Color based on status
                Color jobColor;
                string statusIcon;
                if (job.capableColonists == 0)
                {
                    jobColor = new Color(1f, 0.2f, 0.2f); // Red - no capable colonists
                    statusIcon = "⚠";
                }
                else if (job.capableColonists < 2)
                {
                    jobColor = new Color(1f, 0.7f, 0.2f); // Orange - only 1 colonist
                    statusIcon = "!";
                }
                else
                {
                    jobColor = new Color(0.4f, 1f, 0.4f); // Green - multiple colonists available
                    statusIcon = "✓";
                }
                
                // Job type icon
                string typeIcon = GetJobTypeIcon(job.type);
                
                GUI.color = jobColor;
                Widgets.Label(new Rect(0f, curY, 30f, 25f), statusIcon);
                GUI.color = Color.white;
                
                Widgets.Label(new Rect(35f, curY, 30f, 25f), typeIcon);
                Widgets.Label(new Rect(70f, curY, 350f, 25f), job.description);
                
                // Capable colonists count
                string capableText = job.capableColonists == 0 ? "No capable colonists!" :
                                   job.capableColonists == 1 ? "1 colonist" :
                                   $"{job.capableColonists} colonists";
                GUI.color = jobColor;
                Widgets.Label(new Rect(430f, curY, 150f, 25f), capableText);
                GUI.color = Color.white;
                
                // Work type
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(590f, curY, 150f, 25f), job.workType);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                
                curY += 25f;
            }
            
            if (pendingJobs.Count > 15)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(0f, curY, width, 20f), $"... and {pendingJobs.Count - 15} more jobs");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                curY += 25f;
            }
            
            return curY;
        }

        private string GetJobTypeIcon(JobType type)
        {
            switch (type)
            {
                case JobType.Construction:
                    return "🔨";
                case JobType.Deconstruction:
                    return "🔧";
                case JobType.Mining:
                    return "⛏";
                case JobType.Crafting:
                    return "🔥";
                case JobType.Harvesting:
                    return "🌾";
                case JobType.Sowing:
                    return "🌱";
                case JobType.PlantCutting:
                    return "✂";
                case JobType.Hauling:
                    return "📦";
                case JobType.Repair:
                    return "🔧";
                case JobType.Research:
                    return "📚";
                default:
                    return "•";
            }
        }

        private float DrawPerformanceMetrics(ColonyMetrics metrics, float startY, float width)
        {
            float curY = startY;
            
            // Section Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 30f), "Performance Metrics");
            Text.Font = GameFont.Small;
            curY += 35f;
            
            Widgets.DrawLineHorizontal(0f, curY, width);
            curY += 10f;
            
            // Efficiency Score
            float efficiencyScore = metrics.GetColonyEfficiencyScore();
            Color scoreColor = efficiencyScore >= 80f ? Color.green : 
                             efficiencyScore >= 60f ? Color.yellow : 
                             new Color(1f, 0.5f, 0f);
            
            GUI.color = scoreColor;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 35f), $"Colony Efficiency: {efficiencyScore:F0}%");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 40f;
            
            // Idle Time
            float avgIdle = metrics.GetAverageIdleTime(TimeWindow.Day);
            Widgets.Label(new Rect(0f, curY, 200f, 25f), "Average Idle Time (24h):");
            Color idleColor = avgIdle < 10f ? Color.green : avgIdle < 25f ? Color.yellow : new Color(1f, 0.5f, 0f);
            GUI.color = idleColor;
            Widgets.Label(new Rect(210f, curY, 100f, 25f), $"{avgIdle:F1}%");
            GUI.color = Color.white;
            curY += 30f;
            
            // Job Bottlenecks
            var bottlenecks = metrics.GetJobBottlenecks();
            Widgets.Label(new Rect(0f, curY, width, 25f), $"Job Bottlenecks: {bottlenecks.Count}");
            curY += 30f;
            
            if (bottlenecks.Count > 0)
            {
                foreach (var bottleneck in bottlenecks.Take(5))
                {
                    Color bottleneckColor = bottleneck.severity == BottleneckSeverity.BelowMinimum ? new Color(1f, 0.3f, 0.3f) :
                                           bottleneck.severity == BottleneckSeverity.AboveMaximum ? new Color(1f, 0.7f, 0.3f) :
                                           Color.yellow;
                    
                    GUI.color = bottleneckColor;
                    string icon = bottleneck.severity == BottleneckSeverity.BelowMinimum ? "⚠" :
                                 bottleneck.severity == BottleneckSeverity.AboveMaximum ? "⚠" : "!";
                    
                    Widgets.Label(new Rect(20f, curY, 30f, 25f), icon);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(50f, curY, 200f, 25f), bottleneck.workType.labelShort);
                    Widgets.Label(new Rect(260f, curY, width - 260f, 25f), bottleneck.description);
                    curY += 25f;
                }
                
                if (bottlenecks.Count > 5)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(new Rect(20f, curY, width - 20f, 20f), $"... and {bottlenecks.Count - 5} more");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    curY += 25f;
                }
            }
            else
            {
                GUI.color = Color.green;
                Widgets.Label(new Rect(20f, curY, width - 20f, 25f), "No bottlenecks detected - good job!");
                GUI.color = Color.white;
                curY += 30f;
            }
            
            return curY;
        }

        private float DrawStaffingOverview(ColonyMetrics metrics, float startY, float width)
        {
            float curY = startY;
            
            // Section Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width, 30f), "Staffing Overview");
            Text.Font = GameFont.Small;
            curY += 35f;
            
            Widgets.DrawLineHorizontal(0f, curY, width);
            curY += 10f;
            
            // Role Distribution
            var roleDistribution = metrics.GetRoleDistribution();
            Widgets.Label(new Rect(0f, curY, width, 25f), "Role Distribution:");
            curY += 30f;
            
            foreach (var kvp in roleDistribution.OrderByDescending(x => x.Value))
            {
                string roleName = kvp.Key.ToString();
                if (kvp.Key == RolePreset.Custom)
                    roleName = "Custom Roles";
                else if (kvp.Key == RolePreset.Auto)
                    roleName = "Auto-Assigned";
                
                Widgets.Label(new Rect(20f, curY, 200f, 25f), roleName + ":");
                Widgets.Label(new Rect(230f, curY, 100f, 25f), kvp.Value.ToString() + " colonists");
                curY += 25f;
            }
            
            curY += 10f;
            
            // Min/Max Status
            var staffingStatus = metrics.GetStaffingStatus();
            var problemJobs = staffingStatus.Where(kvp => kvp.Value.level != StaffingLevel.Optimal).ToList();
            
            Widgets.Label(new Rect(0f, curY, width, 25f), $"Staffing Status: {problemJobs.Count} jobs need attention");
            curY += 30f;
            
            if (problemJobs.Count > 0)
            {
                foreach (var kvp in problemJobs.Take(10))
                {
                    var status = kvp.Value;
                    Color statusColor = status.level == StaffingLevel.Understaffed ? new Color(1f, 0.3f, 0.3f) :
                                       new Color(1f, 0.7f, 0.3f);
                    string statusText = status.level == StaffingLevel.Understaffed ? "UNDERSTAFFED" : "OVERSTAFFED";
                    
                    GUI.color = statusColor;
                    Widgets.Label(new Rect(20f, curY, 120f, 25f), statusText);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(150f, curY, 200f, 25f), status.workType.labelShort);
                    
                    string targetText = "";
                    if (status.minWorkers > 0 && status.maxWorkers > 0)
                        targetText = $"{status.currentWorkers} / {status.minWorkers}-{status.maxWorkers}";
                    else if (status.minWorkers > 0)
                        targetText = $"{status.currentWorkers} / {status.minWorkers}+";
                    else if (status.maxWorkers > 0)
                        targetText = $"{status.currentWorkers} / max {status.maxWorkers}";
                    
                    Widgets.Label(new Rect(360f, curY, 200f, 25f), targetText);
                    curY += 25f;
                }
            }
            else
            {
                GUI.color = Color.green;
                Widgets.Label(new Rect(20f, curY, width - 20f, 25f), "All jobs adequately staffed!");
                GUI.color = Color.white;
                curY += 30f;
            }
            
            curY += 10f;
            
            // Skill Coverage Gaps
            var skillGaps = metrics.GetSkillCoverageGaps();
            Widgets.Label(new Rect(0f, curY, width, 25f), $"Skill Coverage Gaps: {skillGaps.Count}");
            curY += 30f;
            
            if (skillGaps.Count > 0)
            {
                foreach (var gap in skillGaps.Take(10))
                {
                    Color gapColor = gap.severity == GapSeverity.NoSkill ? new Color(1f, 0.3f, 0.3f) :
                                    gap.severity == GapSeverity.LowSkill ? new Color(1f, 0.7f, 0.3f) :
                                    Color.yellow;
                    
                    string severityText = gap.severity == GapSeverity.NoSkill ? "NO SKILL" :
                                         gap.severity == GapSeverity.LowSkill ? "LOW SKILL" :
                                         "SINGLE POINT";
                    
                    GUI.color = gapColor;
                    Widgets.Label(new Rect(20f, curY, 120f, 25f), severityText);
                    GUI.color = Color.white;
                    Widgets.Label(new Rect(150f, curY, 200f, 25f), gap.workType.labelShort);
                    Widgets.Label(new Rect(360f, curY, 200f, 25f), $"{gap.skilledColonists} skilled (avg: {gap.averageSkillLevel:F1})");
                    curY += 25f;
                }
                
                if (skillGaps.Count > 10)
                {
                    Text.Font = GameFont.Tiny;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    Widgets.Label(new Rect(20f, curY, width - 20f, 20f), $"... and {skillGaps.Count - 10} more");
                    GUI.color = Color.white;
                    Text.Font = GameFont.Small;
                    curY += 25f;
                }
            }
            else
            {
                GUI.color = Color.green;
                Widgets.Label(new Rect(20f, curY, width - 20f, 25f), "No critical skill gaps detected!");
                GUI.color = Color.white;
                curY += 30f;
            }
            
            return curY;
        }

        private float DrawSkillMatrixSection(ColonyMetrics metrics, float startY, float width)
        {
            float curY = startY;
            
            // Section Header with expand/collapse button
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, curY, width - 100f, 30f), "Skill Coverage Matrix");
            Text.Font = GameFont.Small;
            
            // Expand/collapse button
            Rect expandButtonRect = new Rect(width - 100f, curY, 100f, 28f);
            if (Widgets.ButtonText(expandButtonRect, showSkillMatrix ? "Hide Matrix" : "Show Matrix"))
            {
                showSkillMatrix = !showSkillMatrix;
            }
            
            curY += 35f;
            Widgets.DrawLineHorizontal(0f, curY, width);
            curY += 10f;
            
            if (!showSkillMatrix)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f);
                Widgets.Label(new Rect(0f, curY, width, 20f), "Click 'Show Matrix' to view detailed skill coverage for all colonists and work types");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                curY += 25f;
                return curY;
            }
            
            // Filter toggle
            Rect filterRect = new Rect(0f, curY, width, 25f);
            Widgets.CheckboxLabeled(filterRect, "Show only skill gaps", ref showOnlyGaps);
            curY += 30f;
            
            // Get matrix data
            var matrixData = metrics.GetSkillMatrix();
            if (matrixData.workTypes.Count == 0 || matrixData.colonists.Count == 0)
            {
                Widgets.Label(new Rect(0f, curY, width, 25f), "No data available");
                curY += 30f;
                return curY;
            }
            
            // Draw matrix
            const float cellSize = 30f;
            const float rowLabelWidth = 150f;
            const float colLabelHeight = 80f;
            
            float matrixWidth = rowLabelWidth + matrixData.colonists.Count * cellSize;
            float matrixHeight = colLabelHeight + matrixData.workTypes.Count * cellSize;
            
            Rect matrixViewRect = new Rect(0f, curY, width, Mathf.Min(400f, matrixHeight + 20f));
            Rect matrixContentRect = new Rect(0f, 0f, matrixWidth, matrixHeight);
            
            Widgets.BeginScrollView(matrixViewRect, ref skillMatrixScrollPosition, matrixContentRect);
            
            // Draw column headers (colonist names, rotated)
            for (int i = 0; i < matrixData.colonists.Count; i++)
            {
                var colonist = matrixData.colonists[i];
                Rect headerRect = new Rect(rowLabelWidth + i * cellSize, 0f, cellSize, colLabelHeight);
                
                // Draw colonist name vertically
                GUI.BeginGroup(headerRect);
                Text.Anchor = TextAnchor.LowerCenter;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(0f, 0f, cellSize, colLabelHeight - 5f), colonist.Name.ToStringShort);
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.EndGroup();
            }
            
            // Draw rows
            int rowIndex = 0;
            foreach (var workType in matrixData.workTypes)
            {
                if (showOnlyGaps)
                {
                    // Check if this row has any gaps
                    bool hasGap = false;
                    foreach (var colonist in matrixData.colonists)
                    {
                        var cell = matrixData.cells[workType][colonist];
                        if (cell.canDo && cell.skillLevel < 6)
                        {
                            hasGap = true;
                            break;
                        }
                    }
                    if (!hasGap)
                        continue;
                }
                
                float rowY = colLabelHeight + rowIndex * cellSize;
                
                // Row label
                Rect labelRect = new Rect(0f, rowY, rowLabelWidth, cellSize);
                Widgets.DrawBoxSolid(labelRect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(5f, rowY, rowLabelWidth - 10f, cellSize), workType.labelShort);
                Text.Anchor = TextAnchor.UpperLeft;
                
                // Draw cells
                for (int i = 0; i < matrixData.colonists.Count; i++)
                {
                    var colonist = matrixData.colonists[i];
                    var cell = matrixData.cells[workType][colonist];
                    
                    Rect cellRect = new Rect(rowLabelWidth + i * cellSize, rowY, cellSize, cellSize);
                    
                    // Cell color based on skill level
                    Color cellColor;
                    if (!cell.canDo)
                    {
                        cellColor = new Color(0.3f, 0.1f, 0.1f); // Dark red - can't do
                    }
                    else if (cell.skillLevel == 0)
                    {
                        cellColor = new Color(0.7f, 0.2f, 0.2f); // Red - no skill
                    }
                    else if (cell.skillLevel <= 5)
                    {
                        cellColor = new Color(0.9f, 0.5f, 0.2f); // Orange - low skill
                    }
                    else if (cell.skillLevel <= 10)
                    {
                        cellColor = new Color(0.9f, 0.9f, 0.3f); // Yellow - medium skill
                    }
                    else if (cell.skillLevel <= 15)
                    {
                        cellColor = new Color(0.5f, 0.9f, 0.5f); // Light green - good skill
                    }
                    else
                    {
                        cellColor = new Color(0.2f, 0.7f, 0.2f); // Dark green - excellent skill
                    }
                    
                    // Darken if not assigned
                    if (cell.canDo && !cell.isAssigned)
                    {
                        cellColor = cellColor * 0.6f;
                    }
                    
                    Widgets.DrawBoxSolid(cellRect, cellColor);
                    Widgets.DrawBox(cellRect, 1);
                    
                    // Draw skill level number
                    if (cell.canDo)
                    {
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Text.Font = GameFont.Tiny;
                        GUI.color = Color.white;
                        Widgets.Label(cellRect, cell.skillLevel.ToString());
                        
                        // Draw passion indicator
                        if (cell.passion == Passion.Major)
                        {
                            Widgets.Label(new Rect(cellRect.x, cellRect.y, cellRect.width, cellRect.height - 10f), "🔥");
                        }
                        else if (cell.passion == Passion.Minor)
                        {
                            Widgets.Label(new Rect(cellRect.x, cellRect.y, cellRect.width, cellRect.height - 10f), "·");
                        }
                        
                        GUI.color = Color.white;
                        Text.Font = GameFont.Small;
                        Text.Anchor = TextAnchor.UpperLeft;
                        
                        // Tooltip
                        if (Mouse.IsOver(cellRect))
                        {
                            string passionText = cell.passion == Passion.Major ? " (Burning Passion)" :
                                               cell.passion == Passion.Minor ? " (Interested)" : "";
                            string assignedText = cell.isAssigned ? " [ASSIGNED]" : "";
                            string tooltip = $"{colonist.Name.ToStringShort}\n{workType.labelShort}\nSkill: {cell.skillLevel}{passionText}{assignedText}";
                            TooltipHandler.TipRegion(cellRect, tooltip);
                        }
                    }
                    else
                    {
                        // Can't do this work
                        Text.Anchor = TextAnchor.MiddleCenter;
                        Text.Font = GameFont.Small;
                        GUI.color = new Color(0.5f, 0.5f, 0.5f);
                        Widgets.Label(cellRect, "X");
                        GUI.color = Color.white;
                        Text.Anchor = TextAnchor.UpperLeft;
                        
                        if (Mouse.IsOver(cellRect))
                        {
                            TooltipHandler.TipRegion(cellRect, $"{colonist.Name.ToStringShort}\nCannot do {workType.labelShort}");
                        }
                    }
                }
                
                rowIndex++;
            }
            
            Widgets.EndScrollView();
            curY += matrixViewRect.height + 10f;
            
            // Legend
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            Widgets.Label(new Rect(0f, curY, width, 60f), 
                "Legend: Dark Red = Can't do | Red = No skill (0) | Orange = Low (1-5) | Yellow = Medium (6-10) | Light Green = Good (11-15) | Dark Green = Excellent (16-20)\n" +
                "Dimmed colors = Not currently assigned | 🔥 = Passion | Numbers show average skill level for work type");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            curY += 65f;
            
            return curY;
        }

        public override void PreClose()
        {
            base.PreClose();
            // Save settings
            PriorityManagerMod.Instance.WriteSettings();
        }
    }
}

