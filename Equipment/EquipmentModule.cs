using System;
using System.Collections.Generic;
using TechtonicaFramework.Core;
using UnityEngine;

namespace TechtonicaFramework.Equipment
{
    /// <summary>
    /// Equipment Module - Provides custom equipment, vehicle controllers, and movement modifiers.
    /// </summary>
    public class EquipmentModule : IFrameworkModule
    {
        public bool IsActive { get; private set; }

        // Registered custom equipment types
        private Dictionary<string, CustomEquipmentDefinition> equipmentDefinitions = new Dictionary<string, CustomEquipmentDefinition>();

        // Active movement modifiers
        private Dictionary<string, MovementModifier> activeModifiers = new Dictionary<string, MovementModifier>();

        // Base movement values (cached from game)
        private float baseWalkSpeed = 5f;
        private float baseRunSpeed = 10f;
        private float baseJumpHeight = 2f;

        // Current modified values
        public float CurrentWalkSpeed { get; private set; }
        public float CurrentRunSpeed { get; private set; }
        public float CurrentJumpHeight { get; private set; }

        public void Initialize()
        {
            IsActive = true;

            // Cache base movement values from game
            CacheBaseMovementValues();

            TechtonicaFrameworkPlugin.LogDebug("EquipmentModule: Initialized");
        }

        public void Update()
        {
            if (!IsActive) return;

            // Process movement modifiers
            ProcessMovementModifiers();
        }

        public void Shutdown()
        {
            IsActive = false;
            equipmentDefinitions.Clear();
            activeModifiers.Clear();
            TechtonicaFrameworkPlugin.LogDebug("EquipmentModule: Shutdown");
        }

        #region Base Movement

        private void CacheBaseMovementValues()
        {
            try
            {
                // TODO: Get actual values from game's movement system
                // These are placeholder defaults
                CurrentWalkSpeed = baseWalkSpeed;
                CurrentRunSpeed = baseRunSpeed;
                CurrentJumpHeight = baseJumpHeight;
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Could not cache movement values: {ex.Message}");
            }
        }

        #endregion

        #region Custom Equipment Registration

        /// <summary>
        /// Register a custom equipment type definition.
        /// </summary>
        public CustomEquipmentDefinition RegisterEquipment(
            string id,
            string displayName,
            string description,
            EquipmentSlot slot,
            Action<Player> onEquip = null,
            Action<Player> onUnequip = null,
            Action<Player> onUse = null)
        {
            var definition = new CustomEquipmentDefinition
            {
                Id = id,
                DisplayName = displayName,
                Description = description,
                Slot = slot,
                OnEquip = onEquip,
                OnUnequip = onUnequip,
                OnUse = onUse
            };

            equipmentDefinitions[id] = definition;
            TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Registered equipment '{id}' ({displayName})");
            return definition;
        }

