using UnityEngine;
namespace FrostDuel {
    [CreateAssetMenu(menuName = "Frost Duel/Balance")]
    public sealed class BalanceSettings : ScriptableObject {
        public int maxHealth = 100, initialWarmth = 50, maxWarmth = 100;
        public int iceCost = 10, snowDamage = 8, snowWarmthDamage = 22;
        public int iceBodyDamage = 35, iceHeadDamage = 60, iceWarmthDamage = 8;
        public int warmActionGain = 35, passiveWarmthGain = 6, quietTurnsForRecovery = 2, freezeDamage = 7;
        public float gravity = 9.8f, minSpeed = 5, maxSpeed = 19, maxWind = 3.2f;
        public float snowWindFactor = 1, iceWindFactor = .16f, blizzardMultiplier = 1.5f;
        public float frozenAimDegrees = 5, snowCraterRadius = .85f, iceCraterRadius = 1.35f;
        public float worldWidth = 32, worldHeight = 18, characterInset = 4, headRadius = .34f;
        public float bodyRadius = .44f, bodyHeight = 1.15f, platformHalfWidth = .6f;
        public int terrainSamples = 321;
        public float physicsStep = .016666667f, maxFlightSeconds = 8;
        public void Validate() {
            maxHealth = Mathf.Clamp(maxHealth, 1, 10000); maxWarmth = Mathf.Clamp(maxWarmth, 10, 10000);
            initialWarmth = Mathf.Clamp(initialWarmth, 0, maxWarmth); iceCost = Mathf.Clamp(iceCost, 1, maxWarmth);
            worldWidth = Mathf.Clamp(worldWidth, 20, 50); worldHeight = Mathf.Clamp(worldHeight, 12, 30);
            characterInset = Mathf.Clamp(characterInset, 3, worldWidth / 4); terrainSamples = Mathf.Clamp(terrainSamples, 101, 601);
            gravity = Mathf.Clamp(gravity, 1, 30); minSpeed = Mathf.Clamp(minSpeed, 1, 10); maxSpeed = Mathf.Clamp(maxSpeed, minSpeed+1, 30);
            maxWind = Mathf.Clamp(maxWind, 0, 6); physicsStep = Mathf.Clamp(physicsStep, .005f, .025f); maxFlightSeconds = Mathf.Clamp(maxFlightSeconds, 3, 12);
            quietTurnsForRecovery = Mathf.Max(1, quietTurnsForRecovery);
            snowDamage = Mathf.Max(0, snowDamage); snowWarmthDamage = Mathf.Max(0, snowWarmthDamage);
            iceBodyDamage = Mathf.Max(0, iceBodyDamage); iceHeadDamage = Mathf.Max(0, iceHeadDamage); iceWarmthDamage = Mathf.Max(0, iceWarmthDamage);
            warmActionGain = Mathf.Max(1, warmActionGain); passiveWarmthGain = Mathf.Max(0, passiveWarmthGain); freezeDamage = Mathf.Max(0, freezeDamage);
        }
        void OnValidate() { Validate(); }
    }
}
