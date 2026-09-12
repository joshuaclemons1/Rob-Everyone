using System.Collections.Generic;

namespace RobEveryone.Voice
{
    // The mute half of the settings menu's Audio tab -- session-local,
    // deliberately not persisted (muting someone only matters while
    // they're in your current lobby), matching voip-setup.md Part 5's
    // original design.
    public static class VoiceMuteList
    {
        private static readonly HashSet<uint> muted = new();

        public static bool IsMuted(uint netId) => muted.Contains(netId);

        public static void SetMuted(uint netId, bool value)
        {
            if (value) muted.Add(netId);
            else muted.Remove(netId);
        }
    }
}
