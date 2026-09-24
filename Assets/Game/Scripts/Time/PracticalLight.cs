using System;
using UnityEngine;

namespace GoLive.GameTime
{
    // One lamp of the apartment (a ceiling light, the desk lamp): its lights and the glowing parts of the fixture, dimmed
    // together by DayLightingView. The authored light intensities and the parts' emission colours are the lamp at full
    // level.
    [DisallowMultipleComponent]
    public sealed class PracticalLight : MonoBehaviour
    {
        [Serializable]
        private struct GlowingPart
        {
            [Tooltip("Its material needs emission enabled.")]
            public Renderer renderer;

            [Tooltip("Emission at full level.")]
            [ColorUsage(false, true)] public Color fullEmission;
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Light[] lights = Array.Empty<Light>();

        [Tooltip("Parts of the fixture that glow with the lamp (bulb, opal glass, the inside of a shade).")]
        [SerializeField] private GlowingPart[] glowingParts = Array.Empty<GlowingPart>();

        private float[] _authoredIntensities;
        private MaterialPropertyBlock _block;
        private float _level = -1f;

        public float Level => Mathf.Max(_level, 0f);

        private void Awake()
        {
            CaptureAuthoredIntensities();
        }

        public void SetLevel(float level)
        {
            level = Mathf.Clamp01(level);
            if (level == _level)
                return;

            CaptureAuthoredIntensities();
            _level = level;

            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].intensity = _authoredIntensities[i] * level;
                lights[i].enabled = level > 0.001f;
            }

            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < glowingParts.Length; i++)
            {
                _block.SetColor(EmissionColorId, glowingParts[i].fullEmission * level);
                glowingParts[i].renderer.SetPropertyBlock(_block);
            }
        }

        public bool IsConfigured()
        {
            if (Array.IndexOf(lights, null) >= 0)
                return false;

            for (int i = 0; i < glowingParts.Length; i++)
            {
                if (glowingParts[i].renderer == null)
                    return false;
            }

            return true;
        }

        // Light intensities are read once, before the first level is applied: they are the authored full level.
        private void CaptureAuthoredIntensities()
        {
            if (_authoredIntensities != null)
                return;

            _authoredIntensities = new float[lights.Length];

            for (int i = 0; i < lights.Length; i++)
                _authoredIntensities[i] = lights[i].intensity;
        }
    }
}
