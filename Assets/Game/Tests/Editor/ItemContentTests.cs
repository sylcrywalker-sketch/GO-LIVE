using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoLive.Editor.Items;
using GoLive.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Content contract for pickup items: every ItemDefinition describes a physical item the player can pick up,
    // so each one needs a reusable gameplay prefab and a real Inventory/hands icon. The generic cube glyph is only
    // a defensive fallback, never production content.
    public sealed class ItemContentTests
    {
        private const string TemporaryIconFolder = "Assets/Game/UI/Generated/_IconGeneratorTest";
        private const string GameScene = "Assets/Game/Scenes/GL.unity";
        private const string MugPrefab = "Assets/Game/Prefab/Items/Item_Mug.prefab";

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
                Object.DestroyImmediate(created);

            _created.Clear();

            if (AssetDatabase.IsValidFolder(TemporaryIconFolder))
                AssetDatabase.DeleteAsset(TemporaryIconFolder);
        }

        [Test]
        public void EveryPickupItemDefinitionHasAReusableGameplayPrefab()
        {
            List<ItemDefinition> definitions = ItemIconGenerator.FindItemDefinitions();
            Assert.That(definitions, Is.Not.Empty);

            foreach (ItemDefinition definition in definitions)
            {
                string label = $"{definition.name} ({definition.ItemId})";

                Assert.That(ItemDefinition.IsValidItemId(definition.ItemId), Is.True, $"{label}: invalid Item ID");
                Assert.That(definition.TryGetRuntimePrefab(out WorldItem prefab), Is.True,
                    $"{label}: World Prefab must be a gameplay wrapper with a WorldItem of this definition, no scene ID and a Collider");
                Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null, $"{label}: World Prefab needs a Rigidbody");
            }

            Assert.That(definitions.Select(definition => definition.ItemId).Distinct().Count(), Is.EqualTo(definitions.Count), "Item IDs must be unique");
        }

        [Test]
        public void EveryPickupItemDefinitionResolvesARealInventoryIcon()
        {
            foreach (ItemDefinition definition in ItemIconGenerator.FindItemDefinitions())
            {
                Sprite icon = definition.InventoryIcon;

                Assert.That(icon, Is.Not.Null,
                    $"{definition.name} ({definition.ItemId}) has no Inventory icon. Run GO LIVE > Items > Generate Missing Item Icons.");

                string path = AssetDatabase.GetAssetPath(icon);
                Assert.That(path, Is.Not.Empty, $"{definition.name}: the icon must be a project asset");

                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), $"{definition.name}: {path} is not a Sprite");
                Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True, $"{definition.name}: {path} has no transparent background");
            }
        }

        [Test]
        public void GeneratedIconsLiveInOneFilePerItemId()
        {
            foreach (ItemDefinition definition in ItemIconGenerator.FindItemDefinitions())
            {
                Object generated = new SerializedObject(definition).FindProperty("generatedIcon").objectReferenceValue;

                if (generated != null)
                    Assert.That(AssetDatabase.GetAssetPath(generated), Is.EqualTo($"{ItemIconGenerator.OutputFolder}/{definition.ItemId}.png"), definition.name);
            }
        }

        // No pickup item escapes the contract: every WorldItem in content prefabs and every definition the game
        // scene references is one of the validated production definitions.
        [Test]
        public void EveryWorldItemInContentUsesAValidatedDefinition()
        {
            HashSet<ItemDefinition> validated = new(ItemIconGenerator.FindItemDefinitions());
            string worldItemScript = AssetDatabase.FindAssets($"{nameof(WorldItem)} t:MonoScript")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Single(path => AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass() == typeof(WorldItem));

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ItemIconGenerator.ContentFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Only load prefabs that can contain a WorldItem (some unrelated art variants cannot be loaded at all).
                if (!AssetDatabase.GetDependencies(path, true).Contains(worldItemScript))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                foreach (WorldItem item in prefab.GetComponentsInChildren<WorldItem>(true))
                    Assert.That(item.Definition != null && validated.Contains(item.Definition), Is.True, $"{path}: {item.name} uses an unvalidated ItemDefinition");
            }

            foreach (string dependency in AssetDatabase.GetDependencies(GameScene, true))
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(dependency) != typeof(ItemDefinition))
                    continue;

                Assert.That(validated.Contains(AssetDatabase.LoadAssetAtPath<ItemDefinition>(dependency)), Is.True, $"{GameScene} uses {dependency}");
            }
        }

        [Test]
        public void IconRenderIsATransparentFramedCutoutOfTheRealModel()
        {
            Texture2D icon = Track(ItemIconGenerator.RenderIcon(AssetDatabase.LoadAssetAtPath<GameObject>(MugPrefab)));
            int size = ItemIconGenerator.OutputSize;
            Color32[] pixels = icon.GetPixels32();

            Assert.That(icon.width, Is.EqualTo(size));
            Assert.That(icon.height, Is.EqualTo(size));
            Assert.That(new[] { pixels[0].a, pixels[size - 1].a, pixels[size * (size - 1)].a, pixels[size * size - 1].a }, Is.All.EqualTo(0), "transparent background");

            int minX = size, minY = size, maxX = -1, maxY = -1, opaque = 0;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (pixels[y * size + x].a < 128)
                        continue;

                    opaque++;
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            Assert.That(opaque, Is.GreaterThan(size * size / 10), "the model is rendered, not a blank or tiny sprite");
            Assert.That(Mathf.Max(maxX - minX, maxY - minY) + 1, Is.GreaterThan(size * 0.8f), "the model fills most of the icon");
            Assert.That(minX > 0 && minY > 0 && maxX < size - 1 && maxY < size - 1, Is.True, "nothing is clipped at the icon edge");
        }

        [Test]
        public void RegeneratingReplacesTheSameIconFileAndLeavesAuthoredOverridesAlone()
        {
            ItemDefinition item = Track(ShopTestData.CreateItem("icon-test-item", ItemCategory.Household));
            ShopTestData.Set(item, "worldPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(MugPrefab));

            Assert.That(ItemIconGenerator.GenerateIcons(new[] { item }, regenerateExisting: false, outputFolder: TemporaryIconFolder).Generated, Has.Count.EqualTo(1));
            Sprite first = item.InventoryIcon;
            Assert.That(AssetDatabase.GetAssetPath(first), Is.EqualTo($"{TemporaryIconFolder}/icon-test-item.png"));

            ItemIconReport skipped = ItemIconGenerator.GenerateIcons(new[] { item }, regenerateExisting: false, outputFolder: TemporaryIconFolder);
            Assert.That(skipped.Generated, Is.Empty, "Generate Missing leaves an existing icon alone");
            Assert.That(skipped.AlreadyGenerated, Has.Count.EqualTo(1));

            Assert.That(ItemIconGenerator.GenerateIcons(new[] { item }, regenerateExisting: true, outputFolder: TemporaryIconFolder).Generated, Has.Count.EqualTo(1));
            Assert.That(Directory.GetFiles(TemporaryIconFolder, "*.png"), Has.Length.EqualTo(1), "regenerating overwrites, never duplicates");
            Assert.That(AssetDatabase.GetAssetPath(item.InventoryIcon), Is.EqualTo(AssetDatabase.GetAssetPath(first)));

            Sprite authored = Sprite.Create(Track(new Texture2D(4, 4)), new Rect(0f, 0f, 4f, 4f), Vector2.one * 0.5f);
            Track(authored);
            ShopTestData.Set(item, "iconOverride", authored);

            ItemIconReport overridden = ItemIconGenerator.GenerateIcons(new[] { item }, regenerateExisting: true, outputFolder: TemporaryIconFolder);
            Assert.That(overridden.Generated, Is.Empty);
            Assert.That(overridden.Overridden, Has.Count.EqualTo(1));
            Assert.That(item.InventoryIcon, Is.SameAs(authored), "an authored override always wins");
        }

        [Test]
        public void ADefinitionWithoutAWorldPrefabIsReportedByName()
        {
            ItemDefinition item = Track(ShopTestData.CreateItem("icon-missing-prefab", ItemCategory.Household));
            item.name = "Missing prefab item";

            ItemIconReport report = ItemIconGenerator.GenerateIcons(new[] { item }, regenerateExisting: true, outputFolder: TemporaryIconFolder);

            Assert.That(report.Generated, Is.Empty);
            Assert.That(report.Failures.Single(), Does.Contain("Missing prefab item").And.Contain("no World Prefab"));
            Assert.That(item.InventoryIcon, Is.Null);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
