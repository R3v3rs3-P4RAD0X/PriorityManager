using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    public class PriorityManagerMod : Mod
    {
        public static Harmony harmony;
        public static PriorityManagerSettings settings;
        public static PriorityManagerMod Instance;

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
                
                Log.Message("Priority Manager loaded successfully. Press 'N' or open the Work tab to access Priority Manager settings.");
            }
            catch (System.Exception ex)
            {
                Log.Error($"PriorityManager: FAILED TO LOAD! Exception: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("Priority Manager Settings");
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

            listing.Gap();
            listing.Label("For more detailed colonist-specific settings, open the Priority Manager window from the Work tab or press 'N'.");

            listing.End();
        }

        public override string SettingsCategory()
        {
            return "Priority Manager";
        }
    }
}

