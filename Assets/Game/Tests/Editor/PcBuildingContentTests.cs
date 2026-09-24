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
    // Authored PC content: component data, the Student PC slots and the parts it comes with, the corrected Budget GPU and
    // the GL desk wiring.
    public sealed class PcBuildingContentTests
    {
        private const string StudentPc = "Assets/Game/Prefab/PC/StudentPC.prefab";
        private const string GpuWrapper = "Assets/Game/Prefab/Items/Item_BudgetGPU.prefab";
        private const string GpuArt = "Assets/Game/Prefab/1_Main Room/PC/GPU/PC_GPU_LOD0.prefab";
        private const string GpuMaterial = "Assets/Game/Props/1_Main Room/PC/GPU/M_PC_GPU.mat";
        private const string GameScene = "Assets/Game/Scenes/GL.unity";
        private const string Catalog = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";

        // Every item with PC component data; everything else (peripherals, food, packages) has none.
        private static readonly string[] PcHardwareItemIds =
        {
            "budget-gpu", "used-motherboard", "used-psu", "starter-motherboard", "starter-cpu", "starter-ram", "starter-hdd"
        };

        // The Student PC's slots in layout order: ID, component type, connector, fixed in place.
        private static readonly (string id, PcComponentType type, PcConnector connector, bool isFixed)[] StudentSlots =
        {
            ("motherboard-0", PcComponentType.Motherboard, PcConnector.MotherboardTray, true),
            ("cpu-0", PcComponentType.Cpu, PcConnector.CpuSocket, false),
            ("ram-0", PcComponentType.Ram, PcConnector.MemorySlot, false),
            ("psu-0", PcComponentType.Psu, PcConnector.PowerSupplyBay, false),
            ("storage-0", PcComponentType.Storage, PcConnector.SataStorage, false),
            ("gpu-0", PcComponentType.Gpu, PcConnector.PcieX16, false)
        };

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
                Assert.That(spec.IsValid, Is.True, $"{path} {spec.ValidationError}");
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

            // Keyboard, microphone and router are external setup, never internal PC parts.
            Assert.That(ItemIconGenerator.FindItemDefinitions().Where(definition => definition.PcComponent != null).Select(definition => definition.ItemId),
                Is.EquivalentTo(PcHardwareItemIds));
        }

        // Every PC part that can leave its slot is a complete pickup item: a clean gameplay prefab and a real icon.
        [Test]
        public void EveryPcPartIsACompletePickupItem()
        {
            foreach (ItemDefinition definition in ItemIconGenerator.FindItemDefinitions().Where(definition => definition.PcComponent != null))
            {
                Assert.That(definition.TryGetRuntimePrefab(out WorldItem prefab), Is.True, $"{definition.name}: World Prefab");
                Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null, $"{definition.name}: Rigidbody");
                Assert.That(prefab.transform.Find("Visual"), Is.Not.Null, $"{definition.name}: the art sits under a Visual child");
                Assert.That(definition.InventoryIcon, Is.Not.Null, $"{definition.name} has no icon. Run GO LIVE > Items > Generate Missing Item Icons.");
                Assert.That(AssetDatabase.GetAssetPath(definition.InventoryIcon), Is.EqualTo($"{ItemIconGenerator.OutputFolder}/{definition.ItemId}.png"), $"{definition.name}: a generated render");
                Assert.That(definition.CanStoreInInventory, Is.True, $"{definition.name}: parts go through the Inventory");
            }
        }

        // Watts are data: every part draws a plausible amount and only the power supply gives capacity.
        [Test]
        public void PowerDataIsPlausibleAndTheStarterSupplyCarriesTheBudgetGpu()
        {
            foreach (ItemDefinition definition in ItemIconGenerator.FindItemDefinitions().Where(definition => definition.PcComponent != null))
            {
                PcComponentSpec spec = definition.PcComponent;
                bool supply = spec.ComponentType == PcComponentType.Psu;

                Assert.That(spec.PowerDrawWatts, supply ? Is.Zero : Is.InRange(1, 400), $"{definition.name}: draw");
                Assert.That(spec.PowerCapacityWatts, supply ? Is.InRange(100, 2000) : Is.Zero, $"{definition.name}: capacity");
            }

            PcAssembly starter = StarterRecord(out _);
            Assert.That(starter.TryRecordInstall("gpu-0", "budget-gpu", ShopTestData.LoadItem(ShopTestData.BudgetGpuItem).PcComponent), Is.True);
            PcCapabilities withGpu = PcCapabilities.Evaluate(starter);
            TestContext.WriteLine($"Starter PC with the Budget GPU draws {withGpu.TotalPowerDrawWatts} W of {withGpu.PowerSupplyCapacityWatts} W");
            Assert.That(withGpu.HasEnoughPower, Is.True, "Day 1: the starter power supply carries the Budget GPU");
            Assert.That(withGpu.CanPlayCriticalStrike, Is.True);
        }

        // The new game: the Student PC comes with motherboard, processor, memory, power supply and drive installed as real
        // scene items with persistent IDs, and no graphics card.
        [Test]
        public void StudentPcComesWithItsStarterPartsAndNoGraphicsCard()
        {
            PcAssembly starter = StarterRecord(out (PcComponentSlot slot, WorldItem item)[] parts);
            PcComponentSlot[] authored = AssetDatabase.LoadAssetAtPath<GameObject>(StudentPc).GetComponentsInChildren<PcComponentSlot>(true);

            Assert.That(parts.Select(part => part.slot.SlotId), Is.EqualTo(new[] { "motherboard-0", "cpu-0", "ram-0", "psu-0", "storage-0" }));
            Assert.That(parts.Select(part => part.slot), Is.Unique, "one part per slot");
            Assert.That(parts.Select(part => part.item), Is.Unique);
            Assert.That(parts.Select(part => part.item.AuthoredInstanceId), Is.Unique, "persistent IDs are unique");
            Assert.That(parts.Select(part => part.item.AuthoredInstanceId), Has.None.Null.And.None.Empty);

            foreach ((PcComponentSlot slot, WorldItem item) in parts)
            {
                string label = $"{slot.SlotId}: {item.name}";
                Assert.That(item.IsRuntime, Is.False, $"{label} is a scene item with a persistent ID");
                Assert.That(ItemDefinition.IsValidItemId(item.AuthoredInstanceId) && item.AuthoredInstanceId.Length == 32, Is.True, $"{label}: the scene item ID format");
                Assert.That(item.transform.parent, Is.SameAs(slot.InstallAnchor), $"{label} sits on its slot's Install Anchor");
                Assert.That(item.transform.localPosition, Is.EqualTo(Vector3.zero), label);
                Assert.That(item.transform.localRotation, Is.EqualTo(Quaternion.identity), label);
                Assert.That(PcAssembly.CheckCompatibility(slot.Spec, item.Definition.PcComponent), Is.EqualTo(PcSlotCheck.Allowed), $"{label} matches its slot");
                Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(item.gameObject), Is.SameAs(item.Definition.WorldPrefab), $"{label} is its definition's own prefab, so it looks the same in the PC and in the hands");
            }

            // Nothing else on any anchor: an unlisted item would be inside the PC without a slot record.
            Assert.That(authored.SelectMany(slot => slot.InstallAnchor.GetComponentsInChildren<WorldItem>(true)), Is.EquivalentTo(parts.Select(part => part.item)));

            Dictionary<PcComponentType, int> counts = parts.GroupBy(part => part.item.Definition.PcComponent.ComponentType).ToDictionary(group => group.Key, group => group.Count());
            Assert.That(counts.GetValueOrDefault(PcComponentType.Motherboard), Is.EqualTo(1));
            Assert.That(counts.GetValueOrDefault(PcComponentType.Cpu), Is.EqualTo(1), "exactly one processor");
            Assert.That(counts.GetValueOrDefault(PcComponentType.Ram), Is.GreaterThanOrEqualTo(1), "at least one memory module");
            Assert.That(counts.GetValueOrDefault(PcComponentType.Psu), Is.EqualTo(1), "exactly one power supply");
            Assert.That(counts.GetValueOrDefault(PcComponentType.Storage), Is.EqualTo(1), "exactly one drive");
            Assert.That(counts.GetValueOrDefault(PcComponentType.Gpu), Is.Zero, "Day 1 starts without a graphics card");

            PcCapabilities pc = PcCapabilities.Evaluate(starter);
            Assert.That(pc.CanPowerOn, Is.True);
            Assert.That(pc.CanUseDesktop, Is.True);
            Assert.That(pc.HasDedicatedGpu, Is.False);
            Assert.That(pc.GamingGraphicsAvailable, Is.False);
            Assert.That(pc.CanPlayCriticalStrike, Is.False);
            Assert.That(pc.Diagnostics.Select(diagnostic => diagnostic.Code), Is.EqualTo(new[] { PcDiagnosticCode.NoDedicatedGpu }), "its only limitation");
        }

        // Scene item IDs are unique across the whole GL scene, the Student PC's parts included.
        [Test]
        public void StarterPartIdsAreUniqueInTheGameScene()
        {
            string scene = File.ReadAllText(GameScene);
            StarterRecord(out (PcComponentSlot slot, WorldItem item)[] parts);
            string[] sceneIds = Regex.Matches(scene, @"authoredInstanceId: (\w+)").Cast<Match>().Select(match => match.Groups[1].Value)
                .Concat(Regex.Matches(scene, @"propertyPath: authoredInstanceId\r?\n\s+value: (\w+)").Cast<Match>().Select(match => match.Groups[1].Value))
                .ToArray();

            Assert.That(sceneIds.Concat(parts.Select(part => part.item.AuthoredInstanceId)), Is.Unique);
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

            Assert.That(assembly.Slots.Select(slot => (slot.SlotId, slot.ComponentType, slot.Connector, slot.IsFixed)), Is.EqualTo(StudentSlots), "the slot table");
            Assert.That(() => new PcAssembly(assembly.Slots.Select(slot => slot.Spec).ToArray()), Throws.Nothing, "valid IDs, defined types and connectors");

            Assert.That(assembly, Is.Not.Null);
            Assert.That(pc.GetComponent<Collider>(), Is.Not.Null, "the case answers the B ray");
            Assert.That(pc.GetComponent<PcWorkbenchBehaviour>(), Is.Null, "the Workbench is scene wiring, not part of the PC prefab");
            Assert.That(assembly.Slots, Is.EquivalentTo(authored), "every authored slot is registered");
            Assert.That(authored.Select(slot => slot.SlotId).Distinct().Count(), Is.EqualTo(authored.Length), "slot IDs are unique");

            foreach (PcComponentSlot slot in authored)
            {
                Assert.That(slot.IsConfigured(out string error), Is.True, $"{slot.name}: {error}");
                Assert.That(slot.InstallAnchor.localPosition == Vector3.zero && slot.InstallAnchor.localRotation == Quaternion.identity, Is.True, $"{slot.name}: the slot's pose is the installed pose");
                Assert.That(slot.TechnicalLabel, Is.Not.Empty, slot.name);
            }

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
            // The only colliders under it belong to the preinstalled parts, and the PC turns those off as it installs them.
            Assert.That(root.GetComponentsInChildren<Collider>(true).Where(collider => collider.GetComponentInParent<WorldItem>(true) == null), Is.Empty, "no collider of the PC travels with the presentation");
            Assert.That(root.GetComponentsInChildren<Collider>(true).Select(collider => collider.GetComponentInParent<WorldItem>(true)).Distinct(), Is.EquivalentTo(root.GetComponentsInChildren<WorldItem>(true)), "one pickup collider per part");
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

            foreach (PcDiagnosticCode code in System.Enum.GetValues(typeof(PcDiagnosticCode)).Cast<PcDiagnosticCode>())
            {
                keys.Add(PcDiagnostic.For(code).TitleKey);
                keys.Add(PcDiagnostic.For(code).DetailKey);
            }

            foreach (ItemDefinition definition in ItemIconGenerator.FindItemDefinitions().Where(definition => definition.PcComponent != null))
                keys.Add(definition.NameLocalizationKey);

            string[] missing = keys.Where(key => !catalog.TryGetText(key, GameLanguage.Russian, out _) || !catalog.TryGetText(key, GameLanguage.English, out _)).OrderBy(key => key).ToArray();
            Assert.That(missing, Is.Empty);

            // The weak power supply detail shows both numbers in each language.
            foreach (GameLanguage language in new[] { GameLanguage.Russian, GameLanguage.English })
            {
                catalog.TryGetText(PcDiagnostic.For(PcDiagnosticCode.InsufficientPower).DetailKey, language, out string text);
                Assert.That(string.Format(text, 174, 150), Does.Contain("174").And.Contain("150"), language.ToString());
            }
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
            pc.GetComponent<PcAssemblyBehaviour>().TryGetSlot("storage-0", out PcComponentSlot storage);
            Bounds drive = BoundsOf(storage.InstallAnchor.GetComponentInChildren<WorldItem>().gameObject);

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

        // The Student PC's slots as the plain record, with its authored new-game parts recorded the way its Start does.
        private static PcAssembly StarterRecord(out (PcComponentSlot slot, WorldItem item)[] parts)
        {
            PcAssemblyBehaviour behaviour = AssetDatabase.LoadAssetAtPath<GameObject>(StudentPc).GetComponent<PcAssemblyBehaviour>();
            SerializedProperty entries = new SerializedObject(behaviour).FindProperty("preinstalled");
            parts = new (PcComponentSlot, WorldItem)[entries.arraySize];

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                parts[i] = ((PcComponentSlot)entry.FindPropertyRelative("slot").objectReferenceValue, (WorldItem)entry.FindPropertyRelative("item").objectReferenceValue);
                Assert.That(parts[i].slot != null && parts[i].item != null, Is.True, $"preinstalled entry {i} is complete");
            }

            PcAssembly assembly = new(behaviour.Slots.Select(slot => slot.Spec).ToArray());

            foreach ((PcComponentSlot slot, WorldItem item) in parts)
                Assert.That(assembly.TryRecordInstall(slot.SlotId, item.AuthoredInstanceId, item.Definition.PcComponent), Is.True, $"{item.name} records into {slot.SlotId}");

            return assembly;
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
