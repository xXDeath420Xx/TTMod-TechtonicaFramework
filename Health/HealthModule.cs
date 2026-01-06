using System.Collections.Generic;
using TechtonicaFramework.Core;
using UnityEngine;

namespace TechtonicaFramework.Health
{
    /// <summary>
    /// Health Module - Provides health, damage, and repair systems for machines and entities.
    /// </summary>
    public class HealthModule : IFrameworkModule
    {
        public bool IsActive { get; private set; }

        // Track health data for machines
        private Dictionary<uint, MachineHealthData> machineHealthRegistry = new Dictionary<uint, MachineHealthData>();

        // Configuration
        public float DefaultMachineHealth { get; set; } = 100f;
        public float DefaultRepairRate { get; set; } = 10f; // HP per second when repairing

        public void Initialize()
        {
            IsActive = true;
            TechtonicaFrameworkPlugin.LogDebug("HealthModule: Initialized");
        }

        public void Update()
        {
            if (!IsActive) return;

            // Process any pending health updates
            ProcessHealthUpdates();
        }

        public void Shutdown()
        {
            IsActive = false;
            machineHealthRegistry.Clear();
            TechtonicaFrameworkPlugin.LogDebug("HealthModule: Shutdown");
        }

        #region Machine Health Management

        /// <summary>
        /// Register a machine to have health tracking.
        /// </summary>
        public void RegisterMachine(uint machineId, float maxHealth = 0f)
        {
            if (maxHealth <= 0f) maxHealth = DefaultMachineHealth;

            if (!machineHealthRegistry.ContainsKey(machineId))
            {
                machineHealthRegistry[machineId] = new MachineHealthData
                {
                    MachineId = machineId,
                    MaxHealth = maxHealth,
                    CurrentHealth = maxHealth,
                    IsDestroyed = false,
                    IsBeingRepaired = false
                };
                TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Registered machine {machineId} with {maxHealth} HP");
            }
        }

        /// <summary>
        /// Unregister a machine from health tracking.
        /// </summary>
        public void UnregisterMachine(uint machineId)
        {
            machineHealthRegistry.Remove(machineId);
        }

        /// <summary>
        /// Get health data for a machine.
        /// </summary>
        public MachineHealthData GetMachineHealth(uint machineId)
        {
            return machineHealthRegistry.TryGetValue(machineId, out var data) ? data : null;
        }

        /// <summary>
        /// Apply damage to a machine.
        /// </summary>
        public void DamageMachine(uint machineId, float damage, DamageType damageType = DamageType.Generic)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return;
            if (data.IsDestroyed) return;

            float oldHealth = data.CurrentHealth;
            data.CurrentHealth = Mathf.Max(0f, data.CurrentHealth - damage);
            data.LastDamageType = damageType;
            data.LastDamageTime = Time.time;

            FrameworkEvents.RaiseMachineHealthChanged(machineId, oldHealth, data.CurrentHealth);
            FrameworkEvents.RaiseMachineDamaged(machineId);

            TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Machine {machineId} took {damage} damage ({damageType}), HP: {data.CurrentHealth}/{data.MaxHealth}");

            if (data.CurrentHealth <= 0f)
            {
                DestroyMachine(machineId);
            }
        }

        /// <summary>
        /// Heal a machine.
        /// </summary>
        public void HealMachine(uint machineId, float amount)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return;
            if (data.IsDestroyed) return;

            float oldHealth = data.CurrentHealth;
            data.CurrentHealth = Mathf.Min(data.MaxHealth, data.CurrentHealth + amount);

