using System;
using System.Collections.Generic;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.PcBuilding;
using GoLive.Player;
using GoLive.Viewers;
using GoLive.Voice;
using GoLive.Shop;
using UnityEngine;
using Unity.Profiling;
using Unity.Profiling.LowLevel;

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
        [SerializeField] private ShopBehaviour shop;
        [SerializeField, Min(0)] private float uploadMbps = 5f;
        [SerializeField] private AudienceTuningConfig audienceTuning;
        [SerializeField] private ViewerCoreConfig viewerCore;
        // Optional real-microphone bridge. Without it (or when it fails) broadcasts simply have no speech events.
        [SerializeField] private VoiceInputBehaviour voice;
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
        private ViewerPromiseGameplayAdapter _promiseGameplay;
        private readonly ProfilerMarker _viewerTick = new(ProfilerCategory.Scripts, "GO! LIVE Viewer Tick");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private ProfilerCounterValue<long> _viewerAllocations = new(ProfilerCategory.Scripts, "GO! LIVE Viewer Allocations", ProfilerMarkerDataUnit.Count);
        private ProfilerRecorder _viewerGcAlloc;
#endif

        private void Awake()
        {
            if (pc == null || session == null || peripherals == null || clock == null || wallet == null ||
                catalog == null || catalog.ValidationError != null)
            {
                Debug.LogError("Desktop requires an authored PC, peripheral rig, session, clock, wallet and valid app catalog.", this);
                enabled = false;
                return;
            }
            string tuningError = audienceTuning == null ? "Audience tuning is missing." : audienceTuning.ValidationError;
            if (tuningError != null)
            {
                Debug.LogError(tuningError, this);
                enabled = false;
                return;
            }
            string viewerError = viewerCore == null ? "Viewer core config is missing." : viewerCore.ValidationError;
            if (viewerError != null)
            {
                Debug.LogError(viewerError, this);
                enabled = false;
                return;
            }
            // The local model is optional: without it (disabled, not running) the chat uses compact fallback lines.
            IViewerLanguageModel model = viewerCore.Model.Enabled ? new OpenAiCompatibleChatModel(viewerCore.Model) : null;
            State = new DesktopState(catalog.Apps, peripherals.State, audienceTuning.Tuning, reactionTuning: viewerCore.Reactions,
                languageModel: model, modelSettings: viewerCore.Model, viewerProfiles: viewerCore.Community.Profiles);
        }

        private void Update()
        {
            if (!_bound && pc.IsReady && peripherals.IsReady && clock.Clock != null && wallet.Wallet != null) Bind();
            if (!IsReady) return;
            State.Stream.Tick(Time.deltaTime, clock.Clock.Current.MinuteOfDay);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Unity's Boehm runtime returns zero for GC.GetAllocatedBytesForCurrentThread. The native
            // GC.Alloc marker measures allocation occurrences (Count), not bytes or its timing Value.
            _viewerGcAlloc.Reset();
            _viewerGcAlloc.Start();
            try
            {
#endif
                using (_viewerTick.Auto())
                {
                    State.Viewers.Tick(new StreamerContext(Session.Usage == PcUsageState.Focused, VoiceListening,
                        clock.Clock.Current.TotalSeconds / 60d, StreamTopic.Community));
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            }
            finally
            {
                if (_viewerGcAlloc.Valid)
                {
                    _viewerGcAlloc.Stop();
                    _viewerAllocations.Value = _viewerGcAlloc.Count > 0 ? _viewerGcAlloc.GetSample(0).Count : 0;
                }
            }
#endif
        }

        private void Bind()
        {
            pc.Assembly.Changed += HardwareChanged;
            Session.Changed += PowerChanged;
            peripherals.State.Changed += PowerChanged;
            _payout = new DonationPayout(State.Donation, wallet.Wallet);
            if (shop != null) _promiseGameplay = new ViewerPromiseGameplayAdapter(State.Viewers, shop.Orders, shop.Products,
                pc.Assembly, peripherals.State, State.Stream, () => clock.Clock.Current.TotalSeconds / 60d);
            State.Stream.Changed += RequestVoice;
            if (voice != null && voice.Recognition != null) voice.Recognition.Recognized += OfferSpeech;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _viewerGcAlloc = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1,
                ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
#endif
            _bound = true;
            HardwareChanged();
            IsReady = true;
            Ready?.Invoke();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_viewerGcAlloc.Valid) _viewerGcAlloc.Dispose();
            _viewerGcAlloc = default;
#endif
            if (!_bound) return;
            pc.Assembly.Changed -= HardwareChanged;
            Session.Changed -= PowerChanged;
            peripherals.State.Changed -= PowerChanged;
            State.Stream.Abort();
            State.Windows.CloseAll();
            session.Reset();
            _payout.Dispose();
            _payout = null;
            _promiseGameplay?.Dispose(); _promiseGameplay = null;
            State.Stream.Changed -= RequestVoice;
            if (voice != null && voice.Recognition != null) voice.Recognition.Recognized -= OfferSpeech;
            if (voice != null) voice.SetListening(false);
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

        // The real microphone is open only while a broadcast is starting or live (and the player allows it).
        private void RequestVoice()
        {
            if (voice != null) voice.SetListening(State.Stream.State == StreamState.Starting || State.Stream.State == StreamState.Live);
        }

        private void OfferSpeech(RecognizedSpeech speech) => State.SpeechFeed.Offer(speech);

        private bool VoiceListening => voice != null && voice.Recognition != null && voice.Recognition.Status == VoiceStatus.Listening;

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
            _promiseGameplay?.BeginRestore();
            State.BeginRestore();
            session.Reset();
        }

        public void EndRestore(bool bootstrapSystemApps = false)
        {
            try { ProjectHardware(bootstrapSystemApps); }
            finally { _promiseGameplay?.EndRestore(); State.EndRestore(); _restoring = false; PowerChanged(); }
        }
    }
}
