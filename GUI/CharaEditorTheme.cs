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

        private static readonly Color TextColor = Rgba(232, 238, 241);
        private static readonly Color MutedTextColor = Rgba(174, 185, 191);
        private static readonly Color WindowFill = Rgba(24, 27, 30, 244);
        private static readonly Color PanelFill = Rgba(33, 37, 41, 238);
        private static readonly Color FieldFill = Rgba(18, 20, 23, 242);
        private static readonly Color Stroke = Rgba(70, 78, 84, 226);
        private static readonly Color StrokeSoft = Rgba(58, 65, 71, 214);
        private static readonly Color Accent = Rgba(42, 184, 154);
        private static readonly Color AccentHover = Rgba(55, 205, 175);
        private static readonly Color AccentActive = Rgba(30, 151, 128);
        private static readonly Color Danger = Rgba(205, 76, 78);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        private const uint FrPrivate = 0x10;
        private const int HwndBroadcast = 0xffff;
        private const int WmFontChange = 0x001D;
        private const int SmtoAbortIfHung = 0x0002;

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
            Font themeFont = LoadEmbeddedFont("Pangram-Light.otf");
            if (themeFont != null)
            {
                Skin.font = themeFont;
            }

            Texture2D windowTex = ThemeTexture("ui_window.png", 64, 64, WindowFill, Rgba(92, 103, 111, 235), 5, 1);
            Texture2D panelTex = ThemeTexture("ui_panel.png", 64, 64, PanelFill, StrokeSoft, 3, 1);
            Texture2D panelHoverTex = ThemeTexture("ui_panel_hover.png", 64, 64, Rgba(41, 46, 51, 240), Rgba(90, 102, 111, 226), 3, 1);
            Texture2D fieldTex = ThemeTexture("ui_field.png", 48, 48, FieldFill, Stroke, 2, 1);
            Texture2D fieldFocusTex = ThemeTexture("ui_field_focus.png", 48, 48, FieldFill, Accent, 2, 1);
            Texture2D buttonTex = ThemeTexture("ui_button.png", 48, 48, Rgba(48, 55, 61, 242), Rgba(91, 103, 112, 228), 2, 1);
            Texture2D buttonHoverTex = ThemeTexture("ui_button_hover.png", 48, 48, Rgba(58, 67, 74, 245), Rgba(117, 132, 142, 235), 2, 1);
            Texture2D buttonActiveTex = ThemeTexture("ui_button_active.png", 48, 48, Rgba(35, 41, 46, 246), Accent, 2, 1);
            Texture2D accentTex = ThemeTexture("ui_accent.png", 48, 48, Accent, AccentHover, 2, 1);
            Texture2D accentHoverTex = ThemeTexture("ui_accent_hover.png", 48, 48, AccentHover, Rgba(130, 242, 218), 2, 1);
            Texture2D accentActiveTex = ThemeTexture("ui_accent_active.png", 48, 48, AccentActive, AccentHover, 2, 1);
            Texture2D dangerTex = ThemeTexture("ui_danger.png", 48, 48, Danger, Rgba(238, 117, 119), 1, 1);
            Texture2D clearTex = ThemeTexture("ui_clear.png", 24, 24, Transparent, Transparent, 5, 0);
            Texture2D closeTex = ThemeTexture("ui_close.png", 18, 18, Rgba(120, 16, 20), Rgba(205, 48, 54), 0, 1);
            Texture2D scrollTrackTex = ThemeTexture("ui_scroll_track.png", 32, 32, Rgba(18, 21, 24, 200), Rgba(18, 21, 24, 200), 2, 0);
            Texture2D scrollThumbTex = ThemeTexture("ui_scroll_thumb.png", 32, 32, Rgba(92, 105, 114, 235), Rgba(119, 135, 146, 235), 2, 1);
            Texture2D sliderTrackTex = ThemeTexture("ui_slider_track.png", 64, 8, Rgba(14, 16, 18, 225), Rgba(48, 56, 62, 230), 1, 1);
            Texture2D sliderThumbTex = ThemeTexture("ui_slider_thumb.png", 8, 8, Rgba(150, 161, 168), Rgba(199, 209, 214), 1, 1);
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
            Skin.verticalScrollbar = ScrollBarStyle(Skin.verticalScrollbar, scrollTrackTex, 12f, -1f);
            Skin.horizontalScrollbarThumb = ScrollBarStyle(Skin.horizontalScrollbarThumb, scrollThumbTex, -1f, 8f);
            Skin.verticalScrollbarThumb = ScrollBarStyle(Skin.verticalScrollbarThumb, scrollThumbTex, 12f, 28f);
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
            style.padding = new RectOffset(7, 7, 6, 6);
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
            style.padding = new RectOffset(8, 8, 4, 4);
            style.margin = new RectOffset(2, 2, 2, 2);
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
            style.padding = new RectOffset(8, 8, 5, 5);
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
            style.padding = new RectOffset(0, 4, 0, 0);
            style.margin = new RectOffset(2, 2, 1, 1);
            style.fixedHeight = 16f;
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
            style.fixedWidth = 8f;
            style.fixedHeight = 8f;
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
            return style;
        }

        private GUIStyle HiddenScrollButton(GUIStyle source, Texture2D texture)
        {
            GUIStyle style = ScrollBarStyle(source, texture);
            style.fixedWidth = 0;
            style.fixedHeight = 0;
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

        private Texture2D ThemeTexture(string fileName, int width, int height, Color fill, Color border, int radius, int borderWidth)
        {
            Texture2D embedded = LoadEmbeddedTexture(fileName);
            return embedded ?? RoundedRectTexture(width, height, fill, border, radius, borderWidth);
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
