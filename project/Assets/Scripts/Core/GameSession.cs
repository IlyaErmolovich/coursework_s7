using System;
using UnityEngine;
namespace FrostDuel {
    public enum ActionKind { Snowball, PrepareIce, ThrowIce, WarmUp }
    [Serializable] public sealed class PlayerState {
        public int health, warmth, quietTurns;
        public bool iceReady;
        public float x, groundY;
    }
    [Serializable] public sealed class GameSession {
        public string sessionId;
        public int seed, turnId = 1, activePlayer, winner = -1;
        public bool finished;
        public PlayerState[] players;
        public float[] terrain;
        public float wind;
        public bool blizzard;
    }
    [Serializable] public sealed class PlayerAction {
        public string sessionId;
        public int turnId, player;
        public ActionKind kind;
        public float angle, power;
    }
    [Serializable] public sealed class ShotResult {
        public Vector2[] path = Array.Empty<Vector2>();
        public int target = -1;
        public bool headshot, impact;
        public Vector2 end;
        public ActionKind kind;
        public string message;
        public float step;
    }
}
