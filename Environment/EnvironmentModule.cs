using System.Collections.Generic;
using TechtonicaFramework.Core;
using UnityEngine;

namespace TechtonicaFramework.Environment
{
    /// <summary>
    /// Environment Module - Provides hazard zones, status effects, and environmental systems.
    /// </summary>
    public class EnvironmentModule : IFrameworkModule
    {
        public bool IsActive { get; private set; }

        // Active hazard zones
        private Dictionary<string, HazardZone> hazardZones = new Dictionary<string, HazardZone>();

        // Active status effects on player
        private Dictionary<string, StatusEffect> activeEffects = new Dictionary<string, StatusEffect>();

        // Player reference (cached)
        private Transform playerTransform;

        public void Initialize()
        {
            IsActive = true;
            TechtonicaFrameworkPlugin.LogDebug("EnvironmentModule: Initialized");
        }

        public void Update()
        {
            if (!IsActive) return;

            // Cache player transform
            if (playerTransform == null)
            {
                var player = Player.instance;
                if (player != null) playerTransform = player.transform;
            }

            if (playerTransform != null)
            {
                // Check hazard zone interactions
                CheckHazardZones();

                // Process active status effects
                ProcessStatusEffects();
            }
        }

        public void Shutdown()
        {
            IsActive = false;
            hazardZones.Clear();
            activeEffects.Clear();
            TechtonicaFrameworkPlugin.LogDebug("EnvironmentModule: Shutdown");
        }

        #region Hazard Zone Management

        /// <summary>
        /// Create a hazard zone at a position.
        /// </summary>
        public HazardZone CreateHazardZone(string id, Vector3 position, float radius, HazardType type, float damagePerSecond = 5f)
        {
            var zone = new HazardZone
            {
                Id = id,
                Position = position,
                Radius = radius,
                Type = type,
                DamagePerSecond = damagePerSecond,
                IsActive = true,
                PlayerInside = false
            };

            hazardZones[id] = zone;
            TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Created hazard zone '{id}' at {position} with radius {radius}");
            return zone;
        }

        /// <summary>
        /// Remove a hazard zone.
        /// </summary>
        public void RemoveHazardZone(string id)
        {
            if (hazardZones.TryGetValue(id, out var zone))
            {
                if (zone.PlayerInside)
                {
                    FrameworkEvents.RaiseHazardZoneExited(id, zone.Position);
                }
                hazardZones.Remove(id);
                TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Removed hazard zone '{id}'");
            }
        }

        /// <summary>
        /// Get a hazard zone by ID.
        /// </summary>
        public HazardZone GetHazardZone(string id)
        {
            return hazardZones.TryGetValue(id, out var zone) ? zone : null;
        }

        /// <summary>
        /// Enable or disable a hazard zone.
        /// </summary>
        public void SetHazardZoneActive(string id, bool active)
        {
            if (hazardZones.TryGetValue(id, out var zone))
            {
                zone.IsActive = active;
                if (!active && zone.PlayerInside)
                {
                    zone.PlayerInside = false;
                    FrameworkEvents.RaiseHazardZoneExited(id, zone.Position);
                }
            }
        }

        private void CheckHazardZones()
        {
            Vector3 playerPos = playerTransform.position;

            foreach (var kvp in hazardZones)
            {
                var zone = kvp.Value;
                if (!zone.IsActive) continue;

                float distance = Vector3.Distance(playerPos, zone.Position);
                bool isInside = distance <= zone.Radius;

                // Check for zone entry/exit
                if (isInside && !zone.PlayerInside)
                {
                    zone.PlayerInside = true;
                    zone.TimeEntered = Time.time;
                    FrameworkEvents.RaiseHazardZoneEntered(zone.Id, zone.Position);
                    TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Player entered hazard zone '{zone.Id}'");

                    // Apply initial status effect if applicable
                    ApplyHazardEffect(zone);
                }
                else if (!isInside && zone.PlayerInside)
                {
                    zone.PlayerInside = false;
                    FrameworkEvents.RaiseHazardZoneExited(zone.Id, zone.Position);
                    TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Player exited hazard zone '{zone.Id}'");

                    // Remove associated status effect
                    RemoveHazardEffect(zone);
                }

                // Apply continuous damage while inside
                if (zone.PlayerInside && zone.DamagePerSecond > 0)
                {
                    ApplyEnvironmentalDamage(zone.DamagePerSecond * Time.deltaTime, zone.Type);
                }
            }
        }

        private void ApplyHazardEffect(HazardZone zone)
        {
            string effectId = $"hazard_{zone.Id}";

            switch (zone.Type)
            {
                case HazardType.Toxic:
                    ApplyStatusEffect(effectId, StatusEffectType.Poisoned, -1f, 0.1f); // -1 = infinite while in zone
                    break;
                case HazardType.Radiation:
                    ApplyStatusEffect(effectId, StatusEffectType.Irradiated, -1f, 0.2f);
                    break;
                case HazardType.Fire:
                    ApplyStatusEffect(effectId, StatusEffectType.Burning, -1f, 0.5f);
                    break;
                case HazardType.Frost:
                    ApplyStatusEffect(effectId, StatusEffectType.Frozen, -1f, 0f); // Slow, no damage
                    break;
            }
        }

