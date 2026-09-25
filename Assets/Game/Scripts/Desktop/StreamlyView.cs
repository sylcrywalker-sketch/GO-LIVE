using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class StreamlyView : DesktopAppView
    {
        [SerializeField] private TMP_InputField channelCode;
        [SerializeField] private Button connect;
        [SerializeField] private TMP_Text connectText;
        [SerializeField] private Button paste;
        [SerializeField] private Button startStop;
        [SerializeField] private TMP_Text startStopText;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Image connectionIndicator;
        [SerializeField] private TMP_Text channelIdentity;
        [SerializeField] private TMP_Text requirements;
        [SerializeField] private TMP_Text upload;
        [SerializeField] private TMP_Text hardware;
        [SerializeField] private TMP_Text streamStatus;
        [SerializeField] private TMP_Text preview;
        [SerializeField] private RawImage previewImage;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Button[] quality;
        // Same order as the authored rows: channel, internet, microphone, webcam, quality.
        [SerializeField] private TMP_Text[] readinessTexts;
        [SerializeField] private DesktopGlyphGraphic[] readinessMarks;
        [SerializeField] private DesktopGlyphGraphic[] readinessWarnings;
        private RenderTexture _previewTexture;
        private RenderTexture _previousTarget;
        private bool _cameraWasEnabled;
        private int _restoreGeneration = -1;

        private void Awake()
        {
            connect.onClick.AddListener(() => ShowResult(State.Stream.IsConnected
                ? State.Stream.Disconnect() : State.Stream.Connect(channelCode.text)));
            paste.onClick.AddListener(() => channelCode.text = GUIUtility.systemCopyBuffer);
            startStop.onClick.AddListener(() => ShowResult(State.Stream.State == StreamState.Offline
                ? State.Stream.Start(runtime.Capabilities, runtime.Session.Power == PcPowerState.Running, runtime.UploadMbps)
                : State.Stream.Stop()));
            for (int i = 0; i < quality.Length; i++)
            {
                StreamQuality value = (StreamQuality)i;
                quality[i].onClick.AddListener(() => ShowResult(State.Stream.SetQuality(value)));
            }
        }

        private void ShowResult(string error)
        {
            Result(error, null);
            feedback.color = new Color(.94f, .72f, .42f);
        }

        protected override void OnEnable()
        {
            BeginPreview();
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            EndPreview();
        }

        // The authored camera owns its scene pose. This view owns only its visible preview resource.
        private void BeginPreview()
        {
            if (previewCamera == null || previewImage == null || _previewTexture != null) return;
            _previousTarget = previewCamera.targetTexture;
            _cameraWasEnabled = previewCamera.enabled;
            _previewTexture = new RenderTexture(960, 540, 24)
            {
                name = "Streamly scene preview",
                filterMode = FilterMode.Bilinear,
                antiAliasing = 1
            };
            _previewTexture.Create();
            previewCamera.targetTexture = _previewTexture;
            previewImage.texture = _previewTexture;
            previewCamera.enabled = true;
        }

        private void EndPreview()
        {
            if (_previewTexture == null) return;
            if (previewCamera != null)
            {
                previewCamera.targetTexture = _previousTarget;
                previewCamera.enabled = _cameraWasEnabled;
            }
            if (previewImage != null) previewImage.texture = null;
            _previewTexture.Release();
            Destroy(_previewTexture);
            _previewTexture = null;
            _previousTarget = null;
        }

        protected override void Refresh()
        {
            StreamSession stream = State.Stream;
            bool offline = stream.State == StreamState.Offline;
            StreamReadiness readiness = stream.EvaluateReadiness(runtime.Capabilities,
                runtime.Session.Power == PcPowerState.Running, runtime.UploadMbps);
            if (_restoreGeneration != State.RestoreGeneration)
            {
                channelCode.SetTextWithoutNotify("");
                _restoreGeneration = State.RestoreGeneration;
            }
            status.text = T(stream.IsConnected ? "desktop.stream.connected" : "desktop.stream.not_connected");
            status.color = stream.IsConnected ? new Color(.42f, .79f, .60f) : new Color(.66f, .69f, .75f);
            connectionIndicator.color = status.color;
            connectText.text = T(stream.IsConnected ? "desktop.stream.disconnect" : "desktop.stream.connect");
            channelIdentity.text = State.Trich.IsRegistered ? State.Trich.Name : T("desktop.stream.channel_empty");
            string check = offline ? readiness.ErrorKey : null;
            requirements.text = T(check ?? (offline ? "desktop.stream.ready"
                : stream.State == StreamState.Live ? "desktop.stream.output_active"
                : "desktop.stream.state." + stream.State.ToString().ToLowerInvariant()));
            requirements.color = check == null ? new Color(.52f, .79f, .64f) : new Color(.96f, .42f, .44f);
            RefreshReadinessRow(0, readiness.ChannelReady,
                T(readiness.ChannelReady ? "desktop.stream.connected" : "desktop.stream.not_connected"));
            RefreshReadinessRow(1, readiness.InternetReady, readiness.InternetReady
                ? F("desktop.stream.readiness.internet_ready", runtime.UploadMbps)
                : T("desktop.stream.readiness.internet_missing"));
            RefreshReadinessRow(2, readiness.MicrophoneReady, T(readiness.MicrophoneReady
                ? "desktop.stream.readiness.microphone_ready" : "desktop.stream.readiness.microphone_missing"));
            RefreshReadinessRow(3, readiness.WebcamReady, T(readiness.WebcamReady
                ? "desktop.stream.readiness.webcam_ready" : "desktop.stream.readiness.webcam_missing"), true);
            RefreshReadinessRow(4, readiness.QualitySupported, T(readiness.QualitySupported
                ? "desktop.stream.readiness.quality_ready" : "desktop.stream.readiness.quality_missing"));
            streamStatus.text = T("desktop.stream.state." + stream.State.ToString().ToLowerInvariant());
            startStopText.text = T(offline ? "desktop.stream.start" : "desktop.stream.stop");
            startStop.interactable = offline && readiness.CanStart || stream.State == StreamState.Live;
            startStop.image.color = offline ? new Color(.42f, .27f, .62f) : new Color(.65f, .19f, .22f);
            connect.interactable = offline;
            channelCode.interactable = paste.interactable = offline && !stream.IsConnected;
            preview.text = T(previewCamera != null ? "desktop.stream.preview_scene" : "desktop.stream.preview_unavailable");
            previewImage.enabled = previewCamera != null;
            for (int i = 0; i < quality.Length; i++)
            {
                quality[i].interactable = offline && (int)stream.Quality != i;
                quality[i].image.color = (int)stream.Quality == i
                    ? new Color(.47f, .31f, .68f) : new Color(.15f, .17f, .22f);
            }
        }

        private void RefreshReadinessRow(int index, bool ready, string text, bool optional = false)
        {
            Color color = ready ? new Color(.42f, .79f, .60f)
                : optional ? new Color(.94f, .76f, .39f) : new Color(.96f, .42f, .44f);
            readinessTexts[index].text = text;
            readinessTexts[index].color = color;
            readinessMarks[index].color = color;
            readinessMarks[index].enabled = ready;
            readinessWarnings[index].color = color;
            readinessWarnings[index].enabled = !ready;
        }
    }
}
