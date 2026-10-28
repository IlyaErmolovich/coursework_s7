using System;
using UnityEngine;
namespace FrostDuel {
    // All positions below are in one logical canvas; world projection never uses GUI rotation helpers.
    public sealed class DuelApp : MonoBehaviour {
        enum ScreenKind { Menu, Host, Join, Game }
        ScreenKind screen;
        SessionCoordinator session;
        PrimitiveArt art;
        GUIStyle label,button,title,small,center,letter;
        Font font;
        float scale,canvasWidth=1280,worldScale=40;
        Vector2 origin,scroll;
        readonly AimGesture aim=new AimGesture();
        int finger=-1,lastTurn=-1;
        ActionKind selected=ActionKind.Snowball;
        PlayerAction previewAction;
        ShotResult preview;
        bool confirmExit;
        float noticeUntil;
        string lastNotice="";
        readonly Color ink=new Color(.93f,.98f,1),muted=new Color(.68f,.83f,.88f),panel=new Color(.055f,.16f,.23f,.93f);
        readonly Color cyan=new Color(.22f,.83f,.83f),orange=new Color(1,.59f,.31f),outline=new Color(.07f,.20f,.28f);
        public SessionCoordinator Session=>session;
        public bool IsAiming=>aim.Active;
        public ShotResult AimPreview=>preview;
        public float CanvasWidth=>canvasWidth;
        public Rect SnowButton=>new Rect(canvasWidth-278,630,74,74);
        public Rect IceButton=>new Rect(canvasWidth-190,630,74,74);
        public Rect WarmButton=>new Rect(canvasWidth-102,630,74,74);
        public Rect MenuButton=>new Rect(canvasWidth-76,128,52,52);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(FindFirstObjectByType<DuelApp>()==null)new GameObject("Frost Duel").AddComponent<DuelApp>();}
        void Awake() {
            Application.targetFrameRate=60;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            var cam=new GameObject("Camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.07f,.19f,.32f);
            var b=Resources.Load<BalanceSettings>("Balance");if(b==null)b=ScriptableObject.CreateInstance<BalanceSettings>();
            session=gameObject.AddComponent<SessionCoordinator>();session.Initialize(b);
            font=Resources.Load<Font>("InterfaceFont");art=new PrimitiveArt();Layout();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-localDemo")>=0)session.StartLocal();
        }
        void OnDestroy(){art?.Dispose();}
        void Layout(){var v=new DuelViewport(Screen.safeArea,Screen.height);scale=v.Scale;origin=v.Origin;canvasWidth=v.Width;}
        Vector2 Pointer(Vector2 p)=>new DuelViewport(Screen.safeArea,Screen.height).FromScreen(p,Screen.height);
        public Vector2 World(Vector2 p)=>new Vector2((canvasWidth-session.Balance.worldWidth*worldScale)/2+p.x*worldScale,720-p.y*worldScale);
        void Update() {
            Layout();
            if(session.Started)screen=ScreenKind.Game;
            if(session.Display!=null&&session.Display.turnId!=lastTurn){lastTurn=session.Display.turnId;CancelAim();selected=ActionKind.Snowball;}
            if(lastNotice!=session.Notice){lastNotice=session.Notice;noticeUntil=Time.unscaledTime+4;}
            UpdateFraming();
            if(!session.CanAct||confirmExit){CancelAim();return;}
            if(Input.GetKeyDown(KeyCode.Escape)||Input.GetMouseButtonDown(1)){CancelAim();return;}
            if(Input.touchCount>0) {
                foreach(var t in Input.touches) {
                    Vector2 p=Pointer(t.position);
                    if(t.phase==TouchPhase.Began) {
                        if(finger<0&&CanStartAim(p)){finger=t.fingerId;BeginAim(p);}
                        else if(finger!=t.fingerId&&aim.Active){CancelAim();return;}
                    }
                    if(t.fingerId!=finger)continue;
                    if(t.phase==TouchPhase.Moved||t.phase==TouchPhase.Stationary)MoveAim(p);
                    if(t.phase==TouchPhase.Ended){EndAim(p);finger=-1;}
                    if(t.phase==TouchPhase.Canceled)CancelAim();
                }
            } else {
                finger=-1;Vector2 p=Pointer(Input.mousePosition);
                if(Input.GetMouseButtonDown(0))BeginAim(p);
                if(Input.GetMouseButton(0)&&aim.Active)MoveAim(p);
                if(Input.GetMouseButtonUp(0)&&aim.Active)EndAim(p);
            }
        }
        void UpdateFraming() {
            if(!session.Started)return;
            float top=12.8f;var path=aim.Active?preview?.path:session.Animation?.path;
            if(path!=null)foreach(var p in path)top=Mathf.Max(top,p.y+1.2f);
            float target=Mathf.Min((canvasWidth-48)/session.Balance.worldWidth,570/top);
            worldScale=Mathf.Lerp(worldScale,target,1-Mathf.Exp(-10*Time.unscaledDeltaTime));
        }
        bool InField(Vector2 p)=>p.x>12&&p.x<canvasWidth-12&&p.y>120&&p.y<710;
        bool OverControls(Vector2 p)=>SnowButton.Contains(p)||IceButton.Contains(p)||WarmButton.Contains(p)||MenuButton.Contains(p);
        public bool CanStartAim(Vector2 p) {
            if(!session.CanAct||confirmExit||!InField(p)||OverControls(p))return false;
            foreach(var player in session.Display.players) {
                Vector2 feet=World(new Vector2(player.x,player.groundY));
                if(new Rect(feet.x-worldScale*.7f,feet.y-worldScale*2,worldScale*1.4f,worldScale*2.1f).Contains(p))return false;
            }
            return true;
        }
        // Used by real touch/mouse input and the runtime verification harness.
        public void BeginAim(Vector2 p){if(!CanStartAim(p))return;aim.Begin(p,session.MyPlayer);RefreshPreview();}
        public void MoveAim(Vector2 p){if(!aim.Active)return;aim.Move(p,Time.unscaledDeltaTime);RefreshPreview();}
        void RefreshPreview() {
            if(!aim.Active||!session.CanAct)return;
            previewAction=new PlayerAction{sessionId=session.Display.sessionId,turnId=session.Display.turnId,player=session.MyPlayer,
                kind=selected,angle=aim.Angle,power=aim.Power,aimPhase=Mathf.Repeat(Time.unscaledTime*2.2f,Mathf.PI*2)};
            preview=Projectile.For(selected).Simulate(session.Display,previewAction,session.Balance);
        }
        public void EndAim(Vector2 p) {
            bool release=aim.ReleaseAllowed(p)&&InField(p)&&!OverControls(p)&&session.CanAct&&!confirmExit;
            aim.Cancel();finger=-1;
            if(release&&previewAction!=null)session.Act(selected,previewAction.angle,previewAction.power,previewAction.aimPhase);
        }
        public void CancelAim(){aim.Cancel();finger=-1;}
        public void ChooseSnow(){if(!session.CanAct||confirmExit)return;CancelAim();selected=ActionKind.Snowball;}
        public void ChooseIce() {
            if(!session.CanAct||confirmExit)return;
            var p=session.Display.players[session.MyPlayer];if(!p.iceReady&&p.warmth<session.Balance.iceCost)return;
            CancelAim();if(p.iceReady)selected=ActionKind.ThrowIce;else session.Act(ActionKind.PrepareIce);
        }
        void Styles() {
            if(label!=null)return;
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=21,alignment=TextAnchor.MiddleLeft,wordWrap=true};label.normal.textColor=ink;
            small=new GUIStyle(label){fontSize=16};center=new GUIStyle(label){alignment=TextAnchor.MiddleCenter};
            title=new GUIStyle(label){fontSize=46,fontStyle=FontStyle.Bold};letter=new GUIStyle(center){fontSize=34,fontStyle=FontStyle.Bold};
            button=new GUIStyle(center){fontSize=22};
        }
        void OnGUI() {
            Layout();Styles();GUI.matrix=Matrix4x4.TRS(origin,Quaternion.identity,Vector3.one*scale);
            Background();
            if(screen==ScreenKind.Menu)Menu();else if(screen==ScreenKind.Host||screen==ScreenKind.Join)Lobby();else Game();
        }
        void Fill(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=Color.white;}
        void Image(Rect r,Texture2D t,Color c){GUI.color=c;GUI.DrawTexture(r,t);GUI.color=Color.white;}
        void Ellipse(Rect r,Color c)=>Image(r,art.Circle,c);
        void Dot(Vector2 p,float radius,Color c)=>Ellipse(new Rect(p.x-radius,p.y-radius,radius*2,radius*2),c);
        void Round(Rect r,float radius,Color c) {
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)/2);
            Fill(new Rect(r.x+radius,r.y,r.width-2*radius,r.height),c);Fill(new Rect(r.x,r.y+radius,r.width,r.height-2*radius),c);
            foreach(float x in new[]{r.x+radius,r.xMax-radius})foreach(float y in new[]{r.y+radius,r.yMax-radius})Dot(new Vector2(x,y),radius,c);
        }
        void Text(Rect r,string text,GUIStyle style=null,Color? color=null){GUI.contentColor=color??Color.white;GUI.Label(r,text,style??label);GUI.contentColor=Color.white;}
        void Line(Vector2 a,Vector2 b,float width,Color c) {
            var matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            Fill(new Rect(0,-width/2,Vector2.Distance(a,b),width),c);GUI.matrix=matrix;
            Dot(a,width/2,c);Dot(b,width/2,c);
        }
        bool Button(Rect r,string text,bool enabled=true,bool accent=false) {
            Round(new Rect(r.x,r.y+4,r.width,r.height),14,new Color(.04f,.12f,.18f,.75f));
            Round(r,14,enabled?(accent?new Color(.12f,.50f,.55f):new Color(.12f,.29f,.38f)):new Color(.19f,.30f,.35f,.75f));
            GUI.enabled=enabled;bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=true;
            Text(r,text,button,enabled?Color.white:new Color(.6f,.7f,.73f));return hit;
        }
        bool LetterButton(Rect r,string text,bool enabled,bool active=false,bool ready=false) {
            Dot(r.center+new Vector2(0,4),r.width/2,new Color(.04f,.12f,.18f,.65f));
            Dot(r.center,r.width/2,active?new Color(.91f,.98f,1):ready?cyan:new Color(.33f,.54f,.61f));
            Dot(r.center,r.width/2-3,enabled?(active?new Color(.10f,.52f,.57f):panel):new Color(.20f,.33f,.39f));
            GUI.enabled=enabled;bool hit=GUI.Button(r,GUIContent.none,GUIStyle.none);GUI.enabled=true;
            Text(r,text,letter,enabled?ink:new Color(.47f,.61f,.66f));return hit;
        }
        void Background() {
            Image(new Rect(0,0,canvasWidth,720),art.Sky,Color.white);
            Dot(new Vector2(canvasWidth*.76f,181),44,new Color(.67f,.88f,.90f,.12f));
            Dot(new Vector2(canvasWidth*.76f,181),30,new Color(.86f,.95f,.92f,.8f));
            Image(new Rect(0,240,canvasWidth,400),art.FarRidge,Color.white);
            Image(new Rect(0,385,canvasWidth,270),art.NearRidge,Color.white);
            for(int i=0;i<14;i++)Pine(new Vector2(i*canvasWidth/13,623+(i%3)*8),35+i%4*15,new Color(.15f,.35f,.41f,.7f));
        }
        void Pine(Vector2 foot,float h,Color color) {
            Line(foot,foot-new Vector2(0,h),3,color);
            for(int i=1;i<5;i++){float y=foot.y-h+i*h/6;float span=h*.12f+i*h*.045f;Line(new Vector2(foot.x,y),new Vector2(foot.x-span,y+span*.7f),5,color);Line(new Vector2(foot.x,y),new Vector2(foot.x+span,y+span*.7f),5,color);}
        }
        void Menu() {
            Snow(.6f,false);
            Round(new Rect(52,68,610,580),28,new Color(.04f,.14f,.22f,.87f));
            Text(new Rect(85,92,540,28),"ЗИМНИЙ ПОЕДИНОК • ДВА ИГРОКА",small,cyan);
            Text(new Rect(82,140,570,75),"СНЕЖНАЯ ДУЭЛЬ",title);
            Text(new Rect(85,222,525,68),"Рассчитай бросок. Сохрани тепло.\nBluetooth — без интернета.",label,muted);
            if(Button(new Rect(85,321,520,65),"Создать матч",true,true)){session.CreateMatch();screen=ScreenKind.Host;}
            if(Button(new Rect(85,404,520,65),"Подключиться")){session.FindMatch();screen=ScreenKind.Join;}
            if(Button(new Rect(85,487,520,59),"Выход"))Application.Quit();
#if UNITY_EDITOR || !UNITY_ANDROID
            if(Button(new Rect(85,567,520,48),"Локальная проверка • 2 игрока"))session.StartLocal();
#endif
            float x=canvasWidth-335;
            Ellipse(new Rect(x-155,550,430,55),new Color(.70f,.88f,.91f));
            DrawFigure(new Vector2(x-65,562),80,0,false,false,25);
            DrawFigure(new Vector2(x+140,562),80,1,true,false,25);
            Text(new Rect(40,670,900,30),"Оттяни назад. Отпусти. Попади.",small,muted);
        }
        void Lobby() {
            Round(new Rect(48,40,canvasWidth-96,630),24,panel);
            Text(new Rect(78,63,canvasWidth-160,70),screen==ScreenKind.Host?"СОЗДАНИЕ МАТЧА":"ПОДКЛЮЧЕНИЕ",title);
            Text(new Rect(80,150,canvasWidth-160,85),session.Notice,label,cyan);
            if(screen==ScreenKind.Host) {
                Text(new Rect(80,256,canvasWidth-160,150),"1. Разрешите Bluetooth и видимость телефона.\n2. На втором телефоне нажмите «Подключиться».\n3. Выберите этот телефон и подтвердите сопряжение.\nМатч начнётся после обмена состоянием.");
                if(Button(new Rect(80,442,480,60),"Повторить ожидание / видимость",!session.transport.Working))session.Retry();
            } else {
                scroll=GUI.BeginScrollView(new Rect(80,250,canvasWidth-160,285),scroll,new Rect(0,0,canvasWidth-200,Mathf.Max(280,session.transport.Devices.Count*72)));
                for(int i=0;i<session.transport.Devices.Count;i++){var d=session.transport.Devices[i];if(Button(new Rect(0,i*72,canvasWidth-200,62),d.name+"   /   "+d.address,!session.transport.Working&&!session.transport.Attempting))session.Connect(d.address);}
                GUI.EndScrollView();
                if(Button(new Rect(80,560,460,54),"Обновить поиск",!session.transport.Working&&!session.transport.Attempting))session.transport.Scan();
            }
            if(Button(new Rect(canvasWidth-325,596,245,50),"В меню"))BackToMenu();
        }
        void Game() {
            var s=session.Display;if(s==null)return;var b=session.Balance;
            art.UpdateGround(s,b);
            Vector2 left=World(new Vector2(0,PrimitiveArt.GroundHeight));
            Image(new Rect(left.x,left.y,b.worldWidth*worldScale,PrimitiveArt.GroundHeight*worldScale),art.Ground,Color.white);
            // Extend edge texels outside the playable world; no vertical cut-off when framing high arcs.
            if(left.x>0) {
                GUI.DrawTextureWithTexCoords(new Rect(0,left.y,left.x,PrimitiveArt.GroundHeight*worldScale),art.Ground,new Rect(0,0,1f/art.Ground.width,1));
                GUI.DrawTextureWithTexCoords(new Rect(canvasWidth-left.x,left.y,left.x,PrimitiveArt.GroundHeight*worldScale),art.Ground,new Rect(1-1f/art.Ground.width,0,1f/art.Ground.width,1));
            }
            Snow(s.wind,s.blizzard);
            for(int i=0;i<2;i++) {
                var p=s.players[i];Vector2 feet=World(new Vector2(p.x,p.groundY));
                float shake=p.warmth==0?Mathf.Sin(Time.unscaledTime*7)*.7f:0;
                float armAngle=aim.Active&&i==session.MyPlayer&&previewAction!=null?Projectile.LaunchAngle(s,previewAction,b):25;
                DrawFigure(feet+new Vector2(shake,0),worldScale,i,p.iceReady,p.warmth==0,armAngle);
                if(s.activePlayer==i&&!s.finished){Ellipse(new Rect(feet.x-28,feet.y+4,56,8),new Color(1,1,1,.35f));}
            }
            if(aim.Active)DrawAim();
            if(session.Animation!=null)DrawShot(session.Animation);
            Stats(s.players[0],new Rect(24,18,332,92),0);
            Stats(s.players[1],new Rect(canvasWidth-356,18,332,92),1);
            Round(new Rect(canvasWidth/2-190,18,380,78),20,panel);
            string turn=s.finished?"МАТЧ ЗАВЕРШЁН":session.Paused?"ПАУЗА":session.Busy?"БРОСОК":session.CanAct?"ВАШ ХОД":"ХОД СОПЕРНИКА";
            Text(new Rect(canvasWidth/2-180,22,360,32),turn,center);
            Text(new Rect(canvasWidth/2-175,57,350,25),"Ход "+s.turnId+"  •  "+(session.Local?"игрок "+(s.activePlayer+1):session.MyPlayer==0?"вы слева":"вы справа"),new GUIStyle(small){alignment=TextAnchor.MiddleCenter},muted);
            bool modal=confirmExit||session.Paused||s.finished;
            if(LetterButton(MenuButton,"М",!modal)){CancelAim();confirmExit=true;}
            bool can=session.CanAct&&!modal;
            var me=s.players[session.MyPlayer];
            if(LetterButton(SnowButton,"С",can,selected==ActionKind.Snowball))ChooseSnow();
            if(LetterButton(IceButton,"Л",can&&(me.iceReady||me.warmth>=b.iceCost),selected==ActionKind.ThrowIce,me.iceReady))ChooseIce();
            if(LetterButton(WarmButton,"У",can)){CancelAim();session.Act(ActionKind.WarmUp);}
            string ice=me.iceReady?"Лёд готов • Л: выбрать бросок":me.warmth<b.iceCost?"Лёд: нужно "+b.iceCost+" тепла":"Л: подготовить лёд • −"+b.iceCost+" тепла";
            Round(new Rect(canvasWidth-350,590,326,30),12,panel);Text(new Rect(canvasWidth-342,590,310,30),ice,small,me.iceReady?cyan:muted);
            string hint=aim.Active?(aim.CanRelease?"Отпустите для броска • к началу жеста — отмена":"Тяните назад и вниз • отпустить сейчас — отмена"):
                session.CanAct?"С — снежок   Л — лёд   У — утеплиться. Тяните назад.":"Ожидайте завершения хода";
            Round(new Rect(24,658,Mathf.Min(canvasWidth-390,720),38),14,panel);Text(new Rect(38,658,Mathf.Min(canvasWidth-416,696),38),hint,small,muted);
            if(Time.unscaledTime<noticeUntil&&!aim.Active&&!string.IsNullOrEmpty(session.Notice)) {
                Round(new Rect(canvasWidth/2-325,118,650,40),14,panel);Text(new Rect(canvasWidth/2-311,118,622,40),session.Notice,new GUIStyle(small){alignment=TextAnchor.MiddleCenter});
            }
            if(session.Paused)Modal("Соединение потеряно",session.Notice+"\nПовторите подключение сначала на ведущем, затем на втором телефоне.","Повторить подключение",session.Retry);
            else if(s.finished)Modal(s.winner<0?"Ничья":"Победил игрок "+(s.winner+1),"Снежная дуэль завершена.","Вернуться в меню",BackToMenu,false);
            else if(confirmExit)Modal("Завершить матч?","При выходе в меню текущее состояние будет потеряно.","Завершить и выйти",BackToMenu,false,true);
        }
        void Stats(PlayerState p,Rect r,int player) {
            Round(r,19,panel);Color c=player==0?cyan:orange;
            Text(new Rect(r.x+16,r.y+6,r.width-32,24),"ИГРОК "+(player+1)+(p.iceReady?"    ЛЁД ГОТОВ":""),small,c);
            Bar(new Rect(r.x+16,r.y+36,180,14),(float)p.health/session.Balance.maxHealth,c);
            Text(new Rect(r.x+210,r.y+27,112,29),"Жизнь "+p.health,small);
            Bar(new Rect(r.x+16,r.y+64,180,12),(float)p.warmth/session.Balance.maxWarmth,new Color(1,.74f,.36f));
            Text(new Rect(r.x+210,r.y+54,112,29),p.warmth==0?"Мороз • 0":"Тепло "+p.warmth,small,p.warmth==0?cyan:ink);
        }
        void Bar(Rect r,float value,Color c){Round(r,r.height/2,new Color(.18f,.31f,.38f));if(value>0)Round(new Rect(r.x,r.y,r.width*Mathf.Clamp01(value),r.height),r.height/2,c);}
        void DrawFigure(Vector2 feet,float k,int side,bool ice,bool frozen,float angle) {
            Color coat=side==0?cyan:orange;int d=side==0?1:-1;
            Func<float,float,Vector2> P=(x,y)=>feet+new Vector2(x*d*k,-y*k);
            Color dark=outline,fur=new Color(.88f,.94f,.90f),skin=new Color(1,.80f,.64f);
            // Legs and boots support the exact fixed feet used by the collision model.
            Line(P(-.22f,.48f),P(-.27f,.10f),.23f*k,dark);Line(P(.22f,.48f),P(.27f,.10f),.23f*k,dark);
            Line(P(-.31f,.08f),P(-.08f,.08f),.19f*k,dark);Line(P(.16f,.08f),P(.39f,.08f),.19f*k,dark);
            Ellipse(new Rect(feet.x-.49f*k,feet.y-1.16f*k,.98f*k,.94f*k),dark);
            Ellipse(new Rect(feet.x-.42f*k,feet.y-1.12f*k,.84f*k,.82f*k),coat);
            Line(P(-.28f,.39f),P(.28f,.39f),.12f*k,fur);
            Line(P(.03f,.51f),P(.03f,1.06f),.035f*k,new Color(.95f,.96f,.91f));
            Dot(P(.16f,.80f),.035f*k,dark);Dot(P(.16f,.64f),.035f*k,dark);
            // Head/hood follows the core head center and radius, distinct from the coat.
            float headY=session.Balance.bodyHeight+session.Balance.headRadius;
            Dot(P(0,headY),session.Balance.headRadius*k+.12f*k,dark);
            Dot(P(0,headY),session.Balance.headRadius*k+.08f*k,fur);
            Dot(P(.025f,headY),session.Balance.headRadius*k,skin);
            Line(P(-.22f,headY+.22f),P(.21f,headY+.22f),.16f*k,coat);
            Dot(P(-.08f,headY+.43f),.12f*k,fur);
            Dot(P(.14f,headY+.015f),.035f*k,dark);Dot(P(.31f,headY-.06f),.075f*k,skin);
            Line(P(.14f,headY-.16f),P(.23f,headY-.16f),.025f*k,dark);
            Line(P(-.25f,1.12f),P(.25f,1.12f),.14f*k,side==0?orange:new Color(.18f,.43f,.47f));
            Line(P(-.21f,1.07f),P(-.43f,.84f),.14f*k,side==0?orange:new Color(.18f,.43f,.47f));
            Line(P(-.34f,.98f),P(-.54f,.61f),.19f*k,dark);Line(P(-.34f,.98f),P(-.54f,.64f),.13f*k,coat);Dot(P(-.54f,.61f),.12f*k,fur);
            Vector2 shoulder=P(.30f,.98f),release=P(.65f,1.2f);
            Vector2 elbow=(shoulder+release)/2+new Vector2(-d*.03f*k,Mathf.Lerp(.16f,.02f,angle/85)*k);
            Line(shoulder,elbow,.20f*k,dark);Line(elbow,release,.18f*k,dark);
            Line(shoulder,elbow,.14f*k,coat);Line(elbow,release,.12f*k,coat);Dot(release,.12f*k,fur);
            if(ice) {Dot(P(-.62f,.65f),.16f*k,new Color(.65f,.98f,1));Line(P(-.68f,.69f),P(-.59f,.61f),.025f*k,Color.white);}
            if(frozen){Dot(P(-.44f,1.65f),.035f*k,Color.white);Dot(P(.46f,1.83f),.045f*k,cyan);}
        }
        void Snow(float wind,bool storm) {
            for(int i=0;i<(storm?48:24);i++) {
                float y=150+Mathf.Repeat(i*67.13f+Time.unscaledTime*(16+i%11),485);
                float x=Mathf.Repeat(i*179.7f+Time.unscaledTime*(wind*23),canvasWidth);
                Dot(new Vector2(x,y),.9f+i%2,new Color(.9f,.98f,1,.36f));
            }
        }
        void DrawAim() {
            if(preview?.path==null)return;
            float distance=0;Vector2 previous=preview.path[0];int count=0;
            Color color=selected==ActionKind.ThrowIce?new Color(.61f,.98f,1):Color.white;
            Dot(World(previous),7,outline);Dot(World(previous),4.5f,color);
            foreach(var p in preview.path) {
                distance+=Vector2.Distance(previous,p);previous=p;if(distance<.42f)continue;distance=0;
                Vector2 at=World(p);if(at.y<123||at.y>715)continue;
                float radius=Mathf.Lerp(4.5f,2.7f,Mathf.Clamp01(count/70f));count++;
                Dot(at,radius+1.4f,new Color(.05f,.20f,.28f,.45f));Dot(at,radius,color);
            }
            // Anchor is deliberately subdued: it is an input cue, not another projectile.
            Dot(aim.Anchor,19,new Color(.08f,.21f,.29f,.4f));Dot(aim.Anchor,15,new Color(.70f,.93f,.96f,.35f));
            Dot(aim.Anchor,11,new Color(.12f,.28f,.36f,.5f));Line(aim.Anchor,aim.Pointer,2,new Color(.8f,.95f,1,.35f));Dot(aim.Pointer,6,new Color(.90f,.98f,1,.65f));
            var me=session.Display.players[session.MyPlayer];Vector2 release=World(Projectile.ReleasePoint(me,session.MyPlayer));
            float x=Mathf.Clamp(release.x+(session.MyPlayer==0?-230:18),20,canvasWidth-235);
            var box=new Rect(x,Mathf.Clamp(release.y-180,180,520),212,78);
            Round(box,18,panel);
            Text(new Rect(box.x+12,box.y+5,188,36),Mathf.RoundToInt(aim.Power*100)+"%   /   "+Projectile.LaunchAngle(session.Display,previewAction,session.Balance).ToString("0")+"°",new GUIStyle(center){fontSize=27,fontStyle=FontStyle.Bold});
            Text(new Rect(box.x+12,box.y+43,188,25),(selected==ActionKind.ThrowIce?"ЛЁД":"СНЕЖОК")+(me.warmth==0?" • МОРОЗ":""),new GUIStyle(small){alignment=TextAnchor.MiddleCenter},color);
        }
        void DrawShot(ShotResult shot) {
            if(shot.path==null||shot.path.Length<2)return;
            float index=session.AnimationTime/shot.step;int n=Mathf.Min((int)index,shot.path.Length-1);
            for(int i=Mathf.Max(1,n-20);i<=n;i+=2){Vector2 at=World(shot.path[i]);if(at.y>120)Dot(at,2.4f,new Color(.90f,.98f,1,.45f));}
            Vector2 pos=World(Vector2.Lerp(shot.path[n],shot.path[Mathf.Min(n+1,shot.path.Length-1)],index-n));
            if(n<shot.path.Length-1){Dot(pos,8,outline);Dot(pos,6,shot.kind==ActionKind.ThrowIce?new Color(.55f,.96f,1):Color.white);}
            else if(shot.impact){float t=session.AnimationTime-(shot.path.Length-1)*shot.step;for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Dot(pos+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*t*70,3,new Color(1,1,1,1-t));}}
        }
        void BackToMenu(){session.ResetSession();screen=ScreenKind.Menu;confirmExit=false;CancelAim();lastTurn=-1;}
        void Modal(string heading,string body,string action,Action callback,bool menu=true,bool cancel=false) {
            Fill(new Rect(0,0,canvasWidth,720),new Color(.02f,.09f,.15f,.8f));float x=canvasWidth/2-365;
            Round(new Rect(x,185,730,365),28,panel);
            Text(new Rect(x+35,205,660,60),heading,new GUIStyle(title){fontSize=32});Text(new Rect(x+35,280,660,110),body);
            if(Button(new Rect(x+35,420,660,56),action,true,true))callback();
            if(menu&&Button(new Rect(x+35,487,660,42),"Выйти в меню"))BackToMenu();
            if(cancel&&Button(new Rect(x+35,487,660,42),"Продолжить матч"))confirmExit=false;
        }
    }
}
