using UnityEngine;
using UnityEngine.UI;

namespace GoLive.HUD
{
    /// <summary>Local feathered contrast behind HUD text. Built once using the shared UI material.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    [AddComponentMenu("UI/GO LIVE/HUD Backdrop")]
    public sealed class HudBackdropGraphic : MaskableGraphic
    {
        [SerializeField, Range(0.05f, 0.49f)] private float _feather = 0.24f;
        [SerializeField] private bool _fadeRight;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            const int columns = 24, rows = 14;
            for (int y = 0; y <= rows; y++)
            {
                float v = y / (float)rows;
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns;
                    float featherPixels = Mathf.Min(rect.width, rect.height) * _feather;
                    float edgeX = Mathf.Min(u, 1f - u) * rect.width / Mathf.Max(1f, featherPixels);
                    float edgeY = Mathf.Min(v, 1f - v) * rect.height / Mathf.Max(1f, featherPixels);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeX)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edgeY));
                    if (_fadeRight) alpha *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.65f, 1f, u));
                    Color tint = color;
                    tint.a *= alpha;
                    vh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height), tint, new Vector2(u, v));
                    if (x == columns || y == rows) continue;
                    int i = y * (columns + 1) + x;
                    vh.AddTriangle(i, i + columns + 1, i + 1);
                    vh.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
                }
            }
        }
    }
}
