using System;
using GoLive.Desktop;
using UnityEngine;

namespace GoLive.PcBuilding
{
    // Physical monitor emission and desk glow follow the session's two power switches.
    // The screen surface supplies boot/desktop content; this view never owns runtime power.
    [DisallowMultipleComponent]
    public sealed class PcScreenView : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private PcAssemblyBehaviour pc;
        [SerializeField] private PcSessionBehaviour session;

        [Tooltip("The monitor renderer. Its material's emission map is the screen image, black outside the screen.")]
        [SerializeField] private Renderer screen;

        [Tooltip("Emission multiplier of the screen image while the screen is on.")]
        [SerializeField, ColorUsage(false, true)] private Color screenEmission = Color.white;

        [Tooltip("Light the glowing screen throws into the room; on only while the screen is.")]
        [SerializeField] private Light[] glow = Array.Empty<Light>();

        private MaterialPropertyBlock _block;
        private bool _started;
        private bool _subscribed;

        public bool IsOn { get; private set; }

        private void OnEnable()
        {
            if (_started)
                Subscribe();
            else
                Apply(false);
        }

        // Start allows the authored PC and session to initialize their Awake methods in either order.
        private void Start()
        {
            if (pc == null || session == null || screen == null || glow == null || Array.IndexOf(glow, null) >= 0 || pc.Assembly == null)
            {
                Debug.LogError($"{nameof(PcScreenView)} on {name} requires a PC with an assembly, a session, a screen renderer and no missing glow lights.", this);
                Apply(false);
                enabled = false;
                return;
            }

            _started = true;
            Subscribe();
        }

        private void Subscribe()
        {
            if (_subscribed || session == null)
                return;
            _subscribed = true;
            session.Session.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_subscribed && session != null)
                session.Session.Changed -= Refresh;
            _subscribed = false;
            Apply(false);
        }

        private void Refresh()
        {
            Apply(session.Session.MonitorOn && session.Session.Power != PcPowerState.Off);
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

            for (int i = 0; glow != null && i < glow.Length; i++)
            {
                if (glow[i] != null)
                    glow[i].enabled = on;
            }
        }
    }
}