            if (data.CurrentHealth > oldHealth)
            {
                FrameworkEvents.RaiseMachineHealthChanged(machineId, oldHealth, data.CurrentHealth);
                TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Machine {machineId} healed {amount}, HP: {data.CurrentHealth}/{data.MaxHealth}");
            }
        }

        /// <summary>
        /// Start repairing a machine.
        /// </summary>
        public void StartRepair(uint machineId)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return;
            if (data.IsDestroyed || data.CurrentHealth >= data.MaxHealth) return;

            data.IsBeingRepaired = true;
            TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Started repairing machine {machineId}");
        }

        /// <summary>
        /// Stop repairing a machine.
        /// </summary>
        public void StopRepair(uint machineId)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return;

            data.IsBeingRepaired = false;
            TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Stopped repairing machine {machineId}");
        }

        /// <summary>
        /// Fully repair a machine instantly.
        /// </summary>
        public void FullRepair(uint machineId)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return;
            if (data.IsDestroyed) return;

            float oldHealth = data.CurrentHealth;
            data.CurrentHealth = data.MaxHealth;
            data.IsBeingRepaired = false;

            FrameworkEvents.RaiseMachineHealthChanged(machineId, oldHealth, data.CurrentHealth);
            FrameworkEvents.RaiseMachineRepaired(machineId);
            TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Machine {machineId} fully repaired");
        }

        /// <summary>
        /// Destroy a machine (health reached 0).
        /// </summary>
        private void DestroyMachine(uint machineId)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return;

            data.IsDestroyed = true;
            data.IsBeingRepaired = false;

            FrameworkEvents.RaiseMachineDestroyed(machineId);
            TechtonicaFrameworkPlugin.LogDebug($"HealthModule: Machine {machineId} destroyed!");

            // TODO: Trigger visual destruction effects
            // TODO: Optionally remove machine from world
        }

        /// <summary>
        /// Check if a machine is alive (has health and not destroyed).
        /// </summary>
        public bool IsMachineAlive(uint machineId)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return false;
            return !data.IsDestroyed && data.CurrentHealth > 0f;
        }

        /// <summary>
        /// Get health percentage (0-1) for a machine.
        /// </summary>
        public float GetHealthPercent(uint machineId)
        {
            if (!machineHealthRegistry.TryGetValue(machineId, out var data)) return 1f;
            if (data.MaxHealth <= 0f) return 1f;
            return data.CurrentHealth / data.MaxHealth;
        }

        #endregion

        #region Internal Processing

        private void ProcessHealthUpdates()
        {
            float deltaTime = Time.deltaTime;

            foreach (var kvp in machineHealthRegistry)
            {
                var data = kvp.Value;

                // Process repairs
                if (data.IsBeingRepaired && !data.IsDestroyed && data.CurrentHealth < data.MaxHealth)
                {
                    HealMachine(kvp.Key, DefaultRepairRate * deltaTime);

                    if (data.CurrentHealth >= data.MaxHealth)
                    {
                        data.IsBeingRepaired = false;
                        FrameworkEvents.RaiseMachineRepaired(kvp.Key);
                    }
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get all machines with health below a percentage threshold.
        /// </summary>
        public List<uint> GetDamagedMachines(float healthThreshold = 1f)
        {
            var damaged = new List<uint>();
            foreach (var kvp in machineHealthRegistry)
            {
                if (!kvp.Value.IsDestroyed && GetHealthPercent(kvp.Key) < healthThreshold)
                {
                    damaged.Add(kvp.Key);
                }
            }
            return damaged;
        }

        /// <summary>
        /// Get count of all tracked machines.
        /// </summary>
        public int GetTrackedMachineCount() => machineHealthRegistry.Count;

        #endregion
    }

    /// <summary>
    /// Health data for a tracked machine.
    /// </summary>
    public class MachineHealthData
    {
        public uint MachineId;
        public float MaxHealth;
        public float CurrentHealth;
        public bool IsDestroyed;
        public bool IsBeingRepaired;
        public DamageType LastDamageType;
        public float LastDamageTime;
    }

    /// <summary>
    /// Types of damage that can be applied.
    /// </summary>
    public enum DamageType
    {
        Generic,
        Environmental,
        PowerSurge,
        Overload,
        Explosion,
        Corrosion,
        Radiation,
        Fire,
        Frost
    }
}
