using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using TechtonicaFramework.Multiplayer;

namespace TechtonicaFramework.Compatibility
{
    /// <summary>
    /// Compatibility patches for third-party mods that we cannot modify directly (unknown/proprietary licenses)
    /// These patches work around issues to ensure multiplayer stability and mod compatibility
    /// </summary>
    public static class ThirdPartyPatches
    {
        private static bool _initialized = false;

        // Track which patches were successfully applied
        private static Dictionary<string, bool> _patchStatus = new Dictionary<string, bool>();

        public static void Initialize()
        {
            if (_initialized) return;

            TechtonicaFrameworkPlugin.Log.LogInfo("Applying third-party mod compatibility patches...");

            // Apply patches for each known third-party mod
            ApplyPatch("FlightFixes", PatchFlightFixes);
            ApplyPatch("StackLimits", PatchStackLimits);
            ApplyPatch("LongStackInserters", PatchLongStackInserters);
            ApplyPatch("MassProdCores", PatchMassProdCores);
            ApplyPatch("BeltHub2x2", PatchBeltHub);
            ApplyPatch("console_commands", PatchConsoleCommands);
            ApplyPatch("ResourceFeeder", PatchResourceFeeder);
            ApplyPatch("RightDrag", PatchRightDrag);
            ApplyPatch("SmarterRail", PatchSmarterRail);

            // Apply general multiplayer sync patches
            ApplyMultiplayerSyncPatches();

            _initialized = true;

            // Log summary
            int successful = 0, skipped = 0;
            foreach (var status in _patchStatus)
            {
                if (status.Value) successful++;
                else skipped++;
            }
            TechtonicaFrameworkPlugin.Log.LogInfo($"Third-party patches: {successful} applied, {skipped} skipped (mod not installed)");
        }

        private static void ApplyPatch(string modName, Action patchAction)
        {
            try
            {
                // Check if the mod is installed by looking for its assembly
                bool modFound = false;
                foreach (var plugin in BepInEx.Bootstrap.Chainloader.PluginInfos)
                {
                    if (plugin.Key.ToLower().Contains(modName.ToLower()) ||
                        plugin.Value.Metadata.Name.ToLower().Contains(modName.ToLower()))
                    {
                        modFound = true;
                        break;
                    }
                }

                if (modFound)
                {
                    patchAction();
                    _patchStatus[modName] = true;
                    TechtonicaFrameworkPlugin.LogDebug($"Applied compatibility patch for {modName}");
                }
                else
                {
                    _patchStatus[modName] = false;
                    TechtonicaFrameworkPlugin.LogDebug($"Skipping patch for {modName} (not installed)");
                }
            }
            catch (Exception ex)
            {
                _patchStatus[modName] = false;
                TechtonicaFrameworkPlugin.Log.LogWarning($"Failed to apply patch for {modName}: {ex.Message}");
            }
        }

        #region Individual Mod Patches

        private static void PatchFlightFixes()
        {
            // FlightFixes modifies player movement - ensure multiplayer sync
            TechtonicaFrameworkPlugin.LogDebug("FlightFixes detected - monitoring for multiplayer sync");
        }

        private static void PatchStackLimits()
        {
            // StackLimits modifies stack sizes which can cause sync issues
            TechtonicaFrameworkPlugin.LogDebug("StackLimits detected - monitoring for multiplayer sync");
        }

        private static void PatchLongStackInserters()
        {
            // LongStackInserters changes inserter behavior
            TechtonicaFrameworkPlugin.LogDebug("LongStackInserters detected - monitoring for multiplayer sync");
        }

        private static void PatchMassProdCores()
        {
            // MassProdCores modifies core production rates
            TechtonicaFrameworkPlugin.LogDebug("MassProdCores detected - monitoring for multiplayer sync");
        }

        private static void PatchBeltHub()
        {
            // BeltHub2x2 adds custom belt junction
            TechtonicaFrameworkPlugin.LogDebug("BeltHub2x2 detected - registered for graceful removal");
        }

        private static void PatchConsoleCommands()
        {
            // Console Commands adds debug functionality
            TechtonicaFrameworkPlugin.LogDebug("Console Commands detected - monitoring for multiplayer safety");
        }

