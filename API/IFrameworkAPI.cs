using System;
using System.Collections.Generic;
using TechtonicaFramework.Health;
using TechtonicaFramework.Environment;
using TechtonicaFramework.Narrative;
using TechtonicaFramework.Equipment;
using UnityEngine;

namespace TechtonicaFramework.API
{
    /// <summary>
    /// Main API interface for TechtonicaFramework.
    /// Use this to interact with the framework from other mods.
    /// </summary>
    public static class FrameworkAPI
    {
        #region Health API

        /// <summary>
        /// Register a machine to have health tracking.
        /// </summary>
        public static void RegisterMachineHealth(uint machineId, float maxHealth = 100f)
        {
            TechtonicaFrameworkPlugin.HealthModule?.RegisterMachine(machineId, maxHealth);
        }

        /// <summary>
        /// Apply damage to a machine.
        /// </summary>
        public static void DamageMachine(uint machineId, float damage, DamageType type = DamageType.Generic)
        {
            TechtonicaFrameworkPlugin.HealthModule?.DamageMachine(machineId, damage, type);
        }

        /// <summary>
        /// Heal a machine.
        /// </summary>
        public static void HealMachine(uint machineId, float amount)
        {
            TechtonicaFrameworkPlugin.HealthModule?.HealMachine(machineId, amount);
        }

        /// <summary>
        /// Get machine health as percentage (0-1).
        /// </summary>
        public static float GetMachineHealthPercent(uint machineId)
        {
            return TechtonicaFrameworkPlugin.HealthModule?.GetHealthPercent(machineId) ?? 1f;
        }

        /// <summary>
        /// Check if machine is alive.
        /// </summary>
        public static bool IsMachineAlive(uint machineId)
        {
            return TechtonicaFrameworkPlugin.HealthModule?.IsMachineAlive(machineId) ?? true;
        }

        /// <summary>
        /// Start repairing a machine.
        /// </summary>
        public static void StartMachineRepair(uint machineId)
        {
            TechtonicaFrameworkPlugin.HealthModule?.StartRepair(machineId);
        }

        /// <summary>
        /// Stop repairing a machine.
        /// </summary>
        public static void StopMachineRepair(uint machineId)
        {
            TechtonicaFrameworkPlugin.HealthModule?.StopRepair(machineId);
        }

        /// <summary>
        /// Fully repair a machine instantly.
        /// </summary>
        public static void FullRepairMachine(uint machineId)
        {
            TechtonicaFrameworkPlugin.HealthModule?.FullRepair(machineId);
        }

        /// <summary>
        /// Get all damaged machines below a health threshold.
        /// </summary>
        public static List<uint> GetDamagedMachines(float threshold = 1f)
        {
            return TechtonicaFrameworkPlugin.HealthModule?.GetDamagedMachines(threshold) ?? new List<uint>();
        }

        #endregion

        #region Environment API

        /// <summary>
        /// Create a hazard zone.
        /// </summary>
        public static void CreateHazardZone(string id, Vector3 position, float radius, HazardType type, float damagePerSecond = 5f)
        {
            TechtonicaFrameworkPlugin.EnvironmentModule?.CreateHazardZone(id, position, radius, type, damagePerSecond);
        }

        /// <summary>
        /// Remove a hazard zone.
        /// </summary>
        public static void RemoveHazardZone(string id)
        {
            TechtonicaFrameworkPlugin.EnvironmentModule?.RemoveHazardZone(id);
        }

        /// <summary>
        /// Apply a status effect to the player.
        /// </summary>
        public static void ApplyStatusEffect(string id, StatusEffectType type, float duration, float damagePerSecond = 0f)
        {
            TechtonicaFrameworkPlugin.EnvironmentModule?.ApplyStatusEffect(id, type, duration, damagePerSecond);
        }

        /// <summary>
        /// Remove a status effect.
        /// </summary>
        public static void RemoveStatusEffect(string id)
        {
            TechtonicaFrameworkPlugin.EnvironmentModule?.RemoveStatusEffect(id);
        }

        /// <summary>
        /// Check if player has a status effect.
        /// </summary>
        public static bool HasStatusEffect(string id)
        {
            return TechtonicaFrameworkPlugin.EnvironmentModule?.HasStatusEffect(id) ?? false;
        }

        /// <summary>
        /// Clear all status effects.
        /// </summary>
        public static void ClearAllStatusEffects()
        {
            TechtonicaFrameworkPlugin.EnvironmentModule?.ClearAllStatusEffects();
        }

        #endregion

        #region Narrative API

