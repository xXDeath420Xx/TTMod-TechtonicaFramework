using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EquinoxsModUtils;
using HarmonyLib;
using UnityEngine;
using TechtonicaFramework.Core;
using TechtonicaFramework.Health;
using TechtonicaFramework.Environment;
using TechtonicaFramework.Narrative;
using TechtonicaFramework.Equipment;
using TechtonicaFramework.TechTree;
using TechtonicaFramework.BuildMenu;

namespace TechtonicaFramework
{
    /// <summary>
    /// TechtonicaFramework - A foundation mod providing reusable systems for Techtonica modding.
    /// Includes: Health/Damage, Environmental Hazards, Narrative/Dialogue, Equipment/Vehicles
    /// </summary>
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    [BepInDependency("com.equinox.EquinoxsModUtils")]
    [BepInDependency("com.equinox.EMUAdditions")]
    public class TechtonicaFrameworkPlugin : BaseUnityPlugin
    {
        public const string MyGUID = "com.certifired.TechtonicaFramework";
        public const string PluginName = "TechtonicaFramework";
        public const string VersionString = "1.2.0";

        // Exposed for modules to use - narrative module adds its own patches
        public static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log { get; private set; }
        public static TechtonicaFrameworkPlugin Instance { get; private set; }

        // Module instances
        public static HealthModule HealthModule { get; private set; }
        public static EnvironmentModule EnvironmentModule { get; private set; }
        public static NarrativeModule NarrativeModule { get; private set; }
        public static EquipmentModule EquipmentModule { get; private set; }

        // Configuration
        public static ConfigEntry<bool> EnableHealthModule;
        public static ConfigEntry<bool> EnableEnvironmentModule;
        public static ConfigEntry<bool> EnableNarrativeModule;
        public static ConfigEntry<bool> EnableEquipmentModule;
        public static ConfigEntry<bool> DebugMode;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginName} v{VersionString} is loading...");

            // Initialize configuration
            InitializeConfig();

            // Apply Harmony patches
            Harmony.PatchAll();

            // Initialize the ModdedTab system (always enabled - required for modded content)
            ModdedTabModule.Initialize(Log);
            Log.LogInfo("ModdedTab system initialized - all mods can use category 7 for unlocks");

            // Initialize the BuildMenu module for modded items tab
            BuildMenuModule.Initialize(Log);
            Log.LogInfo("BuildMenu module initialized - modded items will have their own tab");

            // Initialize modules based on config
            InitializeModules();

            // Register framework events
            FrameworkEvents.Initialize();

            // Initialize corrupted unlock cleanup
            CorruptedUnlockCleanup.Initialize();

