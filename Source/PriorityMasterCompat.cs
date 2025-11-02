using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PriorityManager
{
    /// <summary>
    /// Compatibility layer for PriorityMaster mod integration
    /// Handles detection, priority scaling, and settings synchronization
    /// </summary>
    public static class PriorityMasterCompat
    {
        private static bool? _isLoaded;
        private static int? _maxPriority;
        private static object _settingsInstance;
        
        /// <summary>
        /// Check if PriorityMaster mod is loaded
        /// </summary>
        public static bool IsLoaded()
        {
            if (!_isLoaded.HasValue)
            {
                _isLoaded = ModsConfig.IsActive("Lauriichan.PriorityMaster") ||
                           LoadedModManager.RunningMods.Any(m => m.PackageId.ToLower() == "lauriichan.prioritymaster" || 
                                                                  m.PackageId.ToLower() == "prioritymaster");
            }
            return _isLoaded.Value;
        }
        
        /// <summary>
        /// Get PriorityMaster's maximum priority setting
        /// Returns 4 if PriorityMaster is not loaded (vanilla)
        /// </summary>
        public static int GetMaxPriority()
        {
            if (!IsLoaded()) 
                return 4;
            
            // Use reflection to read PriorityMaster's max priority setting
            // Cache the result for performance
            if (!_maxPriority.HasValue)
            {
                _maxPriority = ReadPriorityMasterMaxSetting();
            }
            return _maxPriority.Value;
        }
        
        /// <summary>
        /// Scale vanilla priority (1-4) to PriorityMaster's extended range
        /// Uses dynamic scaling based on PriorityMaster's max setting
        /// </summary>
        public static int ScalePriority(int vanillaPriority)
        {
            if (!IsLoaded()) 
                return vanillaPriority;
            
            // Check if user wants integration disabled
            if (PriorityManagerMod.settings != null && !PriorityManagerMod.settings.enablePriorityMasterIntegration)
                return vanillaPriority;
            
            // Check for custom mapping first
            if (PriorityManagerMod.settings != null && PriorityManagerMod.settings.useCustomMapping)
            {
                if (PriorityManagerMod.settings.customPriorityMapping.TryGetValue(vanillaPriority, out int customValue))
                {
                    return customValue;
                }
            }
            
            int maxPriority = GetMaxPriority();
            
            // Dynamic scaling: map 1-4 to percentage of max
            // Priority 1 = 10%, 2 = 30%, 3 = 60%, 4 = 90%
            switch (vanillaPriority)
            {
                case 1: 
                    return Mathf.Max(1, Mathf.RoundToInt(maxPriority * 0.1f));  // 10%
                case 2: 
                    return Mathf.RoundToInt(maxPriority * 0.3f);  // 30%
                case 3: 
                    return Mathf.RoundToInt(maxPriority * 0.6f);  // 60%
                case 4: 
                    return Mathf.RoundToInt(maxPriority * 0.9f);  // 90%
                default: 
                    return vanillaPriority;
            }
        }
        
        /// <summary>
        /// Invalidate cached values (call when PriorityMaster settings change)
        /// </summary>
        public static void InvalidateCache()
        {
            _maxPriority = null;
            _settingsInstance = null;
        }
        
        /// <summary>
        /// Read PriorityMaster's max priority setting using reflection
        /// Cached for performance
        /// </summary>
        private static int ReadPriorityMasterMaxSetting()
        {
            try
            {
                // Cache reflection lookups for performance
                if (_settingsInstance == null)
                {
                    // Try multiple possible type names for compatibility
                    var modType = AccessTools.TypeByName("PriorityMaster.Mod") ??
                                 AccessTools.TypeByName("Lauriichan.PriorityMaster.Mod");
                    
                    if (modType != null)
                    {
                        var settingsField = AccessTools.Field(modType, "Settings");
                        if (settingsField == null)
                            settingsField = AccessTools.Field(modType, "settings");
                        
                        if (settingsField != null)
                        {
                            _settingsInstance = settingsField.GetValue(null);
                        }
                        else
                        {
                            var settingsProp = AccessTools.Property(modType, "Settings");
                            _settingsInstance = settingsProp?.GetValue(null);
                        }
                    }
                }
                
                if (_settingsInstance != null)
                {
                    // Try to find the max priority field/property
                    var maxPriorityField = AccessTools.Field(_settingsInstance.GetType(), "MaxPriority");
                    if (maxPriorityField == null)
                        maxPriorityField = AccessTools.Field(_settingsInstance.GetType(), "maxPriority");
                    
                    if (maxPriorityField != null)
                    {
                        var value = maxPriorityField.GetValue(_settingsInstance);
                        if (value != null)
                        {
                            return (int)value;
                        }
                    }
                    else
                    {
                        var maxPriorityProp = AccessTools.Property(_settingsInstance.GetType(), "MaxPriority");
                        if (maxPriorityProp != null)
                        {
                            var value = maxPriorityProp.GetValue(_settingsInstance);
                            if (value != null)
                            {
                                return (int)value;
                            }
                        }
                    }
                }
                
                Log.Message("PriorityManager: Could not read PriorityMaster max priority, using default (9)");
            }
            catch (Exception ex)
            {
                Log.Warning($"PriorityManager: Failed to read PriorityMaster settings: {ex.Message}");
            }
            
            return 9; // PriorityMaster default max priority
        }
        
        /// <summary>
        /// Get a display string showing the current priority mapping
        /// </summary>
        public static string GetMappingDescription()
        {
            if (!IsLoaded())
                return "PriorityMaster not detected";
            
            int max = GetMaxPriority();
            return $"Priority mapping (max: {max}): 1→{ScalePriority(1)}, 2→{ScalePriority(2)}, 3→{ScalePriority(3)}, 4→{ScalePriority(4)}";
        }
    }
}

