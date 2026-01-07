using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using EquinoxsModUtils;
using HarmonyLib;
using UnityEngine;

namespace TechtonicaFramework.BuildMenu
{
    /// <summary>
    /// Provides a "Modded" tab in the build/crafting menu for all mods to use.
    /// This keeps modded items separate from vanilla items for clarity.
    /// </summary>
    public static class BuildMenuModule
    {
        public const string MODDED_HEADER_NAME = "Modded";
        public const string MODDED_SUBHEADER_NAME = "Modded Items";
        public const int MODDED_HEADER_ORDER = 999;  // Put at end
        public const int MODDED_SUBHEADER_PRIORITY = 1;

        private static ManualLogSource log;
        private static bool initialized = false;
        private static SchematicsHeader moddedHeader;
        private static SchematicsSubHeader moddedSubHeader;

        // Track vanilla resource names for filtering
        private static HashSet<string> vanillaResourceNames = new HashSet<string>();

        /// <summary>
        /// Initialize the BuildMenu module. Call from TechtonicaFramework.
        /// </summary>
        public static void Initialize(ManualLogSource logger)
        {
            log = logger;

            // Register for game events
            EMU.Events.GameDefinesLoaded += OnGameDefinesLoaded;

            log?.LogInfo("BuildMenuModule: Initialized - waiting for GameDefines");
            initialized = true;
        }

        /// <summary>
        /// Get the Modded header for use when registering items
        /// </summary>
        public static SchematicsHeader ModdedHeader => moddedHeader;

        /// <summary>
        /// Get the Modded subheader for use when registering items
        /// </summary>
        public static SchematicsSubHeader ModdedSubHeader => moddedSubHeader;

        /// <summary>
        /// Check if an item name is a vanilla (non-modded) resource
        /// </summary>
        public static bool IsVanillaResource(string resourceName)
        {
            return vanillaResourceNames.Contains(resourceName);
        }

