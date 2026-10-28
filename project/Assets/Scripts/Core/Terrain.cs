using System;
using UnityEngine;
namespace FrostDuel {
    // Height field is transmitted in full; client never reconstructs crater history.
    public static class Terrain {
        public static float[] Generate(int seed, BalanceSettings b) {
            var r = new System.Random(seed); float phase = (float)r.NextDouble() * 6;
            var h = new float[b.terrainSamples];
            float left = 3.2f + (float)r.NextDouble() * .25f, right = 3.2f + (float)r.NextDouble() * .25f;
            for (int i = 0; i < h.Length; i++) {
                float x = i * b.worldWidth / (h.Length - 1), mirror = Mathf.Min(x, b.worldWidth - x);
                h[i] = 2.7f + .4f * Mathf.Sin(mirror * .7f + phase) + .2f * Mathf.Cos(mirror * 1.5f + phase);
                if (Mathf.Abs(x-b.characterInset) < 1.5f) h[i] = left;
                if (Mathf.Abs(x-(b.worldWidth-b.characterInset)) < 1.5f) h[i] = right;
            }
            return h;
        }
        public static float Height(float[] h, float x, BalanceSettings b) {
            float f = Mathf.Clamp(x / b.worldWidth * (h.Length-1), 0, h.Length-1);
            int i = Mathf.Min((int)f, h.Length-2); return Mathf.Lerp(h[i], h[i+1], f-i);
        }
        public static void Crater(GameSession s, Vector2 at, float radius, BalanceSettings b) {
            for (int i=0; i<s.terrain.Length; i++) {
                float x=i*b.worldWidth/(s.terrain.Length-1), dx=x-at.x;
                if (Mathf.Abs(dx)>radius) continue;
                // Fixed characters stand on small unbreakable ice pedestals.
                if (Mathf.Abs(x-s.players[0].x)<b.platformHalfWidth || Mathf.Abs(x-s.players[1].x)<b.platformHalfWidth) continue;
                float bottom=at.y-Mathf.Sqrt(radius*radius-dx*dx);
                s.terrain[i]=Mathf.Max(.35f,Mathf.Min(s.terrain[i],bottom));
            }
        }
    }
}
