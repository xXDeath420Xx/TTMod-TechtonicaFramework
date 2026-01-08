using System;
using System.Collections.Generic;
using System.Reflection;
using EquinoxsModUtils;
using HarmonyLib;
using UnityEngine;

namespace TechtonicaFramework.Compatibility
{
    /// <summary>
    /// Ensures modded recipes are properly added to the game's recipe cache.
    /// This fixes the issue where modded buildings don't appear in assembler crafting menus.
    /// </summary>
    public static class RecipeCacheFix
    {
        private static bool _initialized = false;

        /// <summary>
        /// Initialize the recipe cache fix system
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // Hook into EMU event to refresh caches after modded content loads
            EMU.Events.TechTreeStateLoaded += OnTechTreeStateLoaded;

            TechtonicaFrameworkPlugin.Log.LogInfo("RecipeCacheFix: Initialized");
        }

        private static void OnTechTreeStateLoaded()
        {
            // Delay the refresh slightly to ensure all mods have registered their content
            RefreshModdedRecipeCaches();
        }

        /// <summary>
        /// Call this after GameDefinesLoaded to refresh recipe caches with modded recipes
        /// </summary>
        public static void RefreshModdedRecipeCaches()
        {
            try
            {
                if (GameDefines.instance == null)
                {
                    TechtonicaFrameworkPlugin.Log.LogWarning("RecipeCacheFix: GameDefines not available");
                    return;
                }

                int addedCount = 0;
                var allRecipes = GameDefines.instance.schematicsRecipeEntries;
                if (allRecipes == null) return;

                // Get the cached arrays via reflection
                var gameDefinesType = typeof(GameDefines);
                var assemblerCacheField = gameDefinesType.GetField("_cachedAssemblerRecipesLookupByOutputId",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var smelterCacheField = gameDefinesType.GetField("_cachedSmelterRecipesLookupByOutputId",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (assemblerCacheField == null)
                {
                    TechtonicaFrameworkPlugin.Log.LogWarning("RecipeCacheFix: Could not find recipe cache fields");
                    return;
                }

                var assemblerCache = assemblerCacheField.GetValue(GameDefines.instance) as List<SchematicsRecipeData>[];
                var smelterCache = smelterCacheField?.GetValue(GameDefines.instance) as List<SchematicsRecipeData>[];

                foreach (var recipe in allRecipes)
                {
                    if (recipe == null) continue;
                    if (recipe.outputTypes == null || recipe.outputTypes.Length != 1) continue;

                    int outputId = recipe.outputTypes[0]?.uniqueId ?? -1;
                    if (outputId < 0) continue;

                    // Check if this recipe is already in the cache
                    if (recipe.craftingMethod == CraftingMethod.Assembler)
                    {
                        if (AddToRecipeCache(assemblerCache, outputId, recipe))
                        {
                            addedCount++;
                            TechtonicaFrameworkPlugin.LogDebug($"Added assembler recipe: {recipe.outputTypes[0].displayName}");
                        }
                    }
                    else if (recipe.craftingMethod == CraftingMethod.Smelter && smelterCache != null)
                    {
                        if (AddToRecipeCache(smelterCache, outputId, recipe))
                        {
                            addedCount++;
                            TechtonicaFrameworkPlugin.LogDebug($"Added smelter recipe: {recipe.outputTypes[0].displayName}");
                        }
                    }
                }

                if (addedCount > 0)
                {
                    TechtonicaFrameworkPlugin.Log.LogInfo($"RecipeCacheFix: Added {addedCount} modded recipes to cache");
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"RecipeCacheFix error: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a recipe to a cache array if not already present
        /// </summary>
        private static bool AddToRecipeCache(List<SchematicsRecipeData>[] cache, int outputId, SchematicsRecipeData recipe)
        {
            if (cache == null) return false;
            if (outputId < 0 || outputId >= cache.Length) return false;

            // Create list if needed
            if (cache[outputId] == null)
            {
                cache[outputId] = new List<SchematicsRecipeData>();
            }

            // Check if already in cache
            foreach (var existing in cache[outputId])
            {
                if (existing.uniqueId == recipe.uniqueId)
                {
                    return false; // Already cached
                }
            }

            // Add to cache
            cache[outputId].Add(recipe);
            return true;
        }

        /// <summary>
        /// Force refresh all recipe display data after modded content is loaded
        /// </summary>
        public static void RefreshRecipeDisplayData()
        {
            try
            {
                // Call PostLoadRuntimeInit to refresh recipe quantities and display data
                var postLoadMethod = typeof(GameDefines).GetMethod("PostLoadRuntimeInit",
                    BindingFlags.Public | BindingFlags.Instance);

                if (postLoadMethod != null && GameDefines.instance != null)
                {
                    postLoadMethod.Invoke(GameDefines.instance, null);
                    TechtonicaFrameworkPlugin.LogDebug("RecipeCacheFix: Refreshed recipe display data");
                }
            }
            catch (Exception ex)
            {
                TechtonicaFrameworkPlugin.Log.LogWarning($"RecipeCacheFix: Error refreshing display data: {ex.Message}");
            }
        }
    }

}
