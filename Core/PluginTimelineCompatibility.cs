using AIChara;
using KKAPI.Utilities;
using Studio;
using System;
using System.Xml;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginTimelineCompatibility
    {
        private const string Owner = "Studio Chara Editor";
        private const float AvailabilityRetryInterval = 2f;
        private static bool populated;
        private static bool populateAttempted;
        private static bool availabilityChecked;
        private static bool availabilityException;
        private static bool timelineAvailable;
        private static float lastAvailabilityCheckTime;
        private static OCIChar selectedTarget;
        private static DetailParameter selectedParameter;

        private enum DetailValueKind
        {
            Float,
            Color,
            Bool,
            Int,
        }

        private enum DetailValueMode
        {
            Direct,
            Abmx,
        }

        internal static void PopulateTimeline()
        {
            if (populated || populateAttempted || !IsTimelineAvailable())
            {
                return;
            }

            populateAttempted = true;
            try
            {
                TimelineCompatibility.AddInterpolableModelDynamic<float, DetailParameter>(
                    owner: Owner,
                    id: "detailFloat",
                    name: "Character Value",
                    interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => SetDetailValue(oci, parameter, Mathf.LerpUnclamped(leftValue, rightValue, factor)),
                    interpolateAfter: null,
                    getValue: (oci, parameter) =>
                    {
                        object value = GetDetailValue(oci, parameter);
                        return value == null ? 0f : Convert.ToSingle(value);
                    },
                    readValueFromXml: (parameter, node) => XmlConvert.ToSingle(node.Attributes["value"].Value),
                    writeValueToXml: (parameter, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString(value)),
                    getParameter: GetSelectedParameter,
                    readParameterFromXml: (oci, node) => ReadParameterFromXml(node, DetailValueKind.Float),
                    writeParameterToXml: WriteParameterToXml,
                    checkIntegrity: (oci, parameter, leftValue, rightValue) => CheckIntegrity(oci, parameter, DetailValueKind.Float),
                    getFinalName: GetFinalName,
                    isCompatibleWithTarget: oci => IsCompatibleWithSelectedTarget(oci, DetailValueKind.Float));

                TimelineCompatibility.AddInterpolableModelDynamic<Color, DetailParameter>(
                    owner: Owner,
                    id: "detailColor",
                    name: "Character Color",
                    interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => SetDetailValue(oci, parameter, Color.LerpUnclamped(leftValue, rightValue, factor)),
                    interpolateAfter: null,
                    getValue: (oci, parameter) =>
                    {
                        object value = GetDetailValue(oci, parameter);
                        return value is Color color ? color : Color.white;
                    },
                    readValueFromXml: (parameter, node) => new Color(
                        XmlConvert.ToSingle(node.Attributes["r"].Value),
                        XmlConvert.ToSingle(node.Attributes["g"].Value),
                        XmlConvert.ToSingle(node.Attributes["b"].Value),
                        XmlConvert.ToSingle(node.Attributes["a"].Value)),
                    writeValueToXml: (parameter, writer, value) =>
                    {
                        writer.WriteAttributeString("r", XmlConvert.ToString(value.r));
                        writer.WriteAttributeString("g", XmlConvert.ToString(value.g));
                        writer.WriteAttributeString("b", XmlConvert.ToString(value.b));
                        writer.WriteAttributeString("a", XmlConvert.ToString(value.a));
                    },
                    getParameter: GetSelectedParameter,
                    readParameterFromXml: (oci, node) => ReadParameterFromXml(node, DetailValueKind.Color),
                    writeParameterToXml: WriteParameterToXml,
                    checkIntegrity: (oci, parameter, leftValue, rightValue) => CheckIntegrity(oci, parameter, DetailValueKind.Color),
                    getFinalName: GetFinalName,
                    isCompatibleWithTarget: oci => IsCompatibleWithSelectedTarget(oci, DetailValueKind.Color));

                TimelineCompatibility.AddInterpolableModelDynamic<bool, DetailParameter>(
                    owner: Owner,
                    id: "detailBool",
                    name: "Character Toggle",
                    interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => SetDetailValue(oci, parameter, leftValue),
                    interpolateAfter: null,
                    getValue: (oci, parameter) => CharaDetailDefine.ParseBool(GetDetailValue(oci, parameter)),
                    readValueFromXml: (parameter, node) => XmlConvert.ToBoolean(node.Attributes["value"].Value),
                    writeValueToXml: (parameter, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString(value)),
                    getParameter: GetSelectedParameter,
                    readParameterFromXml: (oci, node) => ReadParameterFromXml(node, DetailValueKind.Bool),
                    writeParameterToXml: WriteParameterToXml,
                    checkIntegrity: (oci, parameter, leftValue, rightValue) => CheckIntegrity(oci, parameter, DetailValueKind.Bool),
                    getFinalName: GetFinalName,
                    isCompatibleWithTarget: oci => IsCompatibleWithSelectedTarget(oci, DetailValueKind.Bool));

                TimelineCompatibility.AddInterpolableModelDynamic<int, DetailParameter>(
                    owner: Owner,
                    id: "detailInt",
                    name: "Character Status",
                    interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => SetDetailValue(oci, parameter, leftValue),
                    interpolateAfter: null,
                    getValue: (oci, parameter) =>
                    {
                        object value = GetDetailValue(oci, parameter);
                        return value == null ? 0 : Convert.ToInt32(value);
                    },
                    readValueFromXml: (parameter, node) => XmlConvert.ToInt32(node.Attributes["value"].Value),
                    writeValueToXml: (parameter, writer, value) => writer.WriteAttributeString("value", XmlConvert.ToString(value)),
                    getParameter: GetSelectedParameter,
                    readParameterFromXml: (oci, node) => ReadParameterFromXml(node, DetailValueKind.Int),
                    writeParameterToXml: WriteParameterToXml,
                    checkIntegrity: (oci, parameter, leftValue, rightValue) => CheckIntegrity(oci, parameter, DetailValueKind.Int),
                    getFinalName: GetFinalName,
                    isCompatibleWithTarget: oci => IsCompatibleWithSelectedTarget(oci, DetailValueKind.Int));

                populated = true;
            }
            catch (Exception ex)
            {
                timelineAvailable = false;
                availabilityChecked = true;
                availabilityException = true;
                StudioCharaEditor.Logger?.LogWarning("Failed to register Timeline interpolables: " + ex.Message);
            }
        }

        internal static bool IsTimelineAvailable()
        {
            if (availabilityChecked)
            {
                if (timelineAvailable || availabilityException)
                {
                    return timelineAvailable;
                }

                if (Time.realtimeSinceStartup - lastAvailabilityCheckTime < AvailabilityRetryInterval)
                {
                    return timelineAvailable;
                }
            }

            availabilityChecked = true;
            lastAvailabilityCheckTime = Time.realtimeSinceStartup;
            try
            {
                timelineAvailable = TimelineCompatibility.IsTimelineAvailable();
                availabilityException = false;
            }
            catch (Exception ex)
            {
                timelineAvailable = false;
                availabilityException = true;
                if (StudioCharaEditor.VerboseMessage?.Value == true)
                {
                    StudioCharaEditor.Logger?.LogWarning("Timeline compatibility unavailable: " + ex.Message);
                }
            }

            return timelineAvailable;
        }

        internal static bool CanSelect(OCIChar ociTarget, ChaControl chaCtrl, CharaDetailInfo detailInfo)
        {
            if (!IsTimelineAvailable())
            {
                return false;
            }

            PopulateTimeline();
            return populated &&
                   ociTarget != null &&
                   ociTarget.charInfo == chaCtrl &&
                   TryCreateParameter(chaCtrl, detailInfo, string.Empty, out DetailParameter parameter) &&
                   TryGetDetailInfo(ociTarget, parameter, out _);
        }

        internal static bool IsSelected(OCIChar ociTarget, CharaDetailInfo detailInfo)
        {
            return selectedTarget == ociTarget &&
                   selectedParameter != null &&
                   selectedParameter.Mode == DetailValueMode.Direct &&
                   detailInfo?.DetailDefine != null &&
                   selectedParameter.Key == detailInfo.DetailDefine.Key;
        }

        internal static bool CanSelectAbmx(OCIChar ociTarget, ChaControl chaCtrl, CharaDetailInfo detailInfo, int subSliderIndex)
        {
            if (!IsTimelineAvailable())
            {
                return false;
            }

            PopulateTimeline();
            return populated &&
                   ociTarget != null &&
                   ociTarget.charInfo == chaCtrl &&
                   TryCreateAbmxParameter(detailInfo, string.Empty, subSliderIndex, out DetailParameter parameter) &&
                   TryGetDetailInfo(ociTarget, parameter, out _);
        }

        internal static bool IsSelectedAbmx(OCIChar ociTarget, CharaDetailInfo detailInfo, int subSliderIndex)
        {
            return selectedTarget == ociTarget &&
                   selectedParameter != null &&
                   selectedParameter.Mode == DetailValueMode.Abmx &&
                   detailInfo?.DetailDefine != null &&
                   selectedParameter.Key == detailInfo.DetailDefine.Key &&
                   selectedParameter.SubIndex == subSliderIndex &&
                   MatchesCurrentAbmxSelection(detailInfo.DetailDefine, selectedParameter);
        }

        internal static void SelectAbmxInterpolable(OCIChar ociTarget, CharaDetailInfo detailInfo, string displayName, int subSliderIndex)
        {
            PopulateTimeline();
            if (!populated || !TryCreateAbmxParameter(detailInfo, displayName, subSliderIndex, out DetailParameter parameter))
            {
                return;
            }

            selectedTarget = ociTarget;
            selectedParameter = parameter;

            try
            {
                TimelineCompatibility.RefreshInterpolablesList();
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to refresh Timeline interpolables: " + ex.Message);
                }
            }
        }

        internal static void SelectInterpolable(OCIChar ociTarget, ChaControl chaCtrl, string displayName, CharaDetailInfo detailInfo)
        {
            PopulateTimeline();
            if (!populated || !TryCreateParameter(chaCtrl, detailInfo, displayName, out DetailParameter parameter))
            {
                return;
            }

            selectedTarget = ociTarget;
            selectedParameter = parameter;

            try
            {
                TimelineCompatibility.RefreshInterpolablesList();
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to refresh Timeline interpolables: " + ex.Message);
                }
            }
        }

        private static bool TryCreateParameter(ChaControl chaCtrl, CharaDetailInfo detailInfo, string displayName, out DetailParameter parameter)
        {
            parameter = null;
            CharaDetailDefine detail = detailInfo?.DetailDefine;
            if (chaCtrl == null || detail == null || detail.Get == null || detail.Set == null || string.IsNullOrEmpty(detail.Key))
            {
                return false;
            }

            if (detail.Key.IndexOf(CharaEditorController.KEY_SEP_CHAR[0]) < 0)
            {
                return false;
            }

            if (!TryGetKind(chaCtrl, detail, out DetailValueKind kind))
            {
                return false;
            }

            parameter = new DetailParameter(detail.Key, string.IsNullOrEmpty(displayName) ? detail.Key : displayName, kind);
            return true;
        }

        private static bool TryCreateAbmxParameter(CharaDetailInfo detailInfo, string displayName, int subSliderIndex, out DetailParameter parameter)
        {
            parameter = null;
            CharaDetailDefine detail = detailInfo?.DetailDefine;
            CharaABMXDetailDefine1 abmxDefine = detail as CharaABMXDetailDefine1;
            if (detail == null || abmxDefine == null || detail.Get == null || detail.Set == null || string.IsNullOrEmpty(detail.Key) || subSliderIndex < 0)
            {
                return false;
            }

            if (abmxDefine.SubSlidersNames == null || subSliderIndex >= abmxDefine.SubSlidersNames.Length)
            {
                return false;
            }

            int targetIndex = 0;
            int fingerIndex = 0;
            int segmentIndex = 0;
            CharaABMXDetailDefine2 abmx2 = detail as CharaABMXDetailDefine2;
            if (abmx2 != null)
            {
                targetIndex = abmx2.curTargetIndex;
            }

            CharaABMXDetailDefine3 abmx3 = detail as CharaABMXDetailDefine3;
            if (abmx3 != null)
            {
                fingerIndex = abmx3.curFingerIndex;
                segmentIndex = abmx3.curSegmentIndex;
            }

            parameter = new DetailParameter(
                detail.Key,
                string.IsNullOrEmpty(displayName) ? detail.Key + " " + abmxDefine.SubSlidersNames[subSliderIndex] : displayName,
                DetailValueKind.Float,
                DetailValueMode.Abmx,
                subSliderIndex,
                targetIndex,
                fingerIndex,
                segmentIndex);
            return true;
        }

        private static bool TryGetKind(ChaControl chaCtrl, CharaDetailDefine detail, out DetailValueKind kind)
        {
            kind = DetailValueKind.Float;
            switch (detail.Type)
            {
                case CharaDetailDefine.CharaDetailDefineType.SLIDER:
                case CharaDetailDefine.CharaDetailDefineType.VALUEINPUT:
                    if (detail.Get(chaCtrl) is float)
                    {
                        kind = DetailValueKind.Float;
                        return true;
                    }
                    break;
                case CharaDetailDefine.CharaDetailDefineType.COLOR:
                    if (detail.Get(chaCtrl) is Color)
                    {
                        kind = DetailValueKind.Color;
                        return true;
                    }
                    break;
                case CharaDetailDefine.CharaDetailDefineType.TOGGLE:
                    kind = DetailValueKind.Bool;
                    return true;
                case CharaDetailDefine.CharaDetailDefineType.INT_STATUS:
                    kind = DetailValueKind.Int;
                    return true;
            }
            return false;
        }

        private static DetailParameter GetSelectedParameter(ObjectCtrlInfo oci)
        {
            return selectedParameter;
        }

        private static DetailParameter ReadParameterFromXml(XmlNode node, DetailValueKind kind)
        {
            string key = ReadAttribute(node, "key", string.Empty);
            string displayName = ReadAttribute(node, "name", key);
            DetailValueMode mode = ReadEnumAttribute(node, "mode", DetailValueMode.Direct);
            int subIndex = ReadIntAttribute(node, "subIndex", -1);
            int targetIndex = ReadIntAttribute(node, "targetIndex", 0);
            int fingerIndex = ReadIntAttribute(node, "fingerIndex", 0);
            int segmentIndex = ReadIntAttribute(node, "segmentIndex", 0);
            return new DetailParameter(key, displayName, kind, mode, subIndex, targetIndex, fingerIndex, segmentIndex);
        }

        private static void WriteParameterToXml(ObjectCtrlInfo oci, XmlTextWriter writer, DetailParameter parameter)
        {
            writer.WriteAttributeString("key", parameter.Key);
            writer.WriteAttributeString("name", parameter.DisplayName);
            writer.WriteAttributeString("kind", parameter.Kind.ToString());
            writer.WriteAttributeString("mode", parameter.Mode.ToString());
            writer.WriteAttributeString("subIndex", XmlConvert.ToString(parameter.SubIndex));
            writer.WriteAttributeString("targetIndex", XmlConvert.ToString(parameter.TargetIndex));
            writer.WriteAttributeString("fingerIndex", XmlConvert.ToString(parameter.FingerIndex));
            writer.WriteAttributeString("segmentIndex", XmlConvert.ToString(parameter.SegmentIndex));
        }

        private static string ReadAttribute(XmlNode node, string name, string fallback)
        {
            XmlAttribute attribute = node?.Attributes?[name];
            return attribute == null ? fallback : attribute.Value;
        }

        private static int ReadIntAttribute(XmlNode node, string name, int fallback)
        {
            XmlAttribute attribute = node?.Attributes?[name];
            if (attribute == null || !int.TryParse(attribute.Value, out int value))
            {
                return fallback;
            }
            return value;
        }

        private static TEnum ReadEnumAttribute<TEnum>(XmlNode node, string name, TEnum fallback) where TEnum : struct
        {
            XmlAttribute attribute = node?.Attributes?[name];
            if (attribute == null || !Enum.TryParse(attribute.Value, out TEnum value))
            {
                return fallback;
            }
            return value;
        }

        private static string GetFinalName(string currentName, ObjectCtrlInfo oci, DetailParameter parameter)
        {
            string suffix = parameter == null || string.IsNullOrEmpty(parameter.DisplayName) ? parameter?.Key : parameter.DisplayName;
            return string.IsNullOrEmpty(suffix) ? currentName : currentName + ": " + suffix;
        }

        private static bool IsCompatibleWithSelectedTarget(ObjectCtrlInfo oci, DetailValueKind kind)
        {
            return selectedParameter != null &&
                   selectedParameter.Kind == kind &&
                   selectedTarget != null &&
                   ReferenceEquals(selectedTarget, oci) &&
                   CheckIntegrity(oci, selectedParameter, kind);
        }

        private static bool CheckIntegrity(ObjectCtrlInfo oci, DetailParameter parameter, DetailValueKind kind)
        {
            if (parameter == null || parameter.Kind != kind || !TryGetDetailInfo(oci, parameter, out CharaDetailInfo detailInfo))
            {
                return false;
            }

            if (parameter.Mode == DetailValueMode.Abmx)
            {
                return IsValidAbmxParameter(detailInfo.DetailDefine, parameter);
            }

            return detailInfo.DetailDefine.Get != null && detailInfo.DetailDefine.Set != null;
        }

        private static bool TryGetDetailInfo(ObjectCtrlInfo oci, DetailParameter parameter, out CharaDetailInfo detailInfo)
        {
            detailInfo = null;
            if (!(oci is OCIChar ociChar) || ociChar.charInfo == null || CharaEditorMgr.Instance == null || parameter == null || string.IsNullOrEmpty(parameter.Key))
            {
                return false;
            }

            CharaEditorController controller = CharaEditorMgr.Instance.GetEditorController(ociChar);
            return controller?.myDetailDict != null && controller.myDetailDict.TryGetValue(parameter.Key, out detailInfo);
        }

        private static bool CheckIntegrity<TValue>(ObjectCtrlInfo oci, DetailParameter parameter, TValue leftValue, TValue rightValue, DetailValueKind kind)
        {
            return CheckIntegrity(oci, parameter, kind) && leftValue != null && rightValue != null;
        }

        private static object GetDetailValue(ObjectCtrlInfo oci, DetailParameter parameter)
        {
            if (!TryGetDetailInfo(oci, parameter, out CharaDetailInfo detailInfo))
            {
                return null;
            }

            if (parameter.Mode == DetailValueMode.Abmx)
            {
                object dataset = detailInfo.DetailDefine.Get((oci as OCIChar).charInfo);
                return TryGetAbmxWorkSet(dataset, detailInfo.DetailDefine, parameter, out float[] workSet) && parameter.SubIndex < workSet.Length
                    ? workSet[parameter.SubIndex]
                    : 0f;
            }

            return detailInfo.DetailDefine.Get((oci as OCIChar).charInfo);
        }

        private static void SetDetailValue(ObjectCtrlInfo oci, DetailParameter parameter, object value)
        {
            if (!TryGetDetailInfo(oci, parameter, out CharaDetailInfo detailInfo))
            {
                return;
            }

            ChaControl chaCtrl = (oci as OCIChar).charInfo;
            CharaDetailDefine detail = detailInfo.DetailDefine;
            if (parameter.Mode == DetailValueMode.Abmx)
            {
                SetAbmxValue(chaCtrl, detailInfo, parameter, Convert.ToSingle(value));
                return;
            }

            object currentValue = detail.Get != null ? detail.Get(chaCtrl) : null;
            if (CharaEditorController.DataValueEqual(currentValue, value))
            {
                return;
            }

            detail.Set(chaCtrl, value);
            detail.Upd?.Invoke(chaCtrl);
        }

        private static bool IsValidAbmxParameter(CharaDetailDefine detail, DetailParameter parameter)
        {
            CharaABMXDetailDefine1 abmxDefine = detail as CharaABMXDetailDefine1;
            return abmxDefine != null &&
                   detail.Get != null &&
                   detail.Set != null &&
                   parameter.SubIndex >= 0 &&
                   abmxDefine.SubSlidersNames != null &&
                   parameter.SubIndex < abmxDefine.SubSlidersNames.Length;
        }

        private static bool MatchesCurrentAbmxSelection(CharaDetailDefine detail, DetailParameter parameter)
        {
            if (detail is CharaABMXDetailDefine3 abmx3)
            {
                return parameter.TargetIndex == abmx3.curTargetIndex &&
                       parameter.FingerIndex == abmx3.curFingerIndex &&
                       parameter.SegmentIndex == abmx3.curSegmentIndex;
            }

            if (detail is CharaABMXDetailDefine2 abmx2)
            {
                return parameter.TargetIndex == abmx2.curTargetIndex;
            }

            return true;
        }

        private static bool TryGetAbmxWorkSet(object dataset, CharaDetailDefine detail, DetailParameter parameter, out float[] workSet)
        {
            workSet = null;
            if (!IsValidAbmxParameter(detail, parameter))
            {
                return false;
            }

            switch (detail.Type)
            {
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET1:
                    workSet = dataset as float[];
                    return workSet != null && parameter.SubIndex < workSet.Length;
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET2:
                    if (dataset is float[][] symmetricData)
                    {
                        int targetIndex = Mathf.Clamp(parameter.TargetIndex == 0 ? 0 : parameter.TargetIndex - 1, 0, symmetricData.Length - 1);
                        workSet = symmetricData[targetIndex];
                        return workSet != null && parameter.SubIndex < workSet.Length;
                    }
                    break;
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET3:
                    if (dataset is float[][][][] fingerData)
                    {
                        int handIndex = Mathf.Clamp(parameter.TargetIndex == 0 ? 0 : parameter.TargetIndex - 1, 0, fingerData.Length - 1);
                        int fingerIndex = Mathf.Clamp(parameter.FingerIndex == 0 ? 0 : parameter.FingerIndex - 1, 0, fingerData[handIndex].Length - 1);
                        int segmentIndex = Mathf.Clamp(parameter.SegmentIndex, 0, fingerData[handIndex][fingerIndex].Length - 1);
                        workSet = fingerData[handIndex][fingerIndex][segmentIndex];
                        return workSet != null && parameter.SubIndex < workSet.Length;
                    }
                    break;
            }

            return false;
        }

        private static void SetAbmxValue(ChaControl chaCtrl, CharaDetailInfo detailInfo, DetailParameter parameter, float value)
        {
            CharaDetailDefine detail = detailInfo.DetailDefine;
            object dataset = detail.Get(chaCtrl);
            if (!TryGetAbmxWorkSet(dataset, detail, parameter, out float[] workSet))
            {
                return;
            }

            switch (detail.Type)
            {
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET1:
                    workSet[parameter.SubIndex] = value;
                    break;
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET2:
                    ApplyAbmxSymmetricValue((float[][])dataset, parameter, value);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET3:
                    ApplyAbmxFingerValue((float[][][][])dataset, parameter, value);
                    break;
            }

            ApplyAbmxSetWithSavedSelection(chaCtrl, detail, dataset, parameter);
        }

        private static void ApplyAbmxSymmetricValue(float[][] dataset, DetailParameter parameter, float value)
        {
            if (dataset == null)
            {
                return;
            }

            if (parameter.TargetIndex == 0)
            {
                for (int h = 0; h < dataset.Length; h++)
                {
                    SetArrayValue(dataset[h], parameter.SubIndex, value);
                }
            }
            else
            {
                int handIndex = parameter.TargetIndex - 1;
                if (handIndex >= 0 && handIndex < dataset.Length)
                {
                    SetArrayValue(dataset[handIndex], parameter.SubIndex, value);
                }
            }
        }

        private static void ApplyAbmxFingerValue(float[][][][] dataset, DetailParameter parameter, float value)
        {
            if (dataset == null)
            {
                return;
            }

            int firstHand = parameter.TargetIndex == 0 ? 0 : parameter.TargetIndex - 1;
            int lastHand = parameter.TargetIndex == 0 ? dataset.Length - 1 : firstHand;
            for (int h = Math.Max(0, firstHand); h <= Math.Min(dataset.Length - 1, lastHand); h++)
            {
                int firstFinger = parameter.FingerIndex == 0 ? 0 : parameter.FingerIndex - 1;
                int lastFinger = parameter.FingerIndex == 0 ? dataset[h].Length - 1 : firstFinger;
                for (int f = Math.Max(0, firstFinger); f <= Math.Min(dataset[h].Length - 1, lastFinger); f++)
                {
                    int segmentIndex = parameter.SegmentIndex;
                    if (segmentIndex >= 0 && segmentIndex < dataset[h][f].Length)
                    {
                        SetArrayValue(dataset[h][f][segmentIndex], parameter.SubIndex, value);
                    }
                }
            }
        }

        private static void SetArrayValue(float[] values, int index, float value)
        {
            if (values != null && index >= 0 && index < values.Length)
            {
                values[index] = value;
            }
        }

        private static void ApplyAbmxSetWithSavedSelection(ChaControl chaCtrl, CharaDetailDefine detail, object dataset, DetailParameter parameter)
        {
            if (detail is CharaABMXDetailDefine3 abmx3)
            {
                int oldTargetIndex = abmx3.curTargetIndex;
                int oldFingerIndex = abmx3.curFingerIndex;
                int oldSegmentIndex = abmx3.curSegmentIndex;
                try
                {
                    abmx3.curTargetIndex = parameter.TargetIndex;
                    abmx3.curFingerIndex = parameter.FingerIndex;
                    abmx3.curSegmentIndex = parameter.SegmentIndex;
                    detail.Set(chaCtrl, dataset);
                }
                finally
                {
                    abmx3.curTargetIndex = oldTargetIndex;
                    abmx3.curFingerIndex = oldFingerIndex;
                    abmx3.curSegmentIndex = oldSegmentIndex;
                }
                return;
            }

            if (detail is CharaABMXDetailDefine2 abmx2)
            {
                int oldTargetIndex = abmx2.curTargetIndex;
                try
                {
                    abmx2.curTargetIndex = parameter.TargetIndex;
                    detail.Set(chaCtrl, dataset);
                }
                finally
                {
                    abmx2.curTargetIndex = oldTargetIndex;
                }
                return;
            }

            detail.Set(chaCtrl, dataset);
        }

        private sealed class DetailParameter
        {
            internal readonly string Key;
            internal readonly string DisplayName;
            internal readonly DetailValueKind Kind;
            internal readonly DetailValueMode Mode;
            internal readonly int SubIndex;
            internal readonly int TargetIndex;
            internal readonly int FingerIndex;
            internal readonly int SegmentIndex;
            private readonly int hashCode;

            internal DetailParameter(
                string key,
                string displayName,
                DetailValueKind kind,
                DetailValueMode mode = DetailValueMode.Direct,
                int subIndex = -1,
                int targetIndex = 0,
                int fingerIndex = 0,
                int segmentIndex = 0)
            {
                Key = key ?? string.Empty;
                DisplayName = string.IsNullOrEmpty(displayName) ? Key : displayName;
                Kind = kind;
                Mode = mode;
                SubIndex = subIndex;
                TargetIndex = targetIndex;
                FingerIndex = fingerIndex;
                SegmentIndex = segmentIndex;

                unchecked
                {
                    hashCode = 17;
                    hashCode = hashCode * 31 + Key.GetHashCode();
                    hashCode = hashCode * 31 + Kind.GetHashCode();
                    hashCode = hashCode * 31 + Mode.GetHashCode();
                    hashCode = hashCode * 31 + SubIndex.GetHashCode();
                    hashCode = hashCode * 31 + TargetIndex.GetHashCode();
                    hashCode = hashCode * 31 + FingerIndex.GetHashCode();
                    hashCode = hashCode * 31 + SegmentIndex.GetHashCode();
                }
            }

            public override bool Equals(object obj)
            {
                DetailParameter other = obj as DetailParameter;
                return other != null &&
                       Key == other.Key &&
                       Kind == other.Kind &&
                       Mode == other.Mode &&
                       SubIndex == other.SubIndex &&
                       TargetIndex == other.TargetIndex &&
                       FingerIndex == other.FingerIndex &&
                       SegmentIndex == other.SegmentIndex;
            }

            public override int GetHashCode()
            {
                return hashCode;
            }
        }
    }
}
