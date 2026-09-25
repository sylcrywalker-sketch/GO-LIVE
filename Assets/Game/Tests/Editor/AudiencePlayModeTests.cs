using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GoLive.Desktop;
using GoLive.GameTime;
using GoLive.Localization;
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
        public IEnumerator LiveHudFollowsTheAuthoritativeAudienceSimulation()
        {
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(
                "BoxCollider does not support negative scale or size.\n" +
                "The effective box size has been forced positive and is likely to give unexpected collision geometry.\n" +
                "If you absolutely need to use negative scaling you can use the convex MeshCollider. Scene hierarchy path \"Props/SM_Shelf_001\"") + "$"));
            yield return new EnterPlayMode(false);
            yield return Boot();
            yield return FaceCase();
            yield return Press(Key.F);
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "the PC to boot");
            yield return FaceMonitor();
            yield return Press(Key.F);
            yield return SitAndFocus();
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Outline), Is.Null);
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Trich), Is.Null);
            Assert.That(_runtime.State.Outline.CreateAddress("audience.test"), Is.Null);
            Assert.That(_runtime.State.Trich.Register(_runtime.State.Outline, _runtime.State.Outline.Address), Is.Null);
            yield return OpenApp(DesktopAppId.Streamly);
            var view = One<StreamlyView>();
            Type(Field<TMP_InputField>(view, "channelCode"), _runtime.State.Trich.ChannelCode);
            Click(Field<Button>(view, "connect"));
            Click(Field<Button[]>(view, "quality")[1]);

            GameClock clock = One<GameClockBehaviour>().Clock;
            Click(Field<Button>(view, "startStop"));
            yield return PlayModeWait.Until(() => _runtime.State.Stream.State == StreamState.Live, "the Start button to go live");
            AudienceSimulation audience = _runtime.State.Stream.Audience;
            Assert.That(audience.FirstStreamBoost, Is.True);
            Assert.That(audience.PeakViewers, Is.InRange(1, 3), "first-stream discovery: one to three viewers");
            TMP_Text hudViewers = Field<TMP_Text>(_overlay, "statistics");

            // Second-by-second: the HUD always shows the simulation's current value, and it moves both ways.
            var series = new List<int>();
            for (int second = 0; second < 600; second++)
            {
                _runtime.State.Stream.Tick(1, clock.Current.MinuteOfDay);
                yield return null;
                series.Add(audience.CurrentViewers);
                Assert.That(hudViewers.text, Is.EqualTo(_localization.Format("desktop.overlay.count", audience.CurrentViewers)), "second " + second);
                if (second == 119)
                {
                    _localization.SetLanguage(GameLanguage.Russian);
                    yield return CaptureApp("stream-b-ru-01-live-audience-2min", DesktopAppId.Streamly);
                    _localization.SetLanguage(GameLanguage.English);
                    yield return CaptureApp("stream-b-en-01-live-audience-2min", DesktopAppId.Streamly);
                }
            }
            TestContext.WriteLine("AUDIENCE_SERIES " + string.Join(" ", series.Where((_, i) => i % 15 == 0)));
            Assert.That(series.Zip(series.Skip(1), (a, b) => b > a).Any(rose => rose), Is.True, "viewers arrive");
            Assert.That(series.Zip(series.Skip(1), (a, b) => b < a).Any(fell => fell), Is.True, "viewers leave");
            Assert.That(series.Min(), Is.GreaterThanOrEqualTo(0));
            Assert.That(audience.PeakViewers, Is.GreaterThanOrEqualTo(series.Max()), "peak covers every simulated second");
            Assert.That(audience.AverageViewers, Is.EqualTo(audience.ViewerSeconds / audience.SimulatedSeconds).Within(1e-9));
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureApp("stream-b-ru-02-live-audience-10min", DesktopAppId.Streamly);
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureApp("stream-b-en-02-live-audience-10min", DesktopAppId.Streamly);

            yield return StopBroadcast(view);
            Assert.That(_runtime.State.Trich.CompletedStreams, Is.EqualTo(1));
            Assert.That(_runtime.State.Trich.PeakViewers, Is.EqualTo(audience.PeakViewers));
            OutlineMessage mail = _runtime.State.Outline.Messages.Single();
            Assert.That(mail.BodyKey, Is.EqualTo("desktop.mail.stream.body_v2"));
            Assert.That(mail.BodyArguments[2], Is.EqualTo(audience.AverageViewers.ToString("0.0", CultureInfo.InvariantCulture)));
            Assert.That(mail.BodyArguments[3], Is.EqualTo(audience.PeakViewers.ToString(CultureInfo.InvariantCulture)));
            yield return OpenApp(DesktopAppId.Outline);
            var outline = One<OutlineView>();
            object firstMessage = Field<System.Array>(outline, "rows").GetValue(0);
            Click(Field<Button>(firstMessage, "button"));
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureApp("stream-b-ru-03-summary-mail", DesktopAppId.Outline);
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureApp("stream-b-en-03-summary-mail", DesktopAppId.Outline);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
