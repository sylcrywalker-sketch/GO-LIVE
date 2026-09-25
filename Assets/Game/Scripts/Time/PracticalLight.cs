using System;
using UnityEngine;

namespace GoLive.GameTime
{
    // One lamp of the apartment (a ceiling light, the desk lamp): its lights and the glowing parts of the fixture, dimmed
    // together by DayLightingView. Everything is authored at full level: each light's full intensity and each part's
    // emission colour. A level only scales those authored values; nothing is read back from the lights at runtime, so
    // the lamp looks the same however often play mode, a reload or an edit comes in between.
    [DisallowMultipleComponent]
    public sealed class PracticalLight : MonoBehaviour
    {
        [Serializable]
        private struct LampLight
        {
            public Light light;

            [Tooltip("Intensity at full level. The level always scales this value; the Light's own Intensity is only its look in the editor.")]
            [Min(0f)] public float fullIntensity;
        }

        [Serializable]
        private struct GlowingPart
        {
            [Tooltip("Its material needs emission enabled.")]
            public Renderer renderer;

            [Tooltip("Emission at full level.")]
            [ColorUsage(false, true)] public Color fullEmission;
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private LampLight[] lights = Array.Empty<LampLight>();

        [Tooltip("Parts of the fixture that glow with the lamp (bulb, opal glass, the inside of a shade).")]
        [SerializeField] private GlowingPart[] glowingParts = Array.Empty<GlowingPart>();

        private MaterialPropertyBlock _block;
        private float _level = -1f;
        private bool _reportedInvalid;

        public float Level => Mathf.Max(_level, 0f);

        public void SetLevel(float level)
        {
            level = Mathf.Clamp01(level);

            if (level == _level)
                return;

            if (!IsConfigured())
            {
                if (!_reportedInvalid)
                    Debug.LogError($"{nameof(PracticalLight)} on {name} has a missing light or glowing part, or an invalid full intensity: its level is not applied.", this);

                _reportedInvalid = true;
                return;
            }

            _level = level;

            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].light.intensity = lights[i].fullIntensity * level;
                lights[i].light.enabled = level > 0.001f;
            }

            _block ??= new MaterialPropertyBlock();

            for (int i = 0; i < glowingParts.Length; i++)
            {
                _block.SetColor(EmissionColorId, glowingParts[i].fullEmission * level);
                glowingParts[i].renderer.SetPropertyBlock(_block);
            }
        }

        // A lamp controls at least one light or glowing part, every light is assigned with a finite, non-negative full
        // intensity and every glowing part has its renderer.
        public bool IsConfigured()
        {
            if (lights == null || glowingParts == null || lights.Length + glowingParts.Length == 0)
                return false;

            for (int i = 0; i < lights.Length; i++)
            {
                float fullIntensity = lights[i].fullIntensity;

                if (lights[i].light == null ||
                    float.IsNaN(fullIntensity) ||
                    float.IsInfinity(fullIntensity) ||
                    fullIntensity < 0f)
                {
                    return false;
                }
            }

            for (int i = 0; i < glowingParts.Length; i++)
            {
                if (glowingParts[i].renderer == null)
                    return false;
            }

            return true;
        }

        // An edited lamp applies its new values with the next level instead of waiting for the level to change.
        private void OnValidate()
        {
            _level = -1f;
            _reportedInvalid = false;
        }
    }
}
