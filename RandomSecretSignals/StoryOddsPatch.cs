using System.Reflection;
using HarmonyLib;

namespace SignalSimMods.RandomSecretSignals
{
    /// <summary>
    /// SignalGenerator.GenerateSignal decides story vs. random with `random.Next(10) == 3` (10%).
    /// Its private static System.Random is swapped for a subclass that answers that one call using
    /// StorySignalChance. Every other roll passes straight through to the normal generator.
    /// </summary>
    [HarmonyPatch(typeof(SignalGenerator), "GenerateSignal")]
    internal static class StoryOddsPatch
    {
        private const int StoryRoll = 3;   // the value GenerateSignal treats as "story"
        private static readonly FieldInfo RandomField = AccessTools.Field(typeof(SignalGenerator), "random");

        private sealed class StoryOddsRandom : System.Random
        {
            public bool Armed;

            public override int Next(int maxValue)
            {
                if (Armed && maxValue == 10)
                {
                    Armed = false;
                    bool story = UnityEngine.Random.value * 100f < RandomSecretSignalsPlugin.StoryChance.Value;
                    return story ? StoryRoll : 0;
                }
                return base.Next(maxValue);
            }
        }

        [HarmonyPrefix]
        private static void Prefix()
        {
            var rng = RandomField.GetValue(null) as StoryOddsRandom;
            if (rng == null)
            {
                rng = new StoryOddsRandom();
                RandomField.SetValue(null, rng);
            }
            rng.Armed = true;   // only the first Next(10) in this call is the story roll
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            var rng = RandomField.GetValue(null) as StoryOddsRandom;
            if (rng != null) rng.Armed = false;
        }
    }
}
