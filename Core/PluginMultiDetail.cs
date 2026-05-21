using System;
using System.Collections;
using System.Reflection;
using AIChara;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginMultiDetail
    {
        private const string ControllerTypeName = "OptimizedFusionMod.MultiDetailMod.MultiDetailController";
        private const string DetailTargetTypeName = "OptimizedFusionMod.MultiDetailMod.DetailTarget";
        private const string PluginTypeName = "OptimizedFusionMod.MultiDetailMod.MultiDetailPlugin";
        private const string AssemblyName = "MultiDetailMod";
        private const string FaceDetailKey = "Face#FaceType#FaceDetailType";
        private const string BodyDetailKey = "Body#Skin#DetailType";
        internal const int SlotCount = 3;

        private static bool initialized;
        private static Type controllerType;
        private static Type detailTargetType;
        private static Type pluginType;
        private static MethodInfo getIdsMethod;
        private static MethodInfo getPowersMethod;
        private static MethodInfo markBlendDirtyMethod;
        private static MethodInfo applyNativeMethod;
        private static MethodInfo applyPowerOnlyMethod;
        private static MethodInfo refreshStudioUiMethod;
        private static object faceTarget;
        private static object bodyTarget;

        internal static bool IsAvailable
        {
            get { return EnsureInitialized(); }
        }

        internal static bool IsNativeDetailSelector(string detailKey)
        {
            return string.Equals(detailKey, FaceDetailKey, StringComparison.Ordinal) ||
                   string.Equals(detailKey, BodyDetailKey, StringComparison.Ordinal);
        }

        internal static bool IsSlotDetailSelector(string detailKey)
        {
            return TryParseSlotKey(detailKey, out _, out _);
        }

        internal static bool IsPowerDetailSlider(string detailKey)
        {
            return TryParsePowerKey(detailKey, out _, out _);
        }

        internal static int GetSlot(ChaControl chaCtrl, bool body, int slotIndex)
        {
            if (chaCtrl == null || slotIndex < 0 || slotIndex >= SlotCount || !EnsureInitialized())
            {
                return 0;
            }

            try
            {
                object controller = GetOrCreateController(chaCtrl);
                object target = body ? bodyTarget : faceTarget;
                IList ids = GetIds(controller, target);
                IList powers = GetPowers(controller, target);
                SeedFromNativeDetailIfNeeded(chaCtrl, body, ids, powers);
                return slotIndex < ids.Count && ids[slotIndex] is int id ? id : 0;
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to read MultiDetail slot: " + (ex.InnerException ?? ex).Message);
                }
                return 0;
            }
        }

        internal static float GetPower(ChaControl chaCtrl, bool body, int slotIndex)
        {
            if (chaCtrl == null || slotIndex < 0 || slotIndex >= SlotCount || !EnsureInitialized())
            {
                return 1f;
            }

            try
            {
                object controller = GetOrCreateController(chaCtrl);
                object target = body ? bodyTarget : faceTarget;
                IList ids = GetIds(controller, target);
                IList powers = GetPowers(controller, target);
                SeedFromNativeDetailIfNeeded(chaCtrl, body, ids, powers);
                SyncPowerCount(ids, powers);
                return slotIndex < powers.Count && powers[slotIndex] is float power ? power : 1f;
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to read MultiDetail power: " + (ex.InnerException ?? ex).Message);
                }
                return 1f;
            }
        }

        internal static void SetSlot(ChaControl chaCtrl, bool body, int slotIndex, int id)
        {
            if (chaCtrl == null || slotIndex < 0 || slotIndex >= SlotCount || !EnsureInitialized())
            {
                return;
            }

            try
            {
                object controller = GetOrCreateController(chaCtrl);
                if (controller == null)
                {
                    return;
                }

                object target = body ? bodyTarget : faceTarget;
                IList ids = GetIds(controller, target);
                IList powers = GetPowers(controller, target);
                SeedFromNativeDetailIfNeeded(chaCtrl, body, ids, powers);
                EnsureSlotCapacity(ids, powers, slotIndex + 1);
                ids[slotIndex] = id;
                if (!(powers[slotIndex] is float))
                {
                    powers[slotIndex] = 1f;
                }

                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);
                markBlendDirtyMethod?.Invoke(controller, new[] { target });
                applyNativeMethod?.Invoke(controller, new[] { target });
                refreshStudioUiMethod?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning("Failed to set MultiDetail slot: " + (ex.InnerException ?? ex).Message);
            }
        }

        internal static void SetPower(ChaControl chaCtrl, bool body, int slotIndex, float value)
        {
            if (chaCtrl == null || slotIndex < 0 || slotIndex >= SlotCount || !EnsureInitialized())
            {
                return;
            }

            try
            {
                object controller = GetOrCreateController(chaCtrl);
                if (controller == null)
                {
                    return;
                }

                object target = body ? bodyTarget : faceTarget;
                IList ids = GetIds(controller, target);
                IList powers = GetPowers(controller, target);
                SeedFromNativeDetailIfNeeded(chaCtrl, body, ids, powers);
                EnsureSlotCapacity(ids, powers, slotIndex + 1);
                powers[slotIndex] = value;
                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);
                markBlendDirtyMethod?.Invoke(controller, new[] { target });
                applyPowerOnlyMethod?.Invoke(controller, new[] { target });
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning("Failed to set MultiDetail power: " + (ex.InnerException ?? ex).Message);
            }
        }

        private static bool EnsureInitialized()
        {
            if (initialized)
            {
                return controllerType != null &&
                       detailTargetType != null &&
                       getIdsMethod != null &&
                       getPowersMethod != null &&
                       applyNativeMethod != null &&
                       applyPowerOnlyMethod != null;
            }

            controllerType = FindType(ControllerTypeName);
            detailTargetType = FindType(DetailTargetTypeName);
            if (controllerType == null || detailTargetType == null)
            {
                return false;
            }

            getIdsMethod = controllerType.GetMethod("GetIds", BindingFlags.Instance | BindingFlags.Public, null, new[] { detailTargetType }, null);
            getPowersMethod = controllerType.GetMethod("GetPowers", BindingFlags.Instance | BindingFlags.Public, null, new[] { detailTargetType }, null);
            markBlendDirtyMethod = controllerType.GetMethod("MarkBlendDirty", BindingFlags.Instance | BindingFlags.Public, null, new[] { detailTargetType }, null);
            applyNativeMethod = controllerType.GetMethod("ApplyNative", BindingFlags.Instance | BindingFlags.Public, null, new[] { detailTargetType }, null);
            applyPowerOnlyMethod = controllerType.GetMethod("ApplyPowerOnly", BindingFlags.Instance | BindingFlags.Public, null, new[] { detailTargetType }, null);
            if (getIdsMethod == null || getPowersMethod == null || applyNativeMethod == null || applyPowerOnlyMethod == null)
            {
                return false;
            }

            pluginType = FindType(PluginTypeName);
            refreshStudioUiMethod = pluginType?.GetMethod("RefreshStudioUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            faceTarget = Enum.Parse(detailTargetType, "Face");
            bodyTarget = Enum.Parse(detailTargetType, "Body");
            initialized = true;
            return true;
        }

        private static Type FindType(string fullName)
        {
            Type type = Type.GetType(fullName + ", " + AssemblyName, false);
            if (type != null)
            {
                return type;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static bool TryParseSlotKey(string detailKey, out bool body, out int slotIndex)
        {
            body = false;
            slotIndex = -1;
            if (string.IsNullOrEmpty(detailKey))
            {
                return false;
            }

            const string facePrefix = "Face#FaceType#MultiDetail ";
            const string bodyPrefix = "Body#Skin#MultiDetail ";
            string slotText;
            if (detailKey.StartsWith(facePrefix, StringComparison.Ordinal))
            {
                slotText = detailKey.Substring(facePrefix.Length);
            }
            else if (detailKey.StartsWith(bodyPrefix, StringComparison.Ordinal))
            {
                body = true;
                slotText = detailKey.Substring(bodyPrefix.Length);
            }
            else
            {
                return false;
            }

            if (!int.TryParse(slotText, out int slotNumber))
            {
                return false;
            }

            slotIndex = slotNumber - 1;
            return slotIndex >= 0 && slotIndex < SlotCount;
        }

        private static bool TryParsePowerKey(string detailKey, out bool body, out int slotIndex)
        {
            body = false;
            slotIndex = -1;
            const string suffix = " Power";
            if (string.IsNullOrEmpty(detailKey) || !detailKey.EndsWith(suffix, StringComparison.Ordinal))
            {
                return false;
            }

            return TryParseSlotKey(detailKey.Substring(0, detailKey.Length - suffix.Length), out body, out slotIndex);
        }

        private static object GetOrCreateController(ChaControl chaCtrl)
        {
            GameObject gameObject = chaCtrl?.gameObject;
            if (gameObject == null || controllerType == null)
            {
                return null;
            }

            Component controller = gameObject.GetComponent(controllerType);
            return controller != null ? controller : gameObject.AddComponent(controllerType);
        }

        private static IList GetIds(object controller, object target)
        {
            return getIdsMethod.Invoke(controller, new[] { target }) as IList;
        }

        private static IList GetPowers(object controller, object target)
        {
            return getPowersMethod.Invoke(controller, new[] { target }) as IList;
        }

        private static void SeedFromNativeDetailIfNeeded(ChaControl chaCtrl, bool body, IList ids, IList powers)
        {
            if (chaCtrl == null || ids == null || powers == null || ids.Count > 0)
            {
                return;
            }

            int nativeId = body ? chaCtrl.fileBody.detailId : chaCtrl.fileFace.detailId;
            if (nativeId <= 0)
            {
                return;
            }

            ids.Add(nativeId);
            float nativePower = body ? chaCtrl.fileBody.detailPower : chaCtrl.fileFace.detailPower;
            powers.Add(nativePower > 0f ? nativePower : 1f);
        }

        private static void EnsureSlotCapacity(IList ids, IList powers, int count)
        {
            while (ids.Count < count)
            {
                ids.Add(0);
            }

            while (powers.Count < count)
            {
                powers.Add(1f);
            }
        }

        private static void SyncPowerCount(IList ids, IList powers)
        {
            while (powers.Count < ids.Count)
            {
                powers.Add(1f);
            }

            while (powers.Count > ids.Count)
            {
                powers.RemoveAt(powers.Count - 1);
            }
        }

        private static void TrimTrailingEmptySlots(IList ids, IList powers)
        {
            while (ids.Count > 0 && ids[ids.Count - 1] is int id && id == 0)
            {
                ids.RemoveAt(ids.Count - 1);
                if (powers.Count > ids.Count)
                {
                    powers.RemoveAt(powers.Count - 1);
                }
            }
        }

    }
}