        /// <summary>
        /// Get a registered equipment definition.
        /// </summary>
        public CustomEquipmentDefinition GetEquipmentDefinition(string id)
        {
            return equipmentDefinitions.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>
        /// Get all registered equipment definitions.
        /// </summary>
        public List<CustomEquipmentDefinition> GetAllEquipmentDefinitions()
        {
            return new List<CustomEquipmentDefinition>(equipmentDefinitions.Values);
        }

        /// <summary>
        /// Simulate equipping custom equipment.
        /// </summary>
        public void EquipCustomEquipment(string id)
        {
            if (!equipmentDefinitions.TryGetValue(id, out var def))
            {
                TechtonicaFrameworkPlugin.LogWarning($"EquipmentModule: Equipment '{id}' not found");
                return;
            }

            var player = Player.instance;
            if (player == null)
            {
                TechtonicaFrameworkPlugin.LogWarning("EquipmentModule: No player instance");
                return;
            }

            def.OnEquip?.Invoke(player);
            FrameworkEvents.RaiseEquipmentEquipped(id);
            TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Equipped '{id}'");
        }

        /// <summary>
        /// Simulate unequipping custom equipment.
        /// </summary>
        public void UnequipCustomEquipment(string id)
        {
            if (!equipmentDefinitions.TryGetValue(id, out var def))
            {
                return;
            }

            var player = Player.instance;
            if (player == null) return;

            def.OnUnequip?.Invoke(player);
            FrameworkEvents.RaiseEquipmentUnequipped(id);
            TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Unequipped '{id}'");
        }

        /// <summary>
        /// Use custom equipment (trigger ability).
        /// </summary>
        public void UseCustomEquipment(string id)
        {
            if (!equipmentDefinitions.TryGetValue(id, out var def))
            {
                return;
            }

            var player = Player.instance;
            if (player == null) return;

            def.OnUse?.Invoke(player);
            TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Used '{id}'");
        }

        #endregion

        #region Movement Modifiers

        /// <summary>
        /// Apply a movement modifier.
        /// </summary>
        public void ApplyMovementModifier(string id, float speedMultiplier = 1f, float jumpMultiplier = 1f, float duration = -1f)
        {
            var modifier = new MovementModifier
            {
                Id = id,
                SpeedMultiplier = speedMultiplier,
                JumpMultiplier = jumpMultiplier,
                Duration = duration,
                TimeApplied = Time.time,
                IsActive = true
            };

            activeModifiers[id] = modifier;
            RecalculateMovement();
            TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Applied movement modifier '{id}' (speed: {speedMultiplier}x, jump: {jumpMultiplier}x)");
        }

        /// <summary>
        /// Remove a movement modifier.
        /// </summary>
        public void RemoveMovementModifier(string id)
        {
            if (activeModifiers.Remove(id))
            {
                RecalculateMovement();
                TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Removed movement modifier '{id}'");
            }
        }

        /// <summary>
        /// Check if a movement modifier is active.
        /// </summary>
        public bool HasMovementModifier(string id)
        {
            return activeModifiers.ContainsKey(id);
        }

        /// <summary>
        /// Clear all movement modifiers.
        /// </summary>
        public void ClearAllModifiers()
        {
            activeModifiers.Clear();
            RecalculateMovement();
        }

        private void ProcessMovementModifiers()
        {
            var toRemove = new List<string>();
            float currentTime = Time.time;

            foreach (var kvp in activeModifiers)
            {
                var mod = kvp.Value;

                // Check duration (skip if infinite = -1)
                if (mod.Duration > 0)
                {
                    float elapsed = currentTime - mod.TimeApplied;
                    if (elapsed >= mod.Duration)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            // Remove expired modifiers
            if (toRemove.Count > 0)
            {
                foreach (var id in toRemove)
                {
                    activeModifiers.Remove(id);
                }
                RecalculateMovement();
            }
        }

        private void RecalculateMovement()
        {
            // Start with base values
            float speedMult = 1f;
            float jumpMult = 1f;

            // Apply all active modifiers (multiplicative)
            foreach (var mod in activeModifiers.Values)
            {
                if (mod.IsActive)
                {
                    speedMult *= mod.SpeedMultiplier;
                    jumpMult *= mod.JumpMultiplier;
                }
            }

            // Calculate final values
            CurrentWalkSpeed = baseWalkSpeed * speedMult;
            CurrentRunSpeed = baseRunSpeed * speedMult;
            CurrentJumpHeight = baseJumpHeight * jumpMult;

            // TODO: Apply to actual player movement system
            // This would require Harmony patching the player movement controller
        }

        #endregion

        #region Vehicle System

        /// <summary>
        /// Register a vehicle type.
        /// </summary>
        public VehicleDefinition RegisterVehicle(
            string id,
            string displayName,
            float maxSpeed,
            Action<Player> onEnter = null,
            Action<Player> onExit = null)
        {
            var vehicle = new VehicleDefinition
            {
                Id = id,
                DisplayName = displayName,
                MaxSpeed = maxSpeed,
                OnEnter = onEnter,
                OnExit = onExit
            };

            // Store as special equipment type
            var equipDef = RegisterEquipment(
                id,
                displayName,
                $"Vehicle: {displayName}",
                EquipmentSlot.Vehicle,
                (p) => { vehicle.OnEnter?.Invoke(p); FrameworkEvents.RaiseVehicleEntered(id); },
                (p) => { vehicle.OnExit?.Invoke(p); FrameworkEvents.RaiseVehicleExited(id); }
            );

            TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Registered vehicle '{id}' ({displayName})");
            return vehicle;
        }

        #endregion

        #region Speed Zone Support

        /// <summary>
        /// Create a speed boost zone.
        /// </summary>
        public void CreateSpeedZone(string id, Vector3 position, float radius, float speedMultiplier)
        {
            // Use Environment module if available
            var envModule = TechtonicaFrameworkPlugin.EnvironmentModule;
            if (envModule != null)
            {
                // Create a non-damaging zone that applies speed modifier
                envModule.CreateHazardZone(id, position, radius, Environment.HazardType.Generic, 0f);

                // Hook zone events to apply/remove speed modifier
                FrameworkEvents.OnHazardZoneEntered += (zoneId, pos) =>
                {
                    if (zoneId == id) ApplyMovementModifier($"speedzone_{id}", speedMultiplier, 1f, -1f);
                };
                FrameworkEvents.OnHazardZoneExited += (zoneId, pos) =>
                {
                    if (zoneId == id) RemoveMovementModifier($"speedzone_{id}");
                };

                TechtonicaFrameworkPlugin.LogDebug($"EquipmentModule: Created speed zone '{id}' at {position} ({speedMultiplier}x speed)");
            }
        }

        #endregion
    }

    /// <summary>
    /// Custom equipment definition.
    /// </summary>
    public class CustomEquipmentDefinition
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public EquipmentSlot Slot;
        public Sprite Icon;
        public Action<Player> OnEquip;
        public Action<Player> OnUnequip;
        public Action<Player> OnUse;
    }

    /// <summary>
    /// Movement modifier data.
    /// </summary>
    public class MovementModifier
    {
        public string Id;
        public float SpeedMultiplier;
        public float JumpMultiplier;
        public float Duration; // -1 = infinite
        public float TimeApplied;
        public bool IsActive;
    }

    /// <summary>
    /// Vehicle definition.
    /// </summary>
    public class VehicleDefinition
    {
        public string Id;
        public string DisplayName;
        public float MaxSpeed;
        public Action<Player> OnEnter;
        public Action<Player> OnExit;
    }

    /// <summary>
    /// Equipment slot types.
    /// </summary>
    public enum EquipmentSlot
    {
        Hand,
        Head,
        Body,
        Feet,
        Accessory,
        Vehicle
    }
}
