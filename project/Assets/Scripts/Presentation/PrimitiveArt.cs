using System;
using UnityEngine;
namespace FrostDuel {
    /// <summary>Procedural Unity textures only: no borrowed sprites. Cached between state changes.</summary>
    public sealed class PrimitiveArt : IDisposable {
        public Texture2D Circle {get;private set;}
        public Texture2D Sky {get;private set;}
        public Texture2D FarRidge {get;private set;}
        public Texture2D NearRidge {get;private set;}
        public Texture2D Ground {get;private set;}
        public const float GroundHeight=7;
        float[] groundSource;
        public PrimitiveArt() {
            Circle=new Texture2D(128,128,TextureFormat.RGBA32,false);
            var pixels=new Color[128*128];
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[y*128+x]=new Color(1,1,1,Mathf.Clamp01(64-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(64,64))));
            Circle.SetPixels(pixels);Setup(Circle);
            Sky=new Texture2D(1,256,TextureFormat.RGBA32,false);
            for(int y=0;y<256;y++)Sky.SetPixel(0,y,Color.Lerp(new Color(.40f,.67f,.74f),new Color(.07f,.19f,.32f),y/255f));Setup(Sky);
            FarRidge=Ridge(7,new Color(.26f,.42f,.56f),new Color(.46f,.67f,.75f));
            NearRidge=Ridge(19,new Color(.22f,.40f,.51f),new Color(.42f,.63f,.68f));
        }
        static void Setup(Texture2D t){t.wrapMode=TextureWrapMode.Clamp;t.filterMode=FilterMode.Bilinear;t.Apply();}
        static Texture2D Ridge(int seed,Color baseColor,Color snow) {
            const int w=1536,h=400;var t=new Texture2D(w,h,TextureFormat.RGBA32,false);var data=new Color[w*h];
            for(int x=0;x<w;x++) {
                float u=x/(float)w;
                float height=95+190*Mathf.Abs(Mathf.Sin(u*9+seed)) + 43*Mathf.Sin(u*21+seed*.3f);
                for(int y=0;y<h;y++) {
                    float depth=height-y;Color c=depth<22+12*Mathf.Sin(u*80)?snow:baseColor;
                    c.a=Mathf.Clamp01(depth+.5f);data[y*w+x]=c;
                }
            }
            t.SetPixels(data);Setup(t);return t;
        }
        public void UpdateGround(GameSession state,BalanceSettings b) {
            if(ReferenceEquals(groundSource,state.terrain))return;
            groundSource=state.terrain;
            if(Ground!=null)UnityEngine.Object.Destroy(Ground);
            const int w=2560,h=560;Ground=new Texture2D(w,h,TextureFormat.RGBA32,false);var data=new Color[w*h];
            float pixelHeight=GroundHeight/h;
            for(int x=0;x<w;x++) {
                float wx=(x+.5f)/w*b.worldWidth, height=Terrain.Height(state.terrain,wx,b);
                for(int y=0;y<h;y++) {
                    float wy=(y+.5f)*pixelHeight,depth=height-wy;
                    Color c;
                    if(depth<.14f)c=new Color(.93f,.98f,1);
                    else if(depth<.30f)c=new Color(.66f,.86f,.90f);
                    else {
                        c=Color.Lerp(new Color(.16f,.35f,.46f),new Color(.36f,.61f,.66f),Mathf.Clamp01(wy/4));
                        float band=Mathf.Sin(wy*12+Mathf.Sin(wx*.6f)*1.4f);
                        if(band>.91f)c=Color.Lerp(c,new Color(.58f,.79f,.80f),.18f);
                    }
                    c.a=Mathf.Clamp01(depth/pixelHeight+.5f);data[y*w+x]=c;
                }
            }
            Ground.SetPixels(data);Setup(Ground);
        }
        public void Dispose() {foreach(var t in new[]{Circle,Sky,FarRidge,NearRidge,Ground})if(t!=null)UnityEngine.Object.Destroy(t);}
    }
}
