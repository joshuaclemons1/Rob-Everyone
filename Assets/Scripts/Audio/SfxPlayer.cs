using UnityEngine;

namespace RobEveryone.Audio
{
    // Shared one-shot playback for every system that just needs "play a
    // random clip from this pool at this world position" -- pickup,
    // impact, explosion, jail, shop, and AI-stinger sounds all reduce to
    // exactly this, so each of those call sites reuses this instead of
    // re-writing the same null-check-then-PlayClipAtPoint logic. Not a
    // MonoBehaviour: PlayClipAtPoint spins up its own temporary GameObject
    // internally, so nothing here needs a persistent component of its own.
    public static class SfxPlayer
    {
        public static void PlayRandomAt(AudioClip[] pool, Vector3 position, float volume = 1f)
        {
            if (pool == null || pool.Length == 0) return;
            AudioClip clip = pool[Random.Range(0, pool.Length)];
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }
    }
}
