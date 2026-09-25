using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    // Stable cosmetic IDs: default silhouette, owl, robot and frog. Trich owns the saved selection.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ChannelAvatarGraphic : MaskableGraphic
    {
        [SerializeField, Range(0,3)] private int portrait;
        private readonly Color _ink=new(.16f,.22f,.27f);
        public void SetPortrait(int value)
        {
            value = Mathf.Clamp(value, 0, 3);
            if (portrait == value) return;
            portrait = value;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (portrait == 0)
            {
                Circle(mesh, .5f, .5f, .49f, new Color(.24f, .27f, .33f));
                Circle(mesh, .5f, .5f, .475f, new Color(.095f, .11f, .14f));
                Color silhouette = new(.34f, .38f, .45f);
                Circle(mesh, .5f, .60f, .15f, silhouette);
                // A rounded shoulder contour keeps the account default recognizable at small sizes.
                for (int i = 0; i < 24; i++)
                {
                    float a = i * Mathf.PI / 24f;
                    float b = (i + 1) * Mathf.PI / 24f;
                    Triangle(mesh, new(.5f, .22f),
                        new(.5f + Mathf.Cos(a) * .26f, .22f + Mathf.Sin(a) * .18f),
                        new(.5f + Mathf.Cos(b) * .26f, .22f + Mathf.Sin(b) * .18f), silhouette);
                }
                Rect(mesh, .24f, .18f, .52f, .04f, silhouette);
                return;
            }
            Color face=portrait switch {1=>new Color(.75f,.65f,.54f),2=>new Color(.54f,.72f,.79f),_=>new Color(.57f,.72f,.43f)};
            Circle(mesh,.5f,.5f,.47f,new Color(.93f,.94f,.91f));
            Circle(mesh,.5f,.49f,.33f,face);
            if(portrait==2) {Rect(mesh,.20f,.22f,.60f,.55f,face);Rect(mesh,.48f,.75f,.04f,.11f,_ink);Circle(mesh,.50f,.87f,.04f,new Color(.85f,.36f,.29f));}
            float eyeY=portrait==3?.68f:.55f;
            if(portrait==1||portrait==3) {Circle(mesh,.35f,eyeY,.14f,Color.white);Circle(mesh,.65f,eyeY,.14f,Color.white);}
            Circle(mesh,.35f,eyeY,.052f,_ink);Circle(mesh,.65f,eyeY,.052f,_ink);
            Circle(mesh,.365f,eyeY+.015f,.016f,Color.white);Circle(mesh,.665f,eyeY+.015f,.016f,Color.white);
            if(portrait==1) Triangle(mesh,new(.45f,.45f),new(.55f,.45f),new(.5f,.34f),new Color(.86f,.49f,.22f));
            else if(portrait==2) {Rect(mesh,.34f,.33f,.32f,.07f,_ink);for(int i=0;i<3;i++)Rect(mesh,.38f+i*.10f,.345f,.025f,.04f,face);}
            else {Rect(mesh,.36f,.35f,.28f,.025f,_ink);Circle(mesh,.5f,.44f,.026f,_ink);}
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
