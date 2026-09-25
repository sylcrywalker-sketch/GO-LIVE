using GoLive.Localization;
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
        internal readonly Color Ink = new(0.10f, 0.17f, 0.23f);
        internal readonly Color Muted = new(0.36f, 0.44f, 0.49f);
        internal readonly Color Blue = new(0.14f, 0.38f, 0.52f);
        internal readonly Color Paper = new(0.96f, 0.97f, 0.97f);
        internal readonly Color Line = new(0.79f, 0.84f, 0.86f);

        internal DesktopUiAuthoring(TMP_FontAsset font, LocalizationContext localization)
        {
            Font = font;
            Localization = localization;
        }

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
            Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = hit;
            return image;
        }

        internal TextMeshProUGUI Text(string name, Transform parent, float x, float y, float width, float height,
            string value, float size = 22f, Color? color = null, bool bold = false)
        {
            TextMeshProUGUI text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.fontSize = size;
            text.color = color ?? Ink;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
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
            Image plate = Panel(key, parent, x, y, width, height, primary ? Blue : Color.white, true);
            Button button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.86f, 0.94f, 0.97f);
            colors.pressedColor = new Color(0.72f, 0.85f, 0.91f);
            colors.selectedColor = new Color(0.84f, 0.93f, 0.97f);
            colors.disabledColor = new Color(0.62f, 0.67f, 0.70f);
            button.colors = colors;
            TextMeshProUGUI caption = Label(key, plate.transform, 12, 0, width - 24, height, 20,
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
            Image plate = Panel(name, parent, x, y, width, height, Color.white, true);
            Outline outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = Line;
            outline.effectDistance = new Vector2(1, -1);
            TMP_InputField input = plate.gameObject.AddComponent<TMP_InputField>();
            RectTransform area = Rect("Text area", plate.transform, 12, 8, width - 24, height - 16);
            area.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI value = Text("Value", area, 0, 0, width - 24, height - 16, "", 22);
            value.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
            TextMeshProUGUI placeholder = Label(placeholderKey, area, 0, 0, width - 24, height - 16, 21, Muted);
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
            Image icon = Panel(name, parent, x, y, size, size, Color.white);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            return icon;
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
