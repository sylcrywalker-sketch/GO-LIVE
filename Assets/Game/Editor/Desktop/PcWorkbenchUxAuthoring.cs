using System;
using GoLive.HUD;
using GoLive.PcBuilding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Desktop
{
    // Changes the currently loaded scene only. The composition authoring pass decides when to save it.
    public static class PcWorkbenchUxAuthoring
    {
        private static readonly Color Panel = new(0.055f, 0.078f, 0.092f, 0.94f);
        private static readonly Color Line = new(0.22f, 0.43f, 0.46f, 0.8f);
        private static readonly Color Text = new(0.91f, 0.95f, 0.97f, 1f);
        private static readonly Color Quiet = new(0.63f, 0.72f, 0.76f, 1f);

        public static void Apply()
        {
            PcWorkbenchBehaviour workbench = Object.FindFirstObjectByType<PcWorkbenchBehaviour>(FindObjectsInactive.Include);
            PcWorkbenchHudView hud = Object.FindFirstObjectByType<PcWorkbenchHudView>(FindObjectsInactive.Include);
            PcBuildInventoryView parts = Object.FindFirstObjectByType<PcBuildInventoryView>(FindObjectsInactive.Include);
            if (workbench == null || hud == null || parts == null)
                throw new InvalidOperationException("Open the GL scene before authoring the PC workbench UI.");

            RectTransform root = (RectTransform)hud.transform;
            RectTransform status = Require(root, "StatusPanel");
            RectTransform card = Require(root, "Card");
            RectTransform inventory = (RectTransform)parts.transform;
            Canvas canvas = hud.GetComponentInParent<Canvas>().rootCanvas;
            TMP_Text statusTitle = Read<TMP_Text>(hud, "titleText");
            TMP_FontAsset font = statusTitle.font;

            Place(status, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), 348f);
            Place(inventory, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), 372f);
            Place(card, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(-410f, 40f), 390f);
            StylePanel(status, false, 18, 15);
            StylePanel(inventory, true, 14, 14);
            StylePanel(card, false, 18, 14);
            Style(statusTitle, 25, Text, true);
            Style(Read<TMP_Text>(hud, "statusText"), 20, Text);
            Style(Read<TMP_Text>(hud, "cardTitleText"), 23, Text, true);
            Style(Read<TMP_Text>(hud, "cardDetailText"), 19, Quiet);
            Style(Read<TMP_Text>(hud, "actionText"), 21, Text);

            TextMeshProUGUI power = EnsureText(status, "PowerBudget", font);
            Style(power, 21, Text, true);
            power.text = "";
            power.transform.SetAsLastSibling();
            Write(hud, "powerBudgetText", power);

            RectTransform footer = EnsureRect(root, "BuildControls");
            Place(footer, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), 900f);
            footer.sizeDelta = new Vector2(900f, 52f);
            TMP_Text oldControls = Read<TMP_Text>(hud, "controlsText");
            TMP_Text controls = EnsureText(footer, "Controls", font);
            if (oldControls != controls)
            {
                oldControls.gameObject.SetActive(false);
                Dirty(oldControls.gameObject);
            }
            Write(hud, "controlsText", controls);
            Stretch(controls.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Style(controls, 17, Quiet);
            controls.alignment = TextAlignmentOptions.Bottom;

            Style(Read<TMP_Text>(parts, "titleText"), 24, Text, true);
            Style(Read<TMP_Text>(parts, "capacityText"), 19, Quiet);
            Style(Read<TMP_Text>(parts, "handsTitleText"), 19, Text, true);
            Style(Read<TMP_Text>(parts, "handsEmptyText"), 18, Quiet);
            TMP_Text inventoryHint = Read<TMP_Text>(parts, "hintText");
            Style(inventoryHint, 18, Quiet);
            LayoutElement hintLayout = inventoryHint.GetComponent<LayoutElement>();
            if (hintLayout != null)
            {
                // The original prefab forces one line; let TMP report the height of wrapped localized text.
                hintLayout.minHeight = -1f;
                hintLayout.preferredHeight = -1f;
                hintLayout.flexibleHeight = 0f;
                Dirty(hintLayout);
            }

            RectTransform area = EnsureRect(root, "SlotCardArea");
            Stretch(area, new Vector2(0.21f, 0.09f), new Vector2(0.78f, 0.94f), Vector2.zero, Vector2.zero);
            RectTransform marker = EnsureRect(root, "SelectedSlotMarker");
            Place(marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 6f);
            marker.sizeDelta = new Vector2(6f, 6f);
            Image dot = GetOrAdd<Image>(marker.gameObject);
            dot.color = new Color(0.56f, 0.82f, 0.76f, 0.95f);
            dot.raycastTarget = false;

            PcBuildSlotMarkerView placement = GetOrAdd<PcBuildSlotMarkerView>(hud.gameObject);
            Write(placement, "workbench", workbench);
            Write(placement, "worldCamera", Read<Camera>(workbench, "playerCamera"));
            Write(placement, "canvas", canvas);
            Write(placement, "placementRoot", root);
            Write(placement, "placementArea", area);
            Write(placement, "card", card);
            Write(placement, "targetMarker", marker);

            foreach (PcComponentSlot slot in Object.FindObjectsByType<PcComponentSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject serialized = new(slot);
                serialized.FindProperty("emptyColor").colorValue = new Color(0.68f, 0.78f, 0.8f, 0.045f);
                serialized.FindProperty("compatibleColor").colorValue = new Color(0.4f, 0.78f, 0.58f, 0.085f);
                serialized.FindProperty("targetedColor").colorValue = new Color(0.45f, 0.84f, 0.65f, 0.13f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Dirty(slot);
            }

            AuthorWorldPrompts(font);
            LayoutRebuilder.ForceRebuildLayoutImmediate(status);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventory);
        }

        private static void AuthorWorldPrompts(TMP_FontAsset font)
        {
            InteractionPromptView prompts = Object.FindFirstObjectByType<InteractionPromptView>(FindObjectsInactive.Include);
            if (prompts == null) throw new InvalidOperationException("GL requires its existing interaction prompt view.");
            TMP_Text oldAction = Read<TMP_Text>(prompts, "interactionText");
            CanvasGroup existingGroup = new SerializedObject(prompts).FindProperty("worldPromptGroup").objectReferenceValue as CanvasGroup;
            RectTransform panel = existingGroup != null ? (RectTransform)existingGroup.transform : EnsureRect((RectTransform)oldAction.transform.parent, "WorldObjectPrompt");
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -48), 380f);
            StylePanel(panel, false, 18, 12);
            CanvasGroup group = GetOrAdd<CanvasGroup>(panel.gameObject);
            group.blocksRaycasts = false;
            group.interactable = false;

            TextMeshProUGUI objectName = EnsureText(panel, "ObjectName", font);
            Style(objectName, 23, Text, true);
            objectName.text = "";
            objectName.gameObject.SetActive(false);
            objectName.transform.SetAsFirstSibling();
            TMP_Text action = EnsureText(panel, "Actions", font);
            if (oldAction != action)
            {
                oldAction.gameObject.SetActive(false);
                Dirty(oldAction.gameObject);
            }
            Write(prompts, "interactionText", action);
            Style(action, 20, Text);
            action.alignment = TextAlignmentOptions.TopLeft;
            objectName.alignment = TextAlignmentOptions.TopLeft;
            Write(prompts, "objectNameText", objectName);
            Write(prompts, "worldPromptGroup", group);
            group.alpha = 0f;
            Dirty(group);
        }

        private static void StylePanel(RectTransform rect, bool blocksRaycasts, int horizontal, int vertical)
        {
            Image image = GetOrAdd<Image>(rect.gameObject);
            image.color = Panel;
            image.raycastTarget = blocksRaycasts;
            Outline border = GetOrAdd<Outline>(rect.gameObject);
            border.effectColor = Line;
            border.effectDistance = new Vector2(1f, -1f);
            VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(rect.gameObject);
            layout.padding = new RectOffset(horizontal, horizontal, vertical, vertical);
            layout.spacing = 7f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fit = GetOrAdd<ContentSizeFitter>(rect.gameObject);
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Dirty(image); Dirty(border); Dirty(layout); Dirty(fit);
        }

        private static TextMeshProUGUI EnsureText(RectTransform parent, string name, TMP_FontAsset font)
        {
            RectTransform rect = EnsureRect(parent, name);
            TextMeshProUGUI value = GetOrAdd<TextMeshProUGUI>(rect.gameObject);
            value.font = font;
            return value;
        }

        private static void Style(TMP_Text value, float size, Color color, bool bold = false)
        {
            value.fontSize = size;
            value.enableAutoSizing = false;
            value.color = color;
            value.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            value.richText = true;
            value.raycastTarget = false;
            value.textWrappingMode = TextWrappingModes.Normal;
            value.overflowMode = TextOverflowModes.Overflow;
            value.lineSpacing = 0;
            Dirty(value);
        }

        private static RectTransform Require(Transform root, string name)
        {
            RectTransform value = root.Find(name) as RectTransform;
            if (value == null) throw new InvalidOperationException($"Missing authored workbench UI: {name}.");
            return value;
        }

        private static T GetOrAdd<T>(GameObject owner) where T : Component
        {
            T component = owner.GetComponent<T>();
            if (component == null) component = owner.AddComponent<T>();
            return component;
        }

        private static RectTransform EnsureRect(RectTransform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return (RectTransform)existing;
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, float width)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            Dirty(rect);
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            Dirty(rect);
        }

        private static T Read<T>(Object owner, string field) where T : Object
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            T value = property?.objectReferenceValue as T;
            if (value == null) throw new InvalidOperationException($"Missing {owner.GetType().Name}.{field}.");
            return value;
        }

        private static void Write(Object owner, string field, Object value)
        {
            SerializedObject serialized = new(owner);
            SerializedProperty property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"Missing {owner.GetType().Name}.{field}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Dirty(owner);
        }

        private static void Dirty(Object target)
        {
            EditorUtility.SetDirty(target);
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }
}
