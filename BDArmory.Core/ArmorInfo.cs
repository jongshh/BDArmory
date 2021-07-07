using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BDArmory.Core
{
    public class ArmorInfo
    {
        public string name { get; private set; }
        public float Density { get; private set; } //mass kg/m3 lighter is better. Or is it?
        public float Strength { get; private set; } //in MPa, yieldstrength for material, controls fail point for material when projectile can penetrate. Higher is better
        public float Hardness { get; private set; } //hardness, in MPa, of material. Controls how much deformation impacting projectiles experience
        public float Ductility { get; private set; } //measure of ductility, 0 is hardened ceramic, 100 is rubber. Mild steel is about 15. ideally should be around 15-25. 
                                                     //Too low, and armor is brittle. Too High, and armor cannot effectively stop projectiles in reasonable distance
        public float Diffusivity { get; private set; } //ability to disperse electrical/thermal energy when material is subject to laser/EMP attack. Higher is better
        public float SafeUseTemp { get; private set; } //In Kelvin, determines max temp armor retains full mechanical properties
        public float Cost { get; private set; }

        //public bool Reactive {get; private set; } have a reactive armor bool?

        public static ArmorInfos armors;
        public static List<string> armorNames;
        public static ArmorInfo defaultArmor;

        public ArmorInfo(string name, float Density, float Strength, float Hardness, float Ductility, float Diffusivity, float SafeUseTemp, float Cost)
        {
            this.name = name;
            this.Density = Density;
            this.Strength = Strength;
            this.Hardness = Hardness;
            this.Ductility = Ductility;
            this.Diffusivity = Diffusivity;
            this.SafeUseTemp = SafeUseTemp;
            this.Cost = Cost;
        }

        public static void Load()
        {
            if (armors != null) return; // Only load the armor defs once on startup.
            armors = new ArmorInfos();
            if (armorNames == null) armorNames = new List<string>();
            UrlDir.UrlConfig[] nodes = GameDatabase.Instance.GetConfigs("ARMOR");
            ConfigNode node;

            // First locate BDA's default armor definition so we can fill in missing fields.
            if (defaultArmor == null)
                for (int i = 0; i < nodes.Length; ++i)
                {
                    if (nodes[i].parent.name != "BD_Armors") continue; // Ignore other config files.
                    node = nodes[i].config;
                    if (!node.HasValue("name") || (string)ParseField(nodes[i].config, "name", typeof(string)) != "def") continue; // Ignore other configs.
                    Debug.Log("[BDArmory.ArmorInfo]: Parsing default armor definition from " + nodes[i].parent.name);
                    defaultArmor = new ArmorInfo(
                        "def",
                        (float)ParseField(node, "Density", typeof(float)),
                        (float)ParseField(node, "Strength", typeof(float)),
                        (float)ParseField(node, "Hardness", typeof(float)),
                        (float)ParseField(node, "Ductility", typeof(float)),
                        (float)ParseField(node, "Diffusivity", typeof(float)),
                        (float)ParseField(node, "SafeUseTemp", typeof(float)),
                        (float)ParseField(node, "Cost", typeof(float))
                    );
                    armors.Add(defaultArmor);
                    armorNames.Add("def");
                    break;
                }
            if (defaultArmor == null) throw new ArgumentException("Failed to find BDArmory's default armor definition.", "defaultArmor");

            // Now add in the rest of the materials.
            for (int i = 0; i < nodes.Length; i++)
            {
                string name_ = "";
                try
                {
                    node = nodes[i].config;
                    name_ = (string)ParseField(node, "name", typeof(string));
                    if (armorNames.Contains(name_)) // Avoid duplicates.
                    {
                        if (nodes[i].parent.name != "BD_Armors" || name_ != "def") // Don't report the default bullet definition as a duplicate.
                            Debug.LogError("[BDArmory.ArmorInfo]: Armor definition " + name_ + " from " + nodes[i].parent.name + " already exists, skipping.");
                        continue;
                    }
                    Debug.Log("[BDArmory.ArmorInfo]: Parsing definition of armor " + name_ + " from " + nodes[i].parent.name);
                    armors.Add(
                        new ArmorInfo(
                            name_,
                        (float)ParseField(node, "Density", typeof(float)),
                        (float)ParseField(node, "Strength", typeof(float)),
                        (float)ParseField(node, "Hardness", typeof(float)),
                        (float)ParseField(node, "Ductility", typeof(float)),
                        (float)ParseField(node, "Diffusivity", typeof(float)),
                        (float)ParseField(node, "SafeUseTemp", typeof(float)),
                        (float)ParseField(node, "Cost", typeof(float))
                        )
                    );
                    armorNames.Add(name_);
                }
                catch (Exception e)
                {
                    Debug.LogError("[BDArmory.ArmorInfo]: Error Loading Armor Config '" + name_ + "' | " + e.ToString());
                }
            }
            //once armors are loaded, remove the def armor so it isn't found in later list parsings by HitpointTracker when updating parts armor
            armors.Remove(defaultArmor);
            armorNames.Remove("def");
        }

        private static object ParseField(ConfigNode node, string field, Type type)
        {
            try
            {
                if (!node.HasValue(field))
                    throw new ArgumentNullException(field, "Field '" + field + "' is missing.");
                var value = node.GetValue(field);
                try
                {
                    if (type == typeof(string))
                    { return value; }
                    else if (type == typeof(bool))
                    { return bool.Parse(value); }
                    else if (type == typeof(int))
                    { return int.Parse(value); }
                    else if (type == typeof(float))
                    { return float.Parse(value); }
                    else
                    { throw new ArgumentException("Invalid type specified."); }
                }
                catch (Exception e)
                { throw new ArgumentException("Field '" + field + "': '" + value + "' could not be parsed as '" + type.ToString() + "' | " + e.ToString(), field); }
            }
            catch (Exception e)
            {
                if (defaultArmor != null)
                {
                    // Give a warning about the missing or invalid value, then use the default value using reflection to find the field.
                    var defaultValue = typeof(ArmorInfo).GetProperty(field, BindingFlags.Public | BindingFlags.Instance).GetValue(defaultArmor);
                    Debug.LogError("[BDArmory.ArmorInfo]: Using default value of " + defaultValue.ToString() + " for " + field + " | " + e.ToString());
                    return defaultValue;
                }
                else
                    throw;
            }
        }
    }

    public class ArmorInfos : List<ArmorInfo>
    {
        public ArmorInfo this[string name]
        {
            get { return Find((value) => { return value.name == name; }); }
        }
    }
}