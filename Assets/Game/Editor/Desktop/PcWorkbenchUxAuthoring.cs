using System;
using GoLive.HUD;
using GoLive.Inventory;
using GoLive.PcBuilding;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Editor.Desktop
{
    // Authors the loaded scene and its existing ghost material. The composition pass decides when to save them.
    public static class PcWorkbenchUxAuthoring
    {
        private static readonly Color Panel = new(0.055f, 0.067f, 0.074f, 0.95f);
        private static readonly Color Line = new(0.39f, 0.43f, 0.45f, 0.72f);
        private static readonly Color Text = new(0.91f, 0.93f, 0.94f, 1f);
        private static readonly Color Quiet = new(0.67f, 0.70f, 0.72f, 1f);

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

            Place(status, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -32f), 340f);
            Place(inventory, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), 326f);
            Place(card, Vector2.zero, Vector2.zero, new Vector2(42f, 154f), 300f);
            StylePanel(status, false, 18, 15);
            StylePanel(inventory, true, 14, 14);
            StylePanel(card, false, 16, 12);
            Style(statusTitle, 22, Text, true);
            Style(Read<TMP_Text>(hud, "cardTitleText"), 19, Text, true);
            Style(Read<TMP_Text>(hud, "cardDetailText"), 17, Quiet);
            Style(Read<TMP_Text>(hud, "actionText"), 17, Text);
            AuthorHardwareStatus(status, hud, statusTitle, font);

            TextMeshProUGUI power = EnsureText(status, "PowerBudget", font);
            Style(power, 14, Quiet);
            power.text = "";
            power.transform.SetAsLastSibling();
            Write(hud, "powerBudgetText", power);

            RectTransform footer = EnsureRect(root, "BuildControls");
            Place(footer, Vector2.zero, Vector2.zero, new Vector2(42f, 34f), 330f);
            footer.sizeDelta = new Vector2(330f, 96f);
            TMP_Text oldControls = Read<TMP_Text>(hud, "controlsText");
            TMP_Text controls = EnsureText(footer, "Controls", font);
            if (oldControls != controls)
            {
                oldControls.gameObject.SetActive(false);
                Dirty(oldControls.gameObject);
            }
            Write(hud, "controlsText", controls);
            Stretch(controls.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Style(controls, 17, Text);
            controls.lineSpacing = 12f;
            controls.alignment = TextAlignmentOptions.BottomLeft;
            Shadow controlShadow = GetOrAdd<Shadow>(controls.gameObject);
            controlShadow.effectColor = new Color(0, 0, 0, .8f);
            controlShadow.effectDistance = new Vector2(1f, -1f);
            Dirty(controlShadow);

            Style(Read<TMP_Text>(parts, "titleText"), 21, Text, true);
            Style(Read<TMP_Text>(parts, "capacityText"), 15, Quiet);
            Style(Read<TMP_Text>(parts, "handsTitleText"), 16, Text, true);
            Style(Read<TMP_Text>(parts, "handsEmptyText"), 16, Quiet);
            TMP_Text inventoryHint = Read<TMP_Text>(parts, "hintText");
            Style(inventoryHint, 14, Quiet);
            AuthorInventoryRows(parts);
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
            Stretch(area, new Vector2(0.20f, 0.14f), new Vector2(0.81f, 0.94f), Vector2.zero, Vector2.zero);
            RectTransform marker = EnsureRect(root, "SelectedSlotMarker");
            Place(marker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 6f);
            marker.sizeDelta = new Vector2(6f, 6f);
            Image dot = GetOrAdd<Image>(marker.gameObject);
            dot.color = new Color(0.82f, 0.86f, 0.83f, 0.95f);
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
                serialized.FindProperty("emptyColor").colorValue = new Color(0.65f, 0.69f, 0.70f, 0.022f);
                serialized.FindProperty("compatibleColor").colorValue = new Color(0.59f, 0.69f, 0.62f, 0.035f);
                serialized.FindProperty("targetedColor").colorValue = new Color(0.65f, 0.73f, 0.67f, 0.05f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Dirty(slot);
            }

            // A part contains overlapping submeshes: a bright 30% tint accumulated into an almost opaque slab.
            // Keep the same authored material and meshes, with a neutral tint that preserves the case behind them.
            Material ghost = Read<Material>(workbench, "ghostMaterial");
            Color ghostTint = new(.52f, .58f, .55f, .10f);
            if (ghost.HasProperty("_BaseColor")) ghost.SetColor("_BaseColor", ghostTint);
            if (ghost.HasProperty("_Color")) ghost.SetColor("_Color", ghostTint);
            Dirty(ghost);

            AuthorWorldPrompts(font);
            LayoutRebuilder.ForceRebuildLayoutImmediate(status);
            LayoutRebuilder.ForceRebuildLayoutImmediate(card);
            LayoutRebuilder.ForceRebuildLayoutImmediate(inventory);
        }

        private static void AuthorHardwareStatus(RectTransform status, PcWorkbenchHudView hud, TMP_Text title, TMP_FontAsset font)
        {
            // The existing text API remains usable by older scene/prefab consumers. The authored scene displays
            // the same diagnostics through explicit rows, so component icons and verdict order can be composed cleanly.
            TMP_Text legacy = Read<TMP_Text>(hud, "statusText");
            legacy.gameObject.SetActive(false);
            Dirty(legacy.gameObject);
            title.transform.SetAsFirstSibling();
            Separator(status, "Divider").SetSiblingIndex(1);

            RectTransform hardware = EnsureRect(status, "HardwareRows");
            hardware.SetSiblingIndex(2);
            FixedHeight(hardware, 204f);
            PcHardwareStatusView view = GetOrAdd<PcHardwareStatusView>(status.gameObject);
            SerializedObject serialized = new(view);
            SerializedProperty rows = serialized.FindProperty("rows");
            rows.arraySize = 6;
            PcComponentType[] types = { PcComponentType.Motherboard, PcComponentType.Cpu, PcComponentType.Ram,
                PcComponentType.Psu, PcComponentType.Storage, PcComponentType.Gpu };
            PcHardwareGlyph.Shape[] shapes = { PcHardwareGlyph.Shape.Motherboard, PcHardwareGlyph.Shape.Cpu,
                PcHardwareGlyph.Shape.Ram, PcHardwareGlyph.Shape.Psu, PcHardwareGlyph.Shape.Storage, PcHardwareGlyph.Shape.Gpu };

            for (int i = 0; i < types.Length; i++)
            {
                RectTransform row = EnsureRect(hardware, types[i].ToString());
                Stretch(row, new Vector2(0, 1), Vector2.one, new Vector2(0, -34f * (i + 1)), new Vector2(0, -34f * i));
                RectTransform icon = EnsureRect(row, "HardwareIcon");
                Place(icon, new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, 27f);
                icon.sizeDelta = new Vector2(27f, 27f);
                GetOrAdd<CanvasRenderer>(icon.gameObject);
                PcHardwareGlyph hardwareGlyph = GetOrAdd<PcHardwareGlyph>(icon.gameObject);
                hardwareGlyph.raycastTarget = false;
                hardwareGlyph.Show(shapes[i], new Color(.78f, .82f, .83f, 1));
                Dirty(hardwareGlyph);

                TextMeshProUGUI name = EnsureText(row, "Name", font);
                Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(39f, 0), new Vector2(-34f, 0));
                Style(name, 18, Text);
                name.alignment = TextAlignmentOptions.MidlineLeft;
                name.textWrappingMode = TextWrappingModes.NoWrap;

                RectTransform marker = EnsureRect(row, "StatusMarker");
                Place(marker, new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, 24f);
                marker.sizeDelta = new Vector2(24f, 24f);
                GetOrAdd<CanvasRenderer>(marker.gameObject);
                PcHardwareGlyph mark = GetOrAdd<PcHardwareGlyph>(marker.gameObject);
                mark.raycastTarget = false;
                mark.Show(PcHardwareGlyph.Shape.Check, new Color(.46f, .88f, .43f, 1));
                Dirty(mark);

                SerializedProperty target = rows.GetArrayElementAtIndex(i);
                target.FindPropertyRelative("component").intValue = (int)types[i];
                target.FindPropertyRelative("name").objectReferenceValue = name;
                target.FindPropertyRelative("marker").objectReferenceValue = mark;
            }

            Separator(status, "HardwareStatusRule").SetSiblingIndex(3);
            TextMeshProUGUI verdict = EnsureText(status, "HardwareVerdict", font);
            Style(verdict, 20, Text, true);
            verdict.transform.SetSiblingIndex(4);
            TextMeshProUGUI explanation = EnsureText(status, "HardwareExplanation", font);
            Style(explanation, 16, Quiet);
            explanation.transform.SetSiblingIndex(5);
            serialized.FindProperty("verdictText").objectReferenceValue = verdict;
            serialized.FindProperty("explanationText").objectReferenceValue = explanation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Dirty(view);
            Write(hud, "hardwareStatus", view);
        }

        private static void AuthorInventoryRows(PcBuildInventoryView parts)
        {
            InventoryListView list = Read<InventoryListView>(parts, "list");
            SerializedObject data = new(list);
            data.FindProperty("maxViewportHeight").floatValue = 304f;
            SerializedProperty rows = data.FindProperty("rows");
            for (int i = 0; i < rows.arraySize; i++)
                StyleInventoryRow((InventoryItemView)rows.GetArrayElementAtIndex(i).objectReferenceValue);
            data.ApplyModifiedPropertiesWithoutUndo();
            Dirty(list);
            StyleInventoryRow(Read<InventoryItemView>(parts, "heldItem"));
            Style(Read<TMP_Text>(list, "emptyTitle"), 17, Text);
            Style(Read<TMP_Text>(list, "emptyHint"), 14, Quiet);
        }

        private static void StyleInventoryRow(InventoryItemView row)
        {
            // The original 62 px row reserved only 25 px for the name. RU names and fit notes each need two lines.
            // Give both texts actual space instead of allowing their glyphs to escape their rectangles.
            FixedHeight((RectTransform)row.transform, 96f);
            TMP_Text title = Read<TMP_Text>(row, "title");
            TMP_Text detail = Read<TMP_Text>(row, "detail");
            Style(title, 17, Text, true);
            Style(detail, 14, Quiet);
            Stretch(title.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(62f, -50f), new Vector2(-20f, -8f));
            Stretch(detail.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(62f, -88f), new Vector2(-20f, -54f));
            title.alignment = detail.alignment = TextAlignmentOptions.TopLeft;
            Dirty(title);
            Dirty(detail);
            SerializedObject data = new(row);
            data.FindProperty("normalColor").colorValue = new Color(1, 1, 1, .045f);
            data.FindProperty("hoverColor").colorValue = new Color(1, 1, 1, .105f);
            data.ApplyModifiedPropertiesWithoutUndo();
            Dirty(row);
        }

        private static RectTransform Separator(RectTransform parent, string name)
        {
            RectTransform rect = EnsureRect(parent, name);
            Image image = GetOrAdd<Image>(rect.gameObject);
            image.color = Line;
            image.raycastTarget = false;
            FixedHeight(rect, 1f);
            Dirty(image);
            return rect;
        }

        private static void FixedHeight(RectTransform rect, float height)
        {
            LayoutElement layout = GetOrAdd<LayoutElement>(rect.gameObject);
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = 0;
            Dirty(layout);
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
