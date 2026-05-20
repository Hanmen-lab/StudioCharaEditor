using AIChara;
using BepInEx.Logging;
using CharaCustom;
using EpicToonFX;
using HarmonyLib;
using KKAPI.Utilities;
using Studio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using UnityEngine;
using static AIChara.ChaListDefine;

namespace StudioCharaEditor
{
    class CharaEditorUI : MonoBehaviour
    {
        enum SelectMode
        {
            Normal,
            ForCopy,
            ForPaste,
            PasteSlotPrompt,
        };

        private readonly int windowID = 10123;
        private readonly int selectorWindowID = 10124;
        private readonly string windowTitle = "Studio Charactor Editor";
        internal Rect windowRect = new Rect(0f, 300f, 600f, 400f);
        private Rect selectorWindowRect = new Rect(0f, 300f, 420f, 400f);
        private bool mouseInWindow = false;

        private TreeNodeObject lastSelectedTreeNode;
        private OCIChar ociTarget;
        private Dictionary<string, object> clipboard;
        private List<AccessoryDetailInfo> accSlotClipboard = new List<AccessoryDetailInfo>();
        private List<string> accSlotMultiSelection = new List<string>();
        private bool copySlotAutoArrange = true;
        private bool copySlotMirrorParent = false;
        private bool copySlotMirrorAdjust = false;
        private bool renameMode = false;
        private bool searchingMode = false;
        private string tempCharaName;
        private int catelogIndex1;
        private int[] catelogIndex2 = new int[] { 1, 1, 2, 0, 0 };
        private SelectMode detailPageSelect = SelectMode.Normal;
        private Dictionary<string, bool> selectBuffer = new Dictionary<string, bool>();
        private Dictionary<string, Vector2> scrollPool = new Dictionary<string, Vector2>();
        private Dictionary<string, bool> expandPool = new Dictionary<string, bool>();
        private Dictionary<string, Dictionary<string, Texture2D>> thumbPool = new Dictionary<string, Dictionary<string, Texture2D>>();
        private Dictionary<string, string> searchWordPool = new Dictionary<string, string>();
        private readonly Dictionary<string, List<CustomSelectInfo>> selectorListPool = new Dictionary<string, List<CustomSelectInfo>>();
        private readonly Dictionary<string, Dictionary<int, int>> selectorIndexPool = new Dictionary<string, Dictionary<int, int>>();
        private readonly Dictionary<string, SelectorRenderRange> selectorRenderRangePool = new Dictionary<string, SelectorRenderRange>();
        private readonly Dictionary<string, SelectorSearchState> selectorSearchPool = new Dictionary<string, SelectorSearchState>();
        private readonly Dictionary<string, ColorSwatch> colorSwatchPool = new Dictionary<string, ColorSwatch>();

        // save
        private OCIChar savingChara;
        private string savingPath;
        private string savingFilename;
        private Texture2D savingTexture;
        private bool savingCoordinate = false;
        private string coordinateName = "MyCoordinate";

        // GUI
        private GUIStyle largeLabel;
        private GUIStyle btnstyle;
        private GUIStyle windowStyle;
        private GUIStyle categoryButtonStyle;
        private GUIStyle texTextStyle;
        private GUIStyle colorSwatchButtonStyle;
        private GUIStyle closeButtonStyle;
        private global::Studio.CameraControl.NoCtrlFunc cameraNoCtrlCondition;
        private CharaEditorTheme theme;
        private Vector2 leftScroll = Vector2.zero;
        private Vector2 rightScroll = Vector2.zero;
        private bool resizingWindow = false;
        private Vector2 resizeStartMouse = Vector2.zero;
        private Vector2 resizeStartSize = Vector2.zero;
        private int namew = 100;
        private float thumbSize = 100;
        private float thumbSizeSmall = 70;
        private float thumbBtnHeight = 40;
        private GUIStyle resizeGripStyle;
        private const float ThumbListRowGap = 4f;
        private const int ColorSwatchWidth = 74;
        private const int ColorSwatchHeight = 20;
        private const float MinWindowWidth = 600f;
        private const float MinWindowHeight = 400f;
        private const float ResizeGripSize = 22f;
        private const float ResizeGripReserve = 28f;
        private const float SelectorPanelGap = 8f;
        private const float SelectorPanelWidth = 540f;
        private const float SelectorFolderWidth = 112f;
        private const float SelectorFolderRowHeight = 24f;
        private const float SelectorPanelThumbSize = 78f;
        private const float SelectorPanelButtonHeight = 40f;
        private const float ThinSliderHeight = 20f;
        private const float ThinSliderTrackHeight = 2f;
        private const float ThinSliderThumbSize = 7f;
        private const float TimelineButtonWidth = 22f;
        private const float ColorDragApplyInterval = 0.06f;
        private const float SelectorSearchDelay = 0.25f;
        private const int SelectorSearchBatchSize = 120;
        private const int MaxSelectorRowsPerFrame = 20;
        private const int MaxSelectorThumbLoadsPerFrame = 1;
        private const float SelectorThumbLoadIdleDelay = 0.25f;
        private const float SelectorScrollChangeEpsilon = 0.5f;
        private const string SelectorFolderAllKey = "__all";
        private const string SelectorFolderFavoritesKey = "__favorites";
        private const string SelectorFolderCustomPrefix = "custom:";
        private const float SelectorFavoriteButtonWidth = 24f;
        private readonly Dictionary<string, PendingColorChange> pendingColorChanges = new Dictionary<string, PendingColorChange>();
        private readonly HashSet<string> selectorFavoriteKeys = new HashSet<string>();
        private readonly Dictionary<string, List<SelectorCustomFolder>> selectorCustomFoldersByScope = new Dictionary<string, List<SelectorCustomFolder>>();
        private int selectorThumbLoadFrame = -1;
        private int selectorThumbLoadsThisFrame;
        private float selectorThumbLoadPauseUntil;
        private bool selectorFavoritesLoaded;
        private bool selectorCustomFoldersLoaded;
        private int selectorFavoriteVersion;
        private int selectorCustomFolderVersion;
        private static readonly string[] HairSetKeys =
        {
            "Hair#BackHair",
            "Hair#FrontHair",
            "Hair#SideHair",
            "Hair#ExtensionHair"
        };

        // work
        public static Queue<Action> ToDoQueue = new Queue<Action>();

        enum GuiModeType
        {
            MAIN,
            SAVE,
        };
        private GuiModeType guiMode = GuiModeType.MAIN;

        private class ColorSwatch
        {
            public Texture2D Texture;
            public Color[] Pixels;
            public int ColorKey = int.MinValue;
        }

        private class PendingColorChange
        {
            public ChaControl ChaCtrl;
            public string Name;
            public CharaDetailInfo DetailInfo;
            public Color Color;
            public float LastApplyTime;
            public bool HasPending;
        }

        private class SelectorRenderRange
        {
            public int FirstVisible;
            public int LastVisible;
            public int FilteredCount;
            public bool InSearching;
            public string SearchText;
            public int InfoCount;
            public bool IsValid;
        }

        private class SelectorSearchState
        {
            public string SearchText;
            public List<int> Matches = new List<int>();
            public int NextIndex;
            public int InfoCount;
            public bool Complete;
            public float LastInputTime;
            public int LastBuildFrame = -1;
        }

        private class SelectorSidePanel
        {
            public string Name;
            public string SelectorKey;
            public ChaControl ChaCtrl;
            public CharaDetailInfo DetailInfo;
            public Vector2 Scroll;
            public Vector2 FolderScroll;
            public string SearchText = string.Empty;
            public string SelectedFolderKey = SelectorFolderAllKey;
            public List<SelectorFolderInfo> Folders = new List<SelectorFolderInfo>();
            public int FolderInfoCount = -1;
            public int FolderFavoriteVersion = -1;
            public int FolderCustomVersion = -1;
            public bool ThumbList;
            public bool PendingScrollToSelected;
        }

        private class SelectorFolderInfo
        {
            public string Key;
            public string Name;
            public int Count;
            public List<int> Indices = new List<int>();
        }

        private class SelectorCustomFolder
        {
            public string Scope;
            public string Name;
            public HashSet<string> ItemKeys = new HashSet<string>();
            public HashSet<int> LegacyItemIds = new HashSet<int>();
        }

        private enum SelectorContextMenuType
        {
            Item,
            Folder,
        }

        private class SelectorContextMenu
        {
            public SelectorContextMenuType Type;
            public Rect Rect;
            public Vector2 Position;
            public string Scope;
            public string FolderKey;
            public string FolderName;
            public CustomSelectInfo Item;
            public string NewFolderName = string.Empty;
            public string RenameFolderName = string.Empty;
        }

        private SelectorSidePanel selectorSidePanel;
        private SelectorContextMenu selectorContextMenu;

        // Localize
        public Dictionary<string, string> curLocalizationDict;

        // Control flag
        public bool VisibleGUI { get; set; }
        public bool LaterUpdate { get; set; }

        public void ResetGui()
        {
            guiMode = GuiModeType.MAIN;
            ociTarget = null;
            renameMode = false;
            searchingMode = false;
            tempCharaName = null;
            catelogIndex1 = 0;
            catelogIndex2 = new int[] { 1, 1, 2, 0, 0 };
            detailPageSelect = SelectMode.Normal;
            CloseSelectorSidePanel();
            ClearSelectorCache();
        }

        private void Start()
        {
            theme = new CharaEditorTheme();
            largeLabel = new GUIStyle("label");
            largeLabel.fontSize = 16;
            btnstyle = new GUIStyle("button");
            btnstyle.fontSize = 16;
            categoryButtonStyle = new GUIStyle("button");
            texTextStyle = new GUIStyle("box")
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true
            };
            cameraNoCtrlCondition = () => mouseInWindow && VisibleGUI;

            //Console.WriteLine("StudioCharaEditor CharaEditorUI started.");
        }

        private void OnDestroy()
        {
            FlushPendingColorChanges(true);

            foreach (ColorSwatch swatch in colorSwatchPool.Values)
            {
                if (swatch.Texture != null)
                {
                    Destroy(swatch.Texture);
                }
            }
            colorSwatchPool.Clear();

            if (theme != null)
            {
                theme.Dispose();
                theme = null;
            }

            if (savingTexture != null)
            {
                Destroy(savingTexture);
                savingTexture = null;
            }
        }

        private void EnsureTheme()
        {
            if (theme == null)
            {
                theme = new CharaEditorTheme();
            }

            theme.Ensure(GUI.skin);
            if (theme.Skin == null)
            {
                return;
            }

            largeLabel = theme.LargeLabelStyle;
            btnstyle = theme.PrimaryButtonStyle;
            windowStyle = theme.WindowStyle;
            categoryButtonStyle = theme.CategoryButtonStyle;
            texTextStyle = theme.TextureTextStyle;
            colorSwatchButtonStyle = theme.ColorSwatchButtonStyle;
            closeButtonStyle = theme.CloseButtonStyle;
            EnsureResizeGripStyle();
        }

