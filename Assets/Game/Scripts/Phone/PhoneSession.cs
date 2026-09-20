using System;
using System.Collections.Generic;

namespace GoLive.Phone
{
    public enum PhoneScreenId
    {
        Home,
        Messages,
        Shop
    }

    public sealed class PhoneSession
    {
        public bool IsOpen { get; private set; }
        public PhoneScreenId CurrentScreen { get; private set; } = PhoneScreenId.Home;

        public event Action Changed;

        private readonly List<PhoneScreenId> _history = new();

        public bool Open()
        {
            if (IsOpen)
                return false;

            IsOpen = true;
            CurrentScreen = PhoneScreenId.Home;
            _history.Clear();

            Changed?.Invoke();
            return true;
        }

        public bool TryNavigate(PhoneScreenId screen)
        {
            if (!IsOpen ||
                screen == CurrentScreen ||
                !Enum.IsDefined(typeof(PhoneScreenId), screen))
            {
                return false;
            }

            _history.Add(CurrentScreen);
            CurrentScreen = screen;

            Changed?.Invoke();
            return true;
        }

        public bool TryBack()
        {
            if (!IsOpen || _history.Count == 0)
                return false;

            int lastIndex = _history.Count - 1;

            CurrentScreen = _history[lastIndex];
            _history.RemoveAt(lastIndex);

            Changed?.Invoke();
            return true;
        }

        public bool Close()
        {
            if (!IsOpen)
                return false;

            IsOpen = false;
            CurrentScreen = PhoneScreenId.Home;
            _history.Clear();

            Changed?.Invoke();
            return true;
        }
    }
}