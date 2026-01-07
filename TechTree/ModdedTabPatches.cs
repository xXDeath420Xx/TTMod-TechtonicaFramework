using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace TechtonicaFramework.TechTree
{
    /// <summary>
    /// Harmony patches for the Modded tab functionality.
    /// These patches extend the tech tree from 7 categories to 8.
    /// </summary>
    [HarmonyPatch]
    internal static class ModdedTabPatches
    {
        /// <summary>
        /// Transpiler to modify TechTreeState.Init to use 8 categories instead of 7
        /// </summary>
        [HarmonyPatch(typeof(TechTreeState), "Init")]
        [HarmonyTranspiler]
        [HarmonyPriority(Priority.First)]
        public static IEnumerable<CodeInstruction> TechTreeState_Init_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int patchCount = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_7)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4_8);
                    patchCount++;
                    TechtonicaFrameworkPlugin.Log?.LogInfo($"ModdedTabPatches: Patched constant 7 -> 8 at IL offset {i}");
                }
                else if (codes[i].opcode == OpCodes.Ldc_I4_S && codes[i].operand is sbyte sb && sb == 7)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4_S, (sbyte)8);
                    patchCount++;
                    TechtonicaFrameworkPlugin.Log?.LogInfo($"ModdedTabPatches: Patched constant 7 -> 8 (ldc.i4.s) at IL offset {i}");
                }
                else if (codes[i].opcode == OpCodes.Ldc_I4 && codes[i].operand is int intVal && intVal == 7)
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4, 8);
                    patchCount++;
                    TechtonicaFrameworkPlugin.Log?.LogInfo($"ModdedTabPatches: Patched constant 7 -> 8 (ldc.i4) at IL offset {i}");
                }
            }

            TechtonicaFrameworkPlugin.Log?.LogInfo($"ModdedTabPatches: Transpiler patched {patchCount} occurrences of '7' to '8' in TechTreeState.Init");
            return codes;
        }

        [HarmonyPatch(typeof(TechTreeState), "Init")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        public static void TechTreeState_Init_Postfix(TechTreeState __instance)
        {
            ModdedTabModule.OnTechTreeStateInitPostfix(__instance);
        }

        [HarmonyPatch(typeof(TechTreeUI), "Init")]
        [HarmonyPostfix]
        public static void TechTreeUI_Init_Postfix(TechTreeUI __instance)
        {
            ModdedTabModule.OnTechTreeUIInitPostfix(__instance);
        }

        [HarmonyPatch(typeof(TechTreeGrid), "Init")]
        [HarmonyPostfix]
        public static void TechTreeGrid_Init_Postfix(TechTreeGrid __instance)
        {
            ModdedTabModule.OnTechTreeGridInitPostfix(__instance);
        }

        [HarmonyPatch(typeof(TechTreeCategoryButton), "InitButton")]
        [HarmonyPrefix]
        public static bool TechTreeCategoryButton_InitButton_Prefix(
            TechTreeCategoryButton __instance,
            TechTreeGrid gridUI,
            Unlock.TechCategory category,
            bool grayOut)
        {
            return ModdedTabModule.OnInitButtonPrefix(__instance, gridUI, category, grayOut);
        }

        [HarmonyPatch(typeof(TechTreeCategoryButton), "InitButton")]
        [HarmonyPostfix]
        public static void TechTreeCategoryButton_InitButton_Postfix(
            TechTreeCategoryButton __instance,
            TechTreeGrid gridUI,
            Unlock.TechCategory category)
        {
            ModdedTabModule.OnInitButtonPostfix(__instance, category);
        }

        [HarmonyPatch(typeof(TechTreeGrid), "InitForCategory")]
        [HarmonyPrefix]
        public static void TechTreeGrid_InitForCategory_Prefix(TechTreeGrid __instance, Unlock.TechCategory category)
        {
            ModdedTabModule.OnInitForCategoryPrefix(__instance, category);
        }

        [HarmonyPatch(typeof(TechTreeGrid), "InitForCategory")]
        [HarmonyPostfix]
        public static void TechTreeGrid_InitForCategory_Postfix(TechTreeGrid __instance, Unlock.TechCategory category)
        {
            ModdedTabModule.OnInitForCategoryPostfix(__instance, category);
        }

        [HarmonyPatch(typeof(TechTreeGrid), "GetNeighboringCategories")]
        [HarmonyPrefix]
        public static bool TechTreeGrid_GetNeighboringCategories_Prefix(
            TechTreeGrid __instance,
            Unlock.TechCategory category,
            ref Unlock.TechCategory prev,
            ref Unlock.TechCategory next)
        {
            return ModdedTabModule.OnGetNeighboringCategoriesPrefix(__instance, category, ref prev, ref next);
        }

        [HarmonyPatch(typeof(TechTreeState), "ReadSaveState")]
        [HarmonyPostfix]
        public static void TechTreeState_ReadSaveState_Postfix()
        {
            ModdedTabModule.MapModdedUnlocks();
        }
    }
}
