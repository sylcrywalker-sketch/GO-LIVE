using System;
using System.Collections.Generic;
using GoLive.Items;
using UnityEngine;

namespace GoLive.Shop
{
    public enum ShopProductAvailability
    {
        Available = 0,
        Unavailable = 1
    }

    [Serializable]
    public sealed class ShopProductDefinition
    {
        [SerializeField] private string productId;
        [SerializeField] private string nameLocalizationKey;
        [SerializeField] private string descriptionLocalizationKey;
        [SerializeField] private string categoryLocalizationKey;
        [SerializeField] private ItemCategory category;
        [SerializeField] private bool showInFeatured;
        [SerializeField, Min(1)] private long priceCents = 100;
        [SerializeField] private ShopProductAvailability availability = ShopProductAvailability.Available;
        [SerializeField, Min(0)] private int maxPurchases;
        [SerializeField] private Sprite image;
        [SerializeField] private ItemDefinition fulfillmentItem;
        [SerializeField, Min(0)] private int deliveryDelayMinutes = 150;

        public string ProductId => productId;
        public string NameLocalizationKey => nameLocalizationKey;
        public string DescriptionLocalizationKey => descriptionLocalizationKey;
        public string CategoryLocalizationKey => categoryLocalizationKey;
        public ItemCategory Category => category;
        public bool ShowInFeatured => showInFeatured;
        public long PriceCents => priceCents;
        public ShopProductAvailability Availability => availability;
        public int MaxPurchases => maxPurchases;
        public Sprite Image => image;
        public ItemDefinition FulfillmentItem => fulfillmentItem;
        public int DeliveryDelayMinutes => deliveryDelayMinutes;

        public bool IsAvailable => availability == ShopProductAvailability.Available;
        public bool HasPurchaseLimit => maxPurchases > 0;

        internal ShopPurchaseOffer CreatePurchaseOffer()
        {
            return new ShopPurchaseOffer(
                productId,
                priceCents,
                maxPurchases,
                deliveryDelayMinutes,
                IsAvailable);
        }

        internal bool Validate(out string error)
        {
            if (!ShopId.IsValid(productId))
            {
                error = $"Product has invalid Product ID '{productId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(nameLocalizationKey))
            {
                error = $"Product '{productId}' has no name localization key.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(descriptionLocalizationKey))
            {
                error = $"Product '{productId}' has no description localization key.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(categoryLocalizationKey))
            {
                error = $"Product '{productId}' has no category localization key.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ItemCategory), category))
            {
                error = $"Product '{productId}' has an invalid category.";
                return false;
            }

            if (priceCents <= 0)
            {
                error = $"Product '{productId}' must have a positive price.";
                return false;
            }

            if (!Enum.IsDefined(typeof(ShopProductAvailability), availability))
            {
                error = $"Product '{productId}' has an invalid availability state.";
                return false;
            }

            if (maxPurchases < 0)
            {
                error = $"Product '{productId}' has an invalid purchase limit.";
                return false;
            }

            if (IsAvailable && fulfillmentItem == null)
            {
                error = $"Available product '{productId}' has no fulfillment Item Definition.";
                return false;
            }

            if (fulfillmentItem != null && !ItemDefinition.IsValidItemId(fulfillmentItem.ItemId))
            {
                error = $"Product '{productId}' references an invalid fulfillment Item Definition.";
                return false;
            }

            if (fulfillmentItem != null && fulfillmentItem.Category != category)
            {
                error = $"Product '{productId}' is listed as {category} but delivers a {fulfillmentItem.Category} item.";
                return false;
            }

            if (IsAvailable && !fulfillmentItem.TryGetRuntimePrefab(out _))
            {
                error = $"Available product '{productId}' cannot be delivered: the World Prefab of '{fulfillmentItem.ItemId}' needs a root WorldItem for that Item Definition, a Collider and no scene instance ID.";
                return false;
            }

            if (IsAvailable && deliveryDelayMinutes <= 0)
            {
                error = $"Available product '{productId}' requires a positive delivery delay.";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(fileName = "ShopCatalog", menuName = "GO! LIVE/Shop/Shop Catalog")]
    public sealed class ShopCatalogConfig : ScriptableObject
    {
        [SerializeField] private ShopProductDefinition[] products = Array.Empty<ShopProductDefinition>();

        public IReadOnlyList<ShopProductDefinition> Products => products;

        private Dictionary<string, ShopProductDefinition> _lookup;

        public bool Validate(out string error)
        {
            if (products == null)
            {
                error = "Shop catalog product array is missing.";
                return false;
            }

            HashSet<string> productIds = new(StringComparer.Ordinal);

            for (int i = 0; i < products.Length; i++)
            {
                ShopProductDefinition product = products[i];

                if (product == null)
                {
                    error = $"Shop product at index {i} is null.";
                    return false;
                }

                if (!product.Validate(out error))
                    return false;

                if (!productIds.Add(product.ProductId))
                {
                    error = $"Duplicate Shop Product ID '{product.ProductId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public bool TryGetProduct(string productId, out ShopProductDefinition product)
        {
            product = null;

            if (!ShopId.IsValid(productId))
                return false;

            if (!EnsureLookup())
                return false;

            return _lookup.TryGetValue(productId, out product);
        }

        public static bool IsValidProductId(string value)
        {
            return ShopId.IsValid(value);
        }

        private bool EnsureLookup()
        {
            if (_lookup != null)
                return true;

            if (!Validate(out _))
                return false;

            Dictionary<string, ShopProductDefinition> lookup =
                new(products.Length, StringComparer.Ordinal);

            for (int i = 0; i < products.Length; i++)
                lookup.Add(products[i].ProductId, products[i]);

            _lookup = lookup;
            return true;
        }

        private void OnEnable()
        {
            _lookup = null;
        }

        private void OnValidate()
        {
            _lookup = null;
        }
    }
}