using System;
using System.Reflection;
using UnityEngine;

namespace StudioCharaEditor
{
    internal static class PluginPandarinkaToolkit
    {
        private const string PluginTypeName =
            "PandarinkaToolkit.PandarinkaToolkitPlugin";
        private const string ClothingTypeName =
            "PandarinkaToolkit.CrossGenderClothing";

        internal static void PrepareMaleCategories()
        {
            InvokeClothingMethod("EnsureStudioMaleClothesCategories", null);
        }

        internal static void ConfigureController(object controller)
        {
            if (controller != null)
            {
                InvokeClothingMethod(
                    "ConfigureStudioController",
                    new[] { controller });
            }
        }

        internal static void BeginOverlayRefresh(object character)
        {
            if (character != null)
            {
                InvokeClothingMethod(
                    "BeginStudioOverlayRefresh",
                    new[] { character });
            }
        }

        internal static void EndOverlayRefresh(object character)
        {
            if (character != null)
            {
                InvokeClothingMethod(
                    "EndStudioOverlayRefresh",
                    new[] { character });
            }
        }

        private static void InvokeClothingMethod(
            string methodName,
            object[] arguments)
        {
            try
            {
                object clothing = FindActiveClothingIntegration();
                MethodInfo method = clothing?.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                method?.Invoke(clothing, arguments);
            }
            catch (Exception exception)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Pandarinka Toolkit integration failed in " +
                    methodName + ": " +
                    (exception.InnerException ?? exception).Message);
            }
        }

        private static object FindActiveClothingIntegration()
        {
            MonoBehaviour[] behaviours =
                UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                Type pluginType = behaviour?.GetType();
                if (pluginType == null ||
                    !string.Equals(
                        pluginType.FullName,
                        PluginTypeName,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Type clothingType =
                    pluginType.Assembly.GetType(ClothingTypeName, false);
                PropertyInfo currentProperty = clothingType?.GetProperty(
                    "Current",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);
                object clothing =
                    currentProperty?.GetValue(null, null);
                if (clothing != null)
                    return clothing;
            }

            return null;
        }
    }
}