        /// <summary>
        /// Move a resource to the Modded tab
        /// </summary>
        public static void MoveToModdedTab(string resourceName)
        {
            if (moddedSubHeader == null)
            {
                log?.LogWarning($"BuildMenuModule: Cannot move '{resourceName}' - Modded tab not ready");
                return;
            }

            try
            {
                EMU.Resources.UpdateResourceHeaderType(resourceName, moddedSubHeader, false);
                log?.LogInfo($"BuildMenuModule: Moved '{resourceName}' to Modded tab");
            }
            catch (Exception ex)
            {
                log?.LogError($"BuildMenuModule: Error moving '{resourceName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Move all non-vanilla resources to the Modded tab
        /// </summary>
        public static void MoveAllModdedItems()
        {
            if (GameDefines.instance == null || moddedSubHeader == null)
            {
                log?.LogWarning("BuildMenuModule: Cannot move items - not ready");
                return;
            }

            int movedCount = 0;
            foreach (var resource in GameDefines.instance.resources)
            {
                if (resource == null) continue;

                string name = resource.displayName;
                if (string.IsNullOrEmpty(name)) continue;

                // Skip vanilla resources
                if (vanillaResourceNames.Contains(name)) continue;

                // Skip if already in Modded tab
                if (resource.headerType == moddedSubHeader) continue;

                try
                {
                    resource.headerType = moddedSubHeader;
                    movedCount++;
                    log?.LogInfo($"BuildMenuModule: Moved '{name}' to Modded tab");
                }
                catch (Exception ex)
                {
                    log?.LogError($"BuildMenuModule: Error moving '{name}': {ex.Message}");
                }
            }

            log?.LogInfo($"BuildMenuModule: Moved {movedCount} modded items to Modded tab");
        }

        private static void OnGameDefinesLoaded()
        {
            try
            {
                // First, capture all vanilla resource names before mods add theirs
                CaptureVanillaResources();

                // Create the Modded header and subheader
                CreateModdedHeader();
                CreateModdedSubHeader();

                log?.LogInfo("BuildMenuModule: Modded build menu tab created successfully");
            }
            catch (Exception ex)
            {
                log?.LogError($"BuildMenuModule: Error in OnGameDefinesLoaded: {ex}");
            }
        }

        private static void CaptureVanillaResources()
        {
            vanillaResourceNames.Clear();

            if (GameDefines.instance?.resources == null) return;

            foreach (var resource in GameDefines.instance.resources)
            {
                if (resource != null && !string.IsNullOrEmpty(resource.displayName))
                {
                    vanillaResourceNames.Add(resource.displayName);
                }
            }

            log?.LogInfo($"BuildMenuModule: Captured {vanillaResourceNames.Count} vanilla resource names");
        }

        private static void CreateModdedHeader()
        {
            if (GameDefines.instance == null)
            {
                log?.LogError("BuildMenuModule: GameDefines not available");
                return;
            }

            // Check if header already exists
            foreach (var header in GameDefines.instance.schematicsHeaderEntries)
            {
                if (header != null && header.title == MODDED_HEADER_NAME)
                {
                    moddedHeader = header;
                    log?.LogInfo("BuildMenuModule: Modded header already exists");
                    return;
                }
            }

            // Create new header
            moddedHeader = ScriptableObject.CreateInstance<SchematicsHeader>();
            moddedHeader.title = MODDED_HEADER_NAME;
            moddedHeader.order = MODDED_HEADER_ORDER;

            // Assign unique ID
            int maxId = 0;
            foreach (var header in GameDefines.instance.schematicsHeaderEntries)
            {
                if (header != null && header.uniqueId > maxId)
                    maxId = header.uniqueId;
            }

            // Use reflection to set uniqueId (it's in UniqueIdScriptableObject base class)
            var uniqueIdField = typeof(UniqueIdScriptableObject).GetField("uniqueId", BindingFlags.Public | BindingFlags.Instance);
            if (uniqueIdField != null)
            {
                uniqueIdField.SetValue(moddedHeader, maxId + 1);
            }

            // Add to game
            GameDefines.instance.schematicsHeaderEntries.Add(moddedHeader);
            log?.LogInfo($"BuildMenuModule: Created Modded header with ID {moddedHeader.uniqueId}");
        }

        private static void CreateModdedSubHeader()
        {
            if (GameDefines.instance == null || moddedHeader == null)
            {
                log?.LogError("BuildMenuModule: Cannot create subheader - prerequisites missing");
                return;
            }

            // Check if subheader already exists
            foreach (var subHeader in GameDefines.instance.schematicsSubHeaderEntries)
            {
                if (subHeader != null && subHeader.title == MODDED_SUBHEADER_NAME &&
                    subHeader.filterTag == moddedHeader)
                {
                    moddedSubHeader = subHeader;
                    log?.LogInfo("BuildMenuModule: Modded subheader already exists");
                    return;
                }
            }

            // Create new subheader
            moddedSubHeader = ScriptableObject.CreateInstance<SchematicsSubHeader>();
            moddedSubHeader.title = MODDED_SUBHEADER_NAME;
            moddedSubHeader.filterTag = moddedHeader;
            moddedSubHeader.priority = MODDED_SUBHEADER_PRIORITY;

            // Assign unique ID
            int maxId = 0;
            foreach (var subHeader in GameDefines.instance.schematicsSubHeaderEntries)
            {
                if (subHeader != null && subHeader.uniqueId > maxId)
                    maxId = subHeader.uniqueId;
            }

            var uniqueIdField = typeof(UniqueIdScriptableObject).GetField("uniqueId", BindingFlags.Public | BindingFlags.Instance);
            if (uniqueIdField != null)
            {
                uniqueIdField.SetValue(moddedSubHeader, maxId + 1);
            }

            // Add to game
            GameDefines.instance.schematicsSubHeaderEntries.Add(moddedSubHeader);
            log?.LogInfo($"BuildMenuModule: Created Modded subheader with ID {moddedSubHeader.uniqueId}");
        }
    }
}
