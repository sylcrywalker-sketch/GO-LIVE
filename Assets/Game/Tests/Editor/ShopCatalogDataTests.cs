using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Items;
using GoLive.Shop;
using NUnit.Framework;
using UnityEditor;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed class ShopCatalogDataTests
    {
        private const string CatalogPath = "Assets/Game/Scripts/Shop/DefaultShopCatalog.asset";
        private const string LocalizationPath = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";

        // Keys the Phone Shop screens resolve at runtime (tabs are configured on PhoneShopView in the scene).
        private static readonly string[] ShopUiKeys =
        {
            "phone.shop", "phone.product_details", "phone.orders", "phone.orders_empty", "phone.catalog_empty",
            "phone.back", "phone.buy", "phone.ordered", "phone.purchased", "phone.not_enough_money", "phone.unavailable",
            "phone.order_placed", "phone.order_delivered", "phone.order_eta", "phone.order_eta_day", "phone.ordered_with_eta",
            "shop.tab.featured", "shop.tab.food", "shop.tab.household", "shop.tab.electronics"
        };

        private readonly List<Object> _created = new();

        private static ShopCatalogConfig Catalog => AssetDatabase.LoadAssetAtPath<ShopCatalogConfig>(CatalogPath);

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);

            _created.Clear();
        }

        [Test]
        public void DefaultCatalogIsValid()
        {
            Assert.That(Catalog, Is.Not.Null);
            Assert.That(Catalog.Validate(out string error), Is.True, error);
        }

        [Test]
        public void BudgetGpuKeepsItsFirstPurchaseTerms()
        {
            Assert.That(Catalog.TryGetProduct("budget-gpu", out ShopProductDefinition gpu), Is.True);
            Assert.That(gpu.PriceCents, Is.EqualTo(1500));
            Assert.That(gpu.IsAvailable, Is.True);
            Assert.That(gpu.MaxPurchases, Is.EqualTo(1));
            Assert.That(gpu.DeliveryDelayMinutes, Is.EqualTo(150));
            Assert.That(gpu.Category, Is.EqualTo(ItemCategory.Electronics));
            Assert.That(gpu.ShowInFeatured, Is.True);
            Assert.That(gpu.FulfillmentItem.ItemId, Is.EqualTo("budget-gpu"));
        }

        [Test]
        public void EveryStorefrontCategoryIsStockedAndFeaturedMixesThem()
        {
            IReadOnlyList<ShopProductDefinition> products = Catalog.Products;

            foreach (ItemCategory category in Enum.GetValues(typeof(ItemCategory)))
            {
                Assert.That(products.Any(product => product.Category == category), Is.True, $"{category} shelf is empty");
                Assert.That(products.Any(product => product.Category == category && product.IsAvailable), Is.True, $"{category} shelf has nothing to buy");
            }

            ItemCategory[] featured = products.Where(product => product.ShowInFeatured).Select(product => product.Category).Distinct().ToArray();
            Assert.That(featured, Is.EquivalentTo(Enum.GetValues(typeof(ItemCategory))));
        }

        [Test]
        public void AvailableProductsHaveAPhysicalItemToDeliver()
        {
            foreach (ShopProductDefinition product in Catalog.Products.Where(product => product.IsAvailable))
            {
                Assert.That(product.FulfillmentItem, Is.Not.Null, product.ProductId);
                Assert.That(product.FulfillmentItem.WorldPrefab, Is.Not.Null, product.ProductId);
                Assert.That(product.DeliveryDelayMinutes, Is.GreaterThan(0), product.ProductId);
            }
        }

        [Test]
        public void EveryProductHasRussianAndEnglishText()
        {
            Dictionary<string, (string Russian, string English)> texts = LoadTexts();

            foreach (ShopProductDefinition product in Catalog.Products)
            {
                AssertTranslated(texts, product.NameLocalizationKey);
                AssertTranslated(texts, product.DescriptionLocalizationKey);
                AssertTranslated(texts, product.CategoryLocalizationKey);
            }
        }

        [Test]
        public void ShopScreensHaveRussianAndEnglishText()
        {
            Dictionary<string, (string Russian, string English)> texts = LoadTexts();

            foreach (string key in ShopUiKeys)
                AssertTranslated(texts, key);
        }

        [Test]
        public void AvailableProductWithoutImageIsValid()
        {
            ItemDefinition item = Track(ShopTestData.CreateItem("test-item", ItemCategory.Household));
            ShopCatalogConfig catalog = Track(ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct("test-item", 100, ItemCategory.Household, item)));

            Assert.That(catalog.Validate(out string error), Is.True, error);
            Assert.That(catalog.Products[0].Image, Is.Null);
        }

        [Test]
        public void ProductCategoryMustMatchItsFulfillmentItem()
        {
            ItemDefinition item = Track(ShopTestData.CreateItem("test-item", ItemCategory.Electronics));
            ShopCatalogConfig catalog = Track(ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct("test-item", 100, ItemCategory.Food, item)));

            Assert.That(catalog.Validate(out string error), Is.False);
            Assert.That(error, Does.Contain("test-item"));
        }

        [Test]
        public void UnavailableFutureEntryNeedsNoItemButAvailableProductDoes()
        {
            ShopCatalogConfig future = Track(ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct("future-item", 100, ItemCategory.Food, null, available: false, deliveryDelayMinutes: 0)));

            ShopCatalogConfig broken = Track(ShopTestData.CreateCatalog(
                ShopTestData.CreateProduct("broken-item", 100, ItemCategory.Food, null)));

            Assert.That(future.Validate(out string futureError), Is.True, futureError);
            Assert.That(broken.Validate(out _), Is.False);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static void AssertTranslated(Dictionary<string, (string Russian, string English)> texts, string key)
        {
            Assert.That(texts.TryGetValue(key, out (string Russian, string English) text), Is.True, $"Missing localization key '{key}'.");
            Assert.That(text.Russian, Is.Not.Null.And.Not.Empty, $"'{key}' has no Russian text.");
            Assert.That(text.English, Is.Not.Null.And.Not.Empty, $"'{key}' has no English text.");
        }

        private static Dictionary<string, (string Russian, string English)> LoadTexts()
        {
            SerializedObject catalog = new(AssetDatabase.LoadMainAssetAtPath(LocalizationPath));
            SerializedProperty entries = catalog.FindProperty("_entries");
            Dictionary<string, (string, string)> texts = new(StringComparer.Ordinal);

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);

                texts[entry.FindPropertyRelative("_key").stringValue] =
                    (entry.FindPropertyRelative("_russian").stringValue.Trim(),
                     entry.FindPropertyRelative("_english").stringValue.Trim());
            }

            return texts;
        }
    }
}
