#if UNITY_EDITOR

using System;
using GoLive.Localization;
using GoLive.Phone;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Editor.Phone
{
    public static class PhoneShopCartBuilder
    {
        private const string MenuPath =
            "GO LIVE/Phone Shop/Migrate To Cart Flow";

        private const string LocalizationPath =
            "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";

        private static readonly Color Surface =
            Hex("202327");

        private static readonly Color SurfaceSoft =
            Hex("2B2E32");

        private static readonly Color Primary =
            Hex("F3F3F0");

        private static readonly Color Secondary =
            Hex("A2A8AF");

        private static readonly Color Accent =
            Hex("4B94DF");

        [MenuItem(MenuPath, true)]
        private static bool ValidateMenu()
        {
            return
                !EditorApplication.isPlaying &&
                FindShopFromSelection() != null;
        }

        [MenuItem(MenuPath)]
        private static void MigrateSelected()
        {
            PhoneShopView selected =
                FindShopFromSelection();

            if (selected == null)
            {
                Debug.LogError(
                    "Select Shop, PhoneShopView, or any child of Shop first.");

                return;
            }

            AddLocalizationEntries();

            if (TryMigratePrefabSource(selected))
                return;

            Undo.RegisterFullObjectHierarchyUndo(
                selected.gameObject,
                "Migrate Phone Shop To Cart Flow");

            Configure(selected);

            EditorUtility.SetDirty(selected);
            EditorSceneManager.MarkSceneDirty(
                selected.gameObject.scene);

            AssetDatabase.SaveAssets();

            Debug.Log(
                "Phone Shop cart migration completed in the current scene. Save the scene before Play Mode.");
        }

        private static bool TryMigratePrefabSource(
            PhoneShopView selected)
        {
            GameObject instanceRoot =
                PrefabUtility.GetNearestPrefabInstanceRoot(
                    selected.gameObject);

            if (instanceRoot == null)
                return false;

            GameObject sourceRoot =
                PrefabUtility.GetCorrespondingObjectFromSource(
                    instanceRoot);

            if (sourceRoot == null)
                return false;

            string prefabPath =
                AssetDatabase.GetAssetPath(
                    sourceRoot);

            if (string.IsNullOrWhiteSpace(prefabPath))
                return false;

            string relativePath =
                GetRelativePath(
                    instanceRoot.transform,
                    selected.transform);

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

                PhoneShopView view =
                    target.GetComponent<PhoneShopView>();

                if (view == null)
                {
                    Debug.LogError(
                        $"'{relativePath}' inside '{prefabPath}' has no {nameof(PhoneShopView)}.");

                    return true;
                }

                Configure(view);

                PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath);

                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"Phone Shop cart migration completed in prefab:\n{prefabPath}\n" +
                    "Scene overrides outside the prefab were not touched.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(
                    prefabRoot);
            }

            return true;
        }

        private static void Configure(
            PhoneShopView shopView)
        {
            SerializedObject shop =
                new(shopView);

            Button ordersButton =
                GetReference<Button>(
                    shop,
                    "_openOrders");

            TMP_Text ordersLabel =
                GetReference<TMP_Text>(
                    shop,
                    "_openOrdersLabel");

            ShopProductCardView card =
                GetReference<ShopProductCardView>(
                    shop,
                    "_productCardTemplate");

            if (ordersButton == null ||
                ordersLabel == null ||
                card == null)
            {
                throw new InvalidOperationException(
                    "The existing Shop wiring is incomplete. Orders button, Orders label and ProductCardTemplate are required before migration.");
            }

            ConfigureProductCard(card);

            Transform bottomBar =
                ordersButton.transform.parent;

            if (bottomBar == null)
            {
                throw new InvalidOperationException(
                    "Phone Shop Orders button has no parent container for the storefront shortcuts.");
            }

            if (bottomBar.GetComponent<RectTransform>() == null)
            {
                throw new InvalidOperationException(
                    "Phone Shop Orders button parent must be a UI RectTransform.");
            }

            Button cartButton =
                EnsureCartShortcut(
                    bottomBar,
                    ordersButton,
                    ordersLabel,
                    out TMP_Text cartLabel,
                    out GameObject cartBadge,
                    out TMP_Text cartCount);

            ShopCartView cartView =
                RebuildCartScreen(
                    shopView.transform,
                    ordersLabel.font,
                    CardSprite(card));

            SetReference(
                shop,
                "_openCart",
                cartButton);

            SetReference(
                shop,
                "_openCartLabel",
                cartLabel);

            SetReference(
                shop,
                "_cartBadge",
                cartBadge);

            SetReference(
                shop,
                "_cartCount",
                cartCount);

            SetReference(
                shop,
                "_cartView",
                cartView);

            Button detailsButton =
                GetReference<Button>(
                    shop,
                    "_detailsAddToCart");

            if (detailsButton != null)
                detailsButton.gameObject.name = "AddToCartButton";

            shop.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shopView);
        }

        // ==========================================================
        // PRODUCT CARD
        // ==========================================================

        private static void ConfigureProductCard(
            ShopProductCardView card)
        {
            Transform root =
                card.transform;

            Transform actions =
                FindDeep(
                    root,
                    "Actions");

            if (actions == null)
            {
                throw new InvalidOperationException(
                    "ProductCardTemplate has no Actions object.");
            }

            Transform oldButton =
                actions.Find(
                    "QuickBuyButton");

            Transform newButton =
                actions.Find(
                    "AddToCartButton");

            Transform buttonTransform =
                newButton != null
                    ? newButton
                    : oldButton;

            if (buttonTransform == null)
            {
                throw new InvalidOperationException(
                    "ProductCardTemplate has no QuickBuyButton/AddToCartButton.");
            }

            buttonTransform.name =
                "AddToCartButton";

            Button addButton =
                buttonTransform.GetComponent<Button>();

            if (addButton == null)
            {
                throw new InvalidOperationException(
                    "ProductCardTemplate AddToCartButton has no Button component.");
            }

            SerializedObject cardSerialized =
                new(card);

            SetReference(
                cardSerialized,
                "_addToCartButton",
                addButton);

            cardSerialized.ApplyModifiedPropertiesWithoutUndo();

            LayoutElement rootLayout =
                GetOrAdd<LayoutElement>(
                    card.gameObject);

            rootLayout.preferredHeight = 150f;
            rootLayout.flexibleHeight = 0f;

            Transform info =
                FindDeep(
                    root,
                    "Info");

            if (info != null)
            {
                VerticalLayoutGroup group =
                    GetOrAdd<VerticalLayoutGroup>(
                        info.gameObject);

                group.spacing = 2f;
                group.childAlignment =
                    TextAnchor.MiddleLeft;

                group.childControlWidth = true;
                group.childControlHeight = true;

                group.childForceExpandWidth = true;
                group.childForceExpandHeight = false;

                LayoutElement infoLayout =
                    GetOrAdd<LayoutElement>(
                        info.gameObject);

                infoLayout.flexibleWidth = 1f;
                infoLayout.minHeight = 102f;
                infoLayout.preferredHeight = 102f;

                SetTextHeight(
                    info,
                    "ProductName",
                    46f);

                SetTextHeight(
                    info,
                    "Category",
                    20f);

                SetTextHeight(
                    info,
                    "Price",
                    28f);

                SetTextHeight(
                    info,
                    "Status",
                    28f);
            }

            EditorUtility.SetDirty(card);
        }

        private static void SetTextHeight(
            Transform parent,
            string name,
            float height)
        {
            Transform child =
                parent.Find(name);

            if (child == null)
                return;

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    child.gameObject);

            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        // ==========================================================
        // CART SHORTCUT
        // ==========================================================

        private static Button EnsureCartShortcut(
            Transform bottomBar,
            Button ordersButton,
            TMP_Text ordersLabel,
            out TMP_Text label,
            out GameObject badge,
            out TMP_Text count)
        {
            Transform existing =
                bottomBar.Find("CartButton");

            GameObject cartObject;

            if (existing != null)
            {
                cartObject = existing.gameObject;
            }
            else
            {
                cartObject =
                    UnityEngine.Object.Instantiate(
                        ordersButton.gameObject,
                        bottomBar);

                cartObject.name =
                    "CartButton";

                cartObject.transform.SetSiblingIndex(
                    ordersButton.transform.GetSiblingIndex());
            }

            Button button =
                cartObject.GetComponent<Button>();

            if (button == null)
                button = cartObject.AddComponent<Button>();

            button.onClick =
                new Button.ButtonClickedEvent();

            label =
                FindTextDeep(
                    cartObject.transform,
                    "Label") ??
                cartObject.GetComponentInChildren<TMP_Text>(
                    true);

            if (label == null)
            {
                label =
                    CreateText(
                        cartObject.transform,
                        "Label",
                        "Корзина",
                        ordersLabel.font,
                        15f,
                        Primary);
            }

            label.text = "Корзина";

            Transform badgeTransform =
                FindDeep(
                    cartObject.transform,
                    "Badge");

            if (badgeTransform == null)
            {
                badgeTransform =
                    CreateRect(
                        "Badge",
                        cartObject.transform);

                Image badgeImage =
                    badgeTransform.gameObject.AddComponent<Image>();

                badgeImage.color =
                    Hex("F47755");

                RectTransform badgeRect =
                    (RectTransform)badgeTransform;

                badgeRect.anchorMin =
                    new Vector2(1f, 1f);

                badgeRect.anchorMax =
                    new Vector2(1f, 1f);

                badgeRect.pivot =
                    new Vector2(1f, 1f);

                badgeRect.anchoredPosition =
                    new Vector2(-5f, -4f);

                badgeRect.sizeDelta =
                    new Vector2(22f, 22f);
            }

            badge =
                badgeTransform.gameObject;

            count =
                FindTextDeep(
                    badgeTransform,
                    "Count");

            if (count == null)
            {
                count =
                    CreateText(
                        badgeTransform,
                        "Count",
                        "0",
                        ordersLabel.font,
                        10f,
                        Color.white);

                Stretch(
                    count.rectTransform,
                    2f);
            }

            badge.SetActive(false);

            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    cartObject);

            layout.flexibleWidth = 1f;
            layout.preferredHeight = 46f;

            return button;
        }

        // ==========================================================
        // CART SCREEN
        // ==========================================================

        private static ShopCartView RebuildCartScreen(
            Transform shopRoot,
            TMP_FontAsset font,
            Sprite roundedSprite)
        {
            Transform existing =
                shopRoot.Find("Cart");

            if (existing != null)
            {
                if (existing.GetComponent<ShopCartView>() == null)
                {
                    throw new InvalidOperationException(
                        "Shop already contains a Cart object that was not authored by the GO LIVE cart migration.");
                }

                UnityEngine.Object.DestroyImmediate(
                    existing.gameObject);
            }

            RectTransform cart =
                CreateRect(
                    "Cart",
                    shopRoot);

            Stretch(cart);
            cart.gameObject.SetActive(false);

            ScrollRect list =
                CreateCartList(
                    cart,
                    roundedSprite);

            ShopCartRowView rowTemplate =
                CreateCartRowTemplate(
                    list.content,
                    font,
                    roundedSprite);

            TMP_Text empty =
                CreateText(
                    cart,
                    "Empty",
                    "Корзина пуста.",
                    font,
                    16f,
                    Secondary);

            SetStretch(
                empty.rectTransform,
                28f,
                28f,
                145f,
                145f);

            empty.alignment =
                TextAlignmentOptions.Center;

            RectTransform summary =
                CreateRect(
                    "Summary",
                    cart);

            SetBottomStretch(
                summary,
                28f,
                28f,
                76f,
                66f);

            HorizontalLayoutGroup summaryLayout =
                summary.gameObject.AddComponent<HorizontalLayoutGroup>();

            summaryLayout.spacing = 10f;
            summaryLayout.childAlignment =
                TextAnchor.MiddleCenter;

            summaryLayout.childControlWidth = true;
            summaryLayout.childControlHeight = true;
            summaryLayout.childForceExpandWidth = false;
            summaryLayout.childForceExpandHeight = true;

            TMP_Text total =
                CreateText(
                    summary,
                    "Total",
                    "Итого: $0.00",
                    font,
                    17f,
                    Primary);

            LayoutElement totalLayout =
                total.gameObject.AddComponent<LayoutElement>();

            totalLayout.flexibleWidth = 1f;

            Button checkout =
                CreateButton(
                    summary,
                    "CheckoutButton",
                    "Заказать",
                    font,
                    roundedSprite,
                    Accent,
                    out TMP_Text checkoutLabel);

            LayoutElement checkoutLayout =
                checkout.gameObject.AddComponent<LayoutElement>();

            checkoutLayout.preferredWidth = 150f;
            checkoutLayout.preferredHeight = 48f;
            checkoutLayout.flexibleWidth = 0f;

            TMP_Text status =
                CreateText(
                    cart,
                    "Status",
                    string.Empty,
                    font,
                    13f,
                    Hex("F0A46B"));

            SetBottomStretch(
                status.rectTransform,
                28f,
                28f,
                42f,
                24f);

            status.alignment =
                TextAlignmentOptions.Center;

            status.gameObject.SetActive(false);

            ShopCartView view =
                cart.gameObject.AddComponent<ShopCartView>();

            SerializedObject serialized =
                new(view);

            SetReference(
                serialized,
                "_list",
                list);

            SetReference(
                serialized,
                "_rowTemplate",
                rowTemplate);

            SetReference(
                serialized,
                "_empty",
                empty);

            SetReference(
                serialized,
                "_total",
                total);

            SetReference(
                serialized,
                "_status",
                status);

            SetReference(
                serialized,
                "_checkout",
                checkout);

            SetReference(
                serialized,
                "_checkoutLabel",
                checkoutLabel);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static ScrollRect CreateCartList(
            RectTransform parent,
            Sprite roundedSprite)
        {
            RectTransform viewport =
                CreateRect(
                    "CartList",
                    parent);

            SetStretch(
                viewport,
                28f,
                28f,
                98f,
                154f);

            Image viewportImage =
                viewport.gameObject.AddComponent<Image>();

            viewportImage.color =
                new Color(0f, 0f, 0f, 0f);

            viewport.gameObject.AddComponent<RectMask2D>();

            ScrollRect scroll =
                viewport.gameObject.AddComponent<ScrollRect>();

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType =
                ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.08f;
            scroll.scrollSensitivity = 28f;

            RectTransform content =
                CreateRect(
                    "Content",
                    viewport);

            content.anchorMin =
                new Vector2(0f, 1f);

            content.anchorMax =
                new Vector2(1f, 1f);

            content.pivot =
                new Vector2(0.5f, 1f);

            content.anchoredPosition =
                Vector2.zero;

            content.sizeDelta =
                new Vector2(0f, 0f);

            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.padding =
                new RectOffset(0, 0, 4, 10);

            layout.spacing = 10f;
            layout.childAlignment =
                TextAnchor.UpperLeft;

            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;

            fitter.verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            scroll.viewport = viewport;

            return scroll;
        }

        private static ShopCartRowView CreateCartRowTemplate(
            RectTransform parent,
            TMP_FontAsset font,
            Sprite roundedSprite)
        {
            RectTransform row =
                CreateRect(
                    "CartRowTemplate",
                    parent);

            Image rowImage =
                row.gameObject.AddComponent<Image>();

            rowImage.sprite = roundedSprite;
            rowImage.type =
                roundedSprite != null
                    ? Image.Type.Sliced
                    : Image.Type.Simple;

            rowImage.color = Surface;

            LayoutElement rowLayout =
                row.gameObject.AddComponent<LayoutElement>();

            rowLayout.preferredHeight = 112f;
            rowLayout.flexibleHeight = 0f;

            HorizontalLayoutGroup rowGroup =
                row.gameObject.AddComponent<HorizontalLayoutGroup>();

            rowGroup.padding =
                new RectOffset(10, 10, 10, 10);

            rowGroup.spacing = 10f;
            rowGroup.childAlignment =
                TextAnchor.MiddleLeft;

            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = false;
            rowGroup.childForceExpandHeight = false;

            RectTransform thumb =
                CreateRect(
                    "Thumb",
                    row);

            Image thumbBackground =
                thumb.gameObject.AddComponent<Image>();

            thumbBackground.sprite = roundedSprite;
            thumbBackground.type =
                roundedSprite != null
                    ? Image.Type.Sliced
                    : Image.Type.Simple;

            thumbBackground.color = SurfaceSoft;

            LayoutElement thumbLayout =
                thumb.gameObject.AddComponent<LayoutElement>();

            thumbLayout.preferredWidth = 78f;
            thumbLayout.preferredHeight = 78f;

            RectTransform productImageRect =
                CreateRect(
                    "ProductImage",
                    thumb);

            Stretch(
                productImageRect,
                7f);

            Image productImage =
                productImageRect.gameObject.AddComponent<Image>();

            productImage.preserveAspect = true;
            productImage.raycastTarget = false;

            RectTransform info =
                CreateRect(
                    "Info",
                    row);

            LayoutElement infoLayout =
                info.gameObject.AddComponent<LayoutElement>();

            infoLayout.flexibleWidth = 1f;

            VerticalLayoutGroup infoGroup =
                info.gameObject.AddComponent<VerticalLayoutGroup>();

            infoGroup.spacing = 1f;
            infoGroup.childAlignment =
                TextAnchor.MiddleLeft;
            infoGroup.childControlWidth = true;
            infoGroup.childControlHeight = true;
            infoGroup.childForceExpandWidth = true;
            infoGroup.childForceExpandHeight = false;

            TMP_Text name =
                CreateText(
                    info,
                    "ProductName",
                    "Товар",
                    font,
                    16f,
                    Primary);

            SetPreferredHeight(
                name.gameObject,
                30f);

            TMP_Text category =
                CreateText(
                    info,
                    "Category",
                    "Электроника",
                    font,
                    12f,
                    Secondary);

            SetPreferredHeight(
                category.gameObject,
                20f);

            TMP_Text price =
                CreateText(
                    info,
                    "Price",
                    "$0.00",
                    font,
                    16f,
                    Accent);

            SetPreferredHeight(
                price.gameObject,
                26f);

            RectTransform controls =
                CreateRect(
                    "QuantityControls",
                    row);

            LayoutElement controlsLayout =
                controls.gameObject.AddComponent<LayoutElement>();

            controlsLayout.preferredWidth = 116f;
            controlsLayout.preferredHeight = 42f;

            HorizontalLayoutGroup controlsGroup =
                controls.gameObject.AddComponent<HorizontalLayoutGroup>();

            controlsGroup.spacing = 4f;
            controlsGroup.childAlignment =
                TextAnchor.MiddleCenter;
            controlsGroup.childControlWidth = true;
            controlsGroup.childControlHeight = true;
            controlsGroup.childForceExpandWidth = false;
            controlsGroup.childForceExpandHeight = false;

            Button minus =
                CreateButton(
                    controls,
                    "RemoveOneButton",
                    "-",
                    font,
                    roundedSprite,
                    SurfaceSoft,
                    out _);

            SetButtonSize(
                minus,
                30f,
                36f);

            TMP_Text quantity =
                CreateText(
                    controls,
                    "Quantity",
                    "1",
                    font,
                    14f,
                    Primary);

            LayoutElement quantityLayout =
                quantity.gameObject.AddComponent<LayoutElement>();

            quantityLayout.preferredWidth = 24f;
            quantityLayout.preferredHeight = 36f;

            quantity.alignment =
                TextAlignmentOptions.Center;

            Button plus =
                CreateButton(
                    controls,
                    "AddOneButton",
                    "+",
                    font,
                    roundedSprite,
                    Accent,
                    out _);

            SetButtonSize(
                plus,
                30f,
                36f);

            Button removeAll =
                CreateButton(
                    controls,
                    "RemoveAllButton",
                    "x",
                    font,
                    roundedSprite,
                    SurfaceSoft,
                    out _);

            SetButtonSize(
                removeAll,
                30f,
                36f);

            ShopCartRowView view =
                row.gameObject.AddComponent<ShopCartRowView>();

            SerializedObject serialized =
                new(view);

            SetReference(
                serialized,
                "_thumbnail",
                thumb.gameObject);

            SetReference(
                serialized,
                "_image",
                productImage);

            SetReference(
                serialized,
                "_name",
                name);

            SetReference(
                serialized,
                "_category",
                category);

            SetReference(
                serialized,
                "_price",
                price);

            SetReference(
                serialized,
                "_quantity",
                quantity);

            SetReference(
                serialized,
                "_addOneButton",
                plus);

            SetReference(
                serialized,
                "_removeOneButton",
                minus);

            SetReference(
                serialized,
                "_removeAllButton",
                removeAll);

            serialized.ApplyModifiedPropertiesWithoutUndo();

            row.gameObject.SetActive(false);

            return view;
        }

        // ==========================================================
        // LOCALIZATION
        // ==========================================================

        private static void AddLocalizationEntries()
        {
            LocalizationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(
                    LocalizationPath);

            if (catalog == null)
            {
                Debug.LogWarning(
                    $"Localization catalog not found at '{LocalizationPath}'. Cart UI will show missing-key placeholders until the keys are added.");

                return;
            }

            SerializedObject serialized =
                new(catalog);

            SerializedProperty entries =
                serialized.FindProperty(
                    "_entries");

            AddLocalizationEntry(
                entries,
                "phone.cart",
                "Корзина",
                "Cart");

            AddLocalizationEntry(
                entries,
                "phone.cart_empty",
                "Корзина пуста.",
                "Your cart is empty.");

            AddLocalizationEntry(
                entries,
                "phone.cart_total",
                "Итого: {0}",
                "Total: {0}");

            AddLocalizationEntry(
                entries,
                "phone.add_to_cart",
                "В корзину",
                "Add to cart");

            AddLocalizationEntry(
                entries,
                "phone.checkout",
                "Заказать",
                "Place order");

            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(catalog);
        }

        private static void AddLocalizationEntry(
            SerializedProperty entries,
            string key,
            string russian,
            string english)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry =
                    entries.GetArrayElementAtIndex(i);

                SerializedProperty existingKey =
                    entry.FindPropertyRelative(
                        "_key");

                if (existingKey != null &&
                    string.Equals(
                        existingKey.stringValue,
                        key,
                        StringComparison.Ordinal))
                {
                    return;
                }
            }

            int index =
                entries.arraySize;

            entries.InsertArrayElementAtIndex(index);

            SerializedProperty created =
                entries.GetArrayElementAtIndex(index);

            created.FindPropertyRelative("_key").stringValue =
                key;

            created.FindPropertyRelative("_russian").stringValue =
                russian;

            created.FindPropertyRelative("_english").stringValue =
                english;
        }

        // ==========================================================
        // UI HELPERS
        // ==========================================================

        private static Button CreateButton(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            Sprite roundedSprite,
            Color color,
            out TMP_Text label)
        {
            RectTransform rect =
                CreateRect(
                    name,
                    parent);

            Image image =
                rect.gameObject.AddComponent<Image>();

            image.sprite = roundedSprite;
            image.type =
                roundedSprite != null
                    ? Image.Type.Sliced
                    : Image.Type.Simple;

            image.color = color;

            Button button =
                rect.gameObject.AddComponent<Button>();

            button.targetGraphic = image;
            button.transition =
                Selectable.Transition.ColorTint;

            ColorBlock colors =
                button.colors;

            colors.normalColor = color;
            colors.highlightedColor =
                new Color(
                    Mathf.Min(1f, color.r + 0.06f),
                    Mathf.Min(1f, color.g + 0.06f),
                    Mathf.Min(1f, color.b + 0.06f),
                    color.a);

            colors.pressedColor =
                new Color(
                    color.r * 0.82f,
                    color.g * 0.82f,
                    color.b * 0.82f,
                    color.a);

            colors.selectedColor =
                colors.highlightedColor;

            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;

            button.colors = colors;
            button.navigation =
                new Navigation
                {
                    mode = Navigation.Mode.None
                };

            label =
                CreateText(
                    rect,
                    "Label",
                    text,
                    font,
                    14f,
                    Primary);

            Stretch(
                label.rectTransform,
                4f);

            label.alignment =
                TextAlignmentOptions.Center;

            return button;
        }

        private static TMP_Text CreateText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            Color color)
        {
            GameObject go =
                new(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));

            go.layer =
                parent.gameObject.layer;

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.SetParent(
                parent,
                false);

            TextMeshProUGUI tmp =
                go.GetComponent<TextMeshProUGUI>();

            tmp.text = text;
            tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.fontStyle =
                FontStyles.Normal;
            tmp.color = color;
            tmp.alignment =
                TextAlignmentOptions.MidlineLeft;
            tmp.textWrappingMode =
                TextWrappingModes.NoWrap;
            tmp.overflowMode =
                TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            return tmp;
        }

        private static RectTransform CreateRect(
            string name,
            Transform parent)
        {
            GameObject go =
                new(
                    name,
                    typeof(RectTransform));

            go.layer =
                parent.gameObject.layer;

            RectTransform rect =
                go.GetComponent<RectTransform>();

            rect.SetParent(
                parent,
                false);

            return rect;
        }

        private static void SetButtonSize(
            Button button,
            float width,
            float height)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    button.gameObject);

            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
        }

        private static void SetPreferredHeight(
            GameObject target,
            float height)
        {
            LayoutElement layout =
                GetOrAdd<LayoutElement>(
                    target);

            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        private static void Stretch(
            RectTransform rect,
            float inset = 0f)
        {
            rect.anchorMin =
                Vector2.zero;

            rect.anchorMax =
                Vector2.one;

            rect.pivot =
                new Vector2(0.5f, 0.5f);

            rect.offsetMin =
                new Vector2(inset, inset);

            rect.offsetMax =
                new Vector2(-inset, -inset);
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
                new Vector2(left, bottom);

            rect.offsetMax =
                new Vector2(-right, -top);
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

        private static Sprite CardSprite(
            ShopProductCardView card)
        {
            Image image =
                card.GetComponent<Image>();

            return image != null
                ? image.sprite
                : null;
        }

        private static T GetReference<T>(
            SerializedObject serialized,
            string name)
            where T : UnityEngine.Object
        {
            SerializedProperty property =
                serialized.FindProperty(name);

            return property != null
                ? property.objectReferenceValue as T
                : null;
        }

        private static void SetReference(
            SerializedObject serialized,
            string name,
            UnityEngine.Object value)
        {
            SerializedProperty property =
                serialized.FindProperty(name);

            if (property == null)
            {
                throw new InvalidOperationException(
                    $"Serialized field '{name}' was not found on {serialized.targetObject.name}.");
            }

            property.objectReferenceValue =
                value;
        }

        private static PhoneShopView FindShopFromSelection()
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
            string name)
        {
            if (root.name == name)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found =
                    FindDeep(
                        root.GetChild(i),
                        name);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static TMP_Text FindTextDeep(
            Transform root,
            string name)
        {
            Transform found =
                FindDeep(
                    root,
                    name);

            return found != null
                ? found.GetComponent<TMP_Text>()
                : null;
        }

        private static T GetOrAdd<T>(
            GameObject target)
            where T : Component
        {
            T component =
                target.GetComponent<T>();

            return component != null
                ? component
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
                names.Push(current.name);
                current = current.parent;
            }

            if (current != root)
                return null;

            return string.Join(
                "/",
                names);
        }

        private static Color Hex(string value)
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
