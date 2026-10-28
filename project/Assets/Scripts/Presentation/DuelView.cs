using UnityEngine;
namespace FrostDuel {
    // Presentation only. Canonical world coordinates and serialized actions never change sides.
    public sealed class DuelView {
        public const float Scale=62;
        public const float PreviewLength=4.5f, PreviewSeconds=.65f;
        public Vector2 Center {get; private set;}
        public int Viewer {get; private set;}
        public float Width {get; private set;}
        public float WorldWidth {get; private set;}
        public bool Ready {get; private set;}
        public float Direction=>Viewer==0?1:-1;
        public float ViewX(float canonical)=>Viewer==0?canonical:WorldWidth-canonical;
        public Vector2 Project(Vector2 p)=>new Vector2(Width/2+(ViewX(p.x)-Center.x)*Scale,360-(p.y-Center.y)*Scale);
        public Vector2 Unproject(Vector2 p) {
            float x=Center.x+(p.x-Width/2)/Scale;
            return new Vector2(Viewer==0?x:WorldWidth-x,Center.y+(360-p.y)/Scale);
        }
        public void Update(GameSession state,BalanceSettings balance,int viewer,float width,ShotResult shot,float time,float dt,bool reset) {
            Viewer=viewer;Width=width;WorldWidth=balance.worldWidth;
            var own=state.players[viewer];
            var home=new Vector2(ViewX(own.x)+width*.26f/Scale,own.groundY+200/Scale);
            Vector2 target=home;
            if(shot?.path!=null&&shot.path.Length>1) {
                var p=Position(shot,time);
                // No knowledge of the future arc: follow the current projectile, including incoming shots.
                target=new Vector2(ViewX(p.x),Mathf.Max(home.y,p.y));
            }
            Center=reset?home:Vector2.Lerp(Center,target,1-Mathf.Exp(-8*Mathf.Max(0,dt)));
            Ready=shot==null&&Vector2.Distance(Center,home)<.08f;
        }
        public static Vector2 Position(ShotResult shot,float time) {
            float index=Mathf.Max(0,time)/shot.step;int n=Mathf.Min((int)index,shot.path.Length-1);
            return Vector2.Lerp(shot.path[n],shot.path[Mathf.Min(n+1,shot.path.Length-1)],index-n);
        }
        public static int PreviewCount(ShotResult shot) {
            if(shot?.path==null||shot.path.Length==0)return 0;
            float distance=0;int count=1;
            for(int i=1;i<shot.path.Length&&i*shot.step<=PreviewSeconds;i++) {
                distance+=Vector2.Distance(shot.path[i-1],shot.path[i]);
                if(distance>PreviewLength)break;count++;
            }
            return count;
        }
    }
}
