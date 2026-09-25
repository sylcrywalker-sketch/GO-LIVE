using System;
using UnityEditor;
using UnityEngine;

namespace GoLive.Editor.Desktop
{
    // Only the supplied icons with opaque export mattes need this import treatment. Source PNGs
    // stay byte-identical. Flooding from outside retains enclosed white artwork and dark outlines.
    internal sealed class DesktopIconImport : AssetPostprocessor
    {
        public override uint GetVersion() => 2;
        private void OnPostprocessTexture(Texture2D texture)
        {
            const string root="Assets/Game/Art/Desktop/Icons/";
            if(!assetPath.StartsWith(root,StringComparison.Ordinal)) return;
            string name=System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if(name!="Trich"&&name!="Donation"&&name!="Outline"&&name!="Hub"&&name!="Web") return;
            int width=texture.width,height=texture.height;
            Color32[] pixels=texture.GetPixels32();
            // The two screenshot exports have a neutral outer frame. Clear its documented empty
            // margin before flooding so resize antialiasing cannot seal the white matte inside it.
            if(name=="Donation"||name=="Trich")
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    float u=x/(float)width,v=y/(float)height;
                    bool margin=name=="Donation" ? u<.126f||u>.843f||v<.204f||v>.782f
                        : u<.015f||u>.985f||v<.015f||v>.985f;
                    if(margin)pixels[y*width+x].a=0;
                }
            var visited=new bool[pixels.Length];var queue=new int[pixels.Length];int read=0,write=0;
            // Donation and Trich exports contain a neutral screenshot border around the white matte.
            bool screenshotBorder=name=="Donation"||name=="Trich";
            void Enqueue(int index)
            {
                if(visited[index]) return;visited[index]=true;
                Color32 c=pixels[index];
                int max=Math.Max(c.r,Math.Max(c.g,c.b)),min=Math.Min(c.r,Math.Min(c.g,c.b));
                bool white=min>=235&&max-min<=17;
                bool border=screenshotBorder&&max-min<=5&&min>=30&&max<=55;
                if(c.a==0||white||border) queue[write++]=index;
            }
            for(int x=0;x<width;x++){Enqueue(x);Enqueue((height-1)*width+x);}
            for(int y=0;y<height;y++){Enqueue(y*width);Enqueue(y*width+width-1);}
            while(read<write)
            {
                int index=queue[read++],x=index%width,y=index/width;
                pixels[index].a=0;
                if(x>0)Enqueue(index-1);if(x+1<width)Enqueue(index+1);
                if(y>0)Enqueue(index-width);if(y+1<height)Enqueue(index+width);
            }
            texture.SetPixels32(pixels);texture.Apply(false,false);
        }
    }
}
