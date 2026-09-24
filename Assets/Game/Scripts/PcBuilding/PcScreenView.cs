using System;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // The PC's monitor. The screen glows (the idle desktop image in the monitor material's emission map, plus the light it
    // throws on the desk) while the installed hardware can reach a desktop, and goes dark when it can not. Presentation
    // only: a running PC is not a game state yet (see PcPowerOnResult), so the screen follows what the hardware allows
    // until the Desktop owns an on/off state.
    [DisallowMultipleComponent]
    public sealed class PcScreenView : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private PcAssemblyBehaviour pc;

        [Tooltip("The monitor renderer. Its material's emission map is the screen image, black outside the screen.")]
        [SerializeField] private Renderer screen;

        [Tooltip("Emission multiplier of the screen image while the screen is on.")]
        [SerializeField, ColorUsage(false, true)] private Color screenEmission = Color.white;

        [Tooltip("Light the glowing screen throws into the room; on only while the screen is.")]
        [SerializeField] private Light[] glow = Array.Empty<Light>();

        private MaterialPropertyBlock _block;
        private PcAssembly _assembly;

        public bool IsOn { get; private set; }

        // Start, not Awake: PcAssemblyBehaviour builds its record in Awake. Its own Start installs the new-game parts
        // afterwards or before; either way Assembly.Changed brings the screen up to date.
        private void Start()
        {
            if (pc == null || screen == null || Array.IndexOf(glow, null) >= 0 || pc.Assembly == null)
            {
                Debug.LogError($"{nameof(PcScreenView)} on {name} requires a PC with an assembly, a screen renderer and no missing glow lights.", this);
                Apply(false);
                enabled = false;
                return;
            }

            _assembly = pc.Assembly;
            _assembly.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_assembly != null)
                _assembly.Changed -= Refresh;
        }

        private void Refresh()
        {
            Apply(pc.Capabilities.CanUseDesktop);
        }

        private void Apply(bool on)
        {
            IsOn = on;

            if (screen != null)
            {
                _block ??= new MaterialPropertyBlock();
                screen.GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, on ? screenEmission : Color.black);
                screen.SetPropertyBlock(_block);
            }

            for (int i = 0; i < glow.Length; i++)
            {
                if (glow[i] != null)
                    glow[i].enabled = on;
            }
        }
    }
}
