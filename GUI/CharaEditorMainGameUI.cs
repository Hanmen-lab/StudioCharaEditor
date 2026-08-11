using AIChara;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using CharaCustom;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace StudioCharaEditor
{
    partial class CharaEditorUI
    {
        private enum MainGameUtilityPage
        {
            None,
            MaterialEditorClothes,
            MaterialEditorHair,
            ClothesOverlays,
            PushUp,
            Colliders,
            HairShaderSwapper,
            HairShaderProperties,
            CostumeSaveDelete,
            CostumeLoad,
            StudioCategory
        }

        private const int MainGameLeftWindowId = 10125;
        private const int MainGameRightWindowId = 10126;
        private const int MainGameStatusWindowId = 10127;
        private const int MainGamePluginWindowId = 10128;
        private const int MainGameCollapsedStatusWindowId = 10129;
        private const int MainGameCollapsedPluginWindowId = 10130;
        private const float MainGameLargePreviewScrollbarMinimumThumbHeight = 42f;
        private const float MainGameMinimumLeftWidth = 260f;
        private const float MainGameMinimumLeftHeight = 300f;
        private const float MainGameMinimumRightWidth = 430f;
        private const float MainGameMinimumRightHeight = 300f;
        private const float MainGameAuxiliaryWidth = 400f;
        private const float MainGameStatusHeight = 210f;
        private const float MainGamePluginHeight = 258f;
        private const float MainGameCollapsedStatusWidth = 132f;
        private const float MainGameCollapsedPluginWidth = 190f;
        private const float MainGameCollapsedHeight = 42f;
        private const float MainGameHeaderHeight = 180f;

        private static readonly int[] MainGameIconCategoryMap = { 1, 0, 2, 3, 4, -1 };
        private static readonly string[] MainGameBodyScaleAxisNames =
            { "Body Scale X", "Body Scale Y", "Body Scale Z" };

        private CharaEditorUiTheme activeThemeMode = CharaEditorUiTheme.Modern;
        private Rect mainGameLeftRect;
        private Rect mainGameRightRect;
        private Rect mainGameStatusRect;
        private Rect mainGamePluginRect;
        private Rect mainGameCollapsedStatusRect;
        private Rect mainGameCollapsedPluginRect;
        private bool mainGameStatusCollapsed;
        private bool mainGamePluginCollapsed;
        private bool mainGameStatusCollapsedPositionInitialized;
        private bool mainGamePluginCollapsedPositionInitialized;
        private bool mainGamePanelRectsInitialized;
        private bool mainGameSettingsOpen;
        private int mainGameSettingsPage;
        private bool mainGameUseMouseWheel = true;
        private string mainGameUiScalePercentText = string.Empty;
        private bool mainGameCoordinateRulesVisible;
        private int mainGameMakerPoseIndex = 1;
        private string mainGameCurrentRightTitle = string.Empty;
        private MainGameUtilityPage mainGameUtilityPage;
        private string mainGameStudioCategoryName = string.Empty;
        private bool mainGameSelectorVisibleThisFrame;
        private string mainGameVisibleSelectorKey = string.Empty;
        private string mainGameScrollToSelectorKey = string.Empty;
        private readonly int[] mainGameClothesChannelAssignments = new int[24];
        private readonly Dictionary<int, Dictionary<string, object>> mainGameClothesChannels =
            new Dictionary<int, Dictionary<string, object>>();
        private int mainGameOpenClothesChannelAssignment = -1;
        private string mainGameOpenStudioDropdownName = string.Empty;
        private int mainGamePushUpMode;
        private int mainGameColliderIndex;
        private bool mainGameColliderDropdownOpen;
        private int mainGameColliderCharacterId;
        private bool mainGameHairShaderControlsNeedRefresh;
        private int mainGameHairShaderCharacterId;
        private string mainGameHairShaderStatus = string.Empty;
        private bool mainGameHairShaderInitializationAttempted;
        private int mainGameResizeWindowId;
        private Vector2 mainGameResizeStartMouse;
        private Vector2 mainGameResizeStartSize;
        private float mainGameResizeStartRightEdge;
        private float mainGameRightContentWidth;
        private bool mainGameUncensorListOpen;
        private Vector2 mainGameUncensorScroll;
        private string mainGameUncensorStatus = string.Empty;
        private readonly List<MainGameUncensorOption> mainGameUncensorOptions =
            new List<MainGameUncensorOption>();
        private CharaEditorTheme mainGameSliderStyleTheme;
        private GUIStyle mainGameSliderLabelStyle;
        private GUIStyle mainGameSliderAccentLabelStyle;
        private GUIStyle mainGameSliderValueStyle;
        private GUIStyle mainGameSliderPreciseValueStyle;
        private GUIStyle mainGameResetButtonStyle;
        private GUIStyle mainGameAuxiliaryLabelStyle;
        private GUIStyle mainGameStatusLabelStyle;
        private GUIStyle mainGameAuxiliaryValueStyle;
        private GUIStyle mainGameAuxiliaryButtonStyle;
        private GUIStyle mainGamePlayPauseButtonStyle;
        private GUIStyle mainGameAuxiliaryHeaderButtonStyle;
        private GUIStyle mainGameCollapsedWindowStyle;
        private GUIStyle mainGameCollapsedTitleStyle;
        private ConfigEntry<bool> mainGameXyzScaleEntry;
        private CharaEditorTheme mainGameSelectorStyleTheme;
        private GUIStyle mainGameSelectorItemLabelStyle;
        private GUIStyle mainGameCoordinateCardLabelStyle;
        private CharaEditorTheme mainGameSelectorSourceStyleTheme;
        private GUIStyle mainGameSelectorSourceLabelStyle;
        private readonly Dictionary<string, int> mainGameDetailTabPool = new Dictionary<string, int>();
        private readonly Dictionary<string, string> mainGameStatusValueInputs =
            new Dictionary<string, string>();

        private sealed class MainGameCoordinateCard
        {
            public string Path;
            public string Name;
            public DateTime Modified;
            public Texture2D Preview;
            public bool PreviewLoadAttempted;
        }

        private sealed class MainGameCoordinateFolderNode
        {
            public string Name;
            public string Path;
            public readonly List<MainGameCoordinateFolderNode> Children =
                new List<MainGameCoordinateFolderNode>();
        }

        private readonly List<MainGameCoordinateCard> mainGameCoordinateCards =
            new List<MainGameCoordinateCard>();
        private readonly List<MainGameCoordinateCard> mainGameVisibleCoordinateCards =
            new List<MainGameCoordinateCard>();
        private readonly HashSet<string> mainGameExpandedCoordinateFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool mainGameCoordinateCardsNeedRefresh = true;
        private bool mainGameCoordinateFilterDirty = true;
        private byte mainGameCoordinateCardSex = byte.MaxValue;
        private int mainGameSelectedCoordinateCard = -1;
        private string mainGameSelectedCoordinateCardPath = string.Empty;
        private int mainGameCoordinateDeleteConfirmation = -1;
        private string mainGameCoordinateCardStatus = string.Empty;
        private Vector2 mainGameCoordinateScroll;
        private Vector2 mainGameCoordinateFolderScroll;
        private readonly LinkedList<MainGameCoordinateCard> mainGameCoordinatePreviewQueue =
            new LinkedList<MainGameCoordinateCard>();
        private readonly Queue<MainGameCoordinateCard> mainGameCoordinatePreviewLoadOrder =
            new Queue<MainGameCoordinateCard>();
        private Coroutine mainGameCoordinatePreviewCoroutine;
        private Coroutine mainGameCoordinateNameIndexCoroutine;
        private Coroutine mainGameCoordinateActionCoroutine;
        private MainGameCoordinateFolderNode mainGameCoordinateFolderRoot;
        private string mainGameSelectedCoordinateFolder = string.Empty;
        private string mainGameCoordinateSearch = string.Empty;
        private bool mainGameCoordinateSortNewest;
        private bool mainGameCoordinateFolderOpen;
        private GUIStyle mainGameCoordinateFolderStyle;
        private GUIStyle mainGameCoordinateFolderSelectedStyle;
        private GUIStyle mainGameCoordinateHeaderStyle;
        private GUIStyle mainGameCoordinateHeaderSelectedStyle;

        private bool UseMainGameLayout =>
            activeThemeMode == CharaEditorUiTheme.MainGame &&
            guiMode == GuiModeType.MAIN;

        private float GetActiveGuiScale()
        {
            // Maker's layout is authored in screen pixels. Applying the Modern
            // UI scale here made every panel and icon about 20% too small.
            return guiMode == GuiModeType.MAIN &&
                   StudioCharaEditor.UITheme.Value == CharaEditorUiTheme.MainGame
                ? Mathf.Clamp(StudioCharaEditor.MainGameUIScale?.Value ?? 1f, 0.75f, 1.6f)
                : Math.Max(0.01f, StudioCharaEditor.UIScale.Value);
        }

        private float ActiveEditorWindowWidth =>
            UseMainGameLayout ? mainGameRightRect.width : windowRect.width;

        private float ActiveEditorWindowHeight =>
            UseMainGameLayout ? mainGameRightRect.height : windowRect.height;

        private Rect MainGameHeaderRect => new Rect(
            mainGameLeftRect.x,
            mainGameLeftRect.y - MainGameHeaderHeight,
            416f,
            MainGameHeaderHeight);

        private Rect MainGameExitRect
        {
            get
            {
                float scale = GetActiveGuiScale();
                return new Rect(24f, Screen.height / scale - 74f, 54f, 54f);
            }
        }

        internal void EnsureMainGamePanelRects()
        {
            if (mainGamePanelRectsInitialized)
            {
                ClampMainGamePanelRects();
                return;
            }

            float scale = GetActiveGuiScale();
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            float leftWidth = StudioCharaEditor.MainGameLeftPanelWidth.Value;
            float leftHeight = StudioCharaEditor.MainGameLeftPanelHeight.Value;
            float rightWidth = StudioCharaEditor.MainGameRightPanelWidth.Value;
            float rightHeight = StudioCharaEditor.MainGameRightPanelHeight.Value;
            float rightX = StudioCharaEditor.MainGameRightX.Value;
            if (rightX < 0)
            {
                rightX = logicalWidth - rightWidth - 10f;
            }

            mainGameLeftRect = new Rect(
                StudioCharaEditor.MainGameLeftX.Value,
                StudioCharaEditor.MainGameLeftY.Value < MainGameHeaderHeight
                    ? MainGameHeaderHeight + 10f
                    : StudioCharaEditor.MainGameLeftY.Value,
                leftWidth,
                leftHeight);
            mainGameRightRect = new Rect(
                rightX,
                StudioCharaEditor.MainGameRightY.Value,
                rightWidth,
                rightHeight);
            float statusX = StudioCharaEditor.MainGameStatusX.Value;
            float statusY = StudioCharaEditor.MainGameStatusY.Value;
            if (statusX < 0f)
            {
                statusX = mainGameRightRect.xMax - MainGameAuxiliaryWidth;
            }
            if (statusY < 0f)
            {
                statusY = mainGameRightRect.yMax + 18f;
            }
            mainGameStatusRect = new Rect(
                statusX,
                statusY,
                MainGameAuxiliaryWidth,
                MainGameStatusHeight);
            float pluginX = StudioCharaEditor.MainGamePluginX.Value;
            float pluginY = StudioCharaEditor.MainGamePluginY.Value;
            if (pluginX < 0f)
            {
                pluginX = mainGameRightRect.xMax - MainGameAuxiliaryWidth;
            }
            if (pluginY < 0f)
            {
                pluginY = statusY + MainGameStatusHeight + 12f;
            }
            mainGamePluginRect = new Rect(
                pluginX,
                pluginY,
                MainGameAuxiliaryWidth,
                MainGamePluginHeight);
            mainGameStatusCollapsed = StudioCharaEditor.MainGameStatusCollapsed.Value;
            mainGamePluginCollapsed = StudioCharaEditor.MainGamePluginCollapsed.Value;
            float collapsedStatusX = StudioCharaEditor.MainGameStatusCollapsedX.Value;
            float collapsedStatusY = StudioCharaEditor.MainGameStatusCollapsedY.Value;
            mainGameStatusCollapsedPositionInitialized = collapsedStatusX >= 0f && collapsedStatusY >= 0f;
            mainGameCollapsedStatusRect = new Rect(
                collapsedStatusX < 0f
                    ? statusX + MainGameAuxiliaryWidth - MainGameCollapsedStatusWidth
                    : collapsedStatusX,
                collapsedStatusY < 0f ? statusY : collapsedStatusY,
                MainGameCollapsedStatusWidth,
                MainGameCollapsedHeight);
            float collapsedPluginX = StudioCharaEditor.MainGamePluginCollapsedX.Value;
            float collapsedPluginY = StudioCharaEditor.MainGamePluginCollapsedY.Value;
            mainGamePluginCollapsedPositionInitialized = collapsedPluginX >= 0f && collapsedPluginY >= 0f;
            mainGameCollapsedPluginRect = new Rect(
                collapsedPluginX < 0f
                    ? pluginX + MainGameAuxiliaryWidth - MainGameCollapsedPluginWidth
                    : collapsedPluginX,
                collapsedPluginY < 0f ? pluginY : collapsedPluginY,
                MainGameCollapsedPluginWidth,
                MainGameCollapsedHeight);
            mainGamePanelRectsInitialized = true;
            ClampMainGamePanelRects();
        }

        internal void PersistMainGamePanelPositions()
        {
            if (!mainGamePanelRectsInitialized || StudioCharaEditor.MainGameLeftX == null)
            {
                return;
            }

            StudioCharaEditor.MainGameLeftX.Value = Mathf.RoundToInt(mainGameLeftRect.x);
            StudioCharaEditor.MainGameLeftY.Value = Mathf.RoundToInt(mainGameLeftRect.y);
            StudioCharaEditor.MainGameRightX.Value = Mathf.RoundToInt(mainGameRightRect.x);
            StudioCharaEditor.MainGameRightY.Value = Mathf.RoundToInt(mainGameRightRect.y);
            StudioCharaEditor.MainGameLeftPanelWidth.Value = Mathf.RoundToInt(mainGameLeftRect.width);
            StudioCharaEditor.MainGameLeftPanelHeight.Value = Mathf.RoundToInt(mainGameLeftRect.height);
            StudioCharaEditor.MainGameRightPanelWidth.Value = Mathf.RoundToInt(mainGameRightRect.width);
            StudioCharaEditor.MainGameRightPanelHeight.Value = Mathf.RoundToInt(mainGameRightRect.height);
            StudioCharaEditor.MainGameStatusPanelWidth.Value = Mathf.RoundToInt(MainGameAuxiliaryWidth);
            StudioCharaEditor.MainGameStatusPanelHeight.Value = Mathf.RoundToInt(MainGameStatusHeight);
            StudioCharaEditor.MainGamePluginPanelWidth.Value = Mathf.RoundToInt(MainGameAuxiliaryWidth);
            StudioCharaEditor.MainGamePluginPanelHeight.Value = Mathf.RoundToInt(MainGamePluginHeight);
            StudioCharaEditor.MainGameStatusX.Value = Mathf.RoundToInt(mainGameStatusRect.x);
            StudioCharaEditor.MainGameStatusY.Value = Mathf.RoundToInt(mainGameStatusRect.y);
            StudioCharaEditor.MainGamePluginX.Value = Mathf.RoundToInt(mainGamePluginRect.x);
            StudioCharaEditor.MainGamePluginY.Value = Mathf.RoundToInt(mainGamePluginRect.y);
            StudioCharaEditor.MainGameStatusCollapsed.Value = mainGameStatusCollapsed;
            StudioCharaEditor.MainGamePluginCollapsed.Value = mainGamePluginCollapsed;
            StudioCharaEditor.MainGameStatusCollapsedX.Value = Mathf.RoundToInt(mainGameCollapsedStatusRect.x);
            StudioCharaEditor.MainGameStatusCollapsedY.Value = Mathf.RoundToInt(mainGameCollapsedStatusRect.y);
            StudioCharaEditor.MainGamePluginCollapsedX.Value = Mathf.RoundToInt(mainGameCollapsedPluginRect.x);
            StudioCharaEditor.MainGamePluginCollapsedY.Value = Mathf.RoundToInt(mainGameCollapsedPluginRect.y);
            StudioCharaEditor.SaveConfigNow();
        }

        private void ClampMainGamePanelRects()
        {
            float scale = GetActiveGuiScale();
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            mainGameLeftRect.width = Mathf.Clamp(mainGameLeftRect.width, MainGameMinimumLeftWidth, Math.Max(MainGameMinimumLeftWidth, logicalWidth - 8f));
            mainGameLeftRect.height = Mathf.Clamp(mainGameLeftRect.height, MainGameMinimumLeftHeight, Math.Max(MainGameMinimumLeftHeight, logicalHeight - MainGameHeaderHeight - 8f));
            mainGameStatusRect.width = Math.Min(MainGameAuxiliaryWidth, Math.Max(1f, logicalWidth - 8f));
            mainGameStatusRect.height = Math.Min(MainGameStatusHeight, Math.Max(1f, logicalHeight - 8f));
            mainGamePluginRect.width = Math.Min(MainGameAuxiliaryWidth, Math.Max(1f, logicalWidth - 8f));
            mainGamePluginRect.height = Math.Min(MainGamePluginHeight, Math.Max(1f, logicalHeight - 8f));
            mainGameCollapsedStatusRect.width = Math.Min(MainGameCollapsedStatusWidth, Math.Max(1f, logicalWidth - 8f));
            mainGameCollapsedStatusRect.height = Math.Min(MainGameCollapsedHeight, Math.Max(1f, logicalHeight - 8f));
            mainGameCollapsedPluginRect.width = Math.Min(MainGameCollapsedPluginWidth, Math.Max(1f, logicalWidth - 8f));
            mainGameCollapsedPluginRect.height = Math.Min(MainGameCollapsedHeight, Math.Max(1f, logicalHeight - 8f));
            mainGameRightRect.width = Mathf.Clamp(mainGameRightRect.width, MainGameMinimumRightWidth, Math.Max(MainGameMinimumRightWidth, logicalWidth - 8f));
            mainGameRightRect.height = Mathf.Clamp(mainGameRightRect.height, MainGameMinimumRightHeight, Math.Max(MainGameMinimumRightHeight, logicalHeight - 8f));
            mainGameLeftRect.x = Mathf.Clamp(mainGameLeftRect.x, 4f, Math.Max(4f, logicalWidth - mainGameLeftRect.width - 4f));
            mainGameLeftRect.y = Mathf.Clamp(mainGameLeftRect.y, MainGameHeaderHeight + 4f, Math.Max(MainGameHeaderHeight + 4f, logicalHeight - mainGameLeftRect.height - 4f));
            mainGameRightRect.x = Mathf.Clamp(mainGameRightRect.x, 4f, Math.Max(4f, logicalWidth - mainGameRightRect.width - 4f));
            mainGameRightRect.y = Mathf.Clamp(mainGameRightRect.y, 4f, Math.Max(4f, logicalHeight - mainGameRightRect.height - 4f));
            mainGameStatusRect.x = Mathf.Clamp(mainGameStatusRect.x, 4f, Math.Max(4f, logicalWidth - mainGameStatusRect.width - 4f));
            mainGameStatusRect.y = Mathf.Clamp(mainGameStatusRect.y, 4f, Math.Max(4f, logicalHeight - mainGameStatusRect.height - 4f));
            mainGamePluginRect.x = Mathf.Clamp(mainGamePluginRect.x, 4f, Math.Max(4f, logicalWidth - mainGamePluginRect.width - 4f));
            mainGamePluginRect.y = Mathf.Clamp(mainGamePluginRect.y, 4f, Math.Max(4f, logicalHeight - mainGamePluginRect.height - 4f));
            mainGameCollapsedStatusRect.x = Mathf.Clamp(mainGameCollapsedStatusRect.x, 4f, Math.Max(4f, logicalWidth - mainGameCollapsedStatusRect.width - 4f));
            mainGameCollapsedStatusRect.y = Mathf.Clamp(mainGameCollapsedStatusRect.y, 4f, Math.Max(4f, logicalHeight - mainGameCollapsedStatusRect.height - 4f));
            mainGameCollapsedPluginRect.x = Mathf.Clamp(mainGameCollapsedPluginRect.x, 4f, Math.Max(4f, logicalWidth - mainGameCollapsedPluginRect.width - 4f));
            mainGameCollapsedPluginRect.y = Mathf.Clamp(mainGameCollapsedPluginRect.y, 4f, Math.Max(4f, logicalHeight - mainGameCollapsedPluginRect.height - 4f));
        }

        private Rect GetSelectorAnchorRect()
        {
            return UseMainGameLayout ? mainGameRightRect : windowRect;
        }

        private Rect[] GetEditorMouseRects()
        {
            return UseMainGameLayout
                ? new[]
                {
                    MainGameHeaderRect,
                    mainGameLeftRect,
                    mainGameRightRect,
                    mainGameStatusCollapsed ? mainGameCollapsedStatusRect : mainGameStatusRect,
                    mainGamePluginCollapsed ? mainGameCollapsedPluginRect : mainGamePluginRect,
                    MainGameExitRect
                }
                : new[] { windowRect };
        }

        private void DrawMainGameLayout()
        {
            DrawMainGameNavigationHeader();
            GUIStyle leftWindowStyle = theme.MainGamePanelWindowStyle;
            if (!mainGameSettingsOpen &&
                CharaEditorController.CATEGORY1[catelogIndex1] == CharaEditorController.CT1_HAIR)
            {
                leftWindowStyle = new GUIStyle(theme.MainGamePanelWindowStyle);
                leftWindowStyle.padding = new RectOffset(
                    leftWindowStyle.padding.left,
                    leftWindowStyle.padding.right,
                    14,
                    leftWindowStyle.padding.bottom);
            }
            else
            {
                // Body Shape, Face Settings, Outfit, Slot and Character
                // Setting all start with the panel title already drawn at the
                // top of the window. Keep only a small gap before their first
                // entry; later group spacing remains unchanged.
                leftWindowStyle = new GUIStyle(theme.MainGamePanelWindowStyle);
                leftWindowStyle.padding = new RectOffset(
                    leftWindowStyle.padding.left,
                    leftWindowStyle.padding.right,
                    48,
                    leftWindowStyle.padding.bottom);
            }
            Rect returnedLeftRect = GUI.Window(
                MainGameLeftWindowId,
                mainGameLeftRect,
                DrawMainGameLeftWindow,
                string.Empty,
                leftWindowStyle);
            returnedLeftRect.size = mainGameLeftRect.size;
            mainGameLeftRect = returnedLeftRect;

            string detailTitle = mainGameUtilityPage != MainGameUtilityPage.None
                ? GetMainGameUtilityPageTitle()
                : mainGameSettingsOpen
                    ? GetMainGameSettingsPageTitle()
                    : LC("Studio Character Editor");
            if (mainGameUtilityPage == MainGameUtilityPage.None &&
                !mainGameSettingsOpen && TryGetMainGameContext(
                    out CharaEditorController controller,
                    out string category1,
                    out string category2,
                    out string ignoredKey))
            {
                detailTitle = GetMainGameRightTitle(controller, category1, category2);
            }
            else if (mainGameUtilityPage == MainGameUtilityPage.None && !mainGameSettingsOpen)
            {
                detailTitle = LC("Studio Character Editor");
            }
            mainGameCurrentRightTitle = detailTitle;

            GUIStyle rightWindowStyle = new GUIStyle(theme.MainGamePanelWindowStyle);
            rightWindowStyle.padding = new RectOffset(
                rightWindowStyle.padding.left,
                rightWindowStyle.padding.right,
                68,
                8);
            Rect returnedRightRect = GUI.Window(
                MainGameRightWindowId,
                mainGameRightRect,
                DrawMainGameRightWindow,
                string.Empty,
                rightWindowStyle);
            returnedRightRect.size = mainGameRightRect.size;
            if (mainGameResizeWindowId == MainGameRightWindowId)
            {
                returnedRightRect.x = mainGameRightRect.x;
            }
            mainGameRightRect = returnedRightRect;
            ClampMainGamePanelRects();
            GUIStyle auxiliaryWindowStyle = new GUIStyle(theme.MainGamePanelWindowStyle);
            auxiliaryWindowStyle.padding = new RectOffset(10, 10, 48, 10);
            auxiliaryWindowStyle.fontSize = 20;
            if (mainGameStatusCollapsed)
            {
                mainGameCollapsedStatusRect = GUI.Window(
                    MainGameCollapsedStatusWindowId,
                    mainGameCollapsedStatusRect,
                    DrawMainGameCollapsedStatusWindow,
                    string.Empty,
                    GetMainGameCollapsedWindowStyle());
            }
            else
            {
                Rect returnedStatusRect = GUI.Window(
                    MainGameStatusWindowId,
                    mainGameStatusRect,
                    DrawMainGameStatusWindow,
                    string.Empty,
                    auxiliaryWindowStyle);
                returnedStatusRect.size = mainGameStatusRect.size;
                mainGameStatusRect = returnedStatusRect;
            }
            if (mainGamePluginCollapsed)
            {
                mainGameCollapsedPluginRect = GUI.Window(
                    MainGameCollapsedPluginWindowId,
                    mainGameCollapsedPluginRect,
                    DrawMainGameCollapsedPluginWindow,
                    string.Empty,
                    GetMainGameCollapsedWindowStyle());
            }
            else
            {
                Rect returnedPluginRect = GUI.Window(
                    MainGamePluginWindowId,
                    mainGamePluginRect,
                    DrawMainGamePluginWindow,
                    string.Empty,
                    auxiliaryWindowStyle);
                returnedPluginRect.size = mainGamePluginRect.size;
                mainGamePluginRect = returnedPluginRect;
            }
            ClampMainGamePanelRects();
            if (Event.current.rawType == EventType.MouseUp)
            {
                PersistMainGamePanelPositions();
            }
            DrawMainGameModeExitButton();
        }

        private void DrawMainGameModeExitButton()
        {
            Rect buttonRect = MainGameExitRect;
            bool hover = buttonRect.Contains(Event.current.mousePosition);
            Texture2D texture = hover
                ? theme.MainGameExitSelectedTexture
                : theme.MainGameExitNormalTexture;
            GUI.DrawTexture(buttonRect, texture, ScaleMode.ScaleToFit, true);
            if (GUI.Button(buttonRect, GUIContent.none, theme.MainGameIconButtonStyle))
            {
                PersistMainGamePanelPositions();
                StudioCharaEditor.UITheme.Value = CharaEditorUiTheme.Modern;
                StudioCharaEditor.SaveConfigNow();
            }
        }

        private void DrawMainGameNavigationHeader()
        {
            Rect headerRect = MainGameHeaderRect;
            GUI.BeginGroup(headerRect);
            string characterName = ociTarget?.treeNodeObject?.textName ?? LC("Select a character");
            GUI.Label(new Rect(0f, 0f, 416f, 40f), characterName, theme.MainGameTitleStyle);

            const float iconSize = 56f;
            const float spacing = 16f;
            const float iconY = 42f;
            for (int iconIndex = 0; iconIndex < MainGameIconCategoryMap.Length; iconIndex++)
            {
                int categoryIndex = MainGameIconCategoryMap[iconIndex];
                Rect iconRect = new Rect(iconIndex * (iconSize + spacing), iconY, iconSize, iconSize);
                bool selected = categoryIndex < 0
                    ? mainGameSettingsOpen
                    : !mainGameSettingsOpen && catelogIndex1 == categoryIndex;
                bool hover = iconRect.Contains(Event.current.mousePosition);
                Texture2D icon = selected || hover
                    ? theme.MainGameCategorySelected[iconIndex]
                    : theme.MainGameCategoryNormal[iconIndex];
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                if (GUI.Button(iconRect, GUIContent.none, theme.MainGameIconButtonStyle))
                {
                    mainGameUtilityPage = MainGameUtilityPage.None;
                    mainGameStudioCategoryName = string.Empty;
                    if (categoryIndex < 0)
                    {
                        mainGameSettingsOpen = true;
                        CloseSelectorSidePanel();
                    }
                    else
                    {
                        if (catelogIndex1 != categoryIndex || mainGameSettingsOpen)
                        {
                            CloseSelectorSidePanel();
                        }
                        catelogIndex1 = categoryIndex;
                        mainGameSettingsOpen = false;
                        detailPageSelect = SelectMode.Normal;
                    }
                }
            }

            int activeIcon = mainGameSettingsOpen
                ? 5
                : Array.IndexOf(MainGameIconCategoryMap, catelogIndex1);
            activeIcon = Mathf.Clamp(activeIcon, 0, 5);
            Rect sectionIconRect = new Rect(0f, 111f, 48f, 48f);
            GUI.DrawTexture(sectionIconRect, theme.MainGameCategoryNormal[activeIcon], ScaleMode.ScaleToFit, true);
            string sectionName = mainGameSettingsOpen
                ? LC("Settings")
                : LC(CharaEditorController.CATEGORY1[catelogIndex1]);
            GUI.Label(new Rect(56f, 111f, 330f, 48f), sectionName, theme.MainGameTitleStyle);
            GUI.EndGroup();
        }

        private void DrawMainGameLeftWindow(int windowId)
        {
            HandleMainGameWindowFocus(windowId);
            HandleMainGameResizeGripInput(
                windowId,
                mainGameLeftRect,
                MainGameMinimumLeftWidth,
                MainGameMinimumLeftHeight,
                false);
            string sectionTitle = mainGameSettingsOpen
                ? LC("Character Setting")
                : GetMainGameLeftPanelTitle(CharaEditorController.CATEGORY1[catelogIndex1]);
            if (!string.IsNullOrEmpty(sectionTitle))
            {
                GUI.Label(new Rect(0f, 0f, mainGameLeftRect.width, 44f), sectionTitle, theme.MainGameSectionHeaderStyle);
            }

            if (mainGameSettingsOpen)
            {
                leftScroll = GUILayout.BeginScrollView(leftScroll, GUIStyle.none, GUILayout.ExpandHeight(true));
                DrawMainGameSettingsList();
                GUILayout.EndScrollView();
            }
            else if (!TryGetMainGameContext(
                    out CharaEditorController controller,
                    out string category1,
                    out string ignoredCategory2,
                    out string ignoredKey))
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(LC("Please select a charactor to edit."), largeLabel);
                GUILayout.FlexibleSpace();
            }
            else
            {
                string[] category2List = controller.GetCategoryList(category1);
                leftScroll = GUILayout.BeginScrollView(leftScroll, GUIStyle.none, GUILayout.ExpandHeight(true));
                DrawMainGameCategoryList(controller, category1, category2List);
                GUILayout.EndScrollView();
                DrawMainGameCategoryOperations(controller, category1, category2List);
            }

            DrawMainGameResizeGrip(
                windowId,
                mainGameLeftRect,
                false);
            GUI.DragWindow(new Rect(
                0f,
                0f,
                mainGameLeftRect.width,
                string.IsNullOrEmpty(sectionTitle) ? 12f : 44f));
        }

        private void DrawMainGameCategoryList(
            CharaEditorController controller,
            string category1,
            string[] category2List)
        {
            if (category1 == CharaEditorController.CT1_HAIR)
            {
                DrawMainGameHairCategoryList(controller, category1, category2List);
                return;
            }

            bool renderedContent = false;
            string panelTitle = GetMainGameLeftPanelTitle(category1);
            for (int categoryIndex = 0; categoryIndex < category2List.Length; categoryIndex++)
            {
                string rawTitle = category2List[categoryIndex];
                if ((category1 == CharaEditorController.CT1_BODY || category1 == CharaEditorController.CT1_FACE) &&
                    (rawTitle == "==OVERLAY==" || rawTitle == "Overlay"))
                {
                    continue;
                }
                if (rawTitle.StartsWith("=="))
                {
                    string groupTitle = GetMainGameGroupName(rawTitle);
                    if (renderedContent || !string.Equals(groupTitle, panelTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        GUILayout.Label(groupTitle, theme.MainGameSectionHeaderStyle);
                    }
                    continue;
                }

                if (rawTitle.StartsWith("++"))
                {
                    string toggleTitle = rawTitle.Substring(2);
                    string toggleKey = category1 + "#" + toggleTitle;
                    if (controller.Category2GetFuncDict.TryGetValue(toggleKey, out CharaEditorController.Category2GetFunc getter))
                    {
                        bool oldValue = (bool)getter(controller);
                        bool newValue = DrawMainGameCheckbox(oldValue, GetMainGamePageName(toggleTitle));
                        if (oldValue != newValue)
                        {
                            controller.Category2SetFuncDict[toggleKey](controller, newValue);
                        }
                    }
                    renderedContent = true;
                    continue;
                }

                bool selected = mainGameUtilityPage == MainGameUtilityPage.None &&
                                catelogIndex2[catelogIndex1] == categoryIndex;
                bool multiSelected = catelogIndex1 == 4 && accSlotMultiSelection.Contains(rawTitle) && !selected;
                GUIStyle style = selected
                    ? theme.MainGameListSelectedStyle
                    : multiSelected
                        ? theme.MainGameListMultiSelectedStyle
                        : theme.MainGameListButtonStyle;
                string displayTitle = rawTitle;
                if (catelogIndex1 == 3)
                {
                    displayTitle = controller.GetClothDispName(rawTitle);
                }
                else if (catelogIndex1 == 4)
                {
                    if (accSlotMultiSelection.Count == 0)
                    {
                        accSlotMultiSelection.Add(rawTitle);
                    }
                    displayTitle = controller.GetAccessoryInfoByKey(rawTitle)?.AccName ?? rawTitle;
                }

                if (GUILayout.Button(GetMainGamePageName(category1, displayTitle), style))
                {
                    SelectMainGameCategory2(controller, category2List, categoryIndex, rawTitle);
                }
                renderedContent = true;
            }

            if (category1 == CharaEditorController.CT1_BODY)
            {
                DrawMainGameBodyExtraEntries(controller, category2List);
            }
            else if (category1 == CharaEditorController.CT1_CTHS)
            {
                DrawMainGameClothesExtraEntries(controller);
            }
        }

        private void DrawMainGameBodyExtraEntries(
            CharaEditorController controller,
            string[] category2List)
        {
            GUILayout.Label("Special", theme.MainGameSectionHeaderStyle);
            ChaControl character = controller?.ociTarget?.charInfo;
            if (character?.chaFile?.parameter != null)
            {
                bool oldValue = character.chaFile.parameter.futanari;
                bool newValue = DrawMainGameCheckbox(oldValue, "Futanari");
                if (newValue != oldValue)
                {
                    character.chaFile.parameter.futanari = newValue;
                }
            }

            GUILayout.Label("Mods", theme.MainGameSectionHeaderStyle);
            DrawMainGameDisabledModEntry("Randomize");
            DrawMainGameCollidersEntry();
            DrawMainGameStudioCategoryEntry("Pregnancy+", "Pregnancy +");
            int overlayIndex = Array.IndexOf(category2List, "Overlay");
            if (overlayIndex >= 0)
            {
                bool selected = mainGameUtilityPage == MainGameUtilityPage.None &&
                                catelogIndex2[catelogIndex1] == overlayIndex;
                if (GUILayout.Button(
                        "Skin Overlays",
                        selected ? theme.MainGameListSelectedStyle : theme.MainGameListButtonStyle))
                {
                    SelectMainGameCategory2(controller, category2List, overlayIndex, "Overlay");
                }
            }
            DrawMainGameDisabledModEntry("Measurements");
        }

        private void DrawMainGameClothesExtraEntries(CharaEditorController controller)
        {
            GUILayout.Label("Costume Card", theme.MainGameSectionHeaderStyle);
            if (GUILayout.Button(
                    "Save / Delete",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.CostumeSaveDelete)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.CostumeSaveDelete);
            }
            if (GUILayout.Button(
                    "Load",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.CostumeLoad)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.CostumeLoad);
            }
            GUILayout.Label("Mods", theme.MainGameSectionHeaderStyle);
            if (GUILayout.Button(
                    "Material Editor",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.MaterialEditorClothes)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.MaterialEditorClothes);
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && controller?.HasOverlayPlugin == true;
            if (GUILayout.Button(
                    "Clothes Overlays",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.ClothesOverlays)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.ClothesOverlays);
            }
            GUI.enabled = oldEnabled;
            DrawMainGameDisabledModEntry("Beaver");
            DrawMainGamePushUpEntry();
            DrawMainGameDisabledModEntry("Outfit Painter");
        }

        private void DrawMainGamePassiveModEntry(string label)
        {
            // Some Maker plug-ins do not expose a Studio window. Keep their
            // original navigation entry in place without inventing a setter.
            GUILayout.Label(label, theme.MainGameListButtonStyle);
        }

        private void DrawMainGameDisabledModEntry(string label)
        {
            GUIStyle style = new GUIStyle(theme.MainGameListButtonStyle);
            Color disabled = new Color(0.55f, 0.55f, 0.55f, 0.78f);
            style.normal.textColor = disabled;
            style.hover.textColor = disabled;
            style.active.textColor = disabled;
            style.focused.textColor = disabled;
            GUILayout.Label(label, style);
        }

        private GUIStyle GetMainGameUtilityEntryStyle(
            MainGameUtilityPage page,
            string studioCategoryName = null)
        {
            bool selected = mainGameUtilityPage == page;
            if (selected && page == MainGameUtilityPage.StudioCategory)
            {
                selected = string.Equals(
                    mainGameStudioCategoryName,
                    studioCategoryName ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
            }

            return selected
                ? theme.MainGameListSelectedStyle
                : theme.MainGameListButtonStyle;
        }

        private void DrawMainGameStudioCategoryEntry(string label, string categoryName)
        {
            bool available = FindMainGameStudioCategory(categoryName) != null;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            if (GUILayout.Button(
                    label,
                    GetMainGameUtilityEntryStyle(
                        MainGameUtilityPage.StudioCategory,
                        categoryName)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.StudioCategory, categoryName);
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGamePushUpEntry()
        {
            bool available = FindLoadedType("PushUpAI.PushUpController") != null &&
                             GetMainGamePushUpController() != null;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            if (GUILayout.Button(
                    "Push Up",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.PushUp)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.PushUp);
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameCollidersEntry()
        {
            bool available = GetMainGameColliderController() != null;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            if (GUILayout.Button(
                    "Colliders",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.Colliders)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.Colliders);
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameSettingsList()
        {
            if (mainGameSettingsPage != 0 && mainGameSettingsPage != 3)
            {
                mainGameSettingsPage = 0;
            }
            DrawMainGameSettingsPageButton(0, "Name");

            GUILayout.Label("Character Card", theme.MainGameSectionHeaderStyle);
            DrawMainGameSettingsPageButton(3, "Save / Delete");
            DrawMainGameDisabledModEntry("Fusion");

            GUILayout.Label("System", theme.MainGameSectionHeaderStyle);
            if (GUILayout.Button("Reset Windows Positions", theme.MainGameListButtonStyle))
            {
                ResetMainGamePanelPositions();
            }
            DrawMainGameSettingsUiScaleInput();
            mainGameUseMouseWheel = DrawMainGameCheckbox(mainGameUseMouseWheel, "Use Mouse Wheel in Sliders");
        }

        private void DrawMainGameSettingsUiScaleInput()
        {
            float currentScale = Mathf.Clamp(
                StudioCharaEditor.MainGameUIScale?.Value ?? 1f,
                0.75f,
                1.6f);
            if (string.IsNullOrWhiteSpace(mainGameUiScalePercentText))
            {
                mainGameUiScalePercentText = FormatMainGameUiScale(currentScale);
            }
            GUIStyle labelStyle = new GUIStyle(GetMainGameAuxiliaryLabelStyle())
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 0, 0, 0),
                contentOffset = new Vector2(0f, -1f)
            };
            GUILayout.BeginHorizontal(GUILayout.Height(34f));
            GUILayout.Space(12f);
            GUILayout.Label("UI Scale", labelStyle, GUILayout.Width(92f), GUILayout.Height(32f));
            GUI.SetNextControlName("StudioCharaEditorMainGameUiScaleSettings");
            mainGameUiScalePercentText = GUILayout.TextField(
                mainGameUiScalePercentText,
                GetMainGameAuxiliaryValueStyle(),
                GUILayout.Width(74f),
                GUILayout.Height(30f));
            if (GUILayout.Button(
                    "Apply",
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(64f),
                    GUILayout.Height(30f)))
            {
                ApplyMainGameUiScaleText();
            }
            GUILayout.Space(12f);
            GUILayout.EndHorizontal();

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter) &&
                string.Equals(
                    GUI.GetNameOfFocusedControl(),
                    "StudioCharaEditorMainGameUiScaleSettings",
                    StringComparison.Ordinal))
            {
                ApplyMainGameUiScaleText();
                GUI.FocusControl(string.Empty);
                currentEvent.Use();
            }
        }

        private void DrawMainGameSettingsPageButton(int page, string label)
        {
            GUIStyle style = mainGameSettingsPage == page
                ? theme.MainGameListSelectedStyle
                : theme.MainGameListButtonStyle;
            if (GUILayout.Button(label, style))
            {
                mainGameUtilityPage = MainGameUtilityPage.None;
                mainGameStudioCategoryName = string.Empty;
                mainGameSettingsPage = page;
                rightScroll = Vector2.zero;
                CloseSelectorSidePanel();
            }
        }

        private void DrawMainGameHairCategoryList(
            CharaEditorController controller,
            string category1,
            string[] category2List)
        {
            DrawMainGameHairToggle(controller, category1, "ColorSameSetting", "Match back and bangs color");
            DrawMainGameHairToggle(controller, category1, "ColorAutoSetting", "Auto Set root and tip colors");

            ChaControl character = controller.ociTarget?.charInfo;
            if (character?.fileHair != null)
            {
                bool oldLinked = character.fileHair.ctrlTogether;
                bool newLinked = DrawMainGameCheckbox(oldLinked, "Match Hair Axis Settings");
                if (oldLinked != newLinked)
                {
                    character.fileHair.ctrlTogether = newLinked;
                }
            }

            DrawMainGameHairPage(controller, category2List, "BackHair", "Back Hair", "Back Hair Settings");
            DrawMainGameHairPage(controller, category2List, "FrontHair", "Bangs", "Bangs Settings");
            DrawMainGameHairPage(controller, category2List, "SideHair", "Side Hair", "Side Hair Settings");
            DrawMainGameHairPage(controller, category2List, "ExtensionHair", "Hair Extensions", "Hair Extensions Settings");

            GUILayout.Label("Render Settings", theme.MainGameSectionHeaderStyle);
            if (character?.fileHair != null)
            {
                int shaderType = character.fileHair.shaderType;
                GUILayout.BeginHorizontal();
                if (DrawMainGameRadio(shaderType == 0, "Type 01", 20))
                {
                    SetMainGameHairShader(character, 0);
                }
                if (DrawMainGameRadio(shaderType == 1, "Type 02", 20))
                {
                    SetMainGameHairShader(character, 1);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Label("Mods", theme.MainGameSectionHeaderStyle);
            if (GUILayout.Button(
                    "Material Editor",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.MaterialEditorHair)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.MaterialEditorHair);
            }
            bool hairShaderAvailable = FindLoadedType(
                "HS2_HairShaderSwapper.HairShaderSwapperStudio") != null;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && hairShaderAvailable;
            if (GUILayout.Button(
                    "Hair Shader Swapper",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.HairShaderSwapper)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.HairShaderSwapper);
            }
            if (GUILayout.Button(
                    "Hair Shader Properties",
                    GetMainGameUtilityEntryStyle(MainGameUtilityPage.HairShaderProperties)))
            {
                OpenMainGameUtilityPage(MainGameUtilityPage.HairShaderProperties);
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameHairToggle(
            CharaEditorController controller,
            string category1,
            string toggleTitle,
            string displayTitle)
        {
            string key = category1 + "#" + toggleTitle;
            if (!controller.Category2GetFuncDict.TryGetValue(key, out CharaEditorController.Category2GetFunc getter))
            {
                return;
            }
            bool oldValue = (bool)getter(controller);
            bool newValue = DrawMainGameCheckbox(oldValue, displayTitle);
            if (oldValue != newValue && controller.Category2SetFuncDict.TryGetValue(key, out CharaEditorController.Category2SetFunc setter))
            {
                setter(controller, newValue);
            }
        }

        private void DrawMainGameHairPage(
            CharaEditorController controller,
            string[] category2List,
            string rawPage,
            string groupTitle,
            string displayTitle)
        {
            GUILayout.Label(groupTitle, theme.MainGameSectionHeaderStyle);
            int categoryIndex = Array.IndexOf(category2List, rawPage);
            if (categoryIndex < 0)
            {
                return;
            }
            bool selected = mainGameUtilityPage == MainGameUtilityPage.None &&
                            catelogIndex2[catelogIndex1] == categoryIndex;
            GUIStyle style = selected ? theme.MainGameListSelectedStyle : theme.MainGameListButtonStyle;
            if (GUILayout.Button(displayTitle, style))
            {
                SelectMainGameCategory2(controller, category2List, categoryIndex, rawPage);
            }
        }

        private bool DrawMainGameCheckbox(bool value, string label, params GUILayoutOption[] options)
        {
            GUIStyle labelStyle = GUI.skin.label;
            GUIContent content = new GUIContent(label);
            Rect rect = GUILayoutUtility.GetRect(28f + labelStyle.CalcSize(content).x, 27f, options);
            return DrawMainGameCheckbox(rect, value, label);
        }

        private bool DrawMainGameCheckbox(Rect rect, bool value, string label)
        {
            GUIStyle labelStyle = GUI.skin.label;
            GUIContent content = new GUIContent(label);
            Rect iconRect = new Rect(rect.x + 2f, rect.y + (rect.height - 20f) * 0.5f, 20f, 20f);
            Rect labelRect = new Rect(iconRect.xMax + 8f, rect.y, Math.Max(0f, rect.xMax - iconRect.xMax - 8f), rect.height);
            Event evt = Event.current;
            if (GUI.enabled && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                value = !value;
                evt.Use();
            }
            if (evt.type == EventType.Repaint)
            {
                Texture2D texture = value
                    ? theme.MainGameCheckboxOnTexture
                    : theme.MainGameCheckboxOffTexture;
                GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit, true);
                labelStyle.Draw(labelRect, content, false, false, value, false);
            }
            return value;
        }

        private bool DrawMainGameRadio(
            bool selected,
            string label,
            int fontSize = 0,
            float fixedWidth = 0f)
        {
            GUIStyle labelStyle = GUI.skin.toggle ?? GUI.skin.label;
            if (fontSize > 0)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = fontSize,
                    fixedHeight = 0f,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    clipping = TextClipping.Clip
                };
            }

            GUIContent content = new GUIContent(label);
            float rowHeight = fontSize > 0 ? 34f : 20f;
            GUILayoutOption widthOption = fixedWidth > 0f
                ? GUILayout.Width(fixedWidth)
                : GUILayout.ExpandWidth(true);
            Rect rect = GUILayoutUtility.GetRect(
                25f + Math.Max(1f, labelStyle.CalcSize(content).x),
                rowHeight,
                widthOption);
            if (fixedWidth > 0f)
            {
                rect.y += 2f;
            }
            Rect iconRect = new Rect(
                rect.x,
                rect.y + (rect.height - 16f) * 0.5f,
                16f,
                16f);
            Rect labelRect = new Rect(
                iconRect.xMax + 6f,
                rect.y,
                Math.Max(0f, rect.xMax - iconRect.xMax - 6f),
                rect.height);
            bool value = selected;
            Event evt = Event.current;
            if (GUI.enabled && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                value = !value;
                evt.Use();
            }
            if (evt.type == EventType.Repaint)
            {
                Texture2D toggleTexture = value ? theme.ToggleOnTexture : theme.ToggleOffTexture;
                if (toggleTexture != null)
                {
                    GUI.DrawTexture(iconRect, toggleTexture, ScaleMode.ScaleToFit, true);
                }
                labelStyle.Draw(labelRect, content, false, false, value, false);
            }
            return !selected && value;
        }

        private static void SetMainGameHairShader(ChaControl character, int shaderType)
        {
            if (character?.fileHair == null || character.fileHair.shaderType == shaderType)
            {
                return;
            }
            character.fileHair.shaderType = shaderType;
            character.ChangeSettingHairShader();
        }

        private void DrawMainGameHairModEntry(string label, string typeName, string memberName)
        {
            Type type = FindLoadedType(typeName);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && type != null;
            if (GUILayout.Button(label, theme.MainGameListButtonStyle) && type != null)
            {
                try
                {
                    PropertyInfo instanceProperty = type.GetProperty(
                        "UIInstance",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    object instance = instanceProperty?.GetValue(null, null);
                    if (instance != null)
                    {
                        PropertyInfo instanceVisible = type.GetProperty(
                            memberName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (instanceVisible?.PropertyType == typeof(bool) && instanceVisible.CanWrite)
                        {
                            instanceVisible.SetValue(instance, true, null);
                            GUI.enabled = oldEnabled;
                            return;
                        }
                    }
                    PropertyInfo property = type.GetProperty(
                        memberName,
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (property?.PropertyType == typeof(bool) && property.CanWrite)
                    {
                        property.SetValue(null, true, null);
                    }
                    else
                    {
                        FieldInfo field = type.GetField(
                            memberName,
                            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        if (field?.GetValue(null) is GameObject panel)
                        {
                            panel.SetActive(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    StudioCharaEditor.Logger?.LogWarning(label + " could not be opened: " + ex.Message);
                }
            }
            GUI.enabled = oldEnabled;
        }

        private static Type FindLoadedType(string fullName)
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

        private void SelectMainGameCategory2(
            CharaEditorController controller,
            string[] category2List,
            int categoryIndex,
            string rawTitle)
        {
            mainGameUtilityPage = MainGameUtilityPage.None;
            mainGameStudioCategoryName = string.Empty;
            if (catelogIndex2[catelogIndex1] != categoryIndex)
            {
                CloseSelectorSidePanel();
                rightScroll = Vector2.zero;
            }
            catelogIndex2[catelogIndex1] = categoryIndex;
            detailPageSelect = SelectMode.Normal;
            if (catelogIndex1 != 4)
            {
                return;
            }

            if (Event.current.shift && accSlotMultiSelection.Count > 0)
            {
                int lastIndex = Array.IndexOf(category2List, accSlotMultiSelection[accSlotMultiSelection.Count - 1]);
                if (lastIndex < 0)
                {
                    lastIndex = categoryIndex;
                }
                int from = Math.Min(categoryIndex, lastIndex);
                int to = Math.Max(categoryIndex, lastIndex);
                for (int index = from; index <= to; index++)
                {
                    if (!category2List[index].StartsWith("==") &&
                        !category2List[index].StartsWith("++") &&
                        !accSlotMultiSelection.Contains(category2List[index]))
                    {
                        accSlotMultiSelection.Add(category2List[index]);
                    }
                }
            }
            else if (Event.current.control)
            {
                if (!accSlotMultiSelection.Contains(rawTitle))
                {
                    accSlotMultiSelection.Add(rawTitle);
                }
            }
            else
            {
                accSlotMultiSelection.Clear();
                accSlotMultiSelection.Add(rawTitle);
            }
        }

        private void DrawMainGameCategoryOperations(
            CharaEditorController controller,
            string category1,
            string[] category2List)
        {
            if (catelogIndex1 == 4)
            {
                bool sortByParent = DrawModernToggle(controller.accSortByParent, LC("Sort by parent"));
                if (sortByParent != controller.accSortByParent)
                {
                    controller.accSortByParent = sortByParent;
                    controller.RefreshAccessoriesList();
                    ClearSelectorCache();
                }

                if (PluginMoreAccessories.HasMoreAccessories)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(LC("+1 Slot")))
                    {
                        PluginMoreAccessories.AddOneAccessorySlot(controller.ociTarget.charInfo);
                        controller.RefreshAccessoriesList();
                        ClearSelectorCache();
                    }
                    if (GUILayout.Button(LC("+10 Slots")))
                    {
                        PluginMoreAccessories.AddTenAccessorySlots(controller.ociTarget.charInfo);
                        controller.RefreshAccessoriesList();
                        ClearSelectorCache();
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button(LC("Copy Slot")))
                {
                    accSlotMultiSelection.Sort(CompareSlotNo);
                    accSlotClipboard.Clear();
                    foreach (string accessoryKey in accSlotMultiSelection)
                    {
                        accSlotClipboard.Add(controller.GetAccessoryDetailData(accessoryKey));
                    }
                }
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && accSlotClipboard.Count > 0;
                if (GUILayout.Button(LC("Paste Slot")))
                {
                    accSlotMultiSelection.Sort(CompareSlotNo);
                    detailPageSelect = SelectMode.PasteSlotPrompt;
                }
                GUI.enabled = oldEnabled;
                GUILayout.EndHorizontal();
            }

        }

        private void DrawMainGameGlobalOperations(CharaEditorController controller)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("Copy All"), btnstyle))
            {
                clipboard = controller.GetDataDictFull();
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && clipboard != null;
            if (GUILayout.Button(LC("Paste All"), btnstyle))
            {
                controller.SetDataDict(clipboard);
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("Revert All"), btnstyle))
            {
                controller.RevertAll();
            }
            if (GUILayout.Button(LC("Save"), btnstyle))
            {
                BeginMainGameSave();
            }
            GUILayout.EndHorizontal();
        }

        private GUIStyle GetMainGameAuxiliaryLabelStyle()
        {
            if (mainGameAuxiliaryLabelStyle == null)
            {
                mainGameAuxiliaryLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    fixedHeight = 0f,
                    padding = new RectOffset(0, 0, 0, 0),
                    contentOffset = new Vector2(0f, 1f)
                };
            }
            return mainGameAuxiliaryLabelStyle;
        }

        private GUIStyle GetMainGameAuxiliaryValueStyle()
        {
            if (mainGameAuxiliaryValueStyle == null)
            {
                mainGameAuxiliaryValueStyle = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fixedHeight = 0f,
                    padding = new RectOffset(2, 2, 0, 0),
                    margin = new RectOffset(0, 0, 2, 2),
                    // Pangram's visual baseline sits lower than Unity's font
                    // metrics. Keep the control itself 2px below the row
                    // label, but lift its glyph so digits are optically
                    // centered inside the black field.
                    contentOffset = new Vector2(0f, -2f)
                };
            }
            return mainGameAuxiliaryValueStyle;
        }

        private GUIStyle GetMainGameStatusLabelStyle()
        {
            if (mainGameStatusLabelStyle == null)
            {
                mainGameStatusLabelStyle = new GUIStyle(GetMainGameAuxiliaryLabelStyle())
                {
                    // Align the row captions with the optically centered
                    // controls without moving the controls themselves.
                    contentOffset = new Vector2(0f, -4f)
                };
            }
            return mainGameStatusLabelStyle;
        }

        private GUIStyle GetMainGameAuxiliaryButtonStyle()
        {
            if (mainGameAuxiliaryButtonStyle == null)
            {
                mainGameAuxiliaryButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    fixedHeight = 0f,
                    padding = new RectOffset(2, 2, 0, 0),
                    margin = new RectOffset(0, 0, 2, 2),
                    // Applies to chevrons and the play/pause glyph as well as
                    // ordinary button captions.
                    contentOffset = new Vector2(0f, -2f)
                };
            }
            return mainGameAuxiliaryButtonStyle;
        }

        private GUIStyle GetMainGamePlayPauseButtonStyle()
        {
            if (mainGamePlayPauseButtonStyle == null)
            {
                mainGamePlayPauseButtonStyle = new GUIStyle(GetMainGameAuxiliaryButtonStyle())
                {
                    // The caption is aligned to the status text baseline; the
                    // compact play/pause button sits slightly lower, as in Maker.
                    margin = new RectOffset(0, 0, 6, 0)
                };
            }
            return mainGamePlayPauseButtonStyle;
        }

        private GUIStyle GetMainGameAuxiliaryHeaderButtonStyle()
        {
            if (mainGameAuxiliaryHeaderButtonStyle == null)
            {
                mainGameAuxiliaryHeaderButtonStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 25,
                    alignment = TextAnchor.MiddleCenter,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 3)
                };
                Color accent = new Color32(199, 210, 46, 255);
                mainGameAuxiliaryHeaderButtonStyle.normal.background = null;
                mainGameAuxiliaryHeaderButtonStyle.hover.background = null;
                mainGameAuxiliaryHeaderButtonStyle.active.background = null;
                mainGameAuxiliaryHeaderButtonStyle.focused.background = null;
                mainGameAuxiliaryHeaderButtonStyle.normal.textColor = Color.white;
                mainGameAuxiliaryHeaderButtonStyle.hover.textColor = accent;
                mainGameAuxiliaryHeaderButtonStyle.active.textColor = accent;
                mainGameAuxiliaryHeaderButtonStyle.focused.textColor = accent;
            }
            return mainGameAuxiliaryHeaderButtonStyle;
        }

        private GUIStyle GetMainGameCollapsedWindowStyle()
        {
            if (mainGameCollapsedWindowStyle == null)
            {
                mainGameCollapsedWindowStyle = new GUIStyle(theme.MainGamePanelWindowStyle)
                {
                    padding = new RectOffset(6, 6, 4, 4)
                };
            }
            return mainGameCollapsedWindowStyle;
        }

        private GUIStyle GetMainGameCollapsedTitleStyle()
        {
            if (mainGameCollapsedTitleStyle == null)
            {
                mainGameCollapsedTitleStyle = new GUIStyle(theme.MainGameTitleStyle)
                {
                    fontSize = 17,
                    alignment = TextAnchor.MiddleLeft,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(4, 0, 0, 2)
                };
            }
            return mainGameCollapsedTitleStyle;
        }

        private void DrawMainGameStatusHeaderButtons()
        {
            DrawMainGameCollapseButton(true);
        }

        private bool TryGetMainGameAdvancedBoneMod(
            out Type advancedGuiType,
            out Component boneController)
        {
            advancedGuiType = FindLoadedType("KKABMX.GUI.KKABMX_AdvancedGUI");
            Type boneControllerType = FindLoadedType("KKABMX.Core.BoneController");
            CharaEditorController controller =
                CharaEditorMgr.Instance?.GetEditorController(ociTarget) as CharaEditorController;
            boneController = controller?.BoneController as Component;
            if (boneController == null && boneControllerType != null)
            {
                boneController = ociTarget?.charInfo?.GetComponent(boneControllerType);
            }
            return advancedGuiType != null && boneController != null;
        }

        private void DrawMainGameAdvancedBoneModToggle(float width)
        {
            bool available = TryGetMainGameAdvancedBoneMod(
                out Type advancedGuiType,
                out Component boneController);
            bool enabled = false;
            if (available)
            {
                try
                {
                    PropertyInfo enabledProperty = advancedGuiType.GetProperty(
                        "Enabled",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    enabled = enabledProperty?.PropertyType == typeof(bool) &&
                              (bool)enabledProperty.GetValue(null, null);
                }
                catch
                {
                    enabled = false;
                }
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            bool nextEnabled = DrawMainGameCompactToggle(
                enabled,
                "Advanced BoneMod window",
                GUILayout.Width(width));
            if (available && nextEnabled != enabled)
            {
                try
                {
                    if (nextEnabled)
                    {
                        InvokeMainGameStaticMethod(advancedGuiType, "Enable", boneController);
                    }
                    else
                    {
                        InvokeMainGameStaticMethod(advancedGuiType, "Disable");
                    }
                }
                catch (Exception exception)
                {
                    StudioCharaEditor.Logger?.LogWarning(
                        "Advanced BoneMod window could not be opened: " + exception.Message);
                }
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameCollapseButton(bool statusWindow)
        {
            float width = statusWindow ? mainGameStatusRect.width : mainGamePluginRect.width;
            Rect collapseRect = new Rect(width - 38f, 7f, 30f, 30f);
            if (!GUI.Button(
                    collapseRect,
                    new GUIContent("\u25BC", "Collapse"),
                    GetMainGameAuxiliaryHeaderButtonStyle()))
            {
                return;
            }

            if (statusWindow)
            {
                if (!mainGameStatusCollapsedPositionInitialized)
                {
                    mainGameCollapsedStatusRect.position = new Vector2(
                        mainGameStatusRect.xMax - mainGameCollapsedStatusRect.width,
                        mainGameStatusRect.y);
                    mainGameStatusCollapsedPositionInitialized = true;
                }
                mainGameStatusCollapsed = true;
            }
            else
            {
                if (!mainGamePluginCollapsedPositionInitialized)
                {
                    mainGameCollapsedPluginRect.position = new Vector2(
                        mainGamePluginRect.xMax - mainGameCollapsedPluginRect.width,
                        mainGamePluginRect.y);
                    mainGamePluginCollapsedPositionInitialized = true;
                }
                mainGamePluginCollapsed = true;
            }
            ClampMainGamePanelRects();
            PersistMainGamePanelPositions();
        }

        private void DrawMainGameCollapsedStatusWindow(int windowId)
        {
            DrawMainGameCollapsedAuxiliaryWindow("Status", true);
        }

        private void DrawMainGameCollapsedPluginWindow(int windowId)
        {
            DrawMainGameCollapsedAuxiliaryWindow("Plugin settings", false);
        }

        private void DrawMainGameCollapsedAuxiliaryWindow(string title, bool statusWindow)
        {
            HandleMainGameWindowFocus(
                statusWindow ? MainGameCollapsedStatusWindowId : MainGameCollapsedPluginWindowId);
            float width = statusWindow
                ? mainGameCollapsedStatusRect.width
                : mainGameCollapsedPluginRect.width;
            float height = statusWindow
                ? mainGameCollapsedStatusRect.height
                : mainGameCollapsedPluginRect.height;
            GUI.Label(new Rect(7f, 2f, Math.Max(1f, width - 40f), height - 4f), title, GetMainGameCollapsedTitleStyle());
            if (GUI.Button(
                    new Rect(width - 34f, 6f, 28f, 28f),
                    new GUIContent("\u25B2", "Restore"),
                    GetMainGameAuxiliaryHeaderButtonStyle()))
            {
                if (statusWindow)
                {
                    mainGameStatusCollapsed = false;
                }
                else
                {
                    mainGamePluginCollapsed = false;
                }
                PersistMainGamePanelPositions();
            }
            GUI.DragWindow(new Rect(0f, 0f, Math.Max(1f, width - 36f), height));
        }

        private void DrawMainGameStatusWindow(int windowId)
        {
            HandleMainGameWindowFocus(windowId);
            DrawMainGameWindowTitle("Status", 20, mainGameStatusRect.width);
            DrawMainGameStatusHeaderButtons();
            GUILayout.Space(4f);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && ociTarget?.charInfo != null;
            ChaControl character = ociTarget?.charInfo;

            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            int eyesLook = character?.GetLookEyesPtn() ?? 0;
            GUILayout.Label("Look", GetMainGameStatusLabelStyle(), GUILayout.Width(60f), GUILayout.Height(36f));
            if (DrawMainGameRadio(eyesLook == 1, "Camera", 16, 90f)) ociTarget.ChangeLookEyesPtn(1, true);
            if (DrawMainGameRadio(eyesLook == 0, "Front", 16, 80f)) ociTarget.ChangeLookEyesPtn(0, true);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Play Pose", GetMainGameStatusLabelStyle(), GUILayout.Width(70f), GUILayout.Height(36f));
            bool posePlaying = ociTarget != null && ociTarget.animeSpeed > 0f;
            if (GUILayout.Button(
                    posePlaying ? "II" : ">",
                    GetMainGamePlayPauseButtonStyle(),
                    GUILayout.Width(32f),
                    GUILayout.Height(32f)))
            {
                ociTarget.animeSpeed = posePlaying ? 0f : 1f;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            int neckLook = character?.GetLookNeckPtn() ?? 0;
            GUILayout.Label("Neck", GetMainGameStatusLabelStyle(), GUILayout.Width(60f), GUILayout.Height(36f));
            if (DrawMainGameRadio(neckLook == 1, "Camera", 16, 90f)) ociTarget.ChangeLookNeckPtn(1);
            if (DrawMainGameRadio(neckLook == 3, "Pose", 16, 80f)) ociTarget.ChangeLookNeckPtn(3);
            GUILayout.EndHorizontal();

            float stepperColumnWidth = Math.Max(170f, (mainGameStatusRect.width - 30f) * 0.5f);
            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            GUILayout.BeginHorizontal(GUILayout.Width(stepperColumnWidth), GUILayout.Height(36f));
            DrawMainGamePoseStepper(character);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Width(stepperColumnWidth), GUILayout.Height(36f));
            DrawMainGameStatusPatternStepper(
                "Eyebrows",
                "eyebrows",
                character,
                character?.GetEyebrowPtn() ?? 0,
                ChaListDefine.CategoryNo.custom_eyebrow_m,
                ChaListDefine.CategoryNo.custom_eyebrow_f,
                value => character.ChangeEyebrowPtn(value, true));
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            GUILayout.BeginHorizontal(GUILayout.Width(stepperColumnWidth), GUILayout.Height(36f));
            DrawMainGameStatusPatternStepper(
                "Eyes",
                "eyes",
                character,
                character?.GetEyesPtn() ?? 0,
                ChaListDefine.CategoryNo.custom_eye_m,
                ChaListDefine.CategoryNo.custom_eye_f,
                value => character.ChangeEyesPtn(value, true));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Width(stepperColumnWidth), GUILayout.Height(36f));
            DrawMainGameStatusPatternStepper(
                "Mouth",
                "mouth",
                character,
                character?.GetMouthPtn() ?? 0,
                ChaListDefine.CategoryNo.custom_mouth_m,
                ChaListDefine.CategoryNo.custom_mouth_f,
                value => character.ChangeMouthPtn(value, true));
            GUILayout.EndHorizontal();
            GUILayout.EndHorizontal();
            GUI.enabled = oldEnabled;
            GUI.DragWindow(new Rect(0f, 0f, Math.Max(0f, mainGameStatusRect.width - 42f), 42f));
        }

        private void DrawMainGamePluginWindow(int windowId)
        {
            HandleMainGameWindowFocus(windowId);
            DrawMainGameWindowTitle("Plugin settings", 20, mainGamePluginRect.width);
            DrawMainGameCollapseButton(false);
            GUILayout.Space(4f);
            float columnWidth = Math.Max(150f, (mainGamePluginRect.width - 30f) * 0.5f);
            GUILayout.BeginHorizontal(GUILayout.Height(38f));
            DrawMainGameExternalConfigToggle(
                "com.gebo.BepInEx.TranslationHelper", "Maker", "Save Translated Names",
                "Save with translated names", columnWidth);
            DrawMainGameExternalConfigToggle(
                "HS2_HLightControl", "Defaults", "Lock Camlight",
                "Lock Cameralight", columnWidth);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Height(38f));
            DrawMainGameReflectionToggle(
                "com.shallty.blendshapecreator", "BlendshapeCreator.BlendshapeCreator", "toggleUI",
                "Blendshape Creator", columnWidth);
            DrawMainGameAdvancedBoneModToggle(columnWidth);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Height(38f));
            bool oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            GUI.enabled = false;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * 0.42f);
            DrawMainGameCompactToggle(false, "Coordinate Visibility Rules", GUILayout.Width(columnWidth));
            GUI.color = oldColor;
            GUI.enabled = oldEnabled;
            DrawMainGameExternalConfigToggle(
                "HS2_HLightControl", "Defaults", "Backlight",
                "Toggle Backlight", columnWidth);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Height(38f));
            DrawMainGameHeightBarToggle(columnWidth);
            bool blinking = ociTarget?.charInfo?.GetEyesBlinkFlag() ?? false;
            oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && ociTarget?.charInfo != null;
            bool newBlinking = DrawMainGameCompactToggle(blinking, "Toggle Blinking", GUILayout.Width(columnWidth));
            if (newBlinking != blinking) ociTarget.ChangeBlink(newBlinking);
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal(GUILayout.Height(38f));
            DrawMainGameExternalConfigToggle(
                "KKABMX.Core", "Maker", "Use XYZ scale sliders",
                "Split XYZ scale sliders", columnWidth);
            GUILayout.Space(columnWidth);
            GUILayout.EndHorizontal();
            GUI.DragWindow(new Rect(0f, 0f, Math.Max(0f, mainGamePluginRect.width - 42f), 42f));
        }

        private void DrawMainGameCompactUiScaleSetting()
        {
            float currentScale = Mathf.Clamp(StudioCharaEditor.MainGameUIScale?.Value ?? 1f, 0.75f, 1.6f);
            if (string.IsNullOrWhiteSpace(mainGameUiScalePercentText))
            {
                mainGameUiScalePercentText = FormatMainGameUiScale(currentScale);
            }

            Rect rowRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(36f),
                GUILayout.ExpandWidth(true));
            float applyWidth = Mathf.Clamp(rowRect.width * 0.22f, 58f, 72f);
            float valueWidth = Mathf.Clamp(rowRect.width * 0.19f, 54f, 66f);
            float labelWidth = Math.Max(74f, rowRect.width - applyWidth - valueWidth - 12f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect valueRect = new Rect(labelRect.xMax + 6f, rowRect.y + 2f, valueWidth, rowRect.height - 4f);
            Rect applyRect = new Rect(valueRect.xMax + 6f, rowRect.y + 2f, applyWidth, rowRect.height - 4f);
            GUI.Label(labelRect, "UI Scale", GetMainGameAuxiliaryLabelStyle());
            GUI.SetNextControlName("StudioCharaEditorMainGameUiScaleCompact");
            mainGameUiScalePercentText = GUI.TextField(
                valueRect,
                mainGameUiScalePercentText,
                GetMainGameAuxiliaryValueStyle());
            if (GUI.Button(applyRect, "Apply", GetMainGameAuxiliaryButtonStyle()))
            {
                ApplyMainGameUiScaleText();
            }

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter) &&
                string.Equals(
                    GUI.GetNameOfFocusedControl(),
                    "StudioCharaEditorMainGameUiScaleCompact",
                    StringComparison.Ordinal))
            {
                ApplyMainGameUiScaleText();
                GUI.FocusControl(string.Empty);
                currentEvent.Use();
            }
        }

        private void DrawMainGamePoseStepper(ChaControl character)
        {
            GUILayout.Label(
                "Pose",
                GetMainGameStatusLabelStyle(),
                GUILayout.Width(70f),
                GUILayout.Height(36f));
            int[] keys = GetMainGameStatusKeys(
                character,
                ChaListDefine.CategoryNo.custom_pose_m,
                ChaListDefine.CategoryNo.custom_pose_f);
            string inputKey = "pose:" + (character?.GetInstanceID() ?? 0);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && keys.Length > 1;
            if (GUILayout.Button("<", GetMainGameAuxiliaryButtonStyle(), GUILayout.Width(28f), GUILayout.Height(32f)))
            {
                mainGameMakerPoseIndex--;
                if (mainGameMakerPoseIndex < 1) mainGameMakerPoseIndex = keys.Length - 1;
                LoadMainGameMakerPose(character, keys, mainGameMakerPoseIndex);
                ResetMainGameStatusValueInput(inputKey);
            }
            mainGameMakerPoseIndex = Mathf.Clamp(mainGameMakerPoseIndex, 1, Math.Max(1, keys.Length - 1));
            DrawMainGameEditableStatusValue(
                inputKey,
                mainGameMakerPoseIndex,
                1,
                Math.Max(1, keys.Length - 1),
                value =>
                {
                    mainGameMakerPoseIndex = value;
                    LoadMainGameMakerPose(character, keys, mainGameMakerPoseIndex);
                });
            if (GUILayout.Button(">", GetMainGameAuxiliaryButtonStyle(), GUILayout.Width(28f), GUILayout.Height(32f)))
            {
                mainGameMakerPoseIndex++;
                if (mainGameMakerPoseIndex >= keys.Length) mainGameMakerPoseIndex = 1;
                LoadMainGameMakerPose(character, keys, mainGameMakerPoseIndex);
                ResetMainGameStatusValueInput(inputKey);
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameStatusPatternStepper(
            string label,
            string inputName,
            ChaControl character,
            int currentValue,
            ChaListDefine.CategoryNo maleCategory,
            ChaListDefine.CategoryNo femaleCategory,
            Action<int> setter)
        {
            int[] keys = GetMainGameStatusKeys(character, maleCategory, femaleCategory);
            int currentIndex = Array.IndexOf(keys, currentValue);
            currentIndex = Mathf.Clamp(currentIndex, 0, Math.Max(0, keys.Length - 1));
            string inputKey = inputName + ":" + (character?.GetInstanceID() ?? 0);
            GUILayout.Label(
                label,
                GetMainGameStatusLabelStyle(),
                GUILayout.Width(70f),
                GUILayout.Height(36f));
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && keys.Length > 0;
            if (GUILayout.Button("<", GetMainGameAuxiliaryButtonStyle(), GUILayout.Width(28f), GUILayout.Height(32f)) && keys.Length > 0)
            {
                int nextIndex = (currentIndex + keys.Length - 1) % keys.Length;
                setter(keys[nextIndex]);
                ResetMainGameStatusValueInput(inputKey);
            }
            DrawMainGameEditableStatusValue(
                inputKey,
                currentIndex + 1,
                1,
                Math.Max(1, keys.Length),
                value => setter(keys[value - 1]));
            if (GUILayout.Button(">", GetMainGameAuxiliaryButtonStyle(), GUILayout.Width(28f), GUILayout.Height(32f)) && keys.Length > 0)
            {
                int nextIndex = (currentIndex + 1) % keys.Length;
                setter(keys[nextIndex]);
                ResetMainGameStatusValueInput(inputKey);
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameEditableStatusValue(
            string inputKey,
            int currentValue,
            int minimumValue,
            int maximumValue,
            Action<int> setter)
        {
            string controlName = "StudioCharaEditor.Status." + inputKey;
            bool focused = string.Equals(
                GUI.GetNameOfFocusedControl(),
                controlName,
                StringComparison.Ordinal);
            if (!mainGameStatusValueInputs.TryGetValue(inputKey, out string input) || !focused)
            {
                input = currentValue.ToString(CultureInfo.InvariantCulture);
            }

            GUI.SetNextControlName(controlName);
            string nextInput = GUILayout.TextField(
                input,
                GetMainGameAuxiliaryValueStyle(),
                GUILayout.Width(40f),
                GUILayout.Height(32f));
            mainGameStatusValueInputs[inputKey] = nextInput;
            if (!string.Equals(nextInput, input, StringComparison.Ordinal) &&
                int.TryParse(nextInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) &&
                parsed >= minimumValue &&
                parsed <= maximumValue &&
                parsed != currentValue)
            {
                setter(parsed);
            }
        }

        private void ResetMainGameStatusValueInput(string inputKey)
        {
            mainGameStatusValueInputs.Remove(inputKey);
            GUI.FocusControl(string.Empty);
        }

        private static int[] GetMainGameStatusKeys(
            ChaControl character,
            ChaListDefine.CategoryNo maleCategory,
            ChaListDefine.CategoryNo femaleCategory)
        {
            if (character == null || Singleton<Manager.Character>.Instance?.chaListCtrl == null)
            {
                return new int[0];
            }
            ChaListDefine.CategoryNo category = character.sex == 0 ? maleCategory : femaleCategory;
            Dictionary<int, ListInfoBase> entries = Singleton<Manager.Character>.Instance.chaListCtrl.GetCategoryInfo(category);
            if (entries == null || entries.Count == 0)
            {
                return new int[0];
            }
            int[] keys = new int[entries.Count];
            entries.Keys.CopyTo(keys, 0);
            return keys;
        }

        private static void LoadMainGameMakerPose(ChaControl character, int[] keys, int poseIndex)
        {
            if (character == null || poseIndex < 0 || poseIndex >= keys.Length)
            {
                return;
            }
            ChaListDefine.CategoryNo category = character.sex == 0
                ? ChaListDefine.CategoryNo.custom_pose_m
                : ChaListDefine.CategoryNo.custom_pose_f;
            Dictionary<int, ListInfoBase> entries = Singleton<Manager.Character>.Instance.chaListCtrl.GetCategoryInfo(category);
            if (entries == null || !entries.TryGetValue(keys[poseIndex], out ListInfoBase pose))
            {
                return;
            }
            string manifest = pose.GetInfo(ChaListDefine.KeyType.MainManifest);
            string bundle = pose.GetInfo(ChaListDefine.KeyType.MainAB);
            string asset = pose.GetInfo(ChaListDefine.KeyType.MainData);
            string clip = pose.GetInfo(ChaListDefine.KeyType.Clip);
            character.LoadAnimation(bundle, asset, manifest);
            character.AnimPlay(clip);
            character.resetDynamicBoneAll = true;
        }

        private void DrawMainGameExternalConfigToggle(
            string pluginGuid,
            string section,
            string key,
            string label,
            float width)
        {
            bool available = TryGetExternalBoolConfig(pluginGuid, section, key, out ConfigEntry<bool> entry);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            bool oldValue = available && entry.Value;
            bool newValue = DrawMainGameCompactToggle(oldValue, label, GUILayout.Width(width));
            if (available && newValue != oldValue)
            {
                entry.Value = newValue;
            }
            GUI.enabled = oldEnabled;
        }

        private static bool TryGetExternalBoolConfig(
            string pluginGuid,
            string section,
            string key,
            out ConfigEntry<bool> entry)
        {
            entry = null;
            return Chainloader.PluginInfos.TryGetValue(pluginGuid, out var pluginInfo) &&
                   pluginInfo?.Instance?.Config != null &&
                   pluginInfo.Instance.Config.TryGetEntry(new ConfigDefinition(section, key), out entry);
        }

        private void DrawMainGameReflectionToggle(
            string pluginGuid,
            string typeName,
            string fieldName,
            string label,
            float width)
        {
            FieldInfo field = null;
            bool value = false;
            bool available = Chainloader.PluginInfos.ContainsKey(pluginGuid) &&
                             TryGetStaticBoolField(typeName, fieldName, out field, out value);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            bool newValue = DrawMainGameCompactToggle(value, label, GUILayout.Width(width));
            if (available && newValue != value)
            {
                field.SetValue(null, newValue);
            }
            GUI.enabled = oldEnabled;
        }

        private static bool TryGetStaticBoolField(
            string typeName,
            string fieldName,
            out FieldInfo field,
            out bool value)
        {
            field = null;
            value = false;
            Type type = FindLoadedType(typeName);
            field = type?.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.FieldType != typeof(bool))
            {
                return false;
            }
            value = (bool)field.GetValue(null);
            return true;
        }

        private void DrawMainGameCoordinateRulesToggle(float width)
        {
            const string guid = "orange.spork.additionalaccessorycontrolsplugin";
            bool available = Chainloader.PluginInfos.TryGetValue(guid, out var pluginInfo) && pluginInfo?.Instance != null;
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            bool newValue = DrawMainGameCompactToggle(mainGameCoordinateRulesVisible, "Coordinate Visibility Rules", GUILayout.Width(width));
            if (available && newValue != mainGameCoordinateRulesVisible)
            {
                MethodInfo method = pluginInfo.Instance.GetType().GetMethod(
                    "ShowCoordinateRulesGUI",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                try
                {
                    method?.Invoke(pluginInfo.Instance, new object[] { newValue });
                    mainGameCoordinateRulesVisible = newValue;
                }
                catch (Exception ex)
                {
                    StudioCharaEditor.Logger?.LogWarning("Coordinate Visibility Rules is unavailable in Studio: " + ex.Message);
                }
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameHeightBarToggle(float width)
        {
            const string guid = "HeightBar";
            bool available = Chainloader.PluginInfos.TryGetValue(guid, out var pluginInfo) && pluginInfo?.Instance != null;
            FieldInfo field = available
                ? pluginInfo.Instance.GetType().GetField("_showBar", BindingFlags.Instance | BindingFlags.NonPublic)
                : null;
            available = available && field?.FieldType == typeof(bool);
            bool value = available && (bool)field.GetValue(pluginInfo.Instance);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && available;
            bool newValue = DrawMainGameCompactToggle(value, "Show height measure bar", GUILayout.Width(width));
            if (available && newValue != value)
            {
                field.SetValue(pluginInfo.Instance, newValue);
            }
            GUI.enabled = oldEnabled;
        }

        private bool DrawMainGameCompactToggle(bool value, string label, params GUILayoutOption[] options)
        {
            GUIStyle labelStyle = new GUIStyle(GetMainGameAuxiliaryLabelStyle())
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            GUIContent content = new GUIContent(label);
            Rect rect = GUILayoutUtility.GetRect(24f + labelStyle.CalcSize(content).x, 38f, options);
            Rect iconRect = new Rect(rect.x, rect.y + (rect.height - 17f) * 0.5f, 17f, 17f);
            Rect labelRect = new Rect(iconRect.xMax + 5f, rect.y, Math.Max(0f, rect.xMax - iconRect.xMax - 5f), rect.height);
            Event evt = Event.current;
            if (GUI.enabled && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                value = !value;
                evt.Use();
            }
            if (evt.type == EventType.Repaint)
            {
                Texture2D texture = value ? theme.ToggleOnTexture : theme.ToggleOffTexture;
                GUI.DrawTexture(iconRect, texture, ScaleMode.ScaleToFit, true);
                labelStyle.Draw(labelRect, content, false, false, value, false);
            }
            return value;
        }

        private void DrawMainGameRightWindow(int windowId)
        {
            HandleMainGameWindowFocus(windowId);
            HandleMainGameResizeGripInput(
                windowId,
                mainGameRightRect,
                MainGameMinimumRightWidth,
                MainGameMinimumRightHeight,
                true);
            mainGameRightContentWidth = Math.Max(120f, mainGameRightRect.width - 56f);
            mainGameSelectorVisibleThisFrame = false;
            mainGameVisibleSelectorKey = string.Empty;
            DrawMainGameWindowTitle(mainGameCurrentRightTitle, 24, mainGameRightRect.width);
            if (mainGameUtilityPage != MainGameUtilityPage.None)
            {
                DrawMainGameUtilityPage();
            }
            else if (mainGameSettingsOpen)
            {
                DrawMainGamePluginSettings();
            }
            else if (TryGetMainGameContext(
                         out CharaEditorController controller,
                         out string category1,
                         out string category2,
                         out string detailSetKey))
            {
                DrawMainGameCharacterDetail(controller, category1, category2, detailSetKey);
            }
            else
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label(LC("Please select a charactor to edit."), largeLabel);
                GUILayout.FlexibleSpace();
            }

            Rect closeRect = new Rect(mainGameRightRect.width - 38f, 8f, 28f, 28f);
            if (GUI.Button(closeRect, GUIContent.none, closeButtonStyle ?? GUI.skin.button))
            {
                if (mainGameUtilityPage != MainGameUtilityPage.None)
                {
                    mainGameUtilityPage = MainGameUtilityPage.None;
                    mainGameStudioCategoryName = string.Empty;
                    rightScroll = Vector2.zero;
                }
                else
                {
                    CloseEditorFromMainGame();
                }
            }
            if (mainGameSelectorVisibleThisFrame)
            {
                Rect selectedRect = new Rect(mainGameRightRect.width - 74f, 8f, 28f, 28f);
                GUIStyle selectedButtonStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 25,
                    alignment = TextAnchor.MiddleCenter,
                    contentOffset = new Vector2(0f, -3f)
                };
                selectedButtonStyle.normal.background = null;
                selectedButtonStyle.hover.background = null;
                selectedButtonStyle.active.background = null;
                selectedButtonStyle.normal.textColor = Color.white;
                selectedButtonStyle.hover.textColor = new Color32(112, 205, 67, 255);
                selectedButtonStyle.active.textColor = new Color32(112, 205, 67, 255);
                selectedButtonStyle.focused.textColor = new Color32(112, 205, 67, 255);
                if (GUI.Button(selectedRect, "\u25BC", selectedButtonStyle))
                {
                    mainGameScrollToSelectorKey = mainGameVisibleSelectorKey;
                }
            }
            DrawMainGameResizeGrip(
                windowId,
                mainGameRightRect,
                true);
            GUI.DragWindow(new Rect(0f, 0f, Math.Max(0f, mainGameRightRect.width - 44f), 42f));
        }

        private string GetMainGameUtilityPageTitle()
        {
            switch (mainGameUtilityPage)
            {
                case MainGameUtilityPage.MaterialEditorClothes:
                case MainGameUtilityPage.MaterialEditorHair:
                    return "Material Editor";
                case MainGameUtilityPage.ClothesOverlays:
                    return "Clothes Overlays";
                case MainGameUtilityPage.PushUp:
                    return "Push Up";
                case MainGameUtilityPage.Colliders:
                    return "Colliders";
                case MainGameUtilityPage.HairShaderSwapper:
                    return "Hair Shader Swapper";
                case MainGameUtilityPage.HairShaderProperties:
                    return "Hair Shader Properties";
                case MainGameUtilityPage.CostumeSaveDelete:
                case MainGameUtilityPage.CostumeLoad:
                    return "Costume Card";
                case MainGameUtilityPage.StudioCategory:
                    return string.IsNullOrEmpty(mainGameStudioCategoryName)
                        ? "Plugin"
                        : mainGameStudioCategoryName;
                default:
                    return LC("Studio Character Editor");
            }
        }

        private void OpenMainGameUtilityPage(MainGameUtilityPage page, string studioCategoryName = null)
        {
            mainGameUtilityPage = page;
            mainGameStudioCategoryName = studioCategoryName ?? string.Empty;
            mainGameSettingsOpen = false;
            detailPageSelect = SelectMode.Normal;
            rightScroll = Vector2.zero;
            if (page == MainGameUtilityPage.HairShaderSwapper ||
                page == MainGameUtilityPage.HairShaderProperties)
            {
                mainGameHairShaderControlsNeedRefresh = true;
                mainGameHairShaderStatus = string.Empty;
                mainGameHairShaderInitializationAttempted = false;
            }
            if (page == MainGameUtilityPage.CostumeSaveDelete ||
                page == MainGameUtilityPage.CostumeLoad)
            {
                mainGameCoordinateCardsNeedRefresh = true;
                mainGameSelectedCoordinateCard = -1;
                mainGameSelectedCoordinateCardPath = string.Empty;
                mainGameCoordinateDeleteConfirmation = -1;
                mainGameCoordinateCardStatus = string.Empty;
                mainGameCoordinateScroll = Vector2.zero;
                mainGameCoordinateFilterDirty = true;
            }
            CloseSelectorSidePanel();
        }

        private void DrawMainGameUtilityPage()
        {
            if (mainGameUtilityPage == MainGameUtilityPage.CostumeSaveDelete ||
                mainGameUtilityPage == MainGameUtilityPage.CostumeLoad)
            {
                float coordinateWidth = Math.Max(120f, mainGameRightRect.width - 56f);
                mainGameRightContentWidth = coordinateWidth;
                GUILayout.BeginVertical(
                    GUILayout.Width(coordinateWidth),
                    GUILayout.MaxWidth(coordinateWidth),
                    GUILayout.ExpandHeight(true));
                DrawMainGameCoordinateCardsPage(
                    mainGameUtilityPage == MainGameUtilityPage.CostumeSaveDelete);
                GUILayout.EndVertical();
                return;
            }

            rightScroll.x = 0f;
            rightScroll = GUILayout.BeginScrollView(
                rightScroll,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.ExpandHeight(true));
            float width = Math.Max(120f, mainGameRightRect.width - 56f);
            mainGameRightContentWidth = width;
            GUILayout.BeginVertical(
                GUILayout.Width(width),
                GUILayout.MaxWidth(width),
                GUILayout.ExpandHeight(false));
            switch (mainGameUtilityPage)
            {
                case MainGameUtilityPage.MaterialEditorClothes:
                    DrawMainGameMaterialEditorSlotPage(false);
                    break;
                case MainGameUtilityPage.MaterialEditorHair:
                    DrawMainGameMaterialEditorSlotPage(true);
                    break;
                case MainGameUtilityPage.ClothesOverlays:
                    DrawMainGameClothesOverlaysPage();
                    break;
                case MainGameUtilityPage.PushUp:
                    DrawMainGamePushUpPage();
                    break;
                case MainGameUtilityPage.Colliders:
                    DrawMainGameCollidersPage();
                    break;
                case MainGameUtilityPage.HairShaderSwapper:
                    DrawMainGameHairShaderSwapperPage();
                    break;
                case MainGameUtilityPage.HairShaderProperties:
                    DrawMainGameHairShaderPropertiesPage();
                    break;
                case MainGameUtilityPage.StudioCategory:
                    DrawMainGameStudioCategoryPage(mainGameStudioCategoryName);
                    break;
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private object GetMainGamePushUpController()
        {
            ChaControl character = ociTarget?.charInfo;
            Type controllerType = FindLoadedType("PushUpAI.PushUpController");
            return character == null || controllerType == null
                ? null
                : character.gameObject.GetComponent(controllerType);
        }

        private void DrawMainGamePushUpPage()
        {
            object controller = GetMainGamePushUpController();
            object info = GetMainGameReflectionMember(controller, "Info");
            object cloth = GetMainGameReflectionMember(
                info,
                mainGamePushUpMode == 0 ? "Bra" : "Top");
            if (cloth == null)
            {
                GUILayout.Label("PushUpAI is not available for the selected character.");
                return;
            }

            mainGamePushUpMode = DrawMainGameChoiceRow(
                "Type",
                new[] { "Bra", "Top" },
                Mathf.Clamp(mainGamePushUpMode, 0, 1));
            cloth = GetMainGameReflectionMember(
                info,
                mainGamePushUpMode == 0 ? "Bra" : "Top");
            bool changed = false;
            changed |= DrawMainGameReflectedBool(cloth, "EnablePushUp", "Enabled", true);

            changed |= DrawMainGameReflectedPercentSlider(cloth, "Firmness", "Firmness");
            changed |= DrawMainGameReflectedPercentSlider(cloth, "Lift", "Lift");
            changed |= DrawMainGameReflectedPercentSlider(cloth, "PushTogether", "Push Together");
            changed |= DrawMainGameReflectedPercentSlider(cloth, "Squeeze", "Squeeze");
            changed |= DrawMainGameReflectedPercentSlider(cloth, "CenterNipples", "Center Nipples");

            changed |= DrawMainGameReflectedBool(cloth, "FlattenNipples", "Flatten Nipples", false);
            changed |= DrawMainGameReflectedBool(cloth, "HideAccessories", "Hide Accessories", false);
            DrawMainGameDivider();
            changed |= DrawMainGameReflectedBool(cloth, "HideNipples", "Hide Nipples", false);
            DrawMainGameDivider();
            changed |= DrawMainGameReflectedPercentSlider(cloth, "Corset", "Corset");
            changed |= DrawMainGameReflectedBool(
                cloth,
                "CorsetHalf",
                "Corset active for Half-Off",
                false);

            if (changed)
            {
                InvokeMainGameMethod(controller, "RecalculateBody");
                InvokeMainGameMethod(controller, "ApplyBreastSoftness");
            }
        }

        private bool DrawMainGameReflectedBool(
            object instance,
            string memberName,
            string label,
            bool fallback)
        {
            object value = GetMainGameReflectionMember(instance, memberName);
            bool oldValue = value is bool current ? current : fallback;
            bool newValue = DrawMainGameCheckbox(oldValue, label);
            if (newValue == oldValue)
            {
                return false;
            }
            SetMainGameReflectionMember(instance, memberName, newValue);
            return true;
        }

        private bool DrawMainGameReflectedPercentSlider(
            object instance,
            string memberName,
            string label)
        {
            object value = GetMainGameReflectionMember(instance, memberName);
            float oldValue = value == null
                ? 0f
                : Convert.ToSingle(value, CultureInfo.InvariantCulture);
            float newValue = DrawMainGameRawSliderRow(
                label,
                oldValue,
                0f,
                1f,
                null,
                false,
                true);
            if (Mathf.Approximately(newValue, oldValue))
            {
                return false;
            }
            SetMainGameReflectionMember(instance, memberName, newValue);
            return true;
        }

        private object GetMainGameColliderController()
        {
            ChaControl character = ociTarget?.charInfo;
            Type controllerType = FindLoadedType("CharaColliderEditor.ColliderController");
            return character == null || controllerType == null
                ? null
                : character.gameObject.GetComponent(controllerType);
        }

        private void DrawMainGameCollidersPage()
        {
            ChaControl character = ociTarget?.charInfo;
            object controller = GetMainGameColliderController();
            if (character == null || controller == null)
            {
                GUILayout.Label("Chara Collider Editor is not available for the selected character.");
                return;
            }

            int characterId = character.GetInstanceID();
            if (mainGameColliderCharacterId != characterId)
            {
                mainGameColliderCharacterId = characterId;
                mainGameColliderIndex = 0;
                mainGameColliderDropdownOpen = false;
            }

            IList colliders = GetMainGameReflectionMember(controller, "colliders") as IList;
            if (colliders == null || colliders.Count == 0)
            {
                InvokeMainGameMethod(controller, "RefreshColliders", true);
                colliders = GetMainGameReflectionMember(controller, "colliders") as IList;
            }
            if (colliders == null || colliders.Count == 0)
            {
                GUILayout.Label("No character colliders were found.");
                return;
            }

            mainGameColliderIndex = Mathf.Clamp(mainGameColliderIndex, 0, colliders.Count - 1);
            object collider = colliders[mainGameColliderIndex];
            string colliderName = GetMainGameReflectionString(collider, "Name") ??
                                  ("Collider " + (mainGameColliderIndex + 1));
            GUILayout.Label("Colliders", theme.MainGameBreadcrumbStyle);
            if (DrawMainGameFullWidthButton(colliderName + "  \u25BC"))
            {
                mainGameColliderDropdownOpen = !mainGameColliderDropdownOpen;
            }
            if (mainGameColliderDropdownOpen)
            {
                for (int index = 0; index < colliders.Count; index++)
                {
                    object option = colliders[index];
                    string optionName = GetMainGameReflectionString(option, "Name") ??
                                        ("Collider " + (index + 1));
                    if (DrawMainGameFullWidthButton(optionName))
                    {
                        mainGameColliderIndex = index;
                        collider = option;
                        mainGameColliderDropdownOpen = false;
                    }
                }
            }

            object nativeCollider = GetMainGameReflectionMember(collider, "collider");
            SetMainGameReflectionMember(controller, "highlightedCollider", nativeCollider);
            DrawMainGameDivider();

            DrawMainGameReflectedBool(collider, "Bound", "Pull dynamic bones inside", false);
            int direction = Convert.ToInt32(
                GetMainGameReflectionMember(collider, "Direction") ?? 0,
                CultureInfo.InvariantCulture);
            int newDirection = DrawMainGameChoiceRow(
                "Direction",
                new[] { "X", "Y", "Z" },
                Mathf.Clamp(direction, 0, 2));
            if (newDirection != direction)
            {
                SetMainGameReflectionMember(collider, "Direction", newDirection);
            }

            object defaults = GetMainGameReflectionMember(collider, "defaultData");
            float radius = GetMainGameFloat(collider, "Radius");
            float defaultRadius = GetMainGameFloat(defaults, "m_Radius");
            SetMainGameFloatIfChanged(
                collider,
                "Radius",
                radius,
                DrawMainGameRawSliderRow(
                    "Radius",
                    radius,
                    0f,
                    Math.Max(10f, Math.Max(radius, defaultRadius)),
                    defaultRadius,
                    false,
                    true));

            float height = GetMainGameFloat(collider, "Height");
            float defaultHeight = GetMainGameFloat(defaults, "m_Height");
            SetMainGameFloatIfChanged(
                collider,
                "Height",
                height,
                DrawMainGameRawSliderRow(
                    "Height",
                    height,
                    0f,
                    Math.Max(20f, Math.Max(height, defaultHeight)),
                    defaultHeight,
                    false,
                    true));

            DrawMainGameDivider();
            Vector3 defaultCenter = GetMainGameReflectionMember(defaults, "m_Center") is Vector3 center
                ? center
                : Vector3.zero;
            DrawMainGameColliderCenterSlider(collider, "CenterX", "Center (X)", defaultCenter.x);
            DrawMainGameColliderCenterSlider(collider, "CenterY", "Center (Y)", defaultCenter.y);
            DrawMainGameColliderCenterSlider(collider, "CenterZ", "Center (Z)", defaultCenter.z);
            DrawMainGameDivider();

            if (DrawMainGameFullWidthButton("Reset"))
            {
                InvokeMainGameMethod(controller, "Reset", mainGameColliderIndex);
            }
            if (DrawMainGameFullWidthButton("Reset All"))
            {
                InvokeMainGameMethod(controller, "ResetAll");
            }
            DrawMainGameDivider();

            bool showCollider = GetMainGameReflectionMember(controller, "showCollider") is bool show && show;
            bool newShowCollider = DrawMainGameCheckbox(showCollider, "Show collider");
            if (newShowCollider != showCollider)
            {
                SetMainGameReflectionMember(controller, "showCollider", newShowCollider);
                InvokeMainGameMethod(controller, "SetupDebugColliders");
            }
            DrawMainGameColliderColor();
        }

        private void DrawMainGameColliderCenterSlider(
            object collider,
            string memberName,
            string label,
            float revertValue)
        {
            float oldValue = GetMainGameFloat(collider, memberName);
            float minimum = Math.Min(-20f, Math.Min(oldValue, revertValue));
            float maximum = Math.Max(20f, Math.Max(oldValue, revertValue));
            float newValue = DrawMainGameRawSliderRow(
                label,
                oldValue,
                minimum,
                maximum,
                revertValue,
                false,
                true);
            SetMainGameFloatIfChanged(collider, memberName, oldValue, newValue);
        }

        private void DrawMainGameColliderColor()
        {
            Type debugType = FindLoadedType("CharaColliderEditor.DebugCollider.DebugColliders");
            object currentValue = GetMainGameStaticReflectionMember(debugType, "ColliderColor");
            Color color = currentValue is Color current ? current : Color.cyan;
            Rect row = GetMainGameRightRowRect(44f);
            float labelWidth = Mathf.Clamp(row.width * 0.38f, 110f, 170f);
            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            Rect swatchRect = new Rect(labelRect.xMax + 6f, row.y + 5f, row.width - labelWidth - 6f, 34f);
            DrawMainGameFittedLabel(labelRect, "Collider color", GUI.skin.label);
            if (GUI.Button(
                    swatchRect,
                    GetColorSwatchTexture("MainGameColliderColor", color),
                    colorSwatchButtonStyle ?? GUI.skin.button))
            {
                Studio.Studio studio = Studio.Studio.Instance;
                studio.colorPalette.Setup(
                    "Collider color",
                    color,
                    changed =>
                    {
                        SetMainGameStaticReflectionMember(debugType, "ColliderColor", changed);
                        Type pluginType = FindLoadedType(
                            "CharaColliderEditor.CharaColliderEditorPlugin");
                        object config = GetMainGameStaticReflectionMember(pluginType, "colliderColor");
                        SetMainGameReflectionMember(config, "Value", changed);
                    },
                    true);
                studio.colorPalette.visible = true;
            }
        }

        private static float BeginMainGameLargePreviewScrollbarThumb()
        {
            GUIStyle thumbStyle = GUI.skin?.verticalScrollbarThumb;
            if (thumbStyle == null)
            {
                return -1f;
            }

            float previousHeight = thumbStyle.fixedHeight;
            thumbStyle.fixedHeight = MainGameLargePreviewScrollbarMinimumThumbHeight;
            return previousHeight;
        }

        private static void EndMainGameLargePreviewScrollbarThumb(float previousHeight)
        {
            GUIStyle thumbStyle = GUI.skin?.verticalScrollbarThumb;
            if (thumbStyle != null && previousHeight >= 0f)
            {
                thumbStyle.fixedHeight = previousHeight;
            }
        }

        private void DrawMainGameCoordinateCardsPage(bool allowSaveAndDelete)
        {
            EnsureMainGameCoordinateCards();
            RebuildMainGameVisibleCoordinateCards();

            GUILayout.BeginHorizontal(GUILayout.Height(34f));
            GUILayout.Label(
                mainGameVisibleCoordinateCards.Count == mainGameCoordinateCards.Count
                    ? mainGameCoordinateCards.Count + " coordinates"
                    : mainGameVisibleCoordinateCards.Count + " / " + mainGameCoordinateCards.Count,
                GetMainGameAuxiliaryLabelStyle(),
                GUILayout.Height(32f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(
                    "Folders",
                    mainGameCoordinateFolderOpen
                        ? GetMainGameCoordinateHeaderSelectedStyle()
                        : GetMainGameCoordinateHeaderStyle(),
                    GUILayout.Width(72f),
                    GUILayout.Height(30f)))
            {
                mainGameCoordinateFolderOpen = !mainGameCoordinateFolderOpen;
            }
            if (GUILayout.Button(
                    "Newest",
                    mainGameCoordinateSortNewest
                        ? GetMainGameCoordinateHeaderSelectedStyle()
                        : GetMainGameCoordinateHeaderStyle(),
                    GUILayout.Width(72f),
                    GUILayout.Height(30f)))
            {
                mainGameCoordinateSortNewest = !mainGameCoordinateSortNewest;
                mainGameCoordinateFilterDirty = true;
                mainGameCoordinateScroll = Vector2.zero;
            }
            if (GUILayout.Button(
                    "Refresh",
                    GetMainGameCoordinateHeaderStyle(),
                    GUILayout.Width(72f),
                    GUILayout.Height(30f)))
            {
                mainGameCoordinateCardsNeedRefresh = true;
                mainGameSelectedCoordinateCard = -1;
                mainGameSelectedCoordinateCardPath = string.Empty;
                mainGameCoordinateDeleteConfirmation = -1;
                EnsureMainGameCoordinateCards();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.Height(34f));
            GUILayout.Label(
                "Search",
                GetMainGameAuxiliaryLabelStyle(),
                GUILayout.Width(62f),
                GUILayout.Height(32f));
            string nextSearch = GUILayout.TextField(
                mainGameCoordinateSearch ?? string.Empty,
                GetMainGameAuxiliaryValueStyle(),
                GUILayout.ExpandWidth(true),
                GUILayout.Height(30f));
            if (!string.Equals(nextSearch, mainGameCoordinateSearch, StringComparison.Ordinal))
            {
                mainGameCoordinateSearch = nextSearch;
                mainGameCoordinateFilterDirty = true;
                mainGameCoordinateScroll = Vector2.zero;
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && !string.IsNullOrEmpty(mainGameCoordinateSearch);
            if (GUILayout.Button(
                    "X",
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(30f),
                    GUILayout.Height(30f)))
            {
                mainGameCoordinateSearch = string.Empty;
                mainGameCoordinateFilterDirty = true;
                mainGameCoordinateScroll = Vector2.zero;
                GUI.FocusControl(string.Empty);
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();

            if (mainGameCoordinateFolderOpen)
            {
                DrawMainGameCoordinateFolderPanel();
            }

            RebuildMainGameVisibleCoordinateCards();

            if (!string.IsNullOrEmpty(mainGameCoordinateCardStatus))
            {
                GUILayout.Label(mainGameCoordinateCardStatus, GUILayout.MinHeight(28f));
            }

            bool validSelection = mainGameSelectedCoordinateCard >= 0 &&
                                  mainGameSelectedCoordinateCard < mainGameVisibleCoordinateCards.Count;

            mainGameCoordinateScroll.x = 0f;
            float previousThumbHeight = BeginMainGameLargePreviewScrollbarThumb();
            mainGameCoordinateScroll = GUILayout.BeginScrollView(
                mainGameCoordinateScroll,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.ExpandHeight(true));
            if (mainGameVisibleCoordinateCards.Count == 0)
            {
                GUILayout.Label(
                    mainGameCoordinateCards.Count == 0
                        ? "No coordinate cards were found in " + GetMainGameCoordinateDirectory() + "."
                        : "No coordinate cards match the selected folder and search.",
                    GUILayout.MinHeight(52f));
            }
            else
            {
                DrawMainGameCoordinateCardGrid();
            }
            GUILayout.EndScrollView();
            EndMainGameLargePreviewScrollbarThumb(previousThumbHeight);

            GUILayout.Space(6f);
            if (allowSaveAndDelete)
            {
                DrawMainGameCoordinateSaveFooter(validSelection);
            }
            else
            {
                DrawMainGameCoordinateLoadFooter(validSelection);
            }
        }

        private void DrawMainGameCoordinateLoadFooter(bool validSelection)
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && validSelection && ociTarget?.charInfo != null &&
                          mainGameCoordinateActionCoroutine == null;
            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            float buttonWidth = Math.Max(90f, (mainGameRightContentWidth - 12f) / 3f);
            MainGameCoordinateCard selected = validSelection
                ? mainGameVisibleCoordinateCards[mainGameSelectedCoordinateCard]
                : null;
            if (GUILayout.Button(
                    "Load Clothing",
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(34f)))
            {
                QueueMainGameCoordinateAction(() =>
                    LoadMainGameCoordinateCard(selected, false, false));
            }
            if (GUILayout.Button(
                    "Load Accessories",
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(34f)))
            {
                QueueMainGameCoordinateAction(() =>
                    LoadMainGameCoordinateCard(selected, true, false));
            }
            if (GUILayout.Button(
                    "Load All",
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(34f)))
            {
                QueueMainGameCoordinateAction(() =>
                    LoadMainGameCoordinateCard(selected, false, true));
            }
            GUILayout.EndHorizontal();
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameCoordinateSaveFooter(bool validSelection)
        {
            bool oldEnabled = GUI.enabled;
            float buttonWidth = Math.Max(90f, (mainGameRightContentWidth - 6f) / 2f);
            MainGameCoordinateCard selected = validSelection
                ? mainGameVisibleCoordinateCards[mainGameSelectedCoordinateCard]
                : null;
            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            GUI.enabled = oldEnabled && validSelection;
            string deleteLabel = mainGameCoordinateDeleteConfirmation == mainGameSelectedCoordinateCard &&
                                 validSelection
                ? "Confirm Delete"
                : "Delete";
            if (GUILayout.Button(
                    deleteLabel,
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(34f)))
            {
                if (mainGameCoordinateDeleteConfirmation == mainGameSelectedCoordinateCard)
                {
                    DeleteMainGameCoordinateCard(selected);
                }
                else
                {
                    mainGameCoordinateDeleteConfirmation = mainGameSelectedCoordinateCard;
                    mainGameCoordinateCardStatus = "Press Confirm Delete to remove " + selected.Name + ".";
                }
            }
            GUI.enabled = oldEnabled && ociTarget?.charInfo != null;
            if (GUILayout.Button(
                    "Save New",
                    GetMainGameAuxiliaryButtonStyle(),
                    GUILayout.Width(buttonWidth),
                    GUILayout.Height(34f)))
            {
                mainGameCoordinateDeleteConfirmation = -1;
                BeginMainGameCoordinateSave();
            }
            GUILayout.EndHorizontal();
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameCoordinateFolderPanel()
        {
            if (mainGameCoordinateFolderRoot == null)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Height(166f));
            GUILayout.Label(
                "Select clothes folder",
                GetMainGameAuxiliaryLabelStyle(),
                GUILayout.Height(24f));
            mainGameCoordinateFolderScroll = GUILayout.BeginScrollView(
                mainGameCoordinateFolderScroll,
                false,
                true,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.Height(132f));
            DrawMainGameCoordinateFolderNode(mainGameCoordinateFolderRoot, 0);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawMainGameCoordinateFolderNode(
            MainGameCoordinateFolderNode node,
            int depth)
        {
            if (node == null)
            {
                return;
            }

            bool hasChildren = node.Children.Count > 0;
            bool expanded = hasChildren &&
                            mainGameExpandedCoordinateFolders.Contains(node.Path);
            bool selected = string.Equals(
                mainGameSelectedCoordinateFolder,
                node.Path,
                StringComparison.OrdinalIgnoreCase);
            GUILayout.BeginHorizontal(GUILayout.Height(26f));
            GUILayout.Space(depth * 14f);
            if (hasChildren)
            {
                if (GUILayout.Button(
                        expanded ? "-" : "+",
                        GetMainGameAuxiliaryButtonStyle(),
                        GUILayout.Width(24f),
                        GUILayout.Height(24f)))
                {
                    if (expanded)
                    {
                        mainGameExpandedCoordinateFolders.Remove(node.Path);
                    }
                    else
                    {
                        mainGameExpandedCoordinateFolders.Add(node.Path);
                    }
                }
            }
            else
            {
                GUILayout.Space(24f);
            }

            if (GUILayout.Button(
                    node.Name,
                    GetMainGameCoordinateFolderStyle(selected),
                    GUILayout.Height(24f),
                    GUILayout.ExpandWidth(true)))
            {
                mainGameSelectedCoordinateFolder = node.Path;
                mainGameCoordinateFilterDirty = true;
                mainGameCoordinateScroll = Vector2.zero;
                mainGameCoordinateDeleteConfirmation = -1;
            }
            GUILayout.EndHorizontal();

            if (!expanded)
            {
                return;
            }
            for (int index = 0; index < node.Children.Count; index++)
            {
                DrawMainGameCoordinateFolderNode(node.Children[index], depth + 1);
            }
        }

        private GUIStyle GetMainGameCoordinateFolderStyle(bool selected)
        {
            if (mainGameCoordinateFolderStyle == null)
            {
                mainGameCoordinateFolderStyle = new GUIStyle(theme.MainGameListButtonStyle)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleLeft,
                    fixedHeight = 0f,
                    padding = new RectOffset(6, 2, 0, 2),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            if (mainGameCoordinateFolderSelectedStyle == null)
            {
                mainGameCoordinateFolderSelectedStyle =
                    new GUIStyle(theme.MainGameListSelectedStyle)
                    {
                        fontSize = 14,
                        alignment = TextAnchor.MiddleLeft,
                        fixedHeight = 0f,
                        padding = new RectOffset(6, 2, 0, 2),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
            }
            return selected
                ? mainGameCoordinateFolderSelectedStyle
                : mainGameCoordinateFolderStyle;
        }

        private GUIStyle GetMainGameCoordinateHeaderStyle()
        {
            if (mainGameCoordinateHeaderStyle == null)
            {
                mainGameCoordinateHeaderStyle =
                    new GUIStyle(GetMainGameAuxiliaryButtonStyle())
                    {
                        fontSize = 16,
                        alignment = TextAnchor.MiddleCenter,
                        fixedWidth = 0f,
                        fixedHeight = 0f,
                        stretchWidth = false,
                        stretchHeight = false,
                        padding = new RectOffset(2, 2, 0, 2),
                        margin = new RectOffset(0, 0, 2, 2),
                        contentOffset = new Vector2(0f, -2f)
                    };
            }
            return mainGameCoordinateHeaderStyle;
        }

        private GUIStyle GetMainGameCoordinateHeaderSelectedStyle()
        {
            if (mainGameCoordinateHeaderSelectedStyle == null)
            {
                mainGameCoordinateHeaderSelectedStyle =
                    new GUIStyle(theme.MainGameTabSelectedStyle)
                    {
                        fontSize = 16,
                        alignment = TextAnchor.MiddleCenter,
                        fixedWidth = 0f,
                        fixedHeight = 0f,
                        stretchWidth = false,
                        stretchHeight = false,
                        padding = new RectOffset(2, 2, 0, 2),
                        margin = new RectOffset(0, 0, 2, 2),
                        contentOffset = new Vector2(0f, -2f)
                    };
            }
            return mainGameCoordinateHeaderSelectedStyle;
        }

        private void RebuildMainGameVisibleCoordinateCards()
        {
            if (!mainGameCoordinateFilterDirty)
            {
                return;
            }

            mainGameCoordinateFilterDirty = false;
            mainGameVisibleCoordinateCards.Clear();
            mainGameCoordinateDeleteConfirmation = -1;
            string selectedFolder = string.IsNullOrEmpty(mainGameSelectedCoordinateFolder)
                ? GetMainGameCoordinateDirectory()
                : mainGameSelectedCoordinateFolder;
            string folderPrefix = Path.GetFullPath(selectedFolder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string search = (mainGameCoordinateSearch ?? string.Empty).Trim();

            for (int index = 0; index < mainGameCoordinateCards.Count; index++)
            {
                MainGameCoordinateCard card = mainGameCoordinateCards[index];
                string fullPath = Path.GetFullPath(card.Path);
                if (!fullPath.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!string.IsNullOrEmpty(search) &&
                    card.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    fullPath.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                mainGameVisibleCoordinateCards.Add(card);
            }

            mainGameVisibleCoordinateCards.Sort((left, right) =>
            {
                int comparison = mainGameCoordinateSortNewest
                    ? right.Modified.CompareTo(left.Modified)
                    : string.Compare(
                        left.Name,
                        right.Name,
                        StringComparison.CurrentCultureIgnoreCase);
                return comparison != 0
                    ? comparison
                    : string.Compare(
                        left.Path,
                        right.Path,
                        StringComparison.OrdinalIgnoreCase);
            });

            mainGameSelectedCoordinateCard = -1;
            if (!string.IsNullOrEmpty(mainGameSelectedCoordinateCardPath))
            {
                for (int index = 0; index < mainGameVisibleCoordinateCards.Count; index++)
                {
                    if (string.Equals(
                            mainGameVisibleCoordinateCards[index].Path,
                            mainGameSelectedCoordinateCardPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        mainGameSelectedCoordinateCard = index;
                        break;
                    }
                }
            }
        }

        private void DrawMainGameCoordinateCardGrid()
        {
            float availableWidth = Math.Max(220f, mainGameRightContentWidth - 22f);
            const float gap = 10f;
            const float preferredWidth = 174f;
            int columns = Mathf.Max(2, Mathf.FloorToInt((availableWidth + gap) / (preferredWidth + gap)));
            float cellWidth = Mathf.Floor((availableWidth - gap * (columns - 1)) / columns);
            float previewHeight = Mathf.Round(cellWidth * 1.4f);
            const float labelHeight = 72f;
            float cellHeight = previewHeight + labelHeight;
            GUIStyle labelStyle = GetMainGameCoordinateCardLabelStyle();
            float rowStride = cellHeight + gap;
            int rowCount = Mathf.CeilToInt(mainGameVisibleCoordinateCards.Count / (float)columns);
            float viewportHeight = Math.Max(120f, mainGameRightRect.height - 100f);
            int firstRow = Mathf.Clamp(
                Mathf.FloorToInt(mainGameCoordinateScroll.y / rowStride) - 2,
                0,
                rowCount);
            int lastRowExclusive = Mathf.Clamp(
                Mathf.CeilToInt(
                    (mainGameCoordinateScroll.y + viewportHeight) / rowStride) + 2,
                firstRow,
                rowCount);

            // GUILayout still needs the complete virtual height so Unity can
            // calculate the scrollbar, but only the visible rows receive GUI
            // controls. With ~2,000 cards this cuts per-frame controls from
            // thousands to roughly 8-16.
            if (firstRow > 0)
            {
                GUILayout.Space(firstRow * rowStride);
            }

            for (int row = firstRow; row < lastRowExclusive; row++)
            {
                int start = row * columns;
                GUILayout.BeginHorizontal(GUILayout.Height(cellHeight));
                for (int column = 0; column < columns; column++)
                {
                    int index = start + column;
                    if (index >= mainGameVisibleCoordinateCards.Count)
                    {
                        GUILayout.Space(cellWidth);
                        continue;
                    }

                    MainGameCoordinateCard card = mainGameVisibleCoordinateCards[index];
                    Rect cellRect = GUILayoutUtility.GetRect(
                        cellWidth,
                        cellHeight,
                        GUILayout.Width(cellWidth),
                        GUILayout.Height(cellHeight));
                    Rect previewRect = new Rect(
                        cellRect.x + 4f,
                        cellRect.y + 4f,
                        cellRect.width - 8f,
                        previewHeight - 8f);
                    Rect labelRect = new Rect(
                        cellRect.x,
                        cellRect.y + previewHeight,
                        cellRect.width,
                        labelHeight);

                    TryLoadMainGameCoordinatePreview(card);
                    if (Event.current.type == EventType.Repaint)
                    {
                        Color oldColor = GUI.color;
                        GUI.color = new Color32(231, 225, 216, 255);
                        GUI.DrawTexture(previewRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
                        GUI.color = oldColor;
                        if (card.Preview != null)
                        {
                            GUI.DrawTexture(previewRect, card.Preview, ScaleMode.ScaleAndCrop, true);
                        }
                        if (index == mainGameSelectedCoordinateCard)
                        {
                            DrawMainGameSelectorOutline(
                                new Rect(previewRect.x - 3f, previewRect.y - 3f, previewRect.width + 6f, previewRect.height + 6f),
                                new Color32(112, 205, 67, 255),
                                4f);
                        }
                    }
                    GUI.Label(labelRect, card.Name, labelStyle);
                    if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                    {
                        mainGameSelectedCoordinateCard = index;
                        mainGameSelectedCoordinateCardPath = card.Path;
                        mainGameCoordinateDeleteConfirmation = -1;
                        mainGameCoordinateCardStatus = string.Empty;
                    }

                    if (column < columns - 1)
                    {
                        GUILayout.Space(gap);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(gap);
            }

            int remainingRows = rowCount - lastRowExclusive;
            if (remainingRows > 0)
            {
                GUILayout.Space(remainingRows * rowStride);
            }

        }

        private GUIStyle GetMainGameCoordinateCardLabelStyle()
        {
            if (mainGameCoordinateCardLabelStyle == null)
            {
                GUIStyle baseLabelStyle = GetMainGameSelectorItemLabelStyle();
                mainGameCoordinateCardLabelStyle = new GUIStyle(baseLabelStyle)
                {
                    fontSize = Math.Max(14, baseLabelStyle.fontSize),
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(2, 2, 2, 0)
                };
            }
            return mainGameCoordinateCardLabelStyle;
        }

        private void EnsureMainGameCoordinateCards()
        {
            byte sex = ociTarget?.charInfo?.chaFile?.parameter == null
                ? byte.MaxValue
                : ociTarget.charInfo.chaFile.parameter.sex;
            if (!mainGameCoordinateCardsNeedRefresh && sex == mainGameCoordinateCardSex)
            {
                return;
            }

            bool sexChanged = sex != mainGameCoordinateCardSex;
            DisposeMainGameCoordinateCards();
            mainGameCoordinateCardSex = sex;
            mainGameCoordinateCardsNeedRefresh = false;
            mainGameCoordinateFilterDirty = true;
            mainGameSelectedCoordinateCard = -1;
            mainGameCoordinateDeleteConfirmation = -1;
            if (sexChanged)
            {
                mainGameSelectedCoordinateFolder = string.Empty;
                mainGameExpandedCoordinateFolders.Clear();
                mainGameCoordinateFolderScroll = Vector2.zero;
            }
            if (sex == byte.MaxValue)
            {
                mainGameCoordinateCardStatus = "Select a character first.";
                return;
            }

            string directory = GetMainGameCoordinateDirectory();
            if (!Directory.Exists(directory))
            {
                mainGameCoordinateCardStatus = string.Empty;
                return;
            }

            try
            {
                foreach (string path in Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories))
                {
                    mainGameCoordinateCards.Add(new MainGameCoordinateCard
                    {
                        Path = path,
                        // Keep enumeration side-effect free. Loading coordinate
                        // payloads here would trigger Sideloader for every card.
                        Name = Path.GetFileNameWithoutExtension(path),
                        Modified = File.GetLastWriteTime(path)
                    });
                }
                mainGameCoordinateCards.Sort((left, right) =>
                    string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));
                BuildMainGameCoordinateFolderTree(directory);
                StartMainGameCoordinateNameIndex(sex);
                mainGameCoordinateCardStatus = string.Empty;
            }
            catch (Exception ex)
            {
                mainGameCoordinateCardStatus = "Could not read coordinate cards.";
                StudioCharaEditor.Logger.LogWarning(
                    "Coordinate card list failed: " + GetMainGameInnermostExceptionMessage(ex));
            }
        }

        private void BuildMainGameCoordinateFolderTree(string directory)
        {
            string rootPath = Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            mainGameCoordinateFolderRoot = new MainGameCoordinateFolderNode
            {
                Name = Path.GetFileName(rootPath),
                Path = rootPath
            };
            Dictionary<string, MainGameCoordinateFolderNode> nodes =
                new Dictionary<string, MainGameCoordinateFolderNode>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    [rootPath] = mainGameCoordinateFolderRoot
                };

            for (int cardIndex = 0; cardIndex < mainGameCoordinateCards.Count; cardIndex++)
            {
                string cardDirectory = Path.GetFullPath(
                    Path.GetDirectoryName(mainGameCoordinateCards[cardIndex].Path) ?? rootPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!cardDirectory.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cardDirectory, rootPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = cardDirectory.Substring(rootPath.Length)
                    .Trim(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string currentPath = rootPath;
                MainGameCoordinateFolderNode parent = mainGameCoordinateFolderRoot;
                string[] segments = relative.Split(
                    new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
                for (int segmentIndex = 0; segmentIndex < segments.Length; segmentIndex++)
                {
                    currentPath = Path.Combine(currentPath, segments[segmentIndex]);
                    if (!nodes.TryGetValue(currentPath, out MainGameCoordinateFolderNode node))
                    {
                        node = new MainGameCoordinateFolderNode
                        {
                            Name = segments[segmentIndex],
                            Path = currentPath
                        };
                        nodes[currentPath] = node;
                        parent.Children.Add(node);
                    }
                    parent = node;
                }
            }

            SortMainGameCoordinateFolderTree(mainGameCoordinateFolderRoot);
            if (string.IsNullOrEmpty(mainGameSelectedCoordinateFolder) ||
                !nodes.ContainsKey(mainGameSelectedCoordinateFolder))
            {
                mainGameSelectedCoordinateFolder = rootPath;
            }
            mainGameExpandedCoordinateFolders.Add(rootPath);
        }

        private static void SortMainGameCoordinateFolderTree(
            MainGameCoordinateFolderNode node)
        {
            if (node == null)
            {
                return;
            }
            node.Children.Sort((left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.CurrentCultureIgnoreCase));
            for (int index = 0; index < node.Children.Count; index++)
            {
                SortMainGameCoordinateFolderTree(node.Children[index]);
            }
        }

        private void StartMainGameCoordinateNameIndex(byte sex)
        {
            if (mainGameCoordinateNameIndexCoroutine != null)
            {
                StopCoroutine(mainGameCoordinateNameIndexCoroutine);
                mainGameCoordinateNameIndexCoroutine = null;
            }

            string[] paths = new string[mainGameCoordinateCards.Count];
            for (int index = 0; index < mainGameCoordinateCards.Count; index++)
            {
                paths[index] = mainGameCoordinateCards[index].Path;
            }
            mainGameCoordinateNameIndexCoroutine = StartCoroutine(
                IndexMainGameCoordinateNames(sex, paths));
        }

        private IEnumerator IndexMainGameCoordinateNames(byte sex, string[] paths)
        {
            Task<Dictionary<string, string>> indexTask = Task.Run(() =>
            {
                Dictionary<string, string> names =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < paths.Length; index++)
                {
                    string path = paths[index];
                    string coordinateDisplayName =
                        TryReadMainGameCoordinateDisplayName(path);
                    if (!string.IsNullOrWhiteSpace(coordinateDisplayName))
                    {
                        names[path] = coordinateDisplayName;
                    }
                }
                return names;
            });

            while (!indexTask.IsCompleted)
            {
                yield return null;
            }

            Dictionary<string, string> indexedNames;
            try
            {
                indexedNames = indexTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Coordinate name index failed: " +
                    GetMainGameInnermostExceptionMessage(exception));
                mainGameCoordinateNameIndexCoroutine = null;
                yield break;
            }

            if (sex == mainGameCoordinateCardSex)
            {
                for (int index = 0; index < mainGameCoordinateCards.Count; index++)
                {
                    MainGameCoordinateCard card = mainGameCoordinateCards[index];
                    if (card != null &&
                        indexedNames.TryGetValue(card.Path, out string displayName))
                    {
                        card.Name = displayName;
                    }
                }
                mainGameCoordinateFilterDirty = true;
            }
            mainGameCoordinateNameIndexCoroutine = null;
        }

        private static string TryReadMainGameCoordinateDisplayName(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(
                           path,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    PngFile.SkipPng(reader);
                    if (stream.Position >= stream.Length)
                    {
                        return null;
                    }

                    int productNumber = reader.ReadInt32();
                    string marker = reader.ReadString();
                    if (productNumber > 100 ||
                        !string.Equals(
                            marker,
                            "【AIS_Clothes】",
                            StringComparison.Ordinal))
                    {
                        return null;
                    }

                    // Version and language precede the user-facing coordinate
                    // name. No MessagePack payload is read or deserialized.
                    reader.ReadString();
                    reader.ReadInt32();
                    string coordinateDisplayName = reader.ReadString();
                    return string.IsNullOrWhiteSpace(coordinateDisplayName)
                        ? null
                        : coordinateDisplayName.Trim();
                }
            }
            catch
            {
                return null;
            }
        }

        private void TryLoadMainGameCoordinatePreview(MainGameCoordinateCard card)
        {
            if (card == null || card.Preview != null || card.PreviewLoadAttempted ||
                Event.current.type != EventType.Repaint ||
                !CanLoadSelectorThumbnailThisFrame())
            {
                return;
            }

            card.PreviewLoadAttempted = true;
            // Keep the newest visible rows at the front. During a fast scroll,
            // stale rows must not delay the cards currently on screen.
            mainGameCoordinatePreviewQueue.AddFirst(card);
            const int maximumQueuedPreviews = 24;
            while (mainGameCoordinatePreviewQueue.Count > maximumQueuedPreviews)
            {
                LinkedListNode<MainGameCoordinateCard> oldest =
                    mainGameCoordinatePreviewQueue.Last;
                mainGameCoordinatePreviewQueue.RemoveLast();
                if (oldest?.Value != null && oldest.Value.Preview == null)
                {
                    oldest.Value.PreviewLoadAttempted = false;
                }
            }
            if (mainGameCoordinatePreviewCoroutine == null)
            {
                mainGameCoordinatePreviewCoroutine =
                    StartCoroutine(ProcessMainGameCoordinatePreviewQueue());
            }
        }

        private IEnumerator ProcessMainGameCoordinatePreviewQueue()
        {
            while (mainGameCoordinatePreviewQueue.Count > 0)
            {
                LinkedListNode<MainGameCoordinateCard> next =
                    mainGameCoordinatePreviewQueue.First;
                mainGameCoordinatePreviewQueue.RemoveFirst();
                MainGameCoordinateCard card = next.Value;
                if (card == null || card.Preview != null)
                {
                    continue;
                }

                // Coordinate files are valid PNGs with card data appended.
                // Read bytes off the Unity thread, matching the cached
                // clothing-preview pipeline. Only texture creation remains
                // on the main thread.
                Task<byte[]> readTask = Task.Run(() => File.ReadAllBytes(card.Path));
                while (!readTask.IsCompleted)
                {
                    yield return null;
                }

                try
                {
                    // Coordinate files are valid PNGs with card data appended.
                    // Decode only the PNG image here. LoadFile triggers Sideloader,
                    // missing-zipmod warnings and coordinate plug-in callbacks even
                    // though the user is merely scrolling through previews.
                    byte[] data = readTask.GetAwaiter().GetResult();
                    Texture2D preview = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (ImageConversion.LoadImage(preview, data))
                    {
                        preview.name = "StudioCharaEditor Coordinate " + card.Name;
                        card.Preview = preview;
                        RememberMainGameCoordinatePreview(card);
                    }
                    else
                    {
                        Destroy(preview);
                    }
                }
                catch (Exception ex)
                {
                    StudioCharaEditor.Logger.LogWarning(
                        "Coordinate preview failed for " + card.Path + ": " +
                        GetMainGameInnermostExceptionMessage(ex));
                }

                // Decode at most one texture per frame so continuous loading
                // does not monopolise Studio while the user is scrolling.
                yield return null;
            }

            mainGameCoordinatePreviewCoroutine = null;
        }

        private void RememberMainGameCoordinatePreview(MainGameCoordinateCard loadedCard)
        {
            const int maximumPreviews = 128;
            mainGameCoordinatePreviewLoadOrder.Enqueue(loadedCard);
            while (mainGameCoordinatePreviewLoadOrder.Count > maximumPreviews)
            {
                MainGameCoordinateCard oldest = mainGameCoordinatePreviewLoadOrder.Dequeue();
                if (oldest == null || oldest.Preview == null)
                {
                    continue;
                }

                Destroy(oldest.Preview);
                oldest.Preview = null;
                oldest.PreviewLoadAttempted = false;
            }
        }

        private void QueueMainGameCoordinateAction(Action action)
        {
            if (action == null || mainGameCoordinateActionCoroutine != null)
            {
                return;
            }

            mainGameCoordinateActionCoroutine =
                StartCoroutine(RunMainGameCoordinateAction(action));
        }

        private IEnumerator RunMainGameCoordinateAction(Action action)
        {
            // Applying a coordinate invokes game and plug-in callbacks. It must
            // begin after the IMGUI event has completely finished.
            yield return null;
            try
            {
                action();
            }
            finally
            {
                mainGameCoordinateActionCoroutine = null;
            }
        }

        private void LoadMainGameCoordinateCard(
            MainGameCoordinateCard card,
            bool accessoriesOnly,
            bool loadAll)
        {
            ChaControl character = ociTarget?.charInfo;
            if (card == null || character == null)
            {
                return;
            }
            try
            {
                if (loadAll)
                {
                    ociTarget.LoadClothesFile(card.Path);
                    mainGameCoordinateCardStatus = "Loaded all: " + card.Name;
                    return;
                }

                ChaFileCoordinate source = new ChaFileCoordinate();
                if (!source.LoadFile(card.Path))
                {
                    throw new InvalidOperationException("The coordinate file could not be read.");
                }
                if (accessoriesOnly)
                {
                    character.nowCoordinate.accessory = source.accessory;
                    character.ChangeAccessory(true);
                    mainGameCoordinateCardStatus = "Loaded accessories: " + card.Name;
                }
                else
                {
                    character.nowCoordinate.clothes = source.clothes;
                    character.ChangeClothes(true);
                    mainGameCoordinateCardStatus = "Loaded clothing: " + card.Name;
                }
                character.AssignCoordinate();
            }
            catch (Exception ex)
            {
                mainGameCoordinateCardStatus = "Could not load " + card.Name + ".";
                StudioCharaEditor.Logger.LogWarning(
                    "Coordinate load failed for " + card.Path + ": " +
                    GetMainGameInnermostExceptionMessage(ex));
            }
        }

        private void DeleteMainGameCoordinateCard(MainGameCoordinateCard card)
        {
            if (card == null)
            {
                return;
            }
            try
            {
                string directory = Path.GetFullPath(GetMainGameCoordinateDirectory())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                string target = Path.GetFullPath(card.Path);
                if (!target.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The selected card is outside the coordinate folder.");
                }
                File.Delete(target);
                string deletedName = card.Name;
                mainGameCoordinateCardsNeedRefresh = true;
                EnsureMainGameCoordinateCards();
                mainGameCoordinateCardStatus = "Deleted: " + deletedName;
            }
            catch (Exception ex)
            {
                mainGameCoordinateDeleteConfirmation = -1;
                mainGameCoordinateCardStatus = "Could not delete " + card.Name + ".";
                StudioCharaEditor.Logger.LogWarning(
                    "Coordinate delete failed for " + card.Path + ": " +
                    GetMainGameInnermostExceptionMessage(ex));
            }
        }

        private void BeginMainGameCoordinateSave()
        {
            BeginMainGameSave();
            ChaFile file = savingChara?.charInfo?.chaFile;
            if (file == null)
            {
                return;
            }
            savingCoordinate = true;
            savingPath = GetMainGameCoordinateDirectory();
            coordinateName = string.Format(
                CultureInfo.InvariantCulture,
                "{0}_coordinate_{1:yyyy-MM-dd-HH-mm-ss}",
                file.parameter.fullname,
                DateTime.Now);
            if (file.coordinate.pngData != null && file.coordinate.pngData.Length > 0)
            {
                Texture2D preview = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (ImageConversion.LoadImage(preview, file.coordinate.pngData))
                {
                    SetSavingTexture(preview);
                }
                else
                {
                    Destroy(preview);
                    SetSavingTexture(null);
                }
            }
            else
            {
                SetSavingTexture(null);
            }
        }

        private string GetMainGameCoordinateDirectory()
        {
            byte sex = ociTarget?.charInfo?.chaFile?.parameter?.sex ?? mainGameCoordinateCardSex;
            return Path.Combine(
                BepInEx.Paths.GameRootPath,
                "UserData",
                "coordinate",
                sex == 0 ? "male" : "female");
        }

        private void DisposeMainGameCoordinateCards()
        {
            if (mainGameCoordinateNameIndexCoroutine != null)
            {
                StopCoroutine(mainGameCoordinateNameIndexCoroutine);
                mainGameCoordinateNameIndexCoroutine = null;
            }
            if (mainGameCoordinatePreviewCoroutine != null)
            {
                StopCoroutine(mainGameCoordinatePreviewCoroutine);
                mainGameCoordinatePreviewCoroutine = null;
            }
            mainGameCoordinatePreviewQueue.Clear();
            mainGameCoordinatePreviewLoadOrder.Clear();

            foreach (MainGameCoordinateCard card in mainGameCoordinateCards)
            {
                if (card?.Preview != null)
                {
                    Destroy(card.Preview);
                    card.Preview = null;
                }
            }
            mainGameCoordinateCards.Clear();
            mainGameVisibleCoordinateCards.Clear();
            mainGameCoordinateFolderRoot = null;
        }

        private void StopMainGameCoordinateAction()
        {
            if (mainGameCoordinateActionCoroutine == null)
            {
                return;
            }

            StopCoroutine(mainGameCoordinateActionCoroutine);
            mainGameCoordinateActionCoroutine = null;
        }

        private void DrawMainGameHairShaderSwapperPage()
        {
            Type studioType = FindLoadedType("HS2_HairShaderSwapper.HairShaderSwapperStudio");
            if (!RefreshMainGameHairShaderControls(studioType))
            {
                GUILayout.Label(string.IsNullOrEmpty(mainGameHairShaderStatus)
                    ? "Hair Shader Swapper controls are not available."
                    : mainGameHairShaderStatus);
                return;
            }
            object dropdown = GetMainGameStaticReflectionMember(studioType, "shaderDropdown");
            IList options = GetMainGameReflectionMember(dropdown, "options") as IList;
            object onValueChanged = GetMainGameReflectionMember(dropdown, "onValueChanged");
            if (options == null || onValueChanged == null)
            {
                GUILayout.Label("Hair Shader Swapper controls are not available.");
                return;
            }
            for (int index = 0; index < options.Count; index++)
            {
                string label = index == 0
                    ? "Reset All"
                    : GetMainGameReflectionString(options[index], "text") ?? ("Shader " + index);
                if (DrawMainGameFullWidthButton(label))
                {
                    TryInvokeMainGameHairShader(
                        () => InvokeMainGameMethod(onValueChanged, "Invoke", index),
                        "change the hair shader");
                }
                GUILayout.Space(5f);
            }
        }

        private void DrawMainGameHairShaderPropertiesPage()
        {
            Type studioType = FindLoadedType("HS2_HairShaderSwapper.HairShaderSwapperStudio");
            Type propertiesType = FindLoadedType("HS2_HairShaderSwapper.HairShaderProperties");
            if (!RefreshMainGameHairShaderControls(studioType))
            {
                GUILayout.Label(string.IsNullOrEmpty(mainGameHairShaderStatus)
                    ? "Hair Shader Properties controls are not available."
                    : mainGameHairShaderStatus);
                return;
            }
            IDictionary sliders = GetMainGameStaticReflectionMember(studioType, "Sliders") as IDictionary;
            IDictionary colors = GetMainGameStaticReflectionMember(studioType, "Colors") as IDictionary;
            IEnumerable properties = GetMainGameStaticReflectionMember(
                propertiesType,
                "ShaderPropertyList") as IEnumerable;
            if (properties == null || sliders == null || colors == null)
            {
                GUILayout.Label("Hair Shader Properties controls are not available.");
                return;
            }

            if (DrawMainGameFullWidthButton("Sync All Hairs"))
            {
                TryInvokeMainGameHairShader(
                    () => InvokeMainGameStaticMethod(studioType, "SyncAllHairs"),
                    "sync hair materials");
            }
            DrawMainGameDivider();
            foreach (object property in properties)
            {
                string propertyName = GetMainGameReflectionString(property, "PropertyName");
                string propertyType = GetMainGameReflectionString(property, "PropertyType");
                if (string.IsNullOrEmpty(propertyName))
                {
                    continue;
                }
                if (string.Equals(propertyType, "Float", StringComparison.OrdinalIgnoreCase) &&
                    sliders.Contains(propertyName))
                {
                    object slider = sliders[propertyName];
                    if (!IsMainGameHairShaderControlActive(slider))
                    {
                        continue;
                    }
                    float oldValue = GetMainGameFloat(slider, "value");
                    float minimum = GetMainGameFloat(slider, "minValue");
                    float maximum = GetMainGameFloat(slider, "maxValue");
                    float defaultValue = GetMainGameFloat(slider, "defaultValue");
                    float newValue = DrawMainGameRawSliderRow(
                        propertyName,
                        oldValue,
                        minimum,
                        maximum,
                        defaultValue,
                        false,
                        false);
                    TryInvokeMainGameHairShader(
                        () => SetMainGameFloatIfChanged(slider, "value", oldValue, newValue),
                        "change a hair shader property");
                }
                else if (string.Equals(propertyType, "Color", StringComparison.OrdinalIgnoreCase) &&
                         colors.Contains(propertyName))
                {
                    object colorControl = colors[propertyName];
                    if (!IsMainGameHairShaderControlActive(colorControl))
                    {
                        continue;
                    }
                    DrawMainGameHairShaderColor(propertyName, colorControl);
                }
            }
        }

        private bool RefreshMainGameHairShaderControls(Type studioType)
        {
            int characterId = ociTarget?.charInfo == null
                ? 0
                : ociTarget.charInfo.GetInstanceID();
            if (mainGameHairShaderCharacterId != characterId)
            {
                mainGameHairShaderCharacterId = characterId;
                mainGameHairShaderControlsNeedRefresh = true;
            }
            if (!mainGameHairShaderControlsNeedRefresh)
            {
                return GetMainGameStaticReflectionMember(studioType, "shaderDropdown") != null;
            }

            // UpdateControl assumes the plug-in's own Studio panel has already
            // run CreateControl. Calling it before shaderDropdown exists throws
            // inside OnGUI and corrupts Unity's GUIClip stack.
            if (studioType == null)
            {
                mainGameHairShaderStatus = "Hair Shader Swapper is not installed.";
                return false;
            }

            if (GetMainGameStaticReflectionMember(studioType, "shaderDropdown") == null &&
                !mainGameHairShaderInitializationAttempted)
            {
                mainGameHairShaderInitializationAttempted = true;
                try
                {
                    // A hot-reloaded Hair Shader Swapper can miss its normal
                    // Studio scene callback. Build its backing controls once;
                    // our IMGUI uses their events without opening its panel.
                    InvokeMainGameStaticMethod(studioType, "CreateControl");
                    if (GetMainGameStaticReflectionMember(studioType, "controlPanel") is GameObject panel)
                    {
                        panel.SetActive(false);
                    }
                }
                catch (Exception ex)
                {
                    StudioCharaEditor.Logger.LogWarning(
                        "Hair Shader backing-control initialization failed: " +
                        GetMainGameInnermostExceptionMessage(ex));
                }
            }

            if (GetMainGameStaticReflectionMember(studioType, "shaderDropdown") == null)
            {
                mainGameHairShaderStatus =
                    "Hair Shader controls could not be initialized. Reopen this page after Studio finishes loading.";
                return false;
            }

            try
            {
                InvokeMainGameStaticMethod(studioType, "UpdateControl");
                mainGameHairShaderControlsNeedRefresh = false;
                mainGameHairShaderStatus = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                mainGameHairShaderControlsNeedRefresh = false;
                mainGameHairShaderStatus = "Hair Shader controls could not be refreshed for this character.";
                StudioCharaEditor.Logger.LogWarning(
                    "Hair Shader UI refresh failed: " + GetMainGameInnermostExceptionMessage(ex));
                return false;
            }
        }

        private void TryInvokeMainGameHairShader(Action action, string operation)
        {
            try
            {
                action?.Invoke();
                mainGameHairShaderStatus = string.Empty;
            }
            catch (Exception ex)
            {
                mainGameHairShaderStatus = "Could not " + operation + ".";
                StudioCharaEditor.Logger.LogWarning(
                    "Hair Shader UI could not " + operation + ": " +
                    GetMainGameInnermostExceptionMessage(ex));
            }
        }

        private static string GetMainGameInnermostExceptionMessage(Exception exception)
        {
            Exception current = exception;
            while (current?.InnerException != null)
            {
                current = current.InnerException;
            }
            return current?.Message ?? "Unknown error";
        }

        private static bool IsMainGameHairShaderControlActive(object control)
        {
            Component component = GetMainGameReflectionMember(control, "control") as Component;
            return component == null || component.gameObject.activeSelf;
        }

        private void DrawMainGameHairShaderColor(string propertyName, object colorControl)
        {
            object preview = GetMainGameReflectionMember(colorControl, "colorPreview");
            object colorValue = GetMainGameReflectionMember(preview, "color");
            Color color = colorValue is Color current ? current : Color.white;
            Rect row = GetMainGameRightRowRect(44f);
            float labelWidth = Mathf.Clamp(row.width * 0.38f, 120f, 190f);
            Rect labelRect = new Rect(row.x, row.y, labelWidth, row.height);
            Rect swatchRect = new Rect(labelRect.xMax + 6f, row.y + 5f, row.width - labelWidth - 6f, 34f);
            DrawMainGameFittedLabel(labelRect, propertyName, GUI.skin.label);
            if (GUI.Button(
                    swatchRect,
                    GetColorSwatchTexture("HairShader|" + propertyName, color),
                    colorSwatchButtonStyle ?? GUI.skin.button))
            {
                Studio.Studio studio = Studio.Studio.Instance;
                studio.colorPalette.Setup(
                    propertyName,
                    color,
                    changed =>
                    {
                        if (GetMainGameReflectionMember(colorControl, "onValueChanged") is Action<Color> callback)
                        {
                            callback(changed);
                        }
                        SetMainGameReflectionMember(preview, "color", changed);
                    },
                    true);
                studio.colorPalette.visible = true;
            }
        }

        private void DrawMainGameMaterialEditorSlotPage(bool hair)
        {
            string[] labels = hair
                ? new[] { "Back Hair", "Bangs", "Side Hair", "Hair Extensions" }
                : new[] { "Top", "Bottom", "Bra", "Underwear", "Gloves", "Pantyhose", "Socks", "Shoes" };
            ChaControl character = ociTarget?.charInfo;
            for (int index = 0; index < labels.Length; index++)
            {
                int slot = index;
                bool available = GetMainGameMaterialEditorTarget(character, hair, slot) != null;
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && available;
                if (DrawMainGameFullWidthButton("Material Editor (" + labels[index] + ")"))
                {
                    OpenMainGameMaterialEditor(
                        character,
                        hair ? "Hair" : "Clothing",
                        slot,
                        null);
                }
                GUI.enabled = oldEnabled;
                GUILayout.Space(5f);
            }
        }

        private void DrawMainGameClothesOverlaysPage()
        {
            CharaEditorController controller = ociTarget?.charInfo == null
                ? null
                : CharaEditorMgr.Instance.GetEditorController(ociTarget.charInfo);
            if (controller == null || !controller.HasOverlayPlugin)
            {
                GUILayout.Label("Clothes Overlay is not available for the selected character.");
                return;
            }

            string[] clothes = controller.GetCategoryList(CharaEditorController.CT1_CTHS);
            bool drewAny = false;
            for (int clothIndex = 0; clothIndex < clothes.Length; clothIndex++)
            {
                string clothName = clothes[clothIndex];
                CharaDetailInfo[] details = controller.GetDetailInfoList(
                    CharaEditorController.CT1_CTHS,
                    clothName);
                bool drewCloth = false;
                for (int detailIndex = 0; detailIndex < details.Length; detailIndex++)
                {
                    CharaDetailInfo detail = details[detailIndex];
                    bool isOverlay = detail.DetailDefine.Type ==
                                     CharaDetailDefine.CharaDetailDefineType.CLOTH_OVERLAY;
                    bool isOverlayToggle = detail.DetailDefine.Type ==
                                           CharaDetailDefine.CharaDetailDefineType.TOGGLE &&
                                           detail.DetailDefine.Key.EndsWith(
                                               "#Overlay hide base textrue",
                                               StringComparison.Ordinal);
                    if (!isOverlay && !isOverlayToggle)
                    {
                        continue;
                    }
                    if (!drewCloth)
                    {
                        if (drewAny)
                        {
                            DrawMainGameDivider();
                        }
                        GUILayout.Label(
                            GetMainGamePageName(
                                CharaEditorController.CT1_CTHS,
                                controller.GetClothDispName(clothName)),
                            theme.MainGameBreadcrumbStyle);
                        drewCloth = true;
                        drewAny = true;
                    }
                    DrawMainGameDetailItem(
                        detail,
                        CharaEditorController.CT1_CTHS + "#" + clothName);
                    GUILayout.Space(4f);
                }
            }
            if (!drewAny)
            {
                GUILayout.Label("No clothes overlay layers are available.");
            }
        }

        private void DrawMainGameStudioCategoryPage(string categoryName)
        {
            object category = FindMainGameStudioCategory(categoryName);
            if (category == null)
            {
                GUILayout.Label(categoryName + " is not available.");
                return;
            }

            MethodInfo updateInfo = category.GetType().GetMethod(
                "UpdateInfo",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            updateInfo?.Invoke(category, new object[] { ociTarget });
            object subItemsValue = category.GetType().GetProperty(
                "SubItems",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(category, null);
            IEnumerable subItems = subItemsValue as IEnumerable;
            if (subItems == null)
            {
                GUILayout.Label("No controls are registered for " + categoryName + ".");
                return;
            }

            foreach (object item in subItems)
            {
                DrawMainGameStudioCategoryItem(item);
                GUILayout.Space(4f);
            }
        }

        private static object FindMainGameStudioCategory(string categoryName)
        {
            Type studioApi = FindLoadedType("KKAPI.Studio.StudioAPI");
            FieldInfo categoriesField = studioApi?.GetField(
                "_customCurrentStateCategories",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            IEnumerable categories = categoriesField?.GetValue(null) as IEnumerable;
            if (categories == null)
            {
                return null;
            }
            foreach (object category in categories)
            {
                string name = category?.GetType().GetProperty(
                    "CategoryName",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(category, null) as string;
                if (string.Equals(name, categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
            }
            return null;
        }

        private void DrawMainGameStudioCategoryItem(object item)
        {
            if (item == null)
            {
                return;
            }
            Type itemType = item.GetType();
            object visibilitySubject = itemType.GetProperty(
                "Visible",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item, null);
            object visibleValue = visibilitySubject?.GetType().GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(visibilitySubject, null);
            if (visibleValue is bool visible && !visible)
            {
                return;
            }
            string name = itemType.GetProperty(
                "Name",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item, null) as string ?? itemType.Name;
            object subject = itemType.GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item, null);
            if (subject == null)
            {
                GUILayout.Label(name);
                return;
            }
            object currentValue = subject.GetType().GetProperty(
                "Value",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(subject, null);
            string typeName = itemType.FullName ?? itemType.Name;

            if (typeName.IndexOf("CurrentStateCategorySlider", StringComparison.Ordinal) >= 0)
            {
                float current = Convert.ToSingle(currentValue, CultureInfo.InvariantCulture);
                float minimum = Convert.ToSingle(itemType.GetProperty("MinValue")?.GetValue(item, null), CultureInfo.InvariantCulture);
                float maximum = Convert.ToSingle(itemType.GetProperty("MaxValue")?.GetValue(item, null), CultureInfo.InvariantCulture);
                float changed = DrawMainGameRawSliderRow(
                    name.Trim(),
                    current,
                    minimum,
                    maximum,
                    null,
                    false,
                    false);
                if (!Mathf.Approximately(current, changed))
                {
                    InvokeMainGameSubject(subject, changed);
                }
                return;
            }

            if (typeName.IndexOf("CurrentStateCategoryDropdown", StringComparison.Ordinal) >= 0)
            {
                string[] choices = itemType.GetField(
                    "_items",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(item) as string[];
                int current = currentValue == null ? 0 : Convert.ToInt32(currentValue, CultureInfo.InvariantCulture);
                if (choices != null && choices.Length > 0)
                {
                    int changed = DrawMainGameStudioDropdown(
                        name.Trim(),
                        choices,
                        Mathf.Clamp(current, 0, choices.Length - 1));
                    if (changed != current)
                    {
                        InvokeMainGameSubject(subject, changed);
                    }
                }
                return;
            }

            if (typeName.IndexOf("CurrentStateCategoryToggle", StringComparison.Ordinal) >= 0)
            {
                int count = Convert.ToInt32(itemType.GetProperty("ToggleCount")?.GetValue(item, null) ?? 0);
                int current = currentValue == null ? 0 : Convert.ToInt32(currentValue, CultureInfo.InvariantCulture);
                string[] choices = new string[Math.Max(1, count)];
                for (int index = 0; index < choices.Length; index++)
                {
                    choices[index] = (index + 1).ToString(CultureInfo.InvariantCulture);
                }
                int changed = DrawMainGameChoiceRow(name.Trim(), choices, Mathf.Clamp(current, 0, choices.Length - 1));
                if (changed != current)
                {
                    InvokeMainGameSubject(subject, changed);
                }
                return;
            }

            if (currentValue is bool currentBool)
            {
                bool action = name.StartsWith("Copy ", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("Paste ", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("Reset ", StringComparison.OrdinalIgnoreCase) ||
                              name.StartsWith("Open ", StringComparison.OrdinalIgnoreCase);
                if (action)
                {
                    if (DrawMainGameFullWidthButton(name.Trim()))
                    {
                        InvokeMainGameSubject(subject, true);
                        InvokeMainGameSubject(subject, false);
                    }
                }
                else
                {
                    bool changed = DrawMainGameCheckbox(currentBool, name.Trim());
                    if (changed != currentBool)
                    {
                        InvokeMainGameSubject(subject, changed);
                    }
                }
                return;
            }

            GUILayout.Label(name.Trim());
        }

        private int DrawMainGameStudioDropdown(
            string name,
            string[] choices,
            int selectedIndex)
        {
            GUILayout.Label(name, mainGameSliderLabelStyle ?? GUI.skin.label);
            string key = mainGameStudioCategoryName + "|" + name;
            string selected = selectedIndex >= 0 && selectedIndex < choices.Length
                ? choices[selectedIndex]
                : "None";
            if (DrawMainGameFullWidthButton(selected + "  ▼"))
            {
                mainGameOpenStudioDropdownName =
                    mainGameOpenStudioDropdownName == key ? string.Empty : key;
            }
            if (mainGameOpenStudioDropdownName != key)
            {
                return selectedIndex;
            }
            for (int index = 0; index < choices.Length; index++)
            {
                if (DrawMainGameFullWidthButton(choices[index]))
                {
                    selectedIndex = index;
                    mainGameOpenStudioDropdownName = string.Empty;
                    break;
                }
            }
            return selectedIndex;
        }

        private static void InvokeMainGameSubject(object subject, object value)
        {
            MethodInfo onNext = subject.GetType().GetMethod(
                "OnNext",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            onNext?.Invoke(subject, new[] { value });
        }

        private void DrawMainGameClothesChannelControls(
            ChaControl character,
            string detailSetKey,
            CharaDetailInfo[] details,
            int colorIndex)
        {
            if (character == null || details == null || colorIndex < 0 || colorIndex > 2)
            {
                return;
            }
            string clothName = detailSetKey.Substring("Clothes#".Length);
            int clothIndex = Array.IndexOf(CharaEditorController.FEMALE_CLOTHES_NAME, clothName);
            if (clothIndex < 0)
            {
                return;
            }
            int assignmentIndex = clothIndex * 3 + colorIndex;
            int channel = mainGameClothesChannelAssignments[assignmentIndex];
            Rect rowRect = GetMainGameRightRowRect(40f);
            float labelWidth = Mathf.Clamp(rowRect.width * 0.42f, 130f, 210f);
            GUI.Label(
                new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height),
                "Color " + (colorIndex + 1) + " Channel:");
            Rect dropdownRect = new Rect(
                rowRect.x + labelWidth + 6f,
                rowRect.y,
                Math.Max(70f, rowRect.width - labelWidth - 6f),
                rowRect.height);
            if (GUI.Button(dropdownRect, channel == 0 ? "None  ▼" : "Channel " + channel + "  ▼"))
            {
                mainGameOpenClothesChannelAssignment =
                    mainGameOpenClothesChannelAssignment == assignmentIndex
                        ? -1
                        : assignmentIndex;
            }
            if (mainGameOpenClothesChannelAssignment == assignmentIndex)
            {
                for (int option = 0; option <= 8; option++)
                {
                    string label = option == 0 ? "None" : "Channel " + option;
                    if (DrawMainGameFullWidthButton(label))
                    {
                        mainGameClothesChannelAssignments[assignmentIndex] = option;
                        channel = option;
                        mainGameOpenClothesChannelAssignment = -1;
                    }
                }
            }

            GUILayout.BeginHorizontal();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && channel > 0 && mainGameClothesChannels.ContainsKey(channel);
            if (GUILayout.Button("Set From Channel", GUILayout.Height(36f)))
            {
                ApplyMainGameClothesChannel(character, details, colorIndex, channel);
            }
            GUI.enabled = oldEnabled && channel > 0;
            if (GUILayout.Button("Sync To Channel", GUILayout.Height(36f)))
            {
                CaptureMainGameClothesChannel(character, details, colorIndex, channel);
            }
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
        }

        private void CaptureMainGameClothesChannel(
            ChaControl character,
            CharaDetailInfo[] details,
            int colorIndex,
            int channel)
        {
            if (channel <= 0)
            {
                return;
            }
            Dictionary<string, object> values = new Dictionary<string, object>();
            for (int index = 0; index < details.Length; index++)
            {
                string normalized = GetMainGameClothesColorPropertyName(
                    details[index],
                    colorIndex);
                if (normalized != null && details[index].DetailDefine.Get != null)
                {
                    values[normalized] = details[index].DetailDefine.Get(character);
                }
            }
            mainGameClothesChannels[channel] = values;
        }

        private void ApplyMainGameClothesChannel(
            ChaControl character,
            CharaDetailInfo[] details,
            int colorIndex,
            int channel)
        {
            if (channel <= 0 || !mainGameClothesChannels.TryGetValue(
                    channel,
                    out Dictionary<string, object> values))
            {
                return;
            }
            for (int index = 0; index < details.Length; index++)
            {
                CharaDetailInfo detail = details[index];
                string normalized = GetMainGameClothesColorPropertyName(detail, colorIndex);
                if (normalized == null || detail.DetailDefine.Set == null ||
                    !values.TryGetValue(normalized, out object value))
                {
                    continue;
                }
                detail.DetailDefine.Set(character, value);
                if (detail.DetailDefine.Upd != null && !LaterUpdate)
                {
                    detail.DetailDefine.Upd(character);
                }
            }
        }

        private static string GetMainGameClothesColorPropertyName(
            CharaDetailInfo detail,
            int colorIndex)
        {
            if (detail?.DetailDefine == null ||
                detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.SEPERATOR ||
                detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.BUTTON)
            {
                return null;
            }
            string name = GetDetailName(detail.DetailDefine.Key);
            string number = (colorIndex + 1).ToString(CultureInfo.InvariantCulture);
            if (name == "Color " + number || name == "Gloss " + number ||
                name == "Metallic " + number || name == "Pattern " + number)
            {
                return name.Replace(" " + number, string.Empty);
            }
            string prefix = "Pattern " + number + " ";
            return name.StartsWith(prefix, StringComparison.Ordinal)
                ? "Pattern " + name.Substring(prefix.Length)
                : null;
        }

        private void SetMainGameAllClothesColors(
            ChaControl character,
            CharaDetailInfo[] details,
            int sourceColorIndex)
        {
            if (character == null || details == null)
            {
                return;
            }
            CharaDetailInfo source = null;
            string sourceName = "Color " + (sourceColorIndex + 1);
            for (int index = 0; index < details.Length; index++)
            {
                if (GetDetailName(details[index].DetailDefine.Key) == sourceName)
                {
                    source = details[index];
                    break;
                }
            }
            if (source?.DetailDefine.Get == null)
            {
                return;
            }
            object color = source.DetailDefine.Get(character);
            for (int index = 0; index < details.Length; index++)
            {
                CharaDetailInfo detail = details[index];
                string name = GetDetailName(detail.DetailDefine.Key);
                if (!Regex.IsMatch(name, "^Color [123]$") || detail.DetailDefine.Set == null)
                {
                    continue;
                }
                detail.DetailDefine.Set(character, color);
                if (detail.DetailDefine.Upd != null && !LaterUpdate)
                {
                    detail.DetailDefine.Upd(character);
                }
            }
        }

        private void HandleMainGameResizeGripInput(
            int windowId,
            Rect windowRectToResize,
            float minimumWidth,
            float minimumHeight,
            bool resizeFromLeft)
        {
            EnsureResizeGripStyle();
            Rect gripRect = new Rect(
                resizeFromLeft ? 4f : windowRectToResize.width - ResizeGripSize - 4f,
                windowRectToResize.height - ResizeGripSize - 4f,
                ResizeGripSize,
                ResizeGripSize);
            int controlId = GUIUtility.GetControlID(
                ("StudioCharaEditorMainGameResizeGrip" + windowId).GetHashCode(),
                FocusType.Passive,
                gripRect);
            Event currentEvent = Event.current;
            switch (currentEvent.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (currentEvent.button == 0 && gripRect.Contains(currentEvent.mousePosition))
                    {
                        mainGameResizeWindowId = windowId;
                        mainGameResizeStartMouse = GUIUtility.GUIToScreenPoint(currentEvent.mousePosition);
                        mainGameResizeStartSize = windowRectToResize.size;
                        mainGameResizeStartRightEdge = windowRectToResize.xMax;
                        selectorThumbLoadPauseUntil =
                            Time.realtimeSinceStartup + SelectorThumbLoadIdleDelay;
                        GUIUtility.hotControl = controlId;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (mainGameResizeWindowId == windowId && GUIUtility.hotControl == controlId)
                    {
                        Vector2 currentMouse = GUIUtility.GUIToScreenPoint(currentEvent.mousePosition);
                        Vector2 delta = currentMouse - mainGameResizeStartMouse;
                        float newWidth = resizeFromLeft
                            ? mainGameResizeStartSize.x - delta.x
                            : mainGameResizeStartSize.x + delta.x;
                        SetMainGameWindowSize(
                            windowId,
                            Math.Max(minimumWidth, newWidth),
                            Math.Max(minimumHeight, mainGameResizeStartSize.y + delta.y),
                            resizeFromLeft);
                        selectorThumbLoadPauseUntil =
                            Time.realtimeSinceStartup + SelectorThumbLoadIdleDelay;
                        currentEvent.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (mainGameResizeWindowId == windowId && GUIUtility.hotControl == controlId)
                    {
                        mainGameResizeWindowId = 0;
                        GUIUtility.hotControl = 0;
                        selectorThumbLoadPauseUntil =
                            Time.realtimeSinceStartup + SelectorThumbLoadIdleDelay;
                        ClampMainGamePanelRects();
                        PersistMainGamePanelPositions();
                        currentEvent.Use();
                    }
                    break;
            }
        }

        private void DrawMainGameResizeGrip(
            int windowId,
            Rect windowRectToResize,
            bool resizeFromLeft)
        {
            EnsureResizeGripStyle();
            Rect gripRect = new Rect(
                resizeFromLeft ? 4f : windowRectToResize.width - ResizeGripSize - 4f,
                windowRectToResize.height - ResizeGripSize - 4f,
                ResizeGripSize,
                ResizeGripSize);
            GUI.Label(gripRect, "///", resizeGripStyle);
        }

        private void SetMainGameWindowSize(
            int windowId,
            float width,
            float height,
            bool resizeFromLeft)
        {
            if (windowId == MainGameLeftWindowId)
            {
                mainGameLeftRect.width = width;
                mainGameLeftRect.height = height;
            }
            else if (windowId == MainGameRightWindowId)
            {
                mainGameRightRect.width = width;
                mainGameRightRect.height = height;
                if (resizeFromLeft)
                {
                    mainGameRightRect.x = mainGameResizeStartRightEdge - width;
                }
            }
            else if (windowId == MainGameStatusWindowId)
            {
                mainGameStatusRect.width = width;
                mainGameStatusRect.height = height;
                if (resizeFromLeft)
                {
                    mainGameStatusRect.x = mainGameResizeStartRightEdge - width;
                }
            }
            else if (windowId == MainGamePluginWindowId)
            {
                mainGamePluginRect.width = width;
                mainGamePluginRect.height = height;
                if (resizeFromLeft)
                {
                    mainGamePluginRect.x = mainGameResizeStartRightEdge - width;
                }
            }
        }

        private void DrawMainGameWindowTitle(string title, int fontSize, float windowWidth)
        {
            GUIStyle style = new GUIStyle(theme.MainGameTitleStyle)
            {
                fontSize = fontSize,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            GUI.Label(new Rect(16f, 7f, Math.Max(0f, windowWidth - 66f), 42f), title, style);
        }

        private void DrawMainGamePluginSettings()
        {
            switch (mainGameSettingsPage)
            {
                case 0:
                    DrawMainGameNameSettings();
                    break;
                case 1:
                    DrawMainGamePersonalitySettings();
                    break;
                case 2:
                    DrawMainGameTraitsSettings();
                    break;
                case 3:
                    DrawMainGameCharacterCardSettings();
                    break;
                case 4:
                    DrawMainGameUnavailableSettingsPage("Character card loading is not exposed by Studio.");
                    break;
                case 5:
                    DrawMainGameUnavailableSettingsPage("Character fusion is a Maker-only operation.");
                    break;
                case 7:
                    DrawMainGameUnavailableSettingsPage("Card author data is provided by its Maker plug-in.");
                    break;
                case 8:
                    DrawMainGameSystemSettings();
                    break;
                default:
                    DrawMainGameSystemSettings();
                    break;
            }
        }

        private string GetMainGameSettingsPageTitle()
        {
            switch (mainGameSettingsPage)
            {
                case 0: return "Name";
                case 1: return "Personality";
                case 2: return "Traits";
                case 3: return "Save / Delete";
                case 4: return "Load";
                case 5: return "Fusion";
                case 7: return "Card author data";
                case 8: return "Settings";
                default: return "Options";
            }
        }

        private void DrawMainGameNameSettings()
        {
            ChaControl character = ociTarget?.charInfo;
            if (character?.chaFile?.parameter == null)
            {
                GUILayout.Label(LC("Please select a charactor to edit."));
                return;
            }

            var parameter = character.chaFile.parameter;
            GUILayout.Label("Name", GetMainGameAccentLabelStyle());
            string newName = GUILayout.TextField(parameter.fullname ?? string.Empty, GUILayout.Height(48f));
            if (!string.Equals(newName, parameter.fullname, StringComparison.Ordinal))
            {
                parameter.fullname = newName;
                if (ociTarget?.treeNodeObject != null)
                {
                    ociTarget.treeNodeObject.textName = newName;
                }
            }
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = false;
            GUILayout.Button("Random", GUILayout.Width(120f), GUILayout.Height(38f));
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
        }

        private static GUIStyle GetMainGameAccentLabelStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleLeft
            };
            style.normal.textColor = new Color32(218, 207, 42, 255);
            return style;
        }

        private byte DrawMainGameByteStepper(byte value, int minimum, int maximum)
        {
            int current = Mathf.Clamp(value, minimum, maximum);
            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                current = current <= minimum ? maximum : current - 1;
            }
            GUILayout.Label(current.ToString(), GUI.skin.textField, GUILayout.Width(44f));
            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                current = current >= maximum ? minimum : current + 1;
            }
            return (byte)current;
        }

        private void DrawMainGamePersonalitySettings()
        {
            ChaControl character = ociTarget?.charInfo;
            if (character?.chaFile?.parameter == null)
            {
                GUILayout.Label(LC("Please select a charactor to edit."));
                return;
            }
            GUILayout.Label("Personality", theme.MainGameSectionHeaderStyle);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Type", GUILayout.Width(90f));
            int personality = character.chaFile.parameter.personality;
            if (GUILayout.Button("<", GUILayout.Width(34f))) personality = Math.Max(0, personality - 1);
            GUILayout.Label(personality.ToString(), GUI.skin.textField, GUILayout.Width(64f));
            if (GUILayout.Button(">", GUILayout.Width(34f))) personality++;
            character.chaFile.parameter.personality = personality;
            GUILayout.EndHorizontal();
        }

        private void DrawMainGameTraitsSettings()
        {
            GUILayout.Label("Traits", theme.MainGameSectionHeaderStyle);
            GUILayout.Label("Studio can retain the card's existing traits, but Maker-only trait lists are not available here.");
        }

        private void DrawMainGameCharacterCardSettings()
        {
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && ociTarget?.charInfo != null;
            if (GUILayout.Button("Save character card", GUILayout.Height(40f)))
            {
                BeginMainGameSave();
            }
            if (GUILayout.Button("Revert all changes", GUILayout.Height(40f)) &&
                CharaEditorMgr.Instance.GetEditorController(ociTarget) is CharaEditorController controller)
            {
                controller.RevertAll();
            }
            GUI.enabled = oldEnabled;
        }

        private void DrawMainGameSystemSettings()
        {
            GUILayout.Label("Interface", theme.MainGameSectionHeaderStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Modern", theme.MainGameTabStyle))
            {
                PersistMainGamePanelPositions();
                StudioCharaEditor.UITheme.Value = CharaEditorUiTheme.Modern;
                StudioCharaEditor.SaveConfigNow();
            }
            GUILayout.Button("Main Game", theme.MainGameTabSelectedStyle);
            GUILayout.EndHorizontal();
            DrawMainGameDivider();
            GUILayout.Space(8f);
            if (GUILayout.Button("Reset Windows Positions"))
            {
                ResetMainGamePanelPositions();
            }
        }

        private void DrawMainGameUiScaleSetting()
        {
            float currentScale = Mathf.Clamp(StudioCharaEditor.MainGameUIScale.Value, 0.75f, 1.6f);
            if (string.IsNullOrWhiteSpace(mainGameUiScalePercentText))
            {
                mainGameUiScalePercentText = FormatMainGameUiScale(currentScale);
            }

            GUILayout.Label("UI Scale", GetMainGameAccentLabelStyle());
            GUILayout.BeginHorizontal(GUILayout.Height(36f));
            GUI.SetNextControlName("StudioCharaEditorMainGameUiScale");
            string nextText = GUILayout.TextField(
                mainGameUiScalePercentText,
                GUILayout.Width(82f),
                GUILayout.Height(32f));
            if (!string.Equals(nextText, mainGameUiScalePercentText, StringComparison.Ordinal))
            {
                mainGameUiScalePercentText = nextText;
            }
            if (GUILayout.Button("Apply", GUILayout.Width(70f), GUILayout.Height(32f)))
            {
                ApplyMainGameUiScaleText();
            }
            GUILayout.Label("75–160% or 0.75–1.6", GUILayout.Height(32f));
            GUILayout.EndHorizontal();

            Event currentEvent = Event.current;
            if (currentEvent.type == EventType.KeyDown &&
                (currentEvent.keyCode == KeyCode.Return || currentEvent.keyCode == KeyCode.KeypadEnter) &&
                string.Equals(GUI.GetNameOfFocusedControl(), "StudioCharaEditorMainGameUiScale", StringComparison.Ordinal))
            {
                ApplyMainGameUiScaleText();
                GUI.FocusControl(string.Empty);
                currentEvent.Use();
            }
        }

        private void ApplyMainGameUiScaleText()
        {
            string normalized = (mainGameUiScalePercentText ?? string.Empty).Trim().Replace(',', '.');
            if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out float enteredValue))
            {
                mainGameUiScalePercentText = FormatMainGameUiScale(StudioCharaEditor.MainGameUIScale.Value);
                return;
            }
            float scale = enteredValue <= 1.6f ? enteredValue : enteredValue / 100f;
            scale = Mathf.Clamp(scale, 0.75f, 1.6f);
            StudioCharaEditor.MainGameUIScale.Value = scale;
            mainGameUiScalePercentText = FormatMainGameUiScale(scale);
            ClampMainGamePanelRects();
            PersistMainGamePanelPositions();
        }

        private static string FormatMainGameUiScale(float scale)
        {
            float percent = Mathf.Clamp(scale, 0.75f, 1.6f) * 100f;
            return Mathf.Abs(percent - Mathf.Round(percent)) < 0.05f
                ? Mathf.RoundToInt(percent).ToString(CultureInfo.InvariantCulture)
                : percent.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void DrawMainGameUnavailableSettingsPage(string message)
        {
            GUILayout.Label(message);
        }

        private void DrawMainGameCharacterDetail(
            CharaEditorController controller,
            string category1,
            string category2,
            string detailSetKey)
        {
            if (detailPageSelect == SelectMode.PasteSlotPrompt)
            {
                DrawMainGamePasteSlotPrompt(controller);
                return;
            }

            if (!controller.myDetailSet.ContainsKey(detailSetKey))
            {
                GUILayout.Label("Detail of " + detailSetKey + " is not defined");
                return;
            }

            CharaDetailInfo[] detailSet = controller.GetDetailInfoList(category1, category2);
            string selectedTab = DrawMainGameDetailTabs(controller, detailSetKey, detailSet);
            DrawMainGameDivider();
            detailPageSelect = SelectMode.Normal;
            rightScroll.x = 0f;
            rightScroll = GUILayout.BeginScrollView(
                rightScroll,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.ExpandHeight(true));
            rightScroll.x = 0f;
            float rightViewportWidth = Math.Max(120f, mainGameRightRect.width - 56f);
            mainGameRightContentWidth = rightViewportWidth;
            GUILayout.BeginVertical(
                GUILayout.Width(rightViewportWidth),
                GUILayout.MaxWidth(rightViewportWidth),
                GUILayout.ExpandHeight(false));
            bool isClothesColorPage = detailSetKey.StartsWith("Clothes#", StringComparison.Ordinal) &&
                                      selectedTab != null &&
                                      selectedTab.StartsWith("Color ", StringComparison.Ordinal);
            int clothesColorIndex = isClothesColorPage
                ? Math.Max(0, int.Parse(selectedTab.Substring("Color ".Length), CultureInfo.InvariantCulture) - 1)
                : -1;
            if (isClothesColorPage)
            {
                EnsureMainGameSliderStyleCache();
                GUIStyle colorHeaderStyle = new GUIStyle(mainGameSliderAccentLabelStyle)
                {
                    fontSize = 24,
                    fixedHeight = 38f
                };
                GUILayout.Label(selectedTab, colorHeaderStyle, GUILayout.Height(38f));
            }
            bool drewClothesChannel = false;
            foreach (CharaDetailInfo detail in detailSet)
            {
                if (detailSetKey == "Body#ShapeWhole" &&
                    (detail.DetailDefine.Catelog == CharaDetailDefine.CharaDetailDefineCatelog.ABMX ||
                     detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET1 ||
                     detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET2 ||
                     detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET3))
                {
                    continue;
                }
                if (!MainGameDetailBelongsToTab(detailSetKey, detail.DetailDefine.Key, selectedTab))
                {
                    continue;
                }
                if (ShouldSuppressMainGameSeparator(detailSetKey, detail, selectedTab))
                {
                    continue;
                }
                if (isClothesColorPage &&
                    !drewClothesChannel &&
                    GetDetailName(detail.DetailDefine.Key).StartsWith(
                        "Pattern ",
                        StringComparison.Ordinal))
                {
                    DrawMainGameClothesChannelControls(
                        controller.ociTarget?.charInfo,
                        detailSetKey,
                        detailSet,
                        clothesColorIndex);
                    DrawMainGameDivider();
                    drewClothesChannel = true;
                }
                DrawMainGameDetailItem(detail, detailSetKey);
                GUILayout.Space(3f);
            }
            if (detailSetKey == "Body#ShapeWhole")
            {
                DrawMainGameBodyOverallExtensions(controller.ociTarget?.charInfo, detailSet);
            }
            if (isClothesColorPage && DrawMainGameFullWidthButton("Set all colors"))
            {
                SetMainGameAllClothesColors(
                    controller.ociTarget?.charInfo,
                    detailSet,
                    clothesColorIndex);
            }
            string materialEditorFilter = detailSetKey == "Face#ShapeMouth"
                ? "tang,tooth"
                : detailSetKey == "Face#EyeL" ||
                  detailSetKey == "Face#EyeR" ||
                  detailSetKey == "Face#EyeEtc" ||
                  detailSetKey == "Face#EyeHL"
                    ? "eyebase,eyeshadow"
                    : detailSetKey == "Face#Eyelashes"
                        ? "eyelashes"
                        : null;
            if (materialEditorFilter != null)
            {
                DrawMainGameDivider();
                if (DrawMainGameFullWidthButton("Material Editor"))
                {
                    OpenMainGameMaterialEditor(
                        controller.ociTarget?.charInfo,
                        "Character",
                        0,
                        materialEditorFilter);
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawMainGameBodyOverallExtensions(
            ChaControl character,
            CharaDetailInfo[] detailSet)
        {
            if (character == null)
            {
                return;
            }

            DrawMainGameDivider();
            bool invisibleBody = !character.visibleBody;
            bool newInvisibleBody = DrawMainGameCheckbox(invisibleBody, "Invisible Body");
            if (newInvisibleBody != invisibleBody)
            {
                character.visibleBody = !newInvisibleBody;
                character.UpdateVisible();
            }

            if (DrawMainGameFullWidthButton("Material Editor (Body)"))
            {
                OpenMainGameMaterialEditor(character, "Character", 0, "body");
            }
            if (DrawMainGameFullWidthButton("Material Editor (Head)"))
            {
                OpenMainGameMaterialEditor(character, "Character", 0, "head");
            }
            if (DrawMainGameFullWidthButton("Material Editor (All)"))
            {
                OpenMainGameMaterialEditor(character, "Character", 0, string.Empty);
            }

            DrawMainGameDivider();
            GUILayout.Label("Uncensor Selector", theme.MainGameListButtonStyle);
            Rect uncensorRect = GetMainGameRightRowRect(38f);
            float uncensorLabelWidth = Mathf.Clamp(uncensorRect.width * 0.24f, 58f, 90f);
            GUI.Label(new Rect(uncensorRect.x, uncensorRect.y, uncensorLabelWidth, uncensorRect.height), "Body");
            Rect uncensorButtonRect = new Rect(
                uncensorRect.x + uncensorLabelWidth + 6f,
                uncensorRect.y,
                Math.Max(20f, uncensorRect.width - uncensorLabelWidth - 6f),
                uncensorRect.height);
            if (GUI.Button(
                uncensorButtonRect,
                GetMainGameUncensorBodyName(character),
                GUI.skin.textField))
            {
                mainGameUncensorListOpen = !mainGameUncensorListOpen;
                if (mainGameUncensorListOpen)
                {
                    RefreshMainGameUncensorOptions();
                    mainGameUncensorScroll = Vector2.zero;
                }
            }
            DrawMainGameUncensorList(character);

            CharaDetailInfo bodyScaleDetail = FindMainGameBodyScaleDetail(detailSet);
            if (bodyScaleDetail != null)
            {
                DrawMainGameDivider();
                float[] scaleValues = bodyScaleDetail.DetailDefine.Get(character) as float[];
                if (scaleValues != null && scaleValues.Length >= 3)
                {
                    float[] revertValues = bodyScaleDetail.RevertValue as float[];
                    float[] newValues = (float[])scaleValues.Clone();
                    bool scaleChanged = false;
                    bool splitXyz = IsMainGameSplitXyzEnabled();
                    if (splitXyz)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            float? revertScale = revertValues != null && axis < revertValues.Length
                                ? (float?)revertValues[axis]
                                : null;
                            float axisValue = DrawMainGameRawSliderRow(
                                MainGameBodyScaleAxisNames[axis],
                                scaleValues[axis],
                                0f,
                                2f,
                                revertScale,
                                true);
                            if (!Mathf.Approximately(axisValue, scaleValues[axis]))
                            {
                                newValues[axis] = axisValue;
                                scaleChanged = true;
                            }
                        }
                    }
                    else
                    {
                        float oldScale = (scaleValues[0] + scaleValues[1] + scaleValues[2]) / 3f;
                        float? revertScale = revertValues != null && revertValues.Length >= 3
                            ? (float?)((revertValues[0] + revertValues[1] + revertValues[2]) / 3f)
                            : null;
                        float newScale = DrawMainGameRawSliderRow(
                            "Body Scale",
                            oldScale,
                            0f,
                            2f,
                            revertScale,
                            true);
                        if (!Mathf.Approximately(newScale, oldScale))
                        {
                            newValues[0] = newScale;
                            newValues[1] = newScale;
                            newValues[2] = newScale;
                            scaleChanged = true;
                        }
                    }
                    if (scaleChanged)
                    {
                        bodyScaleDetail.DetailDefine.Set(character, newValues);
                    }
                }
            }
            DrawMainGameDivider();
            GUILayout.Label("Beaver shapes", theme.MainGameListButtonStyle);
        }

        private static CharaDetailInfo FindMainGameBodyScaleDetail(CharaDetailInfo[] detailSet)
        {
            if (detailSet == null)
            {
                return null;
            }
            for (int index = 0; index < detailSet.Length; index++)
            {
                CharaDetailInfo detail = detailSet[index];
                if (detail?.DetailDefine?.Key == "Body#ShapeWhole#ABMX Body")
                {
                    return detail;
                }
            }
            return null;
        }

        private Rect GetMainGameRightRowRect(float height)
        {
            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(height),
                GUILayout.ExpandWidth(true));
            float width = mainGameRightContentWidth > 1f
                ? mainGameRightContentWidth
                : Math.Max(120f, mainGameRightRect.width - 56f);
            rect.width = Math.Min(rect.width, width);
            return rect;
        }

        private bool DrawMainGameFullWidthButton(string label)
        {
            Rect rect = GetMainGameRightRowRect(40f);
            return GUI.Button(rect, label);
        }

        private static GameObject GetMainGameMaterialEditorTarget(
            ChaControl character,
            bool hair,
            int slot)
        {
            if (character == null || slot < 0)
            {
                return null;
            }
            if (hair)
            {
                return character.objHair != null && slot < character.objHair.Length
                    ? character.objHair[slot]
                    : null;
            }
            return character.objClothes != null && slot < character.objClothes.Length
                ? character.objClothes[slot]
                : null;
        }

        private void OpenMainGameMaterialEditor(
            ChaControl character,
            string objectTypeName,
            int slot,
            string filter)
        {
            if (character == null)
            {
                return;
            }
            try
            {
                Type studioType = FindLoadedType("KK_Plugins.MaterialEditor.MEStudio");
                object studioInstance = studioType?.GetField(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                Type objectDataType = FindLoadedType("KK_Plugins.MaterialEditor.ObjectData");
                Type controllerType = FindLoadedType(
                    "KK_Plugins.MaterialEditor.MaterialEditorCharaController");
                Type objectType = controllerType?.GetNestedType(
                    "ObjectType",
                    BindingFlags.Public | BindingFlags.NonPublic);
                if (studioInstance == null || objectDataType == null || objectType == null)
                {
                    throw new InvalidOperationException("Material Editor Studio API was not found.");
                }

                object enumValue = Enum.Parse(objectType, objectTypeName, true);
                object objectData = Activator.CreateInstance(
                    objectDataType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new object[] { slot, enumValue },
                    CultureInfo.InvariantCulture);
                GameObject target = objectTypeName == "Character"
                    ? character.gameObject
                    : GetMainGameMaterialEditorTarget(
                        character,
                        objectTypeName == "Hair",
                        slot);
                if (target == null)
                {
                    return;
                }

                MethodInfo populateList = null;
                for (Type type = studioInstance.GetType(); type != null && populateList == null; type = type.BaseType)
                {
                    MethodInfo[] methods = type.GetMethods(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.DeclaredOnly);
                    for (int index = 0; index < methods.Length; index++)
                    {
                        if (methods[index].Name == "PopulateList" &&
                            methods[index].GetParameters().Length == 3)
                        {
                            populateList = methods[index];
                            break;
                        }
                    }
                }
                if (populateList == null)
                {
                    throw new MissingMethodException("Material Editor PopulateList was not found.");
                }
                populateList.Invoke(studioInstance, new object[] { target, objectData, filter });
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning(
                    "Material Editor could not be opened: " +
                    (ex.InnerException?.Message ?? ex.Message));
            }
        }

        private void DrawMainGameUncensorList(ChaControl character)
        {
            if (!mainGameUncensorListOpen)
            {
                return;
            }

            if (mainGameUncensorOptions.Count == 0)
            {
                Rect statusRect = GetMainGameRightRowRect(38f);
                GUI.Label(
                    statusRect,
                    string.IsNullOrEmpty(mainGameUncensorStatus)
                        ? "No body uncensors found"
                        : mainGameUncensorStatus);
                return;
            }

            TryGetMainGameUncensorGuid(character, out string currentGuid);
            const float rowHeight = 36f;
            float contentHeight = mainGameUncensorOptions.Count * rowHeight + 4f;
            float visibleHeight = Mathf.Min(220f, Math.Max(rowHeight + 4f, contentHeight));
            Rect listRect = GetMainGameRightRowRect(visibleHeight);
            float viewWidth = Math.Max(20f, listRect.width - 16f);
            Rect viewRect = new Rect(0f, 0f, viewWidth, Math.Max(visibleHeight, contentHeight));
            mainGameUncensorScroll = GUI.BeginScrollView(
                listRect,
                mainGameUncensorScroll,
                viewRect,
                false,
                false);
            for (int index = 0; index < mainGameUncensorOptions.Count; index++)
            {
                MainGameUncensorOption option = mainGameUncensorOptions[index];
                Rect optionRect = new Rect(0f, index * rowHeight, viewWidth, rowHeight - 2f);
                GUIStyle optionStyle = string.Equals(currentGuid, option.Guid, StringComparison.Ordinal)
                    ? theme.MainGameListSelectedStyle
                    : theme.MainGameListButtonStyle;
                if (!GUI.Button(optionRect, option.DisplayName, optionStyle))
                {
                    continue;
                }

                if (TryApplyMainGameUncensor(character, option.Guid, out string error))
                {
                    mainGameUncensorListOpen = false;
                    mainGameUncensorStatus = string.Empty;
                }
                else
                {
                    mainGameUncensorStatus = error;
                    StudioCharaEditor.Logger?.LogWarning(error);
                }
            }
            GUI.EndScrollView();
        }

        private void RefreshMainGameUncensorOptions()
        {
            mainGameUncensorOptions.Clear();
            mainGameUncensorStatus = string.Empty;
            try
            {
                Type selectorType = FindLoadedType("KK_Plugins.UncensorSelector");
                FieldInfo dictionaryField = selectorType?.GetField(
                    "BodyDictionary",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                System.Collections.IEnumerable dictionary =
                    dictionaryField?.GetValue(null) as System.Collections.IEnumerable;
                if (dictionary == null)
                {
                    mainGameUncensorStatus = "Uncensor Selector is not available";
                    return;
                }

                foreach (object entry in dictionary)
                {
                    object bodyData = GetMainGameReflectionMember(entry, "Value");
                    string guid = GetMainGameReflectionString(bodyData, "BodyGUID");
                    string displayName = GetMainGameReflectionString(bodyData, "DisplayName");
                    if (string.IsNullOrEmpty(guid) || string.IsNullOrEmpty(displayName))
                    {
                        continue;
                    }
                    mainGameUncensorOptions.Add(new MainGameUncensorOption(guid, displayName));
                }
                mainGameUncensorOptions.Sort((left, right) => string.Compare(
                    left.DisplayName,
                    right.DisplayName,
                    StringComparison.OrdinalIgnoreCase));
                if (mainGameUncensorOptions.Count == 0)
                {
                    mainGameUncensorStatus = "Uncensor list is empty";
                }
            }
            catch (Exception exception)
            {
                mainGameUncensorStatus = "Failed to load uncensors: " +
                    exception.GetBaseException().Message;
            }
        }

        private static string GetMainGameUncensorBodyName(ChaControl character)
        {
            try
            {
                object controller = GetMainGameUncensorController(character);
                object bodyData = GetMainGameReflectionMember(controller, "BodyData");
                string displayName = GetMainGameReflectionString(bodyData, "DisplayName");
                return string.IsNullOrEmpty(displayName) ? "Default" : displayName;
            }
            catch
            {
                return "Default";
            }
        }

        private static bool TryGetMainGameUncensorGuid(ChaControl character, out string guid)
        {
            guid = null;
            try
            {
                object controller = GetMainGameUncensorController(character);
                guid = GetMainGameReflectionString(controller, "BodyGUID");
                if (string.IsNullOrEmpty(guid))
                {
                    object bodyData = GetMainGameReflectionMember(controller, "BodyData");
                    guid = GetMainGameReflectionString(bodyData, "BodyGUID");
                }
                return !string.IsNullOrEmpty(guid);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryApplyMainGameUncensor(
            ChaControl character,
            string guid,
            out string error)
        {
            error = string.Empty;
            if (character == null)
            {
                error = "Character is missing";
                return false;
            }
            try
            {
                const BindingFlags flags =
                    BindingFlags.Instance | BindingFlags.Static |
                    BindingFlags.Public | BindingFlags.NonPublic;
                Type selectorType = FindLoadedType("KK_Plugins.UncensorSelector");
                if (selectorType == null)
                {
                    error = "Uncensor Selector is not available";
                    return false;
                }
                MethodInfo dropdownChanged = selectorType.GetMethod(
                    "BodyDropdownChangedStudio",
                    flags);
                if (dropdownChanged != null)
                {
                    dropdownChanged.Invoke(null, new object[] { character, guid });
                    return true;
                }

                object controller = GetMainGameUncensorController(character);
                if (controller == null)
                {
                    error = "Uncensor Selector controller was not found";
                    return false;
                }
                Type controllerType = controller.GetType();
                PropertyInfo bodyGuidProperty = controllerType.GetProperty("BodyGUID", flags);
                if (bodyGuidProperty?.CanWrite == true)
                {
                    bodyGuidProperty.SetValue(controller, guid, null);
                }
                else
                {
                    controllerType.GetField("BodyGUID", flags)?.SetValue(controller, guid);
                }
                MethodInfo updateUncensor = controllerType.GetMethod("UpdateUncensor", flags);
                if (updateUncensor == null)
                {
                    error = "Uncensor Selector update method was not found";
                    return false;
                }
                updateUncensor.Invoke(controller, null);
                return true;
            }
            catch (Exception exception)
            {
                error = "Failed to apply uncensor: " + exception.GetBaseException().Message;
                return false;
            }
        }

        private static object GetMainGameUncensorController(ChaControl character)
        {
            if (character == null)
            {
                return null;
            }
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;
            Type selectorType = FindLoadedType("KK_Plugins.UncensorSelector");
            MethodInfo getController = selectorType?.GetMethod("GetController", flags);
            if (getController != null)
            {
                return getController.Invoke(null, new object[] { character });
            }
            Type controllerType = FindLoadedType(
                "KK_Plugins.UncensorSelector+UncensorSelectorController");
            return controllerType == null ? null : character.GetComponent(controllerType);
        }

        private static object GetMainGameReflectionMember(object instance, string memberName)
        {
            if (instance == null)
            {
                return null;
            }
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                return property.GetValue(instance, null);
            }
            return type.GetField(memberName, flags)?.GetValue(instance);
        }

        private static bool SetMainGameReflectionMember(
            object instance,
            string memberName,
            object value)
        {
            if (instance == null)
            {
                return false;
            }
            const BindingFlags flags =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(instance, ConvertMainGameReflectionValue(value, property.PropertyType), null);
                return true;
            }
            FieldInfo field = type.GetField(memberName, flags);
            if (field == null)
            {
                return false;
            }
            field.SetValue(instance, ConvertMainGameReflectionValue(value, field.FieldType));
            return true;
        }

        private static object GetMainGameStaticReflectionMember(Type type, string memberName)
        {
            if (type == null)
            {
                return null;
            }
            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(memberName, flags);
            return property != null
                ? property.GetValue(null, null)
                : type.GetField(memberName, flags)?.GetValue(null);
        }

        private static bool SetMainGameStaticReflectionMember(
            Type type,
            string memberName,
            object value)
        {
            if (type == null)
            {
                return false;
            }
            const BindingFlags flags =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(null, ConvertMainGameReflectionValue(value, property.PropertyType), null);
                return true;
            }
            FieldInfo field = type.GetField(memberName, flags);
            if (field == null)
            {
                return false;
            }
            field.SetValue(null, ConvertMainGameReflectionValue(value, field.FieldType));
            return true;
        }

        private static object ConvertMainGameReflectionValue(object value, Type targetType)
        {
            if (value == null || targetType.IsInstanceOfType(value))
            {
                return value;
            }
            if (targetType.IsEnum)
            {
                return Enum.ToObject(targetType, value);
            }
            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static object InvokeMainGameMethod(
            object instance,
            string methodName,
            params object[] arguments)
        {
            if (instance == null)
            {
                return null;
            }
            return InvokeMainGameMethodCore(
                instance.GetType(),
                instance,
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                arguments);
        }

        private static object InvokeMainGameStaticMethod(
            Type type,
            string methodName,
            params object[] arguments)
        {
            return type == null
                ? null
                : InvokeMainGameMethodCore(
                    type,
                    null,
                    methodName,
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    arguments);
        }

        private static object InvokeMainGameMethodCore(
            Type type,
            object instance,
            string methodName,
            BindingFlags flags,
            object[] arguments)
        {
            object[] values = arguments ?? Array.Empty<object>();
            foreach (MethodInfo method in type.GetMethods(flags))
            {
                if (!string.Equals(method.Name, methodName, StringComparison.Ordinal) ||
                    method.GetParameters().Length != values.Length)
                {
                    continue;
                }
                ParameterInfo[] parameters = method.GetParameters();
                object[] converted = new object[values.Length];
                bool compatible = true;
                for (int index = 0; index < values.Length; index++)
                {
                    try
                    {
                        converted[index] = ConvertMainGameReflectionValue(
                            values[index],
                            parameters[index].ParameterType);
                    }
                    catch
                    {
                        compatible = false;
                        break;
                    }
                }
                if (compatible)
                {
                    return method.Invoke(instance, converted);
                }
            }
            return null;
        }

        private static float GetMainGameFloat(object instance, string memberName)
        {
            object value = GetMainGameReflectionMember(instance, memberName);
            return value == null
                ? 0f
                : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }

        private static void SetMainGameFloatIfChanged(
            object instance,
            string memberName,
            float oldValue,
            float newValue)
        {
            if (!Mathf.Approximately(oldValue, newValue))
            {
                SetMainGameReflectionMember(instance, memberName, newValue);
            }
        }

        private static string GetMainGameReflectionString(object instance, string memberName)
        {
            return GetMainGameReflectionMember(instance, memberName)?.ToString();
        }

        private sealed class MainGameUncensorOption
        {
            internal MainGameUncensorOption(string guid, string displayName)
            {
                Guid = guid;
                DisplayName = displayName;
            }

            internal string Guid { get; }
            internal string DisplayName { get; }
        }

        private void DrawMainGameDetailItem(CharaDetailInfo detail, string detailSetKey)
        {
            string detailKey = detail.DetailDefine.Key;
            string detailName = GetMainGameDetailName(detailKey);
            if (detailPageSelect != SelectMode.Normal)
            {
                if (detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.SEPERATOR)
                {
                    return;
                }
                if (selectBuffer.ContainsKey(detailKey))
                {
                    selectBuffer[detailKey] = DrawModernToggle(selectBuffer[detailKey], LC(detailName));
                }
                else
                {
                    GUILayout.Label(greyText("    " + LC(detailName)));
                }
                return;
            }

            ChaControl character = ociTarget.charInfo;
            switch (detail.DetailDefine.Type)
            {
                case CharaDetailDefine.CharaDetailDefineType.SLIDER:
                    guiRenderMainGameSlider(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.COLOR:
                    guiRenderMainGameColor(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.SELECTOR:
                    guiRenderMainGameSelector(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.SEPERATOR:
                    guiRenderMainGameSeparator(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.TOGGLE:
                    guiRenderMainGameToggle(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.VALUEINPUT:
                    guiRenderMainGameValueInput(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.INT_STATUS:
                    guiRenderMainGameIntStatus(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.HAIR_BUNDLE:
                    guiRenderMainGameHairBundle(character, detailSetKey, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.BUTTON:
                    guiRenderMainGameButton(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET1:
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET2:
                case CharaDetailDefine.CharaDetailDefineType.ABMXSET3:
                    guiRenderMainGameABMXSet(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.SKIN_OVERLAY:
                    guiRenderSkinOverlay(character, detailName, detail);
                    break;
                case CharaDetailDefine.CharaDetailDefineType.CLOTH_OVERLAY:
                    guiRenderClothOverlay(character, detailName, detail);
                    break;
                default:
                    GUILayout.Label(detailName + ": UNKNOWN type not implemented");
                    break;
            }
        }

        private GUIStyle CreateMainGameResetButtonStyle()
        {
            EnsureMainGameSliderStyleCache();
            return mainGameResetButtonStyle;
        }

        private void EnsureMainGameSliderStyleCache()
        {
            if (mainGameSliderStyleTheme == theme &&
                mainGameSliderLabelStyle != null)
            {
                return;
            }

            mainGameSliderStyleTheme = theme;
            mainGameSliderLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 22,
                clipping = TextClipping.Clip
            };
            mainGameSliderAccentLabelStyle = new GUIStyle(mainGameSliderLabelStyle);
            Color accent = new Color(0.91f, 0.82f, 0.18f, 1f);
            mainGameSliderAccentLabelStyle.normal.textColor = accent;
            mainGameSliderAccentLabelStyle.hover.textColor = accent;
            mainGameSliderAccentLabelStyle.active.textColor = accent;
            mainGameSliderAccentLabelStyle.focused.textColor = accent;

            mainGameSliderValueStyle = new GUIStyle(theme.MainGameNumericValueStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 22,
                fixedHeight = 42f
            };
            mainGameSliderPreciseValueStyle = new GUIStyle(mainGameSliderValueStyle)
            {
                fontSize = 17
            };
            mainGameResetButtonStyle = new GUIStyle(theme.MainGameIconButtonStyle)
            {
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            mainGameResetButtonStyle.normal.background = theme.MainGameExitNormalTexture;
            mainGameResetButtonStyle.hover.background = theme.MainGameExitSelectedTexture;
            mainGameResetButtonStyle.active.background = theme.MainGameExitSelectedTexture;
        }

        private static void DrawMainGameFittedLabel(
            Rect rect,
            string text,
            GUIStyle sourceStyle,
            int minimumFontSize = 15)
        {
            GUIStyle style = new GUIStyle(sourceStyle ?? GUI.skin.label)
            {
                clipping = TextClipping.Clip
            };
            GUIContent content = new GUIContent(text ?? string.Empty);
            int fontSize = style.fontSize > 0 ? style.fontSize : GUI.skin.label.fontSize;
            while (fontSize > minimumFontSize && style.CalcSize(content).x > rect.width - 4f)
            {
                fontSize--;
                style.fontSize = fontSize;
            }
            GUI.Label(rect, content, style);
        }

        private void guiRenderMainGameColor(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            Color oldColor = (Color)detail.DetailDefine.Get(character);
            if (detail.DetailDefine.Key.StartsWith("Clothes#", StringComparison.Ordinal))
            {
                Rect clothesRow = GetMainGameRightRowRect(42f);
                float clothesLabelWidth = Mathf.Clamp(clothesRow.width * 0.36f, 92f, 150f);
                Rect clothesLabelRect = new Rect(
                    clothesRow.x,
                    clothesRow.y,
                    clothesLabelWidth,
                    clothesRow.height);
                Rect clothesSwatchRect = new Rect(
                    clothesLabelRect.xMax + 6f,
                    clothesRow.y + 4f,
                    Math.Max(40f, clothesRow.xMax - clothesLabelRect.xMax - 6f),
                    clothesRow.height - 8f);
                DrawMainGameFittedLabel(clothesLabelRect, LC(name), GUI.skin.label);
                Texture2D clothesTexture = GetColorSwatchTexture(detail.DetailDefine.Key, oldColor);
                if (GUI.Button(
                    clothesSwatchRect,
                    clothesTexture,
                    colorSwatchButtonStyle ?? GUI.skin.button))
                {
                    Studio.Studio studio = Studio.Studio.Instance;
                    studio.colorPalette.Setup(
                        LC(name),
                        oldColor,
                        color =>
                        {
                            if (color != oldColor)
                            {
                                QueueColorChange(character, name, detail, color);
                            }
                        },
                        true);
                    studio.colorPalette.visible = true;
                }
                return;
            }
            string FormatColor(Color color) => string.Format(
                "R:{0:F0} G:{1:F0} B:{2:F0} A:{3:F0}",
                color.r * 255f,
                color.g * 255f,
                color.b * 255f,
                color.a * 100f);

            Rect rowRect = GetMainGameRightRowRect(42f);
            float gap = 4f;
            float resetWidth = Mathf.Clamp(rowRect.width * 0.09f, 30f, 38f);
            float labelWidth = Mathf.Clamp(rowRect.width * 0.25f, 76f, 110f);
            float swatchWidth = Mathf.Clamp(rowRect.width * 0.18f, 52f, 72f);
            float textWidth = Math.Max(
                40f,
                rowRect.width - labelWidth - swatchWidth - resetWidth - gap * 3f);
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect swatchRect = new Rect(labelRect.xMax + gap, rowRect.y + 6f, swatchWidth, 30f);
            Rect textRect = new Rect(swatchRect.xMax + gap, rowRect.y, textWidth, rowRect.height);
            Rect resetRect = new Rect(textRect.xMax + gap, rowRect.y + 2f, resetWidth, 38f);
            DrawMainGameFittedLabel(labelRect, LC(name), GUI.skin.label);
            Texture2D colorTexture = GetColorSwatchTexture(detail.DetailDefine.Key, oldColor);
            if (GUI.Button(swatchRect, colorTexture, colorSwatchButtonStyle ?? GUI.skin.button))
            {
                Studio.Studio studio = Studio.Studio.Instance;
                studio.colorPalette.Setup(
                    LC(name),
                    oldColor,
                    color =>
                    {
                        if (color != oldColor)
                        {
                            QueueColorChange(character, name, detail, color);
                        }
                    },
                    true);
                studio.colorPalette.visible = true;
            }
            GUIStyle colorTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                fontSize = 14
            };
            GUI.Label(textRect, FormatColor(oldColor), colorTextStyle);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && detail.RevertValue != null;
            if (GUI.Button(resetRect, GUIContent.none, CreateMainGameResetButtonStyle()))
            {
                QueueColorChange(character, name, detail, (Color)detail.RevertValue, true);
            }
            GUI.enabled = oldEnabled;
        }

        private void guiRenderMainGameToggle(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            bool oldValue = CharaDetailDefine.ParseBool(detail.DetailDefine.Get(character));
            Rect rowRect = GetMainGameRightRowRect(38f);
            float resetWidth = Mathf.Clamp(rowRect.width * 0.09f, 30f, 38f);
            Rect toggleRect = new Rect(rowRect.x, rowRect.y, Math.Max(40f, rowRect.width - resetWidth - 6f), rowRect.height);
            Rect resetRect = new Rect(toggleRect.xMax + 6f, rowRect.y, resetWidth, rowRect.height);
            bool newValue = DrawMainGameCheckbox(toggleRect, oldValue, LC(name));
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && detail.RevertValue != null;
            if (GUI.Button(resetRect, GUIContent.none, CreateMainGameResetButtonStyle()))
            {
                newValue = CharaDetailDefine.ParseBool(detail.RevertValue);
            }
            GUI.enabled = oldEnabled;
            if (newValue != oldValue)
            {
                detail.DetailDefine.Set(character, newValue);
                if (detail.DetailDefine.Upd != null && !LaterUpdate)
                {
                    detail.DetailDefine.Upd(character);
                }
                accessoryMultiAdjust(character, name, detail, newValue);
            }
        }

        private void guiRenderMainGameValueInput(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            float oldValue = (float)detail.DetailDefine.Get(character);
            float newValue = oldValue;
            bool precise = StudioCharaEditor.PreciseInputMode.Value;
            CharaValueDetailDefine definition = (CharaValueDetailDefine)detail.DetailDefine;
            float smallStep = precise ? definition.DimStep1 / 10f : definition.DimStep1;
            float largeStep = precise ? definition.DimStep2 / 10f : definition.DimStep2;
            string valueText = precise
                ? oldValue.ToString("F5", CultureInfo.InvariantCulture)
                : oldValue.ToString("F3", CultureInfo.InvariantCulture);

            Rect valueRow = GetMainGameRightRowRect(42f);
            float resetWidth = Mathf.Clamp(valueRow.width * 0.09f, 30f, 38f);
            float labelWidth = Mathf.Clamp(valueRow.width * 0.40f, 96f, 190f);
            Rect labelRect = new Rect(valueRow.x, valueRow.y, labelWidth, valueRow.height);
            Rect resetRect = new Rect(valueRow.xMax - resetWidth, valueRow.y + 2f, resetWidth, 38f);
            Rect fieldRect = new Rect(
                labelRect.xMax + 6f,
                valueRow.y + 2f,
                Math.Max(44f, resetRect.x - labelRect.xMax - 12f),
                38f);
            DrawMainGameFittedLabel(labelRect, LC(name), GUI.skin.label);
            string enteredText = GUI.TextField(fieldRect, valueText);
            if (!string.Equals(enteredText, valueText, StringComparison.Ordinal) &&
                float.TryParse(
                    enteredText.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedValue))
            {
                newValue = parsedValue;
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && detail.RevertValue != null;
            if (GUI.Button(resetRect, GUIContent.none, CreateMainGameResetButtonStyle()))
            {
                newValue = (float)detail.RevertValue;
            }
            GUI.enabled = oldEnabled;

            bool canUseSingleSlotOperations = accSlotMultiSelection.Count <= 1;
            bool hasDefault = canUseSingleSlotOperations && !float.IsNaN(definition.DefValue);
            int buttonCount = 4 + (hasDefault ? 1 : 0) + (canUseSingleSlotOperations ? 1 : 0);
            Rect buttonsRow = GetMainGameRightRowRect(34f);
            float buttonGap = 4f;
            float buttonWidth = Math.Max(30f, (buttonsRow.width - buttonGap * (buttonCount - 1)) / buttonCount);
            int buttonIndex = 0;
            Rect NextButtonRect()
            {
                Rect rect = new Rect(
                    buttonsRow.x + buttonIndex * (buttonWidth + buttonGap),
                    buttonsRow.y,
                    buttonWidth,
                    buttonsRow.height);
                buttonIndex++;
                return rect;
            }
            if (GUI.RepeatButton(NextButtonRect(), precise ? "-0.1" : "-10")) newValue -= largeStep;
            if (GUI.RepeatButton(NextButtonRect(), precise ? "-0.01" : "-1")) newValue -= smallStep;
            if (GUI.RepeatButton(NextButtonRect(), precise ? "+0.01" : "+1")) newValue += smallStep;
            if (GUI.RepeatButton(NextButtonRect(), precise ? "+0.1" : "+10")) newValue += largeStep;
            if (hasDefault && GUI.Button(NextButtonRect(), "Default")) newValue = definition.DefValue;
            if (canUseSingleSlotOperations && GUI.Button(NextButtonRect(), "INV")) newValue = -newValue;

            if (newValue != oldValue)
            {
                if (definition.LoopValue && !float.IsNaN(definition.MinValue) && !float.IsNaN(definition.MaxValue))
                {
                    while (newValue < definition.MinValue)
                    {
                        newValue = definition.MaxValue - (definition.MinValue - newValue);
                    }
                    while (newValue > definition.MaxValue)
                    {
                        newValue = definition.MinValue + (newValue - definition.MaxValue);
                    }
                }
                else
                {
                    if (!float.IsNaN(definition.MinValue)) newValue = Math.Max(newValue, definition.MinValue);
                    if (!float.IsNaN(definition.MaxValue)) newValue = Math.Min(newValue, definition.MaxValue);
                }
                detail.DetailDefine.Set(character, newValue);
                if (detail.DetailDefine.Upd != null && !LaterUpdate)
                {
                    detail.DetailDefine.Upd(character);
                }
                accessoryMultiAdjust(character, name, detail, newValue - oldValue, true);
            }
        }

        private void guiRenderMainGameIntStatus(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            int oldValue = Convert.ToInt32(detail.DetailDefine.Get(character));
            int newValue = oldValue;
            CharaIntStatusDetailDefine definition = (CharaIntStatusDetailDefine)detail.DetailDefine;
            Rect headerRect = GetMainGameRightRowRect(38f);
            float resetWidth = Mathf.Clamp(headerRect.width * 0.09f, 30f, 38f);
            GUI.Label(new Rect(headerRect.x, headerRect.y, headerRect.width - resetWidth - 6f, headerRect.height), LC(name));
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && detail.RevertValue != null;
            if (GUI.Button(
                    new Rect(headerRect.xMax - resetWidth, headerRect.y, resetWidth, headerRect.height),
                    GUIContent.none,
                    CreateMainGameResetButtonStyle()))
            {
                newValue = Convert.ToInt32(detail.RevertValue);
            }
            GUI.enabled = oldEnabled;

            const int columns = 3;
            for (int startIndex = 0; startIndex < definition.IntStatus.Length; startIndex += columns)
            {
                int count = Math.Min(columns, definition.IntStatus.Length - startIndex);
                Rect rowRect = GetMainGameRightRowRect(36f);
                float gap = 4f;
                float width = (rowRect.width - gap * (count - 1)) / count;
                for (int column = 0; column < count; column++)
                {
                    int index = startIndex + column;
                    Rect buttonRect = new Rect(rowRect.x + column * (width + gap), rowRect.y, width, rowRect.height);
                    GUIStyle style = oldValue == definition.IntStatus[index]
                        ? theme.MainGameTabSelectedStyle
                        : theme.MainGameTabStyle;
                    if (GUI.Button(buttonRect, LC(definition.IntStatusName[index]), style))
                    {
                        newValue = definition.IntStatus[index];
                    }
                }
            }
            if (newValue != oldValue)
            {
                detail.DetailDefine.Set(character, newValue);
                if (detail.DetailDefine.Upd != null && !LaterUpdate)
                {
                    detail.DetailDefine.Upd(character);
                }
            }
        }

        private void guiRenderMainGameButton(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            if (DrawMainGameFullWidthButton(LC(name)) && detail.DetailDefine.Upd != null)
            {
                detail.DetailDefine.Upd(character);
                accessoryMultiAdjust(character, name, detail, null);
            }
        }

        private void guiRenderMainGameHairBundle(
            ChaControl character,
            string setKey,
            CharaDetailInfo detail)
        {
            HairBundleDetailSet.PartsNo = Array.IndexOf(HairSetKeys, setKey);
            if (HairBundleDetailSet.PartsNo < 0)
            {
                return;
            }
            Dictionary<int, float[]> bundleValues =
                (Dictionary<int, float[]>)detail.DetailDefine.Get(character);
            if (bundleValues == null)
            {
                return;
            }
            Dictionary<int, float[]> revertValues =
                detail.RevertValue as Dictionary<int, float[]>;
            foreach (int bundleKey in bundleValues.Keys)
            {
                HairBundleDetailSet.BundleKey = bundleKey;
                string bundleName = string.Format("Bundle {0} Adjust", bundleKey);
                float[] bundleRevert = revertValues != null && revertValues.ContainsKey(bundleKey)
                    ? revertValues[bundleKey]
                    : null;
                foreach (CharaHairBundleDetailDefine definition in HairBundleDetailSet.Details)
                {
                    CharaDetailInfo bundleDetail = new CharaDetailInfo(character, definition);
                    switch (definition.Type)
                    {
                        case CharaDetailDefine.CharaDetailDefineType.SEPERATOR:
                            guiRenderMainGameSeparator(character, bundleName, bundleDetail);
                            break;
                        case CharaDetailDefine.CharaDetailDefineType.TOGGLE:
                            bundleDetail.RevertValue = bundleRevert != null
                                ? definition.GetRevertValue(bundleRevert)
                                : null;
                            guiRenderMainGameToggle(character, definition.Key, bundleDetail);
                            break;
                        case CharaDetailDefine.CharaDetailDefineType.SLIDER:
                            bundleDetail.RevertValue = bundleRevert != null
                                ? definition.GetRevertValue(bundleRevert)
                                : null;
                            guiRenderMainGameSlider(character, definition.Key, bundleDetail);
                            break;
                    }
                }
            }
        }

        private int DrawMainGameChoiceRow(string label, string[] choices, int selectedIndex)
        {
            Rect rowRect = GetMainGameRightRowRect(38f);
            float gap = 4f;
            float labelWidth = Mathf.Clamp(rowRect.width * 0.25f, 72f, 105f);
            GUI.Label(new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height), LC(label));
            int choiceCount = Math.Max(1, choices.Length);
            float choicesWidth = Math.Max(40f, rowRect.width - labelWidth - gap);
            float buttonWidth = Math.Max(24f, (choicesWidth - gap * (choiceCount - 1)) / choiceCount);
            for (int index = 0; index < choices.Length; index++)
            {
                Rect buttonRect = new Rect(
                    rowRect.x + labelWidth + gap + index * (buttonWidth + gap),
                    rowRect.y,
                    buttonWidth,
                    rowRect.height);
                GUIStyle style = index == selectedIndex
                    ? theme.MainGameTabSelectedStyle
                    : theme.MainGameTabStyle;
                if (GUI.Button(buttonRect, LC(choices[index]), style))
                {
                    selectedIndex = index;
                }
            }
            return selectedIndex;
        }

        private float DrawMainGameRawSliderRow(
            string name,
            float oldValue,
            float minimum,
            float maximum,
            float? revertValue,
            bool accentLabel = false,
            bool displayPercent = true)
        {
            bool precise = StudioCharaEditor.PreciseInputMode.Value;
            bool unlimited = StudioCharaEditor.UnlimitedSlider.Value;
            float newValue = oldValue;
            if (unlimited)
            {
                minimum = Math.Min(minimum, newValue);
                maximum = Math.Max(maximum, newValue);
            }
            string valueText = precise
                ? string.Format(CultureInfo.InvariantCulture, "{0:F3}", displayPercent ? oldValue * 100f : oldValue)
                : displayPercent
                    ? string.Format(CultureInfo.InvariantCulture, "{0:F0}", oldValue * 100f)
                    : oldValue.ToString("0.###", CultureInfo.InvariantCulture);
            Rect rowRect = GetMainGameRightRowRect(44f);
            float gap = Mathf.Clamp(rowRect.width * 0.012f, 3f, 6f);
            float resetWidth = Mathf.Clamp(rowRect.width * 0.105f, 34f, 43f);
            float inputWidth = precise
                ? Mathf.Clamp(rowRect.width * 0.18f, 58f, 72f)
                : Mathf.Clamp(rowRect.width * 0.15f, 48f, 58f);
            float labelWidth = Mathf.Clamp(rowRect.width * 0.40f, 96f, 190f);
            float sliderWidth = rowRect.width - labelWidth - inputWidth - resetWidth - gap * 3f;
            if (sliderWidth < 64f)
            {
                float reduction = Math.Min(64f - sliderWidth, Math.Max(0f, labelWidth - 76f));
                labelWidth -= reduction;
                sliderWidth += reduction;
            }
            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect sliderRect = new Rect(labelRect.xMax + gap, rowRect.y + 16f, Math.Max(40f, sliderWidth), 12f);
            Rect valueRect = new Rect(sliderRect.xMax + gap, rowRect.y + 1f, inputWidth, 42f);
            Rect resetRect = new Rect(valueRect.xMax + gap, rowRect.y + 1f, resetWidth, 42f);
            EnsureMainGameSliderStyleCache();
            GUIStyle labelStyle = accentLabel
                ? mainGameSliderAccentLabelStyle
                : mainGameSliderLabelStyle;
            DrawMainGameFittedLabel(labelRect, LC(name), labelStyle);
            newValue = DrawMainGameSliderControl(
                sliderRect,
                newValue,
                minimum,
                maximum);
            GUIStyle valueStyle = precise
                ? mainGameSliderPreciseValueStyle
                : mainGameSliderValueStyle;
            string enteredText = GUI.TextField(valueRect, valueText, valueStyle);
            if (!string.Equals(enteredText, valueText, StringComparison.Ordinal) &&
                float.TryParse(
                    enteredText.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedValue))
            {
                newValue = displayPercent ? parsedValue / 100f : parsedValue;
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && revertValue.HasValue;
            if (GUI.Button(resetRect, GUIContent.none, CreateMainGameResetButtonStyle()) && revertValue.HasValue)
            {
                newValue = revertValue.Value;
            }
            GUI.enabled = oldEnabled;
            return unlimited ? newValue : Mathf.Clamp(newValue, minimum, maximum);
        }

        private float DrawMainGameSliderControl(
            Rect trackRect,
            float value,
            float minimum,
            float maximum)
        {
            const float thumbSize = 22f;
            Rect inputRect = new Rect(
                trackRect.x,
                trackRect.center.y - thumbSize * 0.5f,
                trackRect.width,
                thumbSize);
            Rect valueRect = new Rect(
                trackRect.x + thumbSize * 0.5f,
                trackRect.y,
                Math.Max(1f, trackRect.width - thumbSize),
                trackRect.height);
            int controlId = GUIUtility.GetControlID(
                "StudioCharaEditorMainGameSlider".GetHashCode(),
                FocusType.Passive,
                inputRect);
            Event evt = Event.current;
            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (GUI.enabled && evt.button == 0 && inputRect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        GUIUtility.keyboardControl = 0;
                        value = ValueFromSliderMouse(valueRect, minimum, maximum, evt.mousePosition.x);
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        value = ValueFromSliderMouse(valueRect, minimum, maximum, evt.mousePosition.x);
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
            }

            if (Event.current.type == EventType.Repaint)
            {
                if (theme.MainGameSliderTrackTexture != null)
                {
                    GUI.DrawTexture(
                        trackRect,
                        theme.MainGameSliderTrackTexture,
                        ScaleMode.StretchToFill,
                        true);
                }
                else
                {
                    GUI.Box(trackRect, GUIContent.none, GUI.skin.horizontalSlider);
                }

                float normalized = Mathf.InverseLerp(minimum, maximum, value);
                float thumbCenterX = Mathf.Lerp(valueRect.xMin, valueRect.xMax, normalized);
                Rect thumbRect = new Rect(
                    thumbCenterX - thumbSize * 0.5f,
                    trackRect.center.y - thumbSize * 0.5f,
                    thumbSize,
                    thumbSize);
                GUI.Box(thumbRect, GUIContent.none, GUI.skin.horizontalSliderThumb);
            }
            return value;
        }

        private void guiRenderMainGameABMXSet(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            object dataSet = detail.DetailDefine.Get(character);
            float[] workSet;
            float[] workRevert = null;
            DrawMainGameDivider();
            string cleanName = name.StartsWith("ABMX ", StringComparison.Ordinal)
                ? name.Substring(5)
                : name;
            if (detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET1)
            {
                workSet = (float[])dataSet;
                workRevert = detail.RevertValue as float[];
            }
            else if (detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET2)
            {
                CharaABMXDetailDefine2 definition = (CharaABMXDetailDefine2)detail.DetailDefine;
                definition.curTargetIndex = DrawMainGameChoiceRow(
                    "Side to edit",
                    definition.targetNames,
                    definition.curTargetIndex);
                int targetIndex = definition.curTargetIndex == 0 ? 0 : definition.curTargetIndex - 1;
                workSet = ((float[][])dataSet)[targetIndex];
                float[][] revertData = detail.RevertValue as float[][];
                if (revertData != null && targetIndex < revertData.Length)
                {
                    workRevert = revertData[targetIndex];
                }
            }
            else
            {
                CharaABMXDetailDefine3 definition = (CharaABMXDetailDefine3)detail.DetailDefine;
                definition.curTargetIndex = DrawMainGameChoiceRow(
                    "Hand",
                    definition.targetNames,
                    definition.curTargetIndex);
                definition.curFingerIndex = DrawMainGameChoiceRow(
                    "Finger",
                    definition.fingerNames,
                    definition.curFingerIndex);
                definition.curSegmentIndex = DrawMainGameChoiceRow(
                    "Segment",
                    definition.segmentNames,
                    definition.curSegmentIndex);
                int targetIndex = definition.curTargetIndex == 0 ? 0 : definition.curTargetIndex - 1;
                int fingerIndex = definition.curFingerIndex == 0 ? 0 : definition.curFingerIndex - 1;
                workSet = ((float[][][][])dataSet)[targetIndex][fingerIndex][definition.curSegmentIndex];
                float[][][][] revertData = detail.RevertValue as float[][][][];
                if (revertData != null)
                {
                    workRevert = revertData[targetIndex][fingerIndex][definition.curSegmentIndex];
                }
            }

            CharaABMXDetailDefine1 sliderDefinition = (CharaABMXDetailDefine1)detail.DetailDefine;
            bool splitXyz = IsMainGameSplitXyzEnabled();
            bool changed = false;
            if (!splitXyz)
            {
                int scaleCount = 0;
                float scaleValueTotal = 0f;
                float revertValueTotal = 0f;
                bool hasRevert = workRevert != null;
                for (int sliderIndex = 0;
                     sliderIndex < workSet.Length && sliderIndex < sliderDefinition.SubSliderTargets.Length;
                     sliderIndex++)
                {
                    if ((sliderDefinition.SubSliderTargets[sliderIndex] &
                         CharaABMXDetailDefine1.ABMXSliderTarget.Scale) != 0)
                    {
                        scaleCount++;
                        scaleValueTotal += workSet[sliderIndex];
                        if (workRevert != null && sliderIndex < workRevert.Length)
                        {
                            revertValueTotal += workRevert[sliderIndex];
                        }
                        else
                        {
                            hasRevert = false;
                        }
                    }
                }

                if (scaleCount > 0)
                {
                    float oldValue = scaleValueTotal / scaleCount;
                    float? revertValue = hasRevert
                        ? (float?)(revertValueTotal / scaleCount)
                        : null;
                    float newValue = DrawMainGameRawSliderRow(
                        cleanName + " Scale",
                        oldValue,
                        0f,
                        2f,
                        revertValue,
                        true);
                    if (!Mathf.Approximately(newValue, oldValue))
                    {
                        for (int sliderIndex = 0;
                             sliderIndex < workSet.Length && sliderIndex < sliderDefinition.SubSliderTargets.Length;
                             sliderIndex++)
                        {
                            if ((sliderDefinition.SubSliderTargets[sliderIndex] &
                                 CharaABMXDetailDefine1.ABMXSliderTarget.Scale) != 0)
                            {
                                SetMainGameAbmxSliderValue(
                                    dataSet,
                                    detail,
                                    workSet,
                                    sliderIndex,
                                    newValue);
                            }
                        }
                        changed = true;
                    }
                }

                for (int sliderIndex = 0; sliderIndex < workSet.Length; sliderIndex++)
                {
                    if (sliderIndex < sliderDefinition.SubSliderTargets.Length &&
                        (sliderDefinition.SubSliderTargets[sliderIndex] &
                         CharaABMXDetailDefine1.ABMXSliderTarget.Scale) != 0)
                    {
                        continue;
                    }
                    changed |= DrawMainGameAbmxSlider(
                        dataSet,
                        detail,
                        workSet,
                        workRevert,
                        sliderIndex,
                        cleanName + " " + sliderDefinition.SubSlidersNames[sliderIndex]);
                }
            }
            else
            {
                for (int sliderIndex = 0; sliderIndex < workSet.Length; sliderIndex++)
                {
                    changed |= DrawMainGameAbmxSlider(
                        dataSet,
                        detail,
                        workSet,
                        workRevert,
                        sliderIndex,
                        cleanName + " " + sliderDefinition.SubSlidersNames[sliderIndex]);
                }
            }

            if (changed)
            {
                detail.DetailDefine.Set(character, dataSet);
            }
        }

        private bool DrawMainGameAbmxSlider(
            object dataSet,
            CharaDetailInfo detail,
            float[] workSet,
            float[] workRevert,
            int sliderIndex,
            string label)
        {
            float oldValue = workSet[sliderIndex];
            float? revertValue = workRevert != null && sliderIndex < workRevert.Length
                ? (float?)workRevert[sliderIndex]
                : null;
            float newValue = DrawMainGameRawSliderRow(
                label,
                oldValue,
                0f,
                2f,
                revertValue,
                true);
            if (Mathf.Approximately(newValue, oldValue))
            {
                return false;
            }
            SetMainGameAbmxSliderValue(dataSet, detail, workSet, sliderIndex, newValue);
            return true;
        }

        private bool IsMainGameSplitXyzEnabled()
        {
            if (mainGameXyzScaleEntry == null)
            {
                TryGetExternalBoolConfig(
                    "KKABMX.Core",
                    "Maker",
                    "Use XYZ scale sliders",
                    out mainGameXyzScaleEntry);
            }
            return mainGameXyzScaleEntry?.Value ?? false;
        }

        private static void SetMainGameAbmxSliderValue(
            object dataSet,
            CharaDetailInfo detail,
            float[] workSet,
            int sliderIndex,
            float newValue)
        {
            workSet[sliderIndex] = newValue;
            if (detail.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET2 &&
                ((CharaABMXDetailDefine2)detail.DetailDefine).curTargetIndex == 0)
            {
                ((float[][])dataSet)[1][sliderIndex] = newValue;
            }
            if (detail.DetailDefine.Type != CharaDetailDefine.CharaDetailDefineType.ABMXSET3)
            {
                return;
            }

            CharaABMXDetailDefine3 definition = (CharaABMXDetailDefine3)detail.DetailDefine;
            if (definition.curTargetIndex == 0)
            {
                ((float[][][][])dataSet)[1]
                    [definition.curFingerIndex == 0 ? 0 : definition.curFingerIndex - 1]
                    [definition.curSegmentIndex]
                    [sliderIndex] = newValue;
            }
            if (definition.curFingerIndex == 0)
            {
                for (int hand = 0; hand < 2; hand++)
                {
                    if (definition.curTargetIndex != 0 && definition.curTargetIndex - 1 != hand)
                    {
                        continue;
                    }
                    for (int finger = 1; finger < 5; finger++)
                    {
                        ((float[][][][])dataSet)[hand]
                            [finger]
                            [definition.curSegmentIndex]
                            [sliderIndex] = newValue;
                    }
                }
            }
        }

        private void guiRenderMainGameSeparator(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            DrawMainGameDivider();
            string title = detail.DetailDefine.Get != null
                ? LC((string)detail.DetailDefine.Get(character))
                : LC(name);
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fixedHeight = 44f,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip
            };
            style.padding = new RectOffset(2, 2, 3, 6);
            GUILayout.Label(title, style, GUILayout.Height(44f), GUILayout.ExpandWidth(true));
        }

        private void guiRenderMainGameSlider(
            ChaControl character,
            string name,
            CharaDetailInfo detail)
        {
            float oldValue = (float)detail.DetailDefine.Get(character);
            float newValue = oldValue;
            bool precise = StudioCharaEditor.PreciseInputMode.Value;
            bool unlimited = StudioCharaEditor.UnlimitedSlider.Value;
            CharaSliderDetailDefine definition = detail.DetailDefine as CharaSliderDetailDefine;
            float minimum = definition?.MinValue ?? -1f;
            float maximum = definition?.MaxValue ?? 2f;
            string valueText = precise
                ? string.Format("{0:F3}", oldValue * 100f)
                : string.Format("{0:F0}", oldValue * 100f);

            if (unlimited)
            {
                maximum = Math.Max(maximum, newValue);
                minimum = Math.Min(minimum, newValue);
            }

            EnsureMainGameSliderStyleCache();
            GUIStyle labelStyle = mainGameSliderLabelStyle;
            GUIStyle valueStyle = precise
                ? mainGameSliderPreciseValueStyle
                : mainGameSliderValueStyle;
            GUIStyle resetStyle = mainGameResetButtonStyle;

            Rect rowRect = GetMainGameRightRowRect(44f);
            float gap = Mathf.Clamp(rowRect.width * 0.012f, 3f, 6f);
            float resetWidth = Mathf.Clamp(rowRect.width * 0.105f, 34f, 43f);
            float inputWidth = precise
                ? Mathf.Clamp(rowRect.width * 0.18f, 58f, 72f)
                : Mathf.Clamp(rowRect.width * 0.15f, 48f, 58f);
            float labelWidth = Mathf.Clamp(rowRect.width * 0.40f, 96f, 190f);
            float sliderWidth = rowRect.width - labelWidth - inputWidth - resetWidth - gap * 3f;
            if (sliderWidth < 64f)
            {
                float missingWidth = 64f - sliderWidth;
                float labelReduction = Math.Min(missingWidth, Math.Max(0f, labelWidth - 76f));
                labelWidth -= labelReduction;
                sliderWidth += labelReduction;
            }

            Rect labelRect = new Rect(rowRect.x, rowRect.y, labelWidth, rowRect.height);
            Rect sliderRect = new Rect(labelRect.xMax + gap, rowRect.y + 16f, Math.Max(40f, sliderWidth), 12f);
            Rect valueRect = new Rect(sliderRect.xMax + gap, rowRect.y + 1f, inputWidth, 42f);
            Rect resetRect = new Rect(valueRect.xMax + gap, rowRect.y + 1f, resetWidth, 42f);
            DrawMainGameFittedLabel(labelRect, LC(name), labelStyle);
            float sliderValue = DrawMainGameSliderControl(
                sliderRect,
                newValue,
                minimum,
                maximum);
            if (!Mathf.Approximately(sliderValue, newValue))
            {
                newValue = sliderValue;
            }
            string newValueText = GUI.TextField(valueRect, valueText, valueStyle);
            if (!string.Equals(newValueText, valueText, StringComparison.Ordinal) &&
                float.TryParse(
                    newValueText.Replace(',', '.'),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float parsedValue))
            {
                newValue = parsedValue / 100f;
            }
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && detail.RevertValue != null;
            if (GUI.Button(resetRect, GUIContent.none, resetStyle))
            {
                newValue = (float)detail.RevertValue;
            }
            GUI.enabled = oldEnabled;
            GUILayout.Space(2f);

            if (!unlimited)
            {
                newValue = Mathf.Clamp(newValue, minimum, maximum);
            }
            if (!Mathf.Approximately(newValue, oldValue))
            {
                detail.DetailDefine.Set(character, newValue);
                if (detail.DetailDefine.Upd != null && !LaterUpdate)
                {
                    detail.DetailDefine.Upd(character);
                }
                accessoryMultiAdjust(character, name, detail, newValue);
            }
        }

        private string DrawMainGameDetailTabs(
            CharaEditorController controller,
            string detailSetKey,
            CharaDetailInfo[] detailSet)
        {
            string[] tabs = GetMainGameDetailTabs(controller, detailSetKey, detailSet);
            if (tabs == null || tabs.Length == 0)
            {
                return null;
            }

            if (!mainGameDetailTabPool.TryGetValue(detailSetKey, out int selectedTab))
            {
                selectedTab = 0;
            }
            selectedTab = Mathf.Clamp(selectedTab, 0, tabs.Length - 1);
            float tabWidth = Math.Min(
                90f,
                Math.Max(58f, (mainGameRightRect.width - 42f - (tabs.Length * 4f)) / tabs.Length));
            GUIStyle normalStyle = new GUIStyle(theme.MainGameTabStyle)
            {
                fixedWidth = tabWidth,
                fontSize = tabs.Length >= 5 ? 15 : theme.MainGameTabStyle.fontSize
            };
            GUIStyle selectedStyle = new GUIStyle(theme.MainGameTabSelectedStyle)
            {
                fixedWidth = tabWidth,
                fontSize = tabs.Length >= 5 ? 15 : theme.MainGameTabSelectedStyle.fontSize
            };
            GUILayout.BeginHorizontal();
            for (int index = 0; index < tabs.Length; index++)
            {
                GUIStyle style = index == selectedTab
                    ? selectedStyle
                    : normalStyle;
                if (GUILayout.Button(GetMainGameTabLabel(detailSetKey, tabs[index]), style))
                {
                    selectedTab = index;
                    mainGameDetailTabPool[detailSetKey] = index;
                    rightScroll = Vector2.zero;
                    CloseSelectorSidePanel();
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            mainGameDetailTabPool[detailSetKey] = selectedTab;
            return tabs[selectedTab];
        }

        private string[] GetMainGameDetailTabs(
            CharaEditorController controller,
            string detailSetKey,
            CharaDetailInfo[] detailSet)
        {
            string[] candidates;
            if (detailSetKey.StartsWith("Hair#", StringComparison.Ordinal))
            {
                candidates = GetMainGameHairTabs(controller, detailSetKey);
            }
            else if (detailSetKey.StartsWith("Clothes#", StringComparison.Ordinal))
            {
                candidates = new[] { "Type", "Color 01", "Color 02", "Color 03", "Other" };
            }
            else if (detailSetKey.StartsWith("Accessories#", StringComparison.Ordinal))
            {
                candidates = new[] { "Type", "Accessory Color", "Hair Color", "Base Position", "Settings" };
            }
            else
            {
                switch (detailSetKey)
                {
                    case "Body#Skin":
                        candidates = new[] { "Skin", "Build", "Color" };
                        break;
                    case "Body#Sunburn":
                    case "Body#Nip":
                    case "Body#Underhair":
                    case "Face#Bread":
                    case "Face#Eyebrow":
                    case "Face#Eyelashes":
                    case "Face#MakeupEyeshadow":
                    case "Face#MakeupCheek":
                    case "Face#MakeupLip":
                        candidates = new[] { "Type", "Color" };
                        break;
                    case "Body#Nail":
                        candidates = new[] { "Color" };
                        break;
                    case "Body#Paint1":
                    case "Body#Paint2":
                    case "Face#MakeupPaint1":
                    case "Face#MakeupPaint2":
                    case "Face#Mole":
                        candidates = new[] { "Type", "Color", "Placement" };
                        break;
                    case "Face#FaceType":
                        candidates = new[] { "Contour", "Skin", "Wrinkles" };
                        break;
                    case "Face#EyeL":
                    case "Face#EyeR":
                        candidates = new[] { "Iris Type", "Iris Settings", "Pupil Type", "Pupil Settings", "Eye Whites" };
                        break;
                    case "Face#EyeEtc":
                        candidates = new[] { "Settings" };
                        break;
                    case "Face#EyeHL":
                        candidates = new[] { "Type", "Color", "Settings" };
                        break;
                    default:
                        return null;
                }
            }

            if (candidates == null || detailSet == null)
            {
                return candidates;
            }

            List<string> visibleTabs = new List<string>();
            for (int tabIndex = 0; tabIndex < candidates.Length; tabIndex++)
            {
                string tab = candidates[tabIndex];
                for (int detailIndex = 0; detailIndex < detailSet.Length; detailIndex++)
                {
                    if (MainGameDetailBelongsToTab(detailSetKey, detailSet[detailIndex].DetailDefine.Key, tab))
                    {
                        visibleTabs.Add(tab);
                        break;
                    }
                }
            }
            return visibleTabs.ToArray();
        }

        private string[] GetMainGameHairTabs(CharaEditorController controller, string detailSetKey)
        {
            List<string> tabs = new List<string> { "Type" };
            ChaControl character = controller?.ociTarget?.charInfo;
            int hairIndex = detailSetKey == "Hair#BackHair" ? 0
                : detailSetKey == "Hair#FrontHair" ? 1
                : detailSetKey == "Hair#SideHair" ? 2
                : detailSetKey == "Hair#ExtensionHair" ? 3
                : -1;
            if (character?.cmpHair == null || hairIndex < 0 || hairIndex >= character.cmpHair.Length ||
                character.cmpHair[hairIndex] == null)
            {
                return tabs.ToArray();
            }

            var cmpHair = character.cmpHair[hairIndex];
            tabs.Add("Color");
            if (cmpHair.useAcsColor01 || cmpHair.useAcsColor02 || cmpHair.useAcsColor03)
            {
                tabs.Add("Accessories");
            }
            if (character.fileHair?.parts != null && hairIndex < character.fileHair.parts.Length &&
                character.fileHair.parts[hairIndex].dictBundle != null &&
                character.fileHair.parts[hairIndex].dictBundle.Count > 0)
            {
                tabs.Add("Adjust");
            }
            if (cmpHair.useMesh)
            {
                tabs.Add("Mesh");
            }
            return tabs.ToArray();
        }

        private static string GetMainGameTabLabel(string detailSetKey, string tab)
        {
            if (detailSetKey.StartsWith("Accessories#", StringComparison.Ordinal) &&
                (tab == "Accessory Color" || tab == "Hair Color"))
            {
                return "Color";
            }
            return tab;
        }

        private static bool MainGameDetailBelongsToTab(
            string detailSetKey,
            string detailKey,
            string selectedTab)
        {
            if (string.IsNullOrEmpty(selectedTab))
            {
                return true;
            }

            string name = GetDetailName(detailKey);
            if (detailSetKey.StartsWith("Hair#", StringComparison.Ordinal))
            {
                if (selectedTab == "Type")
                {
                    return name.EndsWith("HairType", StringComparison.Ordinal);
                }
                if (selectedTab == "Color")
                {
                    return name == "BaseColor" ||
                           name == "topColor" ||
                           name == "UnderColor" ||
                           name == "Specular" ||
                           name == "Metallic" ||
                           name == "Smoothness";
                }
                if (selectedTab == "Accessories")
                {
                    return name == "AcsColor";
                }
                if (selectedTab == "Adjust")
                {
                    return name == "Bundles";
                }
                return name.StartsWith("Mesh", StringComparison.Ordinal);
            }
            if (detailSetKey.StartsWith("Clothes#", StringComparison.Ordinal))
            {
                if (name == "Cloth Status" ||
                    name == "Restore all color setting" ||
                    name.StartsWith("Restore color ", StringComparison.Ordinal))
                {
                    return false;
                }
                bool isType = name.EndsWith(" Type", StringComparison.Ordinal);
                if (selectedTab == "Type")
                {
                    return isType;
                }
                if (selectedTab.StartsWith("Color ", StringComparison.Ordinal))
                {
                    string colorNo = selectedTab.Substring("Color ".Length).TrimStart('0');
                    return name == "Color " + colorNo + " Setting" ||
                           name == "Color " + colorNo ||
                           name == "Gloss " + colorNo ||
                           name == "Metallic " + colorNo ||
                           name == "Pattern " + colorNo ||
                           name.StartsWith("Pattern " + colorNo + " ", StringComparison.Ordinal);
                }
                bool isAnyColor = name.StartsWith("Color ", StringComparison.Ordinal) ||
                                  name.StartsWith("Gloss ", StringComparison.Ordinal) ||
                                  name.StartsWith("Metallic ", StringComparison.Ordinal) ||
                                  name.StartsWith("Pattern ", StringComparison.Ordinal);
                return !isType && !isAnyColor;
            }
            if (detailSetKey.StartsWith("Accessories#", StringComparison.Ordinal))
            {
                bool isType = name == "Acc Category" || name == "Acc ID";
                bool isAccessoryColor = name.StartsWith("Acc Color ", StringComparison.Ordinal) ||
                                        name.StartsWith("Color ", StringComparison.Ordinal) ||
                                        name.StartsWith("Gloss ", StringComparison.Ordinal) ||
                                        name.StartsWith("Metallic ", StringComparison.Ordinal) ||
                                        name.StartsWith("Restore default color ", StringComparison.Ordinal);
                bool isHairColor = name == "Acc Hair Color Setting" ||
                                   name == "BaseColor" || name == "TopColor" ||
                                   name == "UnderColor" || name == "Specular" ||
                                   name == "Metallic" || name == "Smoothness" ||
                                   name.StartsWith("Get ", StringComparison.Ordinal) ||
                                   name == "Restore all default color";
                bool isBasePosition = name == "Acc Parent";
                if (selectedTab == "Type") return isType;
                if (selectedTab == "Accessory Color") return isAccessoryColor;
                if (selectedTab == "Hair Color") return isHairColor;
                if (selectedTab == "Base Position") return isBasePosition;
                return !isType && !isAccessoryColor && !isHairColor && !isBasePosition;
            }
            if (detailSetKey == "Face#FaceType")
            {
                if (selectedTab == "Contour")
                {
                    return name == "FaceType";
                }
                if (selectedTab == "Skin")
                {
                    return name == "FaceSkinType";
                }
                return name != "FaceType" && name != "FaceSkinType";
            }
            if (detailSetKey == "Body#Skin")
            {
                if (selectedTab == "Skin")
                {
                    return name == "SkinType";
                }
                if (selectedTab == "Build")
                {
                    return name.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                return name.IndexOf("Detail", StringComparison.OrdinalIgnoreCase) < 0 && name != "SkinType";
            }
            if (detailSetKey == "Face#EyeL" || detailSetKey == "Face#EyeR")
            {
                if (selectedTab == "Iris Type") return name == "PupilType";
                if (selectedTab == "Iris Settings") return name.StartsWith("Pupil", StringComparison.Ordinal) && name != "PupilType";
                if (selectedTab == "Pupil Type") return name == "BlackType";
                if (selectedTab == "Pupil Settings") return name.StartsWith("Black", StringComparison.Ordinal) && name != "BlackType";
                return name == "WhiteColor";
            }
            if (detailSetKey == "Face#EyeHL")
            {
                if (selectedTab == "Type") return name == "EyeHLType";
                if (selectedTab == "Color") return name == "EyeHLColor";
                return name.StartsWith("HL", StringComparison.Ordinal);
            }
            if (detailSetKey == "Face#EyeEtc")
            {
                return true;
            }
            if (detailSetKey == "Body#Nail")
            {
                return true;
            }
            if (detailSetKey == "Body#Paint1" || detailSetKey == "Body#Paint2" ||
                detailSetKey == "Face#MakeupPaint1" || detailSetKey == "Face#MakeupPaint2")
            {
                if (selectedTab == "Type") return name == "PaintType";
                if (selectedTab == "Color") return name == "PaintColor" || name == "PaintGloss" || name == "PaintMetallic";
                return name != "PaintType" && name != "PaintColor" && name != "PaintGloss" && name != "PaintMetallic";
            }
            if (detailSetKey == "Face#Mole")
            {
                if (selectedTab == "Type") return name == "MoleType";
                if (selectedTab == "Color") return name == "MoleColor";
                return name != "MoleType" && name != "MoleColor";
            }
            if (detailSetKey == "Body#Sunburn" || detailSetKey == "Body#Nip" ||
                detailSetKey == "Body#Underhair" || detailSetKey == "Face#Bread" ||
                detailSetKey == "Face#Eyebrow" || detailSetKey == "Face#Eyelashes" ||
                detailSetKey == "Face#MakeupEyeshadow" || detailSetKey == "Face#MakeupCheek" ||
                detailSetKey == "Face#MakeupLip")
            {
                bool isType = name.EndsWith("Type", StringComparison.Ordinal);
                return selectedTab == "Type" ? isType : !isType;
            }
            return true;
        }

        private static bool ShouldSuppressMainGameSeparator(
            string detailSetKey,
            CharaDetailInfo detail,
            string selectedTab)
        {
            if (string.IsNullOrEmpty(selectedTab) ||
                detail.DetailDefine.Type != CharaDetailDefine.CharaDetailDefineType.SEPERATOR)
            {
                return false;
            }
            string name = GetDetailName(detail.DetailDefine.Key);
            if (detailSetKey.StartsWith("Clothes#", StringComparison.Ordinal))
            {
                return name.StartsWith("Color ", StringComparison.Ordinal) || name == "Cloth Options";
            }
            if (detailSetKey.StartsWith("Accessories#", StringComparison.Ordinal))
            {
                return name.StartsWith("Acc Color ", StringComparison.Ordinal) || name == "Acc Hair Color Setting";
            }
            return false;
        }

        private void guiRenderMainGameSelector(
            ChaControl character,
            string displayName,
            CharaDetailInfo detail)
        {
            string selectorKey = detail.DetailDefine.Key;
            string rawName = GetDetailName(selectorKey);
            if (rawName == "Acc Parent" || rawName == "Acc Category")
            {
                guiRenderSelector(character, displayName, detail);
                return;
            }

            List<CustomSelectInfo> allItems = GetSelectorList(character, detail);
            int selectedId = (int)detail.DetailDefine.Get(character);
            if (selectorKey.StartsWith("Clothes#", StringComparison.Ordinal) &&
                rawName.StartsWith("Pattern ", StringComparison.Ordinal))
            {
                DrawMainGameCollapsedPatternSelector(
                    character,
                    detail,
                    allItems,
                    selectedId);
                return;
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label(displayName, theme.MainGameBreadcrumbStyle);
            GUILayout.FlexibleSpace();
            bool oldEnabled = GUI.enabled;
            GUI.enabled = oldEnabled && allItems.Count > 0;
            if (GUILayout.Button("Random", GUILayout.Width(76f)))
            {
                CustomSelectInfo randomItem = allItems[UnityEngine.Random.Range(0, allItems.Count)];
                ChangeMainGameSelectorId(character, detail, randomItem.id, selectedId);
            }
            GUI.enabled = oldEnabled;
            if (detail.RevertValue != null && GUILayout.Button("R", GUILayout.Width(30f)))
            {
                ChangeMainGameSelectorId(character, detail, (int)detail.RevertValue, selectedId);
            }
            GUILayout.EndHorizontal();

            string searchKey = selectorKey + "|MainGameSearch";
            string searchText = searchWordPool.TryGetValue(searchKey, out string storedSearch)
                ? storedSearch
                : string.Empty;
            bool scrollToSelected = string.Equals(
                mainGameScrollToSelectorKey,
                selectorKey,
                StringComparison.Ordinal);
            if (scrollToSelected)
            {
                searchText = string.Empty;
                searchWordPool[searchKey] = string.Empty;
                mainGameScrollToSelectorKey = string.Empty;
            }
            List<CustomSelectInfo> items = allItems;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                items = new List<CustomSelectInfo>();
                for (int index = 0; index < allItems.Count; index++)
                {
                    CustomSelectInfo item = allItems[index];
                    string itemName = GetSelectorDisplayName(item);
                    if (itemName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.id.ToString().IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        items.Add(item);
                    }
                }
            }

            const float gridGap = 8f;
            float preferredCellWidth = GetSelectorGridThumbnailSize() + 18f;
            float availableGridWidth = Math.Max(preferredCellWidth, mainGameRightContentWidth - 18f);
            int columns = Math.Max(
                1,
                (int)Math.Floor(
                    (availableGridWidth + gridGap) /
                    (preferredCellWidth + gridGap)));
            float cellWidth = Math.Max(
                preferredCellWidth,
                (availableGridWidth - gridGap * (columns - 1)) / columns);
            // Keep the preview itself square. Previously the cell height was
            // capped at 124px, so wider windows only added empty side space
            // while the image stayed small.
            float cellHeight = cellWidth + 50f;
            float rowGap = gridGap;
            float rowStride = cellHeight + rowGap;
            float viewportHeight = Math.Max(220f, mainGameRightRect.height * 0.66f);
            int rowCount = Math.Max(1, (items.Count + columns - 1) / columns);
            float contentHeight = rowCount * rowStride;
            float maximumScrollY = Math.Max(0f, contentHeight - viewportHeight);
            string scrollKey = selectorKey + "|MainGameGrid";
            if (!scrollPool.TryGetValue(scrollKey, out Vector2 selectorScroll))
            {
                selectorScroll = Vector2.zero;
            }
            if (scrollToSelected)
            {
                int selectedIndex = GetSelectorIndex(
                    selectorKey,
                    allItems,
                    selectedId,
                    out string ignoredScrollSelectedName);
                if (selectedIndex >= 0)
                {
                    int selectedRow = selectedIndex / columns;
                    float centeredScrollY =
                        selectedRow * rowStride -
                        (viewportHeight - cellHeight) * 0.5f;
                    selectorScroll.y = Mathf.Clamp(
                        centeredScrollY,
                        0f,
                        maximumScrollY);
                }
            }
            selectorScroll.x = 0f;
            selectorScroll.y = Mathf.Clamp(selectorScroll.y, 0f, maximumScrollY);
            if (!mainGameSelectorVisibleThisFrame)
            {
                mainGameSelectorVisibleThisFrame = true;
                mainGameVisibleSelectorKey = selectorKey;
            }
            Vector2 oldScroll = selectorScroll;
            // Every thumbnail grid benefits from a dependable minimum thumb
            // size. Hair Mesh selectors are the sole exception: Unity loses
            // the end of their nested scroll range when the thumb is fixed.
            bool useLargePreviewThumb = !rawName.StartsWith(
                "Mesh",
                StringComparison.OrdinalIgnoreCase);
            float previousThumbHeight = useLargePreviewThumb
                ? BeginMainGameLargePreviewScrollbarThumb()
                : -1f;
            selectorScroll = GUILayout.BeginScrollView(
                selectorScroll,
                false,
                false,
                GUIStyle.none,
                GUI.skin.verticalScrollbar,
                GUIStyle.none,
                GUILayout.Height(viewportHeight),
                GUILayout.ExpandWidth(true));
            selectorScroll.x = 0f;
            selectorScroll.y = Mathf.Clamp(selectorScroll.y, 0f, maximumScrollY);

            int firstVisibleRow = Mathf.Clamp(Mathf.FloorToInt(selectorScroll.y / rowStride) - 1, 0, rowCount - 1);
            int lastVisibleRow = Mathf.Clamp(
                Mathf.CeilToInt((selectorScroll.y + viewportHeight) / rowStride) + 1,
                firstVisibleRow,
                rowCount - 1);
            if (firstVisibleRow > 0)
            {
                GUILayout.Space(firstVisibleRow * rowStride);
            }
            for (int row = firstVisibleRow; row <= lastVisibleRow; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < columns; column++)
                {
                    if (column > 0)
                    {
                        GUILayout.Space(gridGap);
                    }
                    int itemIndex = row * columns + column;
                    if (itemIndex < items.Count)
                    {
                        DrawMainGameSelectorCell(character, detail, items[itemIndex], selectedId, cellWidth, cellHeight);
                    }
                    else
                    {
                        GUILayout.Space(cellWidth);
                    }
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(rowGap);
            }
            int trailingRows = rowCount - lastVisibleRow - 1;
            if (trailingRows > 0)
            {
                GUILayout.Space(trailingRows * rowStride);
            }
            GUILayout.EndScrollView();
            if (useLargePreviewThumb)
            {
                EndMainGameLargePreviewScrollbarThumb(previousThumbHeight);
            }
            scrollPool[scrollKey] = selectorScroll;
            TrackSelectorScroll(oldScroll, selectorScroll);

            GUILayout.BeginHorizontal();
            Rect searchRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUI.skin.textField,
                GUILayout.Height(28f),
                GUILayout.ExpandWidth(true));
            GUI.SetNextControlName(searchKey);
            string newSearch = GUI.TextField(searchRect, searchText);
            if (string.IsNullOrEmpty(searchText) && GUI.GetNameOfFocusedControl() != searchKey)
            {
                GUI.Label(searchRect, "Search", GetSelectorGridLabelStyle());
            }
            if (newSearch != searchText)
            {
                searchWordPool[searchKey] = newSearch;
                scrollPool[scrollKey] = Vector2.zero;
            }
            if (GUILayout.Button("Reset", GUILayout.Width(62f)))
            {
                searchWordPool[searchKey] = string.Empty;
                scrollPool[scrollKey] = Vector2.zero;
            }
            GUILayout.EndHorizontal();

            int selectedItemIndex = GetSelectorIndex(
                selectorKey,
                allItems,
                selectedId,
                out string ignoredSelectedName);
            CustomSelectInfo selectedItem = selectedItemIndex >= 0 && selectedItemIndex < allItems.Count
                ? allItems[selectedItemIndex]
                : null;
            string sourceName = selectedItem == null
                ? "Unknown"
                : PluginSideloaderSource.GetZipmodFileName(selectedItem) ?? "Base game";
            GUILayout.Label(
                new GUIContent("ZIPMOD: " + sourceName, sourceName),
                GetMainGameSelectorSourceLabelStyle(),
                GUILayout.Height(20f));
        }

        private void DrawMainGameCollapsedPatternSelector(
            ChaControl character,
            CharaDetailInfo detail,
            List<CustomSelectInfo> items,
            int selectedId)
        {
            string selectorKey = detail.DetailDefine.Key;
            int selectedIndex = GetSelectorIndex(
                selectorKey,
                items,
                selectedId,
                out string selectedName);
            CustomSelectInfo selected = selectedIndex >= 0 && selectedIndex < items.Count
                ? items[selectedIndex]
                : null;

            EnsureMainGameSliderStyleCache();
            GUIStyle patternHeaderStyle = new GUIStyle(mainGameSliderAccentLabelStyle)
            {
                fontSize = 22,
                fixedHeight = 36f
            };
            GUILayout.Label("Pattern Setting", patternHeaderStyle, GUILayout.Height(36f));
            Rect rowRect = GetMainGameRightRowRect(72f);
            GUI.Label(
                new Rect(rowRect.x, rowRect.y, Math.Min(120f, rowRect.width * 0.30f), rowRect.height),
                "Type",
                mainGameSliderLabelStyle ?? GUI.skin.label);
            float previewSize = 58f;
            Rect previewRect = new Rect(
                rowRect.xMax - previewSize - 2f,
                rowRect.y + 5f,
                previewSize,
                previewSize);
            Texture2D texture = selected == null
                ? Texture2D.blackTexture
                : GetSelectorThumbTexture(detail.DetailDefine.Key, selected);
            if (GUI.Button(previewRect, texture, GUI.skin.button))
            {
                if (IsSelectorSidePanelOpen(selectorKey))
                {
                    CloseSelectorSidePanel();
                }
                else
                {
                    OpenSelectorSidePanel(
                        character,
                        GetMainGameDetailName(selectorKey),
                        detail,
                        selectedIndex,
                        true,
                        GetSelectorGridThumbnailSize() + 50f);
                }
            }
            GUIStyle captionStyle = new GUIStyle(GetMainGameSelectorItemLabelStyle())
            {
                alignment = TextAnchor.LowerCenter,
                fontSize = 11
            };
            GUI.Label(
                new Rect(previewRect.x - 10f, previewRect.yMax - 18f, previewRect.width + 20f, 18f),
                string.IsNullOrEmpty(selectedName) ? "None" : selectedName,
                captionStyle);
        }

        private void DrawMainGameSelectorCell(
            ChaControl character,
            CharaDetailInfo detail,
            CustomSelectInfo item,
            int selectedId,
            float width,
            float height)
        {
            Rect cellRect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            bool selected = item.id == selectedId;
            bool hover = cellRect.Contains(Event.current.mousePosition);
            GUI.Box(cellRect, GUIContent.none, GUI.skin.box);

            Texture2D thumbnail = GetSelectorThumbTexture(detail.DetailDefine.Key, item) ?? Texture2D.blackTexture;
            float thumbnailSize = Math.Max(20f, cellRect.width - 4f);
            Rect thumbnailRect = new Rect(
                cellRect.x + 2f,
                cellRect.y + 2f,
                thumbnailSize,
                thumbnailSize);
            GUI.DrawTexture(thumbnailRect, thumbnail, ScaleMode.ScaleToFit, true);
            Rect labelRect = new Rect(
                cellRect.x + 4f,
                thumbnailRect.yMax + 2f,
                cellRect.width - 8f,
                Math.Max(42f, cellRect.yMax - thumbnailRect.yMax - 4f));
            GUI.Label(
                labelRect,
                GetSelectorDisplayName(item),
                GetMainGameSelectorItemLabelStyle());
            if (selected)
            {
                DrawMainGameSelectorOutline(
                    cellRect,
                    new Color(0.82f, 0.84f, 0.18f, 0.92f));
            }
            else if (hover)
            {
                DrawMainGameSelectorOutline(
                    cellRect,
                    new Color(0.65f, 0.75f, 0.45f, 0.65f));
            }

            if (GUI.enabled && Event.current.type == EventType.MouseDown && Event.current.button == 0 && cellRect.Contains(Event.current.mousePosition))
            {
                ChangeMainGameSelectorId(character, detail, item.id, selectedId);
                Event.current.Use();
            }
        }

        private GUIStyle GetMainGameSelectorItemLabelStyle()
        {
            if (mainGameSelectorStyleTheme != theme || mainGameSelectorItemLabelStyle == null)
            {
                mainGameSelectorStyleTheme = theme;
                mainGameSelectorItemLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperCenter,
                    wordWrap = true,
                    clipping = TextClipping.Clip,
                    fontSize = 15,
                    padding = new RectOffset(3, 3, 1, 1),
                    margin = new RectOffset(0, 0, 0, 0)
                };
            }
            return mainGameSelectorItemLabelStyle;
        }

        private GUIStyle GetMainGameSelectorSourceLabelStyle()
        {
            if (mainGameSelectorSourceStyleTheme != theme || mainGameSelectorSourceLabelStyle == null)
            {
                mainGameSelectorSourceStyleTheme = theme;
                mainGameSelectorSourceLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    fontSize = 13,
                    fixedHeight = 20f,
                    padding = new RectOffset(1, 1, 0, 1),
                    margin = new RectOffset(0, 0, 0, 0)
                };
                Color sourceColor = new Color(0.68f, 0.73f, 0.74f, 1f);
                mainGameSelectorSourceLabelStyle.normal.textColor = sourceColor;
                mainGameSelectorSourceLabelStyle.hover.textColor = sourceColor;
                mainGameSelectorSourceLabelStyle.active.textColor = sourceColor;
                mainGameSelectorSourceLabelStyle.focused.textColor = sourceColor;
            }
            return mainGameSelectorSourceLabelStyle;
        }

        private static void DrawMainGameSelectorOutline(
            Rect rect,
            Color color,
            float thickness = 1f)
        {
            thickness = Mathf.Max(1f, thickness);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void ChangeMainGameSelectorId(
            ChaControl character,
            CharaDetailInfo detail,
            int newId,
            int oldId)
        {
            if (newId == oldId)
            {
                return;
            }
            detail.DetailDefine.Set(character, newId);
            ClearSelectorCache();
            if (detail.DetailDefine.Upd != null && !LaterUpdate)
            {
                detail.DetailDefine.Upd(character);
            }
        }

        private void DrawMainGamePasteSlotPrompt(CharaEditorController controller)
        {
            string GetNewEmptySlot(List<string> selectedKeys, List<string> registeredKeys)
            {
                int slot = int.Parse(selectedKeys[0]);
                for (;; slot++)
                {
                    string key = slot.ToString();
                    if (selectedKeys.Contains(key) || registeredKeys.Contains(key))
                    {
                        continue;
                    }
                    AccessoryInfo accessory = controller.GetAccessoryInfoByKey(key);
                    if (accessory == null || accessory.IsEmptySlot)
                    {
                        return key;
                    }
                }
            }

            List<string> targetKeys = new List<string>();
            for (int index = 0; index < accSlotClipboard.Count; index++)
            {
                if (index < accSlotMultiSelection.Count)
                {
                    AccessoryInfo accessory = controller.GetAccessoryInfoByKey(accSlotMultiSelection[index]);
                    if (accessory != null && !accessory.IsEmptySlot && copySlotAutoArrange)
                    {
                        targetKeys.Add(GetNewEmptySlot(accSlotMultiSelection, targetKeys));
                    }
                    else
                    {
                        targetKeys.Add(accSlotMultiSelection[index]);
                    }
                }
                else if (copySlotAutoArrange)
                {
                    targetKeys.Add(GetNewEmptySlot(accSlotMultiSelection, targetKeys));
                }
                else
                {
                    break;
                }
            }

            rightScroll = GUILayout.BeginScrollView(rightScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label(LC("Copy/paste accessory between slot:"));
            int newSlotCount = 0;
            for (int index = 0; index < targetKeys.Count; index++)
            {
                AccessoryInfo accessory = controller.GetAccessoryInfoByKey(targetKeys[index]);
                string targetName;
                if (accessory == null)
                {
                    int slotIndex = int.Parse(targetKeys[index]);
                    if (PluginMoreAccessories.HasMoreAccessories)
                    {
                        targetName = cyanText("new slot " + (slotIndex + 1));
                        newSlotCount++;
                    }
                    else
                    {
                        targetName = redText(LC("No more slot! MoreAccessories not found?!"));
                    }
                }
                else
                {
                    targetName = accessory.IsEmptySlot
                        ? greenText(accessory.AccName)
                        : magentaText(accessory.AccName);
                }
                GUILayout.Label("  " + accSlotClipboard[index].accInfo.AccName + " -> " + targetName);
            }
            GUILayout.EndScrollView();

            copySlotAutoArrange = DrawModernToggle(copySlotAutoArrange, LC("Auto arrange empty slot, create new if needed"));
            GUILayout.BeginHorizontal();
            copySlotMirrorParent = DrawModernToggle(copySlotMirrorParent, LC("Mirror accessory parent"));
            copySlotMirrorAdjust = DrawModernToggle(copySlotMirrorAdjust, LC("Mirror accessory adjustment"));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("OK")))
            {
                if (newSlotCount > 0)
                {
                    int tenSlotBlocks = (newSlotCount - 1) / 10 + 1;
                    for (int index = 0; index < tenSlotBlocks; index++)
                    {
                        PluginMoreAccessories.AddTenAccessorySlots(controller.ociTarget.charInfo);
                    }
                    controller.RefreshAccessoriesList();
                    ClearSelectorCache();
                }
                for (int index = 0; index < targetKeys.Count; index++)
                {
                    if (controller.GetAccessoryInfoByKey(targetKeys[index]) != null)
                    {
                        controller.SetAccessoryDetailData(
                            targetKeys[index],
                            accSlotClipboard[index],
                            copySlotMirrorParent,
                            copySlotMirrorAdjust);
                    }
                }
                detailPageSelect = SelectMode.Normal;
            }
            if (GUILayout.Button(LC("Cancel")))
            {
                detailPageSelect = SelectMode.Normal;
            }
            GUILayout.EndHorizontal();
        }

        private static string GetMainGameGroupName(string rawTitle)
        {
            string group = (rawTitle ?? string.Empty).Trim('=');
            switch (group.ToUpperInvariant())
            {
                case "SHAPE":
                    return "Body Shape";
                case "SKIN":
                    return "Skin";
                case "FACE":
                    return "Face Settings";
                case "EYES":
                    return "Eye Settings";
                case "MAKEUP":
                    return "Makeup";
                default:
                    return HumanizeMainGameName(group);
            }
        }

        private static string GetMainGameLeftPanelTitle(string category1)
        {
            switch (category1)
            {
                case "Body": return "Body Shape";
                case "Face": return "Face Settings";
                case "Hair": return null;
                case "Clothes": return "Outfit";
                case "Accessories": return "Slot";
                default: return HumanizeMainGameName(category1);
            }
        }

        private static string GetMainGameRightTitle(
            CharaEditorController controller,
            string category1,
            string category2)
        {
            if (category1 == CharaEditorController.CT1_ACCS)
            {
                return controller.GetAccessoryInfoByKey(category2)?.AccName ?? "Accessory";
            }
            if (category1 == CharaEditorController.CT1_CTHS)
            {
                return GetMainGamePageName(category1, controller.GetClothDispName(category2));
            }
            return GetMainGamePageName(category1, category2);
        }

        private static string GetMainGamePageName(string rawName)
        {
            return GetMainGamePageName(null, rawName);
        }

        private static string GetMainGamePageName(string category1, string rawName)
        {
            switch (rawName)
            {
                case "FaceType": return "Facial Type";
                case "ShapeWhole": return "Overall";
                case "ShapeChin": return "Jaw";
                case "ShapeCheek": return "Cheeks";
                case "ShapeEyebrow": return "Eyebrows";
                case "ShapeEyes": return "Eyes";
                case "ShapeNose": return "Nose";
                case "ShapeMouth": return "Mouth";
                case "ShapeEar": return "Ears";
                case "Mole": return "Moles";
                case "Bread": return "Facial Hair Type";
                case "EyesSameSetting": return "Apply settings to both left and right eyes";
                case "EyeL": return "Eye (Left)";
                case "EyeR": return "Eye (Right)";
                case "EyeEtc": return "Iris Settings";
                case "EyeHL": return "Eye Highlights";
                case "Eyebrow": return "Eyebrow Type";
                case "Eyelashes": return "Eyelash Type";
                case "MakeupEyeshadow": return "Eye Shadow";
                case "MakeupCheek": return "Cheeks";
                case "MakeupLip": return "Lips";
                case "MakeupPaint1": return "Face Paint 1";
                case "MakeupPaint2": return "Face Paint 2";
                case "ShapeBreast": return "Breasts";
                case "ShapeUpper": return "Upper Body";
                case "ShapeLower": return "Lower Body";
                case "ShapeArm": return "Arms";
                case "ShapeLeg": return "Legs";
                case "Skin": return "Skin Type";
                case "Sunburn": return "Suntan";
                case "Nip": return "Nipples";
                case "Underhair": return "Pubic Hair";
                case "Nail": return "Nail Color";
                case "Paint1": return "Paint 01";
                case "Paint2": return "Paint 02";
                case "ColorAutoSetting": return "Auto color settings";
                case "ColorSameSetting": return "Use the same hair color";
                case "BackHair": return "Back Hair Settings";
                case "FrontHair": return "Bangs Settings";
                case "SideHair": return "Side Hair Settings";
                case "ExtensionHair": return "Hair Extensions Settings";
                case "Top": return "Tops";
                case "Bot": return "Bottom";
                case "Inner_t": return "Inner Top";
                case "Inner_b": return "Inner Bottom";
                case "Gloves": return "Gloves";
                case "Panst": return "Pantyhose";
                case "Socks": return "Socks";
                case "Shoes": return "Shoes";
                default:
                    return HumanizeMainGameName(rawName);
            }
        }

        private static string GetMainGameDetailName(string detailKey)
        {
            string rawName = GetDetailName(detailKey);
            if (detailKey.StartsWith("Clothes#", StringComparison.Ordinal))
            {
                if (Regex.IsMatch(rawName, "^Color [123]$")) return "Color";
                if (Regex.IsMatch(rawName, "^Gloss [123]$")) return "Shine";
                if (Regex.IsMatch(rawName, "^Metallic [123]$")) return "Texture";
                if (Regex.IsMatch(rawName, "^Pattern [123]$")) return "Type";
                Match patternDetail = Regex.Match(rawName, "^Pattern [123] (.+)$");
                if (patternDetail.Success)
                {
                    return HumanizeMainGameName(patternDetail.Groups[1].Value);
                }
            }
            if (rawName.EndsWith("HairType", StringComparison.Ordinal) ||
                rawName.EndsWith(" Type", StringComparison.Ordinal) ||
                rawName == "PupilType" || rawName == "BlackType" ||
                rawName == "EyeHLType" || rawName == "MoleType" ||
                rawName == "SunburnType" || rawName == "NipType" ||
                rawName == "UnderhairType" || rawName == "BreadType" ||
                rawName == "EyebrowType" || rawName == "EyelashesType" ||
                rawName == "CheekType" || rawName == "LipType" ||
                rawName == "PaintType")
            {
                return "Type";
            }
            switch (rawName)
            {
                case "FaceType":
                case "FaceSkinType":
                case "FaceDetailType":
                case "SkinType":
                case "DetailType":
                    return "Type";
                case "FaceDetailPower":
                case "DetailPower":
                    return "Strength";
                case "SkinColor": return "Color";
                case "SkinGloss": return "Gloss";
                case "SkinMetallic": return "Metallic";
                case "Acc Category": return "Category";
                case "Acc ID": return "Type";
                case "Height": return "Height";
                case "HeadSize": return "Head Size";
                case "EyeshadowType": return "Type";
                case "EyeshadowColor": return "Color";
                case "EyeshadowGloss": return "Gloss";
                default:
                    if (rawName.StartsWith("MultiDetail ", StringComparison.Ordinal))
                    {
                        return rawName.Replace("MultiDetail", "Wrinkles");
                    }
                    return HumanizeMainGameName(rawName);
            }
        }

        private static string HumanizeMainGameName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            string result = value.Replace('_', ' ');
            result = Regex.Replace(result, "([a-z0-9])([A-Z])", "$1 $2");
            result = Regex.Replace(result, "([A-Za-z])([0-9])", "$1 $2");
            result = Regex.Replace(result, "\\s+", " ").Trim();
            return result;
        }

        private bool TryGetMainGameContext(
            out CharaEditorController controller,
            out string category1,
            out string category2,
            out string detailSetKey)
        {
            controller = null;
            category1 = null;
            category2 = null;
            detailSetKey = null;
            if (ociTarget == null)
            {
                return false;
            }

            controller = CharaEditorMgr.Instance.GetEditorController(ociTarget);
            if (controller == null)
            {
                return false;
            }

            catelogIndex1 = Mathf.Clamp(catelogIndex1, 0, CharaEditorController.CATEGORY1.Length - 1);
            category1 = CharaEditorController.CATEGORY1[catelogIndex1];
            string[] category2List = controller.GetCategoryList(category1);
            if (category2List == null || category2List.Length == 0)
            {
                return false;
            }

            int selectedIndex = Mathf.Clamp(catelogIndex2[catelogIndex1], 0, category2List.Length - 1);
            if (category2List[selectedIndex].StartsWith("==") || category2List[selectedIndex].StartsWith("++"))
            {
                selectedIndex = Array.FindIndex(
                    category2List,
                    title => !title.StartsWith("==") && !title.StartsWith("++"));
                if (selectedIndex < 0)
                {
                    return false;
                }
                catelogIndex2[catelogIndex1] = selectedIndex;
            }

            category2 = category2List[selectedIndex];
            detailSetKey = category1 + "#" + category2;
            return true;
        }

        private void BeginMainGameSave()
        {
            CloseSelectorSidePanel();
            savingChara = ociTarget;
            ChaFile savingFile = savingChara.charInfo.chaFile;
            savingPath = CharaEditorMgr.GetExportCharaPath(savingFile.parameter.sex);
            savingFilename = string.Format(
                "CharaEditor_{0:yyyy-MM-dd-HH-mm-ss}_{1}_{2}.png",
                DateTime.Now,
                savingFile.parameter.sex == 0 ? "male" : "female",
                savingFile.parameter.fullname);
            if (savingFile.pngData != null)
            {
                Texture2D preview = new Texture2D(2, 2);
                ImageConversion.LoadImage(preview, savingFile.pngData);
                SetSavingTexture(preview);
            }
            else
            {
                SetSavingTexture(null);
            }
            savingCoordinate = false;
            coordinateName = savingFile.parameter.fullname + "_cood";
            float scale = Math.Max(0.01f, StudioCharaEditor.UIScale.Value);
            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            windowRect = new Rect(
                Math.Max(4f, (logicalWidth - 760f) * 0.5f),
                Math.Max(4f, (logicalHeight - 520f) * 0.5f),
                Math.Min(760f, logicalWidth - 8f),
                Math.Min(520f, logicalHeight - 8f));
            guiMode = GuiModeType.SAVE;
        }

        private void ResetMainGamePanelPositions()
        {
            float scale = GetActiveGuiScale();
            float logicalWidth = Screen.width / scale;
            mainGameLeftRect.width = 320f;
            mainGameLeftRect.height = 730f;
            mainGameRightRect.width = 468f;
            mainGameRightRect.height = 540f;
            mainGameStatusRect.width = MainGameAuxiliaryWidth;
            mainGameStatusRect.height = MainGameStatusHeight;
            mainGamePluginRect.width = MainGameAuxiliaryWidth;
            mainGamePluginRect.height = MainGamePluginHeight;
            mainGameLeftRect.x = 8f;
            mainGameLeftRect.y = MainGameHeaderHeight + 10f;
            mainGameRightRect.x = logicalWidth - mainGameRightRect.width - 10f;
            mainGameRightRect.y = 10f;
            mainGameStatusRect.x = mainGameRightRect.xMax - mainGameStatusRect.width;
            mainGameStatusRect.y = mainGameRightRect.yMax + 18f;
            mainGamePluginRect.x = mainGameRightRect.xMax - mainGamePluginRect.width;
            mainGamePluginRect.y = mainGameStatusRect.yMax + 12f;
            mainGameCollapsedStatusRect.position = new Vector2(
                mainGameStatusRect.xMax - mainGameCollapsedStatusRect.width,
                mainGameStatusRect.y);
            mainGameCollapsedPluginRect.position = new Vector2(
                mainGamePluginRect.xMax - mainGameCollapsedPluginRect.width,
                mainGamePluginRect.y);
            mainGameStatusCollapsed = false;
            mainGamePluginCollapsed = false;
            mainGameStatusCollapsedPositionInitialized = true;
            mainGamePluginCollapsedPositionInitialized = true;
            ClampMainGamePanelRects();
            PersistMainGamePanelPositions();
        }

        private void DrawMainGameDivider()
        {
            Rect rect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.Height(10f),
                GUILayout.ExpandWidth(true));
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 5f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.color = new Color(0.93f, 0.90f, 0.84f, 0.92f);
            GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void HandleMainGameWindowFocus(int windowId)
        {
            if (Event.current.type == EventType.MouseDown)
            {
                GUI.FocusControl(string.Empty);
                GUI.FocusWindow(windowId);
            }
            GUI.enabled = true;
        }

        private void CloseEditorFromMainGame()
        {
            PersistMainGamePanelPositions();
            VisibleGUI = false;
            if (StudioCharaEditor.Instance?._toolbarCharEditor != null)
            {
                StudioCharaEditor.Instance._toolbarCharEditor.Toggled.OnNext(false);
            }
        }
    }
}
