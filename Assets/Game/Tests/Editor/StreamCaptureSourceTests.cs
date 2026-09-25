using System;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.PcBuilding;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Structural guards for the broadcast video source; the real-scene journey proves the captured pixels.
    public sealed class StreamCaptureSourceTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void StreamlyPreviewReferencesTheSharedSourceAndNoCamera()
        {
            Type[] fields = typeof(StreamlyView).GetFields(Instance).Select(field => field.FieldType).ToArray();
            Assert.That(fields, Does.Contain(typeof(DesktopCaptureSource)));
            Assert.That(fields, Has.None.EqualTo(typeof(Camera)), "the room/player camera is never a Streamly source");
            Assert.That(fields.Where(type => typeof(Texture).IsAssignableFrom(type)), Is.Empty, "the view owns no render texture");
        }

        [Test]
        public void MainSourceHasNoCameraOrWebcamDependency()
        {
            Type[] fields = typeof(DesktopCaptureSource).GetFields(Instance).Select(field => field.FieldType).ToArray();
            Assert.That(fields, Has.None.EqualTo(typeof(Camera)));
            Assert.That(fields, Has.None.EqualTo(typeof(WebCamTexture)));
            Assert.That(fields, Has.None.EqualTo(typeof(PcPeripherals)), "webcam presence cannot replace the desktop source");
            Assert.That(fields, Has.None.EqualTo(typeof(PcPeripheralsBehaviour)));
        }

        [Test]
        public void SourceIsNeededOnlyWhileAPreviewIsRegistered()
        {
            var owner = new GameObject("capture test");
            try
            {
                var capture = owner.AddComponent<DesktopCaptureSource>();
                Assert.That(capture.IsNeeded, Is.False);
                Assert.That(capture.Frame, Is.Null);
                Assert.That(capture.IsAvailable, Is.False, "without a ready desktop there is nothing to capture");
                capture.AddPreviewViewer();
                capture.AddPreviewViewer();
                Assert.That(capture.IsNeeded, Is.True);
                capture.RemovePreviewViewer();
                Assert.That(capture.IsNeeded, Is.True, "another preview still uses the source");
                capture.RemovePreviewViewer();
                capture.RemovePreviewViewer();
                Assert.That(capture.PreviewViewers, Is.Zero, "an unmatched release cannot drive the count negative");
                Assert.That(capture.IsNeeded, Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
