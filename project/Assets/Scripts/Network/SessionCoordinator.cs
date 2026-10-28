using System;
using UnityEngine;
namespace FrostDuel {
    [Serializable] public sealed class Packet {
        public int protocol=2;
        public string type,token,sessionId,balanceJson;
        public int turnId;
        public GameSession state;
        public PlayerAction action;
        public ShotResult shot;
    }
    public sealed class SessionCoordinator : MonoBehaviour {
        public BalanceSettings Balance {get;private set;}
        public GameSession Display {get;private set;}
        public ShotResult Animation {get;private set;}
        public float AnimationTime {get;private set;}
        public bool HostRole {get;private set;}
        public bool Local {get;private set;}
        public bool Paused {get;private set;}
        public bool Started => Display!=null;
        public int MyPlayer => Local && Display!=null ? Display.activePlayer : HostRole?0:1;
        public bool Busy => Animation!=null || pending!=null;
        public bool CanAct => Started && !Display.finished && !Paused && !Busy && Display.activePlayer==MyPlayer && (Local || (transport.IsConnected && (HostRole?peerReady==Display.turnId:ready)));
        public string Notice {get;private set;}="";
        public BluetoothConnection transport {get;private set;}
        TurnManager rules;
        GameSession pending;
        string clientToken=Guid.NewGuid().ToString("N"),peerToken,peerAddress;
        int peerReady=-1;
        bool ready,helloAccepted,requestPending,accepting;
        public event Action<string> PacketSent;
        float requestAt,connectedAt;
        public void Initialize(BalanceSettings balance) {
            Balance=Instantiate(balance);Balance.Validate();rules=new TurnManager(Balance);
            transport=gameObject.AddComponent<BluetoothConnection>();
            transport.Connected+=OnConnected;transport.Disconnected+=()=>{if(Started){Paused=true;ready=false;Notice="Связь потеряна. Ведущий хранит состояние матча.";}};
            transport.Status+=s=>Notice=s;transport.Message+=OnMessage;
        }
        public void CreateMatch() {ResetSession();HostRole=true;accepting=true;Notice="Включение Bluetooth…";transport.Host();}
        public void FindMatch() {ResetSession();HostRole=false;accepting=true;transport.Scan();}
        public void Connect(string address)=>transport.Connect(address);
        public void StartLocal() {ResetSession();HostRole=true;Local=true;Display=Clone(rules.NewMatch(Environment.TickCount));Notice="Локальная проверка: оба игрока на одном экране";}
        public void Retry() {
            Paused=Started;ready=false;helloAccepted=false;peerReady=-1;transport.Drop();
            // Wait one frame for queued disconnect callbacks before establishing a fresh socket.
            StartCoroutine(RetryNextFrame());
        }
        System.Collections.IEnumerator RetryNextFrame() {yield return null;yield return null;if(HostRole)transport.Host();else if(!string.IsNullOrEmpty(transport.Address))transport.Connect(transport.Address);else transport.Scan();}
        public void ResetSession() {
            accepting=false;transport.Drop();Display=pending=null;Animation=null;Paused=false;Local=false;ready=false;helloAccepted=false;requestPending=false;peerReady=-1;peerToken=null;peerAddress=null;
            rules=new TurnManager(Balance);Notice="";
        }
        void OnConnected() {
            if(Local || !accepting){transport.Drop();return;}
            connectedAt=Time.realtimeSinceStartup;helloAccepted=false;ready=false;peerReady=-1;
            if(HostRole) {
                if(peerAddress!=null && peerAddress!=transport.Address){transport.Drop();Notice="Ожидается прежний соперник";return;}
                Notice="Соединение установлено. Проверка матча…";
            } else Send(new Packet{type="hello",token=clientToken,sessionId=Display?.sessionId});
        }
        void Send(Packet p) {string json=JsonUtility.ToJson(p);PacketSent?.Invoke(json);transport.Send(json);}
        void Snapshot() {
            Send(new Packet{type="snapshot",state=rules.State,balanceJson=JsonUtility.ToJson(Balance),sessionId=rules.State.sessionId});
        }
        void OnMessage(string json) {
            Packet p;
            try {p=JsonUtility.FromJson<Packet>(json);}catch{Notice="Некорректное сообщение";return;}
            if(p==null||p.type=="ping")return;
            if(p.protocol!=2){Notice="Версии игры несовместимы";transport.Drop();return;}
            if(HostRole) {
                if(p.type=="hello") {
                    if(string.IsNullOrEmpty(p.token)|| (peerToken!=null && (peerToken!=p.token || p.sessionId!=rules.State?.sessionId))) {transport.Drop();Notice="Это другой матч. Вернитесь в меню на обоих телефонах.";return;}
                    peerToken=p.token;peerAddress=transport.Address;helloAccepted=true;
                    if(rules.State==null)rules.NewMatch(Environment.TickCount);
                    Display=Clone(rules.State);Animation=null;pending=null;Paused=true;Snapshot();
                } else if(helloAccepted && p.type=="ready" && p.sessionId==rules.State.sessionId && p.turnId==rules.State.turnId) {
                    peerReady=p.turnId;Paused=false;Send(new Packet{type="resume",sessionId=rules.State.sessionId,turnId=rules.State.turnId});
                } else if(helloAccepted && p.type=="action") {
                    if(Paused||Busy||peerReady!=rules.State.turnId||p.action==null||p.action.player!=1) {Snapshot();return;}
                    Commit(p.action);
                } else if(helloAccepted && p.type=="sync")Snapshot();
            } else {
                if(p.type=="resume" && Display!=null && p.sessionId==Display.sessionId && p.turnId==Display.turnId) {Paused=false;ready=true;Notice="Связь установлена";return;}
                if(p.type!="snapshot" && p.type!="result")return;
                if(!Valid(p.state)){Notice="Некорректное состояние матча";transport.Drop();return;}
                if(Display!=null && (p.state.sessionId!=Display.sessionId||p.state.turnId<Display.turnId))return;
                if(p.type=="snapshot") {
                    if(!string.IsNullOrEmpty(p.balanceJson)){JsonUtility.FromJsonOverwrite(p.balanceJson,Balance);Balance.Validate();}
                    Display=p.state;pending=null;Animation=null;requestPending=false;Paused=true;ready=false;Ack();
                } else if(Display==null||p.state.turnId>Display.turnId) {requestPending=false;Present(p.state,p.shot);}
            }
        }
        static bool Valid(GameSession s) {
            if(s==null||string.IsNullOrEmpty(s.sessionId)||s.players==null||s.players.Length!=2||s.terrain==null||s.terrain.Length<101||s.terrain.Length>601||s.turnId<1||s.activePlayer<0||s.activePlayer>1)return false;
            foreach(var p in s.players)if(p==null||p.health<0||p.warmth<0||!Finite(p.x)||!Finite(p.groundY))return false;
            foreach(float h in s.terrain)if(!Finite(h))return false;
            return Finite(s.wind);
        }
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public void Act(ActionKind kind,float angle=45,float power=.6f,float aimPhase=0) {
            if(!CanAct||requestPending)return;
            var a=new PlayerAction{sessionId=Display.sessionId,turnId=Display.turnId,player=MyPlayer,kind=kind,angle=angle,power=power,aimPhase=aimPhase};
            if(HostRole)Commit(a);else {requestPending=true;ready=false;requestAt=Time.realtimeSinceStartup;Send(new Packet{type="action",action=a});}
        }
        void Commit(PlayerAction a) {
            if(!rules.Apply(a,out var shot,out var error)) {Notice=error;if(!Local)Snapshot();return;}
            var final=Clone(rules.State);peerReady=-1;
            if(!Local)Send(new Packet{type="result",state=final,shot=shot});
            Present(final,shot);
        }
        void Present(GameSession state,ShotResult shot) {
            pending=state;Animation=shot??new ShotResult();AnimationTime=0;ready=false;
        }
        void Ack() {Send(new Packet{type="ready",sessionId=Display.sessionId,turnId=Display.turnId});}
        void Update() {
            if(!Local && transport!=null && transport.IsConnected && !Started && Time.realtimeSinceStartup-connectedAt>15){transport.Drop();Notice="Не удалось согласовать матч. Повторите подключение.";}
            if(requestPending && Time.realtimeSinceStartup-requestAt>8){requestPending=false;Send(new Packet{type="sync"});}
            if(Animation==null||Paused)return;
            AnimationTime+=Time.deltaTime;
            float duration=Animation.path.Length>1?(Animation.path.Length-1)*Animation.step+.65f:.7f;
            if(AnimationTime<duration)return;
            Notice=Animation.message;Display=pending;pending=null;Animation=null;
            if(!HostRole)Ack();
        }
        static GameSession Clone(GameSession s)=>JsonUtility.FromJson<GameSession>(JsonUtility.ToJson(s));
        void OnApplicationPause(bool pause) {if(pause && Started && !Local && !Paused){Paused=true;ready=false;transport.Drop();}}
    }
}
