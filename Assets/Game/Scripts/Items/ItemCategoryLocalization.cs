using System;

namespace GoLive.Items
{
    // The one mapping from a broad item category to the localization key of its name ("Еда", "Электроника", "Быт"),
    // shared by every screen that names a category: Inventory rows and the Phone Shop.
    public static class ItemCategoryLocalization
    {
        public const string FoodKey = "category.food";
        public const string ElectronicsKey = "category.electronics";
        public const string HouseholdKey = "category.household";

        public static string GetKey(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Food => FoodKey,
                ItemCategory.Electronics => ElectronicsKey,
                ItemCategory.Household => HouseholdKey,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown item category.")
            };
        }
    }
}
