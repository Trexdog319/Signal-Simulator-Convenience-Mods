using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace SignalSimMods.PerformanceTweaks
{
    /// <summary>
    /// Records the first-seen value of everything the plugin changes so the whole set can be
    /// rolled back live (F9) without restarting the game.
    /// </summary>
    internal static class Originals
    {
        private static readonly Dictionary<string, Action> restorers = new Dictionary<string, Action>();
        private static readonly Dictionary<string, object> originals = new Dictionary<string, object>();

        private static string Key(Object owner, string field) =>
            (ReferenceEquals(owner, null) ? "global" : owner.GetInstanceID().ToString()) + ":" + field;

        /// <summary>Set a value, remembering the original the first time this owner/field is touched.</summary>
        public static void Set<T>(Object owner, string field, Func<T> get, Action<T> set, T value)
        {
            var key = Key(owner, field);
            T current = get();
            if (!restorers.ContainsKey(key))
            {
                T original = current;
                originals[key] = original;
                restorers[key] = () =>
                {
                    // Skip objects that were destroyed (scene change); globals have a null owner.
                    if (!ReferenceEquals(owner, null) && owner == null) return;
                    set(original);
                };
            }
            if (!EqualityComparer<T>.Default.Equals(current, value)) set(value);
        }

        /// <summary>Put a single tracked value back and forget it.</summary>
        public static void Restore(Object owner, string field)
        {
            var key = Key(owner, field);
            if (restorers.TryGetValue(key, out var restore))
            {
                restore();
                restorers.Remove(key);
                originals.Remove(key);
            }
        }

        public static bool IsTracked(Object owner, string field) => restorers.ContainsKey(Key(owner, field));

        /// <summary>The game's own value if we've overridden it, otherwise the current value.</summary>
        public static T GetOriginal<T>(Object owner, string field, T current) =>
            originals.TryGetValue(Key(owner, field), out var value) ? (T)value : current;

        public static void RestoreAll()
        {
            foreach (var restore in restorers.Values)
            {
                try { restore(); }
                catch (Exception e) { PerformanceTweaksPlugin.Log.LogWarning("Restore failed: " + e.Message); }
            }
            restorers.Clear();
            originals.Clear();
        }
    }
}
