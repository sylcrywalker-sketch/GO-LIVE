using GoLive.Localization;
using GoLive.Desktop;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Editor.Desktop
{
    // Authoring helpers only. The saved prefab owns its UI hierarchy and references at runtime.
    internal sealed class DesktopUiAuthoring
    {
        internal readonly TMP_FontAsset Font;
        internal readonly LocalizationContext Localization;
        internal readonly Color Ink;
        internal readonly Color Muted;
        internal readonly Color Blue;
        internal readonly Color Paper;
        internal readonly Color Line;
        private readonly Color Field;
        private readonly TMP_FontAsset HeadingFont;

        internal DesktopUiAuthoring(TMP_FontAsset font, LocalizationContext localization, bool dark=false, Color? accent=null)
        {
            Font = font;
            Localization = localization;
            HeadingFont=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Game/UI/Fonts/Manrope/Manrope-SemiBold SDF.asset");
            Ink=dark?new Color(.92f,.94f,.97f):new Color(.08f,.12f,.19f);
            Muted=dark?new Color(.61f,.65f,.72f):new Color(.36f,.43f,.53f);
            Blue=accent??new Color(.08f,.35f,.60f);
            Paper=dark?new Color(.073f,.086f,.11f):new Color(.96f,.975f,.99f);
            Line=dark?new Color(.19f,.23f,.30f):new Color(.74f,.81f,.87f);
            Field=dark?new Color(.10f,.12f,.16f):Color.white;
        }
        internal DesktopUiAuthoring WithTheme(bool dark,Color accent) => new(Font,Localization,dark,accent);

        internal RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal Image Panel(string name, Transform parent, float x, float y, float width, float height, Color color, bool hit = false)
        {
            Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<DesktopPanelGraphic>();
            image.color = color;
            image.raycastTarget = hit;
            return image;
        }

        internal TextMeshProUGUI Text(string name, Transform parent, float x, float y, float width, float height,
            string value, float size = 22f, Color? color = null, bool bold = false)
        {
            // Manrope includes a tall ascent/descent box; allow one full line even in compact labels.
            float lineHeight=size*Font.faceInfo.lineHeight/Font.faceInfo.pointSize;
            TextMeshProUGUI text = Rect(name, parent, x, y, width, Mathf.Max(height,Mathf.Ceil(lineHeight)+2)).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = bold && HeadingFont!=null ? HeadingFont : Font;
            text.fontSize = size;
            text.color = color ?? Ink;
            text.fontStyle = FontStyles.Normal;
            text.text = value;
            text.richText = false;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        internal TextMeshProUGUI Label(string key, Transform parent, float x, float y, float width, float height,
            float size = 22f, Color? color = null, bool bold = false)
        {
            TextMeshProUGUI text = Text(key, parent, x, y, width, height, key, size, color, bold);
            LocalizedTextView localized = text.gameObject.AddComponent<LocalizedTextView>();
            Set(localized, "_localization", Localization);
            String(localized, "_key", key);
            return text;
        }

        internal Button Button(string key, Transform parent, float x, float y, float width, float height, bool primary = false)
        {
            Image plate = Panel(key, parent, x, y, width, height, primary ? Blue : Field, true);
            Button button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.18f,1.18f,1.22f);
            colors.pressedColor = new Color(.80f,.85f,.94f);
            colors.selectedColor = new Color(1.12f,1.16f,1.23f);
            colors.disabledColor = new Color(.64f,.66f,.71f,.72f);
            colors.fadeDuration=.12f;
            button.colors = colors;
            TextMeshProUGUI caption = Label(key, plate.transform, 10, 0, width - 20, height, 18,
                primary ? Color.white : Ink, true);
            caption.alignment = TextAlignmentOptions.Center;
            Outline border = plate.gameObject.AddComponent<Outline>();
            border.effectColor = Line;
            border.effectDistance = new Vector2(1f, -1f);
            return button;
        }

        internal TMP_InputField Input(string name, Transform parent, float x, float y, float width, float height,
            string placeholderKey, int limit, bool multiline = false)
        {
            Image plate = Panel(name, parent, x, y, width, height, Field, true);
            Outline outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1, -1);
            TMP_InputField input = plate.gameObject.AddComponent<TMP_InputField>();
            RectTransform area = Rect("Text area", plate.transform, 12, 8, width - 24, height - 16);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI value = Text("Value", area, 0, 0, width - 24, height - 16, "", 19);
            value.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            TextMeshProUGUI placeholder = Label(placeholderKey, area, 0, 0, width - 24, height - 16, 18, Muted);
            placeholder.alignment = value.alignment;
            input.textViewport = area;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.targetGraphic = plate;
            input.characterLimit = limit;
            input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
            input.richText = false;
            input.customCaretColor = true;
            input.caretColor = Blue;
            input.selectionColor = new Color(0.35f, 0.7f, 0.9f, 0.4f);
            return input;
        }

        internal Image Icon(string name, Transform parent, float x, float y, float size, Sprite sprite)
        {
            // Normalize the supplied export margins in layout; artwork and source PNGs stay intact.
            string identity=System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(sprite));
            float scale=identity switch { "Donation"=>1.42f,"Trich"=>1.20f,"Streamly"=>1.23f,"Hub"=>1.27f,"Web"=>1.14f,"Outline"=>1.07f,_=>1f };
            float display=size*scale,offset=(display-size)*.5f;
            Image icon = Panel(name, parent, x-offset, y-offset, display, display, Color.white);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            return icon;
        }
        internal DesktopGlyphGraphic Glyph(string name,Transform parent,float x,float y,float size,DesktopGlyphGraphic.GlyphKind kind,Color color)
        {
            var glyph=Rect(name,parent,x,y,size,size).gameObject.AddComponent<DesktopGlyphGraphic>();
            Int(glyph,"glyph",(int)kind);glyph.color=color;glyph.raycastTarget=false;return glyph;
        }

        internal static void Set(Object target, string field, Object value)
        {
            SerializedObject serialized = new(target);
            SerializedProperty property = serialized.FindProperty(field);
            if (property == null) throw new System.InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void String(Object target, string field, string value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(field).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void Int(Object target, string field, int value)
        {
            SerializedObject serialized = new(target);
            serialized.FindProperty(field).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void References(Object target, string field, Object[] values)
        {
            SerializedObject serialized = new(target);
            SerializedProperty array = serialized.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
