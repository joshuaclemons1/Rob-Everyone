using System.Collections.Generic;
using RobEveryone.Inventory;
using UnityEngine;
using AudioSettings = RobEveryone.Audio.AudioSettings; // UnityEngine also declares an AudioSettings class -- disambiguate here rather than fully-qualifying every call site

namespace RobEveryone.UI
{
    // Builds the Audio tab: 4 volume sliders bound to AudioSettings,
    // plus a live per-connected-rival mute row list. Unlike the
    // Controls tab's rebind list (a fixed set of actions, built once),
    // the mute list is rebuilt every time this tab opens -- who's
    // actually connected can change between visits.
    public class AudioTabUI : MonoBehaviour
    {
        [SerializeField] private SliderInputFieldSync masterSync;
        [SerializeField] private SliderInputFieldSync musicSync;
        [SerializeField] private SliderInputFieldSync sfxSync;
        [SerializeField] private SliderInputFieldSync voiceSync;

        [SerializeField] private Transform muteRowContainer;
        [SerializeField] private MuteRow muteRowTemplate;

        private readonly List<MuteRow> activeRows = new();

        private void Awake()
        {
            if (masterSync != null) masterSync.OnValueChanged += v => AudioSettings.MasterVolume = v;
            if (musicSync != null) musicSync.OnValueChanged += v => AudioSettings.MusicVolume = v;
            if (sfxSync != null) sfxSync.OnValueChanged += v => AudioSettings.SFXVolume = v;
            if (voiceSync != null) voiceSync.OnValueChanged += v => AudioSettings.VoiceVolume = v;
        }

        private void OnEnable()
        {
            masterSync?.SetValueWithoutNotify(AudioSettings.MasterVolume);
            musicSync?.SetValueWithoutNotify(AudioSettings.MusicVolume);
            sfxSync?.SetValueWithoutNotify(AudioSettings.SFXVolume);
            voiceSync?.SetValueWithoutNotify(AudioSettings.VoiceVolume);

            RebuildMuteRows();
        }

        private void RebuildMuteRows()
        {
            foreach (MuteRow row in activeRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            activeRows.Clear();

            if (muteRowContainer == null || muteRowTemplate == null) return;

            foreach (PlayerInventory player in PlayerInventory.AllPlayers)
            {
                if (player == null || player.isOwned) continue; // can't mute yourself

                MuteRow row = Instantiate(muteRowTemplate, muteRowContainer);
                row.gameObject.SetActive(true);
                row.Bind(player.DisplayName, player.netId);
                activeRows.Add(row);
            }
        }
    }
}
