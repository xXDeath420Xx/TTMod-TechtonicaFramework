using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TechtonicaFramework.Compatibility
{
    /// <summary>
    /// Extends the vanilla Replacer Tool to support modded buildings.
    /// Automatically adds modded machine variants to appropriate ReplaceGroups
    /// so they can be upgraded/downgraded like vanilla machines.
    /// </summary>
    public static class ReplacerToolExtensions
    {
        private static bool _initialized = false;

        // Track all ReplaceGroups we've modified
        private static Dictionary<string, ReplaceGroup> _modifiedGroups = new Dictionary<string, ReplaceGroup>();

        // Track machines we've registered
        private static List<(int machineId, string groupName)> _registeredMachines = new List<(int, string)>();

        /// <summary>
        /// Initialize the replacer tool extensions after GameDefines is loaded
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            TechtonicaFrameworkPlugin.Log.LogInfo("ReplacerToolExtensions: Initializing...");

            // We need to wait for GameDefines to be ready
            // This is called from the main plugin after game data loads
            _initialized = true;
        }

        /// <summary>
        /// Register a modded machine to be included in the replacer tool.
        /// Call this after adding a machine via EMUAdditions.
        /// </summary>
        /// <param name="moddedMachineId">The uniqueId of the modded machine</param>
        /// <param name="baseMachineName">Name of a vanilla machine in the same replace group (e.g., "Smelter")</param>
        /// <param name="variationIndex">Variation index for the modded machine (default 0)</param>
        public static void RegisterModdedMachine(int moddedMachineId, string baseMachineName, int variationIndex = 0)
        {
            try
            {
                // Store for later processing
                _registeredMachines.Add((moddedMachineId, baseMachineName));
                TechtonicaFrameworkPlugin.LogDebug($"Queued modded machine {moddedMachineId} for replacer group '{baseMachineName}'");
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"Failed to queue modded machine for replacer: {ex.Message}");
            }
        }

        /// <summary>
        /// Process all queued machine registrations.
        /// Call this after GameDefines is fully loaded.
        /// </summary>
        public static void ProcessQueuedRegistrations()
        {
            if (GameDefines.instance == null)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning("Cannot process replacer registrations - GameDefines not ready");
                return;
            }

            int processed = 0;
            foreach (var (machineId, groupName) in _registeredMachines)
            {
                if (TryAddToReplaceGroup(machineId, groupName))
                {
                    processed++;
                }
            }

            if (processed > 0)
            {
                TechtonicaFrameworkPlugin.Log.LogInfo($"ReplacerToolExtensions: Added {processed} modded machines to replace groups");
            }
        }

        /// <summary>
        /// Add a modded machine to the same ReplaceGroup as a vanilla machine
        /// </summary>
        private static bool TryAddToReplaceGroup(int moddedMachineId, string baseMachineName)
        {
            try
            {
                // Find the modded machine's BuilderInfo
                BuilderInfo moddedBuilder = FindBuilderInfoById(moddedMachineId);
                if (moddedBuilder == null)
                {
                    TechtonicaFrameworkPlugin.LogDebug($"Could not find BuilderInfo for machine ID {moddedMachineId}");
                    return false;
                }

                // Find a vanilla machine with this name to get its ReplaceGroup
                BuilderInfo baseBuilder = FindBuilderInfoByName(baseMachineName);
                if (baseBuilder == null)
                {
                    TechtonicaFrameworkPlugin.LogDebug($"Could not find base machine '{baseMachineName}'");
                    return false;
                }

                ReplaceGroup baseGroup = baseBuilder.replaceGroup;
                if (baseGroup == null)
                {
                    TechtonicaFrameworkPlugin.LogDebug($"Base machine '{baseMachineName}' has no ReplaceGroup");
                    return false;
                }

                // Assign the same ReplaceGroup to the modded machine
                moddedBuilder.replaceGroup = baseGroup;

                // Try to add the modded machine as a replacement option
                AddToReplaceGroupOptions(baseGroup, moddedBuilder);

                TechtonicaFrameworkPlugin.Log.LogInfo($"Added '{moddedBuilder.displayName}' to replace group with '{baseMachineName}'");
                return true;
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"Failed to add machine to replace group: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Add a new option to an existing ReplaceGroup
        /// </summary>
        private static void AddToReplaceGroupOptions(ReplaceGroup group, BuilderInfo newMachine)
        {
            try
            {
                if (group is SimpleReplaceGroup simpleGroup)
                {
                    // Create a new option for the modded machine
                    var newOption = new SimpleReplaceGroup.SimpleReplaceOption
                    {
                        SwapInfo = new ReplaceGroup.SwapInfo(newMachine, 0),
                        DisplayName = newMachine.displayName,
                        DisplayIcon = newMachine.sprite
                    };

                    // Expand the options array
                    var existingOptions = simpleGroup.ReplaceOptions;
                    var newOptions = new SimpleReplaceGroup.SimpleReplaceOption[existingOptions.Length + 1];
                    Array.Copy(existingOptions, newOptions, existingOptions.Length);
                    newOptions[existingOptions.Length] = newOption;

                    // Use reflection to set the field (it's public but we need to be safe)
                    typeof(SimpleReplaceGroup).GetField("ReplaceOptions").SetValue(simpleGroup, newOptions);

                    TechtonicaFrameworkPlugin.LogDebug($"Added {newMachine.displayName} as option {existingOptions.Length} to SimpleReplaceGroup");
                }
                else if (group is NestedReplaceGroup nestedGroup)
                {
                    // For nested groups, we need to add to each option's SwapInfo array
                    TechtonicaFrameworkPlugin.LogDebug("NestedReplaceGroup detected - complex update required");
                    // This is more complex and may need per-case handling
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"Could not expand replace group options: {ex.Message}");
            }
        }

        /// <summary>
        /// Find a BuilderInfo by its uniqueId
        /// </summary>
        private static BuilderInfo FindBuilderInfoById(int uniqueId)
        {
            if (GameDefines.instance?.resources == null) return null;

            foreach (var resource in GameDefines.instance.resources)
            {
                if (resource is BuilderInfo builder && builder.uniqueId == uniqueId)
                {
                    return builder;
                }
            }
            return null;
        }

        /// <summary>
        /// Find a BuilderInfo by name (partial match)
        /// </summary>
        private static BuilderInfo FindBuilderInfoByName(string name)
        {
            if (GameDefines.instance?.resources == null) return null;

            string lowerName = name.ToLower();
            foreach (var resource in GameDefines.instance.resources)
            {
                if (resource is BuilderInfo builder)
                {
                    if (builder.name.ToLower().Contains(lowerName) ||
                        (builder.displayName != null && builder.displayName.ToLower().Contains(lowerName)))
                    {
                        return builder;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Create a new ReplaceGroup for machines that don't have one
        /// </summary>
        public static ReplaceGroup CreateReplaceGroup(string groupName, params BuilderInfo[] machines)
        {
            try
            {
                var group = ScriptableObject.CreateInstance<SimpleReplaceGroup>();
                group.name = groupName;

                var options = new SimpleReplaceGroup.SimpleReplaceOption[machines.Length];
                for (int i = 0; i < machines.Length; i++)
                {
                    options[i] = new SimpleReplaceGroup.SimpleReplaceOption
                    {
                        SwapInfo = new ReplaceGroup.SwapInfo(machines[i], 0),
                        DisplayName = machines[i].displayName,
                        DisplayIcon = machines[i].sprite
                    };
                }

                typeof(SimpleReplaceGroup).GetField("ReplaceOptions").SetValue(group, options);

                // Assign the group to all machines
                foreach (var machine in machines)
                {
                    machine.replaceGroup = group;
                }

                _modifiedGroups[groupName] = group;
                TechtonicaFrameworkPlugin.Log.LogInfo($"Created new ReplaceGroup '{groupName}' with {machines.Length} machines");

                return group;
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogError($"Failed to create ReplaceGroup: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Automatically scan for modded machines and add them to appropriate replace groups
        /// </summary>
        public static void AutoRegisterModdedMachines()
        {
            if (GameDefines.instance?.resources == null)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning("Cannot auto-register - GameDefines not ready");
                return;
            }

            int registered = 0;

            // Machine type patterns to look for
            // Pattern -> Base machine name to find replace group from
            var machinePatterns = new Dictionary<string, string>
            {
                // Primary production machines
                { "Smelter", "Smelter" },
                { "Assembler", "Assembler" },
                { "Thresher", "Thresher" },
                { "Planter", "Planter" },
                { "Crusher", "Crusher" },
                { "Mining Drill", "Mining Drill" },
                { "Drill", "Mining Drill" },

                // Logistics machines
                { "Inserter", "Inserter" },
                { "Conveyor", "Conveyor Belt" },
                { "Container", "Container" },
                { "Chest", "Container" },

                // Power machines
                { "Reactor", "Crank Generator" },
                { "Generator", "Crank Generator" },
                { "Solar", "Crank Generator" },

                // Other production
                { "Filter", "Filter Inserter" },
                { "Accumulator", "Accumulator" },
                { "Monorail", "Monorail" },
            };

            foreach (var resource in GameDefines.instance.resources)
            {
                if (!(resource is BuilderInfo builder)) continue;
                if (builder.replaceGroup != null) continue; // Already has a group

                // Check if this looks like a modded variant
                string displayName = builder.displayName ?? builder.name ?? "";
                string lowerName = displayName.ToLower();

                // Check for MK/Mark indicators
                bool isMkVariant = lowerName.Contains(" mk") || lowerName.Contains(" mark") ||
                                   lowerName.Contains("_mk") || lowerName.Contains("_mark");

                if (isMkVariant)
                {
                    foreach (var pattern in machinePatterns)
                    {
                        if (lowerName.Contains(pattern.Key.ToLower()))
                        {
                            // Found a modded variant - try to add it
                            RegisterModdedMachine(builder.uniqueId, pattern.Value);
                            registered++;
                            break;
                        }
                    }
                }
            }

            if (registered > 0)
            {
                TechtonicaFrameworkPlugin.Log.LogInfo($"Auto-detected {registered} modded machine variants for replacer tool");
                ProcessQueuedRegistrations();
            }
        }

        /// <summary>
        /// Get statistics about modified replace groups
        /// </summary>
        public static (int modifiedGroups, int addedMachines) GetStats()
        {
            return (_modifiedGroups.Count, _registeredMachines.Count);
        }
    }
}
