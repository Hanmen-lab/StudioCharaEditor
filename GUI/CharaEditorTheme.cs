using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using UnityEngine;

namespace StudioCharaEditor
{
    internal sealed class CharaEditorTheme : IDisposable
    {
        private readonly List<UnityEngine.Object> ownedResources = new List<UnityEngine.Object>();
        private readonly CharaEditorUiTheme mode;

        public GUISkin Skin { get; private set; }
        public GUIStyle WindowStyle { get; private set; }
        public GUIStyle LargeLabelStyle { get; private set; }
        public GUIStyle PrimaryButtonStyle { get; private set; }
        public GUIStyle CategoryButtonStyle { get; private set; }
        public GUIStyle TextureTextStyle { get; private set; }
        public GUIStyle ColorSwatchButtonStyle { get; private set; }
        public GUIStyle CloseButtonStyle { get; private set; }
        public Texture2D ToggleOffTexture { get; private set; }
        public Texture2D ToggleOnTexture { get; private set; }
        public Texture2D MainGameCheckboxOffTexture { get; private set; }
        public Texture2D MainGameCheckboxOnTexture { get; private set; }
        public Texture2D MainGameSelectorSelectedTexture { get; private set; }
        public Texture2D MainGameSliderTrackTexture { get; private set; }
        public Texture2D MainGameExitNormalTexture { get; private set; }
        public Texture2D MainGameExitSelectedTexture { get; private set; }
        public GUIStyle MainGameTransparentWindowStyle { get; private set; }
        public GUIStyle MainGamePanelWindowStyle { get; private set; }
        public GUIStyle MainGameSectionHeaderStyle { get; private set; }
        public GUIStyle MainGameListButtonStyle { get; private set; }
        public GUIStyle MainGameListSelectedStyle { get; private set; }
        public GUIStyle MainGameListMultiSelectedStyle { get; private set; }
        public GUIStyle MainGameIconButtonStyle { get; private set; }
        public GUIStyle MainGameTitleStyle { get; private set; }
        public GUIStyle MainGameBreadcrumbStyle { get; private set; }
        public GUIStyle MainGameTabStyle { get; private set; }
        public GUIStyle MainGameTabSelectedStyle { get; private set; }
        public GUIStyle MainGameNumericValueStyle { get; private set; }
        public Texture2D[] MainGameCategoryNormal { get; private set; }
        public Texture2D[] MainGameCategorySelected { get; private set; }
        public Texture2D MainGameDividerTexture { get; private set; }

        public bool IsMainGame => mode == CharaEditorUiTheme.MainGame;

        private static readonly Color TextColor = Rgba(232, 238, 241);
        private static readonly Color MutedTextColor = Rgba(174, 185, 191);
        private static readonly Color WindowFill = Rgba(24, 27, 30, 204);
        private static readonly Color PanelFill = Rgba(33, 37, 41, 196);
        private static readonly Color FieldFill = Rgba(18, 20, 23, 208);
        private static readonly Color Stroke = Rgba(70, 78, 84, 196);
        private static readonly Color StrokeSoft = Rgba(58, 65, 71, 176);
        private static readonly Color Accent = Rgba(42, 184, 154);
        private static readonly Color AccentHover = Rgba(55, 205, 175);
        private static readonly Color AccentActive = Rgba(30, 151, 128);
        private static readonly Color Danger = Rgba(205, 76, 78);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        private const uint FrPrivate = 0x10;
        private const int HwndBroadcast = 0xffff;
        private const int WmFontChange = 0x001D;
        private const int SmtoAbortIfHung = 0x0002;

        public CharaEditorTheme(CharaEditorUiTheme mode = CharaEditorUiTheme.Modern)
        {
            this.mode = mode;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            IntPtr wParam,
            IntPtr lParam,
            uint fuFlags,
            uint uTimeout,
            out IntPtr lpdwResult);

