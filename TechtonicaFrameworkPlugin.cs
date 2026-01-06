using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using TechtonicaFramework.Core;
using TechtonicaFramework.Health;
using TechtonicaFramework.Environment;
using TechtonicaFramework.Narrative;
using TechtonicaFramework.Equipment;

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
        public const string VersionString = "1.0.2";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
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

            // Initialize modules based on config
            InitializeModules();

            // Register framework events
            FrameworkEvents.Initialize();

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
}
