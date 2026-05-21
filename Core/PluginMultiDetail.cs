using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AIChara;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginMultiDetail
    {
        private const string ControllerTypeName = "OptimizedFusionMod.MultiDetailMod.MultiDetailController";
        private const string DetailTargetTypeName = "OptimizedFusionMod.MultiDetailMod.DetailTarget";
        private const string PatchesTypeName = "OptimizedFusionMod.MultiDetailMod.MultiDetailPatches";
        private const string AssemblyName = "MultiDetailMod";
        private const string FaceDetailKey = "Face#FaceType#FaceDetailType";
        private const string BodyDetailKey = "Body#Skin#DetailType";
        private const int DeferredFullBlendFrames = 18;
        internal const int SlotCount = 3;

        private static bool initialized;
        private static Type controllerType;
        private static Type detailTargetType;
        private static MethodInfo getIdsMethod;
        private static MethodInfo getPowersMethod;
        private static MethodInfo markBlendDirtyMethod;
        private static MethodInfo applyNativeMethod;
        private static MethodInfo applyPowerOnlyMethod;
        private static MethodInfo setPowerMethod;
        private static MethodInfo applyFullBlendMethod;
        private static MethodInfo getExternalTexturesMethod;
        private static FieldInfo pendingBlendsField;
        private static object faceTarget;
        private static object bodyTarget;
        private static readonly Dictionary<string, int> deferredFullBlendVersions = new Dictionary<string, int>();

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
                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);
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
                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);
                return slotIndex < ids.Count && ids[slotIndex] is int id && id != 0 &&
                       slotIndex < powers.Count && powers[slotIndex] is float power
                    ? power
                    : 1f;
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
                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);

                bool changed = false;
                if (id == 0)
                {
                    if (slotIndex < ids.Count && ids[slotIndex] is int oldId && oldId != 0)
                    {
                        RemoveExternalTexture(controller, target, oldId);
                        ids[slotIndex] = 0;
                        if (slotIndex < powers.Count)
                        {
                            powers[slotIndex] = 1f;
                        }
                        changed = true;
                    }
                }
                else
                {
                    EnsureSlotCapacity(ids, powers, slotIndex + 1);
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (i != slotIndex && ids[i] is int existingId && existingId == id)
                        {
                            ids[i] = 0;
                            if (i < powers.Count)
                            {
                                powers[i] = 1f;
                            }
                            changed = true;
                        }
                    }

                    int previousId = ids[slotIndex] is int currentId ? currentId : 0;
                    if (previousId != id)
                    {
                        RemoveExternalTexture(controller, target, previousId);
                        ids[slotIndex] = id;
                        powers[slotIndex] = 1f;
                        changed = true;
                    }
                }

                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);
                if (!changed)
                {
                    return;
                }

                MarkBlendDirty(controller, target);
                applyNativeMethod?.Invoke(controller, new[] { target });
                ApplyFullBlend(chaCtrl, body, true);
                ScheduleFullBlendWhenReady(chaCtrl, body, true);
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
                TrimTrailingEmptySlots(ids, powers);
                SyncPowerCount(ids, powers);
                if (slotIndex >= ids.Count || !(ids[slotIndex] is int id) || id == 0)
                {
                    return;
                }

                float oldPower = slotIndex < powers.Count && powers[slotIndex] is float power ? power : 1f;
                if (Mathf.Approximately(oldPower, value))
                {
                    return;
                }

                InvokeControllerSetPower(controller, slotIndex, value, target);
                if (CountActiveIds(ids) > 1)
                {
                    ScheduleFullBlendWhenReady(chaCtrl, body, true);
                }
                else
                {
                    applyPowerOnlyMethod?.Invoke(controller, new[] { target });
                    if (IsBlendPending(chaCtrl, body))
                    {
                        ScheduleFullBlendWhenReady(chaCtrl, body, true);
                    }
                }
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
                       applyPowerOnlyMethod != null &&
                       applyFullBlendMethod != null;
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
            getExternalTexturesMethod = controllerType.GetMethod("GetExternalTextures", BindingFlags.Instance | BindingFlags.Public, null, new[] { detailTargetType }, null);
            MethodInfo controllerSetPowerMethod = controllerType.GetMethod("SetPower", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(float), detailTargetType }, null);
            Type patchesType = FindType(PatchesTypeName);
            applyFullBlendMethod = patchesType?.GetMethod("ApplyMultiDetailFull", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(ChaControl), typeof(Material), detailTargetType }, null);
            pendingBlendsField = patchesType?.GetField("_pendingBlends", BindingFlags.Static | BindingFlags.NonPublic);
            if (getIdsMethod == null ||
                getPowersMethod == null ||
                applyNativeMethod == null ||
                applyPowerOnlyMethod == null ||
                applyFullBlendMethod == null ||
                controllerSetPowerMethod == null)
            {
                return false;
            }

            setPowerMethod = controllerSetPowerMethod;
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

        private static void InvokeControllerSetPower(object controller, int slotIndex, float value, object target)
        {
            if (setPowerMethod != null)
            {
                setPowerMethod.Invoke(controller, new[] { (object)slotIndex, value, target });
                return;
            }

            IList powers = GetPowers(controller, target);
            if (slotIndex >= 0 && slotIndex < powers.Count)
            {
                powers[slotIndex] = value;
                MarkBlendDirty(controller, target);
            }
        }

        private static void MarkBlendDirty(object controller, object target)
        {
            markBlendDirtyMethod?.Invoke(controller, new[] { target });
        }

        private static void ApplyFullBlend(ChaControl chaCtrl, bool body, bool forceDirty)
        {
            if (chaCtrl == null || !EnsureInitialized())
            {
                return;
            }

            object controller = GetOrCreateController(chaCtrl);
            object target = body ? bodyTarget : faceTarget;
            if (forceDirty)
            {
                MarkBlendDirty(controller, target);
            }

            Material material = GetTargetMaterial(chaCtrl, body);
            if (material == null)
            {
                return;
            }

            applyFullBlendMethod?.Invoke(null, new object[] { chaCtrl, material, target });
        }

        private static void ScheduleFullBlendWhenReady(ChaControl chaCtrl, bool body, bool forceDirty)
        {
            if (chaCtrl == null)
            {
                return;
            }

            string key = GetDeferredBlendKey(chaCtrl, body);
            deferredFullBlendVersions.TryGetValue(key, out int oldVersion);
            int version = oldVersion + 1;
            deferredFullBlendVersions[key] = version;
            CharaEditorMgr mgr = CharaEditorMgr.Instance;
            if (mgr == null)
            {
                ApplyFullBlend(chaCtrl, body, forceDirty);
                return;
            }

            mgr.RunAfterFrames(DeferredFullBlendFrames, () => ApplyFullBlendWhenReady(chaCtrl, body, forceDirty, key, version, 0));
        }

        private static void ApplyFullBlendWhenReady(ChaControl chaCtrl, bool body, bool forceDirty, string key, int version, int attempt)
        {
            if (!deferredFullBlendVersions.TryGetValue(key, out int currentVersion) || currentVersion != version)
            {
                return;
            }

            if (IsBlendPending(chaCtrl, body) && attempt < 20)
            {
                CharaEditorMgr.Instance?.RunAfterFrames(DeferredFullBlendFrames, () => ApplyFullBlendWhenReady(chaCtrl, body, forceDirty, key, version, attempt + 1));
                return;
            }

            deferredFullBlendVersions.Remove(key);
            ApplyFullBlend(chaCtrl, body, forceDirty);
        }

        private static string GetDeferredBlendKey(ChaControl chaCtrl, bool body)
        {
            int id = chaCtrl != null ? ((UnityEngine.Object)chaCtrl).GetInstanceID() : 0;
            return id + "|" + (body ? "Body" : "Face");
        }

        private static bool IsBlendPending(ChaControl chaCtrl, bool body)
        {
            if (chaCtrl == null || pendingBlendsField == null)
            {
                return false;
            }

            string key = ((UnityEngine.Object)chaCtrl).GetInstanceID() + "_" + (body ? "Body" : "Face");
            if (!(pendingBlendsField.GetValue(null) is IEnumerable pending))
            {
                return false;
            }

            foreach (object item in pending)
            {
                if (string.Equals(item as string, key, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Material GetTargetMaterial(ChaControl chaCtrl, bool body)
        {
            if (chaCtrl == null)
            {
                return null;
            }

            if (body)
            {
                return chaCtrl.customTexCtrlBody?.matDraw ?? chaCtrl.customMatBody;
            }

            return chaCtrl.customTexCtrlFace?.matDraw ?? chaCtrl.customMatFace;
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

        private static int CountActiveIds(IList ids)
        {
            int count = 0;
            if (ids == null)
            {
                return count;
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] is int id && id != 0)
                {
                    count++;
                }
            }

            return count;
        }

        private static void RemoveExternalTexture(object controller, object target, int id)
        {
            if (id >= 0 || getExternalTexturesMethod == null)
            {
                return;
            }

            if (getExternalTexturesMethod.Invoke(controller, new[] { target }) is IDictionary externalTextures)
            {
                externalTextures.Remove(id);
            }
        }

    }
}