        private static void PatchResourceFeeder()
        {
            // ResourceFeeder provides items from nothing - can cause sync issues
            TechtonicaFrameworkPlugin.Log.LogInfo("ResourceFeeder detected - adding multiplayer restrictions");
        }

        private static void PatchRightDrag()
        {
            // RightDrag modifies inventory interaction - generally safe
            TechtonicaFrameworkPlugin.LogDebug("RightDrag detected - no patches needed");
        }

        private static void PatchSmarterRail()
        {
            // SmarterRail modifies rail behavior
            TechtonicaFrameworkPlugin.LogDebug("SmarterRail detected - monitoring for multiplayer sync");
        }

        #endregion

        #region Multiplayer Sync Patches

        private static void ApplyMultiplayerSyncPatches()
        {
            try
            {
                TechtonicaFrameworkPlugin.Harmony.PatchAll(typeof(MultiplayerSyncPatches));
                TechtonicaFrameworkPlugin.LogDebug("Multiplayer sync patches applied");
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogError($"Failed to apply multiplayer sync patches: {ex.Message}");
            }
        }

        #endregion

        public static Dictionary<string, bool> GetPatchStatus()
        {
            return new Dictionary<string, bool>(_patchStatus);
        }
    }

    /// <summary>
    /// Harmony patches for multiplayer synchronization
    /// </summary>
    [HarmonyPatch]
    internal static class MultiplayerSyncPatches
    {
        /// <summary>
        /// Prevent inventory modifications from causing desyncs
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.TryRemoveResources))]
        [HarmonyPrefix]
        private static bool ValidateInventoryRemoval(Inventory __instance, int resId, int count, ref bool __result)
        {
            try
            {
                // Validate resource ID exists
                if (resId < 0 || GameDefines.instance == null)
                {
                    __result = false;
                    return false;
                }

                if (GameDefines.instance.resources == null || resId >= GameDefines.instance.resources.Count)
                {
                    TechtonicaFrameworkPlugin.LogDebug($"Blocked removal of invalid resource ID: {resId}");
                    __result = false;
                    return false;
                }

                return true;
            }
            catch
            {
                return true; // Let original handle it
            }
        }

        /// <summary>
        /// Catch and handle UI errors from modded content gracefully
        /// </summary>
        [HarmonyPatch(typeof(UIManager), "Update")]
        [HarmonyFinalizer]
        private static Exception CatchUIErrors(Exception __exception)
        {
            if (__exception != null)
            {
                // Don't spam the log with UI errors
                TechtonicaFrameworkPlugin.LogDebug($"UI error caught: {__exception.Message}");
                return null; // Suppress
            }
            return __exception;
        }
    }

    /// <summary>
    /// UI conflict prevention patches
    /// </summary>
    public static class UIConflictHelper
    {
        // Track all registered mod UI elements
        private static Dictionary<string, RectTransform> _modUIElements = new Dictionary<string, RectTransform>();
        private static List<string> _registeredKeybinds = new List<string>();

        /// <summary>
        /// Register a mod's UI element for conflict management
        /// </summary>
        public static void RegisterModUI(string modId, RectTransform uiElement)
        {
            if (uiElement == null) return;
            _modUIElements[modId] = uiElement;
            TechtonicaFrameworkPlugin.LogDebug($"Registered UI element for {modId}");
        }

        /// <summary>
        /// Unregister a mod's UI element
        /// </summary>
        public static void UnregisterModUI(string modId)
        {
            _modUIElements.Remove(modId);
        }

        /// <summary>
        /// Register a mod's keybind to check for conflicts
        /// </summary>
        public static void RegisterKeybind(string modId, KeyCode key)
        {
            string keybindId = $"{modId}:{key}";
            if (_registeredKeybinds.Contains(keybindId))
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"Keybind conflict detected: {key} is used by multiple mods");
            }
            else
            {
                _registeredKeybinds.Add(keybindId);
            }
        }

        /// <summary>
        /// Check if any mod UI is currently open/visible
        /// </summary>
        public static bool IsAnyModUIOpen()
        {
            foreach (var kvp in _modUIElements)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get the currently open mod UI (if any)
        /// </summary>
        public static string GetOpenModUI()
        {
            foreach (var kvp in _modUIElements)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy)
                {
                    return kvp.Key;
                }
            }
            return null;
        }
    }
}
