using UnityEngine;
namespace FrostDuel {
    /// <summary>Pointer positions are in the same top-left logical canvas as the HUD.</summary>
    public sealed class AimGesture {
        public const float DeadZone = 22f;
        public const float FullPull = 240f;
        public bool Active { get; private set; }
        public bool CanRelease { get; private set; }
        public Vector2 Anchor { get; private set; }
        public Vector2 Pointer { get; private set; }
        public float Angle { get; private set; } = 45;
        public float Power { get; private set; } = .45f;
        Vector2 smoothPull;
        int side;
        public void Begin(Vector2 pointer, int player) {
            Active = true; CanRelease = false; Anchor = Pointer = pointer;
            side = player == 0 ? 1 : -1;
            smoothPull = new Vector2(76, 76); Angle = 45; Power = .45f;
        }
        public void Move(Vector2 pointer, float dt) {
            if (!Active) return;
            Pointer = pointer;
            // Pull backwards/down in screen space -> throw forwards/up in world space.
            var pull = new Vector2((Anchor.x-pointer.x)*side, pointer.y-Anchor.y);
            CanRelease = pull.x > 10 && pull.magnitude >= DeadZone;
            if (!CanRelease) return;
            float blend = 1-Mathf.Exp(-24*Mathf.Max(0,dt));
            smoothPull = Vector2.Lerp(smoothPull,pull,blend);
            Angle = Mathf.Clamp(Mathf.Atan2(smoothPull.y,Mathf.Max(1,smoothPull.x))*Mathf.Rad2Deg,5,85);
            Power = Mathf.Clamp01(smoothPull.magnitude/FullPull);
        }
        public bool ReleaseAllowed(Vector2 pointer) {
            var pull = new Vector2((Anchor.x-pointer.x)*side,pointer.y-Anchor.y);
            return Active && CanRelease && pull.x>10 && pull.magnitude>=DeadZone;
        }
        public void Cancel() { Active=false; CanRelease=false; }
    }
    public readonly struct DuelViewport {
        public readonly float Scale;
        public readonly Vector2 Origin;
        public DuelViewport(Rect safeArea, float screenHeight) {
            Scale=Mathf.Min(safeArea.width/1280f,safeArea.height/720f);
            Origin=new Vector2(safeArea.x+(safeArea.width-1280*Scale)/2,
                screenHeight-safeArea.yMax+(safeArea.height-720*Scale)/2);
        }
        public Vector2 FromScreen(Vector2 p,float screenHeight) => (new Vector2(p.x,screenHeight-p.y)-Origin)/Scale;
        public Vector2 ToScreen(Vector2 p,float screenHeight) {
            Vector2 q=Origin+p*Scale;return new Vector2(q.x,screenHeight-q.y);
        }
    }
}
