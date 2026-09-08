using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AIChara;
using KKABMX.Core;
using KoiSkinOverlayX;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginHSPE
    {
        private const string PoseControllerTypeName = "HSPE.PoseController";
        private const float HeadChangeTimeoutSeconds = 60f;
        private static readonly Dictionary<int, int> RefreshVersions = new Dictionary<int, int>();

        internal sealed class HeadChangeSnapshot
        {
            internal int FaceSkinId;
            internal float[] ShapeValues;
            internal int EyebrowPattern;
            internal float EyebrowOpen;
            internal int EyesPattern;
            internal float EyesOpen;
            internal bool EyesBlink;
            internal int MouthPattern;
            internal float MouthOpenMin;
            internal float MouthOpenMax;
            internal bool MouthFixed;
        }

        internal static HeadChangeSnapshot CaptureHeadChangeSnapshot(ChaControl chaCtrl)
        {
            if (chaCtrl?.fileFace == null || chaCtrl.fileStatus == null)
            {
                return null;
            }

            return new HeadChangeSnapshot
            {
                FaceSkinId = chaCtrl.fileFace.skinId,
                ShapeValues = chaCtrl.fileFace.shapeValueFace == null
                    ? null
                    : (float[])chaCtrl.fileFace.shapeValueFace.Clone(),
                EyebrowPattern = chaCtrl.fileStatus.eyebrowPtn,
                EyebrowOpen = chaCtrl.fileStatus.eyebrowOpenMax,
                EyesPattern = chaCtrl.fileStatus.eyesPtn,
                EyesOpen = chaCtrl.fileStatus.eyesOpenMax,
                EyesBlink = chaCtrl.fileStatus.eyesBlink,
                MouthPattern = chaCtrl.fileStatus.mouthPtn,
                MouthOpenMin = chaCtrl.fileStatus.mouthOpenMin,
                MouthOpenMax = chaCtrl.fileStatus.mouthOpenMax,
                MouthFixed = chaCtrl.fileStatus.mouthFixed
            };
        }

        internal static void ScheduleHeadRefresh(
            ChaControl chaCtrl,
            int expectedHeadId,
            GameObject previousHead,
            HeadChangeSnapshot snapshot,
            bool restoreFaceSkin)
        {
            if (chaCtrl == null || StudioCharaEditor.Instance == null)
            {
                return;
            }

            int key = chaCtrl.GetInstanceID();
            int version = RefreshVersions.TryGetValue(key, out int currentVersion) ? currentVersion + 1 : 1;
            RefreshVersions[key] = version;
            StudioCharaEditor.Instance.StartCoroutine(
                RefreshAfterHeadChange(
                    chaCtrl,
                    expectedHeadId,
                    previousHead,
                    snapshot,
                    restoreFaceSkin,
                    key,
                    version));
        }

        private static IEnumerator RefreshAfterHeadChange(
            ChaControl chaCtrl,
            int expectedHeadId,
            GameObject previousHead,
            HeadChangeSnapshot snapshot,
            bool restoreFaceSkin,
            int key,
            int version)
        {
            float deadline = Time.realtimeSinceStartup + HeadChangeTimeoutSeconds;
            while (chaCtrl != null && Time.realtimeSinceStartup < deadline)
            {
                if (!RefreshVersions.TryGetValue(key, out int currentVersion) || currentVersion != version)
                {
                    yield break;
                }

                if (HeadAndExpressionControllersAreReady(chaCtrl, expectedHeadId, previousHead))
                {
                    break;
                }

                yield return null;
            }

            if (!HeadAndExpressionControllersAreReady(chaCtrl, expectedHeadId, previousHead))
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning(
                        "Timed out waiting to refresh HS2PE after the head change.");
                }
                yield break;
            }

            RestoreVanillaFaceState(chaCtrl, snapshot, restoreFaceSkin);
            RefreshSkinOverlays(chaCtrl);
            RefreshAbmx(chaCtrl);
            RefreshHspeBlendShapes(chaCtrl);
            PluginMaterialEditorClipboard.ReapplyCharacterMaterials(chaCtrl);

            if (StudioCharaEditor.VerboseMessage.Value)
            {
                StudioCharaEditor.Logger?.LogInfo(
                    "Restored face state after changing the head to " + expectedHeadId + ".");
            }
        }

        private static void RestoreVanillaFaceState(
            ChaControl chaCtrl,
            HeadChangeSnapshot snapshot,
            bool restoreFaceSkin)
        {
            if (snapshot == null || chaCtrl?.fileFace == null || chaCtrl.fileStatus == null)
            {
                return;
            }

            try
            {
                if (restoreFaceSkin && chaCtrl.fileFace.skinId != snapshot.FaceSkinId)
                {
                    chaCtrl.fileFace.skinId = snapshot.FaceSkinId;
                    chaCtrl.AddUpdateCMFaceTexFlags(true, true, true, true, true, true, true);
                    chaCtrl.CreateFaceTexture();
                }

                if (snapshot.ShapeValues != null)
                {
                    chaCtrl.fileFace.shapeValueFace = (float[])snapshot.ShapeValues.Clone();
                    chaCtrl.ChangeCustomFaceWithoutCustomTexture();
                }

                chaCtrl.fileStatus.eyebrowPtn = snapshot.EyebrowPattern;
                chaCtrl.fileStatus.eyebrowOpenMax = snapshot.EyebrowOpen;
                chaCtrl.fileStatus.eyesPtn = snapshot.EyesPattern;
                chaCtrl.fileStatus.eyesOpenMax = snapshot.EyesOpen;
                chaCtrl.fileStatus.eyesBlink = snapshot.EyesBlink;
                chaCtrl.fileStatus.mouthPtn = snapshot.MouthPattern;
                chaCtrl.fileStatus.mouthOpenMin = snapshot.MouthOpenMin;
                chaCtrl.fileStatus.mouthOpenMax = snapshot.MouthOpenMax;
                chaCtrl.fileStatus.mouthFixed = snapshot.MouthFixed;

                chaCtrl.ChangeEyebrowPtn(snapshot.EyebrowPattern, false);
                chaCtrl.ChangeEyebrowOpenMax(snapshot.EyebrowOpen);
                chaCtrl.ChangeEyesPtn(snapshot.EyesPattern, false);
                chaCtrl.ChangeEyesOpenMax(snapshot.EyesOpen);
                chaCtrl.ChangeEyesBlinkFlag(snapshot.EyesBlink);
                chaCtrl.ChangeMouthPtn(snapshot.MouthPattern, false);
                chaCtrl.ChangeMouthOpenMin(snapshot.MouthOpenMin);
                chaCtrl.ChangeMouthOpenMax(snapshot.MouthOpenMax);
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Failed to restore the vanilla face state after changing the head: " +
                    (ex.InnerException ?? ex).Message);
            }
        }

        private static void RefreshSkinOverlays(ChaControl chaCtrl)
        {
            try
            {
                KoiSkinOverlayController controller =
                    chaCtrl.GetComponent<KoiSkinOverlayController>();
                controller?.UpdateTexture(TexType.Unknown);
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Failed to refresh skin overlays after changing the head: " +
                    (ex.InnerException ?? ex).Message);
            }
        }

        private static void RefreshAbmx(ChaControl chaCtrl)
        {
            try
            {
                BoneController controller = chaCtrl.GetComponent<BoneController>();
                if (controller?.BoneSearcher == null)
                {
                    return;
                }

                // ABMX caches the Transform belonging to the old head. Rebind
                // the existing modifiers instead of reloading card data, which
                // would discard unsaved Studio edits.
                controller.BoneSearcher.ClearCache(false);
                foreach (BoneModifier modifier in controller.GetAllModifiers())
                {
                    controller.BoneSearcher.AssignBone(modifier);
                }
                controller.NeedsBaselineUpdate = true;
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Failed to refresh ABMX after changing the head: " +
                    (ex.InnerException ?? ex).Message);
            }
        }

        private static void RefreshHspeBlendShapes(ChaControl chaCtrl)
        {
            try
            {
                Type poseControllerType = ResolveLoadedType(PoseControllerTypeName);
                if (poseControllerType == null)
                {
                    return;
                }

                Component poseController = chaCtrl.GetComponent(poseControllerType) ??
                                           chaCtrl.GetComponentInParent(poseControllerType);
                if (poseController == null)
                {
                    return;
                }

                FieldInfo editorField = FindInstanceField(poseController.GetType(), "_blendShapesEditor");
                object editor = editorField?.GetValue(poseController);
                MethodInfo refreshMethod = editor?.GetType().GetMethod(
                    "OnCharacterReplaced",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    Type.EmptyTypes,
                    null);
                refreshMethod?.Invoke(editor, null);
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Failed to refresh HS2PE after changing the head: " +
                    (ex.InnerException ?? ex).Message);
            }
        }

        private static bool HeadAndExpressionControllersAreReady(
            ChaControl chaCtrl,
            int expectedHeadId,
            GameObject previousHead)
        {
            if (chaCtrl == null || chaCtrl.objHead == null ||
                chaCtrl.objHead == previousHead || chaCtrl.fileFace == null ||
                chaCtrl.fileFace.headId != expectedHeadId ||
                chaCtrl.fbsCtrl == null || chaCtrl.eyebrowCtrl == null ||
                chaCtrl.eyesCtrl == null || chaCtrl.mouthCtrl == null)
            {
                return false;
            }

            FaceBlendShape activeController =
                chaCtrl.objHead.GetComponent<FaceBlendShape>();
            return activeController != null &&
                   ReferenceEquals(activeController, chaCtrl.fbsCtrl);
        }

        private static Type ResolveLoadedType(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
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
    }
}
