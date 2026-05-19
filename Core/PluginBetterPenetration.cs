using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AIChara;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginBetterPenetration
    {
        private const string StudioBetterPenetrationGuid = "com.animal42069.studiobetterpenetration";
        private const string UncensorSelectorGuid = "com.deathweasel.bepinex.uncensorselector";
        private const string CurrentStudioBetterPenetrationTypeName =
            "Core_BetterPenetration.Studio_BetterPenetration";
        private const string HS2StudioBetterPenetrationTypeName =
            "HS2_Studio_BetterPenetration.HS2_Studio_BetterPenetration";

        private static bool loggedSkip;
        private static bool loggedClothesReloadSupport;
        private static bool installedExternalReloadPatches;
        private static bool loggedExternalReloadPatch;
        private static readonly Stack<ClothesReloadState> externalReloadStates = new Stack<ClothesReloadState>();

        internal sealed class ControllerSnapshot
        {
            public object Controller;
            public ChaControl Owner;
            public ChaControl CollisionTarget;
            public bool OwnerReloaded;
            public bool TargetReloaded;
            public bool IsKokan;
            public bool IsAna;
            public bool IsOral;
            public bool WasEnabled;
            public string DanEntryParentName;
            public string DanEndParentName;
        }

        internal sealed class ClothesReloadState
        {
            private readonly List<ControllerSnapshot> controllers = new List<ControllerSnapshot>();

            internal List<ControllerSnapshot> Controllers
            {
                get { return controllers; }
            }
        }

        public static bool HasStudioPlugin()
        {
            if (Chainloader.PluginInfos.ContainsKey(StudioBetterPenetrationGuid))
            {
                return true;
            }

            return FindType(CurrentStudioBetterPenetrationTypeName) != null ||
                   FindType(HS2StudioBetterPenetrationTypeName) != null;
        }

        public static void InstallHarmonyPatches(Harmony harmony)
        {
            if (harmony == null || installedExternalReloadPatches || !HasStudioPlugin())
            {
                return;
            }

            try
            {
                if (!Chainloader.PluginInfos.TryGetValue(UncensorSelectorGuid, out var pluginInfo) ||
                    pluginInfo?.Instance == null)
                {
                    return;
                }

                Type nestedType = pluginInfo.Instance.GetType().GetNestedType("UncensorSelectorController", AccessTools.all);
                if (nestedType == null)
                {
                    return;
                }

                bool patchedBody = PatchExternalReloadMethod(harmony, nestedType, "ReloadCharacterBody");
                bool patchedBalls = PatchExternalReloadMethod(harmony, nestedType, "ReloadCharacterBalls");
                installedExternalReloadPatches = patchedBody || patchedBalls;

                if (installedExternalReloadPatches && !loggedExternalReloadPatch)
                {
                    loggedExternalReloadPatch = true;
                    StudioCharaEditor.Logger?.LogInfo(
                        "BetterPenetration detected; repairing BP controllers after UncensorSelector body reloads.");
                }
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning($"Failed to install BetterPenetration reload repair hooks: {ex.Message}");
            }
        }

        private static bool PatchExternalReloadMethod(Harmony harmony, Type nestedType, string methodName)
        {
            MethodInfo method = AccessTools.Method(nestedType, methodName, null, null);
            MethodInfo before = AccessTools.Method(typeof(PluginBetterPenetration), nameof(BeforeExternalCharacterReload));
            MethodInfo after = AccessTools.Method(typeof(PluginBetterPenetration), nameof(AfterExternalCharacterReload));
            if (method == null || before == null || after == null)
            {
                return false;
            }

            HarmonyMethod prefix = new HarmonyMethod(before)
            {
                priority = Priority.First
            };
            HarmonyMethod postfix = new HarmonyMethod(after)
            {
                priority = Priority.Last
            };

            harmony.Patch(method, prefix, postfix);
            return true;
        }

        public static void LogSkippedInitialClothesRefresh()
        {
            if (loggedSkip)
            {
                return;
            }

            loggedSkip = true;
            StudioCharaEditor.Logger?.LogInfo(
                "BetterPenetration detected; skipping StudioCharaEditor initial ChangeClothes refresh to preserve BP studio constraints.");
        }

        public static ClothesReloadState BeforeClothesReload(ChaControl changedChaCtrl)
        {
            if (!HasStudioPlugin())
            {
                return null;
            }

            ClothesReloadState state = CaptureControllerState(changedChaCtrl);
            return state;
        }

        public static void RunWithReloadRepair(ChaControl changedChaCtrl, Action action)
        {
            if (action == null)
            {
                return;
            }

            ClothesReloadState state = BeforeClothesReload(changedChaCtrl);
            try
            {
                action();
            }
            finally
            {
                AfterClothesReload(state);
                PluginHooahComponents.ScheduleRebindDickColliders(changedChaCtrl);
            }
        }

        public static void AfterClothesReload(ClothesReloadState state)
        {
            if (state == null || state.Controllers.Count == 0)
            {
                return;
            }

            if (!loggedClothesReloadSupport)
            {
                loggedClothesReloadSupport = true;
                StudioCharaEditor.Logger?.LogInfo("BetterPenetration detected; reinitializing BP controllers after clothing/body reloads.");
            }

            RepairControllers(state);

            CharaEditorMgr.Instance?.RunAfterFrames(5, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(15, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(30, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(60, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(120, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(240, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(360, () => RepairControllers(state));
            CharaEditorMgr.Instance?.RunAfterFrames(600, () => RepairControllers(state));
        }

        private static void BeforeExternalCharacterReload()
        {
            if (!HasStudioPlugin())
            {
                return;
            }

            externalReloadStates.Push(CaptureControllerState(null));
        }

        private static void AfterExternalCharacterReload()
        {
            ClothesReloadState state = externalReloadStates.Count > 0
                ? externalReloadStates.Pop()
                : CaptureControllerState(null);
            AfterClothesReload(state);
        }

        private static ClothesReloadState CaptureControllerState(ChaControl changedChaCtrl)
        {
            ClothesReloadState state = new ClothesReloadState();
            Type controllerType = FindType("Core_BetterPenetration.BetterPenetrationController");
            if (controllerType == null)
            {
                return state;
            }

            FieldInfo controllersField = controllerType.GetField("controllers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(controllersField?.GetValue(null) is IEnumerable controllers))
            {
                return state;
            }

            foreach (object controller in controllers)
            {
                if (controller == null)
                {
                    continue;
                }

                ChaControl owner = GetControllerOwner(controller);
                ChaControl collisionTarget = GetCollisionTarget(controller);
                ChaControl constraintTarget = GetConstraintTarget(controller);
                string danEntryParentName = GetConstraintParentName(controller, "danEntryParentName", "danEntryConstraint");
                string danEndParentName = GetConstraintParentName(controller, "danEndParentName", "danEndConstraint");
                bool hasActiveTarget = collisionTarget != null ||
                                       constraintTarget != null ||
                                       !string.IsNullOrEmpty(danEntryParentName) ||
                                       !string.IsNullOrEmpty(danEndParentName);
                if (!hasActiveTarget)
                {
                    continue;
                }
                if (changedChaCtrl != null && owner != changedChaCtrl && collisionTarget != changedChaCtrl && constraintTarget != changedChaCtrl)
                {
                    continue;
                }

                state.Controllers.Add(new ControllerSnapshot
                {
                    Controller = controller,
                    Owner = owner,
                    CollisionTarget = collisionTarget ?? constraintTarget,
                    OwnerReloaded = changedChaCtrl != null && owner == changedChaCtrl,
                    TargetReloaded = changedChaCtrl != null && (collisionTarget == changedChaCtrl || constraintTarget == changedChaCtrl),
                    IsKokan = GetBoolField(controller, "isKokan") || ParentNameIsKokan(danEntryParentName),
                    IsAna = GetBoolField(controller, "isAna") || ParentNameIsAna(danEntryParentName),
                    IsOral = GetBoolField(controller, "isOral") || ParentNameIsOral(danEntryParentName),
                    WasEnabled = controller is Behaviour behaviour && behaviour.enabled,
                    DanEntryParentName = danEntryParentName,
                    DanEndParentName = danEndParentName,
                });
            }

            return state;
        }

        private static void RepairControllers(ClothesReloadState state)
        {
            object nodeConstraintPlugin = GetStaticFieldValue("nodeConstraintPlugin");

            foreach (ControllerSnapshot snapshot in state.Controllers)
            {
                if (snapshot.Controller == null)
                {
                    continue;
                }

                if (snapshot.Controller is Behaviour behaviour && !snapshot.WasEnabled)
                {
                    continue;
                }

                bool needsDanAgent = snapshot.OwnerReloaded ||
                                      GetFieldValue(snapshot.Controller, "danAgent") == null ||
                                      !GetBoolField(snapshot.Controller, "danTargetsValid");
                Transform currentEntryParent = GetConstraintParentTransform(snapshot.Controller, "danEntryConstraint");
                Transform currentEndParent = GetConstraintParentTransform(snapshot.Controller, "danEndConstraint");
                bool entryConstraintMissing = !string.IsNullOrEmpty(snapshot.DanEntryParentName) && currentEntryParent == null;
                bool endConstraintMissing = !string.IsNullOrEmpty(snapshot.DanEndParentName) && currentEndParent == null;
                bool needsConstraints = snapshot.OwnerReloaded ||
                                        snapshot.TargetReloaded ||
                                        needsDanAgent ||
                                        entryConstraintMissing ||
                                        endConstraintMissing;

                if (needsConstraints && nodeConstraintPlugin != null)
                {
                    InvokeInstanceMethod(snapshot.Controller, "RemoveDanConstraints", nodeConstraintPlugin);
                }

                if (needsDanAgent)
                {
                    InvokeInstanceMethod(snapshot.Controller, "InitializeDanAgent");
                }

                RestoreConstraintFlags(snapshot);

                if (snapshot.CollisionTarget != null)
                {
                    InvokeInstanceMethod(
                        snapshot.Controller,
                        "SetCollisionAgent",
                        snapshot.CollisionTarget,
                        snapshot.IsKokan,
                        snapshot.IsAna,
                        snapshot.IsOral);
                    InvokeInstanceMethod(snapshot.Controller, "SetBellyColliders", true);
                }

                RestoreConstraintParentNames(snapshot);

                if (nodeConstraintPlugin != null)
                {
                    Transform danEntryParent = FindChildTransform(snapshot.CollisionTarget, snapshot.DanEntryParentName);
                    Transform danEndParent = FindChildTransform(snapshot.CollisionTarget, snapshot.DanEndParentName);
                    if (needsConstraints)
                    {
                        InvokeInstanceMethod(snapshot.Controller, "AddDanConstraints", nodeConstraintPlugin, danEntryParent, danEndParent);
                    }
                    InvokeInstanceMethod(snapshot.Controller, "CheckAutoTarget", nodeConstraintPlugin);
                }
            }
        }

        private static bool InvokeStaticPluginMethod(string methodName)
        {
            Type type = FindStudioPluginType();
            if (type == null)
            {
                return false;
            }

            MethodInfo method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null || method.GetParameters().Length != 0)
            {
                return false;
            }

            try
            {
                method.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage?.Value == true)
                {
                    StudioCharaEditor.Logger?.LogWarning($"BetterPenetration {methodName} call failed: {ex.Message}");
                }
                return false;
            }
        }

        private static object GetStaticFieldValue(string fieldName)
        {
            Type type = FindStudioPluginType();
            FieldInfo field = type?.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return field?.GetValue(null);
        }

        private static bool InvokeInstanceMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null)
            {
                return false;
            }

            MethodInfo method = FindInstanceMethod(instance.GetType(), methodName);
            if (method == null)
            {
                return false;
            }

            try
            {
                method.Invoke(instance, args);
                return true;
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage?.Value == true)
                {
                    StudioCharaEditor.Logger?.LogWarning($"BetterPenetration {methodName} call failed: {ex.Message}");
                }
                return false;
            }
        }

        private static ChaControl GetControllerOwner(object controller)
        {
            PropertyInfo property = FindInstanceProperty(controller.GetType(), "ChaControl");
            return property?.GetValue(controller, null) as ChaControl;
        }

        private static ChaControl GetCollisionTarget(object controller)
        {
            object collisionAgent = GetFieldValue(controller, "collisionAgent");
            return GetFieldValue(collisionAgent, "m_collisionCharacter") as ChaControl;
        }

        private static ChaControl GetConstraintTarget(object controller)
        {
            Transform parentTransform = GetConstraintParentTransform(controller, "danEntryConstraint");
            return parentTransform?.GetComponentInParent<ChaControl>();
        }

        private static bool GetBoolField(object instance, string fieldName)
        {
            object value = GetFieldValue(instance, fieldName);
            return value is bool boolValue && boolValue;
        }

        private static object GetFieldValue(object instance, string fieldName)
        {
            if (instance == null)
            {
                return null;
            }

            FieldInfo field = FindInstanceField(instance.GetType(), fieldName);
            return field?.GetValue(instance);
        }

        private static string GetConstraintParentName(object controller, string parentNameField, string constraintField)
        {
            string savedName = GetFieldValue(controller, parentNameField) as string;
            if (!string.IsNullOrEmpty(savedName))
            {
                return savedName;
            }

            if (!(GetFieldValue(controller, constraintField) is Array constraintParams) || constraintParams.Length <= 1)
            {
                return null;
            }

            object parentValue = constraintParams.GetValue(1);
            if (parentValue is string parentName)
            {
                return parentName;
            }

            return (parentValue as Transform)?.name;
        }

        private static Transform GetConstraintParentTransform(object controller, string constraintField)
        {
            if (!(GetFieldValue(controller, constraintField) is Array constraintParams) || constraintParams.Length <= 1)
            {
                return null;
            }

            return constraintParams.GetValue(1) as Transform;
        }

        private static void RestoreConstraintParentNames(ControllerSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot.DanEntryParentName))
            {
                SetFieldValue(snapshot.Controller, "danEntryParentName", snapshot.DanEntryParentName);
            }

            if (!string.IsNullOrEmpty(snapshot.DanEndParentName))
            {
                SetFieldValue(snapshot.Controller, "danEndParentName", snapshot.DanEndParentName);
            }
        }

        private static void RestoreConstraintFlags(ControllerSnapshot snapshot)
        {
            SetFieldValue(snapshot.Controller, "isKokan", snapshot.IsKokan);
            SetFieldValue(snapshot.Controller, "isAna", snapshot.IsAna);
            SetFieldValue(snapshot.Controller, "isOral", snapshot.IsOral);
        }

        private static Transform FindChildTransform(ChaControl chaCtrl, string transformName)
        {
            if (chaCtrl == null || string.IsNullOrEmpty(transformName))
            {
                return null;
            }

            Transform[] transforms = chaCtrl.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == transformName)
                {
                    return transforms[i];
                }
            }

            return null;
        }

        private static bool ParentNameIsKokan(string parentName)
        {
            return !string.IsNullOrEmpty(parentName) && parentName.Contains("Vagina");
        }

        private static bool ParentNameIsAna(string parentName)
        {
            return !string.IsNullOrEmpty(parentName) && parentName.Contains("Ana");
        }

        private static bool ParentNameIsOral(string parentName)
        {
            return !string.IsNullOrEmpty(parentName) && parentName.Contains("Mouth");
        }

        private static void SetFieldValue(object instance, string fieldName, object value)
        {
            if (instance == null)
            {
                return;
            }

            FieldInfo field = FindInstanceField(instance.GetType(), fieldName);
            field?.SetValue(instance, value);
        }

        private static FieldInfo FindInstanceField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static PropertyInfo FindInstanceProperty(Type type, string propertyName)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperty(
                    propertyName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null)
                {
                    return property;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static MethodInfo FindInstanceMethod(Type type, string methodName)
        {
            while (type != null)
            {
                MethodInfo method = type.GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method != null)
                {
                    return method;
                }

                type = type.BaseType;
            }

            return null;
        }

        private static Type FindStudioPluginType()
        {
            return FindType(CurrentStudioBetterPenetrationTypeName) ??
                   FindType(HS2StudioBetterPenetrationTypeName);
        }

        private static Type FindType(string typeName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
