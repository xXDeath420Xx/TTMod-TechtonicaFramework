using System;
using EquinoxsModUtils;

namespace TechtonicaFramework.Core
{
    /// <summary>
    /// Central event system for the TechtonicaFramework.
    /// Provides hooks for other mods to respond to framework events.
    /// </summary>
    public static class FrameworkEvents
    {
        // Framework lifecycle events
        public static event Action OnFrameworkInitialized;
        public static event Action OnFrameworkShutdown;

        // Health module events
        public static event Action<uint, float, float> OnMachineHealthChanged; // machineId, oldHealth, newHealth
        public static event Action<uint> OnMachineDamaged; // machineId
        public static event Action<uint> OnMachineDestroyed; // machineId
        public static event Action<uint> OnMachineRepaired; // machineId

        // Environment module events
        public static event Action<string, UnityEngine.Vector3> OnHazardZoneEntered; // hazardId, position
        public static event Action<string, UnityEngine.Vector3> OnHazardZoneExited; // hazardId, position
        public static event Action<string, float> OnStatusEffectApplied; // effectId, duration
        public static event Action<string> OnStatusEffectRemoved; // effectId

        // Narrative module events
        public static event Action<string> OnDialogueStarted; // dialogueId
        public static event Action<string> OnDialogueEnded; // dialogueId
        public static event Action<string> OnQuestStarted; // questId
        public static event Action<string> OnQuestCompleted; // questId

        // Equipment module events
        public static event Action<string> OnEquipmentEquipped; // equipmentId
        public static event Action<string> OnEquipmentUnequipped; // equipmentId
        public static event Action<string> OnVehicleEntered; // vehicleId
        public static event Action<string> OnVehicleExited; // vehicleId

        private static bool initialized = false;

        public static void Initialize()
        {
            if (initialized) return;

            // Hook into EMU events to coordinate with game loading
            EMU.Events.GameDefinesLoaded += OnGameDefinesLoaded;
            EMU.Events.GameLoaded += OnGameLoaded;
            EMU.Events.GameUnloaded += OnGameUnloaded;

            initialized = true;
            TechtonicaFrameworkPlugin.LogDebug("FrameworkEvents initialized");
        }

        private static void OnGameDefinesLoaded()
        {
            TechtonicaFrameworkPlugin.LogDebug("Game defines loaded - framework ready");
            OnFrameworkInitialized?.Invoke();
        }

        private static void OnGameLoaded()
        {
            TechtonicaFrameworkPlugin.LogDebug("Game loaded");
        }

        private static void OnGameUnloaded()
        {
            TechtonicaFrameworkPlugin.LogDebug("Game unloaded");
        }

        public static void Shutdown()
        {
            OnFrameworkShutdown?.Invoke();
            initialized = false;
        }

        // Event trigger methods for internal use
        internal static void RaiseMachineHealthChanged(uint machineId, float oldHealth, float newHealth)
            => OnMachineHealthChanged?.Invoke(machineId, oldHealth, newHealth);

        internal static void RaiseMachineDamaged(uint machineId)
            => OnMachineDamaged?.Invoke(machineId);

        internal static void RaiseMachineDestroyed(uint machineId)
            => OnMachineDestroyed?.Invoke(machineId);

        internal static void RaiseMachineRepaired(uint machineId)
            => OnMachineRepaired?.Invoke(machineId);

        internal static void RaiseHazardZoneEntered(string hazardId, UnityEngine.Vector3 position)
            => OnHazardZoneEntered?.Invoke(hazardId, position);

        internal static void RaiseHazardZoneExited(string hazardId, UnityEngine.Vector3 position)
            => OnHazardZoneExited?.Invoke(hazardId, position);

        internal static void RaiseStatusEffectApplied(string effectId, float duration)
            => OnStatusEffectApplied?.Invoke(effectId, duration);

        internal static void RaiseStatusEffectRemoved(string effectId)
            => OnStatusEffectRemoved?.Invoke(effectId);

        internal static void RaiseDialogueStarted(string dialogueId)
            => OnDialogueStarted?.Invoke(dialogueId);

        internal static void RaiseDialogueEnded(string dialogueId)
            => OnDialogueEnded?.Invoke(dialogueId);

        internal static void RaiseQuestStarted(string questId)
            => OnQuestStarted?.Invoke(questId);

        internal static void RaiseQuestCompleted(string questId)
            => OnQuestCompleted?.Invoke(questId);

        internal static void RaiseEquipmentEquipped(string equipmentId)
            => OnEquipmentEquipped?.Invoke(equipmentId);

        internal static void RaiseEquipmentUnequipped(string equipmentId)
            => OnEquipmentUnequipped?.Invoke(equipmentId);

        internal static void RaiseVehicleEntered(string vehicleId)
            => OnVehicleEntered?.Invoke(vehicleId);

        internal static void RaiseVehicleExited(string vehicleId)
            => OnVehicleExited?.Invoke(vehicleId);
    }
}