        /// <summary>
        /// Register a custom speaker.
        /// </summary>
        public static void RegisterSpeaker(string id, string displayName, Color textColor, Sprite portrait = null)
        {
            TechtonicaFrameworkPlugin.NarrativeModule?.RegisterSpeaker(id, displayName, textColor, portrait);
        }

        /// <summary>
        /// Register a dialogue entry.
        /// </summary>
        public static void RegisterDialogue(string id, string speakerId, string text, float duration = 5f)
        {
            TechtonicaFrameworkPlugin.NarrativeModule?.RegisterDialogue(id, speakerId, text, duration);
        }

        /// <summary>
        /// Chain dialogues together.
        /// </summary>
        public static void ChainDialogue(string dialogueId, string nextDialogueId)
        {
            TechtonicaFrameworkPlugin.NarrativeModule?.ChainDialogue(dialogueId, nextDialogueId);
        }

        /// <summary>
        /// Trigger a dialogue to play.
        /// </summary>
        public static void TriggerDialogue(string dialogueId)
        {
            TechtonicaFrameworkPlugin.NarrativeModule?.TriggerDialogue(dialogueId);
        }

        /// <summary>
        /// Check if a dialogue has played.
        /// </summary>
        public static bool HasDialoguePlayed(string dialogueId)
        {
            return TechtonicaFrameworkPlugin.NarrativeModule?.HasDialoguePlayed(dialogueId) ?? false;
        }

        /// <summary>
        /// Create a dialogue sequence builder.
        /// </summary>
        public static DialogueSequenceBuilder CreateDialogueSequence(string sequenceId)
        {
            return TechtonicaFrameworkPlugin.NarrativeModule?.CreateSequence(sequenceId);
        }

        #endregion

        #region Equipment API

        /// <summary>
        /// Register custom equipment.
        /// </summary>
        public static void RegisterEquipment(
            string id,
            string displayName,
            string description,
            EquipmentSlot slot,
            Action<Player> onEquip = null,
            Action<Player> onUnequip = null,
            Action<Player> onUse = null)
        {
            TechtonicaFrameworkPlugin.EquipmentModule?.RegisterEquipment(id, displayName, description, slot, onEquip, onUnequip, onUse);
        }

        /// <summary>
        /// Apply a movement modifier.
        /// </summary>
        public static void ApplyMovementModifier(string id, float speedMultiplier = 1f, float jumpMultiplier = 1f, float duration = -1f)
        {
            TechtonicaFrameworkPlugin.EquipmentModule?.ApplyMovementModifier(id, speedMultiplier, jumpMultiplier, duration);
        }

        /// <summary>
        /// Remove a movement modifier.
        /// </summary>
        public static void RemoveMovementModifier(string id)
        {
            TechtonicaFrameworkPlugin.EquipmentModule?.RemoveMovementModifier(id);
        }

        /// <summary>
        /// Create a speed boost zone.
        /// </summary>
        public static void CreateSpeedZone(string id, Vector3 position, float radius, float speedMultiplier)
        {
            TechtonicaFrameworkPlugin.EquipmentModule?.CreateSpeedZone(id, position, radius, speedMultiplier);
        }

        /// <summary>
        /// Register a vehicle.
        /// </summary>
        public static void RegisterVehicle(
            string id,
            string displayName,
            float maxSpeed,
            Action<Player> onEnter = null,
            Action<Player> onExit = null)
        {
            TechtonicaFrameworkPlugin.EquipmentModule?.RegisterVehicle(id, displayName, maxSpeed, onEnter, onExit);
        }

        #endregion

        #region Framework Info

        /// <summary>
        /// Check if the framework is loaded and ready.
        /// </summary>
        public static bool IsFrameworkReady()
        {
            return TechtonicaFrameworkPlugin.Instance != null;
        }

        /// <summary>
        /// Get framework version.
        /// </summary>
        public static string GetFrameworkVersion()
        {
            return TechtonicaFrameworkPlugin.VersionString;
        }

        /// <summary>
        /// Check if a specific module is enabled.
        /// </summary>
        public static bool IsModuleEnabled(string moduleName)
        {
            return moduleName.ToLower() switch
            {
                "health" => TechtonicaFrameworkPlugin.HealthModule?.IsActive ?? false,
                "environment" => TechtonicaFrameworkPlugin.EnvironmentModule?.IsActive ?? false,
                "narrative" => TechtonicaFrameworkPlugin.NarrativeModule?.IsActive ?? false,
                "equipment" => TechtonicaFrameworkPlugin.EquipmentModule?.IsActive ?? false,
                _ => false
            };
        }

        #endregion
    }
}
