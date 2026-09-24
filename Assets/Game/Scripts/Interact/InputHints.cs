using System.Text;
using UnityEngine.InputSystem;

namespace GoLive.Interaction
{
    // The one place that turns an action's bindings into the key names shown in player hints. Bindings name physical
    // keys (<Keyboard>/e is the key where E sits), so hints use the Input System's layout names for those keys ("E")
    // instead of the live device's names, which follow the OS keyboard layout (a Russian layout would show "У").
    // Presentation only: bindings, overrides and input handling are untouched.
    public static class InputHints
    {
        private static readonly StringBuilder Names = new(32);

        public static string Key(InputAction action)
        {
            if (action == null)
                return string.Empty;

            Names.Clear();

            foreach (InputBinding binding in action.bindings)
            {
                if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(binding.effectivePath))
                    continue;

                if (Names.Length > 0)
                    Names.Append(" | ");

                Names.Append(InputControlPath.ToHumanReadableString(binding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice));
            }

            // Composite-only actions (a WASD vector) have no single key: keep the Input System's own text.
            return Names.Length > 0 ? Names.ToString() : action.GetBindingDisplayString();
        }
    }
}
