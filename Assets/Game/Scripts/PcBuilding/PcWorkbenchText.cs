using System.Collections.Generic;
using System.Text;
using GoLive.Items;
using GoLive.Localization;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // What the build UI says, composed from the PC's own truth: PcCapabilities for what the PC can do, the assembly record
    // and its checks for every slot and part. Plain C# without state of its own: the Workbench asks, the HUD view draws.
    // Each fact is said once: the verdict says what the PC can do, one line says why (or what it costs), the checklist
    // says which parts are in.
    public sealed class PcWorkbenchText
    {
        private const string StatusTitleKey = "pc.status.title";
        private const string WontStartKey = "pc.status.wont_start";
        private const string WontBootKey = "pc.status.wont_boot";
        private const string ReadyKey = "pc.status.ready";
        private const string ReadyForGamesKey = "pc.status.ready_for_games";
        private const string InstallMarkedKey = "pc.status.install_marked";
        private const string ChooseTitleKey = "pc.workbench.choose.title";
        private const string ChooseDetailKey = "pc.workbench.choose.detail";
        private const string PointHintKey = "pc.workbench.hint.point";
        private const string EmptySlotKey = "pc.workbench.slot_empty";
        private const string ControlsKey = "pc.workbench.controls";
        private const string ClickKey = "pc.workbench.click";
        private const string InstallableKey = "pc.part.installable";
        private const string SlotTakenKey = "pc.part.slot_taken";
        private const string NoPlaceKey = "pc.part.no_place";
        private const string NotPartKey = "pc.part.not_part";
        private const string NotPartFeedbackKey = "pc.feedback.not_part";
        private const string AndKey = "pc.list.and";

        private const string Good = "8FE3A8";
        private const string Warning = "F2C46B";
        private const string Bad = "F29B8C";
        private const string Plain = "D5DCE4";
        private const string Quiet = "8E99A6";
        private const char Installed = '●';
        private const char Missing = '○';
        private const char FallsShort = '▲';

        private static readonly Color InstallableColor = new(0.56f, 0.89f, 0.66f, 1f);
        private static readonly Color UnavailableColor = new(0.95f, 0.77f, 0.42f, 1f);
        private static readonly Color NotPartColor = new(0.55f, 0.58f, 0.62f, 1f);

        public readonly struct Card
        {
            public string Title { get; }
            public string Detail { get; }
            public string Action { get; }
            public PcWorkbenchHudView.Tone Tone { get; }

            public Card(string title, string detail, string action, PcWorkbenchHudView.Tone tone)
            {
                Title = title;
                Detail = detail;
                Action = action;
                Tone = tone;
            }
        }

        public readonly struct PartNote
        {
            public string Text { get; }
            public Color Color { get; }
            public bool Dimmed { get; }

            // Parts panel order, lowest first: what can go in right now, other PC parts, then everything else.
            public int Rank { get; }

            public PartNote(string text, Color color, bool dimmed, int rank)
            {
                Text = text;
                Color = color;
                Dimmed = dimmed;
                Rank = rank;
            }
        }

        private readonly PcAssemblyBehaviour _pc;
        private readonly LocalizationContext _localization;
        private readonly StringBuilder _builder = new(256);
        private readonly List<PcComponentType> _checklist = new();
        private readonly List<string> _names = new();

        public PcWorkbenchText(PcAssemblyBehaviour pc, LocalizationContext localization)
        {
            _pc = pc;
            _localization = localization;

            // One checklist row per kind of slot this PC has, in slot layout order.
            foreach (PcComponentSlot slot in pc.Slots)
            {
                if (!_checklist.Contains(slot.ComponentType))
                    _checklist.Add(slot.ComponentType);
            }
        }

        public string StatusTitle => Text(StatusTitleKey);

        // The verdict (what the PC can do), the most important finding in one line, then one row per part: a filled mark
        // when it is in and fine, a hollow one when it is missing, a triangle when it is in but falls short; red where it
        // stops the PC, amber where it only limits it.
        public string Status()
        {
            PcCapabilities pc = _pc.Capabilities;
            IReadOnlyList<PcDiagnostic> findings = pc.Diagnostics;
            _builder.Clear();

            string verdict = !pc.CanPowerOn ? WontStartKey : !pc.CanUseDesktop ? WontBootKey : !pc.GamingGraphicsAvailable ? ReadyKey : ReadyForGamesKey;
            _builder.Append("<size=140%><b><color=#").Append(pc.CanUseDesktop ? Good : Bad).Append('>').Append(Text(verdict)).Append("</color></b></size>");

            if (findings.Count > 0)
            {
                // Missing parts that stop the PC share one line: the checklist marks which they are.
                PcDiagnostic first = findings[0];
                string line = first.Affects == PcFunction.PowerOn && !_pc.Assembly.HasComponent(first.Component)
                    ? Text(InstallMarkedKey)
                    : _localization.Format(first.DetailKey, first.RequiredWatts, first.AvailableWatts);

                _builder.Append("\n<color=#").Append(first.Severity == PcDiagnosticSeverity.Blocker ? Bad : Warning).Append('>').Append(line).Append("</color>");
            }

            _builder.Append("\n<size=50%> </size>");

            foreach (PcComponentType type in _checklist)
            {
                bool flagged = TryFind(findings, type, out PcDiagnostic finding);
                string color = !flagged ? Good : finding.Severity == PcDiagnosticSeverity.Blocker ? Bad : Warning;
                char mark = !flagged ? Installed : _pc.Assembly.HasComponent(type) ? FallsShort : Missing;

                _builder.Append("\n<color=#").Append(color).Append('>').Append(mark).Append("</color>  <color=#").Append(flagged ? color : Plain).Append('>')
                    .Append(Text(type.NameKey())).Append("</color>");
            }

            return _builder.ToString();
        }

        // The slot under the pointer: what it is, what is in it (with its technical label), and what the keys do there.
        // check: removal for a filled slot, installation for an empty one.
        public Card SlotCard(PcComponentSlot slot, PcSlotCheck check, string installKey, string removeKey)
        {
            bool filled = _pc.TryGetInstalledItem(slot, out WorldItem installed);
            string detail = $"{(filled ? ItemName(installed.Definition) : Text(EmptySlotKey))}<color=#{Quiet}>  ·  {slot.TechnicalLabel}</color>";

            if (check != PcSlotCheck.Allowed)
                return new Card(Text(slot.ComponentType.NameKey()), detail, Reason(check, slot.SlotId), check == PcSlotCheck.NothingInHands ? PcWorkbenchHudView.Tone.Hint : PcWorkbenchHudView.Tone.Rejected);

            string action = filled
                ? $"[{removeKey}] {Text(slot.ComponentType.RemovePromptKey())}"
                : $"[{Text(ClickKey)} / {installKey}] {Text(slot.ComponentType.InstallPromptKey())}";

            return new Card(Text(slot.ComponentType.NameKey()), detail, action, PcWorkbenchHudView.Tone.Action);
        }

        // No slot under the pointer: what to do next with empty hands, or where the part in them can go.
        public Card HandsCard(WorldItem held)
        {
            if (held == null)
                return new Card(Text(ChooseTitleKey), Text(ChooseDetailKey), null, PcWorkbenchHudView.Tone.Hint);

            PcSlotCheck fit = _pc.Assembly.CheckPart(held.Definition.PcComponent, out string slotId);

            return fit == PcSlotCheck.Allowed
                ? new Card(ItemName(held.Definition), Text(PointHintKey), null, PcWorkbenchHudView.Tone.Action)
                : new Card(ItemName(held.Definition), null, Reason(fit, slotId), PcWorkbenchHudView.Tone.Rejected);
        }

        public string Controls(string installKey, string removeKey, string exitKey)
        {
            return _localization.Format(ControlsKey, installKey, removeKey, exitKey);
        }

        // What an Inventory item means for this PC right now, for the parts panel.
        public PartNote Note(ItemDefinition definition)
        {
            PcComponentSpec part = definition.PcComponent;

            if (part == null)
                return new PartNote(Text(NotPartKey), NotPartColor, true, 2);

            string component = Text(part.ComponentType.NameKey());

            return _pc.Assembly.CheckPart(part, out string slotId) switch
            {
                PcSlotCheck.Allowed => new PartNote(_localization.Format(InstallableKey, component), InstallableColor, false, 0),
                PcSlotCheck.SlotOccupied => new PartNote(_localization.Format(SlotTakenKey, component), UnavailableColor, false, 1),
                // The row's own name already says what the part is: the one thing to say is what it waits for.
                PcSlotCheck.HostMissing => new PartNote(Reason(PcSlotCheck.HostMissing, slotId), UnavailableColor, false, 1),
                _ => new PartNote(_localization.Format(NoPlaceKey, component), UnavailableColor, false, 1)
            };
        }

        public string NotAPart(ItemDefinition definition)
        {
            return _localization.Format(NotPartFeedbackKey, ItemName(definition));
        }

        // Why an action on this slot is refused, in words; the mounting checks name the parts they wait for.
        public string Reason(PcSlotCheck check, string slotId)
        {
            return check switch
            {
                PcSlotCheck.HostMissing => _localization.Format(check.MessageKey(), HostName(slotId)),
                PcSlotCheck.MountedPartsInstalled => _localization.Format(check.MessageKey(), NamesOf(_pc.Assembly.InstalledOn(slotId))),
                _ => Text(check.MessageKey())
            };
        }

        public string ItemName(ItemDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.NameLocalizationKey) ? definition.ItemId : Text(definition.NameLocalizationKey);
        }

        private string HostName(string slotId)
        {
            _pc.Assembly.TryGetSlot(slotId, out PcSlotSpec slot);
            _pc.Assembly.TryGetSlot(slot.HostSlotId, out PcSlotSpec host);
            return Text(host.ComponentType.ObjectNameKey());
        }

        // "процессор, оперативную память и видеокарту": each kind of part once, in slot order.
        private string NamesOf(List<PcInstalledComponent> parts)
        {
            _names.Clear();

            foreach (PcInstalledComponent part in parts)
            {
                string name = Text(part.ComponentType.ObjectNameKey());

                if (!_names.Contains(name))
                    _names.Add(name);
            }

            if (_names.Count == 0)
                return string.Empty;

            string list = _names[_names.Count - 1];

            for (int i = _names.Count - 2; i >= 0; i--)
                list = i == _names.Count - 2 ? _localization.Format(AndKey, _names[i], list) : $"{_names[i]}, {list}";

            return list;
        }

        private string Text(string key)
        {
            return _localization.Text(key);
        }

        private static bool TryFind(IReadOnlyList<PcDiagnostic> findings, PcComponentType type, out PcDiagnostic finding)
        {
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Component == type)
                {
                    finding = findings[i];
                    return true;
                }
            }

            finding = default;
            return false;
        }
    }
}
