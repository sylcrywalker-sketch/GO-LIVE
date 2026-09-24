using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace GoLive.Tests
{
    // The virtual keyboard (and mouse) a Play Mode test plays the game with. It owns exactly what it changes and Dispose
    // gives back exactly that: the devices it adds, the two Input System settings that let their input reach the game
    // whichever window has focus, and the enabled state of every other device, which it switches off meanwhile so a hand on
    // the real keyboard or mouse can neither press a key in the test nor move its pointer. Create it in Play Mode: a device
    // added in an earlier Play Mode session no longer delivers input.
    internal sealed class VirtualInput : IDisposable
    {
        public Keyboard Keyboard { get; }
        public Mouse Mouse { get; }

        private readonly InputSettings.EditorInputBehaviorInPlayMode _editorInput;
        private readonly InputSettings.BackgroundBehavior _backgroundBehavior;
        private readonly List<InputDevice> _silenced = new();

        public VirtualInput(bool withMouse)
        {
            _editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            _backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            foreach (InputDevice device in new List<InputDevice>(InputSystem.devices))
            {
                if (!device.enabled)
                    continue;

                InputSystem.DisableDevice(device);
                _silenced.Add(device);
            }

            Keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse = withMouse ? InputSystem.AddDevice<Mouse>() : null;
        }

        public void Dispose()
        {
            InputSystem.RemoveDevice(Keyboard);

            if (Mouse != null)
                InputSystem.RemoveDevice(Mouse);

            foreach (InputDevice device in _silenced)
            {
                if (device.added)
                    InputSystem.EnableDevice(device);
            }

            _silenced.Clear();
            InputSystem.settings.editorInputBehaviorInPlayMode = _editorInput;
            InputSystem.settings.backgroundBehavior = _backgroundBehavior;
        }
    }
}
