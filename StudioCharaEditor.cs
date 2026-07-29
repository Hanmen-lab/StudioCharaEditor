using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using KKAPI;
using KKAPI.Utilities;
using UnityEngine;
using KKAPI.Studio.UI.Toolbars;

namespace StudioCharaEditor
{
    [BepInPlugin(GUID, Name, Version)]
    [BepInDependency(KoikatuAPI.GUID, "1.43")]
    [BepInDependency("KCOX", "7.0")]
    [BepInDependency("com.animal42069.studiobetterpenetration", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.hooh.hooah", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("mikke.pushUpAI", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.fairbair.hs2_boobsettings", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInProcess("StudioNEOV2")]
    [BepInProcess("StudioNEOV2.exe")]
    public class StudioCharaEditor : BaseUnityPlugin
    {
        public const string GUID = "Countd360.StudioCharaEditor.HS2";
        public const string Name = "Studio Chara Editor";
        public const string Version = "2.6.0";
        public const string DefaultPathMacro = "$DEFAULT_CHAR_PATH$";
        public const string DefaultCoordMacro = "$DEFAULT_COORD_PATH$";

        public static StudioCharaEditor Instance { get; private set; }
        internal static new ManualLogSource Logger;

        // configs
        public static ConfigEntry<KeyboardShortcut> KeyShowUI { get; private set; }
        public static ConfigEntry<string> CharaExportPath { get; private set; }
        public static ConfigEntry<string> CoordExportPath { get; private set; }
        public static ConfigEntry<bool> DoubleThumbnailSize { get; private set; }
        public static ConfigEntry<bool> PreciseInputMode { get; private set; }
        public static ConfigEntry<bool> UnlimitedSlider { get; private set; }
        public static ConfigEntry<bool> ShowSelectedThumb { get; private set; }
        public static ConfigEntry<bool> CloseListAfterSelect { get; private set; }
        public static ConfigEntry<bool> VerboseMessage { get; private set; }
        public static ConfigEntry<int> UIXPosition { get; private set; }
        public static ConfigEntry<int> UIYPosition { get; private set; }
        public static ConfigEntry<int> UIWidth { get; private set; }
        public static ConfigEntry<int> UIHeight { get; private set; }
        public static ConfigEntry<string> UILanguage { get; private set; }
        public static ConfigEntry<float> UIScale { get; private set; }
        public static ConfigEntry<bool> SelectorGridViewByDefault { get; private set; }
        public static ConfigEntry<float> SelectorGridThumbnailSize { get; private set; }
        public static ConfigEntry<bool> ShowSelectorGridItemNames { get; private set; }
        public static ConfigEntry<float> SelectorWindowWidth { get; private set; }
        public static ConfigEntry<float> SelectorWindowHeight { get; private set; }
        public static ConfigEntry<bool> ShowTimelineIcons { get; private set; }
        public static ConfigEntry<bool> ShowMultiDetailUI { get; private set; }

        internal SimpleToolbarToggle _toolbarCharEditor;
        private Harmony harmony;
        private GameObject editorRoot;

        //private ConfigEntry<string> configGreeting;
        //private ConfigEntry<bool> configDisplayGreeting;

        private void Awake()
        {
            Instance = this;
            Logger = base.Logger;
            RemoveStaleEditorRoots();
            Logger.LogInfo("Studio Chara Editor loaded.");

            // config
            KeyShowUI = Config.Bind("General", "StudioCharaEditor UI shortcut key", new KeyboardShortcut(KeyCode.D, KeyCode.LeftShift), "Toggles the main UI on and off.");
            CharaExportPath = Config.Bind("General", "Default charactor export path", DefaultPathMacro, "Set default charactor export path. $DEFAULT_CHAR_PATH$ stands for UserData\\chara\\male or UserData\\chara\\female");
            CoordExportPath = Config.Bind("General", "Default coordinate export path", DefaultCoordMacro, "Set default coordinate export path. $DEFAULT_COORD_PATH$ stands for UserData\\coordinate\\male or UserData\\coordinate\\female");
            DoubleThumbnailSize = Config.Bind("General", "Double export PNG size", false, "Use double sized thumbnail photo when export char to PNG");
            PreciseInputMode = Config.Bind("General", "Precise input mode", false, "Allows the user to enter decimal for fine adjustment");
            UnlimitedSlider = Config.Bind("General", "Unlimited slider bar", false, "Slider input without limit check. AT YOUR OWN RISK!");
            ShowSelectedThumb = Config.Bind("General", "Thumbnail of current item", true, "Show the thumbnail of current selected item (hair, clothes, etc)");
            CloseListAfterSelect = Config.Bind("General", "Folder list after select", true, "Auto folder up the list after click on a item");

            VerboseMessage = Config.Bind("Debug", "Print verbose info", false, "Print more debug info to console.");

            UIXPosition = Config.Bind("GUI", "Main GUI X position", 50, "X offset from left in pixel");
            UIYPosition = Config.Bind("GUI", "Main GUI Y position", 300, "Y offset from top in pixel");
            UIWidth = Config.Bind("GUI", "Main GUI window width", 600, "Main window width, minimum 600, set it when UI is hided.");
            UIHeight = Config.Bind("GUI", "Main GUI window height", 400, "Main window height, minimum 400, set it when UI is hided.");
            UILanguage = Config.Bind("GUI", "GUI Language", "default", "Language setting, valid setting can be found in HS2StudioCharaEditor.xml. Need reload.");
            UIScale = Config.Bind("GUI", "UI Scale", 1.0f,
                new ConfigDescription(
                    "Scale of the entire UI. 1.0 = 100% (designed for 1080p). Try 1.33 for 1440p.",
                    new AcceptableValueRange<float>(0.5f, 3.0f)));
            SelectorGridViewByDefault = Config.Bind("GUI", "Selector grid view by default", true, "Open the item selector in grid view by default instead of list view.");
            SelectorGridThumbnailSize = Config.Bind("GUI", "Selector grid thumbnail size", 96f,
                new ConfigDescription(
                    "Size in pixels of each thumbnail in the selector grid view. Lower values fit more columns.",
                    new AcceptableValueRange<float>(48f, 200f)));
            ShowSelectorGridItemNames = Config.Bind("GUI", "Show item names in grid view", true, "Show the item name under each thumbnail in the selector grid view.");
            SelectorWindowWidth = Config.Bind("GUI", "Selector window width", 540f, "Remembered width of the item selector window.");
            SelectorWindowHeight = Config.Bind("GUI", "Selector window height", 520f, "Remembered height of the item selector window.");
            ShowTimelineIcons = Config.Bind("GUI", "Show timeline icons", true, "Show the 'T' Timeline interpolation buttons next to character values when the Timeline plugin is installed.");
            ShowMultiDetailUI = Config.Bind("GUI", "Show multidetail plugin UI", true, "When the MultiDetail plugin is installed, replace the Body > Skin and Face > FaceType detail sliders with the multi-slot MultiDetail UI. Turn off to use the vanilla single-detail UI instead.");
            ShowMultiDetailUI.SettingChanged += OnShowMultiDetailUISettingChanged;

            /*
            configGreeting = Config.Bind("General",   // The section under which the option is shown
                                        "GreetingText",  // The key of the configuration option in the configuration file
                                        "Hello, world!", // The default value
                                        "A greeting text to show when the game is launched"); // Description of the option to show in the config file
            configDisplayGreeting = Config.Bind("General.Toggles",
                                            "DisplayGreeting",
                                            true,
                                            "Whether or not to show the greeting text");
            */

            // init accessories plugin
            PluginMoreAccessories.Initialize();

            // start
            editorRoot = new GameObject(Name);
            UnityEngine.Object.DontDestroyOnLoad(editorRoot);
            CharaEditorMgr.Install(editorRoot);

            // Patch compatibility hooks that must run after optional plugin dependencies load.
            harmony = new Harmony(GUID);
            PluginBetterPenetration.InstallHarmonyPatches(harmony);
            PluginHooahComponents.Initialize(harmony);

            // Toolbar Button
            _toolbarCharEditor = new SimpleToolbarToggle(
                "Graphics",
                "Open Studio CharaEditor Inspector window. Hotkey: " + KeyShowUI.Value,
                () => ResourceUtils.GetEmbeddedResource("toolbarbutton.png").LoadTexture(),
                false,
                this,
                val => ToggleUI(val));
            ToolbarManager.AddLeftToolbarControl(_toolbarCharEditor);
        }

        private void OnDestroy()
        {
            if (ShowMultiDetailUI != null)
            {
                ShowMultiDetailUI.SettingChanged -=
                    OnShowMultiDetailUISettingChanged;
            }

            if (editorRoot != null)
            {
                // Disable immediately so no stale Update/OnGUI can run while
                // Unity waits until the end of the frame to destroy the root.
                editorRoot.SetActive(false);
                UnityEngine.Object.Destroy(editorRoot);
                editorRoot = null;
            }

            harmony?.UnpatchSelf();
            harmony = null;
            _toolbarCharEditor = null;
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        private static void RemoveStaleEditorRoots()
        {
            GameObject[] gameObjects =
                UnityEngine.Object.FindObjectsOfType<GameObject>();
            int removed = 0;
            for (int index = 0; index < gameObjects.Length; index++)
            {
                GameObject candidate = gameObjects[index];
                if (candidate == null ||
                    !string.Equals(
                        candidate.name,
                        Name,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                Component[] components = candidate.GetComponents<Component>();
                bool hasEditorManager = false;
                for (int componentIndex = 0;
                    componentIndex < components.Length;
                    componentIndex++)
                {
                    Component component = components[componentIndex];
                    if (component != null &&
                        string.Equals(
                            component.GetType().FullName,
                            "StudioCharaEditor.CharaEditorMgr",
                            StringComparison.Ordinal))
                    {
                        hasEditorManager = true;
                        break;
                    }
                }

                if (!hasEditorManager)
                {
                    continue;
                }

                candidate.SetActive(false);
                UnityEngine.Object.Destroy(candidate);
                removed++;
            }

            if (removed > 0)
            {
                Logger?.LogInfo(
                    $"Removed {removed} stale Studio Chara Editor root object(s) before reload.");
            }
        }

        private static void OnShowMultiDetailUISettingChanged(
            object sender,
            EventArgs eventArgs)
        {
            CharaEditorMgr.Instance?.RefreshAllControllerFileData();
        }

        internal static void SaveConfigNow()
        {
            try
            {
                Instance?.Config.Save();
            }
            catch (Exception exception)
            {
                Logger?.LogWarning(
                    "Failed to save Studio Chara Editor configuration: " +
                    exception.Message);
            }
        }

        private void ToggleUI(bool show)
        {
            //  Find UI in scene
            var ui = UnityEngine.Object.FindObjectOfType<CharaEditorUI>();
            if (ui != null)
            {
                ui.VisibleGUI = show;
                if (show)
                {
                    CharaEditorMgr.Instance?.ReloadDictionary();
                    ui.windowRect = new Rect(UIXPosition.Value, UIYPosition.Value,
                        Math.Max(600, UIWidth.Value), Math.Max(400, UIHeight.Value));
                }
                else
                {
                    UIXPosition.Value = (int)ui.windowRect.x;
                    UIYPosition.Value = (int)ui.windowRect.y;
                    UIWidth.Value = (int)ui.windowRect.width;
                    UIHeight.Value = (int)ui.windowRect.height;
                    ui.PersistSelectorWindowSize();
                }
            }
        }
    }
}
