using System;
using System.Collections.Generic;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.PcBuilding;
using GoLive.Player;
using UnityEngine;

namespace GoLive.Desktop
{
    [DisallowMultipleComponent]
    public sealed class DesktopRuntimeBehaviour : MonoBehaviour
    {
        [SerializeField] private PcAssemblyBehaviour pc;
        [SerializeField] private PcSessionBehaviour session;
        [SerializeField] private PcPeripheralsBehaviour peripherals;
        [SerializeField] private DesktopAppCatalog catalog;
        // The audience follows the game clock; accepted donations are paid into the wallet.
        [SerializeField] private GameClockBehaviour clock;
        [SerializeField] private WalletBehaviour wallet;
        [SerializeField, Min(0)] private float uploadMbps = 5f;
        [SerializeField] private AudienceTuning audienceTuning = new();
        public DesktopState State { get; private set; }
        public DesktopAppCatalog Catalog => catalog;
        public PcPeripheralsBehaviour PeripheralRig => peripherals;
        public PcSession Session => session.Session;
        public PcCapabilities Capabilities { get; private set; }
        public float UploadMbps => uploadMbps;
        public bool IsReady { get; private set; }
        public event Action Ready;
        public event Action<string> Feedback;
        private bool _bound;
        private bool _restoring;
        private DonationPayout _payout;

        private void Awake()
        {
            if (pc == null || session == null || peripherals == null || clock == null || wallet == null ||
                catalog == null || catalog.ValidationError != null)
            {
                Debug.LogError("Desktop requires an authored PC, peripheral rig, session, clock, wallet and valid app catalog.", this);
                enabled = false;
                return;
            }
            string tuningError = audienceTuning?.Validate() ?? "Audience tuning is missing.";
            if (tuningError != null)
            {
                Debug.LogError(tuningError, this);
                enabled = false;
                return;
            }
            State = new DesktopState(catalog.Apps, peripherals.State, audienceTuning);
        }

        private void Update()
        {
            if (!_bound && pc.IsReady && peripherals.IsReady && clock.Clock != null && wallet.Wallet != null) Bind();
            if (IsReady) State.Stream.Tick(Time.deltaTime, clock.Clock.Current.MinuteOfDay);
        }

        private void Bind()
        {
            pc.Assembly.Changed += HardwareChanged;
            Session.Changed += PowerChanged;
            peripherals.State.Changed += PowerChanged;
            _payout = new DonationPayout(State.Donation, wallet.Wallet);
            _bound = true;
            HardwareChanged();
            IsReady = true;
            Ready?.Invoke();
        }

        private void OnDisable()
        {
            if (!_bound) return;
            pc.Assembly.Changed -= HardwareChanged;
            Session.Changed -= PowerChanged;
            peripherals.State.Changed -= PowerChanged;
            State.Stream.Abort();
            State.Windows.CloseAll();
            session.Reset();
            _payout.Dispose();
            _payout = null;
            _bound = false;
            IsReady = false;
        }

        private void OnDestroy() => State?.Dispose();

        private void HardwareChanged()
        {
            if (_restoring) return;
            ProjectHardware(true);
        }

        private void ProjectHardware(bool bootstrap)
        {
            Capabilities = pc.Capabilities;
            var drives = new List<DesktopDrive>();
            foreach (PcInstalledComponent component in pc.Assembly.InstalledComponents)
                if (component.Spec.ComponentType == PcComponentType.Storage)
                    drives.Add(new DesktopDrive(component.InstanceId, component.SlotId, component.Spec.StorageCapacityMiB));
            State.Storage.SetDrives(drives);
            if (bootstrap && drives.Count > 0) Report(State.EnsureSystemApps());
            foreach (DesktopAppDefinition app in catalog.Apps)
                if (!State.Storage.IsInstalled(app.Id)) State.Windows.Close(app.Id);
            PowerChanged();
        }

        private void PowerChanged()
        {
            if (!_restoring) State.Stream.RefreshEnvironment(Capabilities, Session.Power == PcPowerState.Running, uploadMbps);
        }

        public void SetUploadMbps(float value)
        {
            if (!DesktopAccountValidation.FiniteNonnegative(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (uploadMbps == value) return;
            uploadMbps = value;
            if (_bound) PowerChanged();
        }

        public bool Open(DesktopAppId id)
        {
            if (!IsReady || !State.Storage.IsInstalled(id)) { Report("desktop.error.not_installed"); return false; }
            return State.Windows.Open(id);
        }

        public void Report(string error)
        {
            if (!string.IsNullOrEmpty(error)) Feedback?.Invoke(error);
        }

        public void ResetTransient()
        {
            session.Reset();
            State.Stream.Abort();
            State.Windows.CloseAll();
        }

        public PlayerPoseSnapshot CaptureWorldPose() => session.CaptureWorldPose();

        public void BeginRestore()
        {
            _restoring = true;
            State.BeginRestore();
            session.Reset();
        }

        public void EndRestore(bool bootstrapSystemApps = false)
        {
            try { ProjectHardware(bootstrapSystemApps); }
            finally { State.EndRestore(); _restoring = false; PowerChanged(); }
        }
    }
}
