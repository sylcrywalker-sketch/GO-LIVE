using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Inventory
{
    public enum InventoryIconShape { Backpack, Phone, Tasks, Broadcast, Map, Settings, Hand, Place, Use }

    /// <summary>Small vector UI symbols. These never represent item artwork.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InventoryOutlineIcon : MaskableGraphic
    {
        [field: SerializeField] public InventoryIconShape Shape { get; set; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            switch (Shape)
            {
                case InventoryIconShape.Backpack:
                    Box(mesh, 7, 9, 25, 28); Box(mesh, 10, 18, 22, 26);
                    Arc(mesh, 16, 9, 5, 0, 180); Line(mesh, 7, 14, 25, 14); Line(mesh, 12, 7, 12, 13); Line(mesh, 20, 7, 20, 13); break;
                case InventoryIconShape.Phone:
                    Box(mesh, 8, 3, 24, 29); Line(mesh, 13, 6, 19, 6); Line(mesh, 14, 26, 18, 26); break;
                case InventoryIconShape.Tasks:
                    Box(mesh, 5, 6, 27, 28); Box(mesh, 11, 3, 21, 8); Line(mesh, 10, 17, 14, 21); Line(mesh, 14, 21, 23, 12); break;
                case InventoryIconShape.Broadcast:
                    Arc(mesh, 16, 13, 4, 0, 360); Line(mesh, 16, 17, 16, 29);
                    Arc(mesh, 16, 13, 9, -50, 50); Arc(mesh, 16, 13, 9, 130, 230);
                    Arc(mesh, 16, 13, 14, -43, 43); Arc(mesh, 16, 13, 14, 137, 223); break;
                case InventoryIconShape.Map:
                    Line(mesh, 3, 6, 12, 3); Line(mesh, 12, 3, 21, 7); Line(mesh, 21, 7, 29, 3);
                    Line(mesh, 3, 6, 3, 28); Line(mesh, 3, 28, 12, 25); Line(mesh, 12, 25, 21, 29);
                    Line(mesh, 21, 29, 29, 25); Line(mesh, 29, 25, 29, 3); Line(mesh, 12, 3, 12, 25); Line(mesh, 21, 7, 21, 29); break;
                case InventoryIconShape.Settings:
                    Arc(mesh, 16, 16, 5, 0, 360);
                    for (int i = 0; i < 32; i++)
                    {
                        float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
                        float r = i % 4 < 2 ? 13 : 10, next = (i + 1) % 4 < 2 ? 13 : 10;
                        Line(mesh, 16 + Mathf.Cos(a) * r, 16 + Mathf.Sin(a) * r, 16 + Mathf.Cos(b) * next, 16 + Mathf.Sin(b) * next);
                    }
                    break;
                case InventoryIconShape.Hand:
                    Line(mesh, 9, 18, 9, 9); Arc(mesh, 12, 9, 3, 180, 360); Line(mesh, 15, 9, 15, 4);
                    Arc(mesh, 18, 4, 3, 180, 360); Line(mesh, 21, 4, 21, 12); Line(mesh, 21, 12, 27, 14);
                    Line(mesh, 27, 14, 25, 25); Line(mesh, 25, 25, 21, 29); Line(mesh, 21, 29, 12, 29);
                    Line(mesh, 12, 29, 4, 18); Line(mesh, 4, 18, 7, 15); Line(mesh, 7, 15, 12, 21); break;
                case InventoryIconShape.Place:
                    Box(mesh, 6, 13, 26, 29); Line(mesh, 16, 2, 16, 21); Line(mesh, 11, 16, 16, 21); Line(mesh, 16, 21, 21, 16); break;
                default:
                    Arc(mesh, 16, 16, 12, 0, 360); Line(mesh, 13, 9, 22, 16); Line(mesh, 22, 16, 13, 23); Line(mesh, 13, 23, 13, 9); break;
            }
        }
        private void Box(VertexHelper mesh, float x, float y, float right, float bottom)
        {
            Line(mesh, x, y, right, y); Line(mesh, right, y, right, bottom);
            Line(mesh, right, bottom, x, bottom); Line(mesh, x, bottom, x, y);
        }
        private void Arc(VertexHelper mesh, float x, float y, float radius, float start, float end)
        {
            int count = Mathf.CeilToInt((end - start) / 12f);
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)count) * Mathf.Deg2Rad;
                float b = Mathf.Lerp(start, end, (i + 1f) / count) * Mathf.Deg2Rad;
                Line(mesh, x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, x + Mathf.Cos(b) * radius, y + Mathf.Sin(b) * radius);
            }
        }
        private void Line(VertexHelper mesh, float x, float y, float u, float v)
        {
            Rect rect = rectTransform.rect;
            Vector2 a = new(rect.xMin + x * rect.width / 32f, rect.yMax - y * rect.height / 32f);
            Vector2 b = new(rect.xMin + u * rect.width / 32f, rect.yMax - v * rect.height / 32f);
            Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * (rect.width / 32f * .7f);
            int first = mesh.currentVertCount;
            mesh.AddVert(a - side, color, Vector2.zero); mesh.AddVert(a + side, color, Vector2.zero);
            mesh.AddVert(b + side, color, Vector2.zero); mesh.AddVert(b - side, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2); mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}