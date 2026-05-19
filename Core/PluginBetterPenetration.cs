using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace StudioCharaEditor
{
    internal static class PluginBetterPenetration
    {
        private const string StudioBetterPenetrationGuid = "com.animal42069.studiobetterpenetration";
        private const string CurrentStudioBetterPenetrationTypeName =
            "Core_BetterPenetration.Studio_BetterPenetration";
        private const string HS2StudioBetterPenetrationTypeName =
            "HS2_Studio_BetterPenetration.HS2_Studio_BetterPenetration";

        private static bool loggedSkip;

        public static bool HasStudioPlugin()
        {
            if (Chainloader.PluginInfos.ContainsKey(StudioBetterPenetrationGuid))
            {
                return true;
            }

            return FindType(CurrentStudioBetterPenetrationTypeName) != null ||
                   FindType(HS2StudioBetterPenetrationTypeName) != null;
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
