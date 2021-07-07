using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BDArmory.Core;
using BDArmory.Control;
using BDArmory.Misc;

namespace BDArmory.Modules
{
    /// <summary>
    /// A registry over all the asked for modules in all the asked for vessels.
    /// The lists are automatically updated whenever needed.
    /// Querying for a vessel or module that isn't yet in the registry causes the vessel or module to be added and tracked.
    /// 
    /// This removes the need for each module to scan for such modules, which often causes GC allocations and performance losses.
    /// The exception to this is that there is a race condition for functions triggering on the onVesselPartCountChanged event.
    /// Other functions that trigger on onVesselPartCountChanged or onPartJointBreak events should call OnVesselModified first before performing their own actions.
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselModuleRegistry : MonoBehaviour
    {
        #region Fields
        static public VesselModuleRegistry Instance;
        static public Dictionary<Vessel, Dictionary<Type, List<UnityEngine.Object>>> registry;
        static public Dictionary<Type, System.Reflection.MethodInfo> updateModuleCallbacks;
        public static HashSet<VesselType> ignoredVesselTypes = new HashSet<VesselType> { VesselType.Debris, VesselType.SpaceObject };

        // Specialised registries to avoid the boxing/unboxing GC allocations on frequently used module types.
        static public Dictionary<Vessel, List<MissileFire>> registryMissileFire;
        static public Dictionary<Vessel, List<MissileBase>> registryMissileBase;
        static public Dictionary<Vessel, List<BDModulePilotAI>> registryBDModulePilotAI;
        static public Dictionary<Vessel, List<BDModuleSurfaceAI>> registryBDModuleSurfaceAI;
        static public Dictionary<Vessel, List<IBDAIControl>> registryIBDAIControl;
        static public Dictionary<Vessel, List<ModuleWeapon>> registryModuleWeapon;
        static public Dictionary<Vessel, List<IBDWeapon>> registryIBDWeapon;
        static public Dictionary<Vessel, List<ModuleEngines>> registryModuleEngines;
        static public Dictionary<Vessel, List<ModuleCommand>> registryModuleCommand;
        static public Dictionary<Vessel, List<KerbalSeat>> registryKerbalSeat;
        static public Dictionary<Vessel, List<KerbalEVA>> registryKerbalEVA;

        static Dictionary<Vessel, int> vesselPartCounts;
        #endregion

        #region Monobehaviour methods
        void Awake()
        {
            if (Instance != null) { Destroy(Instance); }
            Instance = this;

            if (registry == null) { registry = new Dictionary<Vessel, Dictionary<Type, List<UnityEngine.Object>>>(); }
            if (registryMissileFire == null) { registryMissileFire = new Dictionary<Vessel, List<MissileFire>>(); }
            if (registryMissileBase == null) { registryMissileBase = new Dictionary<Vessel, List<MissileBase>>(); }
            if (registryModuleWeapon == null) { registryModuleWeapon = new Dictionary<Vessel, List<ModuleWeapon>>(); }
            if (registryIBDWeapon == null) { registryIBDWeapon = new Dictionary<Vessel, List<IBDWeapon>>(); }
            if (registryModuleEngines == null) { registryModuleEngines = new Dictionary<Vessel, List<ModuleEngines>>(); }
            if (registryBDModulePilotAI == null) { registryBDModulePilotAI = new Dictionary<Vessel, List<BDModulePilotAI>>(); }
            if (registryBDModuleSurfaceAI == null) { registryBDModuleSurfaceAI = new Dictionary<Vessel, List<BDModuleSurfaceAI>>(); }
            if (registryIBDAIControl == null) { registryIBDAIControl = new Dictionary<Vessel, List<IBDAIControl>>(); }
            if (registryModuleCommand == null) { registryModuleCommand = new Dictionary<Vessel, List<ModuleCommand>>(); }
            if (registryKerbalSeat == null) { registryKerbalSeat = new Dictionary<Vessel, List<KerbalSeat>>(); }
            if (registryKerbalEVA == null) { registryKerbalEVA = new Dictionary<Vessel, List<KerbalEVA>>(); }

            if (updateModuleCallbacks == null) { updateModuleCallbacks = new Dictionary<Type, System.Reflection.MethodInfo>(); }
            if (vesselPartCounts == null) { vesselPartCounts = new Dictionary<Vessel, int>(); }
        }

        void Start()
        {
            GameEvents.onVesselPartCountChanged.Add(OnVesselModifiedHandler);
        }

        void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(OnVesselModifiedHandler);

            registry.Clear();
            registryMissileFire.Clear();
            registryMissileBase.Clear();
            registryModuleWeapon.Clear();
            registryIBDWeapon.Clear();
            registryModuleEngines.Clear();
            registryBDModulePilotAI.Clear();
            registryBDModuleSurfaceAI.Clear();
            registryIBDAIControl.Clear();
            registryModuleCommand.Clear();
            registryKerbalSeat.Clear();
            registryKerbalEVA.Clear();

            updateModuleCallbacks.Clear();
            vesselPartCounts.Clear();
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Add a vessel to track to the registry.
        /// </summary>
        /// <param name="vessel">The vessel.</param>
        void AddVesselToRegistry(Vessel vessel)
        {
            registry.Add(vessel, new Dictionary<Type, List<UnityEngine.Object>>());
            vesselPartCounts[vessel] = vessel.Parts.Count;
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to registry.");
        }

        /// <summary>
        /// Add a module type to track to a vessel in the registry.
        /// </summary>
        /// <typeparam name="T">The module type to track.</typeparam>
        /// <param name="vessel">The vessel.</param>
        void AddVesselModuleTypeToRegistry<T>(Vessel vessel) where T : class
        {
            if (!registry[vessel].ContainsKey(typeof(T)))
            {
                registry[vessel].Add(typeof(T), new List<UnityEngine.Object>());
                updateModuleCallbacks[typeof(T)] = typeof(VesselModuleRegistry).GetMethod(nameof(VesselModuleRegistry.UpdateVesselModulesInRegistry), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).MakeGenericMethod(typeof(T));
            }
        }

        /// <summary>
        /// Update the list of modules of the given type in the registry for the given vessel.
        /// </summary>
        /// <typeparam name="T">The module type.</typeparam>
        /// <param name="vessel">The vessel.</param>
        void UpdateVesselModulesInRegistry<T>(Vessel vessel) where T : class
        {
            if (!registry.ContainsKey(vessel)) { AddVesselToRegistry(vessel); }
            if (!registry[vessel].ContainsKey(typeof(T))) { AddVesselModuleTypeToRegistry<T>(vessel); }
            registry[vessel][typeof(T)] = vessel.FindPartModulesImplementing<T>().ConvertAll(m => m as UnityEngine.Object);
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Registry entry for {vessel.vesselName} updated to have {registry[vessel][typeof(T)].Count} modules of type {typeof(T).Name}.");
        }

        /// <summary>
        /// Update the registry entries when a tracked vessel gets modified.
        /// </summary>
        /// <param name="vessel">The vessel that was modified.</param>
        public void OnVesselModifiedHandler(Vessel vessel)
        {
            if (vessel == null || !vessel.loaded || vessel.packed) return;
            if (vesselPartCounts.ContainsKey(vessel) && vessel.Parts.Count == vesselPartCounts[vessel]) return; // Already done.

            var partsAdded = vesselPartCounts.ContainsKey(vessel) && vessel.Parts.Count > vesselPartCounts[vessel];
            vesselPartCounts[vessel] = vessel.Parts.Count;

            if (registry.ContainsKey(vessel))
            {
                foreach (var moduleType in registry[vessel].Keys.ToList())
                {
                    if (!partsAdded && registry[vessel][moduleType].Count == 0) continue; // Part loss shouldn't give more modules.
                    // Invoke the specific callback to update the registry for this type of module.
                    updateModuleCallbacks[moduleType].Invoke(this, new object[1] { vessel });
                }
            }

            // Specialised registries.
            if (registryMissileFire.ContainsKey(vessel) && (partsAdded || registryMissileFire[vessel].Count > 0))
            {
                registryMissileFire[vessel] = vessel.FindPartModulesImplementing<MissileFire>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryMissileFire[vessel].Count} modules of type {typeof(MissileFire).Name}.");
            }
            if (registryMissileBase.ContainsKey(vessel) && (partsAdded || registryMissileBase[vessel].Count > 0))
            {
                registryMissileBase[vessel] = vessel.FindPartModulesImplementing<MissileBase>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryMissileBase[vessel].Count} modules of type {typeof(MissileBase).Name}.");
            }
            if (registryBDModulePilotAI.ContainsKey(vessel) && (partsAdded || registryBDModulePilotAI[vessel].Count > 0))
            {
                registryBDModulePilotAI[vessel] = vessel.FindPartModulesImplementing<BDModulePilotAI>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryBDModulePilotAI[vessel].Count} modules of type {typeof(BDModulePilotAI).Name}.");
            }
            if (registryBDModuleSurfaceAI.ContainsKey(vessel) && (partsAdded || registryBDModuleSurfaceAI[vessel].Count > 0))
            {
                registryBDModuleSurfaceAI[vessel] = vessel.FindPartModulesImplementing<BDModuleSurfaceAI>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryBDModuleSurfaceAI[vessel].Count} modules of type {typeof(BDModuleSurfaceAI).Name}.");
            }
            if (registryIBDAIControl.ContainsKey(vessel) && (partsAdded || registryIBDAIControl[vessel].Count > 0))
            {
                registryIBDAIControl[vessel] = vessel.FindPartModulesImplementing<IBDAIControl>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryIBDAIControl[vessel].Count} modules of type {typeof(IBDAIControl).Name}.");
            }
            if (registryModuleWeapon.ContainsKey(vessel) && (partsAdded || registryModuleWeapon[vessel].Count > 0))
            {
                registryModuleWeapon[vessel] = vessel.FindPartModulesImplementing<ModuleWeapon>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryModuleWeapon[vessel].Count} modules of type {typeof(ModuleWeapon).Name}.");
            }
            if (registryIBDWeapon.ContainsKey(vessel) && (partsAdded || registryIBDWeapon[vessel].Count > 0))
            {
                registryIBDWeapon[vessel] = vessel.FindPartModulesImplementing<IBDWeapon>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryIBDWeapon[vessel].Count} modules of type {typeof(IBDWeapon).Name}.");
            }
            if (registryModuleEngines.ContainsKey(vessel) && (partsAdded || registryModuleEngines[vessel].Count > 0))
            {
                registryModuleEngines[vessel] = vessel.FindPartModulesImplementing<ModuleEngines>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryModuleEngines[vessel].Count} modules of type {typeof(ModuleEngines).Name}.");
            }
            if (registryModuleCommand.ContainsKey(vessel) && (partsAdded || registryModuleCommand[vessel].Count > 0))
            {
                registryModuleCommand[vessel] = vessel.FindPartModulesImplementing<ModuleCommand>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryModuleCommand[vessel].Count} modules of type {typeof(ModuleCommand).Name}.");
            }
            if (registryKerbalSeat.ContainsKey(vessel) && (partsAdded || registryKerbalSeat[vessel].Count > 0))
            {
                registryKerbalSeat[vessel] = vessel.FindPartModulesImplementing<KerbalSeat>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryKerbalSeat[vessel].Count} modules of type {typeof(KerbalSeat).Name}.");
            }
            if (registryKerbalEVA.ContainsKey(vessel) && (partsAdded || registryKerbalEVA[vessel].Count > 0))
            {
                registryKerbalEVA[vessel] = vessel.FindPartModulesImplementing<KerbalEVA>();
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Specialised registry entry for {vessel.vesselName} updated to have {registryKerbalEVA[vessel].Count} modules of type {typeof(KerbalEVA).Name}.");
            }
        }
        #endregion

        #region Public methods
        /// <summary>
        /// Static interface to triggering the OnVesselModified handler.
        /// </summary>
        /// <param name="vessel">The vessel that was modified.</param>
        /// <param name="force">Update the registry even if the part count hasn't changed.</param>
        public static void OnVesselModified(Vessel vessel, bool force = false)
        {
            if (vessel == null) return;
            if (force) { vesselPartCounts[vessel] = -1; }
            Instance.OnVesselModifiedHandler(vessel);
        }

        /// <summary>
        /// Get an enumerable over the modules of the specified type in the specified vessel.
        /// This is about 15-30 times faster than FindPartModulesImplementing, but still requires around the same amount of GC allocations due to boxing/unboxing.
        /// </summary>
        /// <typeparam name="T">The module type to get.</typeparam>
        /// <param name="vessel">The vessel to get the modules from.</param>
        /// <returns>An enumerable for use in foreach loops or .ToList calls if the vessel exists, else null.</returns>
        public static List<T> GetModules<T>(Vessel vessel) where T : class
        {
            if (vessel == null) return null;

            if (typeof(T) == typeof(MissileFire)) { return GetMissileFires(vessel) as List<T>; }
            if (typeof(T) == typeof(MissileBase)) { return GetMissileBases(vessel) as List<T>; }
            if (typeof(T) == typeof(BDModulePilotAI)) { return GetBDModulePilotAIs(vessel) as List<T>; }
            if (typeof(T) == typeof(IBDAIControl)) { return GetIBDAIControls(vessel) as List<T>; }
            if (typeof(T) == typeof(BDModuleSurfaceAI)) { return GetBDModuleSurfaceAIs(vessel) as List<T>; }
            if (typeof(T) == typeof(ModuleWeapon)) { return GetModuleWeapons(vessel) as List<T>; }
            if (typeof(T) == typeof(IBDWeapon)) { return GetIBDWeapons(vessel) as List<T>; }
            if (typeof(T) == typeof(ModuleEngines)) { return GetModuleEngines(vessel) as List<T>; }
            if (typeof(T) == typeof(ModuleCommand)) { return GetModuleCommands(vessel) as List<T>; }
            if (typeof(T) == typeof(KerbalSeat)) { return GetKerbalSeats(vessel) as List<T>; }
            if (typeof(T) == typeof(KerbalEVA)) { return GetKerbalEVAs(vessel) as List<T>; }

            if (!registry.ContainsKey(vessel))
            { Instance.AddVesselToRegistry(vessel); }

            if (!registry[vessel].ContainsKey(typeof(T)))
            { Instance.UpdateVesselModulesInRegistry<T>(vessel); }

            return registry[vessel][typeof(T)].ConvertAll(m => m as T);
        }

        /// <summary>
        /// Get the first module of the specified type in the specified vessel.
        /// </summary>
        /// <typeparam name="T">The module type.</typeparam>
        /// <param name="vessel">The vessel.</param>
        /// <param name="firstNonNull">The first module or the first non-null module (may still be null if none are found).</param>
        /// <returns>The first module if it exists, else null.</returns>
        public static T GetModule<T>(Vessel vessel, bool firstNonNull = false) where T : class
        {
            var modules = GetModules<T>(vessel);
            if (modules == null) return null;
            if (!firstNonNull) return modules.FirstOrDefault();
            foreach (var module in modules)
            { if (module != null) return module; }
            return null;
        }

        /// <summary>
        /// Get the number of modules of the given type on the vessel.
        /// </summary>
        /// <typeparam name="T">The module type.</typeparam>
        /// <param name="vessel">The vessel.</param>
        /// <returns>The number of modules of that type on the vessel.</returns>
        public static int GetModuleCount<T>(Vessel vessel) where T : class
        {
            if (vessel == null) return 0;
            if (typeof(T) == typeof(MissileFire)) { return GetMissileFires(vessel).Count; }
            if (typeof(T) == typeof(MissileBase)) { return GetMissileBases(vessel).Count; }
            if (typeof(T) == typeof(BDModulePilotAI)) { return GetBDModulePilotAIs(vessel).Count; }
            if (typeof(T) == typeof(BDModuleSurfaceAI)) { return GetBDModuleSurfaceAIs(vessel).Count; }
            if (typeof(T) == typeof(IBDAIControl)) { return GetIBDAIControls(vessel).Count; }
            if (typeof(T) == typeof(ModuleWeapon)) { return GetModuleWeapons(vessel).Count; }
            if (typeof(T) == typeof(IBDWeapon)) { return GetIBDWeapons(vessel).Count; }
            if (typeof(T) == typeof(ModuleEngines)) { return GetModuleEngines(vessel).Count; }
            if (typeof(T) == typeof(ModuleCommand)) { return GetModuleCommands(vessel).Count; }
            if (typeof(T) == typeof(KerbalSeat)) { return GetKerbalSeats(vessel).Count; }
            if (typeof(T) == typeof(KerbalEVA)) { return GetKerbalEVAs(vessel).Count; }
            if (!registry.ContainsKey(vessel) || !registry[vessel].ContainsKey(typeof(T))) { Instance.UpdateVesselModulesInRegistry<T>(vessel); }
            return registry[vessel][typeof(T)].Count;
        }

        /// <summary>
        /// Clean out the registries and drop null vessels.
        /// </summary>
        public static void CleanRegistries()
        {
            // General registry.
            foreach (var vessel in registry.Keys.ToList()) { registry[vessel] = registry[vessel].Where(kvp => kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); } // Remove empty module lists.
            registry = registry.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            // Specialised registries.
            registryMissileFire = registryMissileFire.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryMissileBase = registryMissileBase.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryBDModulePilotAI = registryBDModulePilotAI.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryBDModuleSurfaceAI = registryBDModuleSurfaceAI.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryIBDAIControl = registryIBDAIControl.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryModuleWeapon = registryModuleWeapon.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryIBDWeapon = registryIBDWeapon.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryModuleEngines = registryModuleEngines.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryModuleCommand = registryModuleCommand.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryKerbalSeat = registryKerbalSeat.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
            registryKerbalEVA = registryKerbalEVA.Where(kvp => kvp.Key != null && kvp.Value.Count > 0).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); // Remove null and empty vessel entries.
        }

        #region Specialised methods
        // This would be much easier if C# implemented proper C++ style template specialisation.
        // These specialised methods give an extra speed boost by avoiding the boxing/unboxing associated with storing the modules as objects in the main registry.
        // They will be automatically used via the general method, but even more speed can be obtained by accessing them directly, particularly the ones returning a single item.

        public static List<MissileFire> GetMissileFires(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryMissileFire.ContainsKey(vessel))
            {
                registryMissileFire.Add(vessel, vessel.FindPartModulesImplementing<MissileFire>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(MissileFire).Name} registry.");
            }
            return registryMissileFire[vessel];
        }
        public static MissileFire GetMissileFire(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetMissileFires(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryMissileFire.ContainsKey(vessel)) { return GetMissileFires(vessel).FirstOrDefault(); }
            return registryMissileFire[vessel].FirstOrDefault();
        }

        public static List<MissileBase> GetMissileBases(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryMissileBase.ContainsKey(vessel))
            {
                registryMissileBase.Add(vessel, vessel.FindPartModulesImplementing<MissileBase>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(MissileBase).Name} registry.");
            }
            return registryMissileBase[vessel];
        }
        public static MissileBase GetMissileBase(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetMissileBases(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryMissileBase.ContainsKey(vessel)) { return GetMissileBases(vessel).FirstOrDefault(); }
            return registryMissileBase[vessel].FirstOrDefault();
        }

        public static List<BDModulePilotAI> GetBDModulePilotAIs(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryBDModulePilotAI.ContainsKey(vessel))
            {
                registryBDModulePilotAI.Add(vessel, vessel.FindPartModulesImplementing<BDModulePilotAI>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(BDModulePilotAI).Name} registry.");
            }
            return registryBDModulePilotAI[vessel];
        }
        public static BDModulePilotAI GetBDModulePilotAI(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetBDModulePilotAIs(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryBDModulePilotAI.ContainsKey(vessel)) { return GetBDModulePilotAIs(vessel).FirstOrDefault(); }
            return registryBDModulePilotAI[vessel].FirstOrDefault();
        }

        public static List<BDModuleSurfaceAI> GetBDModuleSurfaceAIs(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryBDModuleSurfaceAI.ContainsKey(vessel))
            {
                registryBDModuleSurfaceAI.Add(vessel, vessel.FindPartModulesImplementing<BDModuleSurfaceAI>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(BDModuleSurfaceAI).Name} registry.");
            }
            return registryBDModuleSurfaceAI[vessel];
        }
        public static BDModuleSurfaceAI GetBDModuleSurfaceAI(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetBDModuleSurfaceAIs(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryBDModuleSurfaceAI.ContainsKey(vessel)) { return GetBDModuleSurfaceAIs(vessel).FirstOrDefault(); }
            return registryBDModuleSurfaceAI[vessel].FirstOrDefault();
        }

        public static List<IBDAIControl> GetIBDAIControls(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryIBDAIControl.ContainsKey(vessel))
            {
                registryIBDAIControl.Add(vessel, vessel.FindPartModulesImplementing<IBDAIControl>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(IBDAIControl).Name} registry.");
            }
            return registryIBDAIControl[vessel];
        }
        public static IBDAIControl GetIBDAIControl(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetIBDAIControls(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryIBDAIControl.ContainsKey(vessel)) { return GetIBDAIControls(vessel).FirstOrDefault(); }
            return registryIBDAIControl[vessel].FirstOrDefault();
        }

        public static List<ModuleWeapon> GetModuleWeapons(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryModuleWeapon.ContainsKey(vessel))
            {
                registryModuleWeapon.Add(vessel, vessel.FindPartModulesImplementing<ModuleWeapon>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(ModuleWeapon).Name} registry.");
            }
            return registryModuleWeapon[vessel];
        }

        public static List<IBDWeapon> GetIBDWeapons(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryIBDWeapon.ContainsKey(vessel))
            {
                registryIBDWeapon.Add(vessel, vessel.FindPartModulesImplementing<IBDWeapon>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(IBDWeapon).Name} registry.");
            }
            return registryIBDWeapon[vessel];
        }

        public static List<ModuleEngines> GetModuleEngines(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryModuleEngines.ContainsKey(vessel))
            {
                registryModuleEngines.Add(vessel, vessel.FindPartModulesImplementing<ModuleEngines>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(ModuleEngines).Name} registry.");
            }
            return registryModuleEngines[vessel];
        }

        public static List<ModuleCommand> GetModuleCommands(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryModuleCommand.ContainsKey(vessel))
            {
                registryModuleCommand.Add(vessel, vessel.FindPartModulesImplementing<ModuleCommand>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(ModuleCommand).Name} registry.");
            }
            return registryModuleCommand[vessel];
        }
        public static ModuleCommand GetModuleCommand(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetModuleCommands(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryModuleCommand.ContainsKey(vessel)) { return GetModuleCommands(vessel).FirstOrDefault(); }
            return registryModuleCommand[vessel].FirstOrDefault();
        }

        public static List<KerbalSeat> GetKerbalSeats(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryKerbalSeat.ContainsKey(vessel))
            {
                registryKerbalSeat.Add(vessel, vessel.FindPartModulesImplementing<KerbalSeat>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(KerbalSeat).Name} registry.");
            }
            return registryKerbalSeat[vessel];
        }
        public static KerbalSeat GetKerbalSeat(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetKerbalSeats(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryKerbalSeat.ContainsKey(vessel)) { return GetKerbalSeats(vessel).FirstOrDefault(); }
            return registryKerbalSeat[vessel].FirstOrDefault();
        }

        public static List<KerbalEVA> GetKerbalEVAs(Vessel vessel)
        {
            if (vessel == null) return null;
            if (!registryKerbalEVA.ContainsKey(vessel))
            {
                registryKerbalEVA.Add(vessel, vessel.FindPartModulesImplementing<KerbalEVA>());
                vesselPartCounts[vessel] = vessel.Parts.Count;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log($"[BDArmory.VesselModuleRegistry]: Vessel {vessel.vesselName} added to specialised {typeof(KerbalEVA).Name} registry.");
            }
            return registryKerbalEVA[vessel];
        }
        public static KerbalEVA GetKerbalEVA(Vessel vessel, bool firstNonNull = false)
        {
            if (vessel == null) return null;
            if (firstNonNull)
            {
                foreach (var module in GetKerbalEVAs(vessel))
                { if (module != null) return module; }
                return null;
            }
            if (!registryKerbalEVA.ContainsKey(vessel)) { return GetKerbalEVAs(vessel).FirstOrDefault(); }
            return registryKerbalEVA[vessel].FirstOrDefault();
        }
        #endregion

        public IEnumerator PerformanceTest()
        {
            var wait = new WaitForSeconds(0.1f);
            {
                // Note: this test has significant GC allocations due to the allocation of an intermediate list.
                int count = 0;
                int iters = 100000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { foreach (var mf in FlightGlobals.ActiveVessel.FindPartModulesImplementing<MissileFire>()) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via vessel.FindPartModulesImplementing<MissileFire>()");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 100000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { if (FlightGlobals.ActiveVessel.FindPartModuleImplementing<MissileFire>() != null) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via vessel.FindPartModuleImplementing<MissileFire>()");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 10000000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { foreach (var mf in VesselModuleRegistry.GetModules<MissileFire>(FlightGlobals.ActiveVessel)) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via VesselModuleRegistry.GetModules<MissileFire>(vessel)");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 10000000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { foreach (var mf in VesselModuleRegistry.GetMissileFires(FlightGlobals.ActiveVessel)) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via VesselModuleRegistry.GetMissileFires(vessel)");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 10000000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { if (VesselModuleRegistry.GetModule<MissileFire>(FlightGlobals.ActiveVessel) != null) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via VesselModuleRegistry.GetModule<MissileFire>(vessel)");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 10000000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { if (VesselModuleRegistry.GetModule<MissileFire>(FlightGlobals.ActiveVessel, true) != null) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via VesselModuleRegistry.GetModule<MissileFire>(vessel, true)");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 10000000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { if (VesselModuleRegistry.GetMissileFire(FlightGlobals.ActiveVessel) != null) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via VesselModuleRegistry.GetMissileFire(vessel)");
            }
            yield return wait;
            {
                int count = 0;
                int iters = 10000000;
                var startTime = Time.realtimeSinceStartup;
                for (int i = 0; i < iters; ++i) { if (VesselModuleRegistry.GetMissileFire(FlightGlobals.ActiveVessel, true) != null) ++count; }
                Debug.Log($"DEBUG {FlightGlobals.ActiveVessel} has {count / iters} weapon managers, checked at {iters / (Time.realtimeSinceStartup - startTime)}/s via VesselModuleRegistry.GetMissileFire(vessel, true)");
            }
            BDACompetitionMode.Instance.competitionStatus.Add("VesselModuleRegistry performance test complete.");
        }
        #endregion
    }
}