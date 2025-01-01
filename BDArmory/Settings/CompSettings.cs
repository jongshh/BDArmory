using System.Linq;
using System.Reflection;
using UnityEngine;

using System.IO;
using System.Collections.Generic;
using System;
using BDArmory.UI;

namespace BDArmory.Settings
{
	public class CompSettings
	{
		// Settings overrides for AI/WM settings for competition rules compliance

		static readonly string CompSettingsPath = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "GameData/BDArmory/PluginData/Comp_settings.cfg"));
		static public bool CompOverridesEnabled = false;
		public static readonly Dictionary<string, float> CompOverrides = new()
		{
				// FIXME there's probably a few more things that could get set here for AI/WM override if needed in specific rounds.
				//AI Min/max Alt?
                //AI postStallAoA?
                //AI allowRamming?
                //WM gunRange?
                //WM multiMissileTgtNum
				{"extensionCutoffTime", -1},
				{"extendDistanceAirToAir", -1},
				{"MONOCOCKPIT_VIEWRANGE", -1},
				{"DUALCOCKPIT_VIEWRANGE", -1},
				{"guardAngle", -1},
				{"collisionAvoidanceThreshold", -1},
				{"vesselCollisionAvoidanceLookAheadPeriod", -1},
				{"vesselCollisionAvoidanceStrength", -1 },
				{"idleSpeed", -1},
				{"DISABLE_SAS", 0}, //0/1 for F/T
		};

        /// <summary>
        /// Load P:S AI/Wm override settings from file.
        /// </summary>
        public static void Load()
        {
            if (!File.Exists(CompSettingsPath))
            {
                if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.CompSettings]: Override settings not present, skipping.");
                CompOverridesEnabled = false;
                return;
            }
            ConfigNode fileNode = ConfigNode.Load(CompSettingsPath);
            if (!fileNode.HasNode("AIWMChecks")) return;
            CompOverridesEnabled = true;
            ConfigNode settings = fileNode.GetNode("AIWMChecks");

            foreach (ConfigNode.Value fieldNode in settings.values)
            {
                if (float.TryParse(fieldNode.value, out float fieldValue))
                    CompOverrides[fieldNode.name] = fieldValue; // Add or set the override.
            }
            if (BDArmorySettings.DEBUG_OTHER)
            {
                Debug.Log($"[BDArmory.CompSettings]: Comp AI/WM overrides loaded");
                foreach (KeyValuePair<string, float> entry in CompOverrides)
                {
                    Debug.Log($"[BDArmory.CompSettings]: {entry.Key}, value {entry.Value} added");
                }
            }
        }
    }
}