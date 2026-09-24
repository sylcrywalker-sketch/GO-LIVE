using UnityEngine;

namespace GoLive.GameTime
{
    // Sunlight that comes in through the window lands on the floor and lights the room a second time: a warm bounce light
    // hangs over the sun patch and follows how much of the window the sun reaches through (the courtyard wall and the
    // window reveal block it for part of the day).
    [DisallowMultipleComponent]
    public sealed class WindowSunBounce : MonoBehaviour
    {
        private const int Grid = 3;

        [SerializeField] private Light sun;
        [SerializeField] private Light bounce;

        [Tooltip("Centre of the window opening; +Z points into the room, X runs along the width.")]
        [SerializeField] private Transform opening;
        [SerializeField] private Vector2 openingSize = new(1.4f, 1.3f);

        [Tooltip("World height of the floor the sun patch lands on.")]
        [SerializeField] private float floorHeight = 0.15f;
        [SerializeField, Min(0f)] private float bounceHeight = 0.35f;

        [Tooltip("The bounce light keeps at least this far into the room from the window: a point light next to the wall would paint a hot spot on it.")]
        [SerializeField, Min(0f)] private float minDistanceFromWindow = 0.7f;

        [Tooltip("Bounce intensity per unit of sun intensity when the whole window is sunlit.")]
        [SerializeField, Min(0f)] private float strength = 0.18f;

        [Tooltip("Colour the floor gives the light it bounces.")]
        [SerializeField] private Color surfaceTint = new(1f, 0.8f, 0.62f);

        [Tooltip("What can stand between the window and the sun.")]
        [SerializeField] private LayerMask occluders = 1;

        public float SunlitShare { get; private set; }

        private void Awake()
        {
            if (sun != null && bounce != null && opening != null)
                return;

            Debug.LogError($"{nameof(WindowSunBounce)} on {name} requires a sun, a bounce light and the window opening.", this);
            enabled = false;
        }

        // LateUpdate: DayLightingView moves the sun in Update.
        private void LateUpdate()
        {
            Vector3 travel = sun.transform.forward;
            bool entersRoom = sun.isActiveAndEnabled && sun.intensity > 0f &&
                              Vector3.Dot(travel, opening.forward) > 0.05f && travel.y < -0.05f;

            int lit = 0;
            Vector3 patch = Vector3.zero;

            if (entersRoom)
            {
                for (int x = 0; x < Grid; x++)
                {
                    for (int y = 0; y < Grid; y++)
                    {
                        Vector3 point = opening.position +
                                        opening.right * (((x + 0.5f) / Grid - 0.5f) * openingSize.x) +
                                        opening.up * (((y + 0.5f) / Grid - 0.5f) * openingSize.y);

                        if (Physics.Raycast(point, -travel, 60f, occluders, QueryTriggerInteraction.Ignore))
                            continue;

                        lit++;
                        patch += point + travel * ((floorHeight - point.y) / travel.y);
                    }
                }
            }

            SunlitShare = lit / (float)(Grid * Grid);
            bounce.enabled = lit > 0;

            if (lit == 0)
                return;

            Vector3 position = patch / lit + Vector3.up * bounceHeight;
            float depth = Vector3.Dot(position - opening.position, opening.forward);
            if (depth < minDistanceFromWindow)
                position += opening.forward * (minDistanceFromWindow - depth);

            bounce.transform.position = position;
            bounce.color = sun.color * surfaceTint;
            bounce.intensity = strength * sun.intensity * SunlitShare;
        }
    }
}
