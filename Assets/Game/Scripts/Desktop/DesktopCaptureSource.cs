using System;
using System.Collections;
using UnityEngine;

namespace GoLive.Desktop
{
    // The PC's broadcast video source: the desktop exactly as the monitor shows it. The desktop is a
    // screen-space overlay, so its only rendered output is the composed screen while the player is focused
    // on the PC. This bridge copies that frame into the one RenderTexture shared by the Streamly preview
    // and the live broadcast. It never substitutes a scene/room camera: away from the desktop it keeps the
    // last desktop frame. The webcam is a separate optional overlay and never replaces this source.
    [DisallowMultipleComponent]
    public sealed class DesktopCaptureSource : MonoBehaviour
    {
        [SerializeField] private DesktopRuntimeBehaviour runtime;
        private readonly WaitForEndOfFrame _endOfFrame = new();
        private RenderTexture _frame;
        private Coroutine _loop;
        private int _previewViewers;
        private bool _hasFrame;
        private bool _failed;

        // Latest captured desktop frame; null while no preview or broadcast needs one.
        public Texture Frame => _hasFrame ? _frame : null;
        // Back-buffer copies are stored top-down on D3D/Metal/Vulkan; consumers sample through this rect.
        public Rect FrameUv => SystemInfo.graphicsUVStartsAtTop ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
        public bool IsAvailable => !_failed && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null
            && runtime != null && runtime.IsReady && (_hasFrame || DesktopOnScreen);
        public bool IsNeeded => _previewViewers > 0 || BroadcastActive;
        public int PreviewViewers => _previewViewers;
        // Raised when Frame or availability changes, never per captured frame.
        public event Action Changed;

        private bool BroadcastActive => runtime != null && runtime.IsReady && runtime.State.Stream.State != StreamState.Offline;
        // Focus requires a running PC with its monitor on, and the shell shows the desktop only while focused.
        private bool DesktopOnScreen => runtime != null && runtime.IsReady && runtime.Session.Usage == PcUsageState.Focused;

        private void OnEnable()
        {
            if (runtime == null)
            {
                Debug.LogError("Desktop capture requires the authored desktop runtime.", this);
                enabled = false;
                return;
            }
            _loop = StartCoroutine(CaptureLoop());
        }

        private void OnDisable()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            ReleaseFrame();
        }

        public void AddPreviewViewer()
        {
            _previewViewers++;
        }

        public void RemovePreviewViewer()
        {
            if (_previewViewers == 0) return;
            _previewViewers--;
            if (!IsNeeded) ReleaseFrame();
        }

        private IEnumerator CaptureLoop()
        {
            while (true)
            {
                yield return _endOfFrame;
                CaptureDisplayedDesktop();
            }
        }

        // End of frame: the composed screen is the desktop output only while the desktop is on screen.
        private void CaptureDisplayedDesktop()
        {
            if (!IsNeeded)
            {
                ReleaseFrame();
                return;
            }
            if (_failed || !DesktopOnScreen || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            bool replaced = EnsureFrame(Screen.width, Screen.height);
            try
            {
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_frame);
                _frame.GenerateMips();
            }
            catch (Exception exception)
            {
                // One diagnostic, then a stable unavailable state: no per-frame exception spam.
                Debug.LogError("Desktop capture is unavailable: " + exception.Message, this);
                _failed = true;
                ReleaseFrame();
                Changed?.Invoke();
                return;
            }
            if (replaced || !_hasFrame)
            {
                _hasFrame = true;
                Changed?.Invoke();
            }
        }

        private bool EnsureFrame(int width, int height)
        {
            if (_frame != null && _frame.width == width && _frame.height == height) return false;
            ReleaseFrame();
            _frame = new RenderTexture(width, height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default)
            {
                name = "Desktop broadcast capture",
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _frame.Create();
            return true;
        }

        private void ReleaseFrame()
        {
            if (_frame == null) return;
            bool had = _hasFrame;
            _hasFrame = false;
            _frame.Release();
            Destroy(_frame);
            _frame = null;
            if (had) Changed?.Invoke();
        }
    }
}
