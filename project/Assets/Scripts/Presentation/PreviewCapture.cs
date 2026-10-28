#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace FrostDuel {
    // Real runtime renderer and two coordinators. JSON delivery replaces Bluetooth radio only.
    public sealed class PreviewCapture : MonoBehaviour {
        readonly List<string> checks=new List<string>();
        readonly Queue<Action> packets=new Queue<Action>();
        string folder;DuelApp app;SessionCoordinator host,client;
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-capturePreview")>=0)new GameObject("Runtime verification").AddComponent<PreviewCapture>();}
        void Check(bool ok,string text) {
            if(!ok){File.WriteAllText(Path.Combine(folder,"FAILED.txt"),text);Debug.LogError("RUNTIME FAIL: "+text);Application.Quit(1);throw new Exception(text);}
            checks.Add("PASS: "+text);
        }
        static void Set(object obj,string name,object value) {
            var f=obj.GetType().GetField(name,Hidden)??obj.GetType().GetField("<"+name+">k__BackingField",Hidden);f.SetValue(obj,value);
        }
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
        void Flush(){int n=0;while(packets.Count>0){if(++n>100)throw new Exception("Packet loop");packets.Dequeue()();}}
        void Update() {
            if(host==null||client==null)return;
            // The mock channel is live; native socket heartbeat is outside this fixture's scope.
            Set(host.transport,"lastReceive",Time.realtimeSinceStartup);Set(client.transport,"lastReceive",Time.realtimeSinceStartup);Flush();
        }
        void Fixture(int actor,int viewer,ActionKind kind,int warmth,float wind) {
            host.StartLocal();client.ResetSession();packets.Clear();
            var rules=(TurnManager)typeof(SessionCoordinator).GetField("rules",Hidden).GetValue(host);
            rules.State.activePlayer=actor;rules.State.wind=wind;rules.State.blizzard=true;
            rules.State.players[actor].warmth=warmth;rules.State.players[actor].iceReady=kind==ActionKind.ThrowIce;
            Set(host,"Local",false);Set(host,"HostRole",true);Set(client,"HostRole",false);
            foreach(var c in new[]{host,client}) {Set(c,"accepting",true);Set(c.transport,"IsConnected",true);Set(c.transport,"lastReceive",Time.realtimeSinceStartup);}
            Call(host,"OnConnected");Call(client,"OnConnected");Flush();
            Set(app,"session",viewer==0?host:client);Set(app,"resetView",true);
        }
        PlayerAction FindHit(GameSession state,int actor) {
            for(float power=.7f;power<=1.001f;power+=.01f)for(float angle=15;angle<=75;angle++) {
                var a=new PlayerAction{player=actor,kind=ActionKind.ThrowIce,angle=angle,power=power};
                if(new IceProjectile().Simulate(state,a,host.Balance).target==1-actor)return a;
            }
            return null;
        }
        void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        IEnumerator Start() {
            folder=Path.GetFullPath("Builds/VisualVerification-v03");Directory.CreateDirectory(folder);
            string failure=Path.Combine(folder,"FAILED.txt");if(File.Exists(failure))File.Delete(failure);
            yield return new WaitForSeconds(2);app=FindFirstObjectByType<DuelApp>();Capture("00-menu");
            host=app.Session;client=new GameObject("Verification peer").AddComponent<SessionCoordinator>();client.Initialize(host.Balance);
            host.PacketSent+=json=>packets.Enqueue(()=>Call(client,"OnMessage",json));client.PacketSent+=json=>packets.Enqueue(()=>Call(host,"OnMessage",json));
            yield return new WaitForSeconds(.4f);int caseId=0;float maximumError=0;
            foreach(int side in new[]{0,1})foreach(ActionKind kind in new[]{ActionKind.Snowball,ActionKind.ThrowIce})foreach(int heat in new[]{50,0}) {
                Fixture(side,side,kind,heat,side==0?1.6f:-1.6f);yield return new WaitForSeconds(.25f);
                app.ChooseSnow();if(kind==ActionKind.ThrowIce)app.ChooseIce();
                Check(app.View.Ready&&app.OpponentOffscreen,"Home framing and right enemy indicator, viewer "+side);
                foreach(var r in new[]{app.SnowButton,app.IceButton,app.WarmButton,app.MenuButton}){app.BeginAim(r.center);Check(!app.IsAiming,"UI touch excluded "+r+" case "+caseId);}
                Vector2 start=new Vector2(app.CanvasWidth*.5f,330),end=start+new Vector2(-175,148);
                var own=app.Session.Display.players[side];Vector2 ownPoint=app.World(new Vector2(own.x,own.groundY+1));
                app.BeginAim(start);app.MoveAim(end);app.MoveAim(ownPoint);
                Check(!app.IsAiming,"Hover own character cancels immediately, viewer "+side+" "+kind+" heat "+heat);
                app.MoveAim(end);app.EndAim(end);Flush();Check(!host.Busy&&!client.Busy,"Canceled finger cannot restart or fire on release, viewer "+side);
                app.BeginAim(start);app.MoveAim(end);app.EndAim(ownPoint);Flush();Check(!host.Busy&&!client.Busy,"Release directly on own character also cancels, viewer "+side);
                app.BeginAim(start);Check(app.IsAiming,"New gesture after self-cancel accepted, viewer "+side);
                for(int i=0;i<40;i++){app.MoveAim(end);yield return null;}
                var preview=app.AimPreview;
                Check(preview.kind==kind,"Preview uses selected projectile, case "+caseId);
                Check(Vector2.Distance(preview.path[0],Projectile.ReleasePoint(own,side))<.0001f,"Arc starts at own release, viewer "+side);
                int visible=app.VisiblePreviewCount;float length=0;
                for(int i=1;i<visible;i++)length+=Vector2.Distance(preview.path[i-1],preview.path[i]);
                Check(visible<preview.path.Length&&length<=4.5f,"Only short initial arc drawn, viewer "+side);
                Check(app.World(preview.path[visible-1]).x>app.World(preview.path[0]).x,"Same backward gesture throws screen-right, viewer "+side);
                string tag=(++caseId).ToString("00")+"-viewer"+side+"-"+kind+"-"+(heat==0?"cold":"warm");
                Capture(tag+"-aim");yield return new WaitForSeconds(.2f);
                preview=app.AimPreview;app.EndAim(end);Flush();var actual=host.Animation;
                Check(actual!=null&&client.Animation!=null&&actual.path.Length==preview.path.Length,"Real host/client action and result accepted, "+tag);
                for(int i=0;i<preview.path.Length;i++)maximumError=Mathf.Max(maximumError,Vector2.Distance(preview.path[i],actual.path[i]));
                Check(JsonUtility.ToJson(host.Animation)==JsonUtility.ToJson(client.Animation),"Same canonical flight delivered to both peers, "+tag);
                app.BeginAim(start);Check(!app.IsAiming,"Input blocked during flight, "+tag);
                bool beyond=false,impactSeen=false;float maxOffset=0;bool captured=false,verticallyVisible=true;
                while(app.Session.Animation!=null) {
                    float t=app.Session.AnimationTime;var shot=app.Session.Animation;
                    var pos=app.World(DuelView.Position(shot,t));
                    if(t>.6f){maxOffset=Mathf.Max(maxOffset,Mathf.Abs(pos.x-app.CanvasWidth/2));verticallyVisible&=pos.y>=120&&pos.y<=720;}
                    if(Mathf.Abs(DuelView.Position(shot,t).x-own.x)>app.CanvasWidth*.76f/DuelView.Scale)beyond=true;
                    if(t>1.5f&&!captured){Capture(tag+"-flight");captured=true;}
                    if(t>=(shot.path.Length-1)*shot.step&&!impactSeen){impactSeen=true;Check(app.Session.ImpactState!=null&&!app.Session.CanAct,"Result visible during impact hold, input still locked, "+tag);Capture(tag+"-impact");}
                    yield return null;
                }
                Check(verticallyVisible&&beyond&&maxOffset<app.CanvasWidth*.4f,"Camera follows beyond original frame, "+tag);
                Check(impactSeen,"Impact/result is held before return, "+tag);
                yield return new WaitForSeconds(1.2f);Flush();
                Check(app.View.Ready&&app.OpponentOffscreen,"Camera returned to owner's left framing, "+tag);
                Check(JsonUtility.ToJson(host.Display)==JsonUtility.ToJson(client.Display),"State including crater/weather agrees after throw, "+tag);
                Check(app.Session.MyPlayer==side&&!app.Session.CanAct,"Owner remains fixed and opponent turn locks input, "+tag);
                Capture(tag+"-returned");yield return new WaitForSeconds(.15f);
            }
            Check(maximumError==0,"Preview vs authoritative flight maximum error = "+maximumError+" in 8 network cases");
            // Incoming launch and flight in each recipient's own-left view.
            foreach(int viewer in new[]{0,1}) {
                int actor=1-viewer;Fixture(actor,viewer,ActionKind.ThrowIce,50,0);yield return new WaitForSeconds(.25f);
                var hit=FindHit(host.Display,actor);
                Check(hit!=null,"Ice hit reachable for incoming throw, viewer "+viewer);
                (actor==0?host:client).Act(ActionKind.ThrowIce,hit.angle,hit.power);Flush();
                Check(host.Animation.target==viewer,"Incoming throw hits owner, viewer "+viewer);
                yield return new WaitForSeconds(.35f);
                Check(!app.OpponentOffscreen,"Enemy indicator hides while incoming shooter is visible, viewer "+viewer);
                Capture("incoming-viewer"+viewer);yield return new WaitForSeconds(.3f);
                bool hitCaptured=false;
                while(app.Session.Busy) {
                    var shot=app.Session.Animation;
                    if(shot!=null&&!hitCaptured&&app.Session.AnimationTime>=(shot.path.Length-1)*shot.step+.18f){Check(app.Session.ImpactState!=null&&app.Session.ImpactState.players[viewer].health<100&&!app.Session.CanAct,"Damage visible before camera return, viewer "+viewer);Capture("hit-viewer"+viewer);hitCaptured=true;}
                    yield return null;
                }
                yield return new WaitForSeconds(1.2f);Flush();
                Check(app.View.Ready&&app.Session.CanAct,"Incoming flight returns to newly active owner, viewer "+viewer);
                Check(app.Session.Display.players[viewer].health<100&&JsonUtility.ToJson(host.Display)==JsonUtility.ToJson(client.Display),"Hit damage and result synchronized, viewer "+viewer);
            }
            Fixture(0,0,ActionKind.Snowball,50,0);yield return new WaitForSeconds(.25f);
            Vector2 anchor=new Vector2(640,330),pull=new Vector2(470,460);
            app.BeginAim(anchor);app.MoveAim(pull);app.EndAim(anchor);Check(!app.IsAiming&&!host.Busy,"Returning finger to anchor cancels");
            app.BeginAim(anchor);app.MoveAim(pull);app.CancelAim();app.EndAim(pull);Check(!host.Busy,"Explicit cancellation prevents subsequent release");
            app.BeginAim(anchor);app.MoveAim(pull);app.EndAim(app.SnowButton.center);Check(!host.Busy,"Release over UI cancels");
            app.BeginAim(anchor);app.MoveAim(pull);app.EndAim(new Vector2(-5,400));Check(!host.Busy,"Release outside field cancels");
            Set(app,"confirmExit",true);app.BeginAim(anchor);Check(!app.IsAiming,"Menu modal blocks aiming");Set(app,"confirmExit",false);
            Screen.SetResolution(1560,720,false);yield return new WaitForSeconds(.5f);
            Fixture(1,1,ActionKind.Snowball,50,-3.2f);yield return new WaitForSeconds(.3f);
            anchor=new Vector2(app.CanvasWidth/2,320);pull=anchor+new Vector2(-175,148);app.BeginAim(anchor);
            for(int i=0;i<40;i++){app.MoveAim(pull);yield return null;}
            Check(app.CanvasWidth>1500&&app.OpponentOffscreen,"Wide 19.5:9 framing keeps offscreen enemy indicator");
            Check(app.AimPreview.kind==ActionKind.Snowball,"New session resets previous ice selection");Capture("wide-client-aim");yield return new WaitForSeconds(.4f);
            File.WriteAllLines(Path.Combine(folder,"runtime-verification.txt"),checks);
            Debug.Log("FROST: "+checks.Count+" RUNTIME CHECKS PASSED; maximum path error "+maximumError);Application.Quit();
        }
    }
}
#endif
