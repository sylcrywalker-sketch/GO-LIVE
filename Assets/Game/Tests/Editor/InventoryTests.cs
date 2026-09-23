using GoLive.Items;
using GoLive.Localization;
using NUnit.Framework;
using UnityEditor;

namespace GoLive.Tests
{
    public sealed class InventoryTests
    {
        [Test]
        public void ReplaceKeepsTheSlotOrderAndChangesOnce()
        {
            GoLive.Inventory.Inventory inventory = new(3);
            ItemInstance first = Stored("first");
            ItemInstance second = Stored("second");
            ItemInstance third = Stored("third");
            ItemInstance replacement = Stored("replacement");

            Assert.That(inventory.TryAdd(first), Is.True);
            Assert.That(inventory.TryAdd(second), Is.True);
            Assert.That(inventory.TryAdd(third), Is.True);

            int changes = 0;
            inventory.Changed += () => changes++;

            Assert.That(inventory.TryReplace("second", replacement), Is.True);
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(inventory.Items, Is.EqualTo(new[] { first, replacement, third }));
            Assert.That(inventory.Contains("second"), Is.False);
            Assert.That(inventory.Contains("replacement"), Is.True);
            Assert.That(inventory.Count, Is.EqualTo(3));
        }

        [Test]
        public void ReplaceRejectsAnythingThatWouldBreakTheInventory()
        {
            GoLive.Inventory.Inventory inventory = new(2);
            ItemInstance first = Stored("first");
            ItemInstance second = Stored("second");

            inventory.TryAdd(first);
            inventory.TryAdd(second);

            int changes = 0;
            inventory.Changed += () => changes++;

            Assert.That(inventory.TryReplace("first", second), Is.False, "already stored");
            Assert.That(inventory.TryReplace("first", new ItemInstance("carried", "banana", ItemLocation.Carried)), Is.False, "not moved to Inventory");
            Assert.That(inventory.TryReplace("missing", Stored("new")), Is.False, "nothing to replace");
            Assert.That(inventory.TryReplace(null, Stored("new")), Is.False);
            Assert.That(inventory.TryReplace("first", null), Is.False);

            Assert.That(changes, Is.Zero);
            Assert.That(inventory.Items, Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void EveryItemNameKeyExistsInTheCatalog()
        {
            LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>("Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset");
            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/Game" });

            Assert.That(guids, Is.Not.Empty);

            foreach (string guid in guids)
            {
                ItemDefinition definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (string.IsNullOrWhiteSpace(definition.NameLocalizationKey))
                    continue;

                Assert.That(catalog.TryGetText(definition.NameLocalizationKey, GameLanguage.Russian, out _), Is.True, $"{definition.name}: Russian name");
                Assert.That(catalog.TryGetText(definition.NameLocalizationKey, GameLanguage.English, out _), Is.True, $"{definition.name}: English name");
            }
        }

        private static ItemInstance Stored(string id)
        {
            return new ItemInstance(id, "banana", ItemLocation.Inventory);
        }
    }
}