            Log.LogInfo($"{PluginName} v{VersionString} loaded successfully!");
        }

        private void InitializeConfig()
        {
            EnableHealthModule = Config.Bind("Modules", "Enable Health Module", true,
                "Enable the Health/Damage system for machines and entities");

            EnableEnvironmentModule = Config.Bind("Modules", "Enable Environment Module", true,
                "Enable environmental hazards and status effects");

            EnableNarrativeModule = Config.Bind("Modules", "Enable Narrative Module", true,
                "Enable dialogue and quest creation helpers");

            EnableEquipmentModule = Config.Bind("Modules", "Enable Equipment Module", true,
                "Enable custom equipment and vehicle systems");

            DebugMode = Config.Bind("General", "Debug Mode", false,
                "Enable verbose debug logging");
        }

        private void InitializeModules()
        {
            if (EnableHealthModule.Value)
            {
                HealthModule = new HealthModule();
                HealthModule.Initialize();
                LogDebug("Health Module initialized");
            }

            if (EnableEnvironmentModule.Value)
            {
                EnvironmentModule = new EnvironmentModule();
                EnvironmentModule.Initialize();
                LogDebug("Environment Module initialized");
            }

            if (EnableNarrativeModule.Value)
            {
                NarrativeModule = new NarrativeModule();
                NarrativeModule.Initialize();
                LogDebug("Narrative Module initialized");
            }

            if (EnableEquipmentModule.Value)
            {
                EquipmentModule = new EquipmentModule();
                EquipmentModule.Initialize();
                LogDebug("Equipment Module initialized");
            }
        }

        private void Update()
        {
            // Update modules that need per-frame processing
            HealthModule?.Update();
            EnvironmentModule?.Update();
            EquipmentModule?.Update();
        }

        private void OnDestroy()
        {
            // Cleanup modules
            HealthModule?.Shutdown();
            EnvironmentModule?.Shutdown();
            NarrativeModule?.Shutdown();
            EquipmentModule?.Shutdown();
        }

        public static void LogDebug(string message)
        {
            if (DebugMode != null && DebugMode.Value)
            {
                Log.LogInfo($"[DEBUG] {message}");
            }
        }

        public static void LogWarning(string message)
        {
            Log.LogWarning(message);
        }

        public static void LogError(string message)
        {
            Log.LogError(message);
        }
    }

    /// <summary>
    /// Patches to suppress known harmless Unity warnings and errors
    /// </summary>
    [HarmonyPatch]
    internal static class LogFilterPatches
    {
        // Known harmless messages to suppress
        private static readonly string[] SuppressedMessages = new string[]
        {
            "Can't handle reading unlock save info for",
            "FMOD Studio Listener",
            "Could not find Audio Clip Entry",
            "_caveSounds_Ambient",
            "Historic ResourceId"
        };

        /// <summary>
        /// Filter Unity Debug.LogWarning calls
        /// </summary>
        [HarmonyPatch(typeof(Debug), nameof(Debug.LogWarning), typeof(object))]
        [HarmonyPrefix]
        private static bool FilterLogWarning(object message)
        {
            if (message == null) return true;
            string msg = message.ToString();
            foreach (var suppressed in SuppressedMessages)
            {
                if (msg.Contains(suppressed)) return false;
            }
            return true;
        }

        /// <summary>
        /// Filter Unity Debug.LogError calls
        /// </summary>
        [HarmonyPatch(typeof(Debug), nameof(Debug.LogError), typeof(object))]
        [HarmonyPrefix]
        private static bool FilterLogError(object message)
        {
            if (message == null) return true;
            string msg = message.ToString();
            foreach (var suppressed in SuppressedMessages)
            {
                if (msg.Contains(suppressed)) return false;
            }
            return true;
        }

        /// <summary>
        /// Filter Unity Debug.LogWarningFormat calls (used by AudioManager)
        /// </summary>
        [HarmonyPatch(typeof(Debug), nameof(Debug.LogWarningFormat), typeof(string), typeof(object[]))]
        [HarmonyPrefix]
        private static bool FilterLogWarningFormat(string format, object[] args)
        {
            if (format == null) return true;
            // Check format string
            foreach (var suppressed in SuppressedMessages)
            {
                if (format.Contains(suppressed)) return false;
            }
            // Check formatted message
            try
            {
                string msg = string.Format(format, args);
                foreach (var suppressed in SuppressedMessages)
                {
                    if (msg.Contains(suppressed)) return false;
                }
            }
            catch { }
            return true;
        }

        /// <summary>
        /// Filter Unity Debug.LogErrorFormat calls
        /// </summary>
        [HarmonyPatch(typeof(Debug), nameof(Debug.LogErrorFormat), typeof(string), typeof(object[]))]
        [HarmonyPrefix]
        private static bool FilterLogErrorFormat(string format, object[] args)
        {
            if (format == null) return true;
            // Check format string
            foreach (var suppressed in SuppressedMessages)
            {
                if (format.Contains(suppressed)) return false;
            }
            // Check formatted message
            try
            {
                string msg = string.Format(format, args);
                foreach (var suppressed in SuppressedMessages)
                {
                    if (msg.Contains(suppressed)) return false;
                }
            }
            catch { }
            return true;
        }
    }

    /// <summary>
    /// Patches to fix FMOD audio listener warning
    /// </summary>
    [HarmonyPatch]
    internal static class FMODListenerFix
    {
        private static bool listenerAdded = false;

        /// <summary>
        /// Add FMOD listener to camera when game loads
        /// </summary>
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.Start))]
        [HarmonyPostfix]
        private static void AddFMODListener()
        {
            if (listenerAdded) return;

            try
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    // Check if listener already exists
                    var existingListener = mainCam.GetComponent<FMODUnity.StudioListener>();
                    if (existingListener == null)
                    {
                        mainCam.gameObject.AddComponent<FMODUnity.StudioListener>();
                        TechtonicaFrameworkPlugin.LogDebug("Added FMOD Studio Listener to main camera");
                    }
                    listenerAdded = true;
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.LogDebug($"Could not add FMOD listener: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Cleanup system for corrupted/invalid unlock entries from save data
    /// </summary>
    internal static class CorruptedUnlockCleanup
    {
        // ONLY exact known corrupted unlock names - be very conservative!
        private static readonly HashSet<string> ExactCorruptedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "LOG%",               // Corrupted data pattern from screenshot
            "Atlantum Smelter",   // Non-existent machine from old saves
            "Atlantum Planter",   // Non-existent machine from old saves
        };

        private static HashSet<string> cleanedUnlocks = new HashSet<string>();

        /// <summary>
        /// Initialize cleanup - call from plugin Awake after EMU events are available
        /// </summary>
        public static void Initialize()
        {
            EMU.Events.GameDefinesLoaded += OnGameDefinesLoaded;
            EMU.Events.TechTreeStateLoaded += OnTechTreeStateLoaded;
        }

        private static void OnGameDefinesLoaded()
        {
            // Reset for new game load
            cleanedUnlocks.Clear();
        }

        private static void OnTechTreeStateLoaded()
        {
            try
            {
                CleanupCorruptedUnlocks();
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"Error during unlock cleanup: {ex.Message}");
            }
        }

        private static void CleanupCorruptedUnlocks()
        {
            if (GameDefines.instance == null) return;

            var allUnlocks = GameDefines.instance.unlocks;
            if (allUnlocks == null) return;

            int removedCount = 0;
            var unlocksToHide = new List<Unlock>();

            foreach (var unlock in allUnlocks)
            {
                if (unlock == null) continue;

                string displayName = unlock.displayName;

                // Check if this unlock should be removed - ONLY exact matches
                if (IsCorruptedUnlock(displayName))
                {
                    unlocksToHide.Add(unlock);

                    if (!cleanedUnlocks.Contains(displayName ?? "null"))
                    {
                        TechtonicaFrameworkPlugin.Log.LogInfo($"Hiding corrupted unlock: '{displayName}'");
                        cleanedUnlocks.Add(displayName ?? "null");
                    }
                    removedCount++;
                }
            }

            // Hide corrupted unlocks by setting tree position off-screen
            // DON'T change requiredTier as that causes errors with valid internal unlocks
            foreach (var unlock in unlocksToHide)
            {
                // Set tree position to something that won't render
                unlock.treePosition = -9999;
            }

            if (removedCount > 0)
            {
                TechtonicaFrameworkPlugin.Log.LogInfo($"Cleaned up {removedCount} corrupted unlock entries from tech tree");
            }
        }

        private static bool IsCorruptedUnlock(string displayName)
        {
            // Skip null/empty - many valid unlocks have empty displayName
            if (string.IsNullOrEmpty(displayName))
                return false;

            // Only match EXACT known corrupted names
            if (ExactCorruptedNames.Contains(displayName))
                return true;

            // Check for LOG% pattern specifically (might have numbers)
            if (displayName.Contains("LOG") && displayName.Contains("%"))
                return true;

            return false;
        }
    }
}
