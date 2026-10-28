using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace FrostDuel.Editor {
    // Two actual coordinators, serialized packet delivery; no claim of radio/JNI testing.
    public static class ProtocolVerification {
        const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly List<string> checks=new List<string>();
        static void Check(bool yes,string name){if(!yes)throw new Exception("PROTOCOL FAIL: "+name);checks.Add("PASS: "+name);}
        static void Set(object obj,string name,object value){var field=obj.GetType().GetField(name,Hidden)??obj.GetType().GetField("<"+name+">k__BackingField",Hidden);field.SetValue(obj,value);}
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
        static void Finish(SessionCoordinator c){if(c.Animation!=null){Set(c,"AnimationTime",100f);Call(c,"Update");}}
        [MenuItem("Frost Duel/Run protocol verification")]
        public static void Run() {
            checks.Clear();var b=ScriptableObject.CreateInstance<BalanceSettings>();
            var hg=new GameObject("Verification Host");var cg=new GameObject("Verification Client");
            try {
                var h=hg.AddComponent<SessionCoordinator>();var c=cg.AddComponent<SessionCoordinator>();h.Initialize(b);c.Initialize(b);
                var queue=new Queue<Action>();
                h.PacketSent+=json=>queue.Enqueue(()=>Call(c,"OnMessage",json));
                c.PacketSent+=json=>queue.Enqueue(()=>Call(h,"OnMessage",json));
                Action flush=()=>{int n=0;while(queue.Count>0){if(++n>100)throw new Exception("Protocol loop");queue.Dequeue()();}};
                Set(h,"HostRole",true);Set(c,"HostRole",false);Set(h,"accepting",true);Set(c,"accepting",true);
                Set(h.transport,"IsConnected",true);Set(c.transport,"IsConnected",true);
                Call(h,"OnConnected");Call(c,"OnConnected");flush();
                Check(h.Started&&c.Started&&!h.Paused&&!c.Paused,"Initial hello / full snapshot / ready / resume handshake");
                Check(JsonUtility.ToJson(h.Display)==JsonUtility.ToJson(c.Display),"Initial states identical");
                var actor=h.Display.activePlayer==0?h:c;int turn=h.Display.turnId;
                var action=new PlayerAction{sessionId=h.Display.sessionId,turnId=turn,player=h.Display.activePlayer,kind=ActionKind.PrepareIce,angle=45,power=.5f};
                actor.Act(ActionKind.PrepareIce);flush();Check(!h.CanAct&&!c.CanAct,"Actions locked while result is playing");
                Finish(h);Finish(c);flush();
                Check(h.Display.turnId==turn+1&&JsonUtility.ToJson(h.Display)==JsonUtility.ToJson(c.Display),"Action round-trip changes both states exactly once");
                if(action.player==1){Call(h,"OnMessage",JsonUtility.ToJson(new Packet{type="action",action=action}));flush();}
                else {Call(h,"Commit",action);flush();}
                Check(h.Display.turnId==turn+1&&c.Display.turnId==turn+1,"Duplicate delivery cannot advance turn");
                // Commit next throw, lose the connection before the result reaches the client.
                actor=h.Display.activePlayer==0?h:c;
                actor.Act(ActionKind.Snowball,25,.4f);
                if(actor==c){queue.Dequeue()();} // deliver only action; withhold result
                queue.Clear();h.transport.Drop();c.transport.Drop();
                Check(h.Paused&&c.Paused&&!h.CanAct&&!c.CanAct,"Disconnect locks both coordinators");
                Set(h.transport,"IsConnected",true);Set(c.transport,"IsConnected",true);
                Call(h,"OnConnected");Call(c,"OnConnected");flush();
                Check(!h.Paused&&!c.Paused&&!h.Busy&&!c.Busy,"Manual reconnect resolves interrupted animation");
                Check(h.Display.turnId==turn+2&&JsonUtility.ToJson(h.Display)==JsonUtility.ToJson(c.Display),"Reconnect recovers committed turn and full crater state");
                var before=JsonUtility.ToJson(h.Display);
                var rules=(TurnManager)typeof(SessionCoordinator).GetField("rules",Hidden).GetValue(h);
                var stale=JsonUtility.FromJson<GameSession>(before);stale.turnId--;
                Call(c,"OnMessage",JsonUtility.ToJson(new Packet{type="snapshot",state=stale}));Check(JsonUtility.ToJson(c.Display)==before,"Stale snapshot ignored");
                Call(h,"OnMessage",JsonUtility.ToJson(new Packet{type="hello",token="another-client",sessionId=rules.State.sessionId}));
                Check(!h.transport.IsConnected&&h.Paused,"Different peer token cannot resume existing match");
                Directory.CreateDirectory("Builds");File.WriteAllLines("Builds/protocol-verification.txt",checks);Debug.Log("FROST: "+checks.Count+" PROTOCOL CHECKS PASSED");
            } finally {UnityEngine.Object.DestroyImmediate(hg);UnityEngine.Object.DestroyImmediate(cg);UnityEngine.Object.DestroyImmediate(b);}
        }
    }
}
