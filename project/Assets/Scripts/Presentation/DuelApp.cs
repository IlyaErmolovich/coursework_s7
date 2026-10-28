using System;
using UnityEngine;
namespace FrostDuel {
    // Placeholder view: replace this class without changing combat or network code.
    public sealed class DuelApp : MonoBehaviour {
        enum ScreenKind { Menu, Host, Join, Game }
        ScreenKind screen;
        SessionCoordinator session;
        Texture2D circle;
        GUIStyle label,button,title,small;
        Font font;
        Rect safe;
        float scale;
        Vector2 origin;
        bool dragging;
        int finger=-1;
        ActionKind selected=ActionKind.Snowball;
        float angle=45,power=.6f;
        Vector2 startDrag,aimDrag;
        Vector2 scroll;
        int lastTurn=-1;
        readonly Color ink=new Color(.86f,.94f,1),muted=new Color(.53f,.68f,.78f),panel=new Color(.045f,.10f,.16f),cyan=new Color(.28f,.84f,.96f),orange=new Color(1,.59f,.32f);
        public SessionCoordinator Session=>session;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() {if(FindFirstObjectByType<DuelApp>()==null)new GameObject("Frost Duel").AddComponent<DuelApp>();}
        void Awake() {
            Application.targetFrameRate=60;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            var cam=new GameObject("Camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.035f,.075f,.12f);
            var b=Resources.Load<BalanceSettings>("Balance");if(b==null)b=ScriptableObject.CreateInstance<BalanceSettings>();
            session=gameObject.AddComponent<SessionCoordinator>();session.Initialize(b);
            font=Resources.Load<Font>("InterfaceFont");
            circle=new Texture2D(64,64,TextureFormat.RGBA32,false);circle.filterMode=FilterMode.Bilinear;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float a=Mathf.Clamp01(32-Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(32,32)));circle.SetPixel(x,y,new Color(1,1,1,a));}circle.Apply();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-localDemo")>=0){session.StartLocal();screen=ScreenKind.Game;}
        }
        void Layout() {
            safe=Screen.safeArea;scale=Mathf.Min(safe.width/1280f,safe.height/720f);
            origin=new Vector2(safe.x+(safe.width-1280*scale)/2,Screen.height-safe.yMax+(safe.height-720*scale)/2);
        }
        Vector2 Pointer(Vector2 p)=> (new Vector2(p.x,Screen.height-p.y)-origin)/scale;
        void Update() {
            Layout();
            if(session.Started)screen=ScreenKind.Game;
            if(session.Display!=null && session.Display.turnId!=lastTurn){lastTurn=session.Display.turnId;dragging=false;selected=ActionKind.Snowball;}
            if(!session.CanAct || confirmExit){dragging=false;return;}
            if(Input.GetKeyDown(KeyCode.Escape)){dragging=false;return;}
            if(Input.touchCount>0) {
                foreach(var t in Input.touches) {
                    Vector2 p=Pointer(t.position);
                    if(t.phase==TouchPhase.Began && finger<0 && InArena(p)){finger=t.fingerId;BeginAim(p);}
                    if(t.fingerId!=finger)continue;
                    if(t.phase==TouchPhase.Moved || t.phase==TouchPhase.Stationary)MoveAim(p);
                    if(t.phase==TouchPhase.Ended){EndAim(p);finger=-1;}
                    if(t.phase==TouchPhase.Canceled){dragging=false;finger=-1;}
                }
            } else {
                finger=-1;Vector2 p=Pointer(Input.mousePosition);
                if(Input.GetMouseButtonDown(0) && InArena(p))BeginAim(p);
                if(Input.GetMouseButton(0) && dragging)MoveAim(p);
                if(Input.GetMouseButtonUp(0) && dragging)EndAim(p);
            }
        }
        bool InArena(Vector2 p)=>p.x>40 && p.x<1240 && p.y>170 && p.y<575;
        void BeginAim(Vector2 p){dragging=true;startDrag=aimDrag=p;}
        void MoveAim(Vector2 p) {
            aimDrag=p;Vector2 delta=p-startDrag;float forward=delta.x*(session.MyPlayer==0?1:-1);
            angle=Mathf.Clamp(Mathf.Atan2(-delta.y,Mathf.Max(1,forward))*Mathf.Rad2Deg,5,85);
            power=Mathf.Clamp01(delta.magnitude/260f);
        }
        void EndAim(Vector2 p) {
            MoveAim(p);dragging=false;
            if(!InArena(p)||Vector2.Distance(startDrag,p)<25||(p.x-startDrag.x)*(session.MyPlayer==0?1:-1)<10){return;}
            session.Act(selected,angle,power);
        }
        void Styles() {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=21,alignment=TextAnchor.MiddleLeft,wordWrap=true};label.normal.textColor=ink;
            small=new GUIStyle(label){fontSize=16};title=new GUIStyle(label){fontSize=48,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=22,alignment=TextAnchor.MiddleCenter,wordWrap=true};
            button.normal.textColor=ink;button.hover.textColor=Color.white;button.active.textColor=Color.white;
            button.normal.background=Texture2D.whiteTexture;button.hover.background=Texture2D.whiteTexture;button.active.background=Texture2D.whiteTexture;
        }
        void OnGUI() {
            Layout();Styles();GUI.matrix=Matrix4x4.TRS(new Vector3(origin.x,origin.y,0),Quaternion.identity,Vector3.one*scale);
            Fill(new Rect(0,0,1280,720),new Color(.035f,.075f,.12f));
            if(screen==ScreenKind.Menu)Menu();else if(screen==ScreenKind.Host||screen==ScreenKind.Join)Lobby();else Game();
        }
        void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Dot(Vector2 p,float radius,Color c){GUI.color=c;GUI.DrawTexture(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),circle);GUI.color=Color.white;}
        void Text(Rect r,string text,GUIStyle style=null,Color? color=null) {GUI.contentColor=color??Color.white;GUI.Label(r,text,style??label);GUI.contentColor=Color.white;}
        bool Button(Rect r,string text,bool enabled=true,bool accent=false) {
            GUI.enabled=enabled;GUI.backgroundColor=accent?new Color(.08f,.42f,.52f):new Color(.12f,.23f,.31f);
            bool hit=GUI.Button(r,text,button);GUI.backgroundColor=Color.white;GUI.enabled=true;return hit;
        }
        void Line(Vector2 a,Vector2 b,float width,Color c) {
            var matrix=GUI.matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg,a);Fill(new Rect(a.x,a.y-width/2,Vector2.Distance(a,b),width),c);GUI.matrix=matrix;
        }
        void Menu() {
            Snow(0.6f,false);
            Text(new Rect(80,62,900,35),"BLUETOOTH CLASSIC  /  2 ИГРОКА  /  БЕЗ ИНТЕРНЕТА",small,muted);
            Text(new Rect(80,119,1000,80),"СНЕЖНАЯ ДУЭЛЬ",title);
            Text(new Rect(84,211,710,72),"Рассчитай бросок. Сохрани тепло.\nНе дай сопернику подготовить лёд.",label,muted);
            if(Button(new Rect(84,329,430,66),"Создать матч",true,true)){session.CreateMatch();screen=ScreenKind.Host;}
            if(Button(new Rect(84,412,430,66),"Подключиться")){session.FindMatch();screen=ScreenKind.Join;}
            if(Button(new Rect(84,495,430,60),"Выход"))Application.Quit();
#if UNITY_EDITOR || !UNITY_ANDROID
            if(Button(new Rect(84,574,430,48),"Локальная проверка • 2 игрока")){session.StartLocal();screen=ScreenKind.Game;}
#endif
            DrawFigure(new Vector2(930,495),2.2f,cyan,false);DrawFigure(new Vector2(1110,495),2.2f,orange,false);
            Fill(new Rect(720,495,480,5),new Color(.68f,.88f,.95f));
            Text(new Rect(80,665,1100,30),"Учебный прототип  •  Unity 6  •  Bluetooth между двумя Android-телефонами",small,muted);
        }
        void Lobby() {
            Text(new Rect(70,55,1150,70),screen==ScreenKind.Host?"СОЗДАНИЕ МАТЧА":"ПОДКЛЮЧЕНИЕ",title);
            Text(new Rect(72,138,1100,85),session.Notice,label,cyan);
            if(screen==ScreenKind.Host) {
                Text(new Rect(72,245,1070,150),"1. Разрешите доступ к Bluetooth и видимость телефона.\n2. На втором телефоне нажмите «Подключиться».\n3. Выберите этот телефон и подтвердите код сопряжения.\nМатч начнётся после обмена состоянием.");
                if(Button(new Rect(72,435,460,60),"Повторить ожидание / видимость",!session.transport.Working))session.Retry();
            } else {
                scroll=GUI.BeginScrollView(new Rect(72,245,1136,310),scroll,new Rect(0,0,1095,Mathf.Max(300,session.transport.Devices.Count*72)));
                for(int i=0;i<session.transport.Devices.Count;i++){var d=session.transport.Devices[i];if(Button(new Rect(0,i*72,1095,62),d.name+"   /   "+d.address,!session.transport.Working && !session.transport.Attempting)){session.Connect(d.address);}}
                GUI.EndScrollView();
                if(Button(new Rect(72,575,460,54),"Обновить поиск",!session.transport.Working && !session.transport.Attempting))session.transport.Scan();
            }
            if(Button(new Rect(960,643,248,50),"В меню"))BackToMenu();
        }
        Vector2 World(Vector2 p) {float k=1190/session.Balance.worldWidth;return new Vector2(45+p.x*k,580-p.y*k);}
        void Game() {
            var s=session.Display;if(s==null)return;var b=session.Balance;
            Fill(new Rect(0,0,1280,129),panel);
            Stats(s.players[0],new Rect(32,14,358,100),0);
            Stats(s.players[1],new Rect(890,14,358,100),1);
            string turn=s.finished?"МАТЧ ЗАВЕРШЁН":session.Paused?"МАТЧ ПРИОСТАНОВЛЕН":session.Busy?"РЕЗУЛЬТАТ ХОДА":session.CanAct?"ВАШ ХОД":"ХОД СОПЕРНИКА";
            Text(new Rect(426,18,430,35),turn,label,cyan);
            Text(new Rect(426,57,430,28),"Ход "+s.turnId+"  •  "+(session.Local?"локальная проверка":session.MyPlayer==0?"вы слева":"вы справа"),small,muted);
            if(Button(new Rect(1140,134,106,32),"Меню")){confirmExit=true;dragging=false;}
            Snow(s.wind,s.blizzard);
            float k=1190/b.worldWidth;
            for(int i=0;i<s.terrain.Length-1;i++) {
                float x=i*b.worldWidth/(s.terrain.Length-1);Vector2 at=World(new Vector2(x,s.terrain[i]));
                Fill(new Rect(at.x,at.y,1190f/(s.terrain.Length-1)+1,590-at.y),new Color(.23f,.39f,.48f));
                Fill(new Rect(at.x,at.y,1190f/(s.terrain.Length-1)+1,5),new Color(.79f,.92f,.97f));
            }
            for(int i=0;i<2;i++) {
                var p=s.players[i];Vector2 at=World(new Vector2(p.x,p.groundY));
                Fill(new Rect(at.x-b.platformHalfWidth*k,at.y,2*b.platformHalfWidth*k,12),new Color(.46f,.71f,.82f));
                float shake=p.warmth==0?Mathf.Sin(Time.unscaledTime*45)*2:0;
                DrawFigure(at+new Vector2(shake,0),k/32,(i==0?cyan:orange),p.iceReady);
                if(s.activePlayer==i&&!s.finished)Dot(at+new Vector2(0,-82),5,Color.white);
            }
            if(session.Animation!=null)DrawShot(session.Animation);
            if(dragging)DrawAim();
            Fill(new Rect(0,590,1280,130),panel);
            Text(new Rect(35,592,1200,30),dragging?"Отпустите для броска • угол "+angle.ToString("0")+"° • сила "+(power*100).ToString("0")+"% • верните палец к началу, чтобы отменить":session.Notice,small,muted);
            var me=s.players[session.MyPlayer];
            if(Button(new Rect(32,636,280,61),"Снежок"+(selected==ActionKind.Snowball?" • выбран":""),session.CanAct,selected==ActionKind.Snowball)){selected=ActionKind.Snowball;dragging=false;}
            bool iceAllowed=session.CanAct&&(me.iceReady||me.warmth>=b.iceCost);
            if(Button(new Rect(328,636,335,61),me.iceReady?(selected==ActionKind.ThrowIce?"Бросить лёд • выбран":"Бросить лёд"):"Подготовить лёд  −"+b.iceCost,iceAllowed,selected==ActionKind.ThrowIce)) {
                dragging=false;if(me.iceReady)selected=ActionKind.ThrowIce;else session.Act(ActionKind.PrepareIce);
            }
            if(Button(new Rect(679,636,280,61),"Утеплиться  +"+b.warmActionGain,session.CanAct)){dragging=false;session.Act(ActionKind.WarmUp);}
            if(Button(new Rect(975,636,273,61),"Отмена прицела",session.CanAct)){dragging=false;selected=ActionKind.Snowball;}
            if(!dragging&&!session.Busy&&!s.finished)Text(new Rect(280,139,800,30),session.CanAct?"Проведите вверх к сопернику и отпустите для броска":"Ожидайте действия второго игрока",small,muted);
            if(session.Paused)Modal("Соединение потеряно",session.Notice+"\nНа ведущем нажмите повтор, затем повторите на втором телефоне.","Повторить подключение",()=>session.Retry());
            else if(s.finished)Modal(s.winner<0?"Ничья":"Победил игрок "+(s.winner+1),s.winner==session.MyPlayer?"Вы сохранили преимущество в этой снежной дуэли.":"Матч завершён. Можно начать новую дуэль.","Вернуться в меню",BackToMenu,false);
            else if(confirmExit)Modal("Завершить матч?","Текущее состояние будет потеряно при выходе в меню.","Завершить и выйти",BackToMenu,false,true);
        }
        bool confirmExit;
        void BackToMenu(){session.ResetSession();screen=ScreenKind.Menu;confirmExit=false;dragging=false;lastTurn=-1;}
        void Stats(PlayerState p,Rect r,int i) {
            Text(new Rect(r.x,r.y,r.width,26),"ИГРОК "+(i+1)+(p.iceReady?"   • ЛЁД ГОТОВ":""),small,i==0?cyan:orange);
            Bar(new Rect(r.x,r.y+36,r.width,18),(float)p.health/session.Balance.maxHealth,i==0?cyan:orange);
            Text(new Rect(r.x,r.y+29,r.width,29),"Здоровье  "+p.health,small);
            Bar(new Rect(r.x,r.y+70,r.width,18),(float)p.warmth/session.Balance.maxWarmth,new Color(.76f,.51f,.23f));
            Text(new Rect(r.x,r.y+63,r.width,29),"Тепло  "+p.warmth+(p.warmth==0?"   /   МОРОЗ":""),small);
        }
        void Bar(Rect r,float value,Color c){Fill(r,new Color(.12f,.21f,.28f));Fill(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),c*.65f);}
        void DrawFigure(Vector2 feet,float zoom,Color color,bool ice) {
            Fill(new Rect(feet.x-14*zoom,feet.y-37*zoom,28*zoom,37*zoom),color);
            Dot(feet+new Vector2(0,-48)*zoom,11*zoom,new Color(.96f,.86f,.72f));
            Fill(new Rect(feet.x-11*zoom,feet.y-59*zoom,22*zoom,7*zoom),color);
            Fill(new Rect(feet.x-17*zoom,feet.y-8*zoom,13*zoom,8*zoom),new Color(.07f,.16f,.23f));
            Fill(new Rect(feet.x+4*zoom,feet.y-8*zoom,13*zoom,8*zoom),new Color(.07f,.16f,.23f));
            if(ice)Dot(feet+new Vector2(24,-24)*zoom,6*zoom,Color.cyan);
        }
        void Snow(float wind,bool storm) {
            int count=storm?65:32;
            for(int i=0;i<count;i++) {
                float y=145+Mathf.Repeat(i*67.13f+Time.unscaledTime*(16+i%11),420);
                float x=Mathf.Repeat(i*179.7f+Time.unscaledTime*(wind*23),1270);
                Dot(new Vector2(x,y),1.3f+i%2,new Color(.85f,.95f,1,.45f));
            }
        }
        void DrawAim() {
            var p=session.Display.players[session.MyPlayer];float jitter=p.warmth==0?Mathf.Sin(Time.unscaledTime*30)*session.Balance.frozenAimDegrees:0;
            float a=(angle+jitter)*Mathf.Deg2Rad;
            Vector2 at=World(new Vector2(p.x+(session.MyPlayer==0?.65f:-.65f),p.groundY+1.2f));
            Vector2 tip=at+new Vector2(Mathf.Cos(a)*(session.MyPlayer==0?1:-1),-Mathf.Sin(a))*(50+power*130);
            Line(at,tip,3,selected==ActionKind.ThrowIce?Color.cyan:Color.white);Dot(tip,6,Color.white);
            Dot(startDrag,9,new Color(1,1,1,.35f));Line(startDrag,aimDrag,2,new Color(1,1,1,.25f));
        }
        void DrawShot(ShotResult shot) {
            if(shot.path==null||shot.path.Length<2)return;
            float index=session.AnimationTime/shot.step;int n=Mathf.Min((int)index,shot.path.Length-1);
            for(int i=Mathf.Max(1,n-18);i<=n;i++){Vector2 at=World(shot.path[i]);if(at.y>132)Dot(at,2,new Color(.8f,.94f,1,.5f));}
            Vector2 pos=World(Vector2.Lerp(shot.path[n],shot.path[Mathf.Min(n+1,shot.path.Length-1)],index-n));
            if(n<shot.path.Length-1) {
                if(pos.y<145){pos.y=147;Text(new Rect(pos.x-60,148,180,24),"Снаряд выше",small);}
                Dot(pos,shot.kind==ActionKind.ThrowIce?7:6,shot.kind==ActionKind.ThrowIce?cyan:Color.white);
            } else if(shot.impact) {
                float t=session.AnimationTime-(shot.path.Length-1)*shot.step;
                for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Dot(pos+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*t*70,3,new Color(1,1,1,1-t));}
            }
        }
        void Modal(string heading,string body,string action,Action callback,bool menu=true,bool cancel=false) {
            Fill(new Rect(0,0,1280,720),new Color(.01f,.025f,.045f,.87f));Fill(new Rect(270,190,740,350),panel);
            Text(new Rect(305,213,670,60),heading,new GUIStyle(title){fontSize=32});Text(new Rect(305,287,670,108),body);
            if(Button(new Rect(305,422,670,56),action,true,true))callback();
            if(menu&&Button(new Rect(305,485,670,42),"Выйти в меню"))BackToMenu();
            if(cancel&&Button(new Rect(305,485,670,42),"Продолжить матч"))confirmExit=false;
        }
    }
}
