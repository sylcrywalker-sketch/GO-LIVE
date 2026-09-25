#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using GoLive.Phone;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Editor.Phone
{
    public static class PhoneShopProductCardBuilder
    {
        private const string MenuPath =
            "GO LIVE/Phone Shop/Rebuild Selected Product Card";

        private static readonly Color CardColor =
            new(0.125f, 0.137f, 0.153f, 0.96f);

        private static readonly Color CardHoverColor =
            new(0.155f, 0.169f, 0.188f, 0.98f);

        private static readonly Color CardPressedColor =
            new(0.105f, 0.116f, 0.129f, 0.98f);

        private static readonly Color ThumbColor =
            new(0.165f, 0.176f, 0.192f, 1f);

        private static readonly Color PrimaryText =
            new(0.957f, 0.957f, 0.949f, 1f);

        private static readonly Color SecondaryText =
            new(0.643f, 0.659f, 0.682f, 1f);

        private static readonly Color PriceColor =
            new(0.557f, 0.773f, 1f, 1f);

        private static readonly Color QuickBuyColor =
            new(0.263f, 0.553f, 0.847f, 1f);

        private static readonly Color QuickBuyHoverColor =
            new(0.31f, 0.61f, 0.92f, 1f);

        private static readonly Color QuickBuyPressedColor =
            new(0.20f, 0.46f, 0.73f, 1f);

        [MenuItem(MenuPath, true)]
        private static bool ValidateRebuildSelected()
        {
            return
                !EditorApplication.isPlaying &&
                Selection.activeGameObject != null &&
                Selection.activeGameObject.name == "ProductCardTemplate";
        }

        [MenuItem(MenuPath)]
        private static void RebuildSelected()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected == null ||
                selected.name != "ProductCardTemplate")
            {
                Debug.LogError(
                    "Select ProductCardTemplate in the Hierarchy first.");

                return;
            }

            if (TryRebuildPrefabSource(selected))
                return;

            Undo.RegisterFullObjectHierarchyUndo(
                selected,
                "Rebuild Phone Shop Product Card");

            ConfigureCard(selected);

            EditorUtility.SetDirty(selected);
            EditorSceneManager.MarkSceneDirty(selected.scene);

            Debug.Log(
                "Phone Shop ProductCardTemplate rebuilt in the current scene. " +
                "Save the scene after visually checking it.");
        }

        private static bool TryRebuildPrefabSource(GameObject selected)
        {
            GameObject instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(selected);

            if (instanceRoot == null)
                return false;

            GameObject sourceRoot =
                PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);

            if (sourceRoot == null)
                return false;

            string prefabPath =
                AssetDatabase.GetAssetPath(sourceRoot);

            if (string.IsNullOrWhiteSpace(prefabPath))
                return false;

            string relativePath =
                GetRelativePath(
                    instanceRoot.transform,
                    selected.transform);

            if (relativePath == null)
                return false;

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                Transform target =
                    string.IsNullOrEmpty(relativePath)
                        ? prefabRoot.transform
                        : prefabRoot.transform.Find(relativePath);

                if (target == null)
                {
                    Debug.LogError(
                        $"Could not find '{relativePath}' inside prefab '{prefabPath}'.");

                    return true;
                }

                ConfigureCard(target.gameObject);

                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath);

                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"Phone Shop ProductCardTemplate rebuilt in prefab:\n{prefabPath}\n" +
                    "The scene instance should update automatically.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            return true;
        }

        private static void ConfigureCard(GameObject card)
        {
            RectTransform root =
                RequireRectTransform(card);

            root.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                150f);

            LayoutElement rootLayout =
                GetOrAdd<LayoutElement>(card);

            rootLayout.preferredHeight = 150f;
            rootLayout.flexibleHeight = 0f;

            Image rootImage =
                GetOrAdd<Image>(card);

            rootImage.color = CardColor;
            rootImage.raycastTarget = true;

            Button rootButton =
                GetOrAdd<Button>(card);

            rootButton.targetGraphic = rootImage;
            rootButton.transition =
                Selectable.Transition.ColorTint;

            ColorBlock rootColors =
                rootButton.colors;

            rootColors.normalColor =
                CardColor;

            rootColors.highlightedColor =
                CardHoverColor;

            rootColors.pressedColor =
                CardPressedColor;

            rootColors.selectedColor =
                CardHoverColor;

            rootColors.disabledColor =
                new Color(
                    CardColor.r,
                    CardColor.g,
                    CardColor.b,
                    0.55f);

            rootColors.colorMultiplier = 1f;
            rootColors.fadeDuration = 0.08f;

            rootButton.colors =
                rootColors;

            rootButton.navigation =
                new Navigation
                {
                    mode = Navigation.Mode.None
                };

            CanvasGroup canvasGroup =
                GetOrAdd<CanvasGroup>(card);

            HorizontalLayoutGroup rootLayoutGroup =
                GetOrAdd<HorizontalLayoutGroup>(card);

            rootLayoutGroup.padding =
                new RectOffset(
                    12,
                    12,
                    12,
                    12);

            rootLayoutGroup.spacing = 12f;

            rootLayoutGroup.childAlignment =
                TextAnchor.MiddleLeft;

            rootLayoutGroup.childControlWidth = true;
            rootLayoutGroup.childControlHeight = true;

            rootLayoutGroup.childForceExpandWidth = false;
            rootLayoutGroup.childForceExpandHeight = false;

            RectTransform thumb =
                RequireChild(
                    card.transform,
                    "Thumb");

            ConfigureThumb(thumb);

            RectTransform info =
                RequireChild(
                    card.transform,
                    "Info");

            ConfigureInfo(info);

            TMP_Text productName =
                RequireText(
                    info,
                    "ProductName");

            ConfigureProductName(productName);

            TMP_Text category =
                FindText(
                    info,
                    "Category");

            if (category == null)
            {
                TMP_Text oldDescription =
                    FindTextDeep(
                        card.transform,
                        "Description");

                if (oldDescription != null)
                {
                    oldDescription.name =
                        "Category";

                    oldDescription.transform.SetParent(
                        info,
                        false);

                    category =
                        oldDescription;
                }
                else
                {
                    category =
                        CreateText(
                            info,
                            "Category",
                            "Электроника");
                }
            }
            else if (category.transform.parent != info)
            {
                category.transform.SetParent(
                    info,
                    false);
            }

            ConfigureCategory(category);

            TMP_Text price =
                FindTextDeep(
                    card.transform,
                    "Price");

            if (price == null)
            {
                price =
                    CreateText(
                        info,
                        "Price",
                        "$15.00");
            }
            else if (price.transform.parent != info)
            {
                price.transform.SetParent(
                    info,
                    false);
            }

            ConfigurePrice(price);

            Transform priceRow =
                FindDeep(
                    card.transform,
                    "PriceRow");

            if (priceRow != null &&
                priceRow.childCount == 0)
            {
                UnityEngine.Object.DestroyImmediate(
                    priceRow.gameObject);
            }

            TMP_Text status =
                FindTextDeep(
                    card.transform,
                    "Status");

            if (status == null)
            {
                status =
                    CreateText(
                        info,
                        "Status",
                        "Недоступно");
            }
            else if (status.transform.parent != info)
            {
                status.transform.SetParent(
                    info,
                    false);
            }

            ConfigureStatus(status);
            status.gameObject.SetActive(false);

            GameObject badge =
                FindDeep(
                    card.transform,
                    "Badge")
                ?.gameObject;

            TMP_Text badgeLabel;

            if (badge == null)
            {
                badge =
                    CreateBadge(
                        thumb,
                        out badgeLabel);
            }
            else
            {
                badge.transform.SetParent(
                    thumb,
                    false);

                badgeLabel =
                    FindTextDeep(
                        badge.transform,
                        "Label");

                if (badgeLabel == null)
                {
                    badgeLabel =
                        CreateText(
                            RequireRectTransform(badge),
                            "Label",
                            "Заказано");
                }

                ConfigureBadge(
                    badge,
                    badgeLabel);
            }

            badge.SetActive(false);

            RectTransform actions =
                EnsureActions(
                    card.transform);

            TMP_Text chevron =
                FindTextDeep(
                    card.transform,
                    "Chevron");

            if (chevron == null)
            {
                chevron =
                    CreateText(
                        actions,
                        "Chevron",
                        "›");
            }
            else
            {
                chevron.transform.SetParent(
                    actions,
                    false);
            }

            ConfigureChevron(chevron);

            RectTransform quickBuy =
                EnsureQuickBuyButton(
                    actions,
                    rootImage.sprite);

            thumb.SetSiblingIndex(0);
            info.SetSiblingIndex(1);
            actions.SetSiblingIndex(2);

            productName.transform.SetSiblingIndex(0);
            category.transform.SetSiblingIndex(1);
            price.transform.SetSiblingIndex(2);
            status.transform.SetSiblingIndex(3);

            Image productImage =
                RequireImage(
                    thumb,
                    "ProductImage");

            ConfigureProductImage(productImage);

            ShopProductCardView cardView =
                card.GetComponent<ShopProductCardView>();

            if (cardView == null)
            {
                Debug.LogError(
                    $"{card.name} has no ShopProductCardView.");

                return;
            }

            BindExistingRuntimeFields(
                cardView,
                rootButton,
                canvasGroup,
                thumb.gameObject,
                productImage,
                productName,
                price,
                status,
                category,
                badge,
                badgeLabel);

            EditorUtility.SetDirty(cardView);

            TMP_FontAsset semibold =
                FindFontAsset(
                    "manrope",
                    "semibold");

            if (semibold == null)
            {
                Debug.LogWarning(
                    "Manrope SemiBold TMP Font Asset was not found. " +
                    "The builder kept/fell back to the existing phone font.");
            }

            Button quickBuyButton =
                quickBuy.GetComponent<Button>();

            if (quickBuyButton != null)
            {
                Debug.Log(
                    "QuickBuyButton was created visually. " +
                    "Current ShopProductCardView does not own a quick-buy action yet; " +
                    "do not wire it through Inspector manually. " +
                    "We will add that cleanly in code next.");
            }
        }

        private static void ConfigureThumb(
            RectTransform thumb)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    thumb.gameObject);

            layout.minWidth = 104f;
            layout.minHeight = 104f;

            layout.preferredWidth = 104f;
            layout.preferredHeight = 104f;

            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image image =
                GetOrAdd<Image>(
                    thumb.gameObject);

            image.color = ThumbColor;
            image.raycastTarget = false;
        }

        private static void ConfigureProductImage(
            Image image)
        {
            RectTransform rect =
                (RectTransform)image.transform;

            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.offsetMin =
                new Vector2(
                    8f,
                    8f);

            rect.offsetMax =
                new Vector2(
                    -8f,
                    -8f);

            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void ConfigureInfo(
            RectTransform info)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    info.gameObject);

            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 0f;

            VerticalLayoutGroup group =
                GetOrAdd<VerticalLayoutGroup>(
                    info.gameObject);

            group.padding =
                new RectOffset(
                    0,
                    0,
                    0,
                    0);

            group.spacing = 2f;

            group.childAlignment =
                TextAnchor.MiddleLeft;

            group.childControlWidth = true;
            group.childControlHeight = true;

            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
        }

        private static void ConfigureProductName(
            TMP_Text text)
        {
            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "semibold");

            ApplyFontIfAvailable(
                text,
                font);

            text.text =
                "Бюджетная видеокарта";

            text.fontSize = 21f;
            text.fontStyle =
                FontStyles.Normal;

            text.color =
                PrimaryText;

            text.alignment =
                TextAlignmentOptions.MidlineLeft;

            text.textWrappingMode =
                TextWrappingModes.Normal;

            text.overflowMode =
                TextOverflowModes.Ellipsis;

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    text.gameObject);

            layout.preferredHeight = 48f;
            layout.flexibleHeight = 0f;
        }

        private static void ConfigureCategory(
            TMP_Text text)
        {
            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "regular");

            ApplyFontIfAvailable(
                text,
                font);

            text.text =
                "Электроника";

            text.fontSize = 14f;
            text.fontStyle =
                FontStyles.Normal;

            text.color =
                SecondaryText;

            text.alignment =
                TextAlignmentOptions.MidlineLeft;

            text.textWrappingMode =
                TextWrappingModes.NoWrap;

            text.overflowMode =
                TextOverflowModes.Ellipsis;

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    text.gameObject);

            layout.minHeight = 22f;
            layout.preferredHeight = 22f;
            layout.flexibleHeight = 0f;
        }

        private static void ConfigurePrice(
            TMP_Text text)
        {
            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "semibold");

            ApplyFontIfAvailable(
                text,
                font);

            text.text =
                "$15.00";

            text.fontSize = 22f;
            text.fontStyle =
                FontStyles.Normal;

            text.color =
                PriceColor;

            text.alignment =
                TextAlignmentOptions.MidlineLeft;

            text.textWrappingMode =
                TextWrappingModes.NoWrap;

            text.overflowMode =
                TextOverflowModes.Ellipsis;

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    text.gameObject);

            layout.preferredHeight = 30f;
            layout.flexibleHeight = 0f;
        }

        private static void ConfigureStatus(
            TMP_Text text)
        {
            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "medium");

            ApplyFontIfAvailable(
                text,
                font);

            text.fontSize = 14f;
            text.fontStyle =
                FontStyles.Normal;

            text.color =
                SecondaryText;

            text.alignment =
                TextAlignmentOptions.MidlineLeft;

            text.textWrappingMode =
                TextWrappingModes.NoWrap;

            text.overflowMode =
                TextOverflowModes.Ellipsis;

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    text.gameObject);

            layout.preferredHeight = 22f;
            layout.flexibleHeight = 0f;
        }

        private static RectTransform EnsureActions(
            Transform card)
        {
            RectTransform actions =
                FindRect(
                    card,
                    "Actions");

            if (actions == null)
            {
                GameObject objectValue =
                    new(
                        "Actions",
                        typeof(RectTransform));

                objectValue.layer =
                    card.gameObject.layer;

                actions =
                    objectValue.GetComponent<RectTransform>();

                actions.SetParent(
                    card,
                    false);
            }

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    actions.gameObject);

            layout.preferredWidth = 44f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            VerticalLayoutGroup group =
                GetOrAdd<VerticalLayoutGroup>(
                    actions.gameObject);

            group.padding =
                new RectOffset(
                    0,
                    0,
                    0,
                    0);

            group.spacing = 8f;

            group.childAlignment =
                TextAnchor.MiddleCenter;

            group.childControlWidth = true;
            group.childControlHeight = true;

            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            return actions;
        }

        private static void ConfigureChevron(
            TMP_Text chevron)
        {
            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "regular");

            ApplyFontIfAvailable(
                chevron,
                font);

            chevron.text = "›";
            chevron.fontSize = 26f;

            chevron.fontStyle =
                FontStyles.Normal;

            chevron.color =
                SecondaryText;

            chevron.alignment =
                TextAlignmentOptions.Center;

            chevron.textWrappingMode =
                TextWrappingModes.NoWrap;

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    chevron.gameObject);

            layout.preferredWidth = 20f;
            layout.preferredHeight = 32f;

            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            chevron.raycastTarget = false;
        }

        private static RectTransform EnsureQuickBuyButton(
            RectTransform actions,
            Sprite roundedSprite)
        {
            RectTransform buttonRect =
                FindRect(
                    actions,
                    "QuickBuyButton");

            if (buttonRect == null)
            {
                GameObject buttonObject =
                    new(
                        "QuickBuyButton",
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image),
                        typeof(Button),
                        typeof(LayoutElement));

                buttonObject.layer =
                    actions.gameObject.layer;

                buttonRect =
                    buttonObject.GetComponent<RectTransform>();

                buttonRect.SetParent(
                    actions,
                    false);
            }

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    buttonRect.gameObject);

            layout.preferredWidth = 44f;
            layout.preferredHeight = 44f;

            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image image =
                GetOrAdd<Image>(
                    buttonRect.gameObject);

            if (image.sprite == null)
                image.sprite = roundedSprite;

            image.type =
                image.sprite != null
                    ? Image.Type.Sliced
                    : Image.Type.Simple;

            image.color =
                QuickBuyColor;

            image.raycastTarget = true;

            Button button =
                GetOrAdd<Button>(
                    buttonRect.gameObject);

            button.targetGraphic =
                image;

            button.transition =
                Selectable.Transition.ColorTint;

            ColorBlock colors =
                button.colors;

            colors.normalColor =
                QuickBuyColor;

            colors.highlightedColor =
                QuickBuyHoverColor;

            colors.pressedColor =
                QuickBuyPressedColor;

            colors.selectedColor =
                QuickBuyHoverColor;

            colors.disabledColor =
                new Color(
                    QuickBuyColor.r,
                    QuickBuyColor.g,
                    QuickBuyColor.b,
                    0.4f);

            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;

            button.colors =
                colors;

            button.navigation =
                new Navigation
                    {
                        mode =
                            Navigation.Mode.None
                    };

            EnsureQuickBuyIcon(
                buttonRect);

            return buttonRect;
        }

        private static void EnsureQuickBuyIcon(
            RectTransform button)
        {
            Transform existing =
                button.Find("Icon");

            if (existing != null)
            {
                Image existingImage =
                    existing.GetComponent<Image>();

                if (existingImage != null)
                {
                    RectTransform rect =
                        (RectTransform)existing;

                    rect.anchorMin =
                        Vector2.zero;

                    rect.anchorMax =
                        Vector2.one;

                    rect.offsetMin =
                        new Vector2(
                            10f,
                            10f);

                    rect.offsetMax =
                        new Vector2(
                            -10f,
                            -10f);

                    existingImage.preserveAspect = true;
                    existingImage.raycastTarget = false;

                    return;
                }
            }

            GameObject iconObject =
                existing != null
                    ? existing.gameObject
                    : new GameObject(
                        "Icon",
                        typeof(RectTransform));

            iconObject.layer =
                button.gameObject.layer;

            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();

            iconRect.SetParent(
                button,
                false);

            iconRect.anchorMin =
                Vector2.zero;

            iconRect.anchorMax =
                Vector2.one;

            iconRect.offsetMin =
                new Vector2(
                    6f,
                    6f);

            iconRect.offsetMax =
                new Vector2(
                    -6f,
                    -6f);

            TMP_Text icon =
                iconObject.GetComponent<TMP_Text>();

            if (icon == null)
            {
                icon =
                    iconObject.AddComponent<TextMeshProUGUI>();
            }

            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "medium");

            ApplyFontIfAvailable(
                icon,
                font);

            icon.text = "+";
            icon.fontSize = 26f;

            icon.fontStyle =
                FontStyles.Normal;

            icon.color =
                Color.white;

            icon.alignment =
                TextAlignmentOptions.Center;

            icon.textWrappingMode =
                TextWrappingModes.NoWrap;

            icon.raycastTarget = false;
        }

        private static GameObject CreateBadge(
            RectTransform thumb,
            out TMP_Text label)
        {
            GameObject badge =
                new(
                    "Badge",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));

            badge.layer =
                thumb.gameObject.layer;

            RectTransform rect =
                badge.GetComponent<RectTransform>();

            rect.SetParent(
                thumb,
                false);

            label =
                CreateText(
                    rect,
                    "Label",
                    "Заказано");

            ConfigureBadge(
                badge,
                label);

            return badge;
        }

        private static void ConfigureBadge(
            GameObject badge,
            TMP_Text label)
        {
            RectTransform rect =
                RequireRectTransform(badge);

            rect.anchorMin =
                new Vector2(
                    0f,
                    1f);

            rect.anchorMax =
                new Vector2(
                    0f,
                    1f);

            rect.pivot =
                new Vector2(
                    0f,
                    1f);

            rect.anchoredPosition =
                new Vector2(
                    6f,
                    -6f);

            rect.sizeDelta =
                new Vector2(
                    62f,
                    22f);

            Image image =
                GetOrAdd<Image>(badge);

            image.color =
                new Color(
                    0.18f,
                    0.24f,
                    0.30f,
                    0.96f);

            image.raycastTarget = false;

            RectTransform labelRect =
                (RectTransform)label.transform;

            labelRect.anchorMin =
                Vector2.zero;

            labelRect.anchorMax =
                Vector2.one;

            labelRect.offsetMin =
                new Vector2(
                    4f,
                    2f);

            labelRect.offsetMax =
                new Vector2(
                    -4f,
                    -2f);

            TMP_FontAsset font =
                FindFontAsset(
                    "manrope",
                    "medium");

            ApplyFontIfAvailable(
                label,
                font);

            label.fontSize = 11f;

            label.color =
                PrimaryText;

            label.alignment =
                TextAlignmentOptions.Center;

            label.textWrappingMode =
                TextWrappingModes.NoWrap;

            label.raycastTarget = false;
        }

        private static void BindExistingRuntimeFields(
            ShopProductCardView view,
            Button button,
            CanvasGroup canvasGroup,
            GameObject thumbnail,
            Image productImage,
            TMP_Text productName,
            TMP_Text price,
            TMP_Text status,
            TMP_Text description,
            GameObject badge,
            TMP_Text badgeLabel)
        {
            SerializedObject serialized =
                new(view);

            SetReference(
                serialized,
                "_button",
                button);

            SetReference(
                serialized,
                "_canvasGroup",
                canvasGroup);

            SetReference(
                serialized,
                "_thumbnail",
                thumbnail);

            SetReference(
                serialized,
                "_image",
                productImage);

            SetReference(
                serialized,
                "_name",
                productName);

            SetReference(
                serialized,
                "_price",
                price);

            SetReference(
                serialized,
                "_status",
                status);

            // Runtime field is still called "_description".
            // For now it points at the new Category text.
            // We will rename the runtime semantic cleanly in the next code step.
            SetReference(
                serialized,
                "_description",
                description);

            SetReference(
                serialized,
                "_badge",
                badge);

            SetReference(
                serialized,
                "_badgeLabel",
                badgeLabel);

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property =
                serialized.FindProperty(
                    propertyName);

            if (property == null)
            {
                Debug.LogWarning(
                    $"Serialized field '{propertyName}' was not found on {serialized.targetObject.name}.");

                return;
            }

            property.objectReferenceValue =
                value;
        }

        private static TMP_FontAsset FindFontAsset(
            params string[] requiredTokens)
        {
            string[] guids =
                AssetDatabase.FindAssets(
                    "t:TMP_FontAsset");

            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guid);

                string fileName =
                    System.IO.Path
                        .GetFileNameWithoutExtension(path)
                        .ToLowerInvariant();

                bool matches =
                    true;

                foreach (string token in requiredTokens)
                {
                    if (fileName.Contains(
                            token.ToLowerInvariant()))
                    {
                        continue;
                    }

                    matches = false;
                    break;
                }

                if (!matches)
                    continue;

                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    path);
            }

            // Existing phone-font fallback.
            foreach (string guid in guids)
            {
                string path =
                    AssetDatabase.GUIDToAssetPath(
                        guid);

                if (!path
                    .ToLowerInvariant()
                    .Contains("phonesans"))
                {
                    continue;
                }

                return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    path);
            }

            return null;
        }

        private static void ApplyFontIfAvailable(
            TMP_Text text,
            TMP_FontAsset font)
        {
            if (font != null)
                text.font = font;
        }

        private static TMP_Text CreateText(
            RectTransform parent,
            string objectName,
            string initialText)
        {
            GameObject textObject =
                new(
                    objectName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            textObject.layer =
                parent.gameObject.layer;

            RectTransform rect =
                textObject.GetComponent<RectTransform>();

            rect.SetParent(
                parent,
                false);

            TMP_Text text =
                textObject.GetComponent<TMP_Text>();

            text.text =
                initialText;

            return text;
        }

        private static RectTransform RequireChild(
            Transform parent,
            string childName)
        {
            Transform child =
                parent.Find(
                    childName);

            if (child == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}' has no child '{childName}'.");
            }

            return RequireRectTransform(
                child.gameObject);
        }

        private static Image RequireImage(
            Transform parent,
            string childName)
        {
            Transform child =
                parent.Find(
                    childName);

            if (child == null)
            {
                throw new InvalidOperationException(
                    $"'{parent.name}' has no child '{childName}'.");
            }

            Image image =
                child.GetComponent<Image>();

            if (image == null)
            {
                throw new InvalidOperationException(
                    $"'{childName}' has no Image component.");
            }

            return image;
        }

        private static TMP_Text RequireText(
            Transform parent,
            string childName)
        {
            TMP_Text text =
                FindText(
                    parent,
                    childName);

            if (text != null)
                return text;

            throw new InvalidOperationException(
                $"'{parent.name}' has no TMP text child '{childName}'.");
        }

        private static TMP_Text FindText(
            Transform parent,
            string childName)
        {
            Transform child =
                parent.Find(
                    childName);

            return child != null
                ? child.GetComponent<TMP_Text>()
                : null;
        }

        private static TMP_Text FindTextDeep(
            Transform root,
            string objectName)
        {
            Transform transform =
                FindDeep(
                    root,
                    objectName);

            return transform != null
                ? transform.GetComponent<TMP_Text>()
                : null;
        }

        private static RectTransform FindRect(
            Transform parent,
            string childName)
        {
            Transform child =
                parent.Find(
                    childName);

            return child as RectTransform;
        }

        private static Transform FindDeep(
            Transform root,
            string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0;
                 i < root.childCount;
                 i++)
            {
                Transform found =
                    FindDeep(
                        root.GetChild(i),
                        objectName);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static RectTransform RequireRectTransform(
            GameObject gameObject)
        {
            RectTransform rect =
                gameObject.GetComponent<RectTransform>();

            if (rect == null)
            {
                throw new InvalidOperationException(
                    $"'{gameObject.name}' requires RectTransform.");
            }

            return rect;
        }

        private static T GetOrAdd<T>(
            GameObject gameObject)
            where T : Component
        {
            T component =
                gameObject.GetComponent<T>();

            return component != null
                ? component
                : gameObject.AddComponent<T>();
        }

        private static string GetRelativePath(
            Transform root,
            Transform target)
        {
            if (root == target)
                return string.Empty;

            Stack<string> names =
                new();

            Transform current =
                target;

            while (current != null &&
                   current != root)
            {
                names.Push(
                    current.name);

                current =
                    current.parent;
            }

            if (current != root)
                return null;

            return string.Join(
                "/",
                names);
        }
    }
}

#endif