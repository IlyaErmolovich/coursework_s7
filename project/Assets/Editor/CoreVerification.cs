using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
namespace FrostDuel.Editor {
    public static class CoreVerification {
        static readonly List<string> checks=new List<string>();
        static void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);checks.Add("PASS: "+name);}
        static PlayerAction Action(GameSession s,ActionKind kind,float angle=45,float power=.6f)=>new PlayerAction{sessionId=s.sessionId,turnId=s.turnId,player=s.activePlayer,kind=kind,angle=angle,power=power};
        [MenuItem("Frost Duel/Run core verification")]
        public static void Run() {
            checks.Clear();ProjectBuilder.Configure();
            var b=ScriptableObject.CreateInstance<BalanceSettings>();var rules=new TurnManager(b);var s=rules.NewMatch(42);
            Check(s.players[0].health==100&&s.players[1].warmth==50,"Initial 100 health / 50 warmth");
            Check(s.players[0].x==b.worldWidth-s.players[1].x,"Symmetric player positions");
            Check(Mathf.Abs(s.players[0].groundY-s.players[1].groundY)<.26f,"Comparable spawn heights");
            var same=Terrain.Generate(42,b);Check(Equal(s.terrain,same),"Seed deterministically generates terrain");
            var weather=JsonUtility.FromJson<GameSession>(JsonUtility.ToJson(s));WeatherController.Next(weather,b);Check(weather.wind==s.wind&&weather.blizzard==s.blizzard,"Weather reproducible per turn");
            int actor=s.activePlayer;var prepare=Action(s,ActionKind.PrepareIce);
            Check(rules.Apply(prepare,out _,out _)&&s.players[actor].iceReady&&s.players[actor].warmth==40&&s.activePlayer!=actor,"Preparation costs ten warmth and gives opponent a turn");
            Check(!rules.Apply(prepare,out _,out _),"Duplicate action rejected");
            var wrong=Action(s,ActionKind.Snowball);wrong.player=1-s.activePlayer;Check(!rules.Apply(wrong,out _,out _),"Out-of-turn player rejected");
            wrong=Action(s,ActionKind.Snowball);wrong.sessionId="old";Check(!rules.Apply(wrong,out _,out _),"Foreign session rejected");
            wrong=Action(s,ActionKind.Snowball);wrong.angle=float.NaN;Check(!rules.Apply(wrong,out _,out _),"NaN input rejected");
            wrong=Action(s,ActionKind.Snowball);wrong.power=1.1f;Check(!rules.Apply(wrong,out _,out _),"Out-of-range power rejected");
            Check(!rules.Apply(Action(s,ActionKind.ThrowIce),out _,out _),"Unprepared ice rejected");
            s.players[s.activePlayer].warmth=9;Check(!rules.Apply(Action(s,ActionKind.PrepareIce),out _,out _),"Insufficient warmth rejected");
            rules=new TurnManager(b);s=rules.NewMatch(1);int attacker=s.activePlayer,target=1-attacker;s.wind=0;s.blizzard=false;s.players[target].iceReady=true;
            var best=FindHit(s,b,ActionKind.Snowball,false);Check(best!=null,"Snowball can reach opposing character");
            int oldWarmth=s.players[attacker].warmth;rules.Apply(best,out var hit,out _);
            Check(hit.target==target&&s.players[target].health==92&&s.players[target].warmth==28,"Snow damage and warmth loss");
            Check(!s.players[target].iceReady,"Enemy hit interrupts ice preparation");
            Check(s.players[attacker].warmth==oldWarmth,"Attack never restores attacker warmth");
            rules=new TurnManager(b);s=rules.NewMatch(2);attacker=s.activePlayer;target=1-attacker;s.wind=0;s.blizzard=false;s.players[attacker].iceReady=true;
            best=FindHit(s,b,ActionKind.ThrowIce,true);Check(best!=null,"Ice headshot is geometrically reachable");
            rules.Apply(best,out hit,out _);Check(hit.headshot&&s.players[target].health==40&&!s.finished,"Ice headshot deals sixty percent; survivor continues");
            Check(!s.players[attacker].iceReady,"Ice consumed on throw");
            rules=new TurnManager(b);s=rules.NewMatch(5);actor=s.activePlayer;s.players[actor].warmth=80;
            rules.Apply(Action(s,ActionKind.WarmUp),out _,out _);Check(s.players[actor].warmth==100,"Warm action clamps at maximum");
            rules=new TurnManager(b);s=rules.NewMatch(7);target=1-s.activePlayer;s.players[target].warmth=0;s.players[target].health=7;
            rules.Apply(Action(s,ActionKind.WarmUp),out _,out _);Check(s.finished&&s.winner==1-target,"Freezing damage can finish match");
            rules=new TurnManager(b);s=rules.NewMatch(8);actor=s.activePlayer;s.players[actor].warmth=20;s.players[actor].quietTurns=1;
            rules.Apply(Action(s,ActionKind.PrepareIce),out _,out _);rules.Apply(Action(s,ActionKind.WarmUp),out _,out _);
            Check(s.players[actor].warmth==16,"Passive recovery only after quiet own turns");
            rules=new TurnManager(b);s=rules.NewMatch(19);float center=Terrain.Height(s.terrain,16,b);Terrain.Crater(s,new Vector2(16,center),1,b);
            Check(Terrain.Height(s.terrain,16,b)<center-.9f,"Crater deforms terrain");
            float ground=Terrain.Height(s.terrain,s.players[0].x,b);Terrain.Crater(s,new Vector2(s.players[0].x,ground),1,b);Check(Terrain.Height(s.terrain,s.players[0].x,b)==ground,"Fixed player pedestal remains stable");
            var restored=JsonUtility.FromJson<GameSession>(JsonUtility.ToJson(s));Check(Equal(s.terrain,restored.terrain)&&restored.turnId==s.turnId&&restored.wind==s.wind,"Full snapshot preserves deformed terrain and turn/weather");
            TestFraming();
            // Full matches use only legal actions and host simulation, including preparation/warm-up.
            int total=0;
            for(int seed=10;seed<15;seed++) {
                rules=new TurnManager(b);s=rules.NewMatch(seed);int turns=0;
                while(!s.finished&&turns<250) {
                    var p=s.players[s.activePlayer];PlayerAction a;
                    if(p.iceReady)a=FindHit(s,b,ActionKind.ThrowIce,true)??FindHit(s,b,ActionKind.ThrowIce,false)??Action(s,ActionKind.ThrowIce);
                    else if(p.warmth<b.iceCost)a=Action(s,ActionKind.WarmUp);
                    else a=Action(s,ActionKind.PrepareIce);
                    int turn=s.turnId;CheckSilent(rules.Apply(a,out _,out _)&&s.turnId==turn+1,"Full-match action");turns++;
                }
                Check(s.finished&&s.winner>=0,"Full legal match seed "+seed+" finishes in "+turns+" turns");total+=turns;
            }
            Directory.CreateDirectory("Builds");File.WriteAllLines("Builds/core-verification.txt",checks);Debug.Log("FROST: "+checks.Count+" CHECKS PASSED; "+total+" simulated turns");
            UnityEngine.Object.DestroyImmediate(b);
        }
        static void CheckSilent(bool condition,string name){if(!condition)throw new Exception(name);}
        static bool Equal(float[] a,float[] b){if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
        static PlayerAction FindHit(GameSession s,BalanceSettings b,ActionKind kind,bool head) {
            var projectile=Projectile.For(kind);
            for(float power=.25f;power<=1.001f;power+=.025f)for(float angle=8;angle<=82;angle+=1) {
                var a=Action(s,kind,angle,power);var shot=projectile.Simulate(s,a,b);
                if(shot.target==1-s.activePlayer && (!head||shot.headshot))return a;
            }
            return null;
        }
        static void TestFraming() {
            string a="{\"text\":\"Снежная дуэль\"}";var framer=new LineFramer();var got=new List<string>();byte[] data=Encoding.UTF8.GetBytes(a+"\n"+a+"\n");
            foreach(byte b in data)framer.Feed(new[]{b},got.Add);
            Check(got.Count==2&&got[0]==a&&got[1]==a,"Fragmented UTF-8 frames round-trip");
            got.Clear();framer.Feed(data,got.Add);Check(got.Count==2,"Coalesced stream frames split");
            framer.Feed(Encoding.UTF8.GetBytes("partial"),got.Add);framer.Clear();got.Clear();framer.Feed(Encoding.UTF8.GetBytes(a+"\n"),got.Add);Check(got.Count==1&&got[0]==a,"Reconnect clears incomplete frame");
            bool bounded=false;try{framer.Feed(new byte[262145],got.Add);}catch(InvalidOperationException){bounded=true;}Check(bounded,"Oversized frame rejected");
        }
    }
}
