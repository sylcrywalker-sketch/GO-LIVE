using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    public sealed class StreamlyView : DesktopAppView
    {
        [SerializeField] private TMP_InputField channelCode;
        [SerializeField] private Button connect;
        [SerializeField] private Button paste;
        [SerializeField] private Button startStop;
        [SerializeField] private TMP_Text startStopText;
        [SerializeField] private TMP_Text status;
        [SerializeField] private TMP_Text requirements;
        [SerializeField] private TMP_Text preview;
        [SerializeField] private Button[] quality;
        private void Awake()
        {
            connect.onClick.AddListener(() => Result(State.Stream.Connect(channelCode.text), null));
            paste.onClick.AddListener(() => channelCode.text = GUIUtility.systemCopyBuffer);
            startStop.onClick.AddListener(() => Result(State.Stream.State == StreamState.Offline
                ? State.Stream.Start(runtime.Capabilities, runtime.Session.Power == PcPowerState.Running, runtime.UploadMbps)
                : State.Stream.Stop(), null));
            for (int i = 0; i < quality.Length; i++) { StreamQuality value = (StreamQuality)i; quality[i].onClick.AddListener(() => Result(State.Stream.SetQuality(value), null)); }
        }
        protected override void Refresh()
        {
            StreamSession stream = State.Stream;
            status.text = T(stream.IsConnected ? "desktop.stream.connected" : "desktop.stream.not_connected");
            string check = stream.State == StreamState.Offline
                ? stream.CheckStart(runtime.Capabilities, runtime.Session.Power == PcPowerState.Running, runtime.UploadMbps)
                : "desktop.stream.state." + stream.State.ToString().ToLowerInvariant();
            requirements.text = F("desktop.stream.requirements", runtime.UploadMbps) + "\n" + T(check ?? "desktop.stream.ready");
            startStopText.text = T(stream.State == StreamState.Offline ? "desktop.stream.start" : "desktop.stream.stop");
            startStop.interactable = stream.State == StreamState.Offline || stream.State == StreamState.Live;
            connect.interactable = stream.State == StreamState.Offline;
            preview.text = T("desktop.stream.preview") + "\n\n" + (State.Trich.IsRegistered ? State.Trich.Name : T("desktop.trich.your_channel")) + "\n" + T("desktop.stream.state." + stream.State.ToString().ToLowerInvariant());
            for (int i = 0; i < quality.Length; i++) quality[i].interactable = stream.State == StreamState.Offline && (int)stream.Quality != i;
        }
    }
}
