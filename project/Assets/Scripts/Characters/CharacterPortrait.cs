using System;
using UnityEngine;
namespace FrostDuel {
    /// <summary>Render the actual articulated prefab into the existing IMGUI battlefield.</summary>
    public sealed class CharacterPortrait : IDisposable {
        readonly CutoutCharacter rig;
        readonly Camera camera;
        readonly RenderTexture texture;
        public CharacterPortrait(string name,int index) {
            var prefab=Resources.Load<GameObject>("Characters/"+name);
            if(prefab==null)return;
            var root=UnityEngine.Object.Instantiate(prefab,new Vector3(100+index*10,100,0),Quaternion.identity);
            rig=root.GetComponent<CutoutCharacter>();
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=30;
            foreach(var b in rig.bodies)b.simulated=false;
            texture=new RenderTexture(384,384,16,RenderTextureFormat.ARGB32){antiAliasing=4,name=name+" portrait"};texture.Create();
            camera=new GameObject(name+" capture").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.25f;
            camera.transform.position=root.transform.position+new Vector3(0,1.05f,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<30;camera.targetTexture=texture;
        }
        public bool Draw(Vector2 feet,float pixelsPerUnit,int facing,float angle,float throwPhase=0) {
            if(rig==null)return false;
            if(Event.current.type==EventType.Repaint){rig.Pose(Time.unscaledTime,angle,throwPhase);camera.Render();}
            var saved=GUI.matrix;GUI.matrix=saved*Matrix4x4.TRS(feet,Quaternion.identity,new Vector3(facing*pixelsPerUnit,pixelsPerUnit,1));
            GUI.color=Color.white;GUI.DrawTexture(new Rect(-1.25f,-2.3f,2.5f,2.5f),texture);GUI.matrix=saved;return true;
        }
        public void Dispose(){if(rig!=null)UnityEngine.Object.Destroy(rig.gameObject);if(camera!=null)UnityEngine.Object.Destroy(camera.gameObject);if(texture!=null){texture.Release();UnityEngine.Object.Destroy(texture);}}
    }
}
