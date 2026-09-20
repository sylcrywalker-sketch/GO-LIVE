using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoLive.GameTime
{
    [DisallowMultipleComponent]
    public sealed class DayLightingView : MonoBehaviour
    {
        [Serializable]
        private struct LightingProfile
        {
            [field: SerializeField] public Color SunColor { get; private set; }
            [field: SerializeField, Min(0f)] public float SunIntensity { get; private set; }
            [field: SerializeField] public Color AmbientColor { get; private set; }

            public LightingProfile(Color sunColor, float sunIntensity, Color ambientColor)
            {
                SunColor = sunColor;
                SunIntensity = sunIntensity;
                AmbientColor = ambientColor;
            }
        }

        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private Light sun;
        [SerializeField, Range(0f, 360f)] private float sunYaw = 170f;

        [Header("Morning")]
        [SerializeField] private LightingProfile morning = new(
            new Color(1f, 0.72f, 0.5f),
            0.65f,
            new Color(0.42f, 0.39f, 0.38f));

        [Header("Day")]
        [SerializeField] private LightingProfile day = new(
            new Color(1f, 0.96f, 0.88f),
            1.1f,
            new Color(0.62f, 0.66f, 0.72f));

        [Header("Evening")]
        [SerializeField] private LightingProfile evening = new(
            new Color(1f, 0.48f, 0.28f),
            0.45f,
            new Color(0.28f, 0.24f, 0.27f));

        [Header("Night")]
        [SerializeField] private LightingProfile night = new(
            new Color(0.38f, 0.47f, 0.68f),
            0.08f,
            new Color(0.07f, 0.09f, 0.14f));

        private void Awake()
        {
            if (gameClock != null && sun != null)
                return;

            Debug.LogError($"{nameof(DayLightingView)} on {name} requires Game Clock and Sun references.", this);
            enabled = false;
        }

        private void Update()
        {
            ApplyLighting();
        }

        private void ApplyLighting()
        {
            GameTimeSnapshot time = gameClock.Clock.Current;
            DayPhaseSchedule schedule = gameClock.PhaseSchedule;

            DayPhase currentPhase = schedule.GetPhase(time.MinuteOfDay);
            DayPhase nextPhase = schedule.GetNextPhase(currentPhase);
            float phaseProgress = schedule.GetPhaseProgress(time.MinuteOfDay);

            LightingProfile currentProfile = GetProfile(currentPhase);
            LightingProfile nextProfile = GetProfile(nextPhase);

            sun.color = Color.Lerp(currentProfile.SunColor, nextProfile.SunColor, phaseProgress);
            sun.intensity = Mathf.Lerp(currentProfile.SunIntensity, nextProfile.SunIntensity, phaseProgress);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(currentProfile.AmbientColor, nextProfile.AmbientColor, phaseProgress);

            float normalizedDay = time.MinuteOfDay / (float)GameTimeSnapshot.MinutesPerDay;
            float sunPitch = normalizedDay * 360f - 90f;

            sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
        }

        private LightingProfile GetProfile(DayPhase phase)
        {
            return phase switch
            {
                DayPhase.Morning => morning,
                DayPhase.Day => day,
                DayPhase.Evening => evening,
                DayPhase.Night => night,
                _ => day
            };
        }
    }
}