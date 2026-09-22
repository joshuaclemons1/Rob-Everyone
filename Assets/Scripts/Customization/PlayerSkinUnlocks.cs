using System;
using System.Collections.Generic;
using UnityEngine;

namespace RobEveryone.Customization
{
    // Issue #52 (Phase 0): the set of skins a player has actually
    // unlocked, persisted locally via PlayerPrefs -- same "static class
    // over PlayerPrefs, OnChanged event" shape PlayerCosmeticSelection
    // already uses, but holding a *set* of indices instead of one
    // selected index. PlayerPrefs has no native array/set type, so the
    // set is stored as a single delimited string under one key.
    //
    // Skin index 0 is always implicitly unlocked, unconditionally --
    // without that, a brand-new player who's never visited a Lobby
    // pedestal would have nothing valid to spawn wearing at all.
    //
    // This is the "genuinely new piece" issue #54 identified as
    // missing (selected-skin persistence already existed via
    // PlayerCosmeticSelection; a persisted *set of unlocked* skins did
    // not, in any form) -- effectively resolves that issue once wired
    // up through SkinOfferPedestal.
    public static class PlayerSkinUnlocks
    {
        private const string UnlockedSkinsKey = "RobEveryone.UnlockedSkins";
        private const char Delimiter = ',';

        public static event Action OnChanged;

        public static bool IsUnlocked(int skinIndex) =>
            skinIndex == 0 || LoadSet().Contains(skinIndex);

        // Idempotent -- unlocking an already-unlocked skin (a pedestal
        // re-rolling to something a player already has, or interacting
        // with the same offer twice) is a harmless no-op, not an error.
        public static void Unlock(int skinIndex)
        {
            HashSet<int> set = LoadSet();
            if (!set.Add(skinIndex)) return;

            SaveSet(set);
            OnChanged?.Invoke();
        }

        // Sorted, and always includes 0 even though it's never actually
        // stored -- callers (MirrorSkinCycleButton's next/previous
        // cycling) shouldn't need to separately special-case "0 is
        // implicitly unlocked" on top of whatever this returns.
        public static IReadOnlyList<int> UnlockedSkins
        {
            get
            {
                HashSet<int> set = LoadSet();
                set.Add(0);
                var sorted = new List<int>(set);
                sorted.Sort();
                return sorted;
            }
        }

        private static HashSet<int> LoadSet()
        {
            string raw = PlayerPrefs.GetString(UnlockedSkinsKey, "");
            var set = new HashSet<int>();
            if (string.IsNullOrEmpty(raw)) return set;

            foreach (string token in raw.Split(Delimiter))
            {
                if (int.TryParse(token, out int index)) set.Add(index);
            }
            return set;
        }

        private static void SaveSet(HashSet<int> set)
        {
            PlayerPrefs.SetString(UnlockedSkinsKey, string.Join(Delimiter, set));
            PlayerPrefs.Save();
        }
    }
}
