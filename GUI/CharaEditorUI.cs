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
        private readonly string windowTitle = "Studio Charactor Editor";
        internal Rect windowRect = new Rect(0f, 300f, 600f, 400f);
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
        private const float ThinSliderHeight = 20f;
        private const float ThinSliderTrackHeight = 2f;
        private const float ThinSliderThumbSize = 7f;
        private const float TimelineButtonWidth = 22f;
        private const float ColorDragApplyInterval = 0.06f;
        private const float SelectorSearchDelay = 0.25f;
        private const int SelectorSearchBatchSize = 350;
        private readonly Dictionary<string, PendingColorChange> pendingColorChanges = new Dictionary<string, PendingColorChange>();
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
            ClearSelectorCache();
            selectorRenderRangePool.Clear();
            selectorSearchPool.Clear();
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

                    mouseInWindow = windowRect.Contains(Event.current.mousePosition);
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
                int lastVisible = Math.Min(filteredCount - 1, firstVisible + showBefore + showAfter + 1);

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
            if (!selectorSearchPool.TryGetValue(selectorKey, out SelectorSearchState state))
            {
                state = new SelectorSearchState();
                selectorSearchPool[selectorKey] = state;
            }

            int infoCount = infoList?.Count ?? 0;
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
                    if (SelectorMatchesSearch(infoList[i], true, searchText))
                    {
                        state.Matches.Add(i);
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

        private void OnSelectChange(TreeNodeObject newSel)
        {
            lastSelectedTreeNode = newSel;
            ociTarget = GetOCICharFromNode(newSel);
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
                if (GUILayout.Button(canPaste ? LC("Paste All") : " ", btnstyle, GUILayout.Width(cbwidth)) && canPaste)
                {
                    cec.SetDataDict(clipboard);
                }
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
                if (info.assetBundle != null && info.assetName != null)
                {
                    string texKey = info.assetBundle + "+" + info.assetName;
                    if (!thumbPool[name].ContainsKey(texKey))
                    {
                        if (Event.current.type != EventType.Repaint)
                        {
                            return Texture2D.blackTexture;
                        }
                        thumbPool[name][texKey] = CommonLib.LoadAsset<Texture2D>(info.assetBundle, info.assetName, false, ""); ;
                    }
                    return thumbPool[name][texKey];
                }
                else
                {
                    return Texture2D.blackTexture;
                }
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
                if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        expandPool[name] = true;
                        scrollPool[name] = new Vector2(0, Math.Max(0, oldIndex) * thumbRowHeight + ThumbListRowGap);
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
                    if (GUILayout.Button("+", GUILayout.Width(25)))
                    {
                        expandPool[name] = true;
                        if (thumbList)
                            scrollPool[name] = new Vector2(0, Math.Max(0, oldIndex) * thumbRowHeight + ThumbListRowGap);
                        else
                            scrollPool[name] = new Vector2(0, oldIndex * (20 + 4) + 4);
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

        private void ClearSelectorCache()
        {
            selectorListPool.Clear();
            selectorIndexPool.Clear();
            selectorRenderRangePool.Clear();
            selectorSearchPool.Clear();
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
