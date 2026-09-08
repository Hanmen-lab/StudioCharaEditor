using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using AIChara;
using UnityEngine;

namespace StudioCharaEditor
{
    /// <summary>
    /// Material Editor keeps hair edits outside ChaFileHair. The editor's normal
    /// hair clipboard therefore needs a small optional sidecar when ME is loaded.
    /// Reflection keeps Material Editor an optional dependency.
    /// </summary>
    internal static class PluginMaterialEditorClipboard
    {
        private const string PluginTypeName = "KK_Plugins.MaterialEditor.MaterialEditorPlugin";
        private const string ControllerTypeName = "KK_Plugins.MaterialEditor.MaterialEditorCharaController";
        private static readonly string[] HairPropertyLists =
        {
            "MaterialShaderList",
            "RendererPropertyList",
            "ProjectorPropertyList",
            "MaterialNamePropertyList",
            "MaterialFloatPropertyList",
            "MaterialKeywordPropertyList",
            "MaterialColorPropertyList",
            "MaterialTexturePropertyList",
            "MaterialCopyList"
        };

        internal sealed class HairSnapshot
        {
            internal readonly Dictionary<string, List<object>> Entries =
                new Dictionary<string, List<object>>(StringComparer.Ordinal);
            internal readonly Dictionary<int, byte[]> Textures =
                new Dictionary<int, byte[]>();
            internal readonly HashSet<int> Slots = new HashSet<int>();
        }

        internal static HairSnapshot Capture(ChaControl character, IEnumerable<int> slots)
        {
            HashSet<int> requestedSlots = slots == null
                ? new HashSet<int>()
                : new HashSet<int>(slots);
            if (character == null || requestedSlots.Count == 0)
            {
                return null;
            }

            try
            {
                object controller = GetController(character);
                if (controller == null)
                {
                    return null;
                }

                HairSnapshot snapshot = new HairSnapshot();
                snapshot.Slots.UnionWith(requestedSlots);
                HashSet<int> textureIds = new HashSet<int>();
                for (int fieldIndex = 0; fieldIndex < HairPropertyLists.Length; fieldIndex++)
                {
                    string fieldName = HairPropertyLists[fieldIndex];
                    IList source = FindField(controller.GetType(), fieldName)?.GetValue(controller) as IList;
                    List<object> captured = new List<object>();
                    if (source != null)
                    {
                        for (int itemIndex = 0; itemIndex < source.Count; itemIndex++)
                        {
                            object item = source[itemIndex];
                            if (!IsHairEntryForSlots(item, requestedSlots))
                            {
                                continue;
                            }

                            object clone = DeepClone(item, new Dictionary<object, object>(ReferenceComparer.Instance));
                            captured.Add(clone);
                            if (string.Equals(fieldName, "MaterialTexturePropertyList", StringComparison.Ordinal))
                            {
                                CollectTextureIds(clone, textureIds, new HashSet<object>(ReferenceComparer.Instance));
                            }
                        }
                    }
                    snapshot.Entries[fieldName] = captured;
                }

                IDictionary textureDictionary = FindField(controller.GetType(), "TextureDictionary")
                    ?.GetValue(controller) as IDictionary;
                if (textureDictionary != null)
                {
                    foreach (int textureId in textureIds)
                    {
                        if (!textureDictionary.Contains(textureId))
                        {
                            continue;
                        }
                        object container = textureDictionary[textureId];
                        byte[] data = container?.GetType().GetProperty(
                            "Data",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.GetValue(container, null) as byte[];
                        if (data != null)
                        {
                            snapshot.Textures[textureId] = (byte[])data.Clone();
                        }
                    }
                }

                return snapshot;
            }
            catch (Exception exception)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Could not copy Material Editor hair data: " +
                    GetInnermostMessage(exception));
                return null;
            }
        }

        internal static void ScheduleRestore(
            ChaControl character,
            HairSnapshot snapshot,
            IEnumerable<int> slots)
        {
            if (character == null || snapshot == null || StudioCharaEditor.Instance == null)
            {
                return;
            }

            HashSet<int> requestedSlots = new HashSet<int>(slots ?? new int[0]);
            requestedSlots.IntersectWith(snapshot.Slots);
            if (requestedSlots.Count == 0)
            {
                return;
            }

            StudioCharaEditor.Instance.StartCoroutine(
                RestoreAfterHairReload(character, snapshot, requestedSlots));
        }

