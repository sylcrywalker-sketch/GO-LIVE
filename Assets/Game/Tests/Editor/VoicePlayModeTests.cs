using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GoLive.Desktop;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Voice;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GoLive.Tests
{
    public sealed partial class DesktopFlowPlayModeTests
    {
        [UnityTest, Timeout(360000)]
        public IEnumerator StreamlyShowsRealVoiceStatusSeparateFromTheInGameMicrophone()
        {
            int savedEnabled = PlayerPrefs.GetInt(VoiceInputBehaviour.EnabledKey, 1);
            int savedLanguage = PlayerPrefs.GetInt(VoiceInputBehaviour.LanguageKey, 0);
            try
            {
                ExpectShelfWarning();
                yield return new EnterPlayMode(false);
                yield return Boot();
                yield return OpenLinkedStreamly();
                var view = One<StreamlyView>();
                var voice = One<VoiceInputBehaviour>();
                Assert.That(Field<VoiceInputBehaviour>(_runtime, "voice"), Is.SameAs(voice));
                Assert.That(Field<VoiceInputBehaviour>(view, "voice"), Is.SameAs(voice));
                TMP_Text status = Field<TMP_Text>(view, "voiceStatus");
                TMP_Text language = Field<TMP_Text>(view, "voiceLanguage");
                TMP_Text inGameMicrophone = Field<TMP_Text[]>(view, "readinessTexts")[2];
                Assert.That(voice.IsCapturing, Is.False, "the OS microphone is closed while offline");

                if (!voice.Recognition.Enabled) Click(Field<Button>(view, "voiceToggle"));
                voice.SetLanguage(SpeechLanguage.Auto);
                Assert.That(voice.Recognition.Status, Is.EqualTo(VoiceStatus.Idle));
                _localization.SetLanguage(GameLanguage.Russian);
                yield return PlayModeWait.Frames(2);
                Assert.That(status.text, Is.EqualTo("Голос: включится в эфире"));
                Assert.That(inGameMicrophone.text, Is.EqualTo("Микрофон подключён"), "the in-game microphone row is a separate concept");
                yield return CaptureApp("stream-c-ru-01-voice-ready", DesktopAppId.Streamly);
                _localization.SetLanguage(GameLanguage.English);
                yield return CaptureApp("stream-c-en-01-voice-ready", DesktopAppId.Streamly);
                Assert.That(status.text, Is.EqualTo("Voice: on during streams"));

                // Player control: On/Off and language are persisted as settings, never as game save data.
                Click(Field<Button>(view, "voiceToggle"));
                Assert.That(voice.Recognition.Status, Is.EqualTo(VoiceStatus.Disabled));
                Assert.That(PlayerPrefs.GetInt(VoiceInputBehaviour.EnabledKey), Is.Zero);
                Assert.That(status.text, Is.EqualTo(_localization.Text("desktop.stream.voice.disabled")));
                _localization.SetLanguage(GameLanguage.Russian);
                yield return CaptureApp("stream-c-ru-02-voice-disabled", DesktopAppId.Streamly);
                _localization.SetLanguage(GameLanguage.English);
                yield return CaptureApp("stream-c-en-02-voice-disabled", DesktopAppId.Streamly);
                var languages = new List<string>();
                for (int i = 0; i < 3; i++)
                {
                    languages.Add(language.text);
                    Click(Field<Button>(view, "voiceLanguageToggle"));
                }
                Assert.That(languages, Is.EqualTo(new[] { "Auto RU/EN", "RU", "EN" }));
                Assert.That(voice.Recognition.Language, Is.EqualTo(SpeechLanguage.Auto));

                // A broadcast with recognition off never opens the microphone.
                yield return StartBroadcast(view);
                yield return PlayModeWait.Frames(10);
                Assert.That(voice.IsCapturing, Is.False);
                Assert.That(voice.Recognition.Status, Is.EqualTo(VoiceStatus.Disabled));
                Click(Field<Button>(view, "voiceToggle"));
                yield return PlayModeWait.Until(() => voice.Recognition.Status != VoiceStatus.Idle && voice.Recognition.Status != VoiceStatus.Disabled,
                    "voice to start listening (or report why it cannot)");
                yield return PlayModeWait.Until(() => voice.Recognition.Status != VoiceStatus.Loading, "the speech model to load");
                TestContext.WriteLine("VOICE_STATUS_LIVE " + voice.Recognition.Status + " devices=" + string.Join(",", Microphone.devices));
                if (Microphone.devices.Length == 0) Assert.That(voice.Recognition.Status, Is.EqualTo(VoiceStatus.MicrophoneUnavailable));
                else Assert.That(voice.Recognition.Status, Is.EqualTo(VoiceStatus.Listening), "bundled tiny model and default microphone");
                Assert.That(status.text, Is.EqualTo(_localization.Text(voice.Recognition.Status == VoiceStatus.Listening
                    ? "desktop.stream.voice.listening" : "desktop.stream.voice.microphone_unavailable")));

                // Removing the fictional desk microphone changes readiness only, never real recognition.
                VoiceStatus before = voice.Recognition.Status;
                Assert.That(_runtime.PeripheralRig.State.TryDisconnect(PcPeripheralKind.Microphone), Is.True);
                yield return PlayModeWait.Frames(5);
                Assert.That(Field<TMP_Text[]>(view, "readinessTexts")[2].text, Is.EqualTo(_localization.Text("desktop.stream.readiness.microphone_missing")));
                Assert.That(_runtime.State.Stream.State, Is.EqualTo(StreamState.Live));
                Assert.That(voice.Recognition.Status, Is.EqualTo(before));
                _localization.SetLanguage(GameLanguage.Russian);
                yield return CaptureApp("stream-c-ru-03-live-voice-status", DesktopAppId.Streamly);
                _localization.SetLanguage(GameLanguage.English);
                yield return CaptureApp("stream-c-en-03-live-voice-status", DesktopAppId.Streamly);

                yield return StopBroadcast(view);
                yield return PlayModeWait.Frames(5);
                Assert.That(voice.IsCapturing, Is.False, "stopping the broadcast closes the OS microphone");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                PlayerPrefs.SetInt(VoiceInputBehaviour.EnabledKey, savedEnabled);
                PlayerPrefs.SetInt(VoiceInputBehaviour.LanguageKey, savedLanguage);
            }
        }

        // C15 real-device acceptance. Needs a person and a connected microphone:
        // Test Runner > select this test > Run Selected, then follow the Console instructions.
        [UnityTest, Explicit("Requires a real microphone and a person speaking"), Category("RealMicrophone"), Timeout(600000)]
        public IEnumerator RealMicrophoneRussianAndEnglishReachTheStreamOnlyWhileLive()
        {
            int savedEnabled = PlayerPrefs.GetInt(VoiceInputBehaviour.EnabledKey, 1);
            var report = new StringBuilder();
            try
            {
                ExpectShelfWarning();
                yield return new EnterPlayMode(false);
                yield return Boot();
                Assert.That(Microphone.devices, Is.Not.Empty, "connect a microphone");
                report.AppendLine("Devices: " + string.Join(", ", Microphone.devices));
                yield return OpenLinkedStreamly();
                var view = One<StreamlyView>();
                var voice = One<VoiceInputBehaviour>();
                voice.SetEnabled(true);
                var heard = new List<RecognizedSpeech>();
                var streamed = new List<StreamSpeechEvent>();
                voice.Recognition.Recognized += heard.Add;
                _runtime.State.SpeechFeed.SpeechAdded += streamed.Add;
                yield return StartBroadcast(view);
                yield return PlayModeWait.Until(() => voice.Recognition.Status == VoiceStatus.Listening, "real voice listening");

                foreach ((string expected, string prompt) in new[] {
                    ("ru", "SPEAK RUSSIAN NOW: «Привет чат, сегодня попробуем новый стрим.»"),
                    ("en", "SPEAK ENGLISH NOW: \"Hello chat, let's try this again.\"") })
                {
                    Debug.Log("[GO! LIVE voice acceptance] " + prompt);
                    int count = streamed.Count;
                    yield return PlayModeWait.Until(() => streamed.Skip(count).Any(e => e.Speech.Language == expected), prompt, 90);
                    StreamSpeechEvent result = streamed.Skip(count).First(e => e.Speech.Language == expected);
                    string line = $"LIVE {expected}: \"{result.Speech.Text}\" confidence={result.Speech.Confidence:0.00} recognition={voice.Recognition.LastRecognitionSeconds:0.00}s";
                    report.AppendLine(line);
                    Debug.Log("[GO! LIVE voice acceptance] " + line);
                    yield return _capture.Capture("stream-c-real-mic-" + expected);
                }

                yield return StopBroadcast(view);
                int streamedAtStop = streamed.Count;
                int heardAtStop = heard.Count;
                Debug.Log("[GO! LIVE voice acceptance] Broadcast stopped. SPEAK AGAIN (anything) during the next 15 seconds.");
                float until = Time.realtimeSinceStartup + 15;
                yield return PlayModeWait.Until(() => Time.realtimeSinceStartup >= until, "the post-stop listening window", 30);
                Assert.That(streamed.Count, Is.EqualTo(streamedAtStop), "speech after Stop never enters the stream feed");
                report.AppendLine($"After stop: {heard.Count - heardAtStop} phrase(s) recognized by the pipeline, {streamed.Count - streamedAtStop} entered the stream feed.");
                Assert.That(voice.IsCapturing, Is.False, "the microphone is closed after the broadcast");
            }
            finally
            {
                PlayerPrefs.SetInt(VoiceInputBehaviour.EnabledKey, savedEnabled);
                Directory.CreateDirectory(DesktopVisualCapture.OutputDirectory);
                File.WriteAllText(Path.Combine(DesktopVisualCapture.OutputDirectory, "voice-acceptance.txt"), report.ToString());
                TestContext.WriteLine(report.ToString());
            }
        }

        private static void ExpectShelfWarning() => LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(
            "BoxCollider does not support negative scale or size.\n" +
            "The effective box size has been forced positive and is likely to give unexpected collision geometry.\n" +
            "If you absolutely need to use negative scaling you can use the convex MeshCollider. Scene hierarchy path \"Props/SM_Shelf_001\"") + "$"));

        private IEnumerator OpenLinkedStreamly()
        {
            yield return FaceCase();
            yield return Press(Key.F);
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "the PC to boot");
            yield return FaceMonitor();
            yield return Press(Key.F);
            yield return SitAndFocus();
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(_runtime.State.Outline.CreateAddress("voice.test"), Is.Null);
            Assert.That(_runtime.State.Trich.Register(_runtime.State.Outline, _runtime.State.Outline.Address), Is.Null);
            yield return OpenApp(DesktopAppId.Streamly);
            var view = One<StreamlyView>();
            Type(Field<TMP_InputField>(view, "channelCode"), _runtime.State.Trich.ChannelCode);
            Click(Field<Button>(view, "connect"));
            Click(Field<Button[]>(view, "quality")[1]);
        }
    }
}
