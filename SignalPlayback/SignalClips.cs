using System;
using System.Collections;
using UnityEngine;

namespace SignalSimMods.SignalPlayback
{
    /// <summary>
    /// Works out which AudioClip DBMain.DBaudioPlay would play for a signal, so its length can be shown
    /// as soon as the signal is selected (before PLAY is pressed). Mirrors the game's lookup order, but
    /// never writes back to the save (the game resets SignalAudio to 0 when a clip is missing).
    /// </summary>
    internal static class SignalClips
    {
        public static IEnumerator Resolve(SignalModel signal, Action<AudioClip> done)
        {
            if (signal == null) { done(null); yield break; }

            var library = SignalLibrary.instance;
            if (signal.SignalFilter == "Random")
            {
                yield return Load("Signals/audio/randomAudio/s", signal.SignalAudio, done);
            }
            else if (signal.HalloWeenEvent) done(SignalLibrary.HalloweenClip);
            else if (signal.TriPodEvent) done(library != null ? library.Tripod : null);
            else if (signal.BorgEvent || signal.BorgInvasion) done(library != null ? library.borgAudio : null);
            else if (signal.SignalId == "Event1") done(library != null ? library.broadcast1 : null);
            else if (signal.SignalId == "Event2") done(library != null ? library.broadcast2 : null);
            else if (signal.SignalId == "Event3") done(library != null ? library.broadcast3 : null);
            else if (signal.SignalId == "Event4") done(library != null ? library.broadcast4 : null);
            else if (signal.SignalId == "Event5") done(library != null ? library.morseCode : null);
            else
            {
                yield return Load("Signals/audio/storyAudio/s", signal.SignalAudio, done);
            }
        }

        private static IEnumerator Load(string prefix, int index, Action<AudioClip> done)
        {
            var request = Resources.LoadAsync<AudioClip>(prefix + index);
            yield return request;
            var clip = request.asset as AudioClip;
            if (clip == null && index != 0)
            {
                // Same fallback as the game: a missing clip plays s0 instead.
                request = Resources.LoadAsync<AudioClip>(prefix + 0);
                yield return request;
                clip = request.asset as AudioClip;
            }
            done(clip);
        }
    }
}
