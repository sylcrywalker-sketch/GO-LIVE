using UnityEngine;
using UnityEngine.UI;

namespace GoLive.PcBuilding
{
    // Small monochrome hardware drawings and status marks. Meshes avoid font-dependent check/cross glyphs.
    [AddComponentMenu("GO! LIVE/UI/PC hardware glyph")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PcHardwareGlyph : MaskableGraphic
    {
        public enum Shape { Motherboard, Cpu, Ram, Psu, Storage, Gpu, Check, Cross, Warning }

        [SerializeField] private Shape shape;

        public void Show(Shape value, Color tint)
        {
            if (shape != value)
            {
                shape = value;
                SetVerticesDirty();
            }
            color = tint;
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (shape)
            {
                case Shape.Motherboard:
                    Box(mesh, .13f, .08f, .74f, .84f, .06f);
                    Box(mesh, .3f, .43f, .3f, .31f, .045f);
                    for (int i = 0; i < 3; i++)
                        Stroke(mesh, .68f + i * .065f, .38f, .68f + i * .065f, .81f, .034f);
                    Stroke(mesh, .28f, .25f, .62f, .25f, .045f);
                    Stroke(mesh, .28f, .15f, .62f, .15f, .045f);
                    Stroke(mesh, .17f, .54f, .23f, .54f, .11f);
                    break;
                case Shape.Cpu:
                    Box(mesh, .23f, .23f, .54f, .54f, .06f);
                    Box(mesh, .34f, .34f, .32f, .32f, .035f);
                    for (int i = 0; i < 5; i++)
                    {
                        float p = .3f + i * .1f;
                        Stroke(mesh, p, .12f, p, .24f, .04f);
                        Stroke(mesh, p, .76f, p, .88f, .04f);
                        Stroke(mesh, .12f, p, .24f, p, .04f);
                        Stroke(mesh, .76f, p, .88f, p, .04f);
                    }
                    break;
                case Shape.Ram:
                    Box(mesh, .07f, .34f, .86f, .35f, .055f);
                    for (int i = 0; i < 4; i++)
                        Box(mesh, .17f + i * .19f, .43f, .10f, .17f, .038f);
                    for (int i = 0; i < 8; i++)
                        Stroke(mesh, .17f + i * .095f, .26f, .17f + i * .095f, .34f, .035f);
                    break;
                case Shape.Psu:
                    Box(mesh, .1f, .18f, .8f, .62f, .06f);
                    Fan(mesh, .39f, .49f, .22f);
                    Box(mesh, .72f, .48f, .08f, .16f, .035f);
                    Stroke(mesh, .74f, .31f, .8f, .31f, .045f);
                    break;
                case Shape.Storage:
                    Box(mesh, .23f, .08f, .54f, .84f, .065f);
                    Stroke(mesh, .32f, .81f, .35f, .81f, .045f);
                    Stroke(mesh, .65f, .81f, .68f, .81f, .045f);
                    Stroke(mesh, .32f, .25f, .68f, .25f, .04f);
                    for (int i = 0; i < 5; i++)
                        Stroke(mesh, .36f + i * .07f, .10f, .36f + i * .07f, .18f, .035f);
                    break;
                case Shape.Gpu:
                    Box(mesh, .1f, .27f, .83f, .48f, .055f);
                    Fan(mesh, .33f, .51f, .155f);
                    Fan(mesh, .71f, .51f, .155f);
                    Stroke(mesh, .04f, .2f, .04f, .82f, .055f);
                    Stroke(mesh, .23f, .19f, .6f, .19f, .065f);
                    break;
                case Shape.Check:
                    Stroke(mesh, .17f, .47f, .39f, .24f, .13f);
                    Stroke(mesh, .39f, .24f, .86f, .80f, .13f);
                    break;
                case Shape.Cross:
                    Stroke(mesh, .24f, .24f, .77f, .77f, .12f);
                    Stroke(mesh, .24f, .77f, .77f, .24f, .12f);
                    break;
                case Shape.Warning:
                    Stroke(mesh, .12f, .14f, .5f, .85f, .065f);
                    Stroke(mesh, .5f, .85f, .88f, .14f, .065f);
                    Stroke(mesh, .12f, .14f, .88f, .14f, .065f);
                    Stroke(mesh, .5f, .39f, .5f, .61f, .075f);
                    Stroke(mesh, .5f, .25f, .5f, .29f, .075f);
                    break;
            }
        }

        private void Box(VertexHelper mesh, float x, float y, float width, float height, float weight)
        {
            Stroke(mesh, x, y, x + width, y, weight);
            Stroke(mesh, x + width, y, x + width, y + height, weight);
            Stroke(mesh, x + width, y + height, x, y + height, weight);
            Stroke(mesh, x, y + height, x, y, weight);
        }

        private void Fan(VertexHelper mesh, float x, float y, float radius)
        {
            const int segments = 20;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Stroke(mesh, x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius,
                    x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius, .035f);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                Stroke(mesh, x + Mathf.Cos(a) * radius * .2f, y + Mathf.Sin(a) * radius * .2f,
                    x + Mathf.Cos(a + .65f) * radius * .78f, y + Mathf.Sin(a + .65f) * radius * .78f, .04f);
            }
        }

        private void Stroke(VertexHelper mesh, float x0, float y0, float x1, float y1, float weight)
        {
            Rect rect = GetPixelAdjustedRect();
            float side = Mathf.Min(rect.width, rect.height);
            Vector2 origin = rect.center - Vector2.one * (side * .5f);
            Vector2 a = origin + new Vector2(x0, y0) * side;
            Vector2 b = origin + new Vector2(x1, y1) * side;
            Vector2 delta = b - a;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (weight * side * .5f);
            int start = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
