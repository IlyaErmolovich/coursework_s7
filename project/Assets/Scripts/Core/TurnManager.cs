using System;
using UnityEngine;
namespace FrostDuel {
    public sealed class TurnManager {
        public readonly BalanceSettings balance;
        public GameSession State {get;private set;}
        public TurnManager(BalanceSettings b) {balance=b;}
        public GameSession NewMatch(int seed) {
            var b=balance;
            State=new GameSession {sessionId=Guid.NewGuid().ToString("N"),seed=seed,activePlayer=(seed & 1),terrain=Terrain.Generate(seed,b),players=new PlayerState[2]};
            for(int i=0;i<2;i++) {float x=i==0?b.characterInset:b.worldWidth-b.characterInset; State.players[i]=new PlayerState {health=b.maxHealth,warmth=b.initialWarmth,x=x,groundY=Terrain.Height(State.terrain,x,b)};}
            WeatherController.Next(State,b);return State;
        }
        public bool CanAct(PlayerAction a,out string error) {
            error=""; var s=State;
            if(s==null||a==null||s.finished)error="Матч завершён";
            else if(a.sessionId!=s.sessionId||a.turnId!=s.turnId||a.player!=s.activePlayer)error="Устаревший ход";
            else if(!Enum.IsDefined(typeof(ActionKind),a.kind))error="Неизвестное действие";
            else if(a.kind==ActionKind.PrepareIce && (s.players[a.player].iceReady||s.players[a.player].warmth<balance.iceCost))error="Недостаточно тепла или лёд уже готов";
            else if(a.kind==ActionKind.ThrowIce && !s.players[a.player].iceReady)error="Лёд не подготовлен";
            else if(float.IsNaN(a.angle)||float.IsInfinity(a.angle)||float.IsNaN(a.power)||float.IsInfinity(a.power)||a.angle<5||a.angle>85||a.power<0||a.power>1)error="Неверные параметры броска";
            return error.Length==0;
        }
        public bool Apply(PlayerAction a,out ShotResult result,out string error) {
            result=null;if(!CanAct(a,out error))return false;
            var s=State;var p=s.players[a.player];var b=balance;
            result=new ShotResult {kind=a.kind};
            switch(a.kind) {
                case ActionKind.PrepareIce: p.warmth-=b.iceCost;p.iceReady=true;result.message="Лёд подготовлен. Попадание соперника собьёт подготовку.";break;
                case ActionKind.WarmUp: p.warmth=Mathf.Min(b.maxWarmth,p.warmth+b.warmActionGain);result.message="Игрок утеплился";break;
                default:
                    var projectile=Projectile.For(a.kind);
                    result=projectile.Simulate(s,a,b);
                    if(a.kind==ActionKind.ThrowIce)p.iceReady=false;
                    if(result.target>=0) {
                        var t=s.players[result.target];bool interrupted=t.iceReady;
                        projectile.Hit(t,result.headshot,b);t.iceReady=false;t.quietTurns=0;
                        result.message=(result.headshot?"Попадание в голову!":"Попадание!")+(interrupted?" Подготовка льда сбита.":"");
                    } else result.message=result.impact?"Снаряд попал в снег":"Промах";
                    if(result.impact)Terrain.Crater(s,result.end,projectile.CraterRadius(b),b);
                    break;
            }
            s.turnId++;
            CheckWinner();
            if(!s.finished) {
                s.activePlayer=1-s.activePlayer;var next=s.players[s.activePlayer];
                next.quietTurns++;
                if(next.quietTurns>=b.quietTurnsForRecovery)next.warmth=Mathf.Min(b.maxWarmth,next.warmth+b.passiveWarmthGain);
                if(next.warmth==0) {next.health=Mathf.Max(0,next.health-b.freezeDamage);result.message+=" Мороз: −"+b.freezeDamage+" здоровья.";}
                CheckWinner();WeatherController.Next(s,b);
            }
            return true;
        }
        void CheckWinner() {
            if(State.players[0].health>0 && State.players[1].health>0)return;
            State.finished=true;State.winner=State.players[0].health<=0?(State.players[1].health<=0?-1:1):0;
        }
    }
}
