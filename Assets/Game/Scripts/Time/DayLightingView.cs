using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GoLive.GameTime
{
    // Time-of-day presentation of the apartment: sun direction and colour, the ambient gradient, the window daylight fills,
    // the apartment's lamps and the night look (cool window light, night colour grade), interpolated between the four
    // phase profiles (each profile is the look at the start of its phase). The room reflection probe is re-rendered as
    // the light changes.
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

            [Tooltip("Level of the kitchen and hallway ceiling lights.")]
            [field: SerializeField, Range(0f, 1f)] public float Lamps { get; private set; }

            [Tooltip("Level of the main room's ceiling light.")]
            [field: SerializeField, Range(0f, 1f)] public float RoomLight { get; private set; }

            [Tooltip("Level of the desk lamp.")]
            [field: SerializeField, Range(0f, 1f)] public float DeskLamp { get; private set; }

            [Tooltip("How much of the night look is in: the cool light through the window and the night colour grade.")]
            [field: SerializeField, Range(0f, 1f)] public float Night { get; private set; }

            public LightingProfile(
                Color sunColor,
                float sunIntensity,
                float sunElevation,
                float sunHeading,
                Color ambientSky,
                Color ambientEquator,
                Color ambientGround,
                float daylight,
                float lamps,
                float roomLight,
                float deskLamp,
                float night)
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
                RoomLight = roomLight;
                DeskLamp = deskLamp;
                Night = night;
            }

            // natural moves the sun, the ambient and the daylight fills; artificial moves the lamps and the night look.
            public static LightingProfile Lerp(LightingProfile from, LightingProfile to, float natural, float artificial)
            {
                return new LightingProfile(
                    Color.Lerp(from.SunColor, to.SunColor, natural),
                    Mathf.Lerp(from.SunIntensity, to.SunIntensity, natural),
                    Mathf.Lerp(from.SunElevation, to.SunElevation, natural),
                    Mathf.LerpAngle(from.SunHeading, to.SunHeading, natural),
                    Color.Lerp(from.AmbientSky, to.AmbientSky, natural),
                    Color.Lerp(from.AmbientEquator, to.AmbientEquator, natural),
                    Color.Lerp(from.AmbientGround, to.AmbientGround, natural),
                    Mathf.Lerp(from.Daylight, to.Daylight, natural),
                    Mathf.Lerp(from.Lamps, to.Lamps, artificial),
                    Mathf.Lerp(from.RoomLight, to.RoomLight, artificial),
                    Mathf.Lerp(from.DeskLamp, to.DeskLamp, artificial),
                    Mathf.Lerp(from.Night, to.Night, artificial));
            }
        }

        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private Light sun;

        [Tooltip("Window and courtyard daylight fills; their authored intensity is scaled by the profile's Daylight.")]
        [SerializeField] private Light[] daylightFills = Array.Empty<Light>();

        [Tooltip("Kitchen and hallway ceiling lights, set to the profile's Lamps level.")]
        [SerializeField] private PracticalLight[] lamps = Array.Empty<PracticalLight>();

        [Tooltip("The main room's ceiling light, set to the profile's Room Light level.")]
        [SerializeField] private PracticalLight[] roomLights = Array.Empty<PracticalLight>();

        [Tooltip("The desk lamp, set to the profile's Desk Lamp level.")]
        [SerializeField] private PracticalLight[] deskLamps = Array.Empty<PracticalLight>();

        [Tooltip("Cool night light through the window and onto the courtyard; authored intensity scaled by the profile's Night.")]
        [SerializeField] private Light[] nightFills = Array.Empty<Light>();

        [Tooltip("Night colour grade; its weight is the profile's Night.")]
        [SerializeField] private Volume nightGrade;

        [Tooltip("Share of the night (22:00 to morning) that keeps the full night look before dawn starts to come in.")]
        [SerializeField, Range(0f, 0.95f)] private float dawnStartsAt = 0.75f;

        [Tooltip("Share of the day (10:00 to evening) before the lamps and the night look start to come in.")]
        [SerializeField, Range(0f, 0.95f)] private float duskStartsAt = 0.75f;

        [Tooltip("Realtime probe (refresh via scripting) re-rendered whenever the game time has moved this far.")]
        [SerializeField] private ReflectionProbe roomReflections;
        [SerializeField, Min(1)] private int reflectionRefreshMinutes = 20;

        [Header("Morning (06:00)")]
        [SerializeField] private LightingProfile morning = new(
            new Color(1f, 0.82f, 0.64f),
            2.8f,
            54f,
            290f,
            new Color(0.32f, 0.33f, 0.35f),
            new Color(0.35f, 0.33f, 0.3f),
            new Color(0.31f, 0.28f, 0.24f),
            0.85f,
            0f,
            0f,
            0f,
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
            0f,
            0f,
            0f,
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
            1f,
            1f,
            1f,
            0.3f);

        [Header("Night (22:00)")]
        [SerializeField] private LightingProfile night = new(
            new Color(0.55f, 0.64f, 0.88f),
            0.14f,
            38f,
            250f,
            new Color(0.2f, 0.195f, 0.19f),
            new Color(0.19f, 0.18f, 0.17f),
            new Color(0.175f, 0.16f, 0.145f),
            0f,
            0.5f,
            0f,
            1f,
            1f);

        private float[] _fillIntensities;
        private float[] _nightFillIntensities;
        private long _reflectionsRenderedAt = long.MinValue;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _fillIntensities = AuthoredIntensities(daylightFills);
            _nightFillIntensities = AuthoredIntensities(nightFills);
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
            float natural = progress;
            float artificial = progress;

            // The night look holds through the dark hours; dawn comes in only over the last part of the night.
            if (phase == DayPhase.Night)
                natural = artificial = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawnStartsAt, 1f, progress));

            // The sun sinks all afternoon, but nobody switches a lamp on at noon: lamps and the night look wait for dusk.
            if (phase == DayPhase.Day)
                artificial = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(duskStartsAt, 1f, progress));

            return LightingProfile.Lerp(GetProfile(phase), GetProfile(schedule.GetNextPhase(phase)), natural, artificial);
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
            Scale(nightFills, _nightFillIntensities, profile.Night);
            SetLevel(lamps, profile.Lamps);
            SetLevel(roomLights, profile.RoomLight);
            SetLevel(deskLamps, profile.DeskLamp);

            if (nightGrade != null)
                nightGrade.weight = profile.Night;
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

        private static void SetLevel(PracticalLight[] fixtures, float level)
        {
            for (int i = 0; i < fixtures.Length; i++)
                fixtures[i].SetLevel(level);
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
                Array.IndexOf(nightFills, null) < 0 &&
                AreConfigured(lamps) &&
                AreConfigured(roomLights) &&
                AreConfigured(deskLamps) &&
                (roomReflections == null || roomReflections.mode == ReflectionProbeMode.Realtime))
            {
                return true;
            }

            Debug.LogError($"{nameof(DayLightingView)} on {name} requires a Game Clock, a Sun, no missing lights or lamps in its lists and a realtime (or no) room reflection probe.", this);
            return false;
        }

        private static bool AreConfigured(PracticalLight[] fixtures)
        {
            for (int i = 0; i < fixtures.Length; i++)
            {
                if (fixtures[i] == null || !fixtures[i].IsConfigured())
                    return false;
            }

            return true;
        }
    }
}
