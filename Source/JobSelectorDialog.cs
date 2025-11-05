using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    /// <summary>
    /// Dialog for selecting a job to add to a custom role
    /// </summary>
    public class JobSelectorDialog : Window
    {
        private CustomRole role;
        private Action onJobAdded;
        private Vector2 scrollPosition = Vector2.zero;
        private string searchFilter = "";

        public override Vector2 InitialSize => new Vector2(500f, 600f);

        public JobSelectorDialog(CustomRole role, Action onJobAdded)
        {
            this.role = role;
            this.onJobAdded = onJobAdded;
            this.doCloseButton = true;
            this.doCloseX = true;
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 35f), "Select Job to Add");
            Text.Font = GameFont.Small;

            // Search box
            Rect searchRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, 30f);
            searchFilter = Widgets.TextField(searchRect, searchFilter);

            // Job list
            Rect listRect = new Rect(inRect.x, inRect.y + 75f, inRect.width, inRect.height - 120f);
            DrawJobList(listRect);
        }

        private void DrawJobList(Rect inRect)
        {
            // Get all available work types
            var allWorkTypes = DefDatabase<WorkTypeDef>.AllDefsListForReading
                .Where(wt => wt.visible)
                .Where(wt => !role.HasJob(wt))
                .Where(wt => string.IsNullOrEmpty(searchFilter) || 
                            wt.labelShort.ToLower().Contains(searchFilter.ToLower()) ||
                            wt.label.ToLower().Contains(searchFilter.ToLower()))
                .OrderBy(wt => wt.labelShort)
                .ToList();

            if (allWorkTypes.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(inRect, "No available jobs found");
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float rowHeight = 35f;
            Rect contentRect = new Rect(0f, 0f, inRect.width - 20f, allWorkTypes.Count * rowHeight);

            Widgets.BeginScrollView(inRect, ref scrollPosition, contentRect);

            float curY = 0f;
            foreach (var workType in allWorkTypes)
            {
                Rect rowRect = new Rect(0f, curY, contentRect.width, rowHeight - 2f);

                if (Widgets.ButtonText(rowRect, workType.labelShort, true, true, true))
                {
                    role.AddJob(workType, JobImportance.Normal);
                    onJobAdded?.Invoke();
                    Close();
                }

                Widgets.DrawLineHorizontal(rowRect.x, curY + rowHeight - 1f, rowRect.width);
                curY += rowHeight;
            }

            Widgets.EndScrollView();
        }
    }
}