        internal static void ReapplyCharacterMaterials(ChaControl character)
        {
            if (character == null)
            {
                return;
            }

            try
            {
                object controller = GetController(character);
                if (controller == null)
                {
                    return;
                }

                MethodInfo loadData = FindMethod(
                    controller.GetType(),
                    "LoadData",
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool));
                IEnumerator reload = loadData?.Invoke(
                    controller,
                    new object[] { false, false, false, true }) as IEnumerator;
                if (reload != null)
                {
                    // Material Editor owns this finite reload coroutine. Its
                    // Character flag reapplies edits to the newly created head
                    // without touching clothes, accessories, or hair.
                    character.StartCoroutine(reload);
                }
            }
            catch (Exception exception)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Could not reapply Material Editor character data after changing the head: " +
                    GetInnermostMessage(exception));
            }
        }

        private static IEnumerator RestoreAfterHairReload(
            ChaControl character,
            HairSnapshot snapshot,
            HashSet<int> slots)
        {
            // ChangeHair replaces renderers synchronously on most installs, but
            // Sideloader and compatibility plug-ins can finish over later frames.
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return null;

            if (character == null)
            {
                yield break;
            }

            try
            {
                object controller = GetController(character);
                if (controller == null)
                {
                    yield break;
                }

                Dictionary<int, int> textureRemap = RestoreTextures(controller, snapshot);
                int restoredEntries = 0;
                for (int fieldIndex = 0; fieldIndex < HairPropertyLists.Length; fieldIndex++)
                {
                    string fieldName = HairPropertyLists[fieldIndex];
                    IList destination = FindField(controller.GetType(), fieldName)?.GetValue(controller) as IList;
                    if (destination == null)
                    {
                        continue;
                    }

                    for (int itemIndex = destination.Count - 1; itemIndex >= 0; itemIndex--)
                    {
                        if (IsHairEntryForSlots(destination[itemIndex], slots))
                        {
                            destination.RemoveAt(itemIndex);
                        }
                    }

                    if (!snapshot.Entries.TryGetValue(fieldName, out List<object> sourceEntries))
                    {
                        continue;
                    }
                    for (int itemIndex = 0; itemIndex < sourceEntries.Count; itemIndex++)
                    {
                        object sourceEntry = sourceEntries[itemIndex];
                        if (!IsHairEntryForSlots(sourceEntry, slots))
                        {
                            continue;
                        }
                        object clone = DeepClone(
                            sourceEntry,
                            new Dictionary<object, object>(ReferenceComparer.Instance));
                        RemapTextureIds(
                            clone,
                            textureRemap,
                            new HashSet<object>(ReferenceComparer.Instance));
                        destination.Add(clone);
                        restoredEntries++;
                    }
                }

                MethodInfo loadData = FindMethod(
                    controller.GetType(),
                    "LoadData",
                    typeof(bool),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool));
                IEnumerator reload = loadData?.Invoke(
                    controller,
                    new object[] { false, false, true, false }) as IEnumerator;
                if (reload != null)
                {
                    character.StartCoroutine(reload);
                }

                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogInfo(
                        "Restored " + restoredEntries +
                        " Material Editor hair entries after clipboard paste.");
                }
            }
            catch (Exception exception)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Could not restore Material Editor hair data: " +
                    GetInnermostMessage(exception));
            }
        }

        private static Dictionary<int, int> RestoreTextures(
            object controller,
            HairSnapshot snapshot)
        {
            Dictionary<int, int> remap = new Dictionary<int, int>();
            IDictionary dictionary = FindField(controller.GetType(), "TextureDictionary")
                ?.GetValue(controller) as IDictionary;
            if (dictionary == null || snapshot.Textures.Count == 0)
            {
                return remap;
            }

            Type textureContainerType = ResolveLoadedType("KK_Plugins.TextureContainer");
            ConstructorInfo constructor = textureContainerType?.GetConstructor(new[] { typeof(byte[]) });
            if (constructor == null)
            {
                return remap;
            }

            int nextId = 0;
            foreach (object key in dictionary.Keys)
            {
                if (key is int id)
                {
                    nextId = Math.Max(nextId, id + 1);
                }
            }

            foreach (KeyValuePair<int, byte[]> texture in snapshot.Textures)
            {
                while (dictionary.Contains(nextId))
                {
                    nextId++;
                }
                dictionary.Add(nextId, constructor.Invoke(new object[] { (byte[])texture.Value.Clone() }));
                remap[texture.Key] = nextId;
                nextId++;
            }
            return remap;
        }

        private static object GetController(ChaControl character)
        {
            Type pluginType = ResolveLoadedType(PluginTypeName);
            MethodInfo getter = pluginType?.GetMethod(
                "GetCharaController",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(ChaControl) },
                null);
            object controller = getter?.Invoke(null, new object[] { character });
            if (controller != null)
            {
                return controller;
            }

            Type controllerType = ResolveLoadedType(ControllerTypeName);
            return controllerType == null ? null : character.GetComponent(controllerType);
        }

        private static bool IsHairEntryForSlots(object entry, HashSet<int> slots)
        {
            if (entry == null || slots == null)
            {
                return false;
            }
            FieldInfo objectTypeField = FindField(entry.GetType(), "ObjectType");
            FieldInfo slotField = FindField(entry.GetType(), "Slot");
            object objectType = objectTypeField?.GetValue(entry);
            object slotValue = slotField?.GetValue(entry);
            return objectType != null &&
                   string.Equals(objectType.ToString(), "Hair", StringComparison.OrdinalIgnoreCase) &&
                   slotValue != null && slots.Contains(Convert.ToInt32(slotValue));
        }

        private static void CollectTextureIds(
            object value,
            HashSet<int> ids,
            HashSet<object> visited)
        {
            TraverseTextureIds(value, ids, null, visited);
        }

        private static void RemapTextureIds(
            object value,
            Dictionary<int, int> remap,
            HashSet<object> visited)
        {
            TraverseTextureIds(value, null, remap, visited);
        }

        private static void TraverseTextureIds(
            object value,
            HashSet<int> collected,
            Dictionary<int, int> remap,
            HashSet<object> visited)
        {
            if (value == null)
            {
                return;
            }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type.IsValueType)
            {
                return;
            }
            if (!visited.Add(value))
            {
                return;
            }
            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    TraverseTextureIds(item, collected, remap, visited);
                }
                return;
            }

            foreach (FieldInfo field in GetInstanceFields(type))
            {
                object fieldValue = field.GetValue(value);
                bool textureIdField = string.Equals(field.Name, "TexID", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(field.Name, "parentTexID", StringComparison.OrdinalIgnoreCase);
                if (textureIdField && fieldValue != null)
                {
                    int oldId = Convert.ToInt32(fieldValue);
                    collected?.Add(oldId);
                    if (remap != null && remap.TryGetValue(oldId, out int newId))
                    {
                        field.SetValue(value, field.FieldType == typeof(int?) ? (int?)newId : newId);
                    }
                }
                else
                {
                    TraverseTextureIds(fieldValue, collected, remap, visited);
                }
            }
        }

        private static object DeepClone(object value, Dictionary<object, object> visited)
        {
            if (value == null)
            {
                return null;
            }
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type.IsValueType ||
                type == typeof(string) || value is UnityEngine.Object)
            {
                return value;
            }
            if (visited.TryGetValue(value, out object existing))
            {
                return existing;
            }
            if (value is Array sourceArray)
            {
                Array cloneArray = Array.CreateInstance(type.GetElementType(), sourceArray.Length);
                visited[value] = cloneArray;
                for (int index = 0; index < sourceArray.Length; index++)
                {
                    cloneArray.SetValue(DeepClone(sourceArray.GetValue(index), visited), index);
                }
                return cloneArray;
            }
            if (value is IList sourceList)
            {
                IList cloneList = Activator.CreateInstance(type) as IList;
                if (cloneList == null)
                {
                    return value;
                }
                visited[value] = cloneList;
                foreach (object item in sourceList)
                {
                    cloneList.Add(DeepClone(item, visited));
                }
                return cloneList;
            }

            object clone = FormatterServices.GetUninitializedObject(type);
            visited[value] = clone;
            foreach (FieldInfo field in GetInstanceFields(type))
            {
                field.SetValue(clone, DeepClone(field.GetValue(value), visited));
            }
            return clone;
        }

        private static IEnumerable<FieldInfo> GetInstanceFields(Type type)
        {
            while (type != null)
            {
                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int index = 0; index < fields.Length; index++)
                {
                    if (!fields[index].IsStatic)
                    {
                        yield return fields[index];
                    }
                }
                type = type.BaseType;
            }
        }

        private static FieldInfo FindField(Type type, string name)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            return null;
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameters)
        {
            while (type != null)
            {
                MethodInfo method = type.GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null,
                    parameters,
                    null);
                if (method != null)
                {
                    return method;
                }
                type = type.BaseType;
            }
            return null;
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

        private static string GetInnermostMessage(Exception exception)
        {
            while (exception?.InnerException != null)
            {
                exception = exception.InnerException;
            }
            return exception?.Message ?? "Unknown error";
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }
    }
}