        private void RemoveHazardEffect(HazardZone zone)
        {
            string effectId = $"hazard_{zone.Id}";
            RemoveStatusEffect(effectId);
        }

        private void ApplyEnvironmentalDamage(float damage, HazardType source)
        {
            // TODO: Hook into player health system when implemented
            // For now, log the damage
            TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Environmental damage {damage} from {source}");
        }

        #endregion

        #region Status Effect Management

        /// <summary>
        /// Apply a status effect to the player.
        /// </summary>
        public void ApplyStatusEffect(string id, StatusEffectType type, float duration, float damagePerSecond = 0f)
        {
            var effect = new StatusEffect
            {
                Id = id,
                Type = type,
                Duration = duration,
                TimeApplied = Time.time,
                DamagePerSecond = damagePerSecond,
                IsActive = true
            };

            activeEffects[id] = effect;
            FrameworkEvents.RaiseStatusEffectApplied(id, duration);
            TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Applied status effect '{id}' ({type}) for {duration}s");
        }

        /// <summary>
        /// Remove a status effect.
        /// </summary>
        public void RemoveStatusEffect(string id)
        {
            if (activeEffects.Remove(id))
            {
                FrameworkEvents.RaiseStatusEffectRemoved(id);
                TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Removed status effect '{id}'");
            }
        }

        /// <summary>
        /// Check if player has a specific status effect.
        /// </summary>
        public bool HasStatusEffect(string id)
        {
            return activeEffects.ContainsKey(id);
        }

        /// <summary>
        /// Check if player has any effect of a specific type.
        /// </summary>
        public bool HasStatusEffectType(StatusEffectType type)
        {
            foreach (var effect in activeEffects.Values)
            {
                if (effect.Type == type && effect.IsActive) return true;
            }
            return false;
        }

        /// <summary>
        /// Clear all status effects.
        /// </summary>
        public void ClearAllStatusEffects()
        {
            foreach (var id in new List<string>(activeEffects.Keys))
            {
                RemoveStatusEffect(id);
            }
        }

        private void ProcessStatusEffects()
        {
            var toRemove = new List<string>();
            float currentTime = Time.time;

            foreach (var kvp in activeEffects)
            {
                var effect = kvp.Value;
                if (!effect.IsActive) continue;

                // Check duration (skip if infinite = -1)
                if (effect.Duration > 0)
                {
                    float elapsed = currentTime - effect.TimeApplied;
                    if (elapsed >= effect.Duration)
                    {
                        toRemove.Add(kvp.Key);
                        continue;
                    }
                }

                // Apply damage over time
                if (effect.DamagePerSecond > 0)
                {
                    // TODO: Apply to player health
                    TechtonicaFrameworkPlugin.LogDebug($"EnvironmentModule: Status effect '{effect.Id}' dealing {effect.DamagePerSecond * Time.deltaTime} damage");
                }

                // Apply movement modifiers
                ApplyEffectModifiers(effect);
            }

            // Remove expired effects
            foreach (var id in toRemove)
            {
                RemoveStatusEffect(id);
            }
        }

        private void ApplyEffectModifiers(StatusEffect effect)
        {
            // TODO: Hook into player movement/stats
            // This would apply slow, speed boost, etc.
            switch (effect.Type)
            {
                case StatusEffectType.Frozen:
                    // Slow player movement
                    break;
                case StatusEffectType.Haste:
                    // Speed up player
                    break;
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Get all active hazard zones.
        /// </summary>
        public List<HazardZone> GetAllHazardZones()
        {
            return new List<HazardZone>(hazardZones.Values);
        }

        /// <summary>
        /// Get all active status effects.
        /// </summary>
        public List<StatusEffect> GetAllStatusEffects()
        {
            return new List<StatusEffect>(activeEffects.Values);
        }

        /// <summary>
        /// Check if position is inside any hazard zone.
        /// </summary>
        public bool IsPositionInHazard(Vector3 position, out HazardZone zone)
        {
            foreach (var kvp in hazardZones)
            {
                var z = kvp.Value;
                if (z.IsActive && Vector3.Distance(position, z.Position) <= z.Radius)
                {
                    zone = z;
                    return true;
                }
            }
            zone = null;
            return false;
        }

        #endregion
    }

    /// <summary>
    /// Data for a hazard zone.
    /// </summary>
    public class HazardZone
    {
        public string Id;
        public Vector3 Position;
        public float Radius;
        public HazardType Type;
        public float DamagePerSecond;
        public bool IsActive;
        public bool PlayerInside;
        public float TimeEntered;
    }

    /// <summary>
    /// Data for a status effect.
    /// </summary>
    public class StatusEffect
    {
        public string Id;
        public StatusEffectType Type;
        public float Duration; // -1 = infinite
        public float TimeApplied;
        public float DamagePerSecond;
        public bool IsActive;
    }

    /// <summary>
    /// Types of environmental hazards.
    /// </summary>
    public enum HazardType
    {
        Generic,
        Toxic,
        Radiation,
        Fire,
        Frost,
        Electric,
        Corrosive
    }

    /// <summary>
    /// Types of status effects.
    /// </summary>
    public enum StatusEffectType
    {
        None,
        Poisoned,
        Irradiated,
        Burning,
        Frozen,
        Electrified,
        Corroded,
        Haste,
        Slow,
        Regenerating
    }
}
