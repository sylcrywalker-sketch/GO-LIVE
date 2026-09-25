using System.Reflection;
using GoLive.Items;
using GoLive.Shop;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GoLive.Tests
{
    internal static class ShopTestData
    {
        public const string BudgetGpuItem = "Item_BudgetGPU";
        public const string BananaItem = "BananaDefinition";
        public const string MugItem = "Item_Mug";
        public const string DeliveryPackageItem = "Item_DeliveryPackage";

        // Project definitions whose World Prefabs are physically deliverable runtime items.
        public static ItemDefinition LoadItem(string assetName)
        {
            return AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/Game/Scripts/Items/Config/{assetName}.asset");
        }

        public static ItemDefinition CreateItem(string itemId, ItemCategory category)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            Set(item, "itemId", itemId);
            Set(item, "category", category);
            return item;
        }

        public static ShopProductDefinition CreateProduct(
            string productId,
            long priceCents,
            ItemCategory category,
            ItemDefinition fulfillmentItem,
            int maxPurchases = 0,
            int deliveryDelayMinutes = 150,
            bool available = true,
            bool featured = false)
        {
            ShopProductDefinition product = new();
            Set(product, "productId", productId);
            Set(product, "nameLocalizationKey", $"test.{productId}.name");
            Set(product, "descriptionLocalizationKey", $"test.{productId}.description");
            Set(product, "category", category);
            Set(product, "showInFeatured", featured);
            Set(product, "priceCents", priceCents);
            Set(product, "availability", available ? ShopProductAvailability.Available : ShopProductAvailability.Unavailable);
            Set(product, "maxPurchases", maxPurchases);
            Set(product, "fulfillmentItem", fulfillmentItem);
            Set(product, "deliveryDelayMinutes", deliveryDelayMinutes);
            return product;
        }

        public static ShopCatalogConfig CreateCatalog(params ShopProductDefinition[] products)
        {
            ShopCatalogConfig catalog = ScriptableObject.CreateInstance<ShopCatalogConfig>();
            Set(catalog, "products", products);
            return catalog;
        }

        // The game's only way to buy: one unit into the cart, then the whole cart through the one checkout.
        public static ShopCheckoutResult Buy(ShopBehaviour shop, string productId)
        {
            ShopPurchaseResultCode added = shop.TryAddToCart(productId);
            Assert.That(added, Is.EqualTo(ShopPurchaseResultCode.Success), $"{productId} goes into the cart");

            return shop.TryCheckout();
        }

        // A single-unit order for tests that only need one placed order of a product.
        public static ShopOrder BuyOne(ShopBehaviour shop, string productId)
        {
            ShopCheckoutResult result = Buy(shop, productId);
            Assert.That(result.Succeeded, Is.True, $"{productId}: {result.Code}");
            Assert.That(result.Orders, Has.Count.EqualTo(1));
            return result.Orders[0];
        }

        public static void Set(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
