using System;
using System.Collections.Generic;
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
        [SerializeField] private DesktopAppCatalog catalog;
        [SerializeField, Min(0)] private float uploadMbps = 5f;
        public DesktopState State { get; private set; }
        public DesktopAppCatalog Catalog => catalog;
        public PcSession Session => session.Session;
        public PcCapabilities Capabilities { get; private set; }
        public float UploadMbps => uploadMbps;
        public bool IsReady { get; private set; }
        public event Action Ready;
        public event Action<string> Feedback;
        private bool _bound;
        private bool _restoring;

        private void Awake()
        {
            if (pc == null || session == null || catalog == null || catalog.ValidationError != null)
            {
                Debug.LogError("Desktop requires an authored PC, session and valid app catalog.", this);
                enabled = false;
                return;
            }
            State = new DesktopState(catalog.Apps);
        }

        private void Update()
        {
            if (!_bound && pc.IsReady) Bind();
            if (IsReady) State.Stream.Tick(Time.deltaTime);
        }

        private void Bind()
        {
            pc.Assembly.Changed += HardwareChanged;
            Session.Changed += PowerChanged;
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
            State.Stream.Abort();
            State.Windows.CloseAll();
            session.Reset();
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

        private void PowerChanged() => State.Stream.RefreshEnvironment(Capabilities, Session.Power == PcPowerState.Running, uploadMbps);

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
            finally { State.EndRestore(); _restoring = false; }
        }
    }
}
