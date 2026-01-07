using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TechtonicaFramework.TechTree
{
    /// <summary>
    /// Provides a shared "Modded" tab in the tech tree for all mods to use.
    /// Any mod can register its unlocks to category 7 and they will appear in this tab.
    /// </summary>
    public static class ModdedTabModule
    {
        // Category constants
        public const int MODDED_CATEGORY_INDEX = 7;
        public const string MODDED_CATEGORY_NAME = "Modded";
        public const int EXTENDED_CATEGORY_COUNT = 8;

        // Singleton references
        private static bool isInitialized = false;
        private static TechTreeCategoryContainer moddedContainer = null;
        private static TechTreeCategoryButton moddedButton = null;

        // Logger reference (set by first mod to initialize)
        private static ManualLogSource log;

        /// <summary>
        /// Initialize the ModdedTab system. Call this from your mod's Awake().
        /// Multiple mods can call this safely - only the first call initializes.
        /// </summary>
        public static void Initialize(ManualLogSource logger = null)
        {
            if (isInitialized) return;

            log = logger ?? TechtonicaFrameworkPlugin.Log;
            isInitialized = true;
            log?.LogInfo("ModdedTabModule: Initialized");
        }

        /// <summary>
        /// Check if the Modded tab is active
        /// </summary>
        public static bool IsModdedTabActive => moddedButton != null && moddedContainer != null;

        /// <summary>
        /// Get the Modded category index for use when configuring unlocks
        /// </summary>
        public static Unlock.TechCategory ModdedCategory => (Unlock.TechCategory)MODDED_CATEGORY_INDEX;

        /// <summary>
        /// Configure an unlock to appear in the Modded tab
        /// </summary>
        public static void RegisterUnlockToModdedTab(Unlock unlock, int treePosition = 50)
        {
            if (unlock == null) return;
            unlock.category = ModdedCategory;
            unlock.treePosition = treePosition;
        }

        /// <summary>
        /// Map all unlocks with category 7 to the Modded category mapping.
        /// Called automatically but can be called manually if needed.
        /// </summary>
        public static void MapModdedUnlocks()
        {
            if (TechTreeState.instance == null || GameDefines.instance?.unlocks == null)
            {
                log?.LogWarning("ModdedTabModule: MapModdedUnlocks - TechTreeState or GameDefines not ready");
                return;
            }

            try
            {
                var categoryMapping = TechTreeState.instance.categoryMapping;
                if (categoryMapping == null)
                {
                    log?.LogWarning("ModdedTabModule: categoryMapping is null");
                    return;
                }

                log?.LogInfo($"ModdedTabModule: categoryMapping has {categoryMapping.Count} entries");

                // Ensure we have 8 entries
                while (categoryMapping.Count < EXTENDED_CATEGORY_COUNT)
                {
                    categoryMapping.Add(new List<int>());
                    log?.LogInfo($"ModdedTabModule: Added categoryMapping entry, now have {categoryMapping.Count}");
                }

                var moddedMapping = categoryMapping[MODDED_CATEGORY_INDEX];
                if (moddedMapping == null)
                {
                    moddedMapping = new List<int>();
                    categoryMapping[MODDED_CATEGORY_INDEX] = moddedMapping;
                }

                int mapped = 0;
                int totalModded = 0;

                log?.LogInfo($"ModdedTabModule: Scanning {GameDefines.instance.unlocks.Count} total unlocks...");

                foreach (var unlock in GameDefines.instance.unlocks)
                {
                    if (unlock == null) continue;

                    if ((int)unlock.category == MODDED_CATEGORY_INDEX)
                    {
                        totalModded++;
                        string actualName = !string.IsNullOrEmpty(unlock.displayNameHash)
                            ? LocsUtility.TranslateStringFromHash(unlock.displayNameHash)
                            : $"(id={unlock.uniqueId})";

                        log?.LogInfo($"ModdedTabModule: Found modded unlock: '{actualName}' id={unlock.uniqueId}");

                        if (!moddedMapping.Contains(unlock.uniqueId))
                        {
                            moddedMapping.Add(unlock.uniqueId);
                            mapped++;
                        }
                    }
                }

                log?.LogInfo($"ModdedTabModule: Found {totalModded} unlocks with category 7, mapped {mapped} new ones. Total in mapping: {moddedMapping.Count}");

                // Sort by tier and tree position
                if (moddedMapping.Count > 0)
                {
                    moddedMapping.Sort((a, b) =>
                    {
                        var stateA = TechTreeState.instance.unlockStates[a];
                        var stateB = TechTreeState.instance.unlockStates[b];
                        int tierCompare = stateA.tier.CompareTo(stateB.tier);
                        return tierCompare != 0 ? tierCompare : stateA.unlockRef.treePosition.CompareTo(stateB.unlockRef.treePosition);
                    });
                }
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Error mapping unlocks: {ex}");
            }
        }

        #region Internal Methods for Patches

        internal static void SetLogger(ManualLogSource logger)
        {
            if (log == null) log = logger;
        }

        internal static void OnTechTreeStateInitPostfix(TechTreeState instance)
        {
            try
            {
                var categoryMappingField = typeof(TechTreeState).GetField("categoryMapping", BindingFlags.Public | BindingFlags.Instance);
                if (categoryMappingField != null)
                {
                    var categoryMapping = categoryMappingField.GetValue(instance);
                    if (categoryMapping != null)
                    {
                        var asList = categoryMapping as IList;
                        if (asList != null)
                        {
                            log?.LogInfo($"ModdedTabModule: categoryMapping now has {asList.Count} entries after Init");

                            if (asList.Count == 7)
                            {
                                var listType = asList[0].GetType();
                                var newList = Activator.CreateInstance(listType);
                                var addMethod = categoryMapping.GetType().GetMethod("Add");
                                if (addMethod != null)
                                {
                                    addMethod.Invoke(categoryMapping, new object[] { newList });
                                    log?.LogInfo("ModdedTabModule: Added 8th entry in Postfix fallback");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Error in Init Postfix: {ex.Message}");
            }
        }

        internal static void OnTechTreeUIInitPostfix(TechTreeUI instance)
        {
            try
            {
                var gridUIField = typeof(TechTreeUI).GetField("gridUI", BindingFlags.NonPublic | BindingFlags.Instance);
                var cachedButtonsField = typeof(TechTreeUI).GetField("cachedButtons", BindingFlags.NonPublic | BindingFlags.Instance);
                var categoryButtonPrefabField = typeof(TechTreeUI).GetField("categoryButtonPrefab", BindingFlags.NonPublic | BindingFlags.Instance);
                var categoryButtonParentField = typeof(TechTreeUI).GetField("categoryButtonParent", BindingFlags.NonPublic | BindingFlags.Instance);

                if (gridUIField == null || cachedButtonsField == null || categoryButtonPrefabField == null || categoryButtonParentField == null)
                {
                    log?.LogError("ModdedTabModule: Could not find required TechTreeUI fields");
                    return;
                }

                var gridUI = (TechTreeGrid)gridUIField.GetValue(instance);
                var cachedButtons = (List<TechTreeCategoryButton>)cachedButtonsField.GetValue(instance);
                var categoryButtonPrefab = (TechTreeCategoryButton)categoryButtonPrefabField.GetValue(instance);
                var categoryButtonParent = (RectTransform)categoryButtonParentField.GetValue(instance);

                if (gridUI == null || cachedButtons == null || categoryButtonPrefab == null || categoryButtonParent == null)
                {
                    log?.LogError("ModdedTabModule: Required TechTreeUI field values are null");
                    return;
                }

                if (cachedButtons.Count > 7)
                {
                    log?.LogInfo("ModdedTabModule: Modded button already exists");
                    return;
                }

                moddedButton = UnityEngine.Object.Instantiate(categoryButtonPrefab, categoryButtonParent);
                moddedButton.gameObject.name = "Modded Button";

                InitializeModdedButton(moddedButton, gridUI);

                cachedButtons.Add(moddedButton);
                MapModdedUnlocks();

                log?.LogInfo("ModdedTabModule: Added Modded tab button to tech tree UI");
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Error adding Modded tab button: {ex}");
            }
        }

        internal static void OnTechTreeGridInitPostfix(TechTreeGrid instance)
        {
            try
            {
                var categoryContainersField = typeof(TechTreeGrid).GetField("categoryContainers", BindingFlags.NonPublic | BindingFlags.Instance);
                var categoryPrefabField = typeof(TechTreeGrid).GetField("categoryPrefab", BindingFlags.Public | BindingFlags.Instance);
                var scrollXfmField = typeof(TechTreeGrid).GetField("scrollXfm", BindingFlags.Public | BindingFlags.Instance);

                if (categoryContainersField == null)
                {
                    log?.LogError("ModdedTabModule: Could not find categoryContainers field");
                    return;
                }

                var categoryContainers = (TechTreeCategoryContainer[])categoryContainersField.GetValue(instance);
                var categoryPrefab = (TechTreeCategoryContainer)categoryPrefabField?.GetValue(instance);
                var scrollXfm = (RectTransform)scrollXfmField?.GetValue(instance);

                if (categoryContainers == null)
                {
                    log?.LogError("ModdedTabModule: categoryContainers is null");
                    return;
                }

                if (categoryContainers.Length > 7)
                {
                    log?.LogInfo("ModdedTabModule: Category containers already expanded");
                    return;
                }

                if (categoryPrefab == null || scrollXfm == null)
                {
                    log?.LogError("ModdedTabModule: categoryPrefab or scrollXfm is null");
                    return;
                }

                var newContainers = new TechTreeCategoryContainer[8];
                Array.Copy(categoryContainers, newContainers, 7);

                moddedContainer = UnityEngine.Object.Instantiate(categoryPrefab, scrollXfm);
                moddedContainer.gameObject.name = "Modded Category Container";

                var initMethod = typeof(TechTreeCategoryContainer).GetMethod("Init", BindingFlags.Public | BindingFlags.Instance);
                if (initMethod != null)
                {
                    initMethod.Invoke(moddedContainer, new object[] { (Unlock.TechCategory)MODDED_CATEGORY_INDEX, instance });
                }

                newContainers[7] = moddedContainer;
                categoryContainersField.SetValue(instance, newContainers);

                log?.LogInfo("ModdedTabModule: Added Modded category container to tech tree grid");
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Error adding Modded category container: {ex}");
            }
        }

        internal static bool OnInitButtonPrefix(TechTreeCategoryButton instance, TechTreeGrid gridUI, Unlock.TechCategory category, bool grayOut)
        {
            if ((int)category != MODDED_CATEGORY_INDEX)
            {
                return true;
            }

            log?.LogInfo($"ModdedTabModule: Prefix intercepting InitButton for category {category}");

            try
            {
                if (instance.categoryNames != null)
                {
                    foreach (var textMesh in instance.categoryNames)
                    {
                        if (textMesh != null)
                        {
                            textMesh.text = MODDED_CATEGORY_NAME;
                        }
                    }
                }

                instance._myCategory = category;
                instance.myGrid = gridUI;
                instance.grayedOut = false;
                instance.hovered = false;

                if (instance.grayedOutCanvasGroup != null)
                {
                    instance.grayedOutCanvasGroup.alpha = 0f;
                    instance.grayedOutCanvasGroup.interactable = false;
                    instance.grayedOutCanvasGroup.blocksRaycasts = false;
                }

                if (instance.hoverCanvasGroup != null)
                {
                    instance.hoverCanvasGroup.alpha = 0f;
                    instance.hoverCanvasGroup.interactable = false;
                    instance.hoverCanvasGroup.blocksRaycasts = false;
                }

                if (instance.unselectedCanvasGroup != null)
                {
                    instance.unselectedCanvasGroup.alpha = 1f;
                    instance.unselectedCanvasGroup.interactable = true;
                    instance.unselectedCanvasGroup.blocksRaycasts = true;
                }

                if (instance.selectedCategoryCanvasGroup != null)
                {
                    instance.selectedCategoryCanvasGroup.alpha = 0f;
                }

                if (instance.newNotificationBadge != null)
                {
                    instance.newNotificationBadge.alpha = 0f;
                    instance.newNotificationBadge.interactable = false;
                    instance.newNotificationBadge.blocksRaycasts = false;
                }

                var capturedInstance = instance;
                var capturedGrid = gridUI;

                instance.SetCallbacks(
                    () => { capturedInstance.hovered = true; },
                    () => { capturedInstance.hovered = false; },
                    () => {
                        if (!capturedInstance.grayedOut && capturedGrid != null)
                        {
                            capturedGrid.InitForCategory((Unlock.TechCategory)MODDED_CATEGORY_INDEX);
                        }
                    },
                    null, null, null
                );

                log?.LogInfo($"ModdedTabModule: Prefix completed - mouseLeftClickCallback is {(instance.mouseLeftClickCallback != null ? "SET" : "NULL")}");
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Prefix error: {ex}");
                return true;
            }

            return false;
        }

        internal static void OnInitButtonPostfix(TechTreeCategoryButton instance, Unlock.TechCategory category)
        {
            if ((int)category == MODDED_CATEGORY_INDEX)
            {
                if (instance.categoryNames != null)
                {
                    foreach (var textMesh in instance.categoryNames)
                    {
                        if (textMesh != null && textMesh.text != MODDED_CATEGORY_NAME)
                        {
                            textMesh.text = MODDED_CATEGORY_NAME;
                        }
                    }
                }
                log?.LogInfo($"ModdedTabModule: Postfix - category={instance._myCategory}, grayedOut={instance.grayedOut}, clickCallback={((instance.mouseLeftClickCallback != null) ? "SET" : "NULL")}");
            }
        }

        internal static void OnInitForCategoryPrefix(TechTreeGrid instance, Unlock.TechCategory category)
        {
            try
            {
                int maxUnlockId = TechTreeState.instance?.unlockStates?.Length ?? 0;

                if (instance.techTreeNodes != null && instance.techTreeNodes.Length < maxUnlockId)
                {
                    var newNodes = new TechTreeNode[maxUnlockId + 10];
                    Array.Copy(instance.techTreeNodes, newNodes, instance.techTreeNodes.Length);
                    instance.techTreeNodes = newNodes;
                }
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Error expanding techTreeNodes: {ex.Message}");
            }

            if ((int)category == MODDED_CATEGORY_INDEX)
            {
                MapModdedUnlocks();
            }
        }

        internal static void OnInitForCategoryPostfix(TechTreeGrid instance, Unlock.TechCategory category)
        {
            if ((int)category != MODDED_CATEGORY_INDEX) return;

            try
            {
                var containersField = typeof(TechTreeGrid).GetField("categoryContainers", BindingFlags.NonPublic | BindingFlags.Instance);
                if (containersField == null) return;

                var containers = (TechTreeCategoryContainer[])containersField.GetValue(instance);
                if (containers == null || containers.Length <= MODDED_CATEGORY_INDEX) return;

                var container = containers[MODDED_CATEGORY_INDEX];
                if (container == null) return;

                var mapping = container.categoryMapping;
                log?.LogInfo($"ModdedTabModule POSTFIX: After InitForCategory(7), checking nodes...");

                int nodesCreated = 0;
                foreach (var id in mapping)
                {
                    if (id >= 0 && id < instance.techTreeNodes.Length)
                    {
                        var node = instance.techTreeNodes[id];
                        if (node != null)
                        {
                            nodesCreated++;
                            var pos = node.xfm?.anchoredPosition ?? Vector2.zero;
                            log?.LogInfo($"  Node[{id}]: pos=({pos.x:F1}, {pos.y:F1}), active={node.gameObject.activeSelf}");
                        }
                    }
                }
                log?.LogInfo($"ModdedTabModule POSTFIX: {nodesCreated} nodes created");
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule POSTFIX error: {ex.Message}");
            }
        }

        internal static bool OnGetNeighboringCategoriesPrefix(
            TechTreeGrid instance,
            Unlock.TechCategory category,
            ref Unlock.TechCategory prev,
            ref Unlock.TechCategory next)
        {
            prev = category - 1;
            next = category + 1;

            var hasUnlockedMethod = typeof(TechTreeGrid).GetMethod("HasUnlockedCategory", BindingFlags.Public | BindingFlags.Instance);
            if (hasUnlockedMethod == null) return true;

            Func<Unlock.TechCategory, bool> hasUnlocked = (cat) =>
            {
                if ((int)cat == MODDED_CATEGORY_INDEX) return true;
                if ((int)cat < 0 || (int)cat > MODDED_CATEGORY_INDEX) return false;
                return (bool)hasUnlockedMethod.Invoke(instance, new object[] { cat });
            };

            while (!hasUnlocked(prev))
            {
                if ((int)prev < 0) prev = (Unlock.TechCategory)MODDED_CATEGORY_INDEX;
                else prev = prev - 1;
                if (prev == category) break;
            }

            while (!hasUnlocked(next))
            {
                if ((int)next > MODDED_CATEGORY_INDEX) next = Unlock.TechCategory.Terraforming;
                else next = next + 1;
                if (next == category) break;
            }

            return false;
        }

        private static void InitializeModdedButton(TechTreeCategoryButton button, TechTreeGrid gridUI)
        {
            try
            {
                if (button.categoryNames != null)
                {
                    foreach (var textMesh in button.categoryNames)
                    {
                        if (textMesh != null) textMesh.text = MODDED_CATEGORY_NAME;
                    }
                }

                button._myCategory = (Unlock.TechCategory)MODDED_CATEGORY_INDEX;
                button.myGrid = gridUI;
                button.grayedOut = false;
                button.hovered = false;

                if (button.grayedOutCanvasGroup != null)
                {
                    button.grayedOutCanvasGroup.alpha = 0f;
                    button.grayedOutCanvasGroup.interactable = false;
                    button.grayedOutCanvasGroup.blocksRaycasts = false;
                }

                if (button.hoverCanvasGroup != null)
                {
                    button.hoverCanvasGroup.alpha = 0f;
                    button.hoverCanvasGroup.interactable = false;
                    button.hoverCanvasGroup.blocksRaycasts = false;
                }

                if (button.unselectedCanvasGroup != null)
                {
                    button.unselectedCanvasGroup.alpha = 1f;
                    button.unselectedCanvasGroup.interactable = true;
                    button.unselectedCanvasGroup.blocksRaycasts = true;
                }

                if (button.selectedCategoryCanvasGroup != null)
                {
                    button.selectedCategoryCanvasGroup.alpha = 0f;
                }

                if (!button.gameObject.activeSelf)
                {
                    button.gameObject.SetActive(true);
                }

                var capturedGrid = gridUI;
                var capturedButton = button;

                button.SetCallbacks(
                    () => { capturedButton.hovered = true; },
                    () => { capturedButton.hovered = false; },
                    () => {
                        if (capturedGrid != null && !capturedButton.grayedOut)
                        {
                            capturedGrid.InitForCategory((Unlock.TechCategory)MODDED_CATEGORY_INDEX);
                        }
                    },
                    null, null, null
                );

                log?.LogInfo("ModdedTabModule: Modded button initialized");
            }
            catch (Exception ex)
            {
                log?.LogError($"ModdedTabModule: Error initializing Modded button: {ex}");
            }
        }

        #endregion
    }
}
