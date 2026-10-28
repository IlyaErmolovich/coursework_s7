#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
namespace FrostDuel {
    // Development-only fixture. Exercises the same entry points as touch; does not fake Android hardware.
    public sealed class PreviewCapture : MonoBehaviour {
        readonly List<string> checks=new List<string>();
        string folder;
        DuelApp app;
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-capturePreview")>=0)new GameObject("Runtime verification").AddComponent<PreviewCapture>();}
        void Check(bool ok,string text) {
            if(!ok){File.WriteAllText(Path.Combine(folder,"FAILED.txt"),text);Debug.LogError("RUNTIME FAIL: "+text);Application.Quit(1);throw new Exception(text);}
            checks.Add("PASS: "+text);
        }
        void Set(object obj,string name,object value) {
            var f=obj.GetType().GetField(name,Hidden)??obj.GetType().GetField("<"+name+">k__BackingField",Hidden);f.SetValue(obj,value);
        }
        void Fixture(int side,ActionKind kind,int warmth,float wind) {
            app.Session.StartLocal();
            var rules=(TurnManager)typeof(SessionCoordinator).GetField("rules",Hidden).GetValue(app.Session);
            foreach(var s in new[]{rules.State,app.Session.Display}) {
                s.activePlayer=side;s.wind=wind;s.blizzard=true;
                s.players[side].warmth=warmth;s.players[side].iceReady=kind==ActionKind.ThrowIce;
            }
        }
        void Capture(string name)=>ScreenCapture.CaptureScreenshot(Path.Combine(folder,name+".png"));
        IEnumerator Start() {
            folder=Path.GetFullPath("Builds/VisualVerification");Directory.CreateDirectory(folder);
            string failure=Path.Combine(folder,"FAILED.txt");if(File.Exists(failure))File.Delete(failure);
            yield return new WaitForSeconds(2);app=FindFirstObjectByType<DuelApp>();Capture("00-menu");
            yield return new WaitForSeconds(.4f);
            int caseId=0;float maximumError=0;
            foreach(int side in new[]{0,1})foreach(ActionKind kind in new[]{ActionKind.Snowball,ActionKind.ThrowIce})foreach(int heat in new[]{50,0}) {
                Fixture(side,kind,heat,side==0?3.2f:-3.2f);yield return null;
                app.ChooseSnow();if(kind==ActionKind.ThrowIce)app.ChooseIce();
                yield return new WaitForSeconds(.2f);
                foreach(var r in new[]{app.SnowButton,app.IceButton,app.WarmButton,app.MenuButton}){app.BeginAim(r.center);Check(!app.IsAiming,"UI touch excluded "+r+" case "+caseId);}
                Vector2 start=new Vector2(app.CanvasWidth*.5f,330),end=start+new Vector2(side==0?-175:175,148);
                app.BeginAim(start);Check(app.IsAiming,"Free field begins aim, case "+caseId);
                for(int i=0;i<65;i++){app.MoveAim(end);yield return null;}
                var preview=app.AimPreview;
                Check(preview.kind==kind,"Preview uses selected projectile, case "+caseId);
                Check(preview.path.Length>30&&Vector2.Distance(preview.path[0],Projectile.ReleasePoint(app.Session.Display.players[side],side))<.0001f,"Visible arc starts at character release, case "+caseId);
                int visible=0;foreach(var p in preview.path){var q=app.World(p);if(q.y>=120&&q.y<=720&&q.x>=0&&q.x<=app.CanvasWidth)visible++;}
                Check(visible==preview.path.Length,"Entire preview fits playfield after camera settles, case "+caseId);
                string tag=(++caseId).ToString("00")+"-"+(side==0?"left":"right")+"-"+kind+"-"+(heat==0?"cold":"warm");
                Capture(tag+"-aim");yield return new WaitForSeconds(.25f);
                // Save the last displayed samples; no resimulation on release.
                preview=app.AimPreview;app.EndAim(end);var actual=app.Session.Animation;
                Check(actual!=null&&actual.path.Length==preview.path.Length,"Release commits displayed arc, "+tag);
                for(int i=0;i<preview.path.Length;i++)maximumError=Mathf.Max(maximumError,Vector2.Distance(preview.path[i],actual.path[i]));
                app.BeginAim(start);Check(!app.IsAiming,"Input blocked during result, "+tag);
                yield return new WaitForSeconds(.55f);Capture(tag+"-flight");
                yield return new WaitForSeconds(.3f);
            }
            Check(maximumError==0,"Displayed vs actual runtime trajectory maximum error = "+maximumError+" world units in eight cases");
            Fixture(0,ActionKind.Snowball,50,0);yield return null;
            Vector2 anchor=new Vector2(640,330),pull=new Vector2(470,460);int turn=app.Session.Display.turnId;
            app.BeginAim(anchor);for(int i=0;i<20;i++){app.MoveAim(pull);yield return null;}
            app.EndAim(anchor);Check(!app.IsAiming&&app.Session.Animation==null&&app.Session.Display.turnId==turn,"Returning pointer to anchor cancels without spending turn");
            app.BeginAim(anchor);app.MoveAim(pull);app.CancelAim();app.EndAim(pull);Check(app.Session.Animation==null,"Explicit cancellation never fires on later release");
            app.BeginAim(anchor);app.MoveAim(pull);app.EndAim(app.SnowButton.center);Check(app.Session.Animation==null,"Release over UI cancels without shot");
            app.BeginAim(anchor);app.MoveAim(pull);app.EndAim(new Vector2(-5,400));Check(app.Session.Animation==null,"Release outside field cancels");
            Set(app,"confirmExit",true);app.BeginAim(anchor);Check(!app.IsAiming,"Menu modal blocks aiming");Set(app,"confirmExit",false);
            Set(app.Session,"Local",false);Set(app.Session,"HostRole",true);Set(app.Session.transport,"IsConnected",true);Set(app.Session,"peerReady",turn);
            app.Session.Display.activePlayer=1;app.BeginAim(anchor);Check(!app.IsAiming&&!app.Session.CanAct,"Opponent turn blocks aiming");
            Set(app.Session.transport,"IsConnected",false);
            Screen.SetResolution(1560,720,false);yield return new WaitForSeconds(1);
            Fixture(1,ActionKind.Snowball,50,-3.2f);yield return null;
            anchor=new Vector2(app.CanvasWidth/2,320);pull=anchor+new Vector2(175,148);app.BeginAim(anchor);
            for(int i=0;i<70;i++){app.MoveAim(pull);yield return null;}
            Check(app.CanvasWidth>1500&&Mathf.Abs(app.MenuButton.xMax-(app.CanvasWidth-24))<.01f,"Wide 19.5:9 canvas and edge-anchored controls");
            Check(app.AimPreview.kind==ActionKind.Snowball,"New session resets previously selected ice even when turn number matches");
            Capture("09-wide-right-aim");yield return new WaitForSeconds(.4f);
            File.WriteAllLines(Path.Combine(folder,"runtime-verification.txt"),checks);
            Debug.Log("FROST: "+checks.Count+" RUNTIME CHECKS PASSED; maximum path error "+maximumError);
            Application.Quit();
        }
    }
}
#endif
