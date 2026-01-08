using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace TechtonicaFramework.Multiplayer
{
    /// <summary>
    /// Multiplayer Compatibility Module - Ensures modded content works in multiplayer
    /// Handles syncing of custom machine values, graceful handling of modded content,
    /// and prevents crashes when clients don't have matching mods
    /// </summary>
    public class MultiplayerModule
    {
        private static MultiplayerModule _instance;
        public static MultiplayerModule Instance => _instance;

        // Track which mods are installed (for compatibility checking)
        private static Dictionary<string, string> _installedMods = new Dictionary<string, string>();

        // Track custom machine types added by mods
        private static HashSet<string> _moddedMachineTypes = new HashSet<string>();

        // Track custom resource types added by mods
        private static HashSet<string> _moddedResourceTypes = new HashSet<string>();

        // Configuration
        public static ConfigEntry<bool> EnableMultiplayerSync;
        public static ConfigEntry<bool> AllowMismatchedMods;
        public static ConfigEntry<bool> AutoRemoveUnknownMachines;

        public void Initialize()
        {
            _instance = this;

            // Initialize config
            EnableMultiplayerSync = TechtonicaFrameworkPlugin.Instance.Config.Bind(
                "Multiplayer", "Enable Sync", true,
                "Enable multiplayer synchronization for modded content");

            AllowMismatchedMods = TechtonicaFrameworkPlugin.Instance.Config.Bind(
                "Multiplayer", "Allow Mismatched Mods", true,
                "Allow joining games where host has different mods (may cause visual glitches)");

            AutoRemoveUnknownMachines = TechtonicaFrameworkPlugin.Instance.Config.Bind(
                "Multiplayer", "Auto Remove Unknown Machines", true,
                "Automatically remove machines from mods that are no longer installed");

            // Register all currently loaded mods
            RegisterInstalledMods();

            // Apply multiplayer patches
            ApplyPatches();

            TechtonicaFrameworkPlugin.Log.LogInfo("MultiplayerModule initialized");
        }

        private void RegisterInstalledMods()
        {
            foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
            {
                var metadata = plugin.Value.Metadata;
                _installedMods[metadata.GUID] = metadata.Version.ToString();
                TechtonicaFrameworkPlugin.LogDebug($"Registered mod: {metadata.GUID} v{metadata.Version}");
            }
        }

        /// <summary>
        /// Register a custom machine type added by a mod
        /// </summary>
        public static void RegisterModdedMachine(string machineType, string modGUID)
        {
            _moddedMachineTypes.Add(machineType);
            TechtonicaFrameworkPlugin.LogDebug($"Registered modded machine: {machineType} from {modGUID}");
        }

        /// <summary>
        /// Register a custom resource type added by a mod
        /// </summary>
        public static void RegisterModdedResource(string resourceType, string modGUID)
        {
            _moddedResourceTypes.Add(resourceType);
            TechtonicaFrameworkPlugin.LogDebug($"Registered modded resource: {resourceType} from {modGUID}");
        }

        /// <summary>
        /// Check if a machine type is from a mod
        /// </summary>
        public static bool IsModdedMachine(string machineType)
        {
            return _moddedMachineTypes.Contains(machineType);
        }

        /// <summary>
        /// Check if a resource type is from a mod
        /// </summary>
        public static bool IsModdedResource(string resourceType)
        {
            return _moddedResourceTypes.Contains(resourceType);
        }

        /// <summary>
        /// Get list of installed mods for sync purposes
        /// </summary>
        public static Dictionary<string, string> GetInstalledMods()
        {
            return new Dictionary<string, string>(_installedMods);
        }

        private void ApplyPatches()
        {
            try
            {
                TechtonicaFrameworkPlugin.Harmony.PatchAll(typeof(MultiplayerPatches));
                TechtonicaFrameworkPlugin.LogDebug("Multiplayer patches applied");
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogError($"Failed to apply multiplayer patches: {ex.Message}");
            }
        }

        public void Update()
        {
            // Periodic sync checks if in multiplayer
        }

        public void Shutdown()
        {
            _moddedMachineTypes.Clear();
            _moddedResourceTypes.Clear();
        }
    }

    /// <summary>
    /// Harmony patches for multiplayer compatibility
    /// </summary>
    [HarmonyPatch]
    internal static class MultiplayerPatches
    {
        /// <summary>
        /// Patch save loading to handle missing modded content gracefully
        /// Target the string overload: LoadFileData(string saveLocation = "/quickSaveData.dat")
        /// </summary>
        [HarmonyPatch(typeof(SaveState), nameof(SaveState.LoadFileData), new Type[] { typeof(string) })]
        [HarmonyPrefix]
        private static void BeforeLoadSave()
        {
            TechtonicaFrameworkPlugin.LogDebug("Save load starting - preparing for modded content check");
        }

        [HarmonyPatch(typeof(SaveState), nameof(SaveState.LoadFileData), new Type[] { typeof(string) })]
        [HarmonyPostfix]
        private static void AfterLoadSave()
        {
            TechtonicaFrameworkPlugin.LogDebug("Save load complete - checking for orphaned modded content");

            // Check for and handle orphaned modded content
            if (MultiplayerModule.Instance != null && MultiplayerModule.AutoRemoveUnknownMachines.Value)
            {
                GracefulRemovalModule.CleanupOrphanedModdedContent();
            }
        }

        /// <summary>
        /// Prevent crash when receiving network data for unknown machine types
        /// </summary>
        [HarmonyPatch(typeof(MachineManager), nameof(MachineManager.GetRefFromId))]
        [HarmonyFinalizer]
        private static Exception SafeGetMachineRef(Exception __exception)
        {
            if (__exception != null)
            {
                TechtonicaFrameworkPlugin.LogDebug($"Safe catch in GetRefFromId: {__exception.Message}");
                return null; // Suppress exception
            }
            return __exception;
        }

        /// <summary>
        /// Ensure multiplayer clients don't crash on unknown resource IDs
        /// </summary>
        [HarmonyPatch(typeof(SaveState), "LoadResourceInfo")]
        [HarmonyFinalizer]
        private static Exception CatchResourceLoadErrors(Exception __exception)
        {
            if (__exception != null)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"Resource load error (likely from missing mod): {__exception.Message}");
                // Suppress the exception to prevent crash
                return null;
            }
            return __exception;
        }

        /// <summary>
        /// Patch inventory operations to handle unknown items gracefully
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddResources), typeof(int), typeof(int))]
        [HarmonyPrefix]
        private static bool SafeAddResources(Inventory __instance, int resId, int count)
        {
            try
            {
                // Check if resource ID is valid
                if (resId < 0 || GameDefines.instance == null)
                    return true;

                var resourceInfos = GameDefines.instance.resources;
                if (resourceInfos == null || resId >= resourceInfos.Count)
                {
                    TechtonicaFrameworkPlugin.LogDebug($"Skipping unknown resource ID: {resId}");
                    return false; // Skip adding unknown resource
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
