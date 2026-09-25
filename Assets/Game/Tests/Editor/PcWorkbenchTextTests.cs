using System.Linq;
using System.Reflection;
using GoLive.Items;
using GoLive.Localization;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // What the build UI says about the real Student PC, straight from its record: the verdict, one line about the most
    // important finding, one checklist row per kind of part, and refusals that name the parts they wait for. The record is
    // changed directly, so every PC state the game can reach (and the weak power supply it cannot reach yet) is covered.
    public sealed class PcWorkbenchTextTests
    {
        private const string Catalog = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";
        private const char Installed = '●';
        private const char Missing = '○';
        private const char FallsShort = '▲';

        private GameObject _holder;
        private PcAssemblyBehaviour _pc;
        private LocalizationContext _localization;
        private PcWorkbenchText _text;
        private PcComponentSpec _hungryCard;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("Test PC holder");
            _holder.SetActive(false);
            GameObject pc = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SaveTestWorld.StudentPcPrefab), _holder.transform);
            _pc = pc.GetComponent<PcAssemblyBehaviour>();

            foreach (WorldItem item in pc.GetComponentsInChildren<WorldItem>(true))
                Invoke(item, "Awake");

            Invoke(_pc, "Awake");
            Invoke(_pc, "Start");
            Assert.That(_pc.IsReady, Is.True);

            _localization = _holder.AddComponent<LocalizationContext>();
            SerializedObject localization = new(_localization);
            localization.FindProperty("_catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(Catalog);
            localization.ApplyModifiedPropertiesWithoutUndo();
            _text = new PcWorkbenchText(_pc, _localization);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);

            if (_hungryCard != null)
                Object.DestroyImmediate(_hungryCard);
        }

        [Test]
        public void TheStarterPcIsReadyAndSaysWhatTheMissingCardCostsOnce()
        {
            string[] lines = Lines();

            Assert.That(lines[0], Is.EqualTo(Text("pc.status.ready")));
            Assert.That(lines[1], Is.EqualTo(Text("pc.diagnostic.no_gpu.detail")), "the one limitation, as what it costs");
            Assert.That(Rows(), Is.EqualTo(new[]
            {
                $"{Installed} {Text("pc.component.motherboard")}",
                $"{Installed} {Text("pc.component.cpu")}",
                $"{Installed} {Text("pc.component.ram")}",
                $"{Installed} {Text("pc.component.psu")}",
                $"{Installed} {Text("pc.component.storage")}",
                $"{Missing} {Text("pc.component.gpu")}"
            }), "one row per kind of part, nothing said twice");
            Assert.That(_text.Status(), Does.Not.Contain(Text("pc.diagnostic.no_gpu.title")));
            Assert.That(_text.StatusTitle, Is.EqualTo(Text("pc.status.title")));
        }

        [Test]
        public void MissingPartsThatStopThePcShareOneLine()
        {
            Remove("cpu-0", "ram-0", "psu-0");
            string[] lines = Lines();

            Assert.That(lines[0], Is.EqualTo(Text("pc.status.wont_start")));
            Assert.That(lines[1], Is.EqualTo(Text("pc.status.install_marked")), "not one paragraph per missing part");
            Assert.That(Rows().Count(row => row[0] == Missing), Is.EqualTo(4), "processor, memory, power supply and graphics card");
            Assert.That(lines.Count(line => line.Contains(Text("pc.status.install_marked"))), Is.EqualTo(1));
        }

        [Test]
        public void WithoutADriveThePcStartsButDoesNotBoot()
        {
            Remove("storage-0");
            string[] lines = Lines();

            Assert.That(lines[0], Is.EqualTo(Text("pc.status.wont_boot")));
            Assert.That(lines[1], Is.EqualTo(Text("pc.diagnostic.no_storage.detail")));
            Assert.That(Rows(), Has.Member($"{Missing} {Text("pc.component.storage")}"));
        }

        [Test]
        public void AWeakPowerSupplyIsTheOneLineAndItsRowFallsShort()
        {
            _hungryCard = ScriptableObject.CreateInstance<PcComponentSpec>();
            ShopTestData.Set(_hungryCard, "componentType", PcComponentType.Gpu);
            ShopTestData.Set(_hungryCard, "connector", PcConnector.PcieX16);
            ShopTestData.Set(_hungryCard, "powerDrawWatts", 400);
            Assert.That(_pc.Assembly.TryRecordInstall("gpu-0", "hungry-card", _hungryCard), Is.True);

            string[] lines = Lines();
            PcCapabilities pc = _pc.Capabilities;

            Assert.That(lines[0], Is.EqualTo(Text("pc.status.wont_start")));
            Assert.That(lines[1], Is.EqualTo(_localization.Format("pc.diagnostic.weak_psu.detail", pc.TotalPowerDrawWatts, pc.PowerSupplyCapacityWatts)));
            Assert.That(lines[1], Does.Contain("499").And.Contain("300"), "the numbers where they help");
            Assert.That(Rows(), Has.Member($"{FallsShort} {Text("pc.component.psu")}"), "installed, but not enough");
            Assert.That(Rows(), Has.Member($"{Installed} {Text("pc.component.gpu")}"));
        }

        [Test]
        public void TheGraphicsCardMakesThePcReadyForGamesWithNothingLeftToSay()
        {
            Assert.That(_pc.Assembly.TryRecordInstall("gpu-0", "card", ShopTestData.LoadItem(ShopTestData.BudgetGpuItem).PcComponent), Is.True);
            string[] lines = Lines();

            Assert.That(lines[0], Is.EqualTo(Text("pc.status.ready_for_games")));
            Assert.That(Rows(), Is.All.StartsWith(Installed.ToString()));
            Assert.That(lines, Has.Length.EqualTo(1 + 1 + 6), "verdict, spacer, six rows");
        }

        [Test]
        public void RefusalsNameThePartsTheyWaitFor()
        {
            Assert.That(_text.Reason(PcSlotCheck.MountedPartsInstalled, "motherboard-0"), Is.EqualTo("Сначала снимите процессор и оперативную память"));

            Assert.That(_pc.Assembly.TryRecordInstall("gpu-0", "card", ShopTestData.LoadItem(ShopTestData.BudgetGpuItem).PcComponent), Is.True);
            Assert.That(_text.Reason(PcSlotCheck.MountedPartsInstalled, "motherboard-0"), Is.EqualTo("Сначала снимите процессор, оперативную память и видеокарту"));

            _localization.SetLanguage(GameLanguage.English);
            Assert.That(_text.Reason(PcSlotCheck.MountedPartsInstalled, "motherboard-0"), Is.EqualTo("Remove the processor, the memory and the graphics card first"));

            Remove("gpu-0", "cpu-0", "ram-0", "motherboard-0");
            Assert.That(_text.Reason(PcSlotCheck.HostMissing, "cpu-0"), Is.EqualTo("Install the motherboard first"));
            _localization.SetLanguage(GameLanguage.Russian);
            Assert.That(_text.Reason(PcSlotCheck.HostMissing, "cpu-0"), Is.EqualTo("Сначала установите материнскую плату"));
            Assert.That(_text.Reason(PcSlotCheck.SlotOccupied, "gpu-0"), Is.EqualTo(Text("pc.reject.slot_occupied")));
        }

        // What fits right now comes first, other PC parts say why not, everything else is faded and last.
        [Test]
        public void PartNotesPutWhatFitsFirst()
        {
            PcWorkbenchText.PartNote gpu = _text.Note(ShopTestData.LoadItem(ShopTestData.BudgetGpuItem));
            PcWorkbenchText.PartNote board = _text.Note(ShopTestData.LoadItem("Item_UsedMotherboard"));
            PcWorkbenchText.PartNote banana = _text.Note(ShopTestData.LoadItem(ShopTestData.BananaItem));

            Assert.That((gpu.Text, gpu.Rank, gpu.Dimmed), Is.EqualTo((_localization.Format("pc.part.installable", Text("pc.component.gpu")), 0, false)));
            Assert.That((board.Text, board.Rank, board.Dimmed), Is.EqualTo((_localization.Format("pc.part.slot_taken", Text("pc.component.motherboard")), 1, false)));
            Assert.That((banana.Text, banana.Rank, banana.Dimmed), Is.EqualTo((Text("pc.part.not_part"), 2, true)));

            Remove("cpu-0", "ram-0", "motherboard-0");
            PcWorkbenchText.PartNote cpu = _text.Note(ShopTestData.LoadItem("Item_StarterCpu"));
            Assert.That((cpu.Text, cpu.Rank), Is.EqualTo(("Сначала установите материнскую плату", 1)), "the row's own name says what it is; the note says what it waits for");
            Assert.That(_text.Note(ShopTestData.LoadItem("Item_UsedMotherboard")).Rank, Is.Zero, "the board fits now");
        }

        private void Remove(params string[] slotIds)
        {
            foreach (string slotId in slotIds)
                Assert.That(_pc.Assembly.TryRecordRemoval(slotId, out _), Is.True, slotId);
        }

        [Test]
        public void PowerBudgetUsesCurrentHardwareAndLocalizedUnits()
        {
            Assert.That(_text.PowerBudget(), Is.EqualTo("Питание: 99 / 300 Вт"));
            Remove("storage-0");
            Assert.That(_text.PowerBudget(), Is.EqualTo("Питание: 93 / 300 Вт"));
            _localization.SetLanguage(GameLanguage.English);
            Assert.That(_text.PowerBudget(), Is.EqualTo("Power: 93 / 300 W"));
            Remove("psu-0");
            Assert.That(_text.PowerBudget(), Is.EqualTo("Power: 93 / 0 W"), "never invent a power supply capacity");
        }

        [Test]
        public void CompatibleEmptySlotExplicitlyNamesItsCompatibility()
        {
            Assert.That(_pc.TryGetSlot("gpu-0", out PcComponentSlot slot), Is.True);
            PcWorkbenchText.Card card = _text.SlotCard(slot, PcSlotCheck.Allowed, "E", "F");
            Assert.That(card.Detail, Does.Contain("Совместимо"));
            Assert.That(card.Action, Does.Contain("["));
            Assert.That(card.Tone, Is.EqualTo(PcWorkbenchHudView.Tone.Action));
        }

        [Test]
        public void IncompatibleSlotNamesTheRequiredTypeAndPreservesActualRefusal()
        {
            Assert.That(_pc.TryGetSlot("gpu-0", out PcComponentSlot slot), Is.True);
            _localization.SetLanguage(GameLanguage.English);
            PcWorkbenchText.Card card = _text.SlotCard(slot, PcSlotCheck.WrongComponentType, "E", "F");
            Assert.That(card.Detail, Does.Contain("Not compatible"));
            Assert.That(card.Detail, Does.Contain("Required: " + Text("pc.component.gpu")));
            Assert.That(card.Action, Is.EqualTo(Text(PcSlotCheck.WrongComponentType.MessageKey())));
            Assert.That(card.Tone, Is.EqualTo(PcWorkbenchHudView.Tone.Rejected));
        }

        // The status without markup, one entry per line.
        private string[] Lines()
        {
            return System.Text.RegularExpressions.Regex.Replace(_text.Status(), "<[^>]+>", string.Empty).Split('\n').Select(line => line.Trim()).ToArray();
        }

        private string[] Rows()
        {
            return Lines().Where(line => line.Length > 0 && (line[0] == Installed || line[0] == Missing || line[0] == FallsShort))
                .Select(line => System.Text.RegularExpressions.Regex.Replace(line, @"\s+", " "))
                .ToArray();
        }

        private string Text(string key)
        {
            return _localization.Text(key);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }
    }
}
