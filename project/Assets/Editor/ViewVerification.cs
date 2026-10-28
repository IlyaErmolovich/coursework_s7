using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace FrostDuel.Editor {
    public static class ViewVerification {
        static readonly List<string> checks=new List<string>();
        static void Check(bool value,string name){if(!value)throw new Exception("VIEW FAIL: "+name);checks.Add("PASS: "+name);}
        public static void Run() {
            checks.Clear();var b=ScriptableObject.CreateInstance<BalanceSettings>();
            try {
                var rules=new TurnManager(b);var s=rules.NewMatch(42);
                Terrain.Crater(s,new Vector2(13,Terrain.Height(s.terrain,13,b)),1.2f,b);
                string before=JsonUtility.ToJson(s);
                foreach(int viewer in new[]{0,1})foreach(float width in new[]{1100f,1280f,1560f}) {
                    var view=new DuelView();view.Update(s,b,viewer,width,null,0,1f/60,true);
                    var own=s.players[viewer];var enemy=s.players[1-viewer];
                    Check(Mathf.Abs(view.Project(new Vector2(own.x,own.groundY)).x-width*.24f)<.01f,"Own character at left quarter, viewer "+viewer+" width "+width);
                    Check(view.Project(new Vector2(enemy.x,enemy.groundY)).x>width,"Enemy initially outside right edge, viewer "+viewer+" width "+width);
                    float error=0;
                    for(int i=0;i<s.terrain.Length;i++) {
                        float x=i*b.worldWidth/(s.terrain.Length-1);var p=new Vector2(x,s.terrain[i]);
                        error=Mathf.Max(error,Vector2.Distance(p,view.Unproject(view.Project(p))));
                    }
                    Check(error<.00001f,"Terrain/crater projection and touch inverse, viewer "+viewer+" width "+width);
                    Check(view.Direction==(viewer==0?1:-1),"Wind direction mirrors once, viewer "+viewer+" width "+width);
                    foreach(int actor in new[]{0,1})foreach(var kind in new[]{ActionKind.Snowball,ActionKind.ThrowIce}) {
                        s.activePlayer=actor;s.wind=actor==0?2:-2;
                        var shot=Projectile.For(kind).Simulate(s,new PlayerAction{player=actor,kind=kind,angle=44,power=1},b);
                        int count=DuelView.PreviewCount(shot);float length=0;
                        for(int i=1;i<count;i++)length+=Vector2.Distance(shot.path[i-1],shot.path[i]);
                        Check(count>4&&count<shot.path.Length&&length<=DuelView.PreviewLength&&(count-1)*shot.step<=DuelView.PreviewSeconds,"Short initial prediction only, viewer "+viewer+" actor "+actor+" "+kind);
                        float maxDistance=0;
                        for(float t=0;t<(shot.path.Length-1)*shot.step+.5f;t+=1f/60) {
                            view.Update(s,b,viewer,width,shot,t,1f/60,false);
                            if(t>.5f){var p=view.Project(DuelView.Position(shot,t));maxDistance=Mathf.Max(maxDistance,Mathf.Abs(p.x-width/2));CheckSilent(p.y>=120&&p.y<=720,"Projectile vertical frame");}
                        }
                        Check(maxDistance<width*.4f,"Outgoing/incoming projectile followed beyond initial frame, viewer "+viewer+" actor "+actor+" "+kind);
                        for(int i=0;i<100;i++)view.Update(s,b,viewer,width,null,0,1f/60,false);
                        Check(view.Ready&&Mathf.Abs(view.Project(new Vector2(own.x,own.groundY)).x-width*.24f)<.1f,"Camera returns home, viewer "+viewer+" actor "+actor+" "+kind);
                    }
                }
                // Restore only the weather/turn test inputs; presentation must not mutate any match data.
                var initial=JsonUtility.FromJson<GameSession>(before);s.activePlayer=initial.activePlayer;s.wind=initial.wind;
                Check(JsonUtility.ToJson(s)==before,"Projection, camera and preview leave authoritative state and crater unchanged");
                File.WriteAllLines("Builds/view-verification.txt",checks);Debug.Log("FROST: "+checks.Count+" VIEW CHECKS PASSED");
            } finally {UnityEngine.Object.DestroyImmediate(b);}
        }
        static void CheckSilent(bool ok,string message){if(!ok)throw new Exception(message);}
    }
}
