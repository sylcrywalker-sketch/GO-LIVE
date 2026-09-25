using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    // Four authored vector portraits: cat, owl, robot and frog. Cosmetic selection is owned by Trich.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChannelAvatarGraphic : MaskableGraphic
    {
        [SerializeField, Range(0,3)] private int portrait;
        private readonly Color _ink=new(.16f,.22f,.27f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Color face=portrait switch {0=>new Color(.90f,.65f,.39f),1=>new Color(.75f,.65f,.54f),2=>new Color(.54f,.72f,.79f),_=>new Color(.57f,.72f,.43f)};
            Circle(mesh,.5f,.5f,.47f,new Color(.93f,.94f,.91f));
            if(portrait==0)
            {
                Triangle(mesh,new(.20f,.56f),new(.19f,.88f),new(.44f,.69f),face);
                Triangle(mesh,new(.56f,.69f),new(.81f,.88f),new(.80f,.56f),face);
            }
            Circle(mesh,.5f,.49f,.33f,face);
            if(portrait==2) {Rect(mesh,.20f,.22f,.60f,.55f,face);Rect(mesh,.48f,.75f,.04f,.11f,_ink);Circle(mesh,.50f,.87f,.04f,new Color(.85f,.36f,.29f));}
            float eyeY=portrait==3?.68f:.55f;
            if(portrait==1||portrait==3) {Circle(mesh,.35f,eyeY,.14f,Color.white);Circle(mesh,.65f,eyeY,.14f,Color.white);}
            Circle(mesh,.35f,eyeY,.052f,_ink);Circle(mesh,.65f,eyeY,.052f,_ink);
            Circle(mesh,.365f,eyeY+.015f,.016f,Color.white);Circle(mesh,.665f,eyeY+.015f,.016f,Color.white);
            if(portrait==1) Triangle(mesh,new(.45f,.45f),new(.55f,.45f),new(.5f,.34f),new Color(.86f,.49f,.22f));
            else if(portrait==2) {Rect(mesh,.34f,.33f,.32f,.07f,_ink);for(int i=0;i<3;i++)Rect(mesh,.38f+i*.10f,.345f,.025f,.04f,face);}
            else {Rect(mesh,.36f,.35f,.28f,.025f,_ink);Circle(mesh,.5f,.44f,.026f,_ink);}
            if(portrait==0) {Rect(mesh,.13f,.42f,.16f,.015f,_ink);Rect(mesh,.71f,.42f,.16f,.015f,_ink);}
        }
        private Vector3 Point(Vector2 p) {var r=rectTransform.rect;return new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height);}
        private void Triangle(VertexHelper m,Vector2 a,Vector2 b,Vector2 c,Color color)
        {int start=m.currentVertCount;m.AddVert(Point(a),color,Vector2.zero);m.AddVert(Point(b),color,Vector2.zero);m.AddVert(Point(c),color,Vector2.zero);m.AddTriangle(start,start+1,start+2);}
        private void Rect(VertexHelper m,float x,float y,float w,float h,Color color)
        {Triangle(m,new(x,y),new(x,y+h),new(x+w,y+h),color);Triangle(m,new(x,y),new(x+w,y+h),new(x+w,y),color);}
        private void Circle(VertexHelper m,float x,float y,float radius,Color color)
        {for(int i=0;i<40;i++){float a=i*Mathf.PI/20,b=(i+1)*Mathf.PI/20;Triangle(m,new(x,y),new(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius),new(x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius),color);}}
    }
}
