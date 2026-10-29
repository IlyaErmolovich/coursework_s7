using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace FrostDuel.Editor {
    public static class CharacterBuilder {
        [Serializable] class Parts {public Part[] parts;}
        [Serializable] class Part {public string variant,name;public int width,height;public float pivotX,pivotY;}
        static readonly List<Rigidbody2D> bodies=new List<Rigidbody2D>();
        static readonly List<HingeJoint2D> joints=new List<HingeJoint2D>();
        static string variant;
        [MenuItem("Frost Duel/Characters/Build sprites and prefabs")]
        public static void Build() {
            AssetDatabase.Refresh();var manifest=JsonUtility.FromJson<Parts>(File.ReadAllText("Assets/Art/Characters/parts.json"));
            foreach(var p in manifest.parts) {
                string path="Assets/Art/Characters/"+p.variant+"/"+p.name+".png";
                var t=(TextureImporter)AssetImporter.GetAtPath(path);t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;
                t.spritePixelsPerUnit=400;t.alphaIsTransparency=true;t.mipmapEnabled=false;t.filterMode=FilterMode.Bilinear;
                t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=1024;t.isReadable=true;
                var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=new Vector2(p.pivotX,p.pivotY);settings.spriteMeshType=SpriteMeshType.FullRect;t.SetTextureSettings(settings);t.SaveAndReimport();
            }
            Directory.CreateDirectory("Assets/Resources/Characters");
            foreach(var v in new[]{"teal","red"}) {
                variant=v;bodies.Clear();joints.Clear();var root=new GameObject(v=="teal"?"TealCharacter":"RedCharacter");
                var rig=root.AddComponent<CutoutCharacter>();
                rig.torso=PartObject("Torso",root.transform,new Vector2(0,.66f),new Vector2(.65f,.70f),20,new Vector2(0,.16f),new Vector2(.55f,.40f),1.8f);
                rig.thighL=PartObject("ThighL",rig.torso,new Vector2(-.17f,-.01f),new Vector2(.27f,.43f),5,new Vector2(0,-.12f),new Vector2(.19f,.26f),.45f);
                rig.thighR=PartObject("ThighR",rig.torso,new Vector2(.17f,-.01f),new Vector2(.27f,.43f),6,new Vector2(0,-.12f),new Vector2(.19f,.26f),.45f);
                rig.bootL=PartObject("BootL",rig.thighL,new Vector2(0,-.34f),new Vector2(.34f,.35f),7,new Vector2(-.025f,-.17f),new Vector2(.28f,.28f),.4f);
                rig.bootR=PartObject("BootR",rig.thighR,new Vector2(0,-.34f),new Vector2(.34f,.35f),8,new Vector2(.025f,-.17f),new Vector2(.28f,.28f),.4f);
                rig.head=PartObject("Head",rig.torso,new Vector2(0,.50f),new Vector2(.92f,.96f),40,new Vector2(0,.38f),new Vector2(.78f,.80f),1.1f);
                rig.scarf=PartObject("Scarf",rig.torso,new Vector2(-.01f,.36f),new Vector2(.92f,.52f),10,Vector2.zero,Vector2.zero,0);
                PartObject("ScarfFront",rig.scarf,Vector2.zero,new Vector2(.92f,.52f),42,Vector2.zero,Vector2.zero,0);
                if(v=="teal") {PartObject("PocketL",rig.torso,new Vector2(-.17f,.02f),new Vector2(.19f,.17f),21,Vector2.zero,Vector2.zero,0);PartObject("PocketR",rig.torso,new Vector2(.22f,.04f),new Vector2(.12f,.16f),21,Vector2.zero,Vector2.zero,0);}
                rig.upperL=PartObject("UpperArmL",rig.torso,new Vector2(-.24f,.33f),new Vector2(.30f,.33f),25,new Vector2(0,-.13f),new Vector2(.22f,.26f),.25f);
                rig.lowerL=PartObject("ForearmL",rig.upperL,new Vector2(0,-CutoutCharacter.UpperLength),new Vector2(.25f,.28f),24,new Vector2(0,-.12f),new Vector2(.17f,.23f),.20f);
                rig.handL=PartObject("MittenL",rig.lowerL,new Vector2(0,-CutoutCharacter.LowerLength),new Vector2(.25f,.25f),23,new Vector2(0,-.08f),new Vector2(.19f,.19f),.13f);
                rig.upperR=PartObject("UpperArmR",rig.torso,new Vector2(.24f,.33f),new Vector2(.30f,.33f),15,new Vector2(0,-.13f),new Vector2(.22f,.26f),.25f);
                rig.lowerR=PartObject("ForearmR",rig.upperR,new Vector2(0,-CutoutCharacter.UpperLength),new Vector2(.25f,.28f),14,new Vector2(0,-.12f),new Vector2(.17f,.23f),.20f);
                rig.handR=PartObject("MittenR",rig.lowerR,new Vector2(0,-CutoutCharacter.LowerLength),new Vector2(.25f,.25f),13,new Vector2(0,-.08f),new Vector2(.19f,.19f),.13f);
                foreach(var b in bodies) {
                    if(b.transform==rig.torso)continue;
                    var parent=b.transform.parent.GetComponent<Rigidbody2D>();
                    var j=b.gameObject.AddComponent<HingeJoint2D>();j.connectedBody=parent;j.autoConfigureConnectedAnchor=false;j.anchor=Vector2.zero;j.connectedAnchor=b.transform.localPosition;j.enableCollision=false;j.useLimits=true;
                    j.limits=new JointAngleLimits2D{min=-65,max=65};j.enabled=false;joints.Add(j);
                }
                // Prevent self-collision explosions; external ground remains collidable.
                var cols=root.GetComponentsInChildren<Collider2D>();for(int i=0;i<cols.Length;i++)for(int k=i+1;k<cols.Length;k++)Physics2D.IgnoreCollision(cols[i],cols[k]);
                rig.bodies=bodies.ToArray();rig.joints=joints.ToArray();rig.Pose(0);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Characters/"+root.name+".prefab");UnityEngine.Object.DestroyImmediate(root);
            }
            CreateScene();AssetDatabase.SaveAssets();Debug.Log("FROST: Imported 30 clean sprites and built two articulated prefabs");
        }
        static Transform PartObject(string name,Transform parent,Vector2 pos,Vector2 size,int order,Vector2 colliderOffset,Vector2 colliderSize,float mass) {
            var bone=new GameObject(name);bone.transform.SetParent(parent,false);bone.transform.localPosition=pos;
            var image=new GameObject("Sprite");image.transform.SetParent(bone.transform,false);
            var sr=image.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Characters/"+variant+"/"+name+".png");sr.sortingOrder=order;
            image.transform.localScale=new Vector3(size.x/sr.sprite.bounds.size.x,size.y/sr.sprite.bounds.size.y,1);
            if(name=="UpperArmL")image.transform.localRotation=Quaternion.Euler(0,0,20);
            if(name=="UpperArmR")image.transform.localRotation=Quaternion.Euler(0,0,-20);
            if(mass>0) {
                var b=bone.AddComponent<Rigidbody2D>();b.bodyType=RigidbodyType2D.Kinematic;b.simulated=false;b.mass=mass;b.gravityScale=1;b.linearDamping=.6f;b.angularDamping=2;b.interpolation=RigidbodyInterpolation2D.None;b.collisionDetectionMode=CollisionDetectionMode2D.Continuous;bodies.Add(b);
                var c=bone.AddComponent<CapsuleCollider2D>();c.size=colliderSize;c.offset=colliderOffset;c.direction=CapsuleDirection2D.Vertical;
            }
            return bone.transform;
        }
        static void CreateScene() {
            // The shipped workshop already exists in interactive projects. Do not
            // replace a user's unsaved scene just to regenerate the prefabs.
            if(!Application.isBatchMode && string.IsNullOrEmpty(EditorSceneManager.GetActiveScene().path))return;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
            var workshop=new GameObject("Character workshop");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(workshop,scene);
            workshop.AddComponent<CharacterWorkshop>();
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/CharacterWorkshop.unity");
            if(!Application.isBatchMode)EditorSceneManager.CloseScene(scene,true);
        }
        public static void BuildAndLinux(){Build();ProjectBuilder.VerifyAndLinux();}
    }
}
