#if UNITY_EDITOR

using System;
using GoLive.Phone;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Editor.Phone
{
    public static class PhoneShopVisualBuilder
    {
        private const string MenuPath =
            "GO LIVE/Phone Shop/Rebuild Full Shop Visuals";

        // ---------- Palette ----------

        private static readonly Color ShopBackground =
            Hex("111315");

        private static readonly Color CardBackground =
            Hex("202327");

        private static readonly Color CardHover =
            Hex("272B30");

        private static readonly Color ThumbBackground =
            Hex("2B2E32");

        private static readonly Color PrimaryText =
            Hex("F3F3F0");

        private static readonly Color SecondaryText =
            Hex("A2A8AF");

        private static readonly Color MutedText =
            Hex("727980");

        private static readonly Color Accent =
            Hex("4B94DF");

        private static readonly Color AccentHover =
            Hex("5CA4EE");

        private static readonly Color AccentPressed =
            Hex("397CC0");

        private static readonly Color InactiveTab =
            Hex("272A2E");

        private static readonly Color ActiveTab =
            Hex("3F83C7");

        private static readonly Color BottomButton =
            Hex("24282D");

        private static readonly Color BottomButtonHover =
            Hex("2D3339");

        private static readonly Color BadgeColor =
            Hex("F47755");

        // ----------------------------------------------------------

        [MenuItem(MenuPath, true)]
        private static bool ValidateMenu()
        {
            if (EditorApplication.isPlaying)
                return false;

            return FindViewFromSelection() != null;
        }

        [MenuItem(MenuPath)]
        private static void Rebuild()
        {
            PhoneShopView selectedView =
                FindViewFromSelection();

            if (selectedView == null)
            {
                Debug.LogError(
                    "Select the Shop object, PhoneShopView, or any child of Shop first.");

                return;
            }

            if (TryRebuildPrefabSource(selectedView))
                return;

            Undo.RegisterFullObjectHierarchyUndo(
                selectedView.gameObject,
                "Rebuild Phone Shop Visuals");

            ConfigureShop(selectedView);

            EditorUtility.SetDirty(selectedView);
            EditorSceneManager.MarkSceneDirty(
                selectedView.gameObject.scene);

            Debug.Log(
                "Phone Shop visual pass rebuilt in the current scene. " +
                "Save the scene and check it in Play Mode.");
        }

        // ==========================================================
        // PREFAB HANDLING
        // ==========================================================

        private static bool TryRebuildPrefabSource(
            PhoneShopView sceneView)
        {
            GameObject instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(
                    sceneView.gameObject);

            if (instanceRoot == null)
                return false;

            GameObject sourceRoot =
                PrefabUtility.GetCorrespondingObjectFromSource(
                    instanceRoot);

            if (sourceRoot == null)
                return false;

            string prefabPath =
                AssetDatabase.GetAssetPath(sourceRoot);

            if (string.IsNullOrWhiteSpace(prefabPath))
                return false;

            string relativePath =
                GetRelativePath(
                    instanceRoot.transform,
                    sceneView.transform);

            if (relativePath == null)
                return false;

            GameObject prefabRoot =
                PrefabUtility.LoadPrefabContents(
                    prefabPath);

            try
            {
                Transform target =
                    string.IsNullOrEmpty(relativePath)
                        ? prefabRoot.transform
                        : prefabRoot.transform.Find(relativePath);

                if (target == null)
                {
                    Debug.LogError(
                        $"Could not find Shop path '{relativePath}' inside '{prefabPath}'.");

                    return true;
                }

                PhoneShopView prefabView =
                    target.GetComponent<PhoneShopView>();

                if (prefabView == null)
                {
                    Debug.LogError(
                        $"'{relativePath}' in '{prefabPath}' has no PhoneShopView.");

                    return true;
                }

                ConfigureShop(prefabView);

                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath);

                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"Phone Shop visual pass rebuilt in prefab:\n{prefabPath}\n" +
                    "Scene instance should refresh automatically.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot);
            }

            return true;
        }

        // ==========================================================
        // WHOLE SHOP
        // ==========================================================

        private static void ConfigureShop(
            PhoneShopView view)
        {
            SerializedObject serialized =
                new(view);

            ConfigureBackground(view.gameObject);
            ConfigureRuntimePalette(serialized);

            GameObject storefront =
                GetObjectReference<GameObject>(
                    serialized,
                    "_storefront");

            if (storefront != null)
            {
                StretchFull(
                    RequireRect(storefront));

                ConfigureStorefront(
                    storefront,
                    serialized);
            }

            ConfigureStatusAndTitle(
                view.transform);

            ConfigureProductDetails(
                serialized);

            ConfigureOrders(
                serialized);

            ConfigureBottomBar(
                view.transform,
                serialized);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(view);
        }

        // ==========================================================
        // BACKGROUND
        // ==========================================================

        private static void ConfigureBackground(
            GameObject shop)
        {
            Image background =
                GetOrAdd<Image>(shop);

            background.color =
                ShopBackground;

            background.raycastTarget =
                false;

            background.sprite =
                null;

            background.type =
                Image.Type.Simple;
        }

        // ==========================================================
        // STOREFRONT
        // ==========================================================

        private static void ConfigureStorefront(
            GameObject storefront,
            SerializedObject serialized)
        {
            Transform categoryTabsTransform =
                FindDeep(
                    storefront.transform,
                    "CategoryTabs");

            if (categoryTabsTransform is RectTransform tabs)
            {
                SetTopStretch(
                    tabs,
                    left: 22f,
                    right: 22f,
                    top: 102f,
                    height: 42f);

                HorizontalLayoutGroup layout =
                    GetOrAdd<HorizontalLayoutGroup>(
                        tabs.gameObject);

                layout.padding =
                    new RectOffset(0, 0, 0, 0);

                layout.spacing = 6f;

                layout.childAlignment =
                    TextAnchor.MiddleLeft;

                layout.childControlWidth = true;
                layout.childControlHeight = true;

                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = true;
            }

            ConfigureCategoryTabs(
                serialized);

            ScrollRect catalog =
                GetObjectReference<ScrollRect>(
                    serialized,
                    "_catalog");

            if (catalog != null)
                ConfigureCatalog(catalog);

            ShopProductCardView productCard =
                GetObjectReference<ShopProductCardView>(
                    serialized,
                    "_productCardTemplate");

            if (productCard != null)
                ConfigureProductCard(productCard);

            TMP_Text empty =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_catalogEmpty");

            if (empty != null)
            {
                ApplyFont(
                    empty,
                    "manrope",
                    "regular");

                empty.fontSize = 14f;
                empty.color = SecondaryText;

                empty.alignment =
                    TextAlignmentOptions.Center;

                empty.textWrappingMode =
                    TextWrappingModes.Normal;
            }
        }

        // ==========================================================
        // TABS
        // ==========================================================

        private static void ConfigureRuntimePalette(
            SerializedObject serialized)
        {
            SetColor(
                serialized,
                "_tabColor",
                InactiveTab);

            SetColor(
                serialized,
                "_tabLabelColor",
                SecondaryText);

            SetColor(
                serialized,
                "_selectedTabColor",
                ActiveTab);

            SetColor(
                serialized,
                "_selectedTabLabelColor",
                PrimaryText);
        }

        private static void ConfigureCategoryTabs(
            SerializedObject serialized)
        {
            SerializedProperty tabs =
                serialized.FindProperty("_tabs");

            if (tabs == null || !tabs.isArray)
                return;

            for (int i = 0; i < tabs.arraySize; i++)
            {
                SerializedProperty tab =
                    tabs.GetArrayElementAtIndex(i);

                Button button =
                    tab
                        .FindPropertyRelative(
                            "<Button>k__BackingField")
                        ?.objectReferenceValue as Button;

                TMP_Text label =
                    tab
                        .FindPropertyRelative(
                            "<Label>k__BackingField")
                        ?.objectReferenceValue as TMP_Text;

                if (button != null)
                {
                    LayoutElement layout =
                        GetOrAdd<LayoutElement>(
                            button.gameObject);

                    layout.minWidth = 0f;
                    layout.preferredWidth = 0f;
                    layout.flexibleWidth = 1f;

                    layout.minHeight = 40f;
                    layout.preferredHeight = 40f;
                    layout.flexibleHeight = 0f;

                    Image image =
                        button.image;

                    if (image != null)
                    {
                        image.color =
                            InactiveTab;

                        image.raycastTarget =
                            true;
                    }

                    button.transition =
                        Selectable.Transition.ColorTint;

                    ColorBlock colors =
                        button.colors;

                    colors.normalColor =
                        InactiveTab;

                    colors.highlightedColor =
                        CardHover;

                    colors.pressedColor =
                        ActiveTab;

                    colors.selectedColor =
                        ActiveTab;

                    colors.disabledColor =
                        new Color(
                            InactiveTab.r,
                            InactiveTab.g,
                            InactiveTab.b,
                            0.45f);

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
                }

                if (label != null)
                {
                    ApplyFont(
                        label,
                        "manrope",
                        "medium");

                    label.fontSize = 14f;
                    label.fontStyle =
                        FontStyles.Normal;

                    label.color =
                        SecondaryText;

                    label.alignment =
                        TextAlignmentOptions.Center;

                    label.textWrappingMode =
                        TextWrappingModes.NoWrap;
                }
            }
        }

        // ==========================================================
        // CATALOG
        // ==========================================================

        private static void ConfigureCatalog(
            ScrollRect catalog)
        {
            RectTransform catalogRect =
                catalog.GetComponent<RectTransform>();

            if (catalogRect != null)
            {
                SetStretch(
                    catalogRect,
                    left: 22f,
                    right: 22f,
                    top: 154f,
                    bottom: 104f);
            }

            Image background =
                catalog.GetComponent<Image>();

            if (background != null)
            {
                background.color =
                    new Color(
                        0f,
                        0f,
                        0f,
                        0f);

                background.raycastTarget =
                    true;
            }

            catalog.horizontal = false;
            catalog.vertical = true;

            catalog.movementType =
                ScrollRect.MovementType.Clamped;

            catalog.inertia = true;
            catalog.decelerationRate = 0.08f;
            catalog.scrollSensitivity = 28f;

            RectTransform content =
                catalog.content;

            if (content != null)
                ConfigureCatalogContent(content);

            if (catalog.verticalScrollbar != null)
            {
                Scrollbar scrollbar =
                    catalog.verticalScrollbar;

                Image scrollbarImage =
                    scrollbar.GetComponent<Image>();

                if (scrollbarImage != null)
                {
                    scrollbarImage.color =
                        new Color(
                            1f,
                            1f,
                            1f,
                            0.03f);
                }

                if (scrollbar.handleRect != null)
                {
                    Image handle =
                        scrollbar.handleRect.GetComponent<Image>();

                    if (handle != null)
                    {
                        handle.color =
                            new Color(
                                0.55f,
                                0.65f,
                                0.75f,
                                0.35f);
                    }
                }
            }
        }

        private static void ConfigureCatalogContent(
            RectTransform content)
        {
            content.anchorMin =
                new Vector2(0f, 1f);

            content.anchorMax =
                new Vector2(1f, 1f);

            content.pivot =
                new Vector2(0.5f, 1f);

            content.anchoredPosition =
                Vector2.zero;

            content.sizeDelta =
                new Vector2(
                    0f,
                    content.sizeDelta.y);

            VerticalLayoutGroup layout =
                GetOrAdd<VerticalLayoutGroup>(
                    content.gameObject);

            layout.padding =
                new RectOffset(
                    0,
                    0,
                    4,
                    10);

            layout.spacing = 10f;

            layout.childAlignment =
                TextAnchor.UpperLeft;

            layout.childControlWidth = true;
            layout.childControlHeight = true;

            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                GetOrAdd<ContentSizeFitter>(
                    content.gameObject);

            fitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
        }

        // ==========================================================
        // PRODUCT CARD
        // ==========================================================

        private static void ConfigureProductCard(
            ShopProductCardView cardView)
        {
            GameObject card =
                cardView.gameObject;

            LayoutElement rootLayout =
                GetOrAdd<LayoutElement>(card);

            rootLayout.preferredHeight = 138f;
            rootLayout.flexibleHeight = 0f;

            Image image =
                GetOrAdd<Image>(card);

            image.color =
                CardBackground;

            Button button =
                card.GetComponent<Button>();

            if (button != null)
            {
                button.targetGraphic =
                    image;

                button.transition =
                    Selectable.Transition.ColorTint;

                ColorBlock colors =
                    button.colors;

                colors.normalColor =
                    CardBackground;

                colors.highlightedColor =
                    CardHover;

                colors.pressedColor =
                    Hex("181B1F");

                colors.selectedColor =
                    CardHover;

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
            }

            HorizontalLayoutGroup rootGroup =
                GetOrAdd<HorizontalLayoutGroup>(
                    card);

            rootGroup.padding =
                new RectOffset(
                    10,
                    10,
                    10,
                    10);

            rootGroup.spacing = 10f;

            rootGroup.childAlignment =
                TextAnchor.MiddleLeft;

            rootGroup.childControlWidth = true;
            rootGroup.childControlHeight = true;

            rootGroup.childForceExpandWidth = false;
            rootGroup.childForceExpandHeight = false;

            Transform thumb =
                FindDeep(
                    card.transform,
                    "Thumb");

            if (thumb != null)
                ConfigureThumb(thumb);

            Transform info =
                FindDeep(
                    card.transform,
                    "Info");

            if (info != null)
                ConfigureInfo(info);

            Transform actions =
                FindDeep(
                    card.transform,
                    "Actions");

            if (actions != null)
                ConfigureActions(actions);
        }

        private static void ConfigureThumb(
            Transform thumb)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    thumb.gameObject);

            layout.minWidth = 100f;
            layout.minHeight = 100f;
            layout.preferredWidth = 100f;
            layout.preferredHeight = 100f;

            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image background =
                GetOrAdd<Image>(
                    thumb.gameObject);

            background.color =
                ThumbBackground;

            background.raycastTarget =
                false;

            Transform productImage =
                thumb.Find("ProductImage");

            if (productImage != null)
            {
                Image image =
                    productImage.GetComponent<Image>();

                if (image != null)
                {
                    RectTransform rect =
                        (RectTransform)productImage;

                    rect.anchorMin =
                        Vector2.zero;

                    rect.anchorMax =
                        Vector2.one;

                    rect.offsetMin =
                        new Vector2(8f, 8f);

                    rect.offsetMax =
                        new Vector2(-8f, -8f);

                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }
        }

        private static void ConfigureInfo(
            Transform info)
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
                new RectOffset(0, 0, 0, 0);

            group.spacing = 1f;

            group.childAlignment =
                TextAnchor.MiddleLeft;

            group.childControlWidth = true;
            group.childControlHeight = true;

            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;

            TMP_Text name =
                FindTextDeep(
                    info,
                    "ProductName");

            if (name != null)
            {
                ApplyFont(
                    name,
                    "manrope",
                    "semibold");

                name.fontSize = 19f;
                name.fontStyle =
                    FontStyles.Normal;

                name.color =
                    PrimaryText;

                name.textWrappingMode =
                    TextWrappingModes.Normal;

                name.overflowMode =
                    TextOverflowModes.Ellipsis;

                LayoutElement nameLayout =
                    GetOrAdd<LayoutElement>(
                        name.gameObject);

                nameLayout.preferredHeight = 44f;
                nameLayout.flexibleHeight = 0f;
            }

            TMP_Text category =
                FindTextDeep(
                    info,
                    "Category");

            if (category != null)
            {
                ApplyFont(
                    category,
                    "manrope",
                    "regular");

                category.fontSize = 12f;
                category.fontStyle =
                    FontStyles.Normal;

                category.color =
                    SecondaryText;

                category.textWrappingMode =
                    TextWrappingModes.NoWrap;

                LayoutElement categoryLayout =
                    GetOrAdd<LayoutElement>(
                        category.gameObject);

                categoryLayout.preferredHeight = 20f;
                categoryLayout.flexibleHeight = 0f;
            }

            TMP_Text price =
                FindTextDeep(
                    info,
                    "Price");

            if (price != null)
            {
                ApplyFont(
                    price,
                    "manrope",
                    "semibold");

                price.fontSize = 20f;
                price.fontStyle =
                    FontStyles.Normal;

                price.color =
                    Accent;

                price.textWrappingMode =
                    TextWrappingModes.NoWrap;

                LayoutElement priceLayout =
                    GetOrAdd<LayoutElement>(
                        price.gameObject);

                priceLayout.preferredHeight = 28f;
                priceLayout.flexibleHeight = 0f;
            }

            TMP_Text status =
                FindTextDeep(
                    info,
                    "Status");

            if (status != null)
            {
                ApplyFont(
                    status,
                    "manrope",
                    "medium");

                status.fontSize = 12f;
                status.color = SecondaryText;

                status.textWrappingMode =
                    TextWrappingModes.NoWrap;
            }
        }

        private static void ConfigureActions(
            Transform actions)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    actions.gameObject);

            layout.preferredWidth = 42f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            VerticalLayoutGroup group =
                GetOrAdd<VerticalLayoutGroup>(
                    actions.gameObject);

            group.spacing = 6f;

            group.childAlignment =
                TextAnchor.MiddleCenter;

            group.childControlWidth = true;
            group.childControlHeight = true;

            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            TMP_Text chevron =
                FindTextDeep(
                    actions,
                    "Chevron");

            if (chevron != null)
            {
                ApplyFont(
                    chevron,
                    "manrope",
                    "regular");

                chevron.fontSize = 24f;
                chevron.color =
                    SecondaryText;

                chevron.alignment =
                    TextAlignmentOptions.Center;

                chevron.textWrappingMode =
                    TextWrappingModes.NoWrap;

                LayoutElement chevronLayout =
                    GetOrAdd<LayoutElement>(
                        chevron.gameObject);

                chevronLayout.preferredWidth = 20f;
                chevronLayout.preferredHeight = 28f;
            }

            Transform quickBuy =
                actions.Find("QuickBuyButton");

            if (quickBuy != null)
                ConfigureQuickBuy(quickBuy);
        }

        private static void ConfigureQuickBuy(
            Transform quickBuy)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    quickBuy.gameObject);

            layout.preferredWidth = 42f;
            layout.preferredHeight = 42f;

            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image image =
                GetOrAdd<Image>(
                    quickBuy.gameObject);

            image.color =
                Accent;

            Button button =
                GetOrAdd<Button>(
                    quickBuy.gameObject);

            button.targetGraphic =
                image;

            ColorBlock colors =
                button.colors;

            colors.normalColor =
                Accent;

            colors.highlightedColor =
                AccentHover;

            colors.pressedColor =
                AccentPressed;

            colors.selectedColor =
                AccentHover;

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
        }

        // ==========================================================
        // TITLE / HEADER
        // ==========================================================

        private static void ConfigureStatusAndTitle(
            Transform shopRoot)
        {
            Transform group =
                FindDeep(
                    shopRoot,
                    "StatusAndTitle");

            if (group == null)
                return;

            TMP_Text[] texts =
                group.GetComponentsInChildren<TMP_Text>(
                    true);

            if (texts.Length == 0)
                return;

            TMP_Text title =
                null;

            float largest =
                float.MinValue;

            foreach (TMP_Text text in texts)
            {
                if (text.fontSize > largest)
                {
                    largest = text.fontSize;
                    title = text;
                }
            }

            foreach (TMP_Text text in texts)
            {
                bool isTitle =
                    text == title;

                ApplyFont(
                    text,
                    "manrope",
                    isTitle
                        ? "semibold"
                        : "regular");

                text.fontStyle =
                    FontStyles.Normal;

                text.color =
                    isTitle
                        ? PrimaryText
                        : SecondaryText;

                if (isTitle)
                    text.fontSize = 28f;

                text.textWrappingMode =
                    TextWrappingModes.NoWrap;
            }
        }

        // ==========================================================
        // BOTTOM BAR
        // ==========================================================

        private static void ConfigureBottomBar(
            Transform shopRoot,
            SerializedObject serialized)
        {
            Transform bottomTransform =
                FindDeep(
                    shopRoot,
                    "BottomBar");

            if (bottomTransform is not RectTransform bottom)
                return;

            SetBottomStretch(
                bottom,
                left: 22f,
                right: 22f,
                bottom: 18f,
                height: 52f);

            HorizontalLayoutGroup layout =
                GetOrAdd<HorizontalLayoutGroup>(
                    bottom.gameObject);

            layout.padding =
                new RectOffset(0, 0, 0, 0);

            layout.spacing = 10f;

            layout.childAlignment =
                TextAnchor.MiddleCenter;

            layout.childControlWidth = true;
            layout.childControlHeight = true;

            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            Button orders =
                GetObjectReference<Button>(
                    serialized,
                    "_openOrders");

            TMP_Text ordersLabel =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_openOrdersLabel");

            if (orders != null)
                ConfigureBottomButton(orders);

            if (ordersLabel != null)
                ConfigureBottomLabel(ordersLabel);

            Transform backTransform =
                FindDeep(
                    bottom,
                    "Back");

            if (backTransform != null)
            {
                Button back =
                    backTransform.GetComponent<Button>();

                if (back != null)
                    ConfigureBottomButton(back);

                TMP_Text backLabel =
                    backTransform.GetComponentInChildren<TMP_Text>(
                        true);

                if (backLabel != null)
                    ConfigureBottomLabel(backLabel);
            }

            GameObject badge =
                GetObjectReference<GameObject>(
                    serialized,
                    "_activeOrdersBadge");

            TMP_Text count =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_activeOrdersCount");

            if (badge != null)
            {
                Image image =
                    badge.GetComponent<Image>();

                if (image != null)
                    image.color = BadgeColor;
            }

            if (count != null)
            {
                ApplyFont(
                    count,
                    "manrope",
                    "semibold");

                count.fontSize = 10f;
                count.color = Color.white;
                count.alignment =
                    TextAlignmentOptions.Center;
            }
        }

        private static void ConfigureBottomButton(
            Button button)
        {
            Image image =
                button.image;

            if (image != null)
                image.color = BottomButton;

            button.transition =
                Selectable.Transition.ColorTint;

            ColorBlock colors =
                button.colors;

            colors.normalColor =
                BottomButton;

            colors.highlightedColor =
                BottomButtonHover;

            colors.pressedColor =
                Hex("1C2024");

            colors.selectedColor =
                BottomButtonHover;

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
        }

        private static void ConfigureBottomLabel(
            TMP_Text label)
        {
            ApplyFont(
                label,
                "manrope",
                "medium");

            label.fontSize = 15f;
            label.fontStyle =
                FontStyles.Normal;

            label.color =
                PrimaryText;

            label.alignment =
                TextAlignmentOptions.Center;

            label.textWrappingMode =
                TextWrappingModes.NoWrap;
        }

        // ==========================================================
        // PRODUCT DETAILS
        // ==========================================================

        private static void ConfigureProductDetails(
            SerializedObject serialized)
        {
            GameObject root =
                GetObjectReference<GameObject>(
                    serialized,
                    "_productDetails");

            if (root == null)
                return;

            StretchFull(
                RequireRect(root));

            GameObject media =
                GetObjectReference<GameObject>(
                    serialized,
                    "_detailsMedia");

            if (media != null)
            {
                Image background =
                    GetOrAdd<Image>(
                        media);

                background.color =
                    ThumbBackground;

                background.raycastTarget =
                    false;
            }

            Image productImage =
                GetObjectReference<Image>(
                    serialized,
                    "_detailsImage");

            if (productImage != null)
            {
                productImage.preserveAspect = true;
                productImage.raycastTarget = false;
            }

            TMP_Text category =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_detailsCategory");

            TMP_Text name =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_detailsName");

            TMP_Text price =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_detailsPrice");

            TMP_Text delivery =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_detailsDelivery");

            TMP_Text description =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_detailsDescription");

            Button buy =
                GetObjectReference<Button>(
                    serialized,
                    "_detailsBuy");

            TMP_Text buyLabel =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_detailsBuyLabel");

            StyleDetailsText(
                category,
                13f,
                SecondaryText,
                "regular");

            StyleDetailsText(
                name,
                24f,
                PrimaryText,
                "semibold");

            StyleDetailsText(
                price,
                27f,
                Accent,
                "semibold");

            StyleDetailsText(
                delivery,
                13f,
                SecondaryText,
                "regular");

            StyleDetailsText(
                description,
                14f,
                SecondaryText,
                "regular");

            if (description != null)
            {
                description.textWrappingMode =
                    TextWrappingModes.Normal;
            }

            if (buy != null)
            {
                Image image =
                    buy.image;

                if (image != null)
                    image.color = Accent;

                ColorBlock colors =
                    buy.colors;

                colors.normalColor = Accent;
                colors.highlightedColor = AccentHover;
                colors.pressedColor = AccentPressed;
                colors.selectedColor = AccentHover;

                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;

                buy.colors = colors;

                buy.navigation =
                    new Navigation
                        {
                            mode =
                                Navigation.Mode.None
                        };
            }

            if (buyLabel != null)
            {
                ApplyFont(
                    buyLabel,
                    "manrope",
                    "semibold");

                buyLabel.fontSize = 16f;
                buyLabel.fontStyle =
                    FontStyles.Normal;
            }
        }

        // ==========================================================
        // ORDERS
        // ==========================================================

        private static void ConfigureOrders(
            SerializedObject serialized)
        {
            GameObject root =
                GetObjectReference<GameObject>(
                    serialized,
                    "_orders");

            if (root == null)
                return;

            StretchFull(
                RequireRect(root));

            ScrollRect list =
                GetObjectReference<ScrollRect>(
                    serialized,
                    "_orderList");

            if (list != null &&
                list.content != null)
            {
                ConfigureCatalogContent(
                    list.content);
            }

            ShopOrderRowView template =
                GetObjectReference<ShopOrderRowView>(
                    serialized,
                    "_orderRowTemplate");

            if (template != null)
            {
                Image image =
                    template.GetComponent<Image>();

                if (image != null)
                    image.color = CardBackground;

                TMP_Text[] texts =
                    template.GetComponentsInChildren<TMP_Text>(
                        true);

                foreach (TMP_Text text in texts)
                {
                    ApplyFont(
                        text,
                        "manrope",
                        "regular");

                    text.fontStyle =
                        FontStyles.Normal;
                }
            }

            TMP_Text empty =
                GetObjectReference<TMP_Text>(
                    serialized,
                    "_ordersEmpty");

            if (empty != null)
            {
                ApplyFont(
                    empty,
                    "manrope",
                    "regular");

                empty.fontSize = 14f;
                empty.color = SecondaryText;
            }
        }

        // ==========================================================
        // TEXT HELPERS
        // ==========================================================

        private static void StyleDetailsText(
            TMP_Text text,
            float size,
            Color color,
            params string[] fontTokens)
        {
            if (text == null)
                return;

            ApplyFont(
                text,
                fontTokens);

            text.fontSize = size;
            text.fontStyle =
                FontStyles.Normal;

            text.color = color;

            text.alignment =
                TextAlignmentOptions.Left;
        }

        private static void ApplyFont(
            TMP_Text text,
            params string[] tokens)
        {
            TMP_FontAsset font =
                FindFontAsset(tokens);

            if (font != null)
                text.font = font;
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

                string name =
                    System.IO.Path
                        .GetFileNameWithoutExtension(path)
                        .ToLowerInvariant();

                bool matches =
                    true;

                foreach (string token in requiredTokens)
                {
                    if (name.Contains(
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

            Debug.LogWarning(
                "Neither requested Manrope font nor PhoneSans fallback was found.");

            return null;
        }

        // ==========================================================
        // SERIALIZED HELPERS
        // ==========================================================

        private static T GetObjectReference<T>(
            SerializedObject serialized,
            string fieldName)
            where T : UnityEngine.Object
        {
            SerializedProperty property =
                serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning(
                    $"PhoneShopView field '{fieldName}' was not found.");

                return null;
            }

            return property.objectReferenceValue as T;
        }

        private static void SetColor(
            SerializedObject serialized,
            string fieldName,
            Color value)
        {
            SerializedProperty property =
                serialized.FindProperty(fieldName);

            if (property != null)
                property.colorValue = value;
        }

        // ==========================================================
        // RECT HELPERS
        // ==========================================================

        private static void StretchFull(
            RectTransform rect)
        {
            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.pivot =
                new Vector2(0.5f, 0.5f);

            rect.offsetMin =
                Vector2.zero;

            rect.offsetMax =
                Vector2.zero;
        }

        private static void SetTopStretch(
            RectTransform rect,
            float left,
            float right,
            float top,
            float height)
        {
            rect.anchorMin =
                new Vector2(0f, 1f);

            rect.anchorMax =
                new Vector2(1f, 1f);

            rect.pivot =
                new Vector2(0.5f, 1f);

            rect.offsetMin =
                new Vector2(
                    left,
                    -top - height);

            rect.offsetMax =
                new Vector2(
                    -right,
                    -top);
        }

        private static void SetBottomStretch(
            RectTransform rect,
            float left,
            float right,
            float bottom,
            float height)
        {
            rect.anchorMin =
                new Vector2(0f, 0f);

            rect.anchorMax =
                new Vector2(1f, 0f);

            rect.pivot =
                new Vector2(0.5f, 0f);

            rect.offsetMin =
                new Vector2(
                    left,
                    bottom);

            rect.offsetMax =
                new Vector2(
                    -right,
                    bottom + height);
        }

        private static void SetStretch(
            RectTransform rect,
            float left,
            float right,
            float top,
            float bottom)
        {
            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.pivot =
                new Vector2(0.5f, 0.5f);

            rect.offsetMin =
                new Vector2(
                    left,
                    bottom);

            rect.offsetMax =
                new Vector2(
                    -right,
                    -top);
        }

        // ==========================================================
        // GENERAL HELPERS
        // ==========================================================

        private static PhoneShopView FindViewFromSelection()
        {
            GameObject selected =
                Selection.activeGameObject;

            if (selected == null)
                return null;

            PhoneShopView view =
                selected.GetComponent<PhoneShopView>();

            if (view != null)
                return view;

            view =
                selected.GetComponentInParent<PhoneShopView>(
                    true);

            if (view != null)
                return view;

            return selected.GetComponentInChildren<PhoneShopView>(
                true);
        }

        private static Transform FindDeep(
            Transform root,
            string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
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

        private static TMP_Text FindTextDeep(
            Transform root,
            string objectName)
        {
            Transform found =
                FindDeep(
                    root,
                    objectName);

            return found != null
                ? found.GetComponent<TMP_Text>()
                : null;
        }

        private static RectTransform RequireRect(
            GameObject target)
        {
            RectTransform rect =
                target.GetComponent<RectTransform>();

            if (rect == null)
            {
                throw new InvalidOperationException(
                    $"'{target.name}' requires a RectTransform.");
            }

            return rect;
        }

        private static T GetOrAdd<T>(
            GameObject target)
            where T : Component
        {
            T existing =
                target.GetComponent<T>();

            return existing != null
                ? existing
                : target.AddComponent<T>();
        }

        private static string GetRelativePath(
            Transform root,
            Transform target)
        {
            if (root == target)
                return string.Empty;

            System.Collections.Generic.Stack<string> names =
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

        private static Color Hex(
            string value)
        {
            if (!ColorUtility.TryParseHtmlString(
                    "#" + value,
                    out Color color))
            {
                return Color.magenta;
            }

            return color;
        }
    }
}

#endif