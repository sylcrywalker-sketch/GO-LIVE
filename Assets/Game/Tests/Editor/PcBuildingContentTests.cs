using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GoLive.Editor.Items;
using GoLive.Items;
using GoLive.Localization;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoLive.Tests
{
    // Authored PC content: component data, the Student PC slots, the corrected Budget GPU and the GL desk wiring.
    public sealed class PcBuildingContentTests
    {
        private const string StudentPc = "Assets/Game/Prefab/PC/StudentPC.prefab";
        private const string GpuWrapper = "Assets/Game/Prefab/Items/Item_BudgetGPU.prefab";
        private const string GpuArt = "Assets/Game/Prefab/1_Main Room/PC/GPU/PC_GPU_LOD0.prefab";
        private const string GpuMaterial = "Assets/Game/Props/1_Main Room/PC/GPU/M_PC_GPU.mat";
        private const string GameScene = "Assets/Game/Scenes/GL.unity";
        private const string Catalog = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";

        private Scene _preview;

        [TearDown]
        public void TearDown()
        {
            if (_preview.IsValid())
                EditorSceneManager.ClosePreviewScene(_preview);
        }

        [Test]
        public void EveryPcComponentSpecIsValidAndBelongsToAnItemDefinition()
        {
            List<ItemDefinition> definitions = ItemIconGenerator.FindItemDefinitions();
            string[] specs = AssetDatabase.FindAssets($"t:{nameof(PcComponentSpec)}", new[] { "Assets/Game" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();

            Assert.That(specs, Is.Not.Empty);

            foreach (string path in specs)
            {
                PcComponentSpec spec = AssetDatabase.LoadAssetAtPath<PcComponentSpec>(path);
                Assert.That(spec.IsValid, Is.True, $"{path} is not valid PC component data");
                Assert.That(definitions.Any(definition => definition.PcComponent == spec), Is.True, $"{path} is not used by any ItemDefinition");
            }

            foreach (ItemDefinition definition in definitions.Where(definition => definition.PcComponent != null))
                Assert.That(definition.PcComponent.IsValid, Is.True, $"{definition.name} points at invalid PC component data");
        }

        [Test]
        public void OnlyPcHardwareHasPcComponentData()
        {
            Assert.That(ShopTestData.LoadItem(ShopTestData.BananaItem).PcComponent, Is.Null);
            Assert.That(ShopTestData.LoadItem(ShopTestData.MugItem).PcComponent, Is.Null);
            Assert.That(ShopTestData.LoadItem(ShopTestData.DeliveryPackageItem).PcComponent, Is.Null);
        }

        [Test]
        public void BudgetGpuIsAPcieGraphicsCardWithItsOriginalIdentity()
        {
            ItemDefinition gpu = ShopTestData.LoadItem(ShopTestData.BudgetGpuItem);

            Assert.That(gpu.ItemId, Is.EqualTo("budget-gpu"));
            Assert.That(gpu.PcComponent, Is.Not.Null);
            Assert.That(gpu.PcComponent.ComponentType, Is.EqualTo(PcComponentType.Gpu));
            Assert.That(gpu.PcComponent.Connector, Is.EqualTo(PcConnector.PcieX16));
            Assert.That(gpu.TryGetRuntimePrefab(out WorldItem prefab), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(GpuWrapper));
        }

        [Test]
        public void BudgetGpuIconIsTheGeneratedRenderOfTheCorrectedModel()
        {
            Sprite icon = ShopTestData.LoadItem(ShopTestData.BudgetGpuItem).InventoryIcon;

            Assert.That(icon, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(icon), Is.EqualTo($"{ItemIconGenerator.OutputFolder}/budget-gpu.png"));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(icon))).DoesSourceTextureHaveAlpha(), Is.True);
        }

        [Test]
        public void BudgetGpuWrapperIsACleanGameplayRootOverTheArtPrefab()
        {
            GameObject wrapper = AssetDatabase.LoadAssetAtPath<GameObject>(GpuWrapper);
            GameObject art = AssetDatabase.LoadAssetAtPath<GameObject>(GpuArt);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GpuMaterial);

            Assert.That(wrapper.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(wrapper.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(wrapper.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(wrapper.GetComponent<WorldItem>().IsRuntime, Is.True);
            Assert.That(wrapper.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(wrapper.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(wrapper.transform.Find("Visual").gameObject), Is.SameAs(art), "the art stays a nested, untouched prefab");

            Assert.That(art.transform.localPosition, Is.EqualTo(Vector3.zero), "no baked scene position in the art prefab");
            Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_OcclusionMap"), Is.Not.Null);
            Assert.That(material.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(art.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials), Is.All.SameAs(material));

            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(art)));
            Assert.That(importer.bakeAxisConversion, Is.True, "imported in the author's fit frame");
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
        }

        [Test]
        public void StudentPcSlotsAreUniqueAndCompletelyAuthored()
        {
            GameObject pc = AssetDatabase.LoadAssetAtPath<GameObject>(StudentPc);
            PcAssemblyBehaviour assembly = pc.GetComponent<PcAssemblyBehaviour>();
            PcComponentSlot[] authored = pc.GetComponentsInChildren<PcComponentSlot>(true);

            Assert.That(assembly, Is.Not.Null);
            Assert.That(pc.GetComponent<Collider>(), Is.Not.Null, "the case answers the B ray");
            Assert.That(pc.GetComponent<PcWorkbenchBehaviour>(), Is.Null, "the Workbench is scene wiring, not part of the PC prefab");
            Assert.That(assembly.Slots, Is.EquivalentTo(authored), "every authored slot is registered");
            Assert.That(authored.Select(slot => slot.SlotId).Distinct().Count(), Is.EqualTo(authored.Length), "slot IDs are unique");

            foreach (PcComponentSlot slot in authored)
                Assert.That(slot.IsConfigured(out string error), Is.True, $"{slot.name}: {error}");

            PcComponentSlot gpu = authored.Single(slot => slot.SlotId == "gpu-0");
            Assert.That(gpu.ComponentType, Is.EqualTo(PcComponentType.Gpu));
            Assert.That(gpu.Connector, Is.EqualTo(PcConnector.PcieX16));
            Assert.That(gpu.InstallAnchor.parent, Is.SameAs(gpu.transform));
            Assert.That(gpu.InstallAnchor.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(gpu.InstallAnchor.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(gpu.TechnicalLabel, Is.EqualTo("PCIe x16"));
        }

        // The gameplay root (collider, record, place in the world) stays; everything the player sees of the PC sits under
        // one presentation root that PC Build Mode brings to the player: case, side panel, internals, slots, view anchor.
        [Test]
        public void StudentPcPresentsEverythingVisibleThroughOnePresentationRoot()
        {
            GameObject pc = AssetDatabase.LoadAssetAtPath<GameObject>(StudentPc);
            PcBuildPresentation presentation = pc.GetComponent<PcBuildPresentation>();

            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.IsConfigured(out string error), Is.True, error);
            Transform root = presentation.PresentationRoot;
            Assert.That(root.name, Is.EqualTo("BuildPresentationRoot"));
            Assert.That(root.parent, Is.SameAs(pc.transform));
            Assert.That(root.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(root.localScale, Is.EqualTo(Vector3.one), "moves and turns, never stretches");
            Assert.That(presentation.SidePanel.name, Is.EqualTo("SidePanel_Removable_LOD0"));
            Assert.That(presentation.ViewAnchor.name, Is.EqualTo("BuildViewAnchor"));

            Assert.That(pc.GetComponent<Collider>(), Is.Not.Null, "the B ray still hits the gameplay root");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty, "no collider travels with the presentation");
            Assert.That(pc.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.transform.IsChildOf(root)), Is.True, "every visible part travels");
            Assert.That(pc.GetComponent<PcAssemblyBehaviour>().Slots.All(slot => slot.transform.IsChildOf(root)), Is.True, "slots, and what is installed on them, travel");

            // The view anchor looks at the case from its open side (-X), slightly from above, without roll.
            Transform anchor = presentation.ViewAnchor;
            Vector3 centre = root.InverseTransformPoint(pc.transform.TransformPoint(pc.GetComponent<BoxCollider>().center));
            Vector3 toCase = Quaternion.Inverse(anchor.localRotation) * (centre - anchor.localPosition);
            Vector3 euler = anchor.localEulerAngles;
            Assert.That(toCase.z, Is.GreaterThan(0.3f), "the case is in front of the eye");
            Assert.That(anchor.localPosition.x, Is.LessThan(-0.2f), "the eye is on the open side");
            Assert.That(euler.x, Is.InRange(10f, 30f), "a slight downward look");
            Assert.That(Mathf.DeltaAngle(euler.z, 0f), Is.EqualTo(0f).Within(0.01f), "no roll");
        }

        // Every key the build mode shows exists in both languages.
        [Test]
        public void EveryPcBuildModeTextIsLocalized()
        {
            LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(Catalog);
            HashSet<string> keys = new();

            foreach (System.Type type in new[] { typeof(PcWorkbenchBehaviour), typeof(PcBuildInventoryView) })
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic).Where(field => field.IsLiteral && field.Name.EndsWith("Key")))
                    keys.Add((string)field.GetRawConstantValue());
            }

            foreach (PcSlotCheck check in System.Enum.GetValues(typeof(PcSlotCheck)).Cast<PcSlotCheck>().Where(check => check != PcSlotCheck.Allowed))
                keys.Add(check.MessageKey());

            foreach (PcComponentSlot slot in AssetDatabase.LoadAssetAtPath<GameObject>(StudentPc).GetComponentsInChildren<PcComponentSlot>(true))
            {
                keys.Add(slot.NameLocalizationKey);
                keys.Add(slot.ComponentType.NameKey());
                keys.Add(slot.ComponentType.InstallPromptKey());
                keys.Add(slot.ComponentType.RemovePromptKey());
            }

            foreach (string path in AssetDatabase.FindAssets($"t:{nameof(PcComponentSpec)}", new[] { "Assets/Game" }).Select(AssetDatabase.GUIDToAssetPath))
                keys.Add(AssetDatabase.LoadAssetAtPath<PcComponentSpec>(path).ComponentType.NameKey());

            PcAssembly empty = new(new[] { new PcSlotSpec("gpu-0", PcComponentType.Gpu, PcConnector.PcieX16) });
            foreach (PcDiagnostic diagnostic in PcCapabilities.Evaluate(empty).Diagnostics)
            {
                keys.Add(diagnostic.TitleKey);
                keys.Add(diagnostic.DetailKey);
            }

            string[] missing = keys.Where(key => !catalog.TryGetText(key, GameLanguage.Russian, out _) || !catalog.TryGetText(key, GameLanguage.English, out _)).OrderBy(key => key).ToArray();
            Assert.That(missing, Is.Empty);
        }

        // The installed card sits inside the real case: clear of the side panel and the drive, bracket inside the rear.
        [Test]
        public void InstalledBudgetGpuFitsTheStudentCase()
        {
            _preview = EditorSceneManager.NewPreviewScene();
            GameObject pc = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StudentPc), _preview);
            pc.GetComponent<PcAssemblyBehaviour>().TryGetSlot("gpu-0", out PcComponentSlot slot);
            Assert.That(slot, Is.Not.Null);

            GameObject gpu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GpuWrapper), _preview);
            gpu.transform.SetParent(slot.InstallAnchor, false);

            Bounds card = BoundsOf(gpu);
            Bounds shell = BoundsOf(pc.transform.Find("BuildPresentationRoot/Case").gameObject);
            Bounds panel = Named(pc, "SidePanel_Removable_LOD0").GetComponent<Renderer>().bounds;
            Bounds drive = BoundsOf(pc.transform.Find("BuildPresentationRoot/StaticParts/HDD").gameObject);

            Assert.That(card.min.x - panel.max.x, Is.GreaterThan(0.001f), "clear of the side panel");
            Assert.That(card.min.y - drive.max.y, Is.GreaterThan(0.001f), "clear of the hard drive below");
            Assert.That(shell.max.z - card.max.z, Is.GreaterThanOrEqualTo(0f), "the bracket stays inside the rear wall");
            Assert.That(card.min.x, Is.GreaterThan(shell.min.x));
            Assert.That(card.min.y, Is.GreaterThan(shell.min.y));
            Assert.That(card.min.z, Is.GreaterThan(shell.min.z));
            Assert.That(card.size.y, Is.LessThan(Mathf.Min(card.size.x, card.size.z)), "the board stands off the vertical motherboard, so the card lies flat");
            Assert.That(slot.TargetBounds.Contains(slot.transform.InverseTransformPoint(card.center)), Is.True, "the pointer volume covers the card");
        }

        [Test]
        public void GlDeskUsesTheStudentPcWithAWiredWorkbench()
        {
            string scene = File.ReadAllText(GameScene);
            string pcGuid = AssetDatabase.AssetPathToGUID(StudentPc);
            string workbenchGuid = AssetDatabase.FindAssets($"{nameof(PcWorkbenchBehaviour)} t:MonoScript")
                .Single(guid => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid)).GetClass() == typeof(PcWorkbenchBehaviour));

            Assert.That(scene, Does.Contain($"m_SourcePrefab: {{fileID: 100100000, guid: {pcGuid}, type: 3}}"));
            Assert.That(scene, Does.Contain($"m_Script: {{fileID: 11500000, guid: {workbenchGuid}, type: 3}}"));
            Assert.That(scene, Does.Not.Contain("value: SM_PCCase_001"), "the old decorative case is gone");
            Assert.That(scene, Does.Not.Contain("value: SM_Motherboard_001"), "the loose decorative motherboard is gone");
            Assert.That(scene, Does.Not.Contain("m_Name: PC Workbench Stand"), "the build view is the PC's own anchor, not a scene stand point");

            // Installed parts inherit the PC's scale: only a uniform scale keeps them the same shape as in the hands.
            string instance = Regex.Split(scene.Replace("\r\n", "\n"), @"\n(?=--- !u!)").Single(document => document.StartsWith("--- !u!1001") && document.Contains($"m_SourcePrefab: {{fileID: 100100000, guid: {pcGuid}, type: 3}}"));
            float[] scale = new[] { "x", "y", "z" }.Select(axis => ScaleOverride(instance, axis)).ToArray();
            Assert.That(scale[0], Is.EqualTo(scale[1]).Within(1e-5f));
            Assert.That(scale[1], Is.EqualTo(scale[2]).Within(1e-5f));
            Assert.That(scene.Split('\n').Count(line => line.Trim().StartsWith("_pc: {fileID:") && !line.Contains("fileID: 0}")), Is.EqualTo(1), "the save controller owns the PC record");
            Assert.That(scene.Split('\n').Count(line => line.Trim().StartsWith("workbench: {fileID:") && !line.Contains("fileID: 0}")), Is.EqualTo(1), "the input router knows the Workbench");
        }

        private static float ScaleOverride(string instance, string axis)
        {
            Match match = Regex.Match(instance, $@"propertyPath: m_LocalScale\.{axis}\n\s+value: (\S+)");
            return match.Success ? float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1f;
        }

        private static Transform Named(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(transform => transform.name == name);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;

            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            return bounds;
        }
    }
}
