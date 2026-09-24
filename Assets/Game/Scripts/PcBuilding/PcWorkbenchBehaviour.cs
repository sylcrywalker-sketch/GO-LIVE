using System;
using System.Collections.Generic;
using System.Text;
using GoLive.Interaction;
using GoLive.Inventory;
using GoLive.Items;
using GoLive.Localization;
using GoLive.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace GoLive.PcBuilding
{
    // PC Build Mode. B at the PC hands control to this mode: the PC is brought right in front of the player (its
    // presentation root; the gameplay root stays in place), the side panel comes off past the player's eye and the
    // gameplay HUD gives way to the build UI (PC status, parts panel, slot card). A part taken from the parts panel
    // lights up the slots it fits; pointing at one shows the ghost of the actual part in its installed pose; a click or
    // E installs it and F takes an installed part back into empty hands. B or Esc (through GameUiInputRouter) plays the
    // same presentation backwards and only then gives control back.
    // This behaviour owns the mode, the pointer target and presentation. The timeline is PcBuildModeSequence, the PC's
    // movement is PcBuildPresentation, and every item state change is one call:
    // PcAssemblyBehaviour.TryInstallCarried / TryRemoveToCarry or PlayerInventory.TryTakeToCarry / TryStoreCarriedItem.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PcAssemblyBehaviour))]
    [RequireComponent(typeof(PcBuildPresentation))]
    public sealed class PcWorkbenchBehaviour : MonoBehaviour, IInteractable
    {
        private const string EnterPromptKey = "pc.workbench.enter";
        private const string TitleKey = "pc.workbench.title";
        private const string MissingKey = "pc.workbench.missing";
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

        private const float ApproachSeconds = 0.6f;
        private const float CoverSeconds = 0.55f;
        private const float OverlapSeconds = 0.15f;
        // A hitch slows the presentation down instead of making the PC jump.
        private const float MaxStepSeconds = 1f / 30f;
        private const float MaxPointerDistance = 3f;

        private static readonly Color InstallableColor = new(0.56f, 0.89f, 0.66f, 1f);
        private static readonly Color UnavailableColor = new(0.93f, 0.78f, 0.5f, 1f);
        private static readonly Color NotPartColor = new(0.55f, 0.58f, 0.62f, 1f);

        [Header("PC")]
        [SerializeField] private Material ghostMaterial;

        [Header("Player")]
        [SerializeField] private PlayerController playerController;
        [SerializeField] private PlayerCarry playerCarry;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private Camera playerCamera;

        [Header("UI")]
        [SerializeField] private PcWorkbenchHudView hud;
        [SerializeField] private PcBuildInventoryView parts;
        [SerializeField] private LocalizationContext localization;
        [SerializeField] private InputSystemUIInputModule pointerInput;

        [Header("Input")]
        [SerializeField] private InputActionReference installAction;
        [SerializeField] private InputActionReference removeAction;
        [SerializeField] private InputActionReference toggleAction;

        // Opening, open or closing: the mode owns the controls until the closing presentation has finished.
        public bool IsOpen => _sequence.IsActive;
        public bool IsInteractive => _sequence.IsInteractive;
        public PcBuildModePhase Phase => _sequence.Phase;
        public PcComponentSlot TargetSlot { get; private set; }
        public bool IsGhostVisible => _ghost != null && _ghost.IsVisible;
        internal GameObject GhostRoot => _ghost?.Root;

        private readonly PcBuildModeSequence _sequence = new(ApproachSeconds, CoverSeconds, OverlapSeconds);
        private readonly StringBuilder _status = new(256);

        private PcAssemblyBehaviour _pc;
        private PcBuildPresentation _presentation;
        private PcBuildCamera _camera;
        private PcInstallGhost _ghost;
        private PcComponentSlot _previewSlot;
        private IDisposable _controlBlock;
        private CursorLockMode _previousCursorLockMode;
        private bool _previousCursorVisible;
        private int _openedFrame;

        private void Awake()
        {
            _pc = GetComponent<PcAssemblyBehaviour>();
            _presentation = GetComponent<PcBuildPresentation>();

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _camera = new PcBuildCamera(playerCamera);
            _ghost = new PcInstallGhost(ghostMaterial);
        }

        // The keys are shared with PlayerInteractor, which owns their lifetime; the Workbench only makes sure they listen.
        private void OnEnable()
        {
            installAction.action.Enable();
            removeAction.action.Enable();
            toggleAction.action.Enable();
        }

        // Disabled mid-mode (scene unload, domain teardown): no presentation, just give everything back.
        private void OnDisable()
        {
            if (!IsOpen)
                return;

            _sequence.Reset();
            Finish();
        }

        private void OnDestroy()
        {
            _ghost?.Dispose();
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            // The B press that opened the mode is still "this frame" for the Workbench's own Update.
            if (Time.frameCount != _openedFrame && toggleAction.action.WasPressedThisFrame())
                Close();

            bool wasInteractive = _sequence.IsInteractive;

            if (_sequence.Tick(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds)) == PcBuildModePhase.Closed)
            {
                Finish();
                return;
            }

            if (!_sequence.IsInteractive)
                return;

            if (!wasInteractive)
                Refresh();

            UpdateTarget();

            if (TargetSlot == null)
                return;

            if (installAction.action.WasPressedThisFrame() || ClickedInTheBuildView())
                InstallIntoTarget();
            else if (removeAction.action.WasPressedThisFrame())
                RemoveFromTarget();
        }

        // After the player's own Update, so the PC, the panel and the camera's aim follow the timeline every rendered frame.
        private void LateUpdate()
        {
            if (!IsOpen)
                return;

            _presentation.SetApproach(_sequence.ApproachAmount);
            _presentation.SetCoverOpen(_sequence.CoverAmount);
            _camera.Apply(_sequence.ApproachAmount, _presentation.BuildViewRotation);
            hud.SetPresence(_sequence.Progress, _sequence.IsInteractive);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return context.Action == InteractionAction.Special &&
                   !IsOpen &&
                   isActiveAndEnabled &&
                   _pc.IsReady &&
                   context.Actor == playerController.gameObject;
        }

        public string GetPromptKey(in InteractionContext context)
        {
            return CanInteract(in context) ? EnterPromptKey : null;
        }

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(in context))
                Open();
        }

        public bool Open()
        {
            if (IsOpen || !isActiveAndEnabled || !_pc.IsReady || !parts.Bind(playerInventory, playerCarry, localization, AnnotatePart))
                return false;

            _sequence.Begin();
            _presentation.PlanApproach(playerCamera.transform);
            _openedFrame = Time.frameCount;
            _previousCursorLockMode = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _controlBlock = playerController.Controls.Block(PlayerControlMask.All);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            playerCarry.SetHeldItemHidden(true);

            _pc.Assembly.Changed += HandleAssemblyChanged;
            playerCarry.CarriedItemChanged += Refresh;
            localization.LanguageChanged += HandleLanguageChanged;
            parts.PartClicked += TakePart;
            parts.HeldClicked += PutHeldPartAway;

            Refresh();
            return true;
        }

        // Starts leaving: the presentation plays backwards and Finish gives control back at its end.
        public void Close()
        {
            if (!_sequence.End())
                return;

            TargetSlot = null;
            Refresh();
        }

        // On scene unload OnDisable can run after other scene objects are already destroyed.
        private void Finish()
        {
            _pc.Assembly.Changed -= HandleAssemblyChanged;
            playerCarry.CarriedItemChanged -= Refresh;
            localization.LanguageChanged -= HandleLanguageChanged;
            parts.PartClicked -= TakePart;
            parts.HeldClicked -= PutHeldPartAway;

            if (parts != null)
                parts.Unbind();

            TargetSlot = null;
            ShowGhost(null);

            IReadOnlyList<PcComponentSlot> slots = _pc.Slots;

            for (int i = 0; i < slots.Count; i++)
                slots[i].SetHighlight(PcSlotHighlight.None);

            _camera.Release();
            _presentation.ResetToRest();

            if (hud != null)
                hud.SetPresence(0f, false);

            if (playerCarry != null)
                playerCarry.SetHeldItemHidden(false);

            Cursor.lockState = _previousCursorLockMode;
            Cursor.visible = _previousCursorVisible;

            _controlBlock?.Dispose();
            _controlBlock = null;
        }

        private void UpdateTarget()
        {
            PcComponentSlot target = null;

            if (!IsPointerOverUi())
            {
                Ray ray = playerCamera.ScreenPointToRay(pointerInput.point.action.ReadValue<Vector2>());
                float nearest = MaxPointerDistance;
                IReadOnlyList<PcComponentSlot> slots = _pc.Slots;

                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].Raycast(ray, out float distance) && distance < nearest)
                    {
                        nearest = distance;
                        target = slots[i];
                    }
                }
            }

            if (target == TargetSlot)
                return;

            TargetSlot = target;
            Refresh();
        }

        private void InstallIntoTarget()
        {
            if (_pc.Assembly.IsSlotOccupied(TargetSlot.SlotId))
                return;

            PcSlotCheck check = _pc.CheckInstall(TargetSlot, playerCarry);

            if (check == PcSlotCheck.Allowed)
                _pc.TryInstallCarried(TargetSlot, playerCarry);
            else
                hud.ShowFeedback(localization.Text(check.MessageKey()));
        }

        private void RemoveFromTarget()
        {
            PcSlotCheck check = _pc.CheckRemove(TargetSlot, playerCarry);

            if (check == PcSlotCheck.Allowed)
                _pc.TryRemoveToCarry(TargetSlot, playerCarry);
            else if (check != PcSlotCheck.SlotEmpty)
                hud.ShowFeedback(localization.Text(check.MessageKey()));
        }

        // Parts panel: a click on a PC part takes it into the hands (swapping a storable held item back); anything
        // else stays in the Inventory with a short explanation.
        private void TakePart(string instanceId)
        {
            if (!IsInteractive || !playerInventory.TryGetDefinition(instanceId, out ItemDefinition definition))
                return;

            if (definition.PcComponent == null)
            {
                hud.ShowFeedback(localization.Format(NotPartFeedbackKey, ItemName(definition)));
                return;
            }

            InventoryTransfer transfer = playerInventory.CheckTakeToCarry(instanceId);

            if (transfer.IsAllowed())
                playerInventory.TryTakeToCarry(instanceId);
            else
                hud.ShowFeedback(localization.Text(transfer.MessageKey()));
        }

        private void PutHeldPartAway()
        {
            if (!IsInteractive || !playerCarry.HasItem)
                return;

            InventoryTransfer transfer = playerInventory.CheckStoreCarried();

            if (transfer == InventoryTransfer.Store)
                playerInventory.TryStoreCarriedItem();
            else
                hud.ShowFeedback(localization.Text(transfer.MessageKey()));
        }

        private void HandleAssemblyChanged()
        {
            parts.Render();
            Refresh();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            parts.Render();
            Refresh();
        }

        // Highlights, ghost, PC status and slot card from the current hands, record and target. Runs on changes only.
        private void Refresh()
        {
            if (!IsOpen)
                return;

            WorldItem held = playerCarry.HasItem ? playerCarry.CarriedItem : null;
            PcComponentSpec heldPart = held != null ? held.Definition.PcComponent : null;
            bool interactive = _sequence.IsInteractive;
            IReadOnlyList<PcComponentSlot> slots = _pc.Slots;

            for (int i = 0; i < slots.Count; i++)
                slots[i].SetHighlight(interactive ? HighlightFor(slots[i], heldPart) : PcSlotHighlight.None);

            bool preview = interactive && TargetSlot != null && _pc.CheckInstall(TargetSlot, playerCarry) == PcSlotCheck.Allowed;
            ShowGhost(preview ? held : null);
            RenderStatus();
            RenderCard(held, heldPart);
        }

        // Green where the held part fits, a faint outline on other empty places, nothing over installed parts.
        private PcSlotHighlight HighlightFor(PcComponentSlot slot, PcComponentSpec heldPart)
        {
            if (_pc.Assembly.IsSlotOccupied(slot.SlotId))
                return PcSlotHighlight.None;

            if (heldPart != null && PcAssembly.CheckCompatibility(slot.Spec, heldPart) == PcSlotCheck.Allowed)
                return slot == TargetSlot ? PcSlotHighlight.Targeted : PcSlotHighlight.Compatible;

            return PcSlotHighlight.Empty;
        }

        private void ShowGhost(WorldItem part)
        {
            if (_previewSlot != null && (part == null || _previewSlot != TargetSlot))
            {
                _previewSlot.SetPreview(false);
                _previewSlot = null;
            }

            if (part == null)
            {
                _ghost.Hide();
                return;
            }

            _ghost.Show(part, TargetSlot.InstallAnchor);
            _previewSlot = TargetSlot;
            _previewSlot.SetPreview(true);
        }

        // One line per slot of this PC, then each diagnostic: what it means for the PC in words first (title in the colour
        // of its severity), numbers only where they help (watts). Installed parts are the quiet, smaller lines so what is
        // missing stands out and the panel stays compact beside the close-up.
        private void RenderStatus()
        {
            _status.Clear();
            IReadOnlyList<PcComponentSlot> slots = _pc.Slots;

            for (int i = 0; i < slots.Count; i++)
            {
                string component = localization.Text(slots[i].ComponentType.NameKey());
                bool installed = _pc.TryGetInstalledItem(slots[i], out WorldItem item);

                if (_status.Length > 0)
                    _status.Append('\n');

                _status.Append(installed ? "<size=85%><color=#8FE3A8>" : "<color=#F2C46B>")
                    .Append(component).Append(" — ")
                    .Append(installed ? ItemName(item.Definition) : localization.Text(MissingKey))
                    .Append(installed ? "</color></size>" : "</color>");
            }

            IReadOnlyList<PcDiagnostic> diagnostics = _pc.Capabilities.Diagnostics;

            for (int i = 0; i < diagnostics.Count; i++)
            {
                PcDiagnostic diagnostic = diagnostics[i];

                _status.Append("\n<color=#").Append(diagnostic.Severity == PcDiagnosticSeverity.Blocker ? "F29B8C" : "F2C46B").Append('>')
                    .Append(localization.Text(diagnostic.TitleKey)).Append("</color>")
                    .Append("\n<size=85%><color=#A3ADB9>")
                    .Append(localization.Format(diagnostic.DetailKey, diagnostic.RequiredWatts, diagnostic.AvailableWatts))
                    .Append("</color></size>");
            }

            hud.RenderStatus(localization.Text(TitleKey), _status.ToString());
        }

        private void RenderCard(WorldItem held, PcComponentSpec heldPart)
        {
            string controls = localization.Format(ControlsKey, Binding(installAction), Binding(removeAction), Binding(toggleAction));

            if (TargetSlot == null)
            {
                if (held == null)
                {
                    hud.RenderCard(localization.Text(ChooseTitleKey), localization.Text(ChooseDetailKey), null, PcWorkbenchHudView.Tone.Hint, controls);
                    return;
                }

                PcSlotCheck fit = _pc.Assembly.CheckPart(heldPart, out _);
                hud.RenderCard(
                    ItemName(held.Definition),
                    fit == PcSlotCheck.Allowed ? localization.Text(PointHintKey) : null,
                    fit == PcSlotCheck.Allowed ? null : localization.Text(fit.MessageKey()),
                    PcWorkbenchHudView.Tone.Rejected,
                    controls);
                return;
            }

            string title = localization.Text(TargetSlot.NameLocalizationKey);

            if (_pc.TryGetInstalledItem(TargetSlot, out WorldItem installed))
            {
                PcSlotCheck removal = _pc.CheckRemove(TargetSlot, playerCarry);
                string action = removal == PcSlotCheck.Allowed
                    ? $"[{Binding(removeAction)}] {localization.Text(TargetSlot.ComponentType.RemovePromptKey())}"
                    : localization.Text(removal.MessageKey());

                hud.RenderCard(title, $"{TargetSlot.TechnicalLabel} · {ItemName(installed.Definition)}", action, Tone(removal), controls);
                return;
            }

            PcSlotCheck install = _pc.CheckInstall(TargetSlot, playerCarry);
            string installLine = install == PcSlotCheck.Allowed
                ? $"[{localization.Text(ClickKey)} / {Binding(installAction)}] {localization.Text(TargetSlot.ComponentType.InstallPromptKey())}"
                : localization.Text(install.MessageKey());

            hud.RenderCard(title, $"{TargetSlot.TechnicalLabel} · {localization.Text(EmptySlotKey)}", installLine, Tone(install), controls);
        }

        // Parts panel rows: what each Inventory item means for this PC right now.
        private void AnnotatePart(InventoryItemView row, ItemDefinition definition)
        {
            PcComponentSpec part = definition.PcComponent;

            if (part == null)
            {
                row.SetNote(localization.Text(NotPartKey), NotPartColor);
                row.SetDimmed(true);
                return;
            }

            string component = localization.Text(part.ComponentType.NameKey());

            switch (_pc.Assembly.CheckPart(part, out _))
            {
                case PcSlotCheck.Allowed:
                    row.SetNote(localization.Format(InstallableKey, component), InstallableColor);
                    break;
                case PcSlotCheck.SlotOccupied:
                    row.SetNote(localization.Format(SlotTakenKey, component), UnavailableColor);
                    break;
                default:
                    row.SetNote(localization.Format(NoPlaceKey, component), UnavailableColor);
                    break;
            }
        }

        private bool ClickedInTheBuildView()
        {
            return pointerInput.leftClick.action.WasPressedThisFrame() && !IsPointerOverUi();
        }

        private string ItemName(ItemDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.NameLocalizationKey) ? definition.ItemId : localization.Text(definition.NameLocalizationKey);
        }

        private static PcWorkbenchHudView.Tone Tone(PcSlotCheck check)
        {
            return check switch
            {
                PcSlotCheck.Allowed => PcWorkbenchHudView.Tone.Action,
                PcSlotCheck.NothingInHands or PcSlotCheck.FixedInPlace => PcWorkbenchHudView.Tone.Hint,
                _ => PcWorkbenchHudView.Tone.Rejected
            };
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static string Binding(InputActionReference reference)
        {
            return InputHints.Key(reference.action);
        }

        private bool ValidateConfiguration()
        {
            if (_presentation.IsConfigured(out _) &&
                ghostMaterial != null &&
                playerController != null &&
                playerCarry != null &&
                playerInventory != null &&
                playerCamera != null &&
                hud != null &&
                parts != null &&
                localization != null &&
                pointerInput != null &&
                pointerInput.point != null &&
                pointerInput.point.action != null &&
                pointerInput.leftClick != null &&
                pointerInput.leftClick.action != null &&
                HasAction(installAction) &&
                HasAction(removeAction) &&
                HasAction(toggleAction))
            {
                return true;
            }

            Debug.LogError($"{nameof(PcWorkbenchBehaviour)} on {name} has incomplete configuration.", this);
            return false;
        }

        private static bool HasAction(InputActionReference reference)
        {
            return reference != null && reference.action != null;
        }
    }
}
