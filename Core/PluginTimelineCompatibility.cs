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
        private static bool populated;
        private static OCIChar selectedTarget;
        private static DetailParameter selectedParameter;

        private enum DetailValueKind
        {
            Float,
            Color,
            Bool,
            Int,
        }

        internal static void PopulateTimeline()
        {
            if (populated || !IsTimelineAvailable())
            {
                return;
            }

            try
            {
                TimelineCompatibility.AddInterpolableModelDynamic<float, DetailParameter>(
                    owner: Owner,
                    id: "detailFloat",
                    name: "Character Value",
                    interpolateBefore: (oci, parameter, leftValue, rightValue, factor) => SetDetailValue(oci, parameter, Mathf.LerpUnclamped(leftValue, rightValue, factor)),
                    interpolateAfter: null,
                    getValue: (oci, parameter) => Convert.ToSingle(GetDetailValue(oci, parameter)),
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
                    getValue: (oci, parameter) => (Color)GetDetailValue(oci, parameter),
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
                    getValue: (oci, parameter) => Convert.ToInt32(GetDetailValue(oci, parameter)),
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
                StudioCharaEditor.Logger?.LogWarning("Failed to register Timeline interpolables: " + ex.Message);
            }
        }

        internal static bool IsTimelineAvailable()
        {
            try
            {
                return TimelineCompatibility.IsTimelineAvailable();
            }
            catch
            {
                return false;
            }
        }

        internal static bool CanSelect(OCIChar ociTarget, ChaControl chaCtrl, CharaDetailInfo detailInfo)
        {
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
                   detailInfo?.DetailDefine != null &&
                   selectedParameter.Key == detailInfo.DetailDefine.Key;
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
            return new DetailParameter(key, displayName, kind);
        }

        private static void WriteParameterToXml(ObjectCtrlInfo oci, XmlTextWriter writer, DetailParameter parameter)
        {
            writer.WriteAttributeString("key", parameter.Key);
            writer.WriteAttributeString("name", parameter.DisplayName);
            writer.WriteAttributeString("kind", parameter.Kind.ToString());
        }

        private static string ReadAttribute(XmlNode node, string name, string fallback)
        {
            XmlAttribute attribute = node?.Attributes?[name];
            return attribute == null ? fallback : attribute.Value;
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
            object currentValue = detail.Get != null ? detail.Get(chaCtrl) : null;
            if (CharaEditorController.DataValueEqual(currentValue, value))
            {
                return;
            }

            detail.Set(chaCtrl, value);
            detail.Upd?.Invoke(chaCtrl);
        }

        private sealed class DetailParameter
        {
            internal readonly string Key;
            internal readonly string DisplayName;
            internal readonly DetailValueKind Kind;
            private readonly int hashCode;

            internal DetailParameter(string key, string displayName, DetailValueKind kind)
            {
                Key = key ?? string.Empty;
                DisplayName = string.IsNullOrEmpty(displayName) ? Key : displayName;
                Kind = kind;

                unchecked
                {
                    hashCode = 17;
                    hashCode = hashCode * 31 + Key.GetHashCode();
                    hashCode = hashCode * 31 + Kind.GetHashCode();
                }
            }

            public override bool Equals(object obj)
            {
                DetailParameter other = obj as DetailParameter;
                return other != null && Key == other.Key && Kind == other.Kind;
            }

            public override int GetHashCode()
            {
                return hashCode;
            }
        }
    }
}
