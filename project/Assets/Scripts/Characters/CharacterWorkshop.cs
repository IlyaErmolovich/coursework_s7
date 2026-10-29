using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace FrostDuel {
    /// <summary>Isolated asset/physics workshop, not a combat rule or Bluetooth state.</summary>
    public sealed class CharacterWorkshop : MonoBehaviour {
        CutoutCharacter red,teal;
        Camera workshopCamera;
        Texture2D redReference,tealReference;
        GUIStyle text,heading,button;
        string mode="Сборка / исходные пропорции";
        int pose;float poseStart;
        readonly List<string> checks=new List<string>();
        string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if((Array.IndexOf(Environment.GetCommandLineArgs(),"-characterWorkshop")>=0||Array.IndexOf(Environment.GetCommandLineArgs(),"-captureCharacters")>=0)&&FindFirstObjectByType<CharacterWorkshop>()==null)new GameObject("Character workshop").AddComponent<CharacterWorkshop>();}
        void Awake() {
            Application.targetFrameRate=60;
            var camera=new GameObject("Workshop Camera").AddComponent<Camera>();workshopCamera=camera;camera.orthographic=true;camera.orthographicSize=1.65f;camera.transform.position=new Vector3(0,1,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.27f,.36f);
            red=Instantiate(Resources.Load<GameObject>("Characters/RedCharacter"),new Vector3(-.8f,0,0),Quaternion.identity).GetComponent<CutoutCharacter>();
            teal=Instantiate(Resources.Load<GameObject>("Characters/TealCharacter"),new Vector3(.8f,0,0),Quaternion.identity).GetComponent<CutoutCharacter>();
            redReference=Resources.Load<Texture2D>("Characters/References/red");tealReference=Resources.Load<Texture2D>("Characters/References/teal");
            var floor=new GameObject("Physical floor");floor.transform.position=new Vector3(0,-.07f,0);floor.AddComponent<BoxCollider2D>().size=new Vector2(20,.12f);
            var line=floor.AddComponent<LineRenderer>();line.positionCount=2;line.SetPosition(0,new Vector3(-10,0,0));line.SetPosition(1,new Vector3(10,0,0));line.startWidth=line.endWidth=.015f;line.material=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=new Color(.7f,.85f,.9f);
        }
        void Update() {
            if(!red.Fallen){float t=Time.time-poseStart;red.Pose(Time.time,pose==1?45:-1,pose==2?Mathf.PingPong(t,1):0);teal.Pose(Time.time,pose==1?65:-1,pose==2?Mathf.PingPong(t,1):0);}
            if(Input.GetKeyDown(KeyCode.Alpha1))SetPose(0);if(Input.GetKeyDown(KeyCode.Alpha2))SetPose(1);if(Input.GetKeyDown(KeyCode.Alpha3))SetPose(2);if(Input.GetKeyDown(KeyCode.Alpha4))Drop();if(Input.GetKeyDown(KeyCode.R))ResetCharacters();
        }
        void SetPose(int p){ResetCharacters();pose=p;poseStart=Time.time;mode=p==0?"Сборка / исходные пропорции":p==1?"Прицеливание: плечо, локоть, запястье":"Замах → выпуск → возврат";}
        void ResetCharacters(){workshopCamera.orthographicSize=1.65f;red.transform.position=new Vector3(-.8f,0,0);teal.transform.position=new Vector3(.8f,0,0);red.ResetPose();teal.ResetPose();pose=0;mode="Сборка / исходные пропорции";}
        void Drop(){ResetCharacters();workshopCamera.orthographicSize=2.0f;red.transform.position+=Vector3.up*.65f;teal.transform.position+=Vector3.up*.65f;red.Fall(new Vector2(-2,1));teal.Fall(new Vector2(2,1));mode="Физическое падение · Rigidbody2D / HingeJoint2D";}
        void OnGUI() {
            float scale=Screen.height/720f;GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);float w=Screen.width/scale;
            if(text==null){var f=Resources.Load<Font>("InterfaceFont");text=new GUIStyle(GUI.skin.label){font=f,fontSize=18,alignment=TextAnchor.MiddleCenter};heading=new GUIStyle(text){fontSize=27,fontStyle=FontStyle.Bold};button=new GUIStyle(GUI.skin.button){font=f,fontSize=17};}
            GUI.Label(new Rect(0,15,w,40),mode,heading);
            GUI.Label(new Rect(0,57,w,30),"Оригинальные детали · суставы и порядок слоёв · отдельная сцена проверки",text);
            if(!red.Fallen) {
                GUI.DrawTexture(new Rect(30,135,265,440),redReference,ScaleMode.ScaleToFit,true);
                GUI.DrawTexture(new Rect(w-295,135,265,440),tealReference,ScaleMode.ScaleToFit,true);
                GUI.Label(new Rect(30,100,265,30),"Красный: исходный образ",text);GUI.Label(new Rect(w-295,100,265,30),"Бирюзовый: исходный образ",text);
                GUI.Label(new Rect(w/2-270,100,220,30),"Собран из спрайтов",text);GUI.Label(new Rect(w/2+50,100,220,30),"Собран из спрайтов",text);
            }
            if(GUI.Button(new Rect(w/2-430,638,200,42),"1 · Покой",button))SetPose(0);
            if(GUI.Button(new Rect(w/2-215,638,200,42),"2 · Прицел",button))SetPose(1);
            if(GUI.Button(new Rect(w/2,638,200,42),"3 · Бросок",button))SetPose(2);
            if(GUI.Button(new Rect(w/2+215,638,200,42),"4 · Падение",button))Drop();
            GUI.Label(new Rect(0,687,w,26),"R — восстановить сборку. Физика этой сцены не перемещает игроков в бою.",text);
        }
        void Check(bool ok,string name){if(!ok){File.WriteAllText(Path.Combine(folder,"FAILED.txt"),name);Debug.LogError("CHARACTER FAIL: "+name);Application.Quit(1);throw new Exception(name);}checks.Add("PASS: "+name);}
        IEnumerator Capture(string name){yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));yield return new WaitForSeconds(.15f);}
        IEnumerator Start() {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-captureCharacters")<0)yield break;
            folder=Path.GetFullPath("Builds/CharacterVerification");Directory.CreateDirectory(folder);var failed=Path.Combine(folder,"FAILED.txt");if(File.Exists(failed))File.Delete(failed);
            yield return new WaitForSeconds(1);yield return Capture("01-assembled");
            foreach(var rig in new[]{red,teal}) {
                Check(rig.GetComponentsInChildren<SpriteRenderer>().Length==(rig==red?14:16),rig.name+": independently rendered parts including scarf layers");
                Check(rig.bodies.Length==12&&rig.joints.Length==11,rig.name+": twelve bodies, eleven articulated joints");
                Check(!rig.Fallen&&rig.bodies[0].bodyType==RigidbodyType2D.Kinematic,rig.name+": physics disabled in puppet mode");
                rig.Pose(0);Vector3 idleHand=rig.handR.position;rig.Pose(0,45);Check(Vector3.Distance(idleHand,rig.handR.position)>.3f,rig.name+": aim actually moves articulated hand");
                float error=0;for(int angle=5;angle<=85;angle+=5){rig.Pose(0,angle);error=Mathf.Max(error,rig.JointError());}
                Check(error<.0001f,rig.name+": no joint anchor separation at 5..85 degrees");
            }
            SetPose(1);yield return new WaitForSeconds(.5f);yield return Capture("02-aim");
            SetPose(2);yield return new WaitForSeconds(.2f);yield return Capture("03-windup");yield return new WaitForSeconds(.6f);yield return Capture("04-release");
            foreach(var rig in new[]{red,teal})Check(rig.JointError()<.0001f,rig.name+": elbow and wrist anchors stay connected during throw");
            Drop();yield return new WaitForSeconds(.22f);yield return Capture("05-falling");
            foreach(var rig in new[]{red,teal})Check(rig.bodies[0].bodyType==RigidbodyType2D.Dynamic,rig.name+": real dynamic physics enabled");
            yield return new WaitForSeconds(7);yield return Capture("06-landed");
            foreach(var rig in new[]{red,teal}) {
                float speed=0;bool finite=true;foreach(var b in rig.bodies){speed=Mathf.Max(speed,b.linearVelocity.magnitude);finite&=!float.IsNaN(b.position.x)&&!float.IsInfinity(b.position.x)&&!float.IsNaN(b.position.y)&&!float.IsInfinity(b.position.y);}
                Check(finite,rig.name+": all bodies remain finite after fall");
                Check(rig.torso.position.y<.8f,rig.name+": gravity brings torso to floor");
                Check(Mathf.Abs(Mathf.DeltaAngle(0,rig.torso.eulerAngles.z))>20,rig.name+": torso tips over instead of remaining upright");
                Check(rig.JointError()<.06f,rig.name+": hinge separation below .06 world units after landing");
                Check(speed<.5f,rig.name+": ragdoll settles below .5 units/sec");
            }
            ResetCharacters();yield return new WaitForSeconds(.3f);yield return Capture("07-reset");
            foreach(var rig in new[]{red,teal})Check(!rig.Fallen&&rig.JointError()<.0001f,rig.name+": full hierarchy and pivots restored after physics");
            File.WriteAllLines(Path.Combine(folder,"checks.txt"),checks);Debug.Log("FROST: "+checks.Count+" CHARACTER RUNTIME CHECKS PASSED");Application.Quit();
        }
    }
}
