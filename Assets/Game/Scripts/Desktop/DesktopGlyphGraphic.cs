using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Desktop
{
    // Neutral OS symbols use geometry, so RU/EN do not depend on font fallback glyphs.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DesktopGlyphGraphic : MaskableGraphic
    {
        public enum GlyphKind { Eye, People, Clock, Check, Warning, Monitor, Network, Volume, ArrowLeft, ArrowRight, Home, Search, Power, Chevron }
        [SerializeField] private GlyphKind glyph;
        private VertexHelper _mesh;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); _mesh = mesh;
            switch (glyph)
            {
                case GlyphKind.Clock: Ring(.5f,.5f,.36f); Line(.5f,.5f,.5f,.74f); Line(.5f,.5f,.69f,.4f); break;
                case GlyphKind.People: Ring(.36f,.68f,.14f); Ring(.71f,.66f,.12f); Arc(.36f,.24f,.25f,0,180); Arc(.73f,.24f,.19f,0,100); break;
                case GlyphKind.Eye: Arc(.5f,.5f,.4f,20,160,.58f); Arc(.5f,.5f,.4f,200,340,.58f); Ring(.5f,.5f,.13f); break;
                case GlyphKind.Check: Line(.16f,.46f,.4f,.23f,.10f); Line(.4f,.23f,.85f,.78f,.10f); break;
                case GlyphKind.Warning: Line(.5f,.88f,.09f,.15f); Line(.09f,.15f,.91f,.15f); Line(.91f,.15f,.5f,.88f); Line(.5f,.62f,.5f,.37f); Line(.5f,.26f,.5f,.24f,.09f); break;
                case GlyphKind.Monitor: Box(.12f,.33f,.88f,.86f); Line(.5f,.33f,.5f,.16f); Line(.3f,.13f,.7f,.13f); break;
                case GlyphKind.Network: Arc(.5f,.14f,.68f,48,132); Arc(.5f,.14f,.45f,48,132); Arc(.5f,.14f,.23f,48,132); Ring(.5f,.14f,.04f); break;
                case GlyphKind.Volume: Line(.16f,.37f,.16f,.63f); Line(.16f,.63f,.33f,.63f); Line(.33f,.63f,.54f,.82f); Line(.54f,.82f,.54f,.18f); Line(.54f,.18f,.33f,.37f); Line(.33f,.37f,.16f,.37f); Arc(.54f,.5f,.22f,-60,60); Arc(.54f,.5f,.36f,-60,60); break;
                case GlyphKind.ArrowLeft: Line(.75f,.5f,.2f,.5f); Line(.2f,.5f,.48f,.8f); Line(.2f,.5f,.48f,.2f); break;
                case GlyphKind.ArrowRight: Line(.25f,.5f,.8f,.5f); Line(.8f,.5f,.52f,.8f); Line(.8f,.5f,.52f,.2f); break;
                case GlyphKind.Chevron: Line(.2f,.36f,.5f,.66f); Line(.5f,.66f,.8f,.36f); break;
                case GlyphKind.Home: Line(.1f,.5f,.5f,.88f); Line(.5f,.88f,.9f,.5f); Box(.24f,.14f,.76f,.58f); break;
                case GlyphKind.Search: Ring(.42f,.59f,.24f); Line(.59f,.42f,.87f,.13f,.09f); break;
                case GlyphKind.Power: Arc(.5f,.47f,.33f,135,405); Line(.5f,.57f,.5f,.92f,.08f); break;
            }
        }
        private void Box(float x0,float y0,float x1,float y1) { Line(x0,y0,x1,y0); Line(x1,y0,x1,y1); Line(x1,y1,x0,y1); Line(x0,y1,x0,y0); }
        private void Ring(float x,float y,float radius) => Arc(x,y,radius,0,360);
        private void Arc(float x,float y,float radius,float from,float to,float height=1)
        {
            for(int i=0;i<24;i++)
            {
                float a=Mathf.Lerp(from,to,i/24f)*Mathf.Deg2Rad, b=Mathf.Lerp(from,to,(i+1)/24f)*Mathf.Deg2Rad;
                Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius*height,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius*height);
            }
        }
        private void Line(float x0,float y0,float x1,float y1,float width=.055f)
        {
            Rect r=rectTransform.rect;
            Vector2 a=new(r.xMin+x0*r.width,r.yMin+y0*r.height),b=new(r.xMin+x1*r.width,r.yMin+y1*r.height);
            Vector2 normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*Mathf.Min(r.width,r.height)*.5f;
            int index=_mesh.currentVertCount;
            _mesh.AddVert(a-normal,color,Vector2.zero); _mesh.AddVert(a+normal,color,Vector2.zero);
            _mesh.AddVert(b+normal,color,Vector2.zero); _mesh.AddVert(b-normal,color,Vector2.zero);
            _mesh.AddTriangle(index,index+1,index+2); _mesh.AddTriangle(index,index+2,index+3);
        }
    }
}
