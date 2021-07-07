using BDArmory.Core.Module;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDAAdjustableArmor : PartModule
    {
        [KSPField(isPersistant = true)] public float Height = 1;

        [KSPField(isPersistant = true)] public float Length = 1;

        [KSPField]
        public string ArmorTransformName = "ArmorRootTransform";
        Transform armorTransform;

        HitpointTracker armor;

        public override void OnStart(StartState state)
        {
            armorTransform = part.FindModelTransform(ArmorTransformName);

            armorTransform.localScale = new Vector3(Height, Length, 1);

            if (HighLogic.LoadedSceneIsEditor)
            {
                armor = GetComponent<HitpointTracker>();
            }
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_IncreaseHeight", active = true)]//Height ++
        public void IncreaseHeight()
        {
            Height = Mathf.Clamp(Height + 0.5f, 1f, 16f);
            armorTransform.localScale = new Vector3(Height, Length, 1);
            List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator();
            while (sym.MoveNext())
            {
                if (sym.Current == null) continue;
                sym.Current.FindModuleImplementing<BDAAdjustableArmor>().UpdateScale(Height, Length);
                updateArmorStats();
            }
            sym.Dispose();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_DecreaseHeight", active = true)]//Height --
        public void DecreaseHeight()
        {
            Height = Mathf.Clamp(Height - 0.5f, 1f, 16f);
            armorTransform.localScale = new Vector3(Height, Length, 1);
            List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator();
            while (sym.MoveNext())
            {
                if (sym.Current == null) continue;
                sym.Current.FindModuleImplementing<BDAAdjustableArmor>().UpdateScale(Height, Length);
                updateArmorStats();
            }
            sym.Dispose();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_IncreaseLength", active = true)]//Length ++
        public void IncreaseLength()
        {
            Length = Mathf.Clamp(Length + 0.5f, 1f, 16f);
            armorTransform.localScale = new Vector3(Height, Length, 1);
            List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator();
            while (sym.MoveNext())
            {
                if (sym.Current == null) continue;
                sym.Current.FindModuleImplementing<BDAAdjustableArmor>().UpdateScale(Height, Length);
                updateArmorStats();
            }
            sym.Dispose();
        }

        [KSPEvent(guiActive = false, guiActiveEditor = true, guiName = "#LOC_BDArmory_DecreaseLength", active = true)]//Length --
        public void DecreaseLength()
        {
            Length = Mathf.Clamp(Length - 0.5f, 1f, 16f);
            armorTransform.localScale = new Vector3(Height, Length, 1);
            List<Part>.Enumerator sym = part.symmetryCounterparts.GetEnumerator();
            while (sym.MoveNext())
            {
                if (sym.Current == null) continue;
                sym.Current.FindModuleImplementing<BDAAdjustableArmor>().UpdateScale(Height, Length);
                updateArmorStats();
            }
            sym.Dispose();
        }

        public void UpdateScale(float height, float length)
        {
            Height = height;
            Length = length;
            armorTransform.localScale = new Vector3(Height, Length, 1);
            updateArmorStats();
        }
        public void updateArmorStats()
        {
            armor.ArmorSetup(null, null);
        }        
    }
}