        public void Ensure(GUISkin baseSkin)
        {
            if (Skin != null || baseSkin == null)
            {
                return;
            }

            Skin = UnityEngine.Object.Instantiate(baseSkin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            ownedResources.Add(Skin);
            Font themeFont = IsMainGame
                ? LoadMainGameFont()
                : LoadEmbeddedFont("Pangram-Light.otf");
            if (themeFont != null)
            {
                Skin.font = themeFont;
            }

            // The mode-switch icon is shared by both themes.
            MainGameExitNormalTexture = ThemeTexture("sp_ai_make_07_00.png", 128, 128, Transparent, Transparent, 0, 0);
            MainGameExitSelectedTexture = ThemeTexture("sp_ai_make_07_01.png", 128, 128, Transparent, Transparent, 0, 0);

            if (IsMainGame)
            {
                BuildMainGameSkin(themeFont);
                return;
            }

            Texture2D windowTex = ThemeTexture("ui_window.png", 64, 64, WindowFill, Rgba(92, 103, 111, 205), 5, 1, 0.80f);
            Texture2D panelTex = ThemeTexture("ui_panel.png", 64, 64, PanelFill, StrokeSoft, 3, 1, 0.76f);
            Texture2D panelHoverTex = ThemeTexture("ui_panel_hover.png", 64, 64, Rgba(41, 46, 51, 208), Rgba(90, 102, 111, 204), 3, 1, 0.82f);
            Texture2D fieldTex = ThemeTexture("ui_field.png", 48, 48, FieldFill, Stroke, 2, 1, 0.82f);
            Texture2D fieldFocusTex = ThemeTexture("ui_field_focus.png", 48, 48, FieldFill, Accent, 2, 1, 0.86f);
            Texture2D buttonTex = ThemeTexture("ui_button.png", 48, 48, Rgba(48, 55, 61, 208), Rgba(91, 103, 112, 200), 2, 1, 0.82f);
            Texture2D buttonHoverTex = ThemeTexture("ui_button_hover.png", 48, 48, Rgba(58, 67, 74, 222), Rgba(117, 132, 142, 212), 2, 1, 0.88f);
            Texture2D buttonActiveTex = ThemeTexture("ui_button_active.png", 48, 48, Rgba(35, 41, 46, 232), Accent, 2, 1, 0.92f);
            Texture2D accentTex = ThemeTexture("ui_accent.png", 48, 48, Accent, AccentHover, 2, 1);
            Texture2D accentHoverTex = ThemeTexture("ui_accent_hover.png", 48, 48, AccentHover, Rgba(130, 242, 218), 2, 1);
            Texture2D accentActiveTex = ThemeTexture("ui_accent_active.png", 48, 48, AccentActive, AccentHover, 2, 1);
            Texture2D dangerTex = ThemeTexture("ui_danger.png", 48, 48, Danger, Rgba(238, 117, 119), 1, 1);
            Texture2D clearTex = ThemeTexture("ui_clear.png", 24, 24, Transparent, Transparent, 5, 0);
            Texture2D closeTex = ThemeTexture("ui_close.png", 18, 18, Rgba(120, 16, 20), Rgba(205, 48, 54), 0, 1);
            Texture2D scrollTrackTex = ThemeTexture("ui_scroll_track.png", 32, 32, Rgba(18, 21, 24, 200), Rgba(18, 21, 24, 200), 2, 0);
            Texture2D scrollThumbTex = ThemeTexture("ui_scroll_thumb.png", 32, 32, Rgba(92, 105, 114, 235), Rgba(119, 135, 146, 235), 2, 1);
            Texture2D sliderTrackTex = ThemeTexture("ui_slider_track.png", 64, 8, Rgba(14, 16, 18, 225), Rgba(48, 56, 62, 230), 1, 1);
            Texture2D sliderThumbTex = ThemeTexture("ui_slider_thumb.png", 10, 10, Rgba(150, 161, 168), Rgba(199, 209, 214), 1, 1);
            ToggleOffTexture = ThemeTexture("ui_toggle_off.png", 18, 18, Transparent, Transparent, 0, 0);
            ToggleOnTexture = ThemeTexture("ui_toggle_on.png", 18, 18, Transparent, Transparent, 0, 0);

            WindowStyle = new GUIStyle(Skin.window);
            WindowStyle.normal.background = windowTex;
            WindowStyle.hover.background = windowTex;
            WindowStyle.active.background = windowTex;
            WindowStyle.focused.background = windowTex;
            WindowStyle.onNormal.background = windowTex;
            WindowStyle.onHover.background = windowTex;
            WindowStyle.onActive.background = windowTex;
            WindowStyle.onFocused.background = windowTex;
            WindowStyle.normal.textColor = TextColor;
            WindowStyle.hover.textColor = TextColor;
            WindowStyle.active.textColor = TextColor;
            WindowStyle.focused.textColor = TextColor;
            WindowStyle.onNormal.textColor = TextColor;
            WindowStyle.onHover.textColor = TextColor;
            WindowStyle.onActive.textColor = TextColor;
            WindowStyle.onFocused.textColor = TextColor;
            WindowStyle.fontStyle = FontStyle.Bold;
            WindowStyle.alignment = TextAnchor.UpperCenter;
            WindowStyle.border = new RectOffset(6, 6, 24, 6);
            WindowStyle.padding = new RectOffset(10, 10, 26, 10);
            WindowStyle.margin = new RectOffset(0, 0, 0, 0);

            Skin.window = WindowStyle;

            Skin.box = PanelStyle(Skin.box, panelTex, panelHoverTex);
            Skin.scrollView = PanelStyle(Skin.scrollView, panelTex, panelHoverTex);

            Skin.label = new GUIStyle(Skin.label)
            {
                normal = { textColor = TextColor },
                richText = true,
                wordWrap = false
            };
            // Pangram's glyphs extend a little below the line metrics Unity
            // reports to IMGUI. Reserve real vertical space for descenders in
            // ordinary labels such as ABMX names and selector captions.
            Skin.label.padding = new RectOffset(
                Skin.label.padding.left,
                Skin.label.padding.right,
                1,
                4);

            LargeLabelStyle = new GUIStyle(Skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                richText = true,
                normal = { textColor = TextColor }
            };

            Skin.button = ButtonStyle(Skin.button, buttonTex, buttonHoverTex, buttonActiveTex, TextColor);
            PrimaryButtonStyle = ButtonStyle(Skin.button, accentTex, accentHoverTex, accentActiveTex, Color.white);
            CategoryButtonStyle = ButtonStyle(Skin.button, buttonTex, buttonHoverTex, buttonActiveTex, TextColor);

            Skin.textField = FieldStyle(Skin.textField, fieldTex, fieldFocusTex);
            Skin.textArea = FieldStyle(Skin.textArea, fieldTex, fieldFocusTex);

            Skin.toggle = ToggleStyle(Skin.toggle);

            TextureTextStyle = PanelStyle(Skin.box, panelTex, panelHoverTex);
            TextureTextStyle.alignment = TextAnchor.MiddleCenter;
            TextureTextStyle.richText = true;
            TextureTextStyle.normal.textColor = MutedTextColor;

            ColorSwatchButtonStyle = new GUIStyle(Skin.button);
            ColorSwatchButtonStyle.padding = new RectOffset(2, 2, 2, 2);
            ColorSwatchButtonStyle.margin = new RectOffset(4, 4, 2, 2);

            Skin.horizontalScrollbar = ScrollBarStyle(Skin.horizontalScrollbar, scrollTrackTex, -1f, 8f);
            // Keep the proportional thumb, but give the preview/category
            // scroll bar a large enough hit target to grab comfortably.
            Skin.verticalScrollbar = ScrollBarStyle(Skin.verticalScrollbar, scrollTrackTex, 18f, -1f);
            Skin.horizontalScrollbarThumb = ScrollBarStyle(Skin.horizontalScrollbarThumb, scrollThumbTex, -1f, 8f);
            Skin.verticalScrollbarThumb = ScrollBarStyle(Skin.verticalScrollbarThumb, scrollThumbTex, 18f, 34f);
            Skin.verticalScrollbarThumb.stretchHeight = true;
            Skin.horizontalScrollbarLeftButton = HiddenScrollButton(Skin.horizontalScrollbarLeftButton, clearTex);
            Skin.horizontalScrollbarRightButton = HiddenScrollButton(Skin.horizontalScrollbarRightButton, clearTex);
            Skin.verticalScrollbarUpButton = HiddenScrollButton(Skin.verticalScrollbarUpButton, clearTex);
            Skin.verticalScrollbarDownButton = HiddenScrollButton(Skin.verticalScrollbarDownButton, clearTex);
            Skin.horizontalSlider = SliderTrackStyle(Skin.horizontalSlider, sliderTrackTex);
            Skin.horizontalSliderThumb = SliderThumbStyle(Skin.horizontalSliderThumb, sliderThumbTex);

            GUIStyle dangerButton = ButtonStyle(Skin.button, dangerTex, dangerTex, dangerTex, Color.white);
            CloseButtonStyle = CloseStyle(Skin.button, closeTex);
            Skin.customStyles = AppendStyles(Skin.customStyles, dangerButton);
            ApplyFont(themeFont);
        }

        private void BuildMainGameSkin(Font themeFont)
        {
            Texture2D clearTex = RoundedRectTexture(8, 8, Transparent, Transparent, 0, 0);
            Texture2D panelTex = ThemeTexture("sp_ai_system_09_00.png", 252, 252, Rgba(35, 36, 38, 194), Rgba(116, 116, 112, 170), 2, 1, 0.76f);
            // In the original uGUI the pale selection sprite is tinted green
            // by the Image component. IMGUI does not preserve that tint, so a
            // full-height translucent texture is generated explicitly.
            Texture2D selectedTex = RoundedRectTexture(
                64,
                32,
                Rgba(76, 151, 43, 148),
                Rgba(130, 190, 72, 92),
                0,
                1);
            MainGameSelectorSelectedTexture = RoundedRectTexture(
                64,
                64,
                Rgba(82, 151, 42, 76),
                Rgba(205, 211, 49, 255),
                0,
                3);
            Texture2D sectionTex = ThemeTexture("sp_ai_system_18_00.png", 253, 40, Rgba(225, 220, 209), Rgba(87, 86, 82), 0, 0);
            Texture2D buttonTex = ThemeTexture("sp_ai_make_23_00.png", 114, 44, Rgba(228, 222, 211), Rgba(91, 89, 84), 3, 1);
            Texture2D buttonHoverTex = ThemeTexture("sp_ai_make_23_01.png", 114, 44, Rgba(205, 202, 48), Rgba(91, 89, 84), 3, 1);
            Texture2D tabTex = ThemeTexture("sp_ai_make_22_00.png", 90, 37, Rgba(226, 220, 209), Rgba(75, 74, 71), 2, 1);
            Texture2D tabHoverTex = ThemeTexture("sp_ai_make_22_01.png", 90, 37, Rgba(238, 232, 220), Rgba(75, 74, 71), 2, 1);
            Texture2D tabSelectedTex = ThemeTexture("sp_ai_make_22_02.png", 90, 37, Rgba(210, 204, 48), Rgba(75, 74, 71), 2, 1);
            Texture2D closeTex = ThemeTexture("sp_ai_system_02_00.png", 126, 126, Rgba(236, 232, 222), Rgba(64, 64, 62), 0, 0);
            Texture2D closeHoverTex = ThemeTexture("sp_ai_system_02_01.png", 126, 126, Rgba(211, 204, 46), Rgba(64, 64, 62), 0, 0);
            Texture2D fieldTex = RoundedRectTexture(48, 32, Rgba(17, 18, 19, 235), Rgba(219, 214, 204), 1, 1);
            Texture2D fieldFocusTex = RoundedRectTexture(48, 32, Rgba(17, 18, 19, 245), Rgba(211, 204, 46), 1, 1);
            Texture2D scrollTrackTex = RoundedRectTexture(20, 32, Rgba(225, 220, 210, 45), Rgba(225, 220, 210, 45), 1, 0);
            Texture2D scrollThumbTex = RoundedRectTexture(20, 32, Rgba(231, 226, 216, 230), Rgba(64, 64, 62), 1, 1);
            Texture2D sliderTrackTex = ThemeTexture("sp_ai_make_14_00.png", 163, 8, Rgba(225, 220, 210), Rgba(225, 220, 210), 0, 0);
            MainGameSliderTrackTexture = sliderTrackTex;
            // The original thumb sprite contains an opaque dark polygon around
            // the pale circle. It becomes visible as black edge pixels after
            // IMGUI scales it down, so use the same pale circle with a clean
            // antialiased transparent edge.
            Texture2D sliderThumbTex = CircleTexture(64, 4, Rgba(242, 239, 226));
            Texture2D numericValueTex = RoundedRectTexture(48, 42, Rgba(3, 3, 3, 245), Rgba(3, 3, 3, 245), 0, 0);
            MainGameDividerTexture = ThemeTexture("sp_ai_pouch_01_00.png", 512, 8, Rgba(225, 220, 210), Rgba(225, 220, 210), 0, 0);

            MainGameCategoryNormal = new Texture2D[6];
            MainGameCategorySelected = new Texture2D[6];
            for (int i = 0; i < 6; i++)
            {
                MainGameCategoryNormal[i] = ThemeTexture($"sp_ai_make_{i:00}_00.png", 128, 128, Transparent, Transparent, 0, 0);
                MainGameCategorySelected[i] = ThemeTexture($"sp_ai_make_{i:00}_01.png", 128, 128, Transparent, Transparent, 0, 0);
            }

            ToggleOffTexture = ThemeTexture("sp_ai_system_19_00.png", 124, 124, Transparent, Transparent, 0, 0);
            ToggleOnTexture = ThemeTexture("sp_ai_system_19_01.png", 124, 124, Transparent, Transparent, 0, 0);
            MainGameCheckboxOffTexture = ThemeTexture("sp_ai_system_20_00.png", 32, 32, Transparent, Transparent, 0, 0);
            MainGameCheckboxOnTexture = ThemeTexture("sp_ai_system_20_01.png", 32, 32, Transparent, Transparent, 0, 0);
            MainGamePanelWindowStyle = new GUIStyle(Skin.window);
            SetAllBackgrounds(MainGamePanelWindowStyle, panelTex, panelTex, panelTex);
            SetAllTextColors(MainGamePanelWindowStyle, Rgba(237, 232, 222));
            MainGamePanelWindowStyle.border = new RectOffset(12, 12, 12, 12);
            MainGamePanelWindowStyle.padding = new RectOffset(16, 16, 56, 14);
            MainGamePanelWindowStyle.margin = new RectOffset(0, 0, 0, 0);
            MainGamePanelWindowStyle.alignment = TextAnchor.UpperLeft;
            MainGamePanelWindowStyle.fontSize = 24;
            MainGamePanelWindowStyle.fontStyle = FontStyle.Normal;

            MainGameTransparentWindowStyle = new GUIStyle(MainGamePanelWindowStyle);
            SetAllBackgrounds(MainGameTransparentWindowStyle, clearTex, clearTex, clearTex);
            MainGameTransparentWindowStyle.border = new RectOffset(0, 0, 0, 0);
            MainGameTransparentWindowStyle.padding = new RectOffset(0, 0, 0, 0);

            WindowStyle = MainGamePanelWindowStyle;
            Skin.window = MainGamePanelWindowStyle;

            Skin.box = PanelStyle(Skin.box, clearTex, clearTex);
            Skin.box.border = new RectOffset(0, 0, 0, 0);
            Skin.box.padding = new RectOffset(4, 4, 4, 4);
            Skin.box.margin = new RectOffset(0, 0, 0, 0);
            Skin.scrollView = new GUIStyle(Skin.box);

            Skin.label = new GUIStyle(Skin.label)
            {
                richText = true,
                wordWrap = false,
                fontSize = 16,
                normal = { textColor = Rgba(237, 232, 222) }
            };
            SetAllTextColors(Skin.label, Rgba(237, 232, 222));
            Skin.label.padding = new RectOffset(2, 2, 2, 3);

            LargeLabelStyle = new GUIStyle(Skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Normal,
                richText = true,
                normal = { textColor = Rgba(237, 232, 222) }
            };

            Skin.button = ButtonStyle(Skin.button, buttonTex, buttonHoverTex, buttonHoverTex, Rgba(43, 43, 41));
            Skin.button.fontSize = 16;
            Skin.button.fixedHeight = 34f;
            Skin.button.border = new RectOffset(12, 12, 12, 12);
            PrimaryButtonStyle = ButtonStyle(Skin.button, buttonHoverTex, tabSelectedTex, buttonHoverTex, Rgba(43, 43, 41));
            PrimaryButtonStyle.fontSize = 17;
            PrimaryButtonStyle.fixedHeight = 36f;
            CategoryButtonStyle = new GUIStyle(Skin.button);

            Skin.textField = FieldStyle(Skin.textField, fieldTex, fieldFocusTex);
            Skin.textField.fixedHeight = 32f;
            Skin.textArea = FieldStyle(Skin.textArea, fieldTex, fieldFocusTex);
            MainGameNumericValueStyle = new GUIStyle(Skin.textField)
            {
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(0, 0, 0, 0),
                padding = new RectOffset(2, 2, 0, 5),
                margin = new RectOffset(0, 0, 0, 0),
                fixedHeight = 42f
            };
            SetAllBackgrounds(MainGameNumericValueStyle, numericValueTex, numericValueTex, numericValueTex);
            SetAllTextColors(MainGameNumericValueStyle, Rgba(237, 232, 222));
            Skin.toggle = ToggleStyle(Skin.toggle);

            TextureTextStyle = new GUIStyle(Skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                richText = true,
                normal = { textColor = Rgba(237, 232, 222) }
            };
            ColorSwatchButtonStyle = new GUIStyle(Skin.button)
            {
                padding = new RectOffset(2, 2, 2, 2),
                margin = new RectOffset(4, 4, 2, 2)
            };

            Skin.horizontalScrollbar = ScrollBarStyle(Skin.horizontalScrollbar, scrollTrackTex, -1f, 8f);
            Skin.verticalScrollbar = ScrollBarStyle(Skin.verticalScrollbar, scrollTrackTex, 18f, -1f);
            Skin.horizontalScrollbarThumb = ScrollBarStyle(Skin.horizontalScrollbarThumb, scrollThumbTex, -1f, 8f);
            // Standard panels must use Unity's proportional thumb size. A
            // global fixed height makes the visual thumb disagree with the
            // scroll range, so it cannot travel to the bottom in Mesh, Adjust
            // and the left navigation. Preview grids opt into a minimum size
            // locally while they are being drawn.
            Skin.verticalScrollbarThumb = ScrollBarStyle(Skin.verticalScrollbarThumb, scrollThumbTex, 18f, 0f);
            Skin.verticalScrollbarThumb.stretchHeight = true;
            Skin.horizontalScrollbarLeftButton = HiddenScrollButton(Skin.horizontalScrollbarLeftButton, clearTex);
            Skin.horizontalScrollbarRightButton = HiddenScrollButton(Skin.horizontalScrollbarRightButton, clearTex);
            Skin.verticalScrollbarUpButton = HiddenScrollButton(Skin.verticalScrollbarUpButton, clearTex);
            Skin.verticalScrollbarDownButton = HiddenScrollButton(Skin.verticalScrollbarDownButton, clearTex);
            Skin.horizontalSlider = SliderTrackStyle(Skin.horizontalSlider, sliderTrackTex);
            // sp_ai_make_14_00 already contains its own transparent one-pixel
            // edge. A 2px nine-slice border removes four of its six visible
            // rows and leaves the 2px line seen in Studio.
            Skin.horizontalSlider.border = new RectOffset(0, 0, 0, 0);
            Skin.horizontalSlider.fixedHeight = 12f;
            Skin.horizontalSliderThumb = SliderThumbStyle(Skin.horizontalSliderThumb, sliderThumbTex);
            Skin.horizontalSliderThumb.border = new RectOffset(0, 0, 0, 0);
            Skin.horizontalSliderThumb.fixedWidth = 22f;
            Skin.horizontalSliderThumb.fixedHeight = 22f;

            CloseButtonStyle = CloseStyle(Skin.button, closeTex);
            CloseButtonStyle.hover.background = closeHoverTex;
            CloseButtonStyle.active.background = closeHoverTex;
            CloseButtonStyle.fixedWidth = 28f;
            CloseButtonStyle.fixedHeight = 28f;

            MainGameSectionHeaderStyle = new GUIStyle(Skin.label);
            SetAllBackgrounds(MainGameSectionHeaderStyle, sectionTex, sectionTex, sectionTex);
            SetAllTextColors(MainGameSectionHeaderStyle, Rgba(43, 43, 41));
            MainGameSectionHeaderStyle.border = new RectOffset(14, 14, 0, 0);
            MainGameSectionHeaderStyle.padding = new RectOffset(14, 4, 2, 3);
            MainGameSectionHeaderStyle.margin = new RectOffset(0, 0, 0, 0);
            MainGameSectionHeaderStyle.fixedHeight = 40f;
            MainGameSectionHeaderStyle.fontSize = 25;

            MainGameListButtonStyle = new GUIStyle(Skin.label);
            SetAllBackgrounds(MainGameListButtonStyle, clearTex, selectedTex, selectedTex);
            SetAllTextColors(MainGameListButtonStyle, Rgba(237, 232, 222));
            MainGameListButtonStyle.alignment = TextAnchor.MiddleLeft;
            MainGameListButtonStyle.padding = new RectOffset(14, 8, 2, 4);
            MainGameListButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            MainGameListButtonStyle.border = new RectOffset(10, 10, 10, 10);
            MainGameListButtonStyle.fixedHeight = 36f;
            MainGameListButtonStyle.fontSize = 20;

            MainGameListSelectedStyle = new GUIStyle(MainGameListButtonStyle);
            SetAllBackgrounds(MainGameListSelectedStyle, selectedTex, selectedTex, selectedTex);
            SetAllTextColors(MainGameListSelectedStyle, Rgba(237, 232, 222));
            MainGameListSelectedStyle.fontStyle = FontStyle.Bold;

            MainGameListMultiSelectedStyle = new GUIStyle(MainGameListSelectedStyle);
            SetAllTextColors(MainGameListMultiSelectedStyle, Rgba(236, 221, 56));

            MainGameIconButtonStyle = new GUIStyle(Skin.button);
            SetAllBackgrounds(MainGameIconButtonStyle, clearTex, clearTex, clearTex);
            MainGameIconButtonStyle.border = new RectOffset(0, 0, 0, 0);
            MainGameIconButtonStyle.padding = new RectOffset(0, 0, 0, 0);
            MainGameIconButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            MainGameIconButtonStyle.fixedHeight = 0f;

            MainGameTitleStyle = new GUIStyle(Skin.label)
            {
                fontSize = 29,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Rgba(237, 232, 222) }
            };
            MainGameBreadcrumbStyle = new GUIStyle(Skin.label)
            {
                fontSize = 22,
                fixedHeight = 36f,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Rgba(237, 232, 222) }
            };

