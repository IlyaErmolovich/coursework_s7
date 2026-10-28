using UnityEngine;
namespace FrostDuel {
    public static class WeatherController {
        public static void Next(GameSession s, BalanceSettings b) {
            var r = new System.Random(unchecked(s.seed * 397 ^ s.turnId * 7919));
            s.wind = ((float)r.NextDouble()*2-1)*b.maxWind;
            s.blizzard = r.NextDouble()<.25;
        }
    }
}
