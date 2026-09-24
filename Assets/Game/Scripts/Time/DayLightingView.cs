using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoLive.GameTime
{
    // Time-of-day presentation of the apartment: sun direction and colour, the ambient gradient, the window daylight fills
    // and the apartment lamps, interpolated between the four phase profiles (each profile is the look at the start of its
    // phase). The room reflection probe is re-rendered as the light changes.
    [DisallowMultipleComponent]
    public sealed class DayLightingView : MonoBehaviour
    {
        [Serializable]
        private struct LightingProfile
        {
            [field: SerializeField] public Color SunColor { get; private set; }
            [field: SerializeField, Min(0f)] public float SunIntensity { get; private set; }

            [Tooltip("Degrees above the horizon.")]
            [field: SerializeField, Range(0f, 90f)] public float SunElevation { get; private set; }

            [Tooltip("Compass heading the light travels along (0 = +Z, 90 = +X). The apartment window faces +X.")]
            [field: SerializeField, Range(0f, 360f)] public float SunHeading { get; private set; }

            [Tooltip("Ambient for up-facing surfaces (floor, desk tops).")]
            [field: SerializeField] public Color AmbientSky { get; private set; }
            [field: SerializeField] public Color AmbientEquator { get; private set; }

            [Tooltip("Ambient for down-facing surfaces (ceiling, undersides).")]
            [field: SerializeField] public Color AmbientGround { get; private set; }

            [Tooltip("Scales the authored intensity of the window daylight fills.")]
            [field: SerializeField, Range(0f, 1f)] public float Daylight { get; private set; }

            [Tooltip("Scales the authored intensity of the apartment lamps.")]
            [field: SerializeField, Range(0f, 1f)] public float Lamps { get; private set; }

            public LightingProfile(
                Color sunColor,
                float sunIntensity,
                float sunElevation,
                float sunHeading,
                Color ambientSky,
                Color ambientEquator,
                Color ambientGround,
                float daylight,
                float lamps)
            {
                SunColor = sunColor;
                SunIntensity = sunIntensity;
                SunElevation = sunElevation;
                SunHeading = sunHeading;
                AmbientSky = ambientSky;
                AmbientEquator = ambientEquator;
                AmbientGround = ambientGround;
                Daylight = daylight;
                Lamps = lamps;
            }

            public static LightingProfile Lerp(LightingProfile from, LightingProfile to, float t)
            {
                return new LightingProfile(
                    Color.Lerp(from.SunColor, to.SunColor, t),
                    Mathf.Lerp(from.SunIntensity, to.SunIntensity, t),
                    Mathf.Lerp(from.SunElevation, to.SunElevation, t),
                    Mathf.LerpAngle(from.SunHeading, to.SunHeading, t),
                    Color.Lerp(from.AmbientSky, to.AmbientSky, t),
                    Color.Lerp(from.AmbientEquator, to.AmbientEquator, t),
                    Color.Lerp(from.AmbientGround, to.AmbientGround, t),
                    Mathf.Lerp(from.Daylight, to.Daylight, t),
                    Mathf.Lerp(from.Lamps, to.Lamps, t));
            }
        }

        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private Light sun;

        [Tooltip("Window and courtyard daylight fills; their authored intensity is scaled by the profile's Daylight.")]
        [SerializeField] private Light[] daylightFills = Array.Empty<Light>();

        [Tooltip("Apartment lamps; their authored intensity is scaled by the profile's Lamps.")]
        [SerializeField] private Light[] lamps = Array.Empty<Light>();

        [Tooltip("Realtime probe (refresh via scripting) re-rendered whenever the game time has moved this far.")]
        [SerializeField] private ReflectionProbe roomReflections;
        [SerializeField, Min(1)] private int reflectionRefreshMinutes = 20;

        [Header("Morning (06:00)")]
        [SerializeField] private LightingProfile morning = new(
            new Color(1f, 0.78f, 0.57f),
            2.8f,
            54f,
            290f,
            new Color(0.32f, 0.33f, 0.35f),
            new Color(0.35f, 0.33f, 0.3f),
            new Color(0.31f, 0.28f, 0.24f),
            0.85f,
            0f);

        [Header("Day (10:00)")]
        [SerializeField] private LightingProfile day = new(
            new Color(1f, 0.93f, 0.84f),
            3f,
            66f,
            318f,
            new Color(0.37f, 0.38f, 0.4f),
            new Color(0.39f, 0.37f, 0.345f),
            new Color(0.345f, 0.32f, 0.28f),
            1f,
            0f);

        [Header("Evening (18:00)")]
        [SerializeField] private LightingProfile evening = new(
            new Color(1f, 0.56f, 0.33f),
            1.8f,
            12f,
            95f,
            new Color(0.22f, 0.2f, 0.2f),
            new Color(0.22f, 0.19f, 0.17f),
            new Color(0.18f, 0.15f, 0.13f),
            0.22f,
            0.7f);

        [Header("Night (22:00)")]
        [SerializeField] private LightingProfile night = new(
            new Color(0.55f, 0.64f, 0.88f),
            0.14f,
            38f,
            250f,
            new Color(0.06f, 0.07f, 0.095f),
            new Color(0.055f, 0.055f, 0.065f),
            new Color(0.04f, 0.04f, 0.045f),
            0.03f,
            1f);

        private float[] _fillIntensities;
        private float[] _lampIntensities;
        private long _reflectionsRenderedAt = long.MinValue;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _fillIntensities = AuthoredIntensities(daylightFills);
            _lampIntensities = AuthoredIntensities(lamps);
        }

        private void Update()
        {
            GameTimeSnapshot time = gameClock.Clock.Current;

            ApplyLighting(Evaluate(time));
            RefreshReflections(time);
        }

        private LightingProfile Evaluate(GameTimeSnapshot time)
        {
            DayPhaseSchedule schedule = gameClock.PhaseSchedule;
            DayPhase phase = schedule.GetPhase(time.MinuteOfDay);
            float progress = schedule.GetPhaseProgress(time.MinuteOfDay);

            // Nights stay dark until the approach of dawn instead of brightening from 22:00 on.
            if (phase == DayPhase.Night)
                progress = progress * progress * progress;

            return LightingProfile.Lerp(GetProfile(phase), GetProfile(schedule.GetNextPhase(phase)), progress);
        }

        private void ApplyLighting(LightingProfile profile)
        {
            sun.color = profile.SunColor;
            sun.intensity = profile.SunIntensity;
            sun.transform.rotation = Quaternion.Euler(profile.SunElevation, profile.SunHeading, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = profile.AmbientSky;
            RenderSettings.ambientEquatorColor = profile.AmbientEquator;
            RenderSettings.ambientGroundColor = profile.AmbientGround;

            Scale(daylightFills, _fillIntensities, profile.Daylight);
            Scale(lamps, _lampIntensities, profile.Lamps);
        }

        private void RefreshReflections(GameTimeSnapshot time)
        {
            if (roomReflections == null)
                return;

            if (_reflectionsRenderedAt != long.MinValue &&
                Math.Abs(time.TotalSeconds - _reflectionsRenderedAt) < reflectionRefreshMinutes * GameTimeSnapshot.SecondsPerMinute)
            {
                return;
            }

            roomReflections.RenderProbe();
            _reflectionsRenderedAt = time.TotalSeconds;
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

        private static void Scale(Light[] lights, float[] authored, float factor)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].intensity = authored[i] * factor;
                lights[i].enabled = factor > 0.001f;
            }
        }

        private static float[] AuthoredIntensities(Light[] lights)
        {
            float[] intensities = new float[lights.Length];

            for (int i = 0; i < lights.Length; i++)
                intensities[i] = lights[i].intensity;

            return intensities;
        }

        private bool ValidateConfiguration()
        {
            if (gameClock != null &&
                sun != null &&
                Array.IndexOf(daylightFills, null) < 0 &&
                Array.IndexOf(lamps, null) < 0 &&
                (roomReflections == null || roomReflections.mode == ReflectionProbeMode.Realtime))
            {
                return true;
            }

            Debug.LogError($"{nameof(DayLightingView)} on {name} requires a Game Clock, a Sun, no missing lights in its lists and a realtime (or no) room reflection probe.", this);
            return false;
        }
    }
}