            MainGameTabStyle = ButtonStyle(Skin.button, tabTex, tabHoverTex, tabHoverTex, Rgba(43, 43, 41));
            MainGameTabStyle.fixedWidth = 90f;
            MainGameTabStyle.fixedHeight = 37f;
            MainGameTabStyle.fontSize = 19;
            MainGameTabStyle.alignment = TextAnchor.MiddleCenter;
            MainGameTabStyle.padding = new RectOffset(6, 6, 0, 4);
            MainGameTabStyle.contentOffset = new Vector2(0f, -2f);
            MainGameTabSelectedStyle = ButtonStyle(Skin.button, tabSelectedTex, tabSelectedTex, tabSelectedTex, Rgba(43, 43, 41));
            MainGameTabSelectedStyle.fixedWidth = 90f;
            MainGameTabSelectedStyle.fixedHeight = 37f;
            MainGameTabSelectedStyle.fontSize = 19;
            MainGameTabSelectedStyle.alignment = TextAnchor.MiddleCenter;
            MainGameTabSelectedStyle.padding = new RectOffset(6, 6, 0, 4);
            MainGameTabSelectedStyle.contentOffset = new Vector2(0f, -2f);

            ApplyFont(themeFont);
        }

        public void Dispose()
        {
            for (int i = 0; i < ownedResources.Count; i++)
            {
                if (ownedResources[i] != null)
                {
                    UnityEngine.Object.Destroy(ownedResources[i]);
                }
            }
            ownedResources.Clear();
            Skin = null;
        }

        private GUIStyle PanelStyle(GUIStyle source, Texture2D normal, Texture2D hover)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.focused.background = hover;
            style.normal.textColor = TextColor;
            style.hover.textColor = TextColor;
            style.active.textColor = TextColor;
            style.focused.textColor = TextColor;
            style.richText = true;
            style.border = new RectOffset(4, 4, 4, 4);
            // Separator headers (for example "ABMX Body") use the box style.
            // Pangram's y/g/p/q descenders sit below Unity's reported line
            // metrics, so keep the same total height but move the text area up.
            style.padding = new RectOffset(7, 7, 4, 8);
            style.margin = new RectOffset(3, 3, 3, 3);
            return style;
        }

        private GUIStyle ButtonStyle(GUIStyle source, Texture2D normal, Texture2D hover, Texture2D active, Color textColor)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.normal.textColor = textColor;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.richText = true;
            style.alignment = TextAnchor.MiddleCenter;
            style.border = new RectOffset(4, 4, 4, 4);
            // Keep enough usable content height for Pangram's descenders.
            // Large padding inside a fixed-height button clips letters such as
            // p, q, g and y even though the outer button itself looks tall.
            style.padding = new RectOffset(8, 8, 2, 3);
            style.margin = new RectOffset(2, 2, 2, 2);
            style.fixedHeight = 26f;
            return style;
        }

        private GUIStyle FieldStyle(GUIStyle source, Texture2D normal, Texture2D focused)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = normal;
            style.hover.background = focused;
            style.focused.background = focused;
            style.active.background = focused;
            style.normal.textColor = TextColor;
            style.hover.textColor = TextColor;
            style.focused.textColor = Color.white;
            style.active.textColor = Color.white;
            style.richText = false;
            style.border = new RectOffset(4, 4, 4, 4);
            style.padding = new RectOffset(8, 8, 3, 4);
            style.margin = new RectOffset(2, 2, 2, 2);
            return style;
        }

        private GUIStyle ToggleStyle(GUIStyle source)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;
            style.normal.textColor = TextColor;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.focused.textColor = Color.white;
            style.onNormal.textColor = Color.white;
            style.onHover.textColor = Color.white;
            style.onActive.textColor = Color.white;
            style.onFocused.textColor = Color.white;
            style.richText = true;
            style.alignment = TextAnchor.MiddleLeft;
            style.border = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 4, 1, 3);
            style.margin = new RectOffset(2, 2, 1, 1);
            style.fixedHeight = 22f;
            return style;
        }

        private GUIStyle ScrollBarStyle(GUIStyle source, Texture2D texture, float fixedWidth = -1f, float fixedHeight = -1f)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.border = new RectOffset(3, 3, 3, 3);
            style.margin = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(0, 0, 0, 0);
            if (fixedWidth >= 0f)
            {
                style.fixedWidth = fixedWidth;
            }
            if (fixedHeight >= 0f)
            {
                style.fixedHeight = fixedHeight;
            }
            return style;
        }

        private GUIStyle SliderTrackStyle(GUIStyle source, Texture2D texture)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.border = new RectOffset(2, 2, 2, 2);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.fixedHeight = 3f;
            return style;
        }

        private GUIStyle SliderThumbStyle(GUIStyle source, Texture2D texture)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.border = new RectOffset(2, 2, 2, 2);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.fixedWidth = 10f;
            style.fixedHeight = 10f;
            return style;
        }

        private GUIStyle CloseStyle(GUIStyle source, Texture2D texture)
        {
            GUIStyle style = new GUIStyle(source);
            style.normal.background = texture;
            style.hover.background = texture;
            style.active.background = texture;
            style.focused.background = texture;
            style.border = new RectOffset(1, 1, 1, 1);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.fixedWidth = 14f;
            style.fixedHeight = 14f;
            style.stretchWidth = false;
            style.stretchHeight = false;
            return style;
        }

        private GUIStyle HiddenScrollButton(GUIStyle source, Texture2D texture)
        {
            GUIStyle style = ScrollBarStyle(source, texture);
            // A zero fixed size means "automatic" to IMGUI, so the transparent
            // 24x24 texture still reserved a large invisible button at each end
            // of the scrollbar. Keep the controls present, but make their travel
            // reservation effectively disappear.
            style.fixedWidth = 1f;
            style.fixedHeight = 1f;
            style.margin = new RectOffset(0, 0, 0, 0);
            style.padding = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private void ApplyFont(Font font)
        {
            if (font == null || Skin == null)
            {
                return;
            }

            Skin.font = font;
            ApplyFont(Skin.box, font);
            ApplyFont(Skin.button, font);
            ApplyFont(Skin.label, font);
            ApplyFont(Skin.textField, font);
            ApplyFont(Skin.textArea, font);
            ApplyFont(Skin.toggle, font);
            ApplyFont(Skin.window, font);
            ApplyFont(Skin.scrollView, font);
            ApplyFont(Skin.horizontalScrollbar, font);
            ApplyFont(Skin.verticalScrollbar, font);
            ApplyFont(Skin.horizontalScrollbarThumb, font);
            ApplyFont(Skin.verticalScrollbarThumb, font);
            ApplyFont(Skin.horizontalSlider, font);
            ApplyFont(Skin.horizontalSliderThumb, font);
            ApplyFont(WindowStyle, font);
            ApplyFont(LargeLabelStyle, font);
            ApplyFont(PrimaryButtonStyle, font);
            ApplyFont(CategoryButtonStyle, font);
            ApplyFont(TextureTextStyle, font);
            ApplyFont(ColorSwatchButtonStyle, font);
            ApplyFont(CloseButtonStyle, font);
            ApplyFont(MainGameTransparentWindowStyle, font);
            ApplyFont(MainGamePanelWindowStyle, font);
            ApplyFont(MainGameSectionHeaderStyle, font);
            ApplyFont(MainGameListButtonStyle, font);
            ApplyFont(MainGameListSelectedStyle, font);
            ApplyFont(MainGameListMultiSelectedStyle, font);
            ApplyFont(MainGameIconButtonStyle, font);
            ApplyFont(MainGameTitleStyle, font);
            ApplyFont(MainGameBreadcrumbStyle, font);
            ApplyFont(MainGameTabStyle, font);
            ApplyFont(MainGameTabSelectedStyle, font);
            if (Skin.customStyles != null)
            {
                for (int i = 0; i < Skin.customStyles.Length; i++)
                {
                    ApplyFont(Skin.customStyles[i], font);
                }
            }
        }

        private static void ApplyFont(GUIStyle style, Font font)
        {
            if (style != null)
            {
                style.font = font;
            }
        }

        private Texture2D ThemeTexture(string fileName, int width, int height, Color fill, Color border, int radius, int borderWidth, float embeddedOpacity = 1f)
        {
            Texture2D embedded = LoadEmbeddedTexture(fileName);
            if (embedded != null)
            {
                ApplyEmbeddedOpacity(embedded, embeddedOpacity);
            }
            return embedded ?? RoundedRectTexture(width, height, fill, border, radius, borderWidth);
        }

        private Font LoadMainGameFont()
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo UI", "Arial" },
                    16);
                if (font != null)
                {
                    font.hideFlags = HideFlags.HideAndDontSave;
                    ownedResources.Add(font);
                }
                return font;
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning($"Failed to load MainGame UI font: {ex.Message}");
                return null;
            }
        }

        private static void SetAllBackgrounds(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active)
        {
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.focused.background = hover;
            style.onNormal.background = active;
            style.onHover.background = active;
            style.onActive.background = active;
            style.onFocused.background = active;
        }

        private static void SetAllTextColors(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static void ApplyEmbeddedOpacity(Texture2D texture, float opacity)
        {
            if (texture == null || opacity >= 0.995f)
            {
                return;
            }

            opacity = Mathf.Clamp01(opacity);
            Color[] pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a *= opacity;
            }
            texture.SetPixels(pixels);
            texture.Apply(false, false);
        }

        private Font LoadEmbeddedFont(string fileName)
        {
            byte[] data = LoadEmbeddedBytes(fileName);
            if (data == null || data.Length == 0)
            {
                return null;
            }

            string fontPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "Windows",
                "Fonts",
                fileName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fontPath));
                if (!File.Exists(fontPath) || new FileInfo(fontPath).Length != data.Length)
                {
                    File.WriteAllBytes(fontPath, data);
                }

                RegisterFontForCurrentUser(fontPath);
                AddFontResourceEx(fontPath, FrPrivate, IntPtr.Zero);
                IntPtr result;
                SendMessageTimeout(
                    new IntPtr(HwndBroadcast),
                    WmFontChange,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    SmtoAbortIfHung,
                    1000,
                    out result);

                Font font = Font.CreateDynamicFontFromOSFont(new[] { "Pangram Light", "Pangram" }, 13);
                if (font != null)
                {
                    font.hideFlags = HideFlags.HideAndDontSave;
                    ownedResources.Add(font);
                }

                return font;
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning($"Failed to load UI font {fileName}: {ex.Message}");
                return null;
            }
        }

        private static void RegisterFontForCurrentUser(string fontPath)
        {
            try
            {
                using (RegistryKey fontsKey = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows NT\CurrentVersion\Fonts"))
                {
                    fontsKey?.SetValue("Pangram Light (OpenType)", fontPath, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                StudioCharaEditor.Logger?.LogWarning($"Failed to register UI font for current user: {ex.Message}");
            }
        }

        private Texture2D LoadEmbeddedTexture(string fileName)
        {
            byte[] data = LoadEmbeddedBytes(fileName);
            if (data != null)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                if (!ImageConversion.LoadImage(texture, data))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = "StudioCharaEditor." + fileName;
                ownedResources.Add(texture);
                return texture;
            }

            return null;
        }

        private static byte[] LoadEmbeddedBytes(string fileName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                return null;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null || stream.Length <= 0 || stream.Length > int.MaxValue)
                {
                    return null;
                }

                byte[] data = new byte[(int)stream.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int read = stream.Read(data, offset, data.Length - offset);
                    if (read <= 0)
                    {
                        break;
                    }

                    offset += read;
                }

                return data;
            }
        }

        private Texture2D RoundedRectTexture(int width, int height, Color fill, Color border, int radius, int borderWidth)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            ownedResources.Add(texture);

            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool insideOuter = InsideRoundedRect(x, y, width, height, radius);
                    if (!insideOuter)
                    {
                        pixels[y * width + x] = Transparent;
                        continue;
                    }

                    bool insideInner = borderWidth <= 0 || InsideRoundedRect(
                        x - borderWidth,
                        y - borderWidth,
                        width - borderWidth * 2,
                        height - borderWidth * 2,
                        Math.Max(0, radius - borderWidth));
                    pixels[y * width + x] = insideInner ? fill : border;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private Texture2D CircleTexture(int size, int padding, Color fill)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            ownedResources.Add(texture);

            float center = (size - 1f) * 0.5f;
            float radius = Math.Max(1f, center - padding);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float coverage = Mathf.Clamp01(radius + 0.5f - distance);
                    pixels[y * size + x] = new Color(
                        fill.r,
                        fill.g,
                        fill.b,
                        fill.a * coverage);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static bool InsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }
            if (radius <= 0)
            {
                return x >= 0 && y >= 0 && x < width && y < height;
            }

            int clampedRadius = Math.Min(radius, Math.Min(width, height) / 2);
            if ((x >= clampedRadius && x < width - clampedRadius) ||
                (y >= clampedRadius && y < height - clampedRadius))
            {
                return x >= 0 && y >= 0 && x < width && y < height;
            }

            int cx = x < clampedRadius ? clampedRadius : width - clampedRadius - 1;
            int cy = y < clampedRadius ? clampedRadius : height - clampedRadius - 1;
            int dx = x - cx;
            int dy = y - cy;
            return dx * dx + dy * dy <= clampedRadius * clampedRadius;
        }

        private static GUIStyle[] AppendStyles(GUIStyle[] styles, GUIStyle style)
        {
            if (styles == null)
            {
                return new[] { style };
            }

            GUIStyle[] result = new GUIStyle[styles.Length + 1];
            Array.Copy(styles, result, styles.Length);
            result[result.Length - 1] = style;
            return result;
        }

        private static Color Rgba(byte r, byte g, byte b, byte a = 255)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }
    }
}
