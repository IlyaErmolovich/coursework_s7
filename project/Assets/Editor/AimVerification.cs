using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace FrostDuel.Editor {
    public static class AimVerification {
        static readonly List<string> checks=new List<string>();
        static void Check(bool value,string name){if(!value)throw new Exception("AIM FAIL: "+name);checks.Add("PASS: "+name);}
        public static void Run() {
            checks.Clear();var b=ScriptableObject.CreateInstance<BalanceSettings>();
            try {
                foreach(int side in new[]{0,1}) {
                    var g=new AimGesture();var start=new Vector2(640,330);var end=start+new Vector2(side==0?-160:160,120);
                    g.Begin(start,side);for(int i=0;i<60;i++)g.Move(end,1f/60);
                    Check(g.ReleaseAllowed(end)&&Mathf.Abs(g.Angle-36.8699f)<.01f&&Mathf.Abs(g.Power-200f/240)<.001f,"Backward pull maps to forward throw, player "+side);
                    g.Move(start,1f/60);Check(!g.ReleaseAllowed(start),"Return to anchor cancels, player "+side);
                    g.Move(start+new Vector2(side==0?120:-120,80),1f/60);Check(!g.CanRelease,"Forward drag cannot fire, player "+side);
                    g.Cancel();Check(!g.Active,"Explicit cancel, player "+side);
                }
                foreach(var screen in new[]{new Vector2(1280,720),new Vector2(2340,1080),new Vector2(1920,1080)}) {
                    var safe=new Rect(60,20,screen.x-100,screen.y-30);var v=new DuelViewport(safe,screen.y);
                    foreach(var p in new[]{new Vector2(170,400),new Vector2(1120,530),new Vector2(640,340)})
                        Check(Vector2.Distance(v.FromScreen(v.ToScreen(p,screen.y),screen.y),p)<.001f,"Safe-area screen round trip "+screen+" / "+p);
                }
                float maxError=0;
                foreach(int side in new[]{0,1})foreach(ActionKind kind in new[]{ActionKind.Snowball,ActionKind.ThrowIce})foreach(float wind in new[]{-3.2f,0,3.2f})foreach(int heat in new[]{0,50}) {
                    var rules=new TurnManager(b);var s=rules.NewMatch(42);s.activePlayer=side;s.wind=wind;s.blizzard=true;
                    s.players[side].warmth=heat;s.players[side].iceReady=true;
                    var a=new PlayerAction{sessionId=s.sessionId,turnId=s.turnId,player=side,kind=kind,angle=42,power=.85f,aimPhase=1.1f};
                    var preview=Projectile.For(kind).Simulate(s,a,b);
                    Check(Vector2.Distance(preview.path[0],Projectile.ReleasePoint(s.players[side],side))<.00001f,"Arc starts at release: "+side+" / "+kind+" / "+wind+" / "+heat);
                    a=JsonUtility.FromJson<PlayerAction>(JsonUtility.ToJson(a));
                    Check(rules.Apply(a,out var actual,out _)&&actual.path.Length==preview.path.Length,"Authority accepts previewed throw: "+side+" / "+kind+" / "+wind+" / "+heat);
                    for(int i=0;i<preview.path.Length;i++)maxError=Mathf.Max(maxError,Vector2.Distance(preview.path[i],actual.path[i]));
                }
                Check(maxError==0,"Preview vs authoritative flight: maximum world error = "+maxError+" across 24 cases");
                var state=new TurnManager(b).NewMatch(3);state.activePlayer=0;state.players[0].warmth=0;
                var action=new PlayerAction{player=0,angle=45,power=.8f};float previous=45,maxDelta=0,maxOffset=0;
                for(int i=0;i<600;i++) {action.aimPhase=Mathf.Repeat(i/60f*2.2f,Mathf.PI*2);float angle=Projectile.LaunchAngle(state,action,b);maxDelta=Mathf.Max(maxDelta,Mathf.Abs(angle-previous));maxOffset=Mathf.Max(maxOffset,Mathf.Abs(angle-45));previous=angle;}
                Check(maxDelta<.08f&&maxOffset<=2.001f,"Cold sway bounded to 2 degrees; frame delta below .08 degrees at 60fps");
                state.players[0].warmth=50;action.aimPhase=1.4f;Check(Projectile.LaunchAngle(state,action,b)==45,"Warm aim has zero sway");
                state.wind=3.2f;state.blizzard=false;action.aimPhase=0;
                var snow=new Snowball().Simulate(state,action,b);var ice=new IceProjectile().Simulate(state,action,b);
                Check(snow.path[45].x>ice.path[45].x,"Wind deflects snow more than ice");
                state.blizzard=true;var storm=new Snowball().Simulate(state,action,b);Check(storm.path[45].x>snow.path[45].x,"Blizzard increases snow drift");
                File.WriteAllLines("Builds/aim-verification.txt",checks);Debug.Log("FROST: "+checks.Count+" AIM CHECKS PASSED");
            } finally {UnityEngine.Object.DestroyImmediate(b);}
        }
    }
}
