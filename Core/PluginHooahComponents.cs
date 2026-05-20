using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AIChara;
using HarmonyLib;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginHooahComponents
    {
        private const string DickControllerTypeName = "DickController";
        private const string NamespacedDickControllerTypeName = "HooahComponents.DickController";
        private const string BetterPenetrationToolsTypeName = "Core_BetterPenetration.Tools";
        private const int RebindThrottleFrames = 2;

        private static Harmony harmony;
        private static bool attemptedBetterPenetrationPatch;
        private static bool installedBetterPenetrationPatch;
        private static Type dickControllerType;
        private static FieldInfo dickControllerInstancesField;
        private static FieldInfo dickChainsField;
        private static MethodInfo collideWithCharacterMethod;
        private static readonly HashSet<ChaControl> scheduledCharacters = new HashSet<ChaControl>();
        private static readonly Dictionary<ChaControl, int> lastRebindFrames = new Dictionary<ChaControl, int>();

        public static void Initialize(Harmony pluginHarmony)
        {
            harmony = pluginHarmony;
            TryInstallBetterPenetrationPatch();
        }

        public static void ScheduleRebindDickColliders(ChaControl chaCtrl)
        {
            if (chaCtrl == null || !HasDickControllers())
            {
                return;
            }

            TryInstallBetterPenetrationPatch();
            RebindDickCollidersThrottled(chaCtrl);
            if (!scheduledCharacters.Add(chaCtrl))
            {
                return;
            }

            CharaEditorMgr.Instance?.RunAfterFrames(1, () => RebindDickColliders(chaCtrl));
            CharaEditorMgr.Instance?.RunAfterFrames(5, () => RebindDickColliders(chaCtrl));
            CharaEditorMgr.Instance?.RunAfterFrames(15, () => RebindDickColliders(chaCtrl));
            CharaEditorMgr.Instance?.RunAfterFrames(30, () => RebindDickColliders(chaCtrl));
            CharaEditorMgr.Instance?.RunAfterFrames(60, () =>
            {
                RebindDickColliders(chaCtrl);
                scheduledCharacters.Remove(chaCtrl);
                lastRebindFrames.Remove(chaCtrl);
            });
        }

        private static void TryInstallBetterPenetrationPatch()
        {
            if (harmony == null || attemptedBetterPenetrationPatch || installedBetterPenetrationPatch)
            {
                return;
            }

            Type toolsType = FindType(BetterPenetrationToolsTypeName);
            MethodInfo removeColliders = toolsType?.GetMethod(
                "RemoveCollidersFromCoordinate",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo afterRemoveColliders = AccessTools.Method(
                typeof(PluginHooahComponents),
                nameof(AfterBetterPenetrationRemoveColliders));

            if (removeColliders == null || afterRemoveColliders == null)
            {
                return;
            }

            try
            {
                HarmonyMethod postfix = new HarmonyMethod(afterRemoveColliders)
                {
                    priority = Priority.Last
                };
                harmony.Patch(removeColliders, postfix: postfix);
                installedBetterPenetrationPatch = true;
            }
            catch (Exception ex)
            {
                attemptedBetterPenetrationPatch = true;
                if (StudioCharaEditor.VerboseMessage?.Value == true)
                {
                    StudioCharaEditor.Logger?.LogWarning($"Failed to install Hooah/BP collider repair hook: {ex.Message}");
                }
            }
        }

        private static void AfterBetterPenetrationRemoveColliders(ChaControl character)
        {
            ScheduleRebindDickColliders(character);
        }

        private static void RebindDickColliders(ChaControl chaCtrl)
        {
            if (chaCtrl == null)
            {
                return;
            }

            List<object> controllers = GetDickControllers();
            if (controllers.Count == 0)
            {
                return;
            }

            List<DynamicBoneColliderBase> colliders = GetDickColliders(controllers);
            RemoveDickColliders(chaCtrl, colliders);

            for (int i = 0; i < controllers.Count; i++)
            {
                InvokeCollideWithCharacter(controllers[i], chaCtrl);
            }

            DedupeDickColliders(chaCtrl, colliders);
        }

        private static void RemoveDickColliders(ChaControl chaCtrl, List<DynamicBoneColliderBase> colliders)
        {
            if (colliders.Count == 0)
            {
                return;
            }

            DynamicBone[] dynamicBones = chaCtrl.gameObject.GetComponentsInChildren<DynamicBone>(true);
            for (int boneIndex = 0; boneIndex < dynamicBones.Length; boneIndex++)
            {
                DynamicBone bone = dynamicBones[boneIndex];
                if (bone?.m_Colliders == null)
                {
                    continue;
                }

                for (int i = bone.m_Colliders.Count - 1; i >= 0; i--)
                {
                    if (bone.m_Colliders[i] == null || colliders.Contains(bone.m_Colliders[i]))
                    {
                        bone.m_Colliders.RemoveAt(i);
                    }
                }
            }
        }

        private static void DedupeDickColliders(ChaControl chaCtrl, List<DynamicBoneColliderBase> colliders)
        {
            if (colliders.Count == 0)
            {
                return;
            }

            DynamicBone[] dynamicBones = chaCtrl.gameObject.GetComponentsInChildren<DynamicBone>(true);
            for (int boneIndex = 0; boneIndex < dynamicBones.Length; boneIndex++)
            {
                DynamicBone bone = dynamicBones[boneIndex];
                if (bone?.m_Colliders == null)
                {
                    continue;
                }

                HashSet<DynamicBoneColliderBase> seenDickColliders = new HashSet<DynamicBoneColliderBase>();
                for (int i = bone.m_Colliders.Count - 1; i >= 0; i--)
                {
                    DynamicBoneColliderBase collider = bone.m_Colliders[i];
                    if (collider == null)
                    {
                        bone.m_Colliders.RemoveAt(i);
                        continue;
                    }

                    if (colliders.Contains(collider) && !seenDickColliders.Add(collider))
                    {
                        bone.m_Colliders.RemoveAt(i);
                    }
                }
            }
        }

        private static void InvokeCollideWithCharacter(object controller, ChaControl chaCtrl)
        {
            try
            {
                collideWithCharacterMethod?.Invoke(controller, new object[] { chaCtrl });
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage?.Value == true)
                {
                    StudioCharaEditor.Logger?.LogWarning($"Failed to rebind Hooah scripted dick colliders: {ex.Message}");
                }
            }
        }

        private static void RebindDickCollidersThrottled(ChaControl chaCtrl)
        {
            int frame = Time.frameCount;
            if (lastRebindFrames.TryGetValue(chaCtrl, out int lastFrame) &&
                frame - lastFrame < RebindThrottleFrames)
            {
                return;
            }

            lastRebindFrames[chaCtrl] = frame;
            RebindDickColliders(chaCtrl);
        }

        private static List<object> GetDickControllers()
        {
            List<object> controllers = new List<object>();
            if (!HasDickControllers() ||
                !(dickControllerInstancesField.GetValue(null) is IEnumerable instances))
            {
                return controllers;
            }

            foreach (object instance in instances)
            {
                if (!(instance is Component component) || component == null)
                {
                    continue;
                }

                controllers.Add(instance);
            }

            return controllers;
        }

        private static List<DynamicBoneColliderBase> GetDickColliders(List<object> controllers)
        {
            List<DynamicBoneColliderBase> colliders = new List<DynamicBoneColliderBase>();

            foreach (object instance in controllers)
            {
                if (!(instance is Component component) || component == null)
                {
                    continue;
                }

                if (!(dickChainsField.GetValue(instance) is GameObject[] dickChains))
                {
                    continue;
                }

                for (int i = 0; i < dickChains.Length; i++)
                {
                    GameObject chain = dickChains[i];
                    DynamicBoneColliderBase collider = chain != null
                        ? chain.GetComponent<DynamicBoneColliderBase>()
                        : null;
                    if (collider != null && !colliders.Contains(collider))
                    {
                        colliders.Add(collider);
                    }
                }
            }

            return colliders;
        }

        private static bool HasDickControllers()
        {
            if (dickControllerType == null)
            {
                dickControllerType = FindType(DickControllerTypeName) ?? FindType(NamespacedDickControllerTypeName);
            }
            if (dickControllerType == null)
            {
                return false;
            }

            if (dickControllerInstancesField == null)
            {
                dickControllerInstancesField = dickControllerType.GetField(
                    "Instances",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }
            if (dickControllerInstancesField == null)
            {
                return false;
            }

            if (dickChainsField == null)
            {
                dickChainsField = FindInstanceField(dickControllerType, "dickChains");
            }

            if (collideWithCharacterMethod == null)
            {
                collideWithCharacterMethod = dickControllerType.GetMethod(
                    "CollideWithCharacter",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(ChaControl) },
                    null);
            }

            return dickChainsField != null && collideWithCharacterMethod != null;
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

        private static Type FindType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
