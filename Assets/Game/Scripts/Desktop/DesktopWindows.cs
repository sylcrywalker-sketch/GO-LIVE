using System;
using System.Collections.Generic;

namespace GoLive.Desktop
{
    public sealed class DesktopWindow
    {
        public DesktopAppId AppId { get; }
        public bool IsMinimized { get; internal set; }

        internal DesktopWindow(DesktopAppId appId) => AppId = appId;
    }

    public sealed class DesktopWindows
    {
        private readonly List<DesktopWindow> _windows = new();
        private readonly List<DesktopAppId> _activationOrder = new();

        // Taskbar order is insertion order; visible-window order is back to front.
        public IReadOnlyList<DesktopWindow> Windows { get; }
        public IReadOnlyList<DesktopAppId> ActivationOrder { get; }
        public DesktopAppId? ActiveApp => _activationOrder.Count == 0 ? null : _activationOrder[_activationOrder.Count - 1];
        public event Action Changed;

        public DesktopWindows()
        {
            Windows = _windows.AsReadOnly();
            ActivationOrder = _activationOrder.AsReadOnly();
        }

        public bool Open(DesktopAppId appId)
        {
            if (!Enum.IsDefined(typeof(DesktopAppId), appId))
                return false;
            var existing = Find(appId);
            if (existing != null)
                return Activate(appId);
            _windows.Add(new DesktopWindow(appId));
            _activationOrder.Add(appId);
            Changed?.Invoke();
            return true;
        }

        public bool Close(DesktopAppId appId)
        {
            var window = Find(appId);
            if (window == null)
                return false;
            _windows.Remove(window);
            _activationOrder.Remove(appId);
            Changed?.Invoke();
            return true;
        }

        public bool Minimize(DesktopAppId appId)
        {
            var window = Find(appId);
            if (window == null || window.IsMinimized)
                return false;
            window.IsMinimized = true;
            _activationOrder.Remove(appId);
            Changed?.Invoke();
            return true;
        }

        public bool Activate(DesktopAppId appId)
        {
            var window = Find(appId);
            if (window == null || ActiveApp == appId)
                return false;
            window.IsMinimized = false;
            _activationOrder.Remove(appId);
            _activationOrder.Add(appId);
            Changed?.Invoke();
            return true;
        }

        public bool IsOpen(DesktopAppId appId) => Find(appId) != null;
        public bool IsVisible(DesktopAppId appId) => Find(appId) is { IsMinimized: false };

        public void CloseAll()
        {
            if (_windows.Count == 0)
                return;
            _windows.Clear();
            _activationOrder.Clear();
            Changed?.Invoke();
        }

        private DesktopWindow Find(DesktopAppId appId)
        {
            foreach (var window in _windows)
                if (window.AppId == appId)
                    return window;
            return null;
        }
    }
}
