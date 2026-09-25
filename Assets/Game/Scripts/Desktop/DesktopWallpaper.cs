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
            // Vertex lighting gives a calm blue glow in the upper third, with no decorative wallpaper text.
            const int columns=64, rows=36;
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            {
                float u=x/(float)columns,v=y/(float)rows;
                float light=Mathf.Exp(-((u-.57f)*(u-.57f)*4.5f+(v-.99f)*(v-.99f)*3.8f));
                Color shade=Color.Lerp(new Color(.015f,.19f,.42f),new Color(.08f,.60f,.94f),light);
                mesh.AddVert(new Vector3(rect.xMin+u*rect.width,rect.yMin+v*rect.height),shade,Vector2.zero);
                if(x==0||y==0) continue;
                int b=y*(columns+1)+x;
                mesh.AddTriangle(b,b-1,b-columns-2);mesh.AddTriangle(b,b-columns-2,b-columns-1);
            }
            Ribbon(mesh,rect,.055f,.10f,.36f,.41f,.18f,.028f,new Color(.26f,.65f,1,.18f));
            Ribbon(mesh,rect,.22f,.22f,.39f,.65f,.03f,.063f,new Color(.40f,.81f,1,.25f));
            Ribbon(mesh,rect,.59f,.55f,.28f,.20f,.17f,.015f,new Color(.07f,.38f,.80f,.15f));
        }
        private static void Ribbon(VertexHelper mesh,Rect rect,float p0,float p1,float p2,float p3,float w0,float w1,Color color)
        {
            const int steps=160, bands=4;
            int first=mesh.currentVertCount;
            for (int i = 0; i <= steps; i++)
            {
                float t=i/(float)steps,s=1-t;
                float y=s*s*s*p0+3*s*s*t*p1+3*s*t*t*p2+t*t*t*p3;
                float width=Mathf.Lerp(w0,w1,t);
                for(int b=0;b<=bands;b++)
                {
                    float edge=Mathf.Min(.1f,1.5f/(width*rect.height));
                    float fraction=b switch { 0=>0,1=>edge,2=>.55f,3=>1-edge,_=>1 };
                    Color shade=color;
                    shade.a*=(b==0||b==bands?0:Mathf.Lerp(.5f,1,fraction))*Mathf.Lerp(.3f,1,t);
                    mesh.AddVert(new Vector3(rect.xMin+t*rect.width,rect.yMin+(y+width*fraction)*rect.height),shade,Vector2.zero);
                    if(i==0||b==0)continue;
                    int v=first+i*(bands+1)+b;
                    mesh.AddTriangle(v,v-1,v-bands-2);mesh.AddTriangle(v,v-bands-2,v-bands-1);
                }
            }
        }
    }
}
