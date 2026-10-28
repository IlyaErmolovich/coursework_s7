using System.Collections.Generic;
using UnityEngine;
namespace FrostDuel {
    public abstract class Projectile {
        public abstract float WindFactor(BalanceSettings b);
        public abstract float CraterRadius(BalanceSettings b);
        public abstract void Hit(PlayerState target, bool head, BalanceSettings b);
        public static Projectile For(ActionKind kind) => kind == ActionKind.ThrowIce ? (Projectile)new IceProjectile() : new Snowball();
        public static Vector2 ReleasePoint(PlayerState p,int player) => new Vector2(p.x+(player==0?.65f:-.65f),p.groundY+1.2f);
        public static float LaunchAngle(GameSession s,PlayerAction a,BalanceSettings b) {
            float sway=s.players[a.player].warmth==0 ? Mathf.Sin(a.aimPhase)*b.frozenAimDegrees : 0;
            return Mathf.Clamp(a.angle+sway,5,85);
        }
        public ShotResult Simulate(GameSession s, PlayerAction a, BalanceSettings b) {
            float direction=a.player==0?1:-1, radians=LaunchAngle(s,a,b)*Mathf.Deg2Rad;
            Vector2 pos=ReleasePoint(s.players[a.player],a.player);
            float speed=Mathf.Lerp(b.minSpeed,b.maxSpeed,a.power);
            Vector2 velocity=new Vector2(direction*Mathf.Cos(radians),Mathf.Sin(radians))*speed;
            float wind=s.wind*WindFactor(b)*(s.blizzard && a.kind==ActionKind.Snowball?b.blizzardMultiplier:1);
            var path=new List<Vector2>{pos};
            var result=new ShotResult {kind=a.kind,step=b.physicsStep};
            for(int n=0;n<Mathf.CeilToInt(b.maxFlightSeconds/b.physicsStep);n++) {
                Vector2 old=pos;
                velocity+=new Vector2(wind,-b.gravity)*b.physicsStep;
                pos+=velocity*b.physicsStep;
                // Swept substeps prevent tunnelling through the small head collider.
                int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(old,pos)/.06f));
                bool stop=false;
                for(int k=1;k<=samples;k++) {
                    Vector2 q=Vector2.Lerp(old,pos,(float)k/samples);
                    for(int j=0;j<2;j++) {
                        if(j==a.player && n*b.physicsStep<.3f) continue;
                        PlayerState t=s.players[j];
                        bool head=Vector2.Distance(q,new Vector2(t.x,t.groundY+b.bodyHeight+b.headRadius))<=b.headRadius+.10f;
                        bool body=Mathf.Abs(q.x-t.x)<=b.bodyRadius+.10f && q.y>=t.groundY && q.y<=t.groundY+b.bodyHeight;
                        if(head||body) { result.target=j;result.headshot=head;result.impact=true;pos=q;stop=true;break; }
                    }
                    if(stop) break;
                    if(q.x<0||q.x>b.worldWidth||q.y<0) { pos=q;stop=true;break; }
                    if(q.y<=Terrain.Height(s.terrain,q.x,b)) { result.impact=true;pos=q;stop=true;break; }
                }
                path.Add(pos);if(stop)break;
            }
            result.path=path.ToArray();result.end=pos;return result;
        }
    }
    public sealed class Snowball : Projectile {
        public override float WindFactor(BalanceSettings b)=>b.snowWindFactor;
        public override float CraterRadius(BalanceSettings b)=>b.snowCraterRadius;
        public override void Hit(PlayerState p,bool head,BalanceSettings b) {p.health=Mathf.Max(0,p.health-b.snowDamage);p.warmth=Mathf.Max(0,p.warmth-b.snowWarmthDamage);}
    }
    public sealed class IceProjectile : Projectile {
        public override float WindFactor(BalanceSettings b)=>b.iceWindFactor;
        public override float CraterRadius(BalanceSettings b)=>b.iceCraterRadius;
        public override void Hit(PlayerState p,bool head,BalanceSettings b) {p.health=Mathf.Max(0,p.health-(head?b.iceHeadDamage:b.iceBodyDamage));p.warmth=Mathf.Max(0,p.warmth-b.iceWarmthDamage);}
    }
}
