using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Test-only Editor inspection. Captures the real Game View including overlays; never a replacement camera,
    // an assembled mockup, or a resized low-resolution image. The temporary resolution preset is removed on exit.
    internal sealed class DesktopVisualCapture : IDisposable
    {
        internal static string OutputDirectory
        {
            get
            {
                string configured = Environment.GetEnvironmentVariable("GO_LIVE_VISUAL_OUTPUT");
                return string.IsNullOrWhiteSpace(configured)
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/DesktopVisualAcceptance"))
                    : Path.GetFullPath(configured);
            }
        }
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly EditorWindow _view;
        private readonly Type _viewType;
        private readonly object _sizes;
        private readonly object _group;
        private readonly int _previousIndex;
        private readonly bool _previousLowResolution;
        private readonly bool _createdWindow;
        private readonly string _presetName;
        private bool _capturing;
        private bool _disposed;

        internal DesktopVisualCapture()
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null), "Real Game View capture requires a graphics device; run without -nographics.");
            Assembly editor = typeof(EditorWindow).Assembly;
            _viewType = editor.GetType("UnityEditor.GameView", true);
            _createdWindow = Resources.FindObjectsOfTypeAll(_viewType).Length == 0;
            _view = EditorWindow.GetWindow(_viewType);
            _previousIndex = (int)Property(_viewType, "selectedSizeIndex").GetValue(_view);
            _previousLowResolution = (bool)Property(_viewType, "lowResolutionForAspectRatios").GetValue(_view);
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes", true);
            _sizes = sizesType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
            _group = Property(sizesType, "currentGroup").GetValue(_sizes);
            _presetName = "GO LIVE visual test " + Guid.NewGuid().ToString("N");
            Method(_viewType, "SetCustomResolution").Invoke(_view, new object[] { new Vector2(1920, 1080), _presetName });
            Property(_viewType, "lowResolutionForAspectRatios").SetValue(_view, false);
            _view.Show();
            _view.Focus();
            _view.Repaint();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        internal IEnumerator WaitForResolution()
        {
            yield return PlayModeWait.Until(() => Screen.width == 1920 && Screen.height == 1080,
                "the actual Game View to render 1920 x 1080 (not supersampling or a probe)");
            yield return PlayModeWait.Frames(3);
        }

        internal IEnumerator Capture(string name)
        {
            Assert.That(_disposed || _capturing, Is.False, "captures must be sequential and own their Editor setup");
            Assert.That(name, Does.Match("^[a-z0-9-]+$"));
            Assert.That(Screen.width, Is.EqualTo(1920));
            Assert.That(Screen.height, Is.EqualTo(1080));
            _capturing = true;
            Directory.CreateDirectory(OutputDirectory);
            string temporary = Path.Combine(OutputDirectory, name + "." + Guid.NewGuid().ToString("N") + ".png");
            string destination = Path.Combine(OutputDirectory, name + ".png");
            try
            {
                // Ordinary game frames only: a forced Game view repaint or QueuePlayerLoopUpdate runs an extra frame at
                // the Editor window's DPI (3840 x 2160 at 200% display scaling), which the game then sees as a screen
                // resize.
                Canvas.ForceUpdateCanvases();
                yield return PlayModeWait.Frames(3);
                // The game's own frame at its render resolution; the file-based ScreenCapture.CaptureScreenshot of an
                // Editor Game view scales with the Editor window's DPI (3840 px wide at 200% display scaling).
                Texture2D frame = null;
                yield return DisplayedFrameReader.Read(read => frame = read);
                Assert.That(frame, Is.Not.Null, name + " was read at the end of a game frame");
                byte[] png;
                try { png = frame.EncodeToPNG(); }
                finally { Object.Destroy(frame); }
                File.WriteAllBytes(temporary, png);
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(ImageConversion.LoadImage(decoded, png), Is.True, name + " is a decodable PNG");
                    Assert.That(decoded.width, Is.EqualTo(1920), name);
                    Assert.That(decoded.height, Is.EqualTo(1080), name);
                }
                finally { Object.DestroyImmediate(decoded); }
                File.Copy(temporary, destination, true);
                File.AppendAllText(Path.Combine(OutputDirectory, "capture-manifest.txt"),
                    DateTime.UtcNow.ToString("O") + " 1920x1080 frame=" + Time.frameCount + " " + destination + Environment.NewLine);
                TestContext.WriteLine("GAME_VIEW_CAPTURE " + destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                _capturing = false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_view != null)
            {
                Method(_viewType, "SizeSelectionCallback").Invoke(_view, new object[] { _previousIndex, null });
                Property(_viewType, "lowResolutionForAspectRatios").SetValue(_view, _previousLowResolution);
            }
            int count = (int)Method(_group.GetType(), "GetTotalCount").Invoke(_group, null);
            for (int i = count - 1; i >= 0; i--)
            {
                object size = Method(_group.GetType(), "GetGameViewSize").Invoke(_group, new object[] { i });
                string text = (string)Property(size.GetType(), "displayText").GetValue(size);
                if (!text.StartsWith(_presetName, StringComparison.Ordinal)) continue;
                Method(_group.GetType(), "RemoveCustomSize").Invoke(_group, new object[] { i });
                break;
            }
            Method(_sizes.GetType(), "SaveToHDD").Invoke(_sizes, null);
            if (_view != null)
            {
                _view.Repaint();
                if (_createdWindow) _view.Close();
            }
        }

        private static PropertyInfo Property(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, InstanceFlags);
            Assert.That(property, Is.Not.Null, "Unity Editor API changed: " + type.FullName + "." + name);
            return property;
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, InstanceFlags);
            Assert.That(method, Is.Not.Null, "Unity Editor API changed: " + type.FullName + "." + name);
            return method;
        }
    }
}
