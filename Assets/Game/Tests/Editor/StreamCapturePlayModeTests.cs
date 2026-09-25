using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using GoLive.Desktop;
using GoLive.Localization;
using GoLive.PcBuilding;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    public sealed partial class DesktopFlowPlayModeTests
    {
        private const string CaptureTextureName = "Desktop broadcast capture";

        [UnityTest, Timeout(360000)]
        public IEnumerator StreamlyPreviewAndLiveShareTheDisplayedDesktopCapture()
        {
            LogAssert.Expect(LogType.Warning, new Regex("^" + Regex.Escape(
                "BoxCollider does not support negative scale or size.\n" +
                "The effective box size has been forced positive and is likely to give unexpected collision geometry.\n" +
                "If you absolutely need to use negative scaling you can use the convex MeshCollider. Scene hierarchy path \"Props/SM_Shelf_001\"") + "$"));
            yield return new EnterPlayMode(false);
            yield return Boot();
            var capture = One<DesktopCaptureSource>();
            var view = One<StreamlyView>();
            Assert.That(Field<DesktopCaptureSource>(view, "capture"), Is.SameAs(capture), "Streamly previews the one shared broadcast source");
            Assert.That(Field<DesktopRuntimeBehaviour>(capture, "runtime"), Is.SameAs(_runtime));
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(camera => camera.name.ToLowerInvariant().Contains("preview")), Is.Empty, "no room/player preview camera remains");
            Assert.That(capture.IsNeeded, Is.False);
            Assert.That(capture.Frame, Is.Null, "no capture resource exists without a preview or broadcast");

            yield return FaceCase();
            yield return Press(Key.F);
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "the PC to boot");
            yield return FaceMonitor();
            yield return Press(Key.F);
            yield return SitAndFocus();
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Streamly), Is.Null);
            Assert.That(_runtime.State.Storage.TryInstall(DesktopAppId.Trich), Is.Null);
            Assert.That(_runtime.State.Outline.CreateAddress("capture.test"), Is.Null);
            Assert.That(_runtime.State.Trich.Register(_runtime.State.Outline, _runtime.State.Outline.Address), Is.Null);

            // Preview: the shared frame is the displayed desktop, in screen orientation.
            yield return OpenApp(DesktopAppId.Streamly);
            RawImage previewImage = Field<RawImage>(view, "previewImage");
            TMP_Text previewLabel = Field<TMP_Text>(view, "preview");
            yield return PlayModeWait.Until(() => capture.Frame != null, "the first captured desktop frame");
            yield return PlayModeWait.Frames(3);
            Assert.That(previewImage.texture, Is.SameAs(capture.Frame));
            Assert.That(previewImage.enabled, Is.True);
            Assert.That(previewImage.uvRect, Is.EqualTo(capture.FrameUv));
            Assert.That(previewLabel.text, Is.EqualTo(_localization.Text("desktop.stream.preview_screen")));
            AssertNoCameraRendersInto(capture.Frame);
            Assert.That(CaptureTextures(), Is.EqualTo(1));
            yield return AssertFrameMirrorsScreen(capture, "desktop with Streamly");
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureApp("stream-a-ru-01-preview-desktop", DesktopAppId.Streamly);
            Assert.That(previewLabel.text, Is.EqualTo("Источник: экран"));
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureApp("stream-a-en-01-preview-desktop", DesktopAppId.Streamly);
            Assert.That(previewLabel.text, Is.EqualTo("Source: screen"));
            long[] streamlyOnly = FrameSignature(capture);

            // Another desktop app changes what the source receives, whichever window is on top.
            yield return OpenApp(DesktopAppId.Web);
            yield return PlayModeWait.Frames(4);
            yield return AssertFrameMirrorsScreen(capture, "browser on top of Streamly");
            Assert.That(SignatureDistance(FrameSignature(capture), streamlyOnly), Is.GreaterThan(4), "opening the browser changes the captured desktop");
            Click(Field<Button>(Presentation(DesktopAppId.Streamly), "task"));
            yield return PlayModeWait.Frames(4);
            Assert.That(_runtime.State.Windows.IsVisible(DesktopAppId.Web), Is.True);
            yield return AssertFrameMirrorsScreen(capture, "Streamly over the open browser");
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CapturePreviewWithOtherApp("stream-a-ru-02-preview-with-browser");
            _localization.SetLanguage(GameLanguage.English);
            yield return CapturePreviewWithOtherApp("stream-a-en-02-preview-with-browser");
            yield return CloseApp(DesktopAppId.Web);

            // Opening and closing the preview never accumulates render textures.
            for (int i = 0; i < 5; i++)
            {
                yield return CloseApp(DesktopAppId.Streamly);
                yield return PlayModeWait.Frames(2);
                Assert.That(capture.IsNeeded, Is.False);
                Assert.That(capture.Frame, Is.Null);
                Assert.That(CaptureTextures(), Is.Zero, "closing the only preview releases the capture texture");
                yield return OpenApp(DesktopAppId.Streamly);
                yield return PlayModeWait.Until(() => capture.Frame != null, "the reopened preview frame");
                Assert.That(CaptureTextures(), Is.EqualTo(1));
                Assert.That(capture.PreviewViewers, Is.EqualTo(1));
            }

            // LIVE: the broadcast keeps the same source alive without the Streamly window.
            Type(Field<TMP_InputField>(view, "channelCode"), _runtime.State.Trich.ChannelCode);
            Click(Field<Button>(view, "connect"));
            Click(Field<Button[]>(view, "quality")[1]);
            Assert.That(_runtime.PeripheralRig.State.HasWebcam, Is.False, "the starter desk has no webcam");
            yield return StartBroadcast(view);
            Texture liveFrame = capture.Frame;
            Assert.That(previewImage.texture, Is.SameAs(liveFrame), "LIVE and preview use one source");
            Assert.That(previewLabel.text, Is.EqualTo(_localization.Text("desktop.stream.preview_screen")), "webcam absence does not change the main source");
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureApp("stream-a-ru-03-live-desktop-without-webcam", DesktopAppId.Streamly);
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureApp("stream-a-en-03-live-desktop-without-webcam", DesktopAppId.Streamly);
            yield return CloseApp(DesktopAppId.Streamly);
            yield return PlayModeWait.Frames(3);
            Assert.That(capture.IsNeeded, Is.True, "an active broadcast needs its source without the preview");
            Assert.That(capture.Frame, Is.SameAs(liveFrame));
            yield return OpenApp(DesktopAppId.Trich);
            yield return PlayModeWait.Frames(4);
            yield return AssertFrameMirrorsScreen(capture, "live broadcast of Trich");
            yield return CloseApp(DesktopAppId.Trich);

            // Webcam is an optional overlay: connecting it keeps the desktop as the main source.
            Assert.That(_runtime.PeripheralRig.State.TryConnect(PcPeripheralKind.Webcam, "capture-test-webcam"), Is.True);
            yield return OpenApp(DesktopAppId.Streamly);
            yield return PlayModeWait.Frames(3);
            Assert.That(previewImage.texture, Is.SameAs(liveFrame), "webcam presence does not replace the main source");
            Assert.That(previewLabel.text, Is.EqualTo(_localization.Text("desktop.stream.preview_screen")));
            yield return AssertFrameMirrorsScreen(capture, "live desktop with webcam connected");
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureApp("stream-a-ru-04-live-desktop-with-webcam", DesktopAppId.Streamly);
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureApp("stream-a-en-04-live-desktop-with-webcam", DesktopAppId.Streamly);
            Assert.That(_runtime.PeripheralRig.State.TryDisconnect(PcPeripheralKind.Webcam), Is.True);
            Assert.That(_runtime.State.Stream.State, Is.EqualTo(StreamState.Live));

            // Leaving the desk: the source holds the last desktop frame and never captures the room.
            yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Seated));
            yield return PlayModeWait.Frames(2);
            long[] lastDesktopFrame = FrameSignature(capture);
            yield return Press(Key.Escape);
            Assert.That(_session.Session.Usage, Is.EqualTo(PcUsageState.Standing));
            yield return PlayModeWait.Frames(10);
            Assert.That(capture.Frame, Is.SameAs(liveFrame));
            Assert.That(capture.IsAvailable, Is.True, "the live source keeps its last desktop frame");
            Assert.That(SignatureDistance(FrameSignature(capture), lastDesktopFrame), Is.Zero, "no room/player camera frame enters the broadcast");
            yield return SitAndFocus();
            yield return OpenApp(DesktopAppId.Streamly);
            yield return StopBroadcast(view);
            yield return CloseApp(DesktopAppId.Streamly);
            yield return PlayModeWait.Frames(2);
            Assert.That(capture.Frame, Is.Null);
            Assert.That(CaptureTextures(), Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator CapturePreviewWithOtherApp(string name)
        {
            Assert.That(_runtime.State.Windows.ActiveApp, Is.EqualTo(DesktopAppId.Streamly));
            Canvas.ForceUpdateCanvases();
            yield return _capture.Capture(name);
        }

        private static int CaptureTextures() => Resources.FindObjectsOfTypeAll<RenderTexture>().Count(texture => texture.name == CaptureTextureName);

        private static void AssertNoCameraRendersInto(Texture frame)
        {
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert.That(camera.targetTexture, Is.Not.SameAs(frame), camera.name + " must not render into the broadcast source");
        }

        // Compares the captured frame (through its published UV rect) against an independent end-of-frame
        // screenshot. The preview shows at most one frame of latency, so content must match the screen
        // far better than the same screen flipped vertically.
        private static IEnumerator AssertFrameMirrorsScreen(DesktopCaptureSource capture, string state)
        {
            yield return new WaitForEndOfFrame();
            Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
            Texture2D frame = ReadFrame(capture, screen.width, screen.height);
            try
            {
                Color32[] a = frame.GetPixels32();
                Color32[] b = screen.GetPixels32();
                double same = BlockDifference(a, b, screen.width, screen.height, false);
                double flipped = BlockDifference(a, b, screen.width, screen.height, true);
                TestContext.WriteLine($"CAPTURE_MIRROR {state} same={same:0.00} flipped={flipped:0.00}");
                Assert.That(same, Is.LessThan(18), state + ": the broadcast frame shows the displayed desktop");
                Assert.That(same, Is.LessThan(flipped * .6), state + ": the broadcast frame is upright");
            }
            finally
            {
                Object.Destroy(screen);
                Object.Destroy(frame);
            }
        }

        private static Texture2D ReadFrame(DesktopCaptureSource capture, int width, int height)
        {
            Rect uv = capture.FrameUv;
            RenderTexture previous = RenderTexture.active;
            RenderTexture upright = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(capture.Frame, upright, new Vector2(uv.width, uv.height), new Vector2(uv.x, uv.y));
                RenderTexture.active = upright;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                return pixels;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(upright);
            }
        }

        // Mean absolute RGB difference of 48 x 27 block averages.
        private static double BlockDifference(Color32[] a, Color32[] b, int width, int height, bool flipB)
        {
            const int columns = 48, rows = 27;
            double total = 0;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Vector3 meanA = BlockMean(a, width, height, column, row, columns, rows, false);
                Vector3 meanB = BlockMean(b, width, height, column, row, columns, rows, flipB);
                total += Mathf.Abs(meanA.x - meanB.x) + Mathf.Abs(meanA.y - meanB.y) + Mathf.Abs(meanA.z - meanB.z);
            }
            return total / (columns * rows * 3);
        }

        private static Vector3 BlockMean(Color32[] pixels, int width, int height, int column, int row, int columns, int rows, bool flip)
        {
            int x0 = column * width / columns, x1 = (column + 1) * width / columns;
            int y0 = row * height / rows, y1 = (row + 1) * height / rows;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int y = y0; y < y1; y += 2)
            for (int x = x0; x < x1; x += 2)
            {
                Color32 pixel = pixels[(flip ? height - 1 - y : y) * width + x];
                sum += new Vector3(pixel.r, pixel.g, pixel.b);
                count++;
            }
            return sum / Mathf.Max(1, count);
        }

        private static long[] FrameSignature(DesktopCaptureSource capture)
        {
            Assert.That(capture.Frame, Is.Not.Null);
            Texture2D frame = ReadFrame(capture, 96, 54);
            try
            {
                Color32[] pixels = frame.GetPixels32();
                var signature = new long[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) signature[i] = pixels[i].r + pixels[i].g + pixels[i].b;
                return signature;
            }
            finally { Object.Destroy(frame); }
        }

        private static double SignatureDistance(long[] a, long[] b)
        {
            double total = 0;
            for (int i = 0; i < a.Length; i++) total += System.Math.Abs(a[i] - b[i]);
            return total / a.Length;
        }
    }
}
