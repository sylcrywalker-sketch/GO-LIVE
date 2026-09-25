using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    // Code-native vector wallpaper, authored once per canvas rebuild. No textures or per-frame work.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DesktopWallpaper : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            Quad(mesh, rect.xMin, rect.yMin, rect.xMax, rect.yMax,
                new Color(.13f,.29f,.39f), new Color(.40f,.63f,.70f));
            Ribbon(mesh, rect, .10f, .44f, .18f, new Color(.65f,.82f,.80f,.16f));
            Ribbon(mesh, rect, .06f, .30f, .10f, new Color(.91f,.88f,.62f,.16f));
            Ribbon(mesh, rect, .24f, .64f, .035f, new Color(.75f,.94f,.98f,.18f));
        }
        private static void Quad(VertexHelper mesh, float x0, float y0, float x1, float y1, Color bottom, Color top)
        {
            int offset = mesh.currentVertCount;
            mesh.AddVert(new Vector3(x0,y0),bottom,Vector2.zero); mesh.AddVert(new Vector3(x0,y1),top,Vector2.zero);
            mesh.AddVert(new Vector3(x1,y1),top,Vector2.zero); mesh.AddVert(new Vector3(x1,y0),bottom,Vector2.zero);
            mesh.AddTriangle(offset,offset+1,offset+2); mesh.AddTriangle(offset,offset+2,offset+3);
        }
        private static void Ribbon(VertexHelper mesh, Rect rect, float start, float end, float width, Color color)
        {
            for (int i = 0; i <= 64; i++)
            {
                float t = i/64f;
                float y = Mathf.Lerp(start,end,t*t) + Mathf.Sin(t*Mathf.PI)*.12f;
                mesh.AddVert(new Vector3(rect.xMin+t*rect.width, rect.yMin+y*rect.height),color,Vector2.zero);
                mesh.AddVert(new Vector3(rect.xMin+t*rect.width, rect.yMin+(y+width)*rect.height),color,Vector2.zero);
                if (i == 0) continue;
                int v = mesh.currentVertCount-4;
                mesh.AddTriangle(v,v+1,v+3); mesh.AddTriangle(v,v+3,v+2);
            }
        }
    }
}
