using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoLive.Editor.Desktop
{
    // Reads the existing authored emissive screen face once in the editor. A saved canvas receives the
    // resulting pose; runtime never inspects meshes, UVs or material names to recover a dependency.
    internal readonly struct MonitorScreenGeometry
    {
        public readonly Vector3 Center;
        public readonly Vector3 Normal;
        public readonly float Width;
        public readonly float Height;
        private MonitorScreenGeometry(Vector3 center,Vector3 normal,float width,float height)
        {Center=center;Normal=normal;Width=width;Height=height;}
        private sealed class Face
        {
            public Vector3 Normal;
            public Vector3 Point;
            public float Area;
            public readonly List<Vector3> Vertices=new();
        }
        internal static MonitorScreenGeometry Read(MeshFilter filter,Renderer renderer)
        {
            if(filter==null||renderer==null) throw new InvalidOperationException("Monitor mesh references missing.");
            Texture source=renderer.sharedMaterial.GetTexture("_EmissionMap");
            if(source==null) throw new InvalidOperationException("Monitor requires its existing screen emission texture.");
            RenderTexture temporary=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
            RenderTexture previous=RenderTexture.active;
            Texture2D mask=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false);
            try
            {
                Graphics.Blit(source,temporary);RenderTexture.active=temporary;
                mask.ReadPixels(new Rect(0,0,source.width,source.height),0,0);mask.Apply();
                Mesh mesh=filter.sharedMesh;Vector3[] positions=mesh.vertices;Vector2[] uv=mesh.uv;int[] triangles=mesh.triangles;
                var faces=new List<Face>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];Vector2 middle=(uv[a]+uv[b]+uv[c])/3f;
                    Color pixel=mask.GetPixelBilinear(middle.x,middle.y);
                    if(Mathf.Max(pixel.r,pixel.g,pixel.b)<.045f) continue;
                    Vector3 va=filter.transform.TransformPoint(positions[a]),vb=filter.transform.TransformPoint(positions[b]),vc=filter.transform.TransformPoint(positions[c]);
                    Vector3 cross=Vector3.Cross(vb-va,vc-va);float area=cross.magnitude*.5f;if(area<.000001f) continue;
                    Vector3 normal=cross.normalized;Face face=null;
                    foreach(Face candidate in faces)
                        if(Vector3.Dot(candidate.Normal,normal)>.999f&&Mathf.Abs(Vector3.Dot(va-candidate.Point,normal))<.002f) {face=candidate;break;}
                    if(face==null) {face=new Face{Normal=normal,Point=va};faces.Add(face);}
                    face.Area+=area;face.Vertices.Add(va);face.Vertices.Add(vb);face.Vertices.Add(vc);
                }
                Face best=null;foreach(Face face in faces) if(best==null||face.Area>best.Area) best=face;
                if(best==null) throw new InvalidOperationException("Could not identify the monitor's emissive face.");
                // The CRT is gently curved; its emissive surface spans several adjacent planar strips.
                var screenVertices=new List<Vector3>();Vector3 screenNormal=Vector3.zero;
                foreach(Face face in faces)
                    if(Vector3.Dot(face.Normal,best.Normal)>.94f&&Mathf.Abs(Vector3.Dot(face.Point-best.Point,best.Normal))<.035f)
                    {screenVertices.AddRange(face.Vertices);screenNormal+=face.Normal*face.Area;}
                best.Normal=screenNormal.normalized;
                Vector3 up=Vector3.ProjectOnPlane(Vector3.up,best.Normal).normalized;
                Vector3 right=Vector3.Cross(up,-best.Normal).normalized;
                float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
                float frontmost=float.MinValue;
                foreach(Vector3 point in screenVertices)
                {
                    Vector3 offset=point-best.Point;float x=Vector3.Dot(offset,right),y=Vector3.Dot(offset,up);
                    minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
                    frontmost=Mathf.Max(frontmost,Vector3.Dot(offset,best.Normal));
                }
                Vector3 center=best.Point+right*((minX+maxX)/2)+up*((minY+maxY)/2)+best.Normal*frontmost;
                return new MonitorScreenGeometry(center,best.Normal,(maxX-minX)*.985f,(maxY-minY)*.985f);
            }
            finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);UnityEngine.Object.DestroyImmediate(mask);}
        }
    }
}
