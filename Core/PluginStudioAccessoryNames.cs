using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginStudioAccessoryNames
    {
        private const string CoroutineTypeName =
            "HS2_StudioAccessoryNames.HS2_StudioAccessoryNames+<UpdateStudioLabelsDelayed>d__3";
        private const string StudioSlotPath =
            "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/Slot";

        private static bool loggedSuppressedException;

        internal static void InstallHarmonyPatches(Harmony harmony)
        {
            if (harmony == null)
            {
                return;
            }

            try
            {
                Type coroutineType = AccessTools.TypeByName(CoroutineTypeName);
                MethodInfo moveNext = AccessTools.Method(coroutineType, "MoveNext");
                MethodInfo prefix = AccessTools.Method(
                    typeof(PluginStudioAccessoryNames),
                    nameof(BeforeMoveNext));
                MethodInfo finalizer = AccessTools.Method(
                    typeof(PluginStudioAccessoryNames),
                    nameof(AfterMoveNextException));
                if (moveNext == null || prefix == null || finalizer == null)
                {
                    return;
                }

                harmony.Patch(
                    moveNext,
                    prefix: new HarmonyMethod(prefix),
                    finalizer: new HarmonyMethod(finalizer));
                StudioCharaEditor.Logger?.LogInfo(
                    "StudioAccessoryNames compatibility guard installed.");
            }
            catch (Exception exception)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Could not install StudioAccessoryNames compatibility guard: " +
                    exception.Message);
            }
        }

        private static bool BeforeMoveNext(ref bool __result)
        {
            // The external coroutine calls GameObject.Find(...).transform
            // without checking whether Studio's accessory-slot UI currently
            // exists. When another editor page is active this object can be
            // absent, producing the same NullReferenceException every frame.
            if (GameObject.Find(StudioSlotPath) != null)
            {
                return true;
            }

            __result = false;
            return false;
        }

        private static Exception AfterMoveNextException(Exception __exception)
        {
            if (!(__exception is NullReferenceException))
            {
                return __exception;
            }

            if (!loggedSuppressedException)
            {
                loggedSuppressedException = true;
                StudioCharaEditor.Logger?.LogWarning(
                    "StudioAccessoryNames tried to update a missing Studio accessory label; " +
                    "the stale update was skipped.");
            }
            return null;
        }
    }
}
