using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    // Small rounded UI surfaces. Geometry is rebuilt only when the Canvas dirties it.
    public sealed class DesktopPanelGraphic : Image
    {
        [SerializeField] private float radius = 4;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            if (sprite != null) { base.OnPopulateMesh(mesh); return; }
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float round = Mathf.Min(radius, Mathf.Min(r.width, r.height) * .5f);
            int center = 0;
            mesh.AddVert(r.center, color, Vector2.zero);
            const int steps = 8;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 origin = new(corner < 2 ? r.xMax - round : r.xMin + round,
                    corner == 0 || corner == 3 ? r.yMax - round : r.yMin + round);
                for (int i = 0; i <= steps; i++)
                {
                    float a = (90 - corner * 90 - i * 90f / steps) * Mathf.Deg2Rad;
                    Vector2 position = origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * round;
                    mesh.AddVert(position, color, Vector2.zero);
                }
            }
            int count = mesh.currentVertCount - 1;
            for (int i = 0; i < count; i++) mesh.AddTriangle(center, i + 1, (i + 1) % count + 1);
        }
    }
}
