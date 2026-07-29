using CharaCustom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace StudioCharaEditor
{
    internal static class PluginSideloaderSource
    {
        private const string ResolverTypeName =
            "Sideloader.AutoResolver.UniversalAutoResolver, HS2_Sideloader";
        private const string SideloaderTypeName =
            "Sideloader.Sideloader, HS2_Sideloader";

        private static readonly Dictionary<long, string> ZipmodByItem =
            new Dictionary<long, string>();

        private static bool initialized;

        internal static string GetZipmodFileName(CustomSelectInfo info)
        {
            if (info == null)
            {
                return null;
            }

            EnsureInitialized();
            ZipmodByItem.TryGetValue(BuildKey(info.category, info.id), out string fileName);
            return fileName;
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            try
            {
                Type resolverType = Type.GetType(ResolverTypeName, false);
                Type sideloaderType = Type.GetType(SideloaderTypeName, false);
                IEnumerable resolveInfos = GetStaticMemberValue(
                    resolverType,
                    "LoadedResolutionInfo") as IEnumerable;
                IDictionary zipArchives = GetStaticMemberValue(
                    sideloaderType,
                    "ZipArchives") as IDictionary;

                if (resolveInfos == null || zipArchives == null)
                {
                    return;
                }

                foreach (object resolveInfo in resolveInfos)
                {
                    if (resolveInfo == null)
                    {
                        continue;
                    }

                    string guid = GetMemberValue(resolveInfo, "GUID") as string;
                    if (string.IsNullOrEmpty(guid) || !zipArchives.Contains(guid))
                    {
                        continue;
                    }

                    int category = GetIntMember(resolveInfo, "CategoryNo", int.MinValue);
                    int localSlot = GetIntMember(resolveInfo, "LocalSlot", int.MinValue);
                    if (category == int.MinValue || localSlot == int.MinValue)
                    {
                        continue;
                    }

                    string archivePath = GetArchivePath(zipArchives[guid]);
                    if (!string.IsNullOrEmpty(archivePath))
                    {
                        ZipmodByItem[BuildKey(category, localSlot)] =
                            Path.GetFileName(archivePath);
                    }
                }
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning(
                        "Failed to build Sideloader source lookup: " + ex.Message);
                }
            }
        }

        private static long BuildKey(int category, int localSlot)
        {
            return ((long)(uint)category << 32) | (uint)localSlot;
        }

        private static object GetStaticMemberValue(Type type, string memberName)
        {
            if (type == null)
            {
                return null;
            }

            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(null, null);
            }

            FieldInfo field = type.GetField(memberName, flags);
            return field?.GetValue(null);
        }

        private static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }

            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }

            FieldInfo field = type.GetField(memberName, flags);
            return field?.GetValue(instance);
        }

        private static int GetIntMember(object instance, string memberName, int fallback)
        {
            object value = GetMemberValue(instance, memberName);
            if (value == null)
            {
                return fallback;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return fallback;
            }
        }

        private static string GetArchivePath(object archive)
        {
            if (archive is string path)
            {
                return path;
            }

            return GetMemberValue(archive, "Path") as string ??
                   GetMemberValue(archive, "FilePath") as string;
        }
    }
}