        private void EnsureResizeGripStyle()
        {
            if (resizeGripStyle != null)
            {
                return;
            }

            resizeGripStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 2, 0, 0),
                margin = new RectOffset(0, 0, 0, 0)
            };
            resizeGripStyle.normal.textColor = new Color(0.58f, 0.66f, 0.70f, 0.85f);
            resizeGripStyle.hover.textColor = Color.white;
            resizeGripStyle.active.textColor = Color.white;
        }

        private bool DrawModernToggle(bool value, string label, params GUILayoutOption[] options)
        {
            GUIStyle labelStyle = GUI.skin.toggle ?? GUI.skin.label;
            GUIContent content = new GUIContent(label);
            float labelWidth = Math.Max(1f, labelStyle.CalcSize(content).x);
            Rect rect = GUILayoutUtility.GetRect(25f + labelWidth, 20f, options);
            Rect iconRect = new Rect(rect.x, rect.y + (rect.height - 16f) * 0.5f, 16f, 16f);
            Rect labelRect = new Rect(iconRect.xMax + 6f, rect.y, Math.Max(0f, rect.xMax - iconRect.xMax - 6f), rect.height);

            Event evt = Event.current;
            if (GUI.enabled && evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
            {
                value = !value;
                evt.Use();
            }

            if (evt.type == EventType.Repaint)
            {
                Texture2D toggleTexture = value ? theme?.ToggleOnTexture : theme?.ToggleOffTexture;
                if (toggleTexture != null)
                {
                    GUI.DrawTexture(iconRect, toggleTexture, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    GUI.Box(iconRect, value ? "X" : string.Empty);
                }
                labelStyle.Draw(labelRect, content, false, false, value, false);
            }

            return value;
        }

        private void DrawTimelineButton(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            if (!PluginTimelineCompatibility.CanSelect(ociTarget, chaCtrl, dInfo))
            {
                return;
            }

            Color oldColor = GUI.color;
            if (PluginTimelineCompatibility.IsSelected(ociTarget, dInfo))
            {
                GUI.color = Color.cyan;
            }

            string displayName = GetTimelineDisplayName(name, dInfo);
            if (GUILayout.Button(new GUIContent("T", "Timeline"), GUILayout.Width(TimelineButtonWidth)))
            {
                PluginTimelineCompatibility.SelectInterpolable(ociTarget, chaCtrl, displayName, dInfo);
            }

            GUI.color = oldColor;
        }

        private string GetTimelineDisplayName(string name, CharaDetailInfo dInfo)
        {
            string detailKey = dInfo?.DetailDefine?.Key;
            string category = string.IsNullOrEmpty(detailKey) ? string.Empty : GetDetailCategory2(detailKey);
            string label = LC(name);
            if (string.IsNullOrEmpty(category) || category == detailKey)
            {
                return label;
            }

            string categoryLabel = LC(category);
            return categoryLabel == label ? label : categoryLabel + " " + label;
        }

        private void DrawAbmxTimelineButton(ChaControl chaCtrl, string name, string slideName, CharaDetailInfo dInfo, int subSliderIndex)
        {
            if (!PluginTimelineCompatibility.CanSelectAbmx(ociTarget, chaCtrl, dInfo, subSliderIndex))
            {
                return;
            }

            Color oldColor = GUI.color;
            if (PluginTimelineCompatibility.IsSelectedAbmx(ociTarget, dInfo, subSliderIndex))
            {
                GUI.color = Color.cyan;
            }

            string displayName = GetAbmxTimelineDisplayName(name, slideName, dInfo);
            if (GUILayout.Button(new GUIContent("T", "Timeline"), GUILayout.Width(TimelineButtonWidth)))
            {
                PluginTimelineCompatibility.SelectAbmxInterpolable(ociTarget, dInfo, displayName, subSliderIndex);
            }

            GUI.color = oldColor;
        }

        private string GetAbmxTimelineDisplayName(string name, string slideName, CharaDetailInfo dInfo)
        {
            string displayName = GetTimelineDisplayName(name, dInfo);
            CharaABMXDetailDefine3 dd3 = dInfo.DetailDefine as CharaABMXDetailDefine3;
            if (dd3 != null)
            {
                displayName += " " + LC(dd3.targetNames[dd3.curTargetIndex]) + " " + LC(dd3.fingerNames[dd3.curFingerIndex]) + " " + LC(dd3.segmentNames[dd3.curSegmentIndex]);
            }
            else if (dInfo.DetailDefine is CharaABMXDetailDefine2 dd2)
            {
                displayName += " " + LC(dd2.targetNames[dd2.curTargetIndex]);
            }

            return displayName + " " + LC(slideName);
        }

        private int GetValueFieldWidth(string valueText, int minWidth)
        {
            int dynamicWidth = (valueText == null ? 0 : valueText.Length) * 8 + 18;
            return Math.Min(96, Math.Max(minWidth, dynamicWidth));
        }

        private float DrawThinSlider(float value, float min, float max)
        {
            Rect sliderRect = GUILayoutUtility.GetRect(40f, 10000f, ThinSliderHeight, ThinSliderHeight, GUILayout.ExpandWidth(true));
            Rect trackRect = new Rect(
                sliderRect.x,
                sliderRect.y + (sliderRect.height - ThinSliderTrackHeight) * 0.5f,
                sliderRect.width,
                ThinSliderTrackHeight);

            if (max <= min)
            {
                max = min + 0.0001f;
            }

            int controlId = GUIUtility.GetControlID("StudioCharaEditorThinSlider".GetHashCode(), FocusType.Passive, sliderRect);
            Event evt = Event.current;
            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (GUI.enabled && evt.button == 0 && sliderRect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlId;
                        value = ValueFromSliderMouse(trackRect, min, max, evt.mousePosition.x);
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlId)
                    {
                        value = ValueFromSliderMouse(trackRect, min, max, evt.mousePosition.x);
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

            float normalized = Mathf.InverseLerp(min, max, value);
            float thumbX = Mathf.Lerp(trackRect.xMin, trackRect.xMax, normalized) - ThinSliderThumbSize * 0.5f;
            Rect thumbRect = new Rect(
                thumbX,
                sliderRect.y + (sliderRect.height - ThinSliderThumbSize) * 0.5f,
                ThinSliderThumbSize,
                ThinSliderThumbSize);

            GUI.Box(trackRect, GUIContent.none, GUI.skin.horizontalSlider);
            GUI.Box(thumbRect, GUIContent.none, GUI.skin.horizontalSliderThumb);
            return value;
        }

        private float ValueFromSliderMouse(Rect trackRect, float min, float max, float mouseX)
        {
            if (trackRect.width <= 0f)
            {
                return min;
            }

            float normalized = Mathf.Clamp01((mouseX - trackRect.xMin) / trackRect.width);
            return Mathf.Lerp(min, max, normalized);
        }

        private void DrawResizeGrip()
        {
            EnsureResizeGripStyle();

            Rect gripRect = new Rect(
                windowRect.width - ResizeGripSize - 4f,
                windowRect.height - ResizeGripSize - 4f,
                ResizeGripSize,
                ResizeGripSize);
            int controlId = GUIUtility.GetControlID("StudioCharaEditorResizeGrip".GetHashCode(), FocusType.Passive, gripRect);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    if (evt.button == 0 && gripRect.Contains(evt.mousePosition))
                    {
                        resizingWindow = true;
                        resizeStartMouse = evt.mousePosition;
                        resizeStartSize = new Vector2(windowRect.width, windowRect.height);
                        GUIUtility.hotControl = controlId;
                        evt.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (resizingWindow && GUIUtility.hotControl == controlId)
                    {
                        Vector2 delta = evt.mousePosition - resizeStartMouse;
                        windowRect.width = Math.Max(MinWindowWidth, resizeStartSize.x + delta.x);
                        windowRect.height = Math.Max(MinWindowHeight, resizeStartSize.y + delta.y);
                        evt.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlId)
                    {
                        resizingWindow = false;
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;
            }

            GUI.Label(gripRect, "///", resizeGripStyle);
        }

        private void OnGUI()
        {
            if (VisibleGUI)
            {
                GUISkin previousSkin = GUI.skin;
                try
                {
                    EnsureTheme();
                    if (theme != null && theme.Skin != null)
                    {
                        GUI.skin = theme.Skin;
                    }

                    if (windowStyle == null)
                    {
                        windowStyle = new GUIStyle(GUI.skin.window);
                    }
                    windowRect = GUI.Window(windowID, windowRect, new GUI.WindowFunction(FuncWindowGUI), windowTitle, windowStyle);
                    if (selectorSidePanel != null)
                    {
                        AlignSelectorWindow();
                        selectorWindowRect = GUI.Window(selectorWindowID, selectorWindowRect, new GUI.WindowFunction(FuncSelectorWindowGUI), LC("Select item"), windowStyle);
                    }

                    mouseInWindow = windowRect.Contains(Event.current.mousePosition) ||
                                    (selectorSidePanel != null && selectorWindowRect.Contains(Event.current.mousePosition));
                    if (mouseInWindow)
                    {
                        Studio.Studio.Instance.cameraCtrl.noCtrlCondition = cameraNoCtrlCondition;
                        Input.ResetInputAxes();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
                finally
                {
                    GUI.skin = previousSkin;
                }
            }
        }

        private void AlignSelectorWindow()
        {
            float width = Math.Min(SelectorPanelWidth, Math.Max(320f, Screen.width - 8f));
            float height = Math.Max(MinWindowHeight, windowRect.height);
            float x = windowRect.xMax + SelectorPanelGap;
            if (x + width > Screen.width - 4f)
            {
                x = windowRect.x - width - SelectorPanelGap;
            }
            if (x < 4f)
            {
                x = Math.Max(4f, Screen.width - width - 4f);
            }

            selectorWindowRect = new Rect(x, windowRect.y, width, height);
        }

        private void Update()
        {
            FlushPendingColorChanges(false);

            // hotkey check
            if (StudioCharaEditor.KeyShowUI.Value.IsDown())
            {
                if (VisibleGUI)
                {
                    FlushPendingColorChanges(true);
                }

                VisibleGUI = !VisibleGUI;

                // Синхронизируем состояние кнопки
                if (StudioCharaEditor.Instance._toolbarCharEditor != null)
                    StudioCharaEditor.Instance._toolbarCharEditor.Toggled.OnNext(VisibleGUI);

                if (VisibleGUI)
                {
                    CharaEditorMgr.Instance.ReloadDictionary();
                    windowRect = new Rect(StudioCharaEditor.UIXPosition.Value,
                        StudioCharaEditor.UIYPosition.Value,
                        Math.Max(MinWindowWidth, StudioCharaEditor.UIWidth.Value),
                        Math.Max(MinWindowHeight, StudioCharaEditor.UIHeight.Value));
                }
                else
                {
                    StudioCharaEditor.UIXPosition.Value = (int)windowRect.x;
                    StudioCharaEditor.UIYPosition.Value = (int)windowRect.y;
                    StudioCharaEditor.UIWidth.Value = (int)windowRect.width;
                    StudioCharaEditor.UIHeight.Value = (int)windowRect.height;
                }
            }

            // change select check
            if (VisibleGUI)
            {
                TreeNodeObject curSel = GetCurrentSelectedNode();
                if (curSel != lastSelectedTreeNode)
                {
                    OnSelectChange(curSel);
                }
            }

            // house keeping
            CharaEditorMgr.Instance.HouseKeeping(VisibleGUI);

            // check todo queue
            if (ToDoQueue.Count > 0)
            {
                Action p = ToDoQueue.Dequeue();
                p();
            }
        }

        private string GetColorChangeKey(ChaControl chaCtrl, CharaDetailInfo dInfo)
        {
            int charId = chaCtrl != null ? chaCtrl.GetInstanceID() : 0;
            string detailKey = dInfo?.DetailDefine?.Key ?? string.Empty;
            return charId.ToString() + "|" + detailKey;
        }

        private void QueueColorChange(ChaControl chaCtrl, string name, CharaDetailInfo dInfo, Color color, bool force = false)
        {
            if (chaCtrl == null || dInfo?.DetailDefine?.Set == null)
            {
                return;
            }

            string key = GetColorChangeKey(chaCtrl, dInfo);
            if (!pendingColorChanges.TryGetValue(key, out PendingColorChange pending))
            {
                pending = new PendingColorChange();
                pendingColorChanges[key] = pending;
            }

            pending.ChaCtrl = chaCtrl;
            pending.Name = name;
            pending.DetailInfo = dInfo;
            pending.Color = color;
            pending.HasPending = true;

            float now = Time.realtimeSinceStartup;
            if (force || pending.LastApplyTime <= 0f || now - pending.LastApplyTime >= ColorDragApplyInterval)
            {
                ApplyPendingColorChange(key, pending);
            }
        }

        private void FlushPendingColorChanges(bool force)
        {
            if (pendingColorChanges.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            List<string> keysToRemove = null;
            foreach (KeyValuePair<string, PendingColorChange> pair in pendingColorChanges)
            {
                PendingColorChange pending = pair.Value;
                if (pending == null || pending.ChaCtrl == null || pending.DetailInfo?.DetailDefine == null)
                {
                    if (keysToRemove == null)
                    {
                        keysToRemove = new List<string>();
                    }
                    keysToRemove.Add(pair.Key);
                    continue;
                }

                if (pending.HasPending && (force || now - pending.LastApplyTime >= ColorDragApplyInterval))
                {
                    ApplyPendingColorChange(pair.Key, pending);
                }
            }

            if (keysToRemove != null)
            {
                for (int i = 0; i < keysToRemove.Count; i++)
                {
                    pendingColorChanges.Remove(keysToRemove[i]);
                }
            }
        }

        private void ApplyPendingColorChange(string key, PendingColorChange pending)
        {
            if (pending == null || pending.ChaCtrl == null || pending.DetailInfo?.DetailDefine == null)
            {
                pendingColorChanges.Remove(key);
                return;
            }

            CharaDetailDefine detail = pending.DetailInfo.DetailDefine;
            object current = detail.Get != null ? detail.Get(pending.ChaCtrl) : null;
            if (current is Color currentColor && currentColor == pending.Color)
            {
                pending.HasPending = false;
                pending.LastApplyTime = Time.realtimeSinceStartup;
                return;
            }

            detail.Set(pending.ChaCtrl, pending.Color);
            if (detail.Upd != null && !LaterUpdate)
            {
                detail.Upd(pending.ChaCtrl);
            }
            accessoryMultiAdjust(pending.ChaCtrl, pending.Name, pending.DetailInfo, pending.Color);

            pending.HasPending = false;
            pending.LastApplyTime = Time.realtimeSinceStartup;
        }

        private SelectorRenderRange GetSelectorRenderRange(
            string selectorKey,
            List<CustomSelectInfo> infoList,
            bool inSearching,
            string searchText,
            int filteredCount,
            Vector2 scrollPosition,
            float rowHeight,
            int showBefore,
            int showAfter)
        {
            if (!selectorRenderRangePool.TryGetValue(selectorKey, out SelectorRenderRange range))
            {
                range = new SelectorRenderRange();
                selectorRenderRangePool[selectorKey] = range;
            }

            bool searchChanged = range.InSearching != inSearching || !string.Equals(range.SearchText, searchText);
            bool countChanged = range.InfoCount != (infoList?.Count ?? 0) || range.FilteredCount != filteredCount;
            if (Event.current.type == EventType.Layout || !range.IsValid || searchChanged || countChanged)
            {
                int firstVisible = rowHeight > 0f
                    ? Math.Max(0, (int)(scrollPosition.y / rowHeight) - showBefore)
                    : 0;
                int requestedRows = Math.Max(1, showBefore + showAfter + 2);
                int rowsToDraw = Math.Min(MaxSelectorRowsPerFrame, requestedRows);
                int lastVisible = Math.Min(filteredCount - 1, firstVisible + rowsToDraw - 1);

                range.FirstVisible = filteredCount > 0 ? firstVisible : 0;
                range.LastVisible = lastVisible;
                range.FilteredCount = filteredCount;
                range.InSearching = inSearching;
                range.SearchText = searchText;
                range.InfoCount = infoList?.Count ?? 0;
                range.IsValid = true;
            }

            return range;
        }

        private SelectorSearchState GetSelectorSearchState(string selectorKey, List<CustomSelectInfo> infoList, string searchText)
        {
            return GetSelectorSearchState(selectorKey, infoList, null, searchText);
        }

        private SelectorSearchState GetSelectorSearchState(string selectorKey, List<CustomSelectInfo> infoList, List<int> sourceIndices, string searchText)
        {
            if (!selectorSearchPool.TryGetValue(selectorKey, out SelectorSearchState state))
            {
                state = new SelectorSearchState();
                selectorSearchPool[selectorKey] = state;
            }

            int infoCount = sourceIndices?.Count ?? (infoList?.Count ?? 0);
            if (!string.Equals(state.SearchText, searchText) || state.InfoCount != infoCount)
            {
                state.SearchText = searchText;
                state.InfoCount = infoCount;
                state.Matches.Clear();
                state.NextIndex = 0;
                state.Complete = string.IsNullOrWhiteSpace(searchText);
                state.LastInputTime = Time.realtimeSinceStartup;
                state.LastBuildFrame = -1;
            }

            if (!state.Complete &&
                Event.current.type == EventType.Layout &&
                Time.realtimeSinceStartup - state.LastInputTime >= SelectorSearchDelay &&
                state.LastBuildFrame != Time.frameCount)
            {
                state.LastBuildFrame = Time.frameCount;
                int endIndex = Math.Min(infoCount, state.NextIndex + SelectorSearchBatchSize);
                for (int i = state.NextIndex; i < endIndex; i++)
                {
                    int infoIndex = sourceIndices != null ? sourceIndices[i] : i;
                    if (infoIndex >= 0 && infoIndex < infoList.Count && SelectorMatchesSearch(infoList[infoIndex], true, searchText))
                    {
                        state.Matches.Add(infoIndex);
                    }
                }

                state.NextIndex = endIndex;
                state.Complete = state.NextIndex >= infoCount;
            }

            return state;
        }

        private void FuncWindowGUI(int winID)
        {
            try
            {
                if (GUIUtility.hotControl == 0)
                {

                }
                if (Event.current.type == EventType.MouseDown)
                {
                    GUI.FocusControl("");
                    GUI.FocusWindow(winID);

                }
                GUI.enabled = true;

                switch (guiMode)
                {
                    case GuiModeType.MAIN:
                        guiEditorMain();
                        break;
                    case GuiModeType.SAVE:
                        guiSave();
                        break;
                    default:
                        throw new Exception("Unknown gui mode");
                }

                DrawResizeGrip();
                GUI.DragWindow(new Rect(0f, 0f, Math.Max(0f, windowRect.width - 24f), 24f));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                ResetGui();
            }
            finally
            {

            }
        }

        private void FuncSelectorWindowGUI(int winID)
        {
            try
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    GUI.FocusControl("");
                    GUI.FocusWindow(winID);
                }

                if (selectorSidePanel == null ||
                    selectorSidePanel.ChaCtrl == null ||
                    selectorSidePanel.DetailInfo?.DetailDefine?.SelectorList == null)
                {
                    CloseSelectorSidePanel();
                    return;
                }

                HandleSelectorContextMenuOutsideClick();
                DrawSelectorSidePanel(selectorSidePanel);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                CloseSelectorSidePanel();
            }
        }

        private void DrawSelectorSidePanel(SelectorSidePanel panel)
        {
            bool blockBehindContextMenu = IsSelectorContextMenuBlockingCurrentMouseEvent();
            bool oldGuiEnabled = GUI.enabled;
            if (blockBehindContextMenu)
            {
                GUI.enabled = false;
            }

            string selectorKey = panel.SelectorKey;
            List<CustomSelectInfo> infoList = GetSelectorList(panel.ChaCtrl, panel.DetailInfo);
            int selectedId = Convert.ToInt32(panel.DetailInfo.DetailDefine.Get(panel.ChaCtrl));
            int selectedIndex = GetSelectorIndex(selectorKey, infoList, selectedId, out string selectedName);

            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(panel.Name), GUILayout.Width(namew));
            GUILayout.Label(string.Format("#{0}: {1}", selectedId, selectedName));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                CloseSelectorSidePanel();
            }
            GUILayout.EndHorizontal();

            if (panel.ThumbList)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(LC("Search"), GUILayout.Width(namew));
                string newSearch = GUILayout.TextField(panel.SearchText ?? string.Empty);
                if (!string.Equals(newSearch, panel.SearchText))
                {
                    panel.SearchText = newSearch;
                    ClearSelectorRuntimeCache(selectorKey);
                }
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    panel.SearchText = string.Empty;
                    ClearSelectorRuntimeCache(selectorKey);
                }
                GUILayout.EndHorizontal();
            }

            EnsureSelectorFolders(panel, infoList);
            List<int> folderIndices = GetSelectedFolderIndices(panel);
            bool inSearching = panel.ThumbList && !string.IsNullOrWhiteSpace(panel.SearchText);
            string selectorFilterKey = selectorKey + "|side|" + (panel.SelectedFolderKey ?? SelectorFolderAllKey) + "|" + (inSearching ? panel.SearchText : string.Empty);
            SelectorSearchState searchState = inSearching ? GetSelectorSearchState(selectorFilterKey, infoList, folderIndices, panel.SearchText) : null;
            List<int> filteredIndices = inSearching ? searchState?.Matches : folderIndices;
            int filteredCount = filteredIndices?.Count ?? infoList.Count;
            float rowHeight = panel.ThumbList ? SelectorPanelThumbSize + ThumbListRowGap : 24f;
            int showBefore = 1;
            int showAfter = Math.Max(4, (int)Math.Ceiling((selectorWindowRect.height - 112f) / rowHeight) + 2);
            int selectedVisibleIndex = GetSelectorVisibleIndex(selectedIndex, filteredIndices);
            if (panel.PendingScrollToSelected)
            {
                panel.Scroll = selectedVisibleIndex >= 0
                    ? new Vector2(0f, Math.Max(0, selectedVisibleIndex) * rowHeight + ThumbListRowGap)
                    : Vector2.zero;
                panel.PendingScrollToSelected = false;
            }

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            Vector2 oldScroll = panel.Scroll;
            panel.Scroll = GUILayout.BeginScrollView(panel.Scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            if (panel.ThumbList)
            {
                SelectorRenderRange range = GetSelectorRenderRange(selectorFilterKey, infoList, inSearching, panel.SearchText, filteredCount, panel.Scroll, rowHeight, showBefore, showAfter);
                int firstVisible = range.FirstVisible;
                int lastVisible = range.LastVisible;

                if (firstVisible > 0)
                {
                    GUILayout.Space(firstVisible * rowHeight);
                }

                int lastDrawn = firstVisible - 1;
                for (int visibleIndex = firstVisible; visibleIndex <= lastVisible; visibleIndex++)
                {
                    int infoIndex = filteredIndices != null ? -1 : visibleIndex;
                    if (filteredIndices != null)
                    {
                        if (visibleIndex < 0 || visibleIndex >= filteredIndices.Count)
                        {
                            break;
                        }
                        infoIndex = filteredIndices[visibleIndex];
                    }

                    if (infoIndex >= 0 && infoIndex < infoList.Count)
                    {
                        DrawSelectorSideThumbRow(panel, infoList[infoIndex], selectedId);
                    }
                    lastDrawn = visibleIndex;
                }

                int trailingRows = filteredCount - lastDrawn - 1;
                if (trailingRows > 0)
                {
                    GUILayout.Space(trailingRows * rowHeight);
                }
                if (inSearching && searchState != null && !searchState.Complete)
                {
                    GUILayout.Label(LC("Searching") + "...", GUI.skin.box);
                }
            }
            else
            {
                SelectorRenderRange range = GetSelectorRenderRange(selectorFilterKey, infoList, false, null, filteredCount, panel.Scroll, rowHeight, 0, MaxSelectorRowsPerFrame - 1);
                if (range.FirstVisible > 0)
                {
                    GUILayout.Space(range.FirstVisible * rowHeight);
                }

                int lastDrawn = range.FirstVisible - 1;
                for (int visibleIndex = range.FirstVisible; visibleIndex <= range.LastVisible; visibleIndex++)
                {
                    int infoIndex = filteredIndices != null ? -1 : visibleIndex;
                    if (filteredIndices != null)
                    {
                        if (visibleIndex < 0 || visibleIndex >= filteredIndices.Count)
                        {
                            break;
                        }
                        infoIndex = filteredIndices[visibleIndex];
                    }

                    if (infoIndex >= 0 && infoIndex < infoList.Count)
                    {
                        DrawSelectorSideTextRow(panel, infoList[infoIndex], selectedId);
                    }
                    lastDrawn = visibleIndex;
                }

                int trailingRows = filteredCount - lastDrawn - 1;
                if (trailingRows > 0)
                {
                    GUILayout.Space(trailingRows * rowHeight);
                }
            }
            GUILayout.EndScrollView();
            TrackSelectorScroll(oldScroll, panel.Scroll);

            if (selectedVisibleIndex >= 0 && GUILayout.Button(LC("Scroll to selected")))
            {
                panel.Scroll = new Vector2(0f, Math.Max(0, selectedVisibleIndex) * rowHeight + ThumbListRowGap);
            }
            GUILayout.EndVertical();
            DrawSelectorFolderPanel(panel, infoList);
            GUILayout.EndHorizontal();
            GUI.enabled = oldGuiEnabled;
            DrawSelectorContextMenu(panel);
        }

        private void DrawSelectorFolderPanel(SelectorSidePanel panel, List<CustomSelectInfo> infoList)
        {
            if (!panel.ThumbList)
            {
                return;
            }

            EnsureSelectorFolders(panel, infoList);
            GUILayout.BeginVertical(GUILayout.Width(SelectorFolderWidth));
            GUILayout.Label(LC("Folders"), GUI.skin.box, GUILayout.Height(SelectorFolderRowHeight));
            panel.FolderScroll = GUILayout.BeginScrollView(panel.FolderScroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            for (int i = 0; i < panel.Folders.Count; i++)
            {
                SelectorFolderInfo folder = panel.Folders[i];
                Color oldColor = GUI.color;
                if (string.Equals(panel.SelectedFolderKey, folder.Key, StringComparison.OrdinalIgnoreCase))
                {
                    GUI.color = Color.green;
                }

                string label = $"{folder.Name} {folder.Count}";
                Rect folderRect = GUILayoutUtility.GetRect(new GUIContent(label), GUI.skin.button, GUILayout.Height(SelectorFolderRowHeight), GUILayout.ExpandWidth(true));
                if (DrawSelectorManualButton(folderRect, new GUIContent(label), string.Equals(panel.SelectedFolderKey, folder.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectFolder(panel, folder.Key);
                }
                else if (ConsumeSelectorRightClick(folderRect))
                {
                    OpenSelectorFolderContextMenu(panel, folder);
                }

                GUI.color = oldColor;
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void SelectFolder(SelectorSidePanel panel, string folderKey)
        {
            if (string.Equals(panel.SelectedFolderKey, folderKey, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            panel.SelectedFolderKey = folderKey;
            panel.PendingScrollToSelected = true;
            panel.Scroll = Vector2.zero;
            selectorRenderRangePool.Clear();
        }

        private void EnsureSelectorFolders(SelectorSidePanel panel, List<CustomSelectInfo> infoList)
        {
            EnsureSelectorFavoritesLoaded();
            EnsureSelectorCustomFoldersLoaded();
            int infoCount = infoList?.Count ?? 0;
            if (panel.Folders != null &&
                panel.FolderInfoCount == infoCount &&
                panel.FolderFavoriteVersion == selectorFavoriteVersion &&
                panel.FolderCustomVersion == selectorCustomFolderVersion)
            {
                return;
            }

            panel.FolderInfoCount = infoCount;
            panel.FolderFavoriteVersion = selectorFavoriteVersion;
            panel.FolderCustomVersion = selectorCustomFolderVersion;
            panel.Folders = new List<SelectorFolderInfo>
            {
                new SelectorFolderInfo
                {
                    Key = SelectorFolderAllKey,
                    Name = LC("All"),
                    Count = infoCount
                }
            };

            SelectorFolderInfo favoritesFolder = new SelectorFolderInfo
            {
                Key = SelectorFolderFavoritesKey,
                Name = LC("Fav")
            };

            for (int i = 0; i < infoCount; i++)
            {
                if (IsSelectorFavorite(panel.SelectorKey, infoList[i]))
                {
                    favoritesFolder.Count++;
                    favoritesFolder.Indices.Add(i);
                }
            }
            panel.Folders.Add(favoritesFolder);

            string scope = GetSelectorFavoriteScope(panel.SelectorKey);
            foreach (SelectorCustomFolder customFolder in GetCustomFolders(scope).OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
            {
                SelectorFolderInfo folderInfo = new SelectorFolderInfo
                {
                    Key = GetCustomFolderKey(customFolder.Name),
                    Name = customFolder.Name
                };

                for (int i = 0; i < infoCount; i++)
                {
                    if (SelectorFolderContainsItem(customFolder, panel.ChaCtrl, infoList[i]))
                    {
                        folderInfo.Count++;
                        folderInfo.Indices.Add(i);
                    }
                }

                panel.Folders.Add(folderInfo);
            }

            if (!panel.Folders.Any(folder => string.Equals(folder.Key, panel.SelectedFolderKey, StringComparison.OrdinalIgnoreCase)))
            {
                panel.SelectedFolderKey = SelectorFolderAllKey;
            }
        }

        private List<int> GetSelectedFolderIndices(SelectorSidePanel panel)
        {
            if (panel == null || string.IsNullOrEmpty(panel.SelectedFolderKey) || panel.SelectedFolderKey == SelectorFolderAllKey)
            {
                return null;
            }

            SelectorFolderInfo folder = panel.Folders?.FirstOrDefault(info => string.Equals(info.Key, panel.SelectedFolderKey, StringComparison.OrdinalIgnoreCase));
            return folder?.Indices;
        }

        private static int GetSelectorVisibleIndex(int infoIndex, List<int> filteredIndices)
        {
            if (infoIndex < 0)
            {
                return -1;
            }

            if (filteredIndices == null)
            {
                return infoIndex;
            }

            for (int i = 0; i < filteredIndices.Count; i++)
            {
                if (filteredIndices[i] == infoIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private void DrawSelectorFavoriteButton(SelectorSidePanel panel, CustomSelectInfo info, float height)
        {
            bool favorite = IsSelectorFavorite(panel.SelectorKey, info);
            Color oldColor = GUI.color;
            if (favorite)
            {
                GUI.color = Color.yellow;
            }

            GUIContent content = new GUIContent(favorite ? "F" : "+", favorite ? "Remove favorite" : "Add favorite");
            if (GUILayout.Button(content, GUILayout.Width(SelectorFavoriteButtonWidth), GUILayout.Height(height)))
            {
                ToggleSelectorFavorite(panel, info);
            }

            GUI.color = oldColor;
        }

        private bool IsSelectorFavorite(string selectorKey, CustomSelectInfo info)
        {
            EnsureSelectorFavoritesLoaded();
            return info != null && selectorFavoriteKeys.Contains(GetSelectorFavoriteKey(selectorKey, info.id));
        }

        private void ToggleSelectorFavorite(SelectorSidePanel panel, CustomSelectInfo info)
        {
            if (panel == null || info == null)
            {
                return;
            }

            EnsureSelectorFavoritesLoaded();
            string key = GetSelectorFavoriteKey(panel.SelectorKey, info.id);
            if (!selectorFavoriteKeys.Add(key))
            {
                selectorFavoriteKeys.Remove(key);
            }

            selectorFavoriteVersion++;
            panel.FolderInfoCount = -1;
            selectorRenderRangePool.Clear();
            selectorSearchPool.Clear();
            SaveSelectorFavorites();
        }

        private static string GetSelectorFavoriteKey(string selectorKey, int id)
        {
            return GetSelectorFavoriteScope(selectorKey) + "|" + id;
        }

        private static string GetSelectorFavoriteScope(string selectorKey)
        {
            if (string.IsNullOrEmpty(selectorKey))
            {
                return string.Empty;
            }

            if (selectorKey.StartsWith(CharaEditorController.CT1_ACCS + "#", StringComparison.Ordinal) &&
                selectorKey.EndsWith("#Acc ID", StringComparison.Ordinal))
            {
                return CharaEditorController.CT1_ACCS + "#Acc ID";
            }

            return selectorKey;
        }

        private void EnsureSelectorFavoritesLoaded()
        {
            if (selectorFavoritesLoaded)
            {
                return;
            }

            selectorFavoritesLoaded = true;
            selectorFavoriteKeys.Clear();
            string path = GetSelectorFavoritesPath();
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i]?.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        selectorFavoriteKeys.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to load StudioCharaEditor favorites: " + ex.Message);
                }
            }
        }

        private void SaveSelectorFavorites()
        {
            try
            {
                File.WriteAllLines(GetSelectorFavoritesPath(), selectorFavoriteKeys.OrderBy(key => key).ToArray());
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to save StudioCharaEditor favorites: " + ex.Message);
                }
            }
        }

        private static string GetSelectorFavoritesPath()
        {
            return Path.Combine(CharaEditorMgr.GetDllPath(), "HS2StudioCharaEditorFavorites.txt");
        }

        private void HandleSelectorContextMenuOutsideClick()
        {
            Event evt = Event.current;
            if (selectorContextMenu == null || evt.type != EventType.MouseDown)
            {
                return;
            }

            if (!selectorContextMenu.Rect.Contains(evt.mousePosition))
            {
                selectorContextMenu = null;
            }
        }

        private bool IsSelectorContextMenuBlockingCurrentMouseEvent()
        {
            if (selectorContextMenu == null)
            {
                return false;
            }

            Event evt = Event.current;
            if (evt.type != EventType.MouseDown &&
                evt.type != EventType.MouseUp &&
                evt.type != EventType.MouseDrag &&
                evt.type != EventType.ScrollWheel)
            {
                return false;
            }

            return selectorContextMenu.Rect.Contains(evt.mousePosition);
        }

        private static bool DrawSelectorManualButton(Rect rect, GUIContent content, bool isOn)
        {
            Event evt = Event.current;
            bool containsMouse = rect.Contains(evt.mousePosition);
            if (evt.type == EventType.Repaint)
            {
                GUI.skin.button.Draw(rect, content, containsMouse, false, isOn, false);
            }

            if (GUI.enabled && evt.type == EventType.MouseDown && evt.button == 0 && containsMouse)
            {
                evt.Use();
                return true;
            }

            return false;
        }

        private static bool ConsumeSelectorRightClick(Rect rect)
        {
            Event evt = Event.current;
            if (!GUI.enabled || evt.type != EventType.MouseDown || evt.button != 1 || !rect.Contains(evt.mousePosition))
            {
                return false;
            }

            evt.Use();
            return true;
        }

        private void OpenSelectorItemContextMenu(SelectorSidePanel panel, CustomSelectInfo info)
        {
            if (panel == null || info == null)
            {
                return;
            }

            string scope = GetSelectorFavoriteScope(panel.SelectorKey);
            selectorContextMenu = new SelectorContextMenu
            {
                Type = SelectorContextMenuType.Item,
                Position = GUIUtility.GUIToScreenPoint(Event.current.mousePosition),
                Scope = scope,
                Item = info
            };
        }

        private void OpenSelectorFolderContextMenu(SelectorSidePanel panel, SelectorFolderInfo folder)
        {
            if (panel == null || folder == null)
            {
                return;
            }

            string scope = GetSelectorFavoriteScope(panel.SelectorKey);
            string folderName = GetCustomFolderNameFromKey(folder.Key);
            selectorContextMenu = new SelectorContextMenu
            {
                Type = SelectorContextMenuType.Folder,
                Position = GUIUtility.GUIToScreenPoint(Event.current.mousePosition),
                Scope = scope,
                FolderKey = folder.Key,
                FolderName = folderName,
                RenameFolderName = folderName
            };
        }

        private void DrawSelectorContextMenu(SelectorSidePanel panel)
        {
            if (selectorContextMenu == null || panel == null)
            {
                return;
            }

            const float menuWidth = 220f;
            float menuHeight = selectorContextMenu.Type == SelectorContextMenuType.Item ? 260f : 210f;
            Vector2 menuPosition = GUIUtility.ScreenToGUIPoint(selectorContextMenu.Position);
            float x = Mathf.Clamp(menuPosition.x, 4f, Math.Max(4f, selectorWindowRect.width - menuWidth - 4f));
            float y = Mathf.Clamp(menuPosition.y, 24f, Math.Max(24f, selectorWindowRect.height - menuHeight - 4f));
            selectorContextMenu.Rect = new Rect(x, y, menuWidth, menuHeight);

            GUILayout.BeginArea(selectorContextMenu.Rect, GUI.skin.box);
            if (selectorContextMenu.Type == SelectorContextMenuType.Item)
            {
                DrawSelectorItemContextMenu(panel, selectorContextMenu);
            }
            else
            {
                DrawSelectorFolderContextMenu(panel, selectorContextMenu);
            }
            GUILayout.EndArea();
        }

        private void DrawSelectorItemContextMenu(SelectorSidePanel panel, SelectorContextMenu menu)
        {
            CustomSelectInfo item = menu.Item;
            if (item == null)
            {
                selectorContextMenu = null;
                return;
            }

            GUILayout.Label("#" + item.id, GUI.skin.box);
            bool favorite = IsSelectorFavorite(panel.SelectorKey, item);
            if (GUILayout.Button(favorite ? LC("Remove favorite") : LC("Add favorite")))
            {
                ToggleSelectorFavorite(panel, item);
            }

            GUILayout.Label(LC("Folders"), GUI.skin.box);
            List<SelectorCustomFolder> folders = GetCustomFolders(menu.Scope);
            for (int i = 0; i < folders.Count; i++)
            {
                SelectorCustomFolder folder = folders[i];
                bool contains = SelectorFolderContainsItem(folder, panel.ChaCtrl, item);
                string label = (contains ? "- " : "+ ") + folder.Name;
                if (GUILayout.Button(label))
                {
                    if (contains)
                    {
                        RemoveSelectorFolderItem(folder, panel.ChaCtrl, item);
                    }
                    else
                    {
                        AddSelectorFolderItem(folder, panel.ChaCtrl, item);
                    }

                    MarkSelectorCustomFoldersChanged(panel);
                    SaveSelectorCustomFolders();
                }
            }

            GUILayout.BeginHorizontal();
            menu.NewFolderName = GUILayout.TextField(menu.NewFolderName ?? string.Empty);
            if (GUILayout.Button(LC("Create"), GUILayout.Width(58)))
            {
                SelectorCustomFolder folder = CreateSelectorCustomFolder(menu.Scope, menu.NewFolderName);
                if (folder != null)
                {
                    AddSelectorFolderItem(folder, panel.ChaCtrl, item);
                    menu.NewFolderName = string.Empty;
                    MarkSelectorCustomFoldersChanged(panel);
                    SaveSelectorCustomFolders();
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(LC("Close")))
            {
                selectorContextMenu = null;
            }
        }

        private void DrawSelectorFolderContextMenu(SelectorSidePanel panel, SelectorContextMenu menu)
        {
            bool isCustomFolder = IsCustomFolderKey(menu.FolderKey);
            GUILayout.Label(isCustomFolder ? menu.FolderName : LC("Folders"), GUI.skin.box);

            if (isCustomFolder)
            {
                GUILayout.BeginHorizontal();
                menu.RenameFolderName = GUILayout.TextField(menu.RenameFolderName ?? string.Empty);
                if (GUILayout.Button(LC("Rename"), GUILayout.Width(68)))
                {
                    if (RenameSelectorCustomFolder(menu.Scope, menu.FolderName, menu.RenameFolderName, panel))
                    {
                        menu.FolderName = NormalizeSelectorFolderName(menu.RenameFolderName);
                        menu.FolderKey = GetCustomFolderKey(menu.FolderName);
                    }
                }
                GUILayout.EndHorizontal();

                if (GUILayout.Button(LC("Clear items")))
                {
                    SelectorCustomFolder folder = FindSelectorCustomFolder(menu.Scope, menu.FolderName);
                    if (folder != null)
                    {
                        folder.ItemKeys.Clear();
                        folder.LegacyItemIds.Clear();
                        MarkSelectorCustomFoldersChanged(panel);
                        SaveSelectorCustomFolders();
                    }
                }

                if (GUILayout.Button(LC("Delete folder")))
                {
                    DeleteSelectorCustomFolder(menu.Scope, menu.FolderName, panel);
                    selectorContextMenu = null;
                    return;
                }
            }

            GUILayout.Label(LC("Create folder"), GUI.skin.box);
            GUILayout.BeginHorizontal();
            menu.NewFolderName = GUILayout.TextField(menu.NewFolderName ?? string.Empty);
            if (GUILayout.Button(LC("Create"), GUILayout.Width(58)))
            {
                if (CreateSelectorCustomFolder(menu.Scope, menu.NewFolderName) != null)
                {
                    menu.NewFolderName = string.Empty;
                    MarkSelectorCustomFoldersChanged(panel);
                    SaveSelectorCustomFolders();
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button(LC("Close")))
            {
                selectorContextMenu = null;
            }
        }

        private void EnsureSelectorCustomFoldersLoaded()
        {
            if (selectorCustomFoldersLoaded)
            {
                return;
            }

            selectorCustomFoldersLoaded = true;
            selectorCustomFoldersByScope.Clear();
            string path = GetSelectorCustomFoldersPath();
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    string[] parts = line.Split('\t');
                    if (parts.Length < 3)
                    {
                        continue;
                    }

                    string scope = DecodeSelectorFolderField(parts[1]);
                    string name = NormalizeSelectorFolderName(DecodeSelectorFolderField(parts[2]));
                    if (string.IsNullOrEmpty(scope) || string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    SelectorCustomFolder folder = GetOrCreateSelectorCustomFolder(scope, name);
                    if (parts[0] == "itemkey" && parts.Length >= 4)
                    {
                        string itemKey = DecodeSelectorFolderField(parts[3]);
                        if (!string.IsNullOrEmpty(itemKey))
                        {
                            folder.ItemKeys.Add(itemKey);
                        }
                    }
                    else if (parts[0] == "item" && parts.Length >= 4 && int.TryParse(parts[3], out int id))
                    {
                        folder.LegacyItemIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to load StudioCharaEditor custom folders: " + ex.Message);
                }
            }
        }

        private void SaveSelectorCustomFolders()
        {
            try
            {
                List<string> lines = new List<string>();
                foreach (KeyValuePair<string, List<SelectorCustomFolder>> pair in selectorCustomFoldersByScope.OrderBy(pair => pair.Key))
                {
                    foreach (SelectorCustomFolder folder in pair.Value.OrderBy(folder => folder.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        string scope = EncodeSelectorFolderField(pair.Key);
                        string name = EncodeSelectorFolderField(folder.Name);
                        lines.Add("folder\t" + scope + "\t" + name);
                        foreach (string itemKey in folder.ItemKeys.OrderBy(key => key, StringComparer.Ordinal))
                        {
                            lines.Add("itemkey\t" + scope + "\t" + name + "\t" + EncodeSelectorFolderField(itemKey));
                        }
                        foreach (int id in folder.LegacyItemIds.OrderBy(id => id))
                        {
                            lines.Add("item\t" + scope + "\t" + name + "\t" + id);
                        }
                    }
                }

                File.WriteAllLines(GetSelectorCustomFoldersPath(), lines.ToArray());
            }
            catch (Exception ex)
            {
                if (StudioCharaEditor.VerboseMessage.Value)
                {
                    StudioCharaEditor.Logger?.LogWarning("Failed to save StudioCharaEditor custom folders: " + ex.Message);
                }
            }
        }

        private List<SelectorCustomFolder> GetCustomFolders(string scope)
        {
            EnsureSelectorCustomFoldersLoaded();
            if (!selectorCustomFoldersByScope.TryGetValue(scope, out List<SelectorCustomFolder> folders))
            {
                folders = new List<SelectorCustomFolder>();
                selectorCustomFoldersByScope[scope] = folders;
            }
            return folders;
        }

        private SelectorCustomFolder CreateSelectorCustomFolder(string scope, string rawName)
        {
            string name = NormalizeSelectorFolderName(rawName);
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            SelectorCustomFolder existing = FindSelectorCustomFolder(scope, name);
            if (existing != null)
            {
                return existing;
            }

            SelectorCustomFolder folder = GetOrCreateSelectorCustomFolder(scope, name);
            selectorCustomFolderVersion++;
            return folder;
        }

        private SelectorCustomFolder GetOrCreateSelectorCustomFolder(string scope, string name)
        {
            List<SelectorCustomFolder> folders = GetCustomFolders(scope);
            SelectorCustomFolder folder = folders.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            if (folder != null)
            {
                return folder;
            }

            folder = new SelectorCustomFolder
            {
                Scope = scope,
                Name = name
            };
            folders.Add(folder);
            return folder;
        }

        private SelectorCustomFolder FindSelectorCustomFolder(string scope, string name)
        {
            return GetCustomFolders(scope).FirstOrDefault(folder => string.Equals(folder.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private bool RenameSelectorCustomFolder(string scope, string oldName, string rawNewName, SelectorSidePanel panel)
        {
            string newName = NormalizeSelectorFolderName(rawNewName);
            if (string.IsNullOrEmpty(newName))
            {
                return false;
            }

            SelectorCustomFolder folder = FindSelectorCustomFolder(scope, oldName);
            if (folder == null)
            {
                return false;
            }

            SelectorCustomFolder existing = FindSelectorCustomFolder(scope, newName);
            if (existing != null && !ReferenceEquals(existing, folder))
            {
                foreach (string itemKey in folder.ItemKeys)
                {
                    existing.ItemKeys.Add(itemKey);
                }
                foreach (int id in folder.LegacyItemIds)
                {
                    existing.LegacyItemIds.Add(id);
                }
                GetCustomFolders(scope).Remove(folder);
            }
            else
            {
                folder.Name = newName;
            }

            if (panel != null && string.Equals(panel.SelectedFolderKey, GetCustomFolderKey(oldName), StringComparison.OrdinalIgnoreCase))
            {
                panel.SelectedFolderKey = GetCustomFolderKey(newName);
            }
            MarkSelectorCustomFoldersChanged(panel);
            SaveSelectorCustomFolders();
            return true;
        }

        private void DeleteSelectorCustomFolder(string scope, string folderName, SelectorSidePanel panel)
        {
            SelectorCustomFolder folder = FindSelectorCustomFolder(scope, folderName);
            if (folder != null)
            {
                GetCustomFolders(scope).Remove(folder);
                if (panel != null && string.Equals(panel.SelectedFolderKey, GetCustomFolderKey(folderName), StringComparison.OrdinalIgnoreCase))
                {
                    panel.SelectedFolderKey = SelectorFolderAllKey;
                }
                MarkSelectorCustomFoldersChanged(panel);
                SaveSelectorCustomFolders();
            }
        }

        private void MarkSelectorCustomFoldersChanged(SelectorSidePanel panel)
        {
            selectorCustomFolderVersion++;
            if (panel != null)
            {
                panel.FolderInfoCount = -1;
                panel.PendingScrollToSelected = true;
            }
            selectorRenderRangePool.Clear();
            selectorSearchPool.Clear();
        }

        private static bool SelectorFolderContainsItem(SelectorCustomFolder folder, ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (folder == null || info == null)
            {
                return false;
            }

            return folder.ItemKeys.Any(key => SelectorItemKeyMatches(key, chaCtrl, info)) || folder.LegacyItemIds.Contains(info.id);
        }

        private static void AddSelectorFolderItem(SelectorCustomFolder folder, ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (folder == null || info == null)
            {
                return;
            }

            RemoveSelectorFolderItemKeys(folder, chaCtrl, info);
            folder.ItemKeys.Add(GetSelectorItemKey(chaCtrl, info));
            folder.LegacyItemIds.Remove(info.id);
        }

        private static void RemoveSelectorFolderItem(SelectorCustomFolder folder, ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (folder == null || info == null)
            {
                return;
            }

            RemoveSelectorFolderItemKeys(folder, chaCtrl, info);
            folder.LegacyItemIds.Remove(info.id);
        }

        private static void RemoveSelectorFolderItemKeys(SelectorCustomFolder folder, ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (folder?.ItemKeys == null || info == null)
            {
                return;
            }

            List<string> keysToRemove = folder.ItemKeys.Where(key => SelectorItemKeyMatches(key, chaCtrl, info)).ToList();
            for (int i = 0; i < keysToRemove.Count; i++)
            {
                folder.ItemKeys.Remove(keysToRemove[i]);
            }
        }

        private static string GetSelectorItemKey(ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (info == null)
            {
                return string.Empty;
            }

            string listInfoKey = GetSelectorListInfoKey(chaCtrl, info);
            if (!string.IsNullOrEmpty(listInfoKey))
            {
                return listInfoKey;
            }

            return string.Join("|", new[]
            {
                info.category.ToString(),
                info.id.ToString()
            });
        }

        private static bool SelectorItemKeyMatches(string storedKey, ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (info == null || string.IsNullOrEmpty(storedKey))
            {
                return false;
            }

            if (string.Equals(storedKey, GetSelectorItemKey(chaCtrl, info), StringComparison.Ordinal))
            {
                return true;
            }

            string[] parts = storedKey.Split('|');
            if (parts.Length < 2)
            {
                return false;
            }

            if (parts[0] == info.category.ToString() &&
                parts[1] == info.id.ToString())
            {
                return true;
            }

            return SelectorLegacyAssetKeyMatches(parts, chaCtrl, info);
        }

        private static string GetSelectorListInfoKey(ChaControl chaCtrl, CustomSelectInfo info)
        {
            ListInfoBase listInfo = TryGetSelectorListInfo(chaCtrl, info);
            if (listInfo == null)
            {
                return null;
            }

            string mainManifest = GetListInfoString(listInfo, KeyType.MainManifest);
            string mainAb = GetListInfoString(listInfo, KeyType.MainAB);
            string mainData = GetListInfoString(listInfo, KeyType.MainData);
            string mainData02 = GetListInfoString(listInfo, KeyType.MainData02);
            string thumbAb = FirstNotEmpty(GetListInfoString(listInfo, KeyType.ThumbAB), info.assetBundle);
            string thumbTex = FirstNotEmpty(GetListInfoString(listInfo, KeyType.ThumbTex), info.assetName);
            string name = FirstNotEmpty(listInfo.Name, info.name);

            bool hasSourceInfo =
                !string.IsNullOrEmpty(mainManifest) ||
                !string.IsNullOrEmpty(mainAb) ||
                !string.IsNullOrEmpty(mainData) ||
                !string.IsNullOrEmpty(mainData02) ||
                !string.IsNullOrEmpty(thumbAb) ||
                !string.IsNullOrEmpty(thumbTex);
            if (hasSourceInfo)
            {
                return string.Join("|", new[]
                {
                    "list",
                    info.category.ToString(),
                    EncodeSelectorKeyPart(mainManifest),
                    EncodeSelectorKeyPart(mainAb),
                    EncodeSelectorKeyPart(mainData),
                    EncodeSelectorKeyPart(mainData02),
                    EncodeSelectorKeyPart(thumbAb),
                    EncodeSelectorKeyPart(thumbTex)
                });
            }

            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            return string.Join("|", new[]
            {
                "name",
                info.category.ToString(),
                EncodeSelectorKeyPart(name)
            });
        }

        private static ListInfoBase TryGetSelectorListInfo(ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (chaCtrl?.lstCtrl == null || info == null)
            {
                return null;
            }

            try
            {
                return chaCtrl.lstCtrl.GetListInfo((CategoryNo)info.category, info.id);
            }
            catch
            {
                return null;
            }
        }

        private static string GetListInfoString(ListInfoBase listInfo, KeyType keyType)
        {
            if (listInfo == null)
            {
                return string.Empty;
            }

            try
            {
                return listInfo.GetInfo(keyType) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool SelectorLegacyAssetKeyMatches(string[] parts, ChaControl chaCtrl, CustomSelectInfo info)
        {
            if (parts.Length < 4 || parts[0] != info.category.ToString())
            {
                return false;
            }

            string storedBundle = DecodeSelectorKeyPart(parts[2]);
            string storedAsset = DecodeSelectorKeyPart(parts[3]);
            if (string.IsNullOrEmpty(storedBundle) && string.IsNullOrEmpty(storedAsset))
            {
                return false;
            }

            ListInfoBase listInfo = TryGetSelectorListInfo(chaCtrl, info);
            string[] bundleCandidates =
            {
                info.assetBundle,
                GetListInfoString(listInfo, KeyType.ThumbAB),
                GetListInfoString(listInfo, KeyType.MainAB)
            };
            string[] assetCandidates =
            {
                info.assetName,
                GetListInfoString(listInfo, KeyType.ThumbTex),
                GetListInfoString(listInfo, KeyType.MainData),
                GetListInfoString(listInfo, KeyType.MainData02)
            };

            return
                SelectorValueMatchesAny(storedBundle, bundleCandidates) &&
                SelectorValueMatchesAny(storedAsset, assetCandidates);
        }

        private static bool SelectorValueMatchesAny(string value, IEnumerable<string> candidates)
        {
            string normalizedValue = NormalizeSelectorKeyValue(value);
            if (string.IsNullOrEmpty(normalizedValue))
            {
                return false;
            }

            return candidates.Any(candidate => string.Equals(normalizedValue, NormalizeSelectorKeyValue(candidate), StringComparison.Ordinal));
        }

        private static string EncodeSelectorKeyPart(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(NormalizeSelectorKeyValue(value)));
        }

        private static string DecodeSelectorKeyPart(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeSelectorKeyValue(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string FirstNotEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                {
                    return values[i];
                }
            }

            return string.Empty;
        }

        private static string NormalizeSelectorFolderName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return string.Empty;
            }

            string name = rawName.Trim().Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
            while (name.Contains("  "))
            {
                name = name.Replace("  ", " ");
            }
            if (name.Length > 32)
            {
                name = name.Substring(0, 32).Trim();
            }

            if (string.Equals(name, "All", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Fav", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "Favorites", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return name;
        }

        private static string GetCustomFolderKey(string folderName)
        {
            return SelectorFolderCustomPrefix + folderName;
        }

        private static bool IsCustomFolderKey(string folderKey)
        {
            return !string.IsNullOrEmpty(folderKey) && folderKey.StartsWith(SelectorFolderCustomPrefix, StringComparison.Ordinal);
        }

        private static string GetCustomFolderNameFromKey(string folderKey)
        {
            return IsCustomFolderKey(folderKey) ? folderKey.Substring(SelectorFolderCustomPrefix.Length) : string.Empty;
        }

        private static string EncodeSelectorFolderField(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string DecodeSelectorFolderField(string value)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? string.Empty));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetSelectorCustomFoldersPath()
        {
            return Path.Combine(CharaEditorMgr.GetDllPath(), "HS2StudioCharaEditorFolders.txt");
        }

        private void DrawSelectorSideThumbRow(SelectorSidePanel panel, CustomSelectInfo info, int selectedId)
        {
            Color color = GUI.color;
            Texture2D texture = GetSelectorThumbTexture(panel.Name, info);
            GUILayout.BeginHorizontal();
            GUILayout.Box(texture, GUILayout.Width(SelectorPanelThumbSize), GUILayout.Height(SelectorPanelThumbSize));
            GUILayout.BeginVertical();
            GUILayout.Space(Math.Max(0f, (SelectorPanelThumbSize - SelectorPanelButtonHeight) * 0.5f));
            GUILayout.BeginHorizontal();
            DrawSelectorFavoriteButton(panel, info, SelectorPanelButtonHeight);
            Color buttonColor = GUI.color;
            GUIContent content = new GUIContent(string.Format("#{0}:\n{1}", info.id, info.name));
            Rect itemRect = GUILayoutUtility.GetRect(content, GUI.skin.button, GUILayout.Height(SelectorPanelButtonHeight), GUILayout.ExpandWidth(true));
            if (info.id == selectedId)
            {
                GUI.color = Color.green;
            }
            if (DrawSelectorManualButton(itemRect, content, info.id == selectedId))
            {
                ChangeSelectorSidePanelId(panel, info.id);
            }
            else if (ConsumeSelectorRightClick(itemRect))
            {
                OpenSelectorItemContextMenu(panel, info);
            }
            GUI.color = buttonColor;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUI.color = color;
        }

        private void DrawSelectorSideTextRow(SelectorSidePanel panel, CustomSelectInfo info, int selectedId)
        {
            Color color = GUI.color;
            GUILayout.BeginHorizontal();
            DrawSelectorFavoriteButton(panel, info, 20f);
            Color buttonColor = GUI.color;
            GUIContent content = new GUIContent(string.Format("#{0}: {1}", info.id, info.name));
            Rect itemRect = GUILayoutUtility.GetRect(content, GUI.skin.button, GUILayout.ExpandWidth(true));
            if (info.id == selectedId)
            {
                GUI.color = Color.green;
            }
            if (DrawSelectorManualButton(itemRect, content, info.id == selectedId))
            {
                ChangeSelectorSidePanelId(panel, info.id);
            }
            else if (ConsumeSelectorRightClick(itemRect))
            {
                OpenSelectorItemContextMenu(panel, info);
            }
            GUI.color = buttonColor;
            GUILayout.EndHorizontal();
            GUI.color = color;
        }

        private void ChangeSelectorSidePanelId(SelectorSidePanel panel, int id)
        {
            int oldId = Convert.ToInt32(panel.DetailInfo.DetailDefine.Get(panel.ChaCtrl));
            if (id == oldId)
            {
                if (StudioCharaEditor.CloseListAfterSelect.Value)
                {
                    CloseSelectorSidePanel();
                }
                return;
            }

            panel.DetailInfo.DetailDefine.Set(panel.ChaCtrl, id);
            ClearSelectorCache();
            if (panel.DetailInfo.DetailDefine.Upd != null && !LaterUpdate)
            {
                panel.DetailInfo.DetailDefine.Upd(panel.ChaCtrl);
            }

            if (StudioCharaEditor.CloseListAfterSelect.Value)
            {
                CloseSelectorSidePanel();
            }
        }

        private void OpenSelectorSidePanel(ChaControl chaCtrl, string name, CharaDetailInfo dInfo, int selectedIndex, bool thumbList, float rowHeight)
        {
            string selectorKey = dInfo.DetailDefine.Key;
            if (!thumbPool.ContainsKey(name))
            {
                thumbPool[name] = new Dictionary<string, Texture2D>();
            }

            selectorSidePanel = new SelectorSidePanel
            {
                Name = name,
                SelectorKey = selectorKey,
                ChaCtrl = chaCtrl,
                DetailInfo = dInfo,
                ThumbList = thumbList,
                SearchText = string.Empty,
                Scroll = new Vector2(0f, Math.Max(0, selectedIndex) * rowHeight + ThumbListRowGap),
                PendingScrollToSelected = true
            };
            ClearSelectorRuntimeCache(selectorKey);
        }

        private bool IsSelectorSidePanelOpen(string selectorKey)
        {
            return selectorSidePanel != null && selectorSidePanel.SelectorKey == selectorKey;
        }

        private void CloseSelectorSidePanel(string selectorKey)
        {
            if (IsSelectorSidePanelOpen(selectorKey))
            {
                CloseSelectorSidePanel();
            }
        }

        private void CloseSelectorSidePanel()
        {
            selectorSidePanel = null;
            selectorContextMenu = null;
        }

        private void OnSelectChange(TreeNodeObject newSel)
        {
            lastSelectedTreeNode = newSel;
            ociTarget = GetOCICharFromNode(newSel);
            CloseSelectorSidePanel();
            ClearSelectorCache();
            //Console.WriteLine("Select change to {0}", ociTarget);
        }

        protected TreeNodeObject GetCurrentSelectedNode()
        {
            return Studio.Studio.Instance.treeNodeCtrl.selectNode;
        }

        protected OCIChar GetOCICharFromNode(TreeNodeObject node)
        {
            if (node == null) return null;

            var dic = Studio.Studio.Instance.dicInfo;
            if (dic.ContainsKey(node))
            {
                ObjectCtrlInfo oci = dic[node];
                if (oci is OCIChar)
                {
                    return oci as OCIChar;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        private void guiEditorMain()
        {
            float fullw = windowRect.width - 20;
            float fullh = windowRect.height - 20;
            float leftw = 150;
            float rightw = fullw - 8 - leftw - 5;

            CharaEditorController cec = CharaEditorMgr.Instance.GetEditorController(ociTarget);
            if (ociTarget == null || cec == null)
            {
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("<color=#00ffff>" + LC("Please select a charactor to edit.") + "</color>", largeLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.FlexibleSpace();
            }
            else
            {
                // charactor selected
                string curDetailSetKey = null;

                GUILayout.BeginHorizontal();

                // LEFT area
                GUILayout.BeginVertical(GUILayout.Width(leftw + 8));

                // catelog1 select
                GUILayout.BeginHorizontal();
                for (int c1 = 0; c1 < CharaEditorController.CATEGORY1.Length; c1++)
                {
                    if (c1 == 3)
                    {
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    string title = LC(CharaEditorController.CATEGORY1[c1]);
                    Color color = GUI.color;
                    if (catelogIndex1 == c1)
                        GUI.color = Color.green;
                    if (GUILayout.Button(title))
                    {
                        catelogIndex1 = c1;
                        detailPageSelect = SelectMode.Normal;
                    }
                    GUI.color = color;
                }
                GUILayout.EndHorizontal();

                // catelog2 select
                leftScroll = GUILayout.BeginScrollView(leftScroll, GUI.skin.box);
                string category1 = CharaEditorController.CATEGORY1[catelogIndex1];
                string category2 = null;
                string[] category2List = cec.GetCategoryList(category1);
                for (int c2 = 0; c2 < category2List.Length; c2++)
                {
                    string title = category2List[c2];
                    if (title.StartsWith("=="))
                    {
                        // seperator
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(LC(title));
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    else if (title.StartsWith("++"))
                    {
                        // toggle
                        string cTitle = title.Substring(2);
                        string checkKey = category1 + "#" + cTitle;
                        if (cec.Category2GetFuncDict.ContainsKey(checkKey))
                        {
                            bool oldV = (bool)cec.Category2GetFuncDict[checkKey](cec);
                            bool newV = DrawModernToggle(oldV, LC(title));
                            if (oldV != newV)
                            {
                                cec.Category2SetFuncDict[checkKey](cec, newV);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Unknown ++{0}", checkKey);
                        }
                    }
                    else
                    {
                        // selectable button
                        string detailSetKey = category1 + "#" + title;
                        // color and init
                        Color color = GUI.color;
                        if (catelogIndex2[catelogIndex1] == c2)
                        {
                            GUI.color = Color.green;
                            category2 = title;
                            curDetailSetKey = detailSetKey;
                        }
                        else if (accSlotMultiSelection.Contains(title))
                        {
                            GUI.color = Color.yellow;
                        }
                        // title and style by catelog
                        if (catelogIndex1 == 3)
                        {
                            title = cec.GetClothDispName(title);
                            categoryButtonStyle.alignment = TextAnchor.MiddleCenter;
                        }
                        else if (catelogIndex1 == 4)
                        {
                            if (accSlotMultiSelection.Count == 0) accSlotMultiSelection.Add(title);
                            title = cec.GetAccessoryInfoByKey(title)?.AccName;
                            categoryButtonStyle.alignment = TextAnchor.MiddleLeft;
                        }
                        else
                        {
                            categoryButtonStyle.alignment = TextAnchor.MiddleCenter;
                        }
                        if (GUILayout.Button(LC(title), categoryButtonStyle))
                        {
                            catelogIndex2[catelogIndex1] = c2;
                            detailPageSelect = SelectMode.Normal;
                            // accessory multi selection
                            if (catelogIndex1 == 4)
                            {
                                string accKey = category2List[c2];
                                if (Event.current.shift)
                                {
                                    // add from last selection
                                    int lastC2 = Array.IndexOf(category2List, accSlotMultiSelection[accSlotMultiSelection.Count - 1]);
                                    if (lastC2 < 0)
                                    {
                                        lastC2 = c2;
                                    }
                                    if (lastC2 != c2)
                                    {
                                        int sFrom = c2 < lastC2 ? c2 : lastC2;
                                        int sTo = c2 < lastC2 ? lastC2 : c2;
                                        for (int s = sFrom; s <= sTo; s++)
                                        {
                                            if (category2List[s].StartsWith("=="))
                                            {
                                                continue;
                                            }
                                            if (!accSlotMultiSelection.Contains(category2List[s]))
                                            {
                                                accSlotMultiSelection.Add(category2List[s]);
                                            }
                                        }
                                    }
                                }
                                else if (Event.current.control)
                                {
                                    // add one slot
                                    if (!accSlotMultiSelection.Contains(accKey))
                                    {
                                        accSlotMultiSelection.Add(accKey);
                                    }
                                }
                                else
                                {
                                    // one slot
                                    accSlotMultiSelection.Clear();
                                    accSlotMultiSelection.Add(accKey);
                                }
                            }
                        }
                        GUI.color = color;
                    }
                }
                GUILayout.EndScrollView();

                // category operation button
                GUILayout.BeginVertical(GUI.skin.box);
                if (catelogIndex1 == 4)
                {
                    // accessory sort mode
                    GUILayout.BeginHorizontal();
                    bool accSortMode = DrawModernToggle(cec.accSortByParent, LC("Sort by parent"));
                    if (accSortMode != cec.accSortByParent)
                    {
                        cec.accSortByParent = accSortMode;
                        cec.RefreshAccessoriesList();
                        ClearSelectorCache();
                    }
                    GUILayout.EndHorizontal();
                    // More Accessories add slot command
                    if (PluginMoreAccessories.HasMoreAccessories)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(LC("+1 Slot")))
                        {
                            PluginMoreAccessories.AddOneAccessorySlot(cec.ociTarget.charInfo);
                            cec.RefreshAccessoriesList();
                            ClearSelectorCache();
                        }
                        if (GUILayout.Button(LC("+10 Slots")))
                        {
                            PluginMoreAccessories.AddTenAccessorySlots(cec.ociTarget.charInfo);
                            cec.RefreshAccessoriesList();
                            ClearSelectorCache();
                        }
                        GUILayout.EndHorizontal();
                    }
                    // copy/paste accessories between slots
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button(LC("Copy Slot")))
                    {
                        accSlotMultiSelection.Sort(CompareSlotNo);
                        //Console.WriteLine("AccSlotMultiSelection to copy: " + string.Join(",", accSlotMultiSelection));
                        // copy to clipboard
                        accSlotClipboard.Clear();
                        foreach (string accKey in accSlotMultiSelection)
                        {
                            accSlotClipboard.Add(cec.GetAccessoryDetailData(accKey));
                        }
                    }
                    if (GUILayout.Button(LC("Paste Slot")) && accSlotClipboard != null)
                    {
                        accSlotMultiSelection.Sort(CompareSlotNo);
                        //Console.WriteLine("AccSlotMultiSelection to paste: " + string.Join(",", accSlotMultiSelection));
                        // change to paste mode
                        detailPageSelect = SelectMode.PasteSlotPrompt;
                    }
                    GUILayout.EndHorizontal();
                }
                if (GUILayout.Button(LC("Copy") + " " + LC(category1)))
                {
                    List<string> tgtKeys = new List<string>();
                    for (int c2 = 0; c2 < category2List.Length; c2++)
                    {
                        string c2name = category2List[c2];
                        if (c2name.StartsWith("==") || c2name.StartsWith("++")) continue;
                        foreach (CharaDetailInfo cdi in cec.GetDetailInfoList(category1, c2name))
                        {
                            tgtKeys.Add(cdi.DetailDefine.Key);
                        }
                    }
                    clipboard = cec.GetDataDictByKeys(tgtKeys.ToArray());
                }
                GUILayout.EndVertical();
                GUILayout.EndVertical();

                // RIGHT area
                if (curDetailSetKey != null)
                {
                    GUILayout.BeginVertical(GUILayout.Width(rightw));

                    // chara name editor line
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (renameMode)
                        tempCharaName = GUILayout.TextField(tempCharaName, GUILayout.Width(200));
                    else
                        GUILayout.Label(magentaText(ociTarget.treeNodeObject.textName) + greenText(" > " + LC(category1) + " > " + LC(category2)));
                    GUILayout.FlexibleSpace();
                    if (renameMode)
                    {
                        if (GUILayout.Button(LC("OK")))
                        {
                            ociTarget.treeNodeObject.textName = tempCharaName;
                            ociTarget.charInfo.chaFile.parameter.fullname = tempCharaName;
                            renameMode = false;
                        }
                        if (GUILayout.Button(LC("Cancel")))
                        {
                            renameMode = false;
                        }
                    }
                    else
                    {
                        if (GUILayout.Button(LC("Rename Chara")))
                        {
                            tempCharaName = ociTarget.treeNodeObject.textName;
                            renameMode = true;
                        }
                    }
                    GUILayout.EndHorizontal();

                    // chara detials editor
                    if (detailPageSelect == SelectMode.PasteSlotPrompt)
                    {
                        // local function
                        string getNewEmptySlot(List<string> selectedSlotKeys, List<string> registedSlotKeys)
                        {
                            int sFrom = int.Parse(selectedSlotKeys[0]);
                            for (; ; sFrom++)
                            {
                                string accKey = sFrom.ToString();
                                // pass selected slot
                                if (selectedSlotKeys.Contains(accKey))
                                {
                                    continue;
                                }
                                // pass registed slot
                                if (registedSlotKeys.Contains(accKey))
                                {
                                    continue;
                                }
                                // pass non-empty slot
                                AccessoryInfo accInfo = cec.GetAccessoryInfoByKey(accKey);
                                if (accInfo != null && !accInfo.IsEmptySlot)
                                {
                                    continue;
                                }
                                // select this slot
                                return accKey;
                            }
                        }

                        // build target slot info
                        List<string> tgtSlotKeys = new List<string>();
                        for (int i = 0; i < accSlotClipboard.Count; i++)
                        {
                            if (i < accSlotMultiSelection.Count)
                            {
                                AccessoryInfo accInfo = cec.GetAccessoryInfoByKey(accSlotMultiSelection[i]);
                                if (!accInfo.IsEmptySlot && copySlotAutoArrange)
                                {
                                    // non-empty slot, arrange a new one
                                    tgtSlotKeys.Add(getNewEmptySlot(accSlotMultiSelection, tgtSlotKeys));
                                }
                                else
                                {
                                    // empty slot or overwrite allowed, copy to selected one
                                    tgtSlotKeys.Add(accSlotMultiSelection[i]);
                                }
                            }
                            else if (copySlotAutoArrange)
                            {
                                // not enough tgt slot selected, arrange a new one
                                tgtSlotKeys.Add(getNewEmptySlot(accSlotMultiSelection, tgtSlotKeys));
                            }
                            else
                            {
                                // target slot less then source slot
                                break;
                            }
                        }

                        // paste slot prompt mode, show copy info
                        rightScroll = GUILayout.BeginScrollView(rightScroll, GUI.skin.box);
                        GUILayout.Label(LC("Copy/paste accessory between slot:"));
                        int newSlotCount = 0;
                        for (int i = 0; i < tgtSlotKeys.Count; i++)
                        {
                            AccessoryInfo accInfo = cec.GetAccessoryInfoByKey(tgtSlotKeys[i]);
                            string tgtSlotName;
                            if (accInfo == null)
                            {
                                int nsIndex = int.Parse(tgtSlotKeys[i]);
                                if (PluginMoreAccessories.HasMoreAccessories)
                                {
                                    // new slot
                                    tgtSlotName = cyanText("new slot " + (nsIndex + 1).ToString());
                                    newSlotCount++;
                                }
                                else
                                {
                                    // no more slot
                                    tgtSlotName = redText(LC("No more slot! MoreAccessories not found?!"));
                                }
                            }
                            else
                            {
                                if (accInfo.IsEmptySlot)
                                {
                                    // copy to empty
                                    tgtSlotName = greenText(accInfo.AccName);
                                }
                                else
                                {
                                    // copy overwrite
                                    tgtSlotName = magentaText(accInfo.AccName);
                                }
                            }
                            GUILayout.Label("  " + accSlotClipboard[i].accInfo.AccName + " -> " + tgtSlotName);
                        }
                        GUILayout.EndScrollView();

                        // detail page copy/paste
                        GUILayout.BeginVertical(GUI.skin.box);
                        copySlotAutoArrange = DrawModernToggle(copySlotAutoArrange, LC("Auto arrange empty slot, create new if needed"));
                        GUILayout.BeginHorizontal();
                        copySlotMirrorParent = DrawModernToggle(copySlotMirrorParent, LC("Mirror accessory parent"));
                        copySlotMirrorAdjust = DrawModernToggle(copySlotMirrorAdjust, LC("Mirror accessory adjustment"));
                        GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button(LC("OK")))
                        {
                            // check and create new slots
                            if (newSlotCount > 0)
                            {
                                int need10 = (newSlotCount - 1) / 10 + 1;
                                for (int i = 0; i < need10; i ++)
                                {
                                    PluginMoreAccessories.AddTenAccessorySlots(cec.ociTarget.charInfo);
                                }
                                cec.RefreshAccessoriesList();
                                ClearSelectorCache();
                            }

                            // copy slots
                            for (int i = 0; i < tgtSlotKeys.Count; i++)
                            {
                                AccessoryInfo accInfo = cec.GetAccessoryInfoByKey(tgtSlotKeys[i]);
                                if (accInfo != null)
                                {
                                    cec.SetAccessoryDetailData(tgtSlotKeys[i], accSlotClipboard[i], copySlotMirrorParent, copySlotMirrorAdjust);
                                }
                                else
                                {
                                    Console.WriteLine($"Skip copy slot {accSlotClipboard[i].accInfo.AccName} to slot #{tgtSlotKeys[i]}, target accessory info not existed.");
                                }
                            }

                            detailPageSelect = SelectMode.Normal;
                        }
                        if (GUILayout.Button(LC("Cancel")))
                        {
                            detailPageSelect = SelectMode.Normal;
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();

                    }
                    else if (cec.myDetailSet.ContainsKey(curDetailSetKey))
                    {
                        CharaDetailInfo[] detailInfoSet = cec.GetDetailInfoList(category1, category2);
                        Dictionary<string, object> pageClipboard = new Dictionary<string, object>();
                        // detail page scroll view
                        rightScroll = GUILayout.BeginScrollView(rightScroll, GUI.skin.box);
                        foreach (CharaDetailInfo dInfo in detailInfoSet)
                        {
                            // setting or selecting mode
                            string dkey = dInfo.DetailDefine.Key;
                            string dname = GetDetailName(dkey);
                            if (detailPageSelect == SelectMode.Normal)
                            {
                                // Setting mode
                                ChaControl chaCtrl = ociTarget.charInfo;
                                switch (dInfo.DetailDefine.Type)
                                {
                                    case CharaDetailDefine.CharaDetailDefineType.SLIDER:
                                        guiRenderSlider(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.COLOR:
                                        guiRenderColor(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.SELECTOR:
                                        guiRenderSelector(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.SEPERATOR:
                                        guiRenderSeperator(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.TOGGLE:
                                        guiRenderToggle(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.VALUEINPUT:
                                        guiRenderValueInput(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.INT_STATUS:
                                        guiRenderIntStatus(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.HAIR_BUNDLE:
                                        guiRenderHairBundle(chaCtrl, curDetailSetKey, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.BUTTON:
                                        guiRenderButton(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.ABMXSET1:
                                    case CharaDetailDefine.CharaDetailDefineType.ABMXSET2:
                                    case CharaDetailDefine.CharaDetailDefineType.ABMXSET3:
                                        guiRenderABMXSet(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.SKIN_OVERLAY:
                                        guiRenderSkinOverlay(chaCtrl, dname, dInfo);
                                        break;
                                    case CharaDetailDefine.CharaDetailDefineType.CLOTH_OVERLAY:
                                        guiRenderClothOverlay(chaCtrl, dname, dInfo);
                                        break;
                                    default:
                                        GUILayout.Label(dname + ": UNKNOWN type not implemented");
                                        break;
                                }
                            }
                            else
                            {
                                // selecting mode
                                if (dInfo.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.SEPERATOR)
                                {
                                    continue;
                                }
                                if (selectBuffer.ContainsKey(dkey))
                                {
                                    selectBuffer[dkey] = DrawModernToggle(selectBuffer[dkey], LC(dname));
                                }
                                else
                                {
                                    GUILayout.Label(greyText("    " + LC(dname)));
                                }
                            }

                            if (clipboard != null && clipboard.ContainsKey(dkey))
                            {
                                pageClipboard[dkey] = clipboard[dkey];
                            }
                        }
                        GUILayout.EndScrollView();

                        // detail page copy/paste
                        GUILayout.BeginHorizontal(GUI.skin.box);
                        if (detailPageSelect == SelectMode.Normal)
                        {
                            if (GUILayout.Button(LC("Copy Page")))
                            {
                                List<string> tgtKeys = new List<string>();
                                foreach (CharaDetailInfo dInfo in detailInfoSet)
                                {
                                    tgtKeys.Add(dInfo.DetailDefine.Key);
                                }
                                clipboard = cec.GetDataDictByKeys(tgtKeys.ToArray());
                            }
                            if (GUILayout.Button(LC("Copy Select")))
                            {
                                detailPageSelect = SelectMode.ForCopy;
                                selectBuffer.Clear();
                                foreach (CharaDetailInfo dInfo in detailInfoSet)
                                {
                                    selectBuffer[dInfo.DetailDefine.Key] = true;
                                }
                            }
                            if (pageClipboard.Count > 0 && GUILayout.Button(LC("Paste Page")))
                            {
                                cec.SetDataDict(pageClipboard);
                            }
                            if (pageClipboard.Count > 0 && GUILayout.Button(LC("Paste Select")))
                            {
                                detailPageSelect = SelectMode.ForPaste;
                                selectBuffer.Clear();
                                foreach (string dkey in pageClipboard.Keys)
                                {
                                    selectBuffer[dkey] = true;
                                }
                            }
                            if (catelogIndex1 == 3 || catelogIndex1 == 4)
                            {
                                Color color = GUI.color;
                                GUI.color = Color.red;
                                if (GUILayout.Button("Clear", GUILayout.ExpandWidth(false)))
                                {
                                    if (catelogIndex1 == 3)
                                    {
                                        cec.ClearClothSlot(category2);
                                    }
                                    else
                                    {
                                        foreach (string accKey in accSlotMultiSelection)
                                        {
                                            cec.ClearAccessorySlot(accKey);
                                        }
                                    }
                                }
                                GUI.color = color;
                            }
                        }
                        else if (detailPageSelect == SelectMode.ForCopy)
                        {
                            if (GUILayout.Button(LC("Copy Selected Data")))
                            {
                                List<string> tgtKeys = new List<string>();
                                foreach (string dkey in selectBuffer.Keys)
                                {
                                    if (selectBuffer[dkey])
                                    {
                                        tgtKeys.Add(dkey);
                                    }
                                }
                                clipboard = cec.GetDataDictByKeys(tgtKeys.ToArray());
                                detailPageSelect = SelectMode.Normal;
                            }
                            if (GUILayout.Button(LC("Cancel")))
                            {
                                detailPageSelect = SelectMode.Normal;
                            }
                        }
                        else if (detailPageSelect == SelectMode.ForPaste)
                        {
                            if (GUILayout.Button(LC("Paste To Selected Data")))
                            {
                                Dictionary<string, object> pageSelClipboard = new Dictionary<string, object>();
                                foreach (string dkey in selectBuffer.Keys)
                                {
                                    if (selectBuffer[dkey])
                                    {
                                        pageSelClipboard[dkey] = pageClipboard[dkey];
                                    }
                                }
                                cec.SetDataDict(pageSelClipboard);
                                detailPageSelect = SelectMode.Normal;
                            }
                            if (GUILayout.Button(LC("Cancel")))
                            {
                                detailPageSelect = SelectMode.Normal;
                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                    else
                    {
                        GUILayout.Label("Detail of " + greenText(curDetailSetKey) + " is not defined");
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();

                // control buttons
                float cbwidth = (fullw - ResizeGripReserve - 4 * 3) / 4;
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button(LC("Copy All"), btnstyle, GUILayout.Width(cbwidth)))
                {
                    clipboard = cec.GetDataDictFull();
                }
                bool canPaste = clipboard != null;
                bool oldEnabled = GUI.enabled;
                GUI.enabled = oldEnabled && canPaste;
                if (GUILayout.Button(LC("Paste All"), btnstyle, GUILayout.Width(cbwidth)))
                {
                    cec.SetDataDict(clipboard);
                }
                GUI.enabled = oldEnabled;
                if (GUILayout.Button(LC("Revert All"), btnstyle, GUILayout.Width(cbwidth)))
                {
                    cec.RevertAll();
                }
                if (GUILayout.Button(LC("Save"), btnstyle, GUILayout.Width(cbwidth)))
                {
                    savingChara = ociTarget;
                    ChaFile savingChaFile = savingChara.charInfo.chaFile;
                    savingPath = CharaEditorMgr.GetExportCharaPath(savingChaFile.parameter.sex);
                    savingFilename = string.Format("CharaEditor_{0:yyyy-MM-dd-HH-mm-ss}_{1}_{2}.png", DateTime.Now, savingChaFile.parameter.sex == 0 ? "male" : "female", savingChaFile.parameter.fullname);
                    if (savingChaFile.pngData != null)
                    {
                        Texture2D previewTexture = new Texture2D(2, 2);
                        ImageConversion.LoadImage(previewTexture, savingChaFile.pngData);
                        SetSavingTexture(previewTexture);
                    }
                    else
                    {
                        SetSavingTexture(null);
                    }
                    savingCoordinate = false;
                    coordinateName = string.Format("{0}_cood", savingChaFile.parameter.fullname);
                    guiMode = GuiModeType.SAVE;
                }
                GUILayout.Space(ResizeGripReserve);
                GUILayout.EndHorizontal();
            }

            // close btn
            Rect cbRect = new Rect(windowRect.width - 18, 3, 14, 14);
            if (GUI.Button(cbRect, "", closeButtonStyle ?? GUI.skin.button))
            {
                VisibleGUI = false;
                // SYNC TOOLBAR BUTTON STATUS
                if (StudioCharaEditor.Instance._toolbarCharEditor != null)
                    StudioCharaEditor.Instance._toolbarCharEditor.Toggled.OnNext(false);
            }
        }

        private void guiRenderSlider(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            float oldV = (float)dInfo.DetailDefine.Get(chaCtrl);
            float newV = oldV;
            bool preciseMode = StudioCharaEditor.PreciseInputMode.Value;
            bool unlimitMode = StudioCharaEditor.UnlimitedSlider.Value;

            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            DrawTimelineButton(chaCtrl, name, dInfo);
            string txtV;
            int inputw;
            if (preciseMode)
            {
                txtV = string.Format("{0:F3}", oldV * 100.0);
                inputw = GetValueFieldWidth(txtV, 68);
            }
            else
            {
                txtV = string.Format("{0:F0}", oldV * 100.0);
                inputw = GetValueFieldWidth(txtV, 48);
            }
            string newTxtV = GUILayout.TextField(txtV, GUILayout.Width(inputw));
            if (!newTxtV.Equals(txtV))
            {
                if (float.TryParse(newTxtV, out float outV))
                {
                    newV = outV / 100.0f;
                }
            }
            if (preciseMode)
            {
                if (GUILayout.Button("-0.1", GUILayout.Width(37)))
                    newV -= 0.001f;
                if (GUILayout.Button("-0.01", GUILayout.Width(43)))
                    newV -= 0.0001f;
            }
            else
            {
                if (GUILayout.Button("-10", GUILayout.Width(35)))
                    newV -= 0.1f;
                if (GUILayout.Button("-1", GUILayout.Width(30)))
                    newV -= 0.01f;
            }
            float sldMax = 2;
            float sldMin = -1;
            if (unlimitMode)
            {
                sldMax = Math.Max(2, newV);
                sldMin = Math.Min(-1, newV);
            }
            float sldV = DrawThinSlider(newV, sldMin, sldMax);
            if (sldV != newV)
                newV = sldV;
            if (preciseMode)
            {
                if (GUILayout.Button("+0.01", GUILayout.Width(43)))
                    newV += 0.0001f;
                if (GUILayout.Button("+0.1", GUILayout.Width(37)))
                    newV += 0.001f;
            }
            else
            {
                if (GUILayout.Button("+1", GUILayout.Width(30)))
                    newV += 0.01f;
                if (GUILayout.Button("+10", GUILayout.Width(35)))
                    newV += 0.1f;
            }
            if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
                newV = (float)dInfo.RevertValue;
            if (!unlimitMode)
            {
                if (newV > 2)
                    newV = 2f;
                if (newV < -1)
                    newV = -1f;
            }
            if (newV != oldV)
            {
                dInfo.DetailDefine.Set(chaCtrl, newV);
                if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);
                accessoryMultiAdjust(chaCtrl, name, dInfo, newV);
            }
            GUILayout.EndHorizontal();
        }

        private void guiRenderColor(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            Color oldC = (Color)dInfo.DetailDefine.Get(chaCtrl);

            string formatColor(Color color)
            {
                return string.Format("R:{0:F0} G:{1:F0} B:{2:F0} A:{3:F0}", color.r * 255, color.g * 255, color.b * 255, color.a * 100);
            }
            void onChangeColor(Color color)
            {
                if (color != oldC)
                {
                    QueueColorChange(chaCtrl, name, dInfo, color);
                }
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            DrawTimelineButton(chaCtrl, name, dInfo);
            Texture2D colorTex = GetColorSwatchTexture(dInfo.DetailDefine.Key, oldC);
            if (GUILayout.Button(colorTex, colorSwatchButtonStyle ?? GUI.skin.button, GUILayout.Height(ColorSwatchHeight), GUILayout.Width(ColorSwatchWidth)))
            {
                Studio.Studio studio = Studio.Studio.Instance;
                studio.colorPalette.Setup(LC(name), oldC, new Action<Color>(onChangeColor), true);
                studio.colorPalette.visible = true;
            }
            GUILayout.Space(4);
            GUILayout.Label(formatColor(oldC));
            GUILayout.FlexibleSpace();
            if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
                QueueColorChange(chaCtrl, name, dInfo, (Color)dInfo.RevertValue, true);
            GUILayout.EndHorizontal();
        }

        private void guiRenderSelector(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            float fullw = windowRect.width - 20;
            float fullh = windowRect.height - 20;
            float leftw = 150;
            float rightw = fullw - 8 - leftw - 5;
            float thumbBtnWidth = rightw - namew - thumbSize - 60;
            float thumbVSpace = (thumbSize - thumbBtnHeight) / 2;
            float thumbRowHeight = thumbSize + ThumbListRowGap;
            float thumbListMinH = thumbSize * 3 + 28f;
            float thumbListMaxH = Math.Max(thumbListMinH, fullh * 0.82f);
            int thumbShowBefore = 1;
            int thumbShowAfter = (int)Math.Ceiling(thumbListMaxH / thumbRowHeight) + 2;
            bool thumbList = name != "Acc Parent" && name != "Acc Category";
            bool showSmallThumbMode = StudioCharaEditor.ShowSelectedThumb.Value;
            bool unexpandOnSelect = StudioCharaEditor.CloseListAfterSelect.Value;
            bool inSearching = false;

            // Get list and current info
            int oldId = (int)dInfo.DetailDefine.Get(chaCtrl);
            string oldName = "!!Unknown!!";
            int oldIndex = -1;
            string selectorKey = dInfo.DetailDefine.Key;
            List<CustomSelectInfo> infoLst = GetSelectorList(chaCtrl, dInfo);
            Dictionary<int, int> indexById;
            if (selectorIndexPool.TryGetValue(selectorKey, out indexById) && indexById.TryGetValue(oldId, out oldIndex))
            {
                CustomSelectInfo selectedInfo = infoLst[oldIndex];
                oldName = selectedInfo.name;
            }
            else
            {
                for (int i = 0; i < infoLst.Count; i++)
                {
                    if (infoLst[i].id == oldId)
                    {
                        oldName = infoLst[i].name;
                        oldIndex = i;
                        break;
                    }
                }
            }

            // initialize pool
            if (!scrollPool.ContainsKey(name))
            {
                scrollPool[name] = Vector2.zero;
                expandPool[name] = false;
                thumbPool[name] = new Dictionary<string, Texture2D>();
                searchWordPool[name] = string.Empty;
            }

            void onChangeId(int id)
            {
                if (unexpandOnSelect)
                {
                    expandPool[name] = false;   // no matter changed or not
                    CloseSelectorSidePanel(selectorKey);
                }
                if (id != oldId)
                {
                    dInfo.DetailDefine.Set(chaCtrl, id);
                    ClearSelectorCache();
                    if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);
                }
            }

            Texture2D getThumbTex(CustomSelectInfo info)
            {
                return GetSelectorThumbTexture(name, info);
            }

            void DrawThumbSelectorRow(CustomSelectInfo info, int selectedId, float rowThumbVSpace, float rowThumbBtnWidth, Func<CustomSelectInfo, Texture2D> loadThumbTex, Action<int> changeId)
            {
                Color color = GUI.color;
                Texture2D tex = loadThumbTex(info);
                GUILayout.BeginHorizontal();
                GUILayout.Box(tex, GUILayout.Width(thumbSize), GUILayout.Height(thumbSize));
                GUILayout.BeginVertical();
                GUILayout.Space(rowThumbVSpace);
                if (info.id == selectedId)
                    GUI.color = Color.green;
                if (GUILayout.Button(string.Format("#{0}:\n{1}", info.id, info.name), GUILayout.Width(rowThumbBtnWidth), GUILayout.Height(thumbBtnHeight)))
                    changeId(info.id);
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
                GUI.color = color;
            }

            // title line
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            if (thumbList && showSmallThumbMode && !expandPool[name])
            {
                Texture2D tex = oldIndex >= 0 ? getThumbTex(infoLst[oldIndex]) : Texture2D.blackTexture;
                GUILayout.Box(tex, GUILayout.Width(thumbSizeSmall), GUILayout.Height(thumbSizeSmall));
                GUILayout.Label(string.Format("#{0}\n{1}", oldId, oldName));
                GUILayout.FlexibleSpace();
                if (IsSelectorSidePanelOpen(selectorKey))
                {
                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        CloseSelectorSidePanel();
                    }
                }
                else if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        OpenSelectorSidePanel(chaCtrl, name, dInfo, oldIndex, thumbList, thumbRowHeight);
                    }
                if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
                    onChangeId((int)dInfo.RevertValue);
            }
            else
            {
                GUILayout.Label(string.Format("#{0}: {1}", oldId, oldName));
                GUILayout.FlexibleSpace();
                if (expandPool[name])
                {
                    // search button
                    var oldColor = GUI.color;
                    if (searchingMode)
                        GUI.color = Color.yellow;
                    if (GUILayout.Button(LC("Search")))
                        searchingMode = !searchingMode;
                    GUI.color = oldColor;
                    // - button
                    if (GUILayout.Button("-", GUILayout.Width(25)))
                    {
                        expandPool[name] = false;
                    }
                }
                else
                {
                    // + button
                    if (IsSelectorSidePanelOpen(selectorKey))
                    {
                        if (GUILayout.Button("-", GUILayout.Width(25)))
                        {
                            CloseSelectorSidePanel();
                        }
                    }
                    else if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        if (thumbList)
                            OpenSelectorSidePanel(chaCtrl, name, dInfo, oldIndex, thumbList, thumbRowHeight);
                        else
                        {
                            expandPool[name] = true;
                            scrollPool[name] = new Vector2(0, oldIndex * (20 + 4) + 4);
                        }
                    }
                }
                // R button
                if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
                    onChangeId((int)dInfo.RevertValue);
            }
            GUILayout.EndHorizontal();

            // expandable list
            if (expandPool[name])
            {
                // search box
                if (searchingMode && thumbList)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(" ", GUILayout.Width(namew));
                    GUILayout.Label(LC("Search"), GUILayout.Width(namew));
                    searchWordPool[name] = GUILayout.TextField(searchWordPool[name]);
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        searchWordPool[name] = string.Empty;
                    }
                    GUILayout.EndHorizontal();
                    inSearching = !string.IsNullOrWhiteSpace(searchWordPool[name]);
                }

                // draw drop list
                string searchText = inSearching ? searchWordPool[name] : null;
                SelectorSearchState searchState = inSearching ? GetSelectorSearchState(selectorKey, infoLst, searchText) : null;
                List<int> filteredIndices = searchState?.Matches;
                int filteredCount = inSearching ? (filteredIndices?.Count ?? 0) : infoLst.Count;
                GUILayout.BeginHorizontal();
                GUILayout.Label(" ", GUILayout.Width(namew));
                Vector2 oldScroll = scrollPool[name];
                scrollPool[name] = GUILayout.BeginScrollView(scrollPool[name], GUI.skin.box, GUILayout.MinHeight(thumbListMinH), GUILayout.MaxHeight(thumbListMaxH));
                if (thumbList)
                {
                    SelectorRenderRange range = GetSelectorRenderRange(selectorKey, infoLst, inSearching, searchText, filteredCount, scrollPool[name], thumbRowHeight, thumbShowBefore, thumbShowAfter);
                    int firstVisible = range.FirstVisible;
                    int lastVisible = range.LastVisible;

                    if (firstVisible > 0)
                    {
                        GUILayout.Space(firstVisible * thumbRowHeight);
                    }

                    int lastDrawn = firstVisible - 1;
                    if (!inSearching)
                    {
                        for (int i = firstVisible; i <= lastVisible; i++)
                        {
                            DrawThumbSelectorRow(infoLst[i], oldId, thumbVSpace, thumbBtnWidth, getThumbTex, onChangeId);
                            lastDrawn = i;
                        }
                    }
                    else
                    {
                        for (int filteredIndex = firstVisible; filteredIndex <= lastVisible; filteredIndex++)
                        {
                            if (filteredIndices == null || filteredIndex < 0 || filteredIndex >= filteredIndices.Count)
                            {
                                break;
                            }

                            CustomSelectInfo info = infoLst[filteredIndices[filteredIndex]];
                            DrawThumbSelectorRow(info, oldId, thumbVSpace, thumbBtnWidth, getThumbTex, onChangeId);
                            lastDrawn = filteredIndex;
                        }
                    }

                    int trailingRows = filteredCount - lastDrawn - 1;
                    if (trailingRows > 0)
                    {
                        GUILayout.Space(trailingRows * thumbRowHeight);
                    }
                    if (inSearching && searchState != null && !searchState.Complete)
                    {
                        GUILayout.Label(LC("Searching") + "...", GUI.skin.box);
                    }
                }
                else
                {
                    for (int i = 0; i < infoLst.Count; i++)
                    {
                        CustomSelectInfo info = infoLst[i];
                        Color color = GUI.color;
                        // button only
                        if (info.id == oldId)
                            GUI.color = Color.green;
                        if (GUILayout.Button(string.Format("#{0}: {1}", info.id, info.name)))
                            onChangeId(info.id);
                        GUI.color = color;
                    }
                }
                GUILayout.EndScrollView();
                TrackSelectorScroll(oldScroll, scrollPool[name]);
                GUILayout.EndHorizontal();
            }
        }

        private void guiRenderSeperator(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            GUILayout.BeginHorizontal();
            if (dInfo.DetailDefine.Get != null)
                GUILayout.Label(LC((string)dInfo.DetailDefine.Get(chaCtrl)), GUI.skin.box);
            else
                GUILayout.Label(LC(name), GUI.skin.box);
            GUILayout.EndHorizontal();
        }

        private void guiRenderToggle(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            bool newV, oldV;
            oldV = CharaDetailDefine.ParseBool(dInfo.DetailDefine.Get(chaCtrl));

            GUILayout.BeginHorizontal();
            GUILayout.Label(" ", GUILayout.Width(namew));
            DrawTimelineButton(chaCtrl, name, dInfo);
            newV = DrawModernToggle(oldV, LC(name));
            GUILayout.FlexibleSpace();
            if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
            {
                newV = CharaDetailDefine.ParseBool(dInfo.RevertValue);
            }
            if (newV != oldV)
            {
                dInfo.DetailDefine.Set(chaCtrl, newV);
                if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);
                accessoryMultiAdjust(chaCtrl, name, dInfo, newV);
            }
            GUILayout.EndHorizontal();
        }

        private void guiRenderValueInput(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            float oldV = (float)dInfo.DetailDefine.Get(chaCtrl);
            float newV = oldV;
            bool preciseMode = StudioCharaEditor.PreciseInputMode.Value;
            CharaValueDetailDefine vDefine = (CharaValueDetailDefine)dInfo.DetailDefine;
            float dim1 = preciseMode ? vDefine.DimStep1 / 10 : vDefine.DimStep1;
            float dim2 = preciseMode ? vDefine.DimStep2 / 10 : vDefine.DimStep2;

            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            DrawTimelineButton(chaCtrl, name, dInfo);
            // dec buttons
            if (GUILayout.RepeatButton("<<", GUILayout.Width(30)))
                newV -= dim2;
            if (GUILayout.RepeatButton("<", GUILayout.Width(25)))
                newV -= dim1;
            // value input
            string txtV;
            int inputw;
            if (preciseMode)
            {
                txtV = string.Format("{0:F5}", oldV);
                inputw = GetValueFieldWidth(txtV, 76);
            }
            else
            {
                txtV = string.Format("{0:F3}", oldV);
                inputw = GetValueFieldWidth(txtV, 66);
            }
            string newTxtV = GUILayout.TextField(txtV, GUILayout.Width(inputw));
            if (!newTxtV.Equals(txtV))
            {
                if (float.TryParse(newTxtV, out float outV))
                {
                    newV = outV;
                }
            }
            // inc buttons
            if (GUILayout.RepeatButton(">", GUILayout.Width(25)))
                newV += dim1;
            if (GUILayout.RepeatButton(">>", GUILayout.Width(30)))
                newV += dim2;
            // def button
            if (!float.IsNaN(vDefine.DefValue) && accSlotMultiSelection.Count <= 1 && GUILayout.Button(vDefine.DefValue.ToString()))
                newV = vDefine.DefValue;
            // inv button
            if (accSlotMultiSelection.Count <= 1 && GUILayout.Button("INV", GUILayout.ExpandWidth(false)))
                newV = -newV;
            // revert
            GUILayout.FlexibleSpace();
            if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
                newV = (float)dInfo.RevertValue;
            GUILayout.EndHorizontal();

            if (newV != oldV)
            {
                if (vDefine.LoopValue && !float.IsNaN(vDefine.MinValue) && !float.IsNaN(vDefine.MaxValue))
                {
                    while (newV < vDefine.MinValue)
                        newV = vDefine.MaxValue - (vDefine.MinValue - newV);
                    while (newV > vDefine.MaxValue)
                        newV = vDefine.MinValue + (newV - vDefine.MaxValue);
                }
                else
                {
                    if (!float.IsNaN(vDefine.MinValue) && newV < vDefine.MinValue)
                        newV = vDefine.MinValue;
                    if (!float.IsNaN(vDefine.MaxValue) && newV > vDefine.MaxValue)
                        newV = vDefine.MaxValue;
                }
                dInfo.DetailDefine.Set(chaCtrl, newV);
                if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);
                accessoryMultiAdjust(chaCtrl, name, dInfo, newV - oldV, true);
            }
        }

        private void guiRenderIntStatus(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            int oldV = Convert.ToInt32(dInfo.DetailDefine.Get(chaCtrl));
            int newV = oldV;
            int btnWidth = 50;
            CharaIntStatusDetailDefine vDefine = (CharaIntStatusDetailDefine)dInfo.DetailDefine;

            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            DrawTimelineButton(chaCtrl, name, dInfo);
            // int selector
            int num = vDefine.IntStatus.Length;
            if (true)
            {
                // select buttons
                for (int i = 0; i < num; i++)
                {
                    Color oldColor = GUI.color;
                    if (oldV == vDefine.IntStatus[i])
                        GUI.color = Color.green;
                    if (GUILayout.Button(LC(vDefine.IntStatusName[i]), GUILayout.Width(btnWidth)))
                        newV = vDefine.IntStatus[i];
                    GUI.color = oldColor;
                }
            }
            // revert
            GUILayout.FlexibleSpace();
            if (dInfo.RevertValue != null && GUILayout.Button("R", GUILayout.Width(25)))
                newV = Convert.ToInt32(dInfo.RevertValue);
            GUILayout.EndHorizontal();

            // update
            if (newV != oldV)
            {
                dInfo.DetailDefine.Set(chaCtrl, newV);
                if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);
            }
        }

        private void guiRenderButton(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(" ", GUILayout.Width(namew));
            // a button
            if (GUILayout.Button(LC(name)) && dInfo.DetailDefine.Upd != null)
            {
                dInfo.DetailDefine.Upd(chaCtrl);
                accessoryMultiAdjust(chaCtrl, name, dInfo, null);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }

        private void guiRenderHairBundle(ChaControl chaCtrl, string setKey, CharaDetailInfo dInfo)
        {
            // get hair PartsNo
            //Console.WriteLine("\nStart render hair bundle, setKey = {0}", setKey);
            HairBundleDetailSet.PartsNo = Array.IndexOf(HairSetKeys, setKey);
            if (HairBundleDetailSet.PartsNo == -1)
            {
                return;
            }
            //Console.WriteLine("Parts no = {0}, dInfo Key = {1}", HairBundleDetailSet.PartsNo, dInfo.DetailDefine.Key);

            // current
            Dictionary<int, float[]> bundleSetDict = (Dictionary<int, float[]>)dInfo.DetailDefine.Get(chaCtrl);
            if (bundleSetDict == null)
            {
                return;
            }

            // revert
            Dictionary<int, float[]> revBundleSetDict = (Dictionary<int, float[]>)dInfo.RevertValue;
            //Console.WriteLine("Parts revBundleSetDict = {0}", revBundleSetDict);

            foreach (int i in bundleSetDict.Keys)
            {
                HairBundleDetailSet.BundleKey = i;
                string bundlename = string.Format("Bundle {0} Adjust", i);
                float[] revValues = null;
                if (revBundleSetDict != null && revBundleSetDict.ContainsKey(i))
                {
                    revValues = revBundleSetDict[i];
                }
                //Console.WriteLine("bundle key = {0} revValues = {1}", i, revValues);

                // render bundle detail
                foreach (CharaHairBundleDetailDefine cDef in HairBundleDetailSet.Details)
                {
                    CharaDetailInfo cInfo = new CharaDetailInfo(chaCtrl, cDef);
                    //Console.WriteLine("rendering {0}", cDef.Key);
                    switch (cDef.Type)
                    {
                        case CharaDetailDefine.CharaDetailDefineType.SEPERATOR:
                            guiRenderSeperator(chaCtrl, bundlename, cInfo);
                            break;
                        case CharaDetailDefine.CharaDetailDefineType.TOGGLE:
                            cInfo.RevertValue = revValues != null ? cDef.GetRevertValue(revValues) : null;
                            guiRenderToggle(chaCtrl, cDef.Key, cInfo);
                            break;
                        case CharaDetailDefine.CharaDetailDefineType.SLIDER:
                            cInfo.RevertValue = revValues != null ? cDef.GetRevertValue(revValues) : null;
                            guiRenderSlider(chaCtrl, cDef.Key, cInfo);
                            break;
                        default:
                            GUILayout.Label(bundlename + cDef.Key + ": UNKNOWN type not implemented");
                            break;
                    }
                }
            }
        }

        private void guiRenderABMXSet(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            object dataset = dInfo.DetailDefine.Get(chaCtrl);
            float[] workSet;
            float[] workRevert;
            // header
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUI.skin.box);
            GUILayout.EndHorizontal();
            // target selector
            if (dInfo.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET1)
            {
                workSet = (float[])dataset;
                workRevert = (float[])dInfo.RevertValue;
            }
            else if (dInfo.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET2)
            {
                CharaABMXDetailDefine2 dd2 = (dInfo.DetailDefine as CharaABMXDetailDefine2);
                GUILayout.BeginHorizontal();
                GUILayout.Label(LC("Side to edit"), GUILayout.Width(namew));
                for (int i = 0; i < dd2.targetNames.Length; i++)
                {
                    Color bkc = GUI.color;
                    if (i == dd2.curTargetIndex)
                    {
                        GUI.color = Color.green;
                    }
                    if (GUILayout.Button(LC(dd2.targetNames[i])))
                    {
                        dd2.curTargetIndex = i;
                    }
                    GUI.color = bkc;
                }
                GUILayout.EndHorizontal();

                workSet = ((float[][])dataset)[dd2.curTargetIndex == 0 ? 0 : dd2.curTargetIndex - 1];
                workRevert = ((float[][])dInfo.RevertValue)[dd2.curTargetIndex == 0 ? 0 : dd2.curTargetIndex - 1];
            }
            else if (dInfo.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET3)
            {
                CharaABMXDetailDefine3 dd3 = (dInfo.DetailDefine as CharaABMXDetailDefine3);
                GUILayout.BeginHorizontal();
                GUILayout.Label(LC("Hand"), GUILayout.Width(namew));
                for (int i = 0; i < dd3.targetNames.Length; i++)
                {
                    Color bkc = GUI.color;
                    if (i == dd3.curTargetIndex)
                    {
                        GUI.color = Color.green;
                    }
                    if (GUILayout.Button(LC(dd3.targetNames[i])))
                    {
                        dd3.curTargetIndex = i;
                    }
                    GUI.color = bkc;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(LC("Finger"), GUILayout.Width(namew));
                for (int i = 0; i < dd3.fingerNames.Length; i++)
                {
                    Color bkc = GUI.color;
                    if (i == dd3.curFingerIndex)
                    {
                        GUI.color = Color.green;
                    }
                    if (GUILayout.Button(LC(dd3.fingerNames[i])))
                    {
                        dd3.curFingerIndex = i;
                    }
                    GUI.color = bkc;
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label(LC("Segment"), GUILayout.Width(namew));
                for (int i = 0; i < dd3.segmentNames.Length; i++)
                {
                    Color bkc = GUI.color;
                    if (i == dd3.curSegmentIndex)
                    {
                        GUI.color = Color.green;
                    }
                    if (GUILayout.Button(LC(dd3.segmentNames[i])))
                    {
                        dd3.curSegmentIndex = i;
                    }
                    GUI.color = bkc;
                }
                GUILayout.EndHorizontal();

                workSet = ((float[][][][])dataset)[dd3.curTargetIndex == 0 ? 0 : dd3.curTargetIndex - 1][dd3.curFingerIndex == 0 ? 0 : dd3.curFingerIndex - 1][dd3.curSegmentIndex];
                workRevert = ((float[][][][])dInfo.RevertValue)[dd3.curTargetIndex == 0 ? 0 : dd3.curTargetIndex - 1][dd3.curFingerIndex == 0 ? 0 : dd3.curFingerIndex - 1][dd3.curSegmentIndex];
            }
            else
            {
                throw new ArgumentException("Unexpected DetailDefine.Type for ABMX bone: " + name);
            }
            // sliders
            for (int i = 0; i < workSet.Length; i++)
            {
                float sldMax = 2;
                float sldMin = 0;
                float oldV = workSet[i];
                float newV = oldV;
                bool preciseMode = StudioCharaEditor.PreciseInputMode.Value;
                bool unlimitMode = StudioCharaEditor.UnlimitedSlider.Value;

                string slideName = (dInfo.DetailDefine as CharaABMXDetailDefine1).SubSlidersNames[i];

                GUILayout.BeginHorizontal();
                GUILayout.Label(LC(slideName), GUILayout.Width(namew));
                DrawAbmxTimelineButton(chaCtrl, name, slideName, dInfo, i);
                string txtV;
                int inputw;
                if (preciseMode)
                {
                    txtV = string.Format("{0:F3}", oldV * 100.0);
                    inputw = GetValueFieldWidth(txtV, 68);
                }
                else
                {
                    txtV = string.Format("{0:F0}", oldV * 100.0);
                    inputw = GetValueFieldWidth(txtV, 48);
                }
                string newTxtV = GUILayout.TextField(txtV, GUILayout.Width(inputw));
                if (!newTxtV.Equals(txtV))
                {
                    if (float.TryParse(newTxtV, out float outV))
                    {
                        newV = outV / 100.0f;
                    }
                }
                if (preciseMode)
                {
                    if (GUILayout.Button("-0.1", GUILayout.Width(37)))
                        newV -= 0.001f;
                    if (GUILayout.Button("-0.01", GUILayout.Width(43)))
                        newV -= 0.0001f;
                }
                else
                {
                    if (GUILayout.Button("-10", GUILayout.Width(35)))
                        newV -= 0.1f;
                    if (GUILayout.Button("-1", GUILayout.Width(30)))
                        newV -= 0.01f;
                }
                if (unlimitMode)
                {
                    sldMax = Math.Max(2, newV);
                    sldMin = Math.Min(-1, newV);
                }
                float sldV = DrawThinSlider(newV, sldMin, sldMax);
                if (sldV != newV)
                    newV = sldV;
                if (preciseMode)
                {
                    if (GUILayout.Button("+0.01", GUILayout.Width(43)))
                        newV += 0.0001f;
                    if (GUILayout.Button("+0.1", GUILayout.Width(37)))
                        newV += 0.001f;
                }
                else
                {
                    if (GUILayout.Button("+1", GUILayout.Width(30)))
                        newV += 0.01f;
                    if (GUILayout.Button("+10", GUILayout.Width(35)))
                        newV += 0.1f;
                }
                if (GUILayout.Button("R", GUILayout.Width(25)))
                    newV = workRevert[i];
                if (!unlimitMode)
                {
                    if (newV > sldMax)
                        newV = sldMax;
                    if (newV < sldMin)
                        newV = sldMin;
                }
                if (newV != oldV)
                {
                    workSet[i] = newV;
                    if (dInfo.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET2 && (dInfo.DetailDefine as CharaABMXDetailDefine2).curTargetIndex == 0)
                    {
                        ((float[][])dataset)[1][i] = newV;
                    }
                    if (dInfo.DetailDefine.Type == CharaDetailDefine.CharaDetailDefineType.ABMXSET3)
                    {
                        CharaABMXDetailDefine3 dd3 = dInfo.DetailDefine as CharaABMXDetailDefine3;
                        if (dd3.curTargetIndex == 0)
                        {
                            ((float[][][][])dataset)[1][dd3.curFingerIndex == 0 ? 0 : dd3.curFingerIndex - 1][dd3.curSegmentIndex][i] = newV;
                        }
                        if (dd3.curFingerIndex == 0)
                        {
                            for (int h = 0; h < 2; h++)
                            {
                                if (dd3.curTargetIndex == 0 || dd3.curTargetIndex - 1 == h)
                                {
                                    for (int f = 1; f < 5; f++)
                                    {
                                        ((float[][][][])dataset)[h][f][dd3.curSegmentIndex][i] = newV;
                                    }
                                }
                            }
                        }
                    }
                    dInfo.DetailDefine.Set(chaCtrl, dataset);
                }
                GUILayout.EndHorizontal();
            }

        }

        private void guiRenderSkinOverlay(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            float OverlayThumbSize = 124;

            // current part texture
            SkinOverlayDetailDefine overlayDefine = (SkinOverlayDetailDefine)dInfo.DetailDefine;
            Texture2D tex = (Texture2D)overlayDefine.GetSkinOverlayTex(chaCtrl);

            // Overlay block
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            if (tex != null)
                GUILayout.Box(tex, GUILayout.Width(OverlayThumbSize), GUILayout.Height(OverlayThumbSize));
            else
                GUILayout.Box(LC("No Texture"), texTextStyle, GUILayout.Width(OverlayThumbSize), GUILayout.Height(OverlayThumbSize));
            GUILayout.BeginVertical();
            if (GUILayout.Button(LC("Load new texture")))
                overlayDefine.LoadNewOverlayTexture(chaCtrl);
            if (tex != null && GUILayout.Button(LC("Clear texture")))
                overlayDefine.SetSkinOverlayTex(chaCtrl, null);
            if (tex != null && GUILayout.Button(LC("Export current texture")))
                overlayDefine.DumpSkinOverlayTexture(chaCtrl);
            if (!CharaEditorController.DataValueEqual(tex, dInfo.RevertValue) && GUILayout.Button(LC("Revert")))
                overlayDefine.SetSkinOverlayTex(chaCtrl, dInfo.RevertValue);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void guiRenderClothOverlay(ChaControl chaCtrl, string name, CharaDetailInfo dInfo)
        {
            float OverlayThumbSize = 124;

            // current part texture
            ClothOverlayDetailDefine overlayDefine = (ClothOverlayDetailDefine)dInfo.DetailDefine;
            KoiClothesOverlayX.ClothesTexData texData = (KoiClothesOverlayX.ClothesTexData)overlayDefine.GetClothOverlayTexData(chaCtrl);

            // Overlay block
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC(name), GUILayout.Width(namew));
            if (texData != null && texData.Texture != null)
                GUILayout.Box(texData.Texture, GUILayout.Width(OverlayThumbSize), GUILayout.Height(OverlayThumbSize));
            else
                GUILayout.Box(LC("No Texture"), texTextStyle, GUILayout.Width(OverlayThumbSize), GUILayout.Height(OverlayThumbSize));
            GUILayout.BeginVertical();
            if (GUILayout.Button(LC("Load overlay texture")))
                overlayDefine.LoadNewOverlayTexture(chaCtrl);
            if (texData != null && GUILayout.Button(LC("Clear overlay texture")))
                overlayDefine.SetClothOverlayTex(chaCtrl, null);
            if (texData != null && GUILayout.Button(LC("Export overlay texture")))
                overlayDefine.DumpClothOverlayTexture(chaCtrl);
            if (GUILayout.Button(LC("Dump original texture")))
                overlayDefine.DumpClothOrignalTexture(chaCtrl);
            if (overlayDefine.modified && GUILayout.Button(LC("Revert")))
            {
                overlayDefine.SetClothOverlayTex(chaCtrl, dInfo.RevertValue);
                overlayDefine.modified = false;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void guiSave()
        {
            float fullw = windowRect.width - 20;
            float fullh = windowRect.height - 20;

            float thumbH = fullh - 40;
            float thumbW = fullw - 350;// thumbH * 252.0f / 352.0f;
            ChaFile savingChaFile = savingChara.charInfo.chaFile;

            // save ui
            GUILayout.BeginHorizontal();
            if (savingTexture != null)
            {
                GUILayout.Box(savingTexture, GUILayout.Width(thumbW), GUILayout.Height(thumbH));
            }
            else
            {
                GUILayout.Box(redText(LC("No Photo")), texTextStyle, GUILayout.Width(thumbW), GUILayout.Height(thumbH));
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC("Charactor name:"), largeLabel);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(cyanText(savingChaFile.parameter.fullname));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC("Output folder:"), largeLabel);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(cyanText(savingPath));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label(LC("PNG file name:"), largeLabel);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (savingCoordinate)
            {
                coordinateName = GUILayout.TextField(coordinateName, GUILayout.Width(200));
                GUILayout.Label(".png", GUILayout.Width(50));
            }
            else
            {
                GUILayout.Label(cyanText(savingFilename));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("Change export path/filename"), btnstyle))
            {
                OpenFileDialog.Show((files) =>
                {
                    if (files != null && files.Length > 0)
                    {
                        string pathname = files[0];
                        savingPath = Path.GetDirectoryName(pathname);
                        savingFilename = Path.GetFileName(pathname);
                        if (!Path.GetExtension(pathname).ToLower().Equals(".png"))
                        {
                            savingFilename += ".png";
                        }
                    }
                }, "Save Character", savingPath, "Images (*.png)|*.png|All files|*.*", ".png", OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER | OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES);
            }
            GUILayout.EndHorizontal();
            //GUILayout.BeginHorizontal();
            //if (GUILayout.Button(LC("Capture thumbnail photo"), btnstyle))
            //{
            //    int capW = 1280;
            //    int capH = 720;
            //    int savW = 504;
            //    int savH = 704;
            //    RenderTextureFormat format = RenderTextureFormat.ARGB32;
            //    int depthBuffer = 0;
            //    RenderTexture targetTexture = Camera.main.targetTexture;
            //    Camera.main.targetTexture = RenderTexture.GetTemporary(capW, capH, depthBuffer, format);
            //    Camera.main.Render();
            //    RenderTexture active = RenderTexture.active;
            //    RenderTexture.active = Camera.main.targetTexture;
            //    savingTexture = new Texture2D(savW, savH);
            //    savingTexture.ReadPixels(new Rect((capW - savW) / 2.0f, (capH - savH) / 2.0f, (float)savW, (float)savH), 0, 0, false);
            //    savingTexture.Apply();
            //    RenderTexture.active = active;
            //    RenderTexture.ReleaseTemporary(Camera.main.targetTexture);
            //    Camera.main.targetTexture = targetTexture;

            //    // shink size
            //    if (!StudioCharaEditor.DoubleThumbnailSize.Value)
            //    {
            //        TextureScale.Bilinear(savingTexture, savW / 2, savH / 2);
            //    }
            //}
            //GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("Capture Thumbnail Photo"), btnstyle))
            {
                int capW = 640;
                int capH = 360;
                int savW = 504;
                int savH = 704;

                byte[] capBuf = Studio.Studio.Instance.gameScreenShot.CreatePngScreen(capW, capH);

                Texture2D capTex = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                capTex.LoadImage(capBuf);
                Color[] capPixels = capTex.GetPixels((1280 - savW) / 2, (720 - savH) / 2, savW, savH, 0);

                Texture2D newSavingTexture = new Texture2D(savW, savH);
                newSavingTexture.SetPixels(capPixels);
                newSavingTexture.Apply();
                SetSavingTexture(newSavingTexture);
                Destroy(capTex);

                // shink size
                if (!StudioCharaEditor.DoubleThumbnailSize.Value)
                {
                    TextureScale.Bilinear(savingTexture, savW / 2, savH / 2);
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool newSavingCoord = DrawModernToggle(savingCoordinate, LC("Save as coordinate file"));
            if (newSavingCoord != savingCoordinate)
            {
                savingCoordinate = newSavingCoord;
                savingPath = savingCoordinate ? CharaEditorMgr.GetExportCoordPath(savingChaFile.parameter.sex) : CharaEditorMgr.GetExportCharaPath(savingChaFile.parameter.sex);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // control buttons
            float cbwidth = (fullw - ResizeGripReserve - 2 * 4) / 3;
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(LC("Export PNG"), btnstyle, GUILayout.Width(cbwidth)))
            {
                try
                {
                    if (savingCoordinate)
                    {
                        if (savingTexture != null)
                        {
                            savingChaFile.coordinate.pngData = savingTexture.EncodeToPNG();
                        }
                        savingChaFile.coordinate.coordinateName = coordinateName;

                        string validCoordName = coordinateName;
                        char[] invalidch = Path.GetInvalidFileNameChars();
                        foreach (char c in invalidch)
                        {
                            validCoordName = validCoordName.Replace(c, '_');
                        }
                        if (!Path.GetExtension(validCoordName).ToLower().Equals(".png"))
                        {
                            validCoordName += ".png";
                        }
                        string filename = Path.Combine(savingPath, validCoordName);

                        // trick KKAPI
                        //CharaEditorMgr.SetMakerApiInsideMaker(true);
                        //CharaEditorMgr.SetCustomBase(savingChara.charInfo);

                        savingChaFile.coordinate.SaveFile(filename, (int)Manager.GameSystem.Instance.language);
                        StudioCharaEditor.Logger.Log(LogLevel.Message | LogLevel.Warning, string.Format("Charactor {0}'s coordinate saved to {1}.", savingChaFile.parameter.fullname, validCoordName));
                        guiMode = GuiModeType.MAIN;
                    }
                    else
                    {
                        if (savingTexture != null)
                        {
                            savingChaFile.pngData = savingTexture.EncodeToPNG();
                        }
                        string filename = Path.Combine(savingPath, savingFilename);

                        Traverse.Create(savingChaFile).Method("SaveFile", new object[] { filename, 0 }).GetValue();
                        StudioCharaEditor.Logger.Log(LogLevel.Message | LogLevel.Warning, string.Format("Charactor {0} saved to {1}.", savingChaFile.parameter.fullname, savingFilename));
                        guiMode = GuiModeType.MAIN;
                    }
                }
                catch (Exception ex)
                {
                    StudioCharaEditor.Logger.LogError(ex.Message);
                }
            }
            if (GUILayout.Button(LC("Save as revert point"), btnstyle, GUILayout.Width(cbwidth)))
            {
                CharaEditorController cec = CharaEditorMgr.Instance.GetEditorController(savingChara);
                if (cec != null)
                {
                    cec.InitFileData();
                    StudioCharaEditor.Logger.Log(LogLevel.Message | LogLevel.Warning, string.Format("Charactor {0}'s revert point updated.", savingChaFile.parameter.fullname));
                    guiMode = GuiModeType.MAIN;
                }
                else
                {
                    StudioCharaEditor.Logger.LogError("Fail to get CharaEditorController!");
                }
            }
            if (GUILayout.Button(LC("Cancel"), btnstyle, GUILayout.Width(cbwidth)))
            {
                guiMode = GuiModeType.MAIN;
            }
            GUILayout.Space(ResizeGripReserve);
            GUILayout.EndHorizontal();

            // close btn
            Rect cbRect = new Rect(windowRect.width - 18, 3, 14, 14);
            if (GUI.Button(cbRect, "", closeButtonStyle ?? GUI.skin.button))
            {
                VisibleGUI = false;
            }
        }

        private void accessoryMultiAdjust(ChaControl chaCtrl, string name, CharaDetailInfo dMasterInfo, object value, bool delta = false)
        {
            if (catelogIndex1 != 4) return; // only for accessories
            if (accSlotMultiSelection.Count <= 1) return;   // only for multi selection

            string masterAccKey = GetDetailCategory2(dMasterInfo.DetailDefine.Key);
            //Console.WriteLine($"Adjust for multi accessories: from={masterAccKey}, to={accSlotMultiSelection.Count - 1}, name={name}, value={value}, delta={delta}");
            CharaEditorController cec = CharaEditorMgr.Instance.GetEditorController(ociTarget);
            foreach (string accKey in accSlotMultiSelection)
            {
                if (accKey.Equals(masterAccKey))
                {
                    continue;   // skip master
                }
                CharaDetailInfo dInfo = cec.GetDetailInfo(CharaEditorController.CT1_ACCS, accKey, name);
                if (dInfo == null)
                {
                    //Console.WriteLine($"Name <{name}> not found for acc slot {accKey}");
                    continue;   // no detail info
                }

                // process value
                if (value != null && !delta)
                {
                    // set and upd
                    dInfo.DetailDefine.Set(chaCtrl, value);
                    if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);

                }
                else if (value != null && delta)
                {
                    // delta set
                    float oldV = (float)dInfo.DetailDefine.Get(chaCtrl);
                    float newV = (float)value + oldV;
                    dInfo.DetailDefine.Set(chaCtrl, newV);
                    if (dInfo.DetailDefine.Upd != null && !LaterUpdate) dInfo.DetailDefine.Upd(chaCtrl);
                }
                else if (value == null && dInfo.DetailDefine.Upd != null)
                {
                    // upd only
                    dInfo.DetailDefine.Upd(chaCtrl);
                }
                else
                {
                    // skip
                    Console.WriteLine("Unknown/Unsupported call input for multi accessory adjustment: " + accKey + "#" + name);
                }
            }
        }

        private Texture2D GetColorSwatchTexture(string swatchKey, Color color)
        {
            Color32 color32 = color;
            int key = unchecked((color32.r << 24) | (color32.g << 16) | (color32.b << 8) | color32.a);
            ColorSwatch swatch;
            if (!colorSwatchPool.TryGetValue(swatchKey, out swatch) || swatch == null || swatch.Texture == null)
            {
                swatch = new ColorSwatch
                {
                    Texture = new Texture2D(ColorSwatchWidth, ColorSwatchHeight, TextureFormat.ARGB32, false),
                    Pixels = new Color[ColorSwatchWidth * ColorSwatchHeight]
                };
                swatch.Texture.wrapMode = TextureWrapMode.Clamp;
                swatch.Texture.filterMode = FilterMode.Point;
                colorSwatchPool[swatchKey] = swatch;
            }

            if (swatch.ColorKey != key)
            {
                for (int i = 0; i < swatch.Pixels.Length; i++)
                {
                    swatch.Pixels[i] = color;
                }
                swatch.Texture.SetPixels(swatch.Pixels);
                swatch.Texture.Apply(false, false);
                swatch.ColorKey = key;
            }

            return swatch.Texture;
        }

        private List<CustomSelectInfo> GetSelectorList(ChaControl chaCtrl, CharaDetailInfo dInfo)
        {
            string selectorKey = dInfo.DetailDefine.Key;
            List<CustomSelectInfo> infoList;
            if (!selectorListPool.TryGetValue(selectorKey, out infoList) || infoList == null)
            {
                infoList = dInfo.DetailDefine.SelectorList(chaCtrl) ?? new List<CustomSelectInfo>();
                selectorListPool[selectorKey] = infoList;

                Dictionary<int, int> indexById = new Dictionary<int, int>();
                for (int i = 0; i < infoList.Count; i++)
                {
                    if (!indexById.ContainsKey(infoList[i].id))
                    {
                        indexById.Add(infoList[i].id, i);
                    }
                }
                selectorIndexPool[selectorKey] = indexById;
            }
            return infoList;
        }

        private Texture2D GetSelectorThumbTexture(string name, CustomSelectInfo info)
        {
            if (info == null || info.assetBundle == null || info.assetName == null)
            {
                return Texture2D.blackTexture;
            }

            if (!thumbPool.ContainsKey(name))
            {
                thumbPool[name] = new Dictionary<string, Texture2D>();
            }

            string texKey = info.assetBundle + "+" + info.assetName;
            if (!thumbPool[name].ContainsKey(texKey))
            {
                if (IsSelectorThumbnailLoadPaused() || !CanLoadSelectorThumbnailThisFrame())
                {
                    return Texture2D.blackTexture;
                }
                thumbPool[name][texKey] = CommonLib.LoadAsset<Texture2D>(info.assetBundle, info.assetName, false, "");
            }
            return thumbPool[name][texKey];
        }

        private void TrackSelectorScroll(Vector2 oldScroll, Vector2 newScroll)
        {
            if ((newScroll - oldScroll).sqrMagnitude > SelectorScrollChangeEpsilon)
            {
                selectorThumbLoadPauseUntil = Time.realtimeSinceStartup + SelectorThumbLoadIdleDelay;
            }
        }

        private bool IsSelectorThumbnailLoadPaused()
        {
            Event evt = Event.current;
            return evt.type == EventType.MouseDrag ||
                   evt.type == EventType.ScrollWheel ||
                   Time.realtimeSinceStartup < selectorThumbLoadPauseUntil;
        }

        private bool CanLoadSelectorThumbnailThisFrame()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return false;
            }

            if (selectorThumbLoadFrame != Time.frameCount)
            {
                selectorThumbLoadFrame = Time.frameCount;
                selectorThumbLoadsThisFrame = 0;
            }

            if (selectorThumbLoadsThisFrame >= MaxSelectorThumbLoadsPerFrame)
            {
                return false;
            }

            selectorThumbLoadsThisFrame++;
            return true;
        }

        private int GetSelectorIndex(string selectorKey, List<CustomSelectInfo> infoList, int id, out string displayName)
        {
            displayName = "!!Unknown!!";
            if (infoList == null)
            {
                return -1;
            }

            if (selectorIndexPool.TryGetValue(selectorKey, out Dictionary<int, int> indexById) &&
                indexById.TryGetValue(id, out int indexed) &&
                indexed >= 0 &&
                indexed < infoList.Count)
            {
                displayName = infoList[indexed].name;
                return indexed;
            }

            for (int i = 0; i < infoList.Count; i++)
            {
                if (infoList[i].id == id)
                {
                    displayName = infoList[i].name;
                    return i;
                }
            }

            return -1;
        }

        private void ClearSelectorCache()
        {
            selectorListPool.Clear();
            selectorIndexPool.Clear();
            selectorRenderRangePool.Clear();
            selectorSearchPool.Clear();

            if (selectorSidePanel != null)
            {
                selectorSidePanel.FolderInfoCount = -1;
                selectorSidePanel.FolderFavoriteVersion = -1;
                selectorSidePanel.FolderCustomVersion = -1;
                selectorSidePanel.Folders?.Clear();
            }
        }

        private void ClearSelectorRuntimeCache(string selectorKey)
        {
            if (string.IsNullOrEmpty(selectorKey))
            {
                selectorRenderRangePool.Clear();
                selectorSearchPool.Clear();
                return;
            }

            RemoveSelectorCacheKeys(selectorRenderRangePool, selectorKey);
            RemoveSelectorCacheKeys(selectorSearchPool, selectorKey);
        }

        private static void RemoveSelectorCacheKeys<TValue>(Dictionary<string, TValue> cache, string selectorKey)
        {
            if (cache == null || cache.Count == 0)
            {
                return;
            }

            string prefix = selectorKey + "|";
            List<string> keysToRemove = cache.Keys
                .Where(key => string.Equals(key, selectorKey, StringComparison.Ordinal) ||
                              key.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            for (int i = 0; i < keysToRemove.Count; i++)
            {
                cache.Remove(keysToRemove[i]);
            }
        }

        private static int GetFilteredSelectorCount(List<CustomSelectInfo> infoList, bool inSearching, string searchText)
        {
            if (!inSearching)
            {
                return infoList.Count;
            }

            int count = 0;
            for (int i = 0; i < infoList.Count; i++)
            {
                if (SelectorMatchesSearch(infoList[i], true, searchText))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool SelectorMatchesSearch(CustomSelectInfo info, bool inSearching, string searchText)
        {
            if (!inSearching)
            {
                return true;
            }
            if (string.IsNullOrEmpty(info.name))
            {
                return false;
            }
            return info.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetSavingTexture(Texture2D texture)
        {
            if (savingTexture != null && savingTexture != texture)
            {
                Destroy(savingTexture);
            }
            savingTexture = texture;
        }

        private static string GetDetailName(string detailKey)
        {
            char keySeparator = CharaEditorController.KEY_SEP_CHAR[0];
            int firstSep = detailKey.IndexOf(keySeparator);
            if (firstSep < 0)
            {
                return detailKey;
            }

            int secondSep = detailKey.IndexOf(keySeparator, firstSep + 1);
            if (secondSep < 0 || secondSep + 1 >= detailKey.Length)
            {
                return detailKey;
            }

            int thirdSep = detailKey.IndexOf(keySeparator, secondSep + 1);
            if (thirdSep < 0)
            {
                return detailKey.Substring(secondSep + 1);
            }

            return detailKey.Substring(secondSep + 1, thirdSep - secondSep - 1);
        }

        private static string GetDetailCategory2(string detailKey)
        {
            char keySeparator = CharaEditorController.KEY_SEP_CHAR[0];
            int firstSep = detailKey.IndexOf(keySeparator);
            if (firstSep < 0 || firstSep + 1 >= detailKey.Length)
            {
                return detailKey;
            }

            int secondSep = detailKey.IndexOf(keySeparator, firstSep + 1);
            if (secondSep < 0)
            {
                return detailKey.Substring(firstSep + 1);
            }

            return detailKey.Substring(firstSep + 1, secondSep - firstSep - 1);
        }


        private string colorText(string text, string color = "ffffff")
        {
            return "<color=#" + color + ">" + text + "</color>";
        }

        private string redText(string text)
        {
            return colorText(text, "ff0000");
        }

        private string greenText(string text)
        {
            return colorText(text, "00ff00");
        }

        private string magentaText(string text)
        {
            return colorText(text, "ff00ff");
        }

        private string cyanText(string text)
        {
            return colorText(text, "00ffff");
        }

        private string greyText(string text)
        {
            return colorText(text, "808080");
        }

        private string LC(string org)
        {
            if (curLocalizationDict != null && curLocalizationDict.ContainsKey(org) && !string.IsNullOrWhiteSpace(curLocalizationDict[org]))
                return curLocalizationDict[org];
            else
                return org;
        }

        private static int CompareSlotNo(string x, string y)
        {
            try
            {
                int sx = int.Parse(x);
                int sy = int.Parse(y);
                if (sx < sy)
                {
                    return -1;
                }
                else if (sy < sx)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
