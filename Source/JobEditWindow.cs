using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    public class JobEditWindow : Window
    {
        private WorkTypeDef workType;
        private PriorityManagerSettings settings;
        private JobImportance tempImportance;
        private int tempMaxWorkers;
        private int tempMinWorkers;
        private bool tempUsePercentage;

        public override Vector2 InitialSize => new Vector2(650f, 550f);

        public JobEditWindow(WorkTypeDef workType)
        {
            this.workType = workType;
            this.settings = PriorityManagerMod.settings;
            
            doCloseButton = true;
            doCloseX = true;
            draggable = true;
            resizeable = false;
            absorbInputAroundWindow = true;

            // Load current settings (raw values, not calculated)
            tempImportance = settings.GetJobImportance(workType);
            tempMaxWorkers = settings.GetRawMaxWorkers(workType);
            tempMinWorkers = settings.GetRawMinWorkers(workType);
            tempUsePercentage = settings.IsUsingPercentage(workType);
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;
            GUI.color = Color.white;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // Title
            Text.Font = GameFont.Medium;
            listing.Label($"Edit Job: {workType.labelShort ?? workType.defName}");
            Text.Font = GameFont.Small;
            listing.Gap();

            // Description
            if (!string.IsNullOrEmpty(workType.description))
            {
                listing.Label(workType.description);
                listing.Gap();
            }

            // Relevant Skills
            if (workType.relevantSkills != null && workType.relevantSkills.Count > 0)
            {
                string skills = "Relevant Skills: " + string.Join(", ", workType.relevantSkills.Select(s => s.skillLabel));
                listing.Label(skills);
                listing.Gap();
            }

            Widgets.DrawLineHorizontal(0f, listing.CurHeight, inRect.width);
            listing.Gap();

            // Importance Setting
            listing.Label("Job Importance:");
            Rect importanceRect = listing.GetRect(30f);
            Rect importanceLabelRect = new Rect(importanceRect.x, importanceRect.y, 150f, 30f);
            Rect importanceButtonRect = new Rect(importanceRect.x + 160f, importanceRect.y, 200f, 30f);

            Widgets.Label(importanceLabelRect, "Priority Level:");
            string importanceLabel = GetImportanceLabel(tempImportance);
            
            if (Widgets.ButtonText(importanceButtonRect, importanceLabel))
            {
                ShowImportanceMenu();
            }

            listing.Gap();

            // Worker Count Settings
            listing.Label("Worker Assignment:");
            listing.Gap();

            // Percentage toggle
            Rect toggleRect = listing.GetRect(30f);
            Widgets.CheckboxLabeled(toggleRect, "Use percentage instead of absolute numbers", ref tempUsePercentage);
            listing.Gap();

            string unit = tempUsePercentage ? "%" : " colonists";
            float maxValue = tempUsePercentage ? 100f : 20f;

            // Minimum Workers
            Rect minWorkersRect = listing.GetRect(30f);
            Rect minLabelRect = new Rect(minWorkersRect.x, minWorkersRect.y, 200f, 30f);
            Rect minSliderRect = new Rect(minWorkersRect.x + 210f, minWorkersRect.y, 250f, 30f);
            Rect minValueRect = new Rect(minWorkersRect.x + 470f, minWorkersRect.y, 100f, 30f);

            Widgets.Label(minLabelRect, "Minimum Workers:");
            tempMinWorkers = (int)Widgets.HorizontalSlider(minSliderRect, tempMinWorkers, 0f, maxValue);
            Widgets.Label(minValueRect, tempMinWorkers.ToString() + unit);

            listing.Gap();

            // Maximum Workers
            Rect maxWorkersRect = listing.GetRect(30f);
            Rect maxLabelRect = new Rect(maxWorkersRect.x, maxWorkersRect.y, 200f, 30f);
            Rect maxSliderRect = new Rect(maxWorkersRect.x + 210f, maxWorkersRect.y, 250f, 30f);
            Rect maxValueRect = new Rect(maxWorkersRect.x + 470f, maxWorkersRect.y, 100f, 30f);

            Widgets.Label(maxLabelRect, "Maximum Workers:");
            tempMaxWorkers = (int)Widgets.HorizontalSlider(maxSliderRect, tempMaxWorkers, 0f, maxValue);
            
            string maxText = tempMaxWorkers == 0 ? "Unlimited" : tempMaxWorkers.ToString() + unit;
            Widgets.Label(maxValueRect, maxText);

            listing.Gap();
            listing.Gap();

            // Help text
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            if (tempUsePercentage)
            {
                listing.Label("Minimum: Ensure at least this % of colonists work this job.");
                listing.Label("Maximum: Limit to at most this % of colonists. 0 = unlimited.");
            }
            else
            {
                listing.Label("Minimum: Ensure at least this many colonists work this job.");
                listing.Label("Maximum: Limit to at most this many colonists. 0 = unlimited.");
            }
            listing.Label("Note: Limits count ALL colonists (including manually-controlled ones).");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            listing.Gap();
            Widgets.DrawLineHorizontal(0f, listing.CurHeight, inRect.width);
            listing.Gap();

            // Current Assignment Info with Target Preview
            Text.Font = GameFont.Small;
            listing.Label("Current Status:");
            
            var gameComp = PriorityDataHelper.GetGameComponent();
            if (gameComp != null)
            {
                var colonists = gameComp.GetAllColonists();
                int totalColonists = colonists.Count;
                int currentWorkers = 0;
                foreach (var colonist in colonists)
                {
                    if (colonist.workSettings != null && colonist.workSettings.GetPriority(workType) > 0)
                        currentWorkers++;
                }
                
                // Calculate target min/max based on current settings
                int targetMin = tempMinWorkers;
                int targetMax = tempMaxWorkers;
                
                if (tempUsePercentage && totalColonists > 0)
                {
                    targetMin = (int)System.Math.Ceiling(totalColonists * (tempMinWorkers / 100f));
                    targetMax = tempMaxWorkers > 0 ? (int)System.Math.Ceiling(totalColonists * (tempMaxWorkers / 100f)) : 0;
                }
                
                // Show current vs target
                Rect statusRect = listing.GetRect(24f);
                
                // Determine status color
                Color statusColor = Color.white;
                string statusText = "";
                
                if (targetMin > 0 && currentWorkers < targetMin)
                {
                    statusColor = new Color(1f, 0.4f, 0.4f); // Red - below minimum
                    statusText = " [BELOW MIN]";
                }
                else if (targetMax > 0 && currentWorkers > targetMax)
                {
                    statusColor = new Color(1f, 0.7f, 0.3f); // Orange - above maximum
                    statusText = " [ABOVE MAX]";
                }
                else if (targetMin > 0 && currentWorkers >= targetMin)
                {
                    statusColor = new Color(0.4f, 1f, 0.4f); // Green - in range
                    statusText = " [OK]";
                }
                
                GUI.color = statusColor;
                string minStr = targetMin > 0 ? targetMin.ToString() : "-";
                string maxStr = targetMax > 0 ? targetMax.ToString() : "∞";
                Widgets.Label(statusRect, $"Currently assigned: {currentWorkers} colonist(s){statusText}");
                GUI.color = Color.white;
                
                Rect targetRect = listing.GetRect(20f);
                string targetDisplay = $"Target range: {minStr} to {maxStr} colonists";
                if (tempUsePercentage)
                {
                    targetDisplay = $"Target range: {tempMinWorkers}% to {(tempMaxWorkers > 0 ? tempMaxWorkers.ToString() + "%" : "∞")} ({minStr} to {maxStr} colonists)";
                }
                Widgets.Label(targetRect, targetDisplay);
            }

            listing.Gap();
            listing.Gap();

            // Action Buttons
            Rect buttonRect = listing.GetRect(35f);
            Rect saveButtonRect = new Rect(buttonRect.x, buttonRect.y, 120f, 30f);
            Rect cancelButtonRect = new Rect(buttonRect.x + 130f, buttonRect.y, 120f, 30f);
            Rect resetButtonRect = new Rect(buttonRect.x + 260f, buttonRect.y, 120f, 30f);

            if (Widgets.ButtonText(saveButtonRect, "Save"))
            {
                // Validate settings
                bool hasError = false;
                
                // Check if min > max
                if (tempMaxWorkers > 0 && tempMinWorkers > tempMaxWorkers)
                {
                    Messages.Message($"Error: Minimum workers ({tempMinWorkers}) cannot be greater than maximum workers ({tempMaxWorkers})", MessageTypeDefOf.RejectInput);
                    hasError = true;
                }
                
                // Check if settings are impossible given colony size
                var gameCompValidation = PriorityDataHelper.GetGameComponent();
                if (gameCompValidation != null && !tempUsePercentage)
                {
                    int totalColonists = gameCompValidation.GetAllColonists().Count;
                    if (tempMinWorkers > totalColonists)
                    {
                        Messages.Message($"Warning: Minimum workers ({tempMinWorkers}) exceeds total colonists ({totalColonists})", MessageTypeDefOf.CautionInput);
                    }
                }
                
                // Check percentage range
                if (tempUsePercentage)
                {
                    if (tempMinWorkers > 100)
                    {
                        Messages.Message($"Error: Minimum percentage cannot exceed 100%", MessageTypeDefOf.RejectInput);
                        hasError = true;
                    }
                    if (tempMaxWorkers > 100)
                    {
                        Messages.Message($"Error: Maximum percentage cannot exceed 100%", MessageTypeDefOf.RejectInput);
                        hasError = true;
                    }
                }
                
                if (!hasError)
                {
                    settings.SetJobImportance(workType, tempImportance);
                    settings.SetWorkerCounts(workType, tempMinWorkers, tempMaxWorkers, tempUsePercentage);
                    PriorityManagerMod.Instance.WriteSettings();
                    Messages.Message($"Settings saved for {workType.labelShort}", MessageTypeDefOf.TaskCompletion);
                    Close();
                }
            }

            if (Widgets.ButtonText(cancelButtonRect, "Cancel"))
            {
                Close();
            }

            if (Widgets.ButtonText(resetButtonRect, "Reset to Default"))
            {
                tempImportance = JobImportance.Normal;
                tempMinWorkers = 0;
                tempMaxWorkers = 0;
                tempUsePercentage = false;
            }

            listing.End();

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void ShowImportanceMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            foreach (JobImportance importance in System.Enum.GetValues(typeof(JobImportance)))
            {
                string label = GetImportanceLabel(importance);
                string description = GetImportanceDescription(importance);
                
                options.Add(new FloatMenuOption($"{label} - {description}", () =>
                {
                    tempImportance = importance;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
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
    }
}

