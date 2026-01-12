using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MetaVoiceChat.Output.AudioSource;
using RooseLabs.Network;
using RooseLabs.Player;
using RooseLabs.UI.Elements;
using RooseLabs.Utils;
using TMPro;
using UnityEngine;

namespace RooseLabs.UI
{
    public class UIVoiceChatVolumeControl : MonoBehaviour
    {
        [SerializeField] private GameObject playerVolumeControlPrefab;

        private readonly Dictionary<PlayerCharacter, GameObject> m_playerVolumeControls = new();
        private Coroutine m_refreshCheckCoroutine;

        private void OnEnable()
        {
            m_refreshCheckCoroutine = StartCoroutine(RefreshCheckCoroutine());
        }

        private void OnDisable()
        {
            // Stop the refresh check coroutine
            if (m_refreshCheckCoroutine != null)
            {
                StopCoroutine(m_refreshCheckCoroutine);
                m_refreshCheckCoroutine = null;
            }

            // Destroy all instantiated volume controls
            foreach (var kvp in m_playerVolumeControls)
            {
                if (kvp.Value) Destroy(kvp.Value);
            }
            m_playerVolumeControls.Clear();
        }

        private IEnumerator RefreshCheckCoroutine()
        {
            var waitForSeconds = new WaitForSeconds(1f);
            while (true)
            {
                // Check if players have joined or left
                var currentPlayers = PlayerHandler.AllConnectedCharacters
                    .Where(p => p != PlayerCharacter.LocalCharacter)
                    .ToList();

                // Check if count changed or if any player is missing from dictionary
                bool needsRefresh = currentPlayers.Count != m_playerVolumeControls.Count ||
                                    currentPlayers.Any(p => !m_playerVolumeControls.ContainsKey(p));

                if (needsRefresh)
                    yield return RefreshPlayerList();

                yield return waitForSeconds;
            }
        }

        private IEnumerator RefreshPlayerList()
        {
            var currentPlayers = PlayerHandler.AllConnectedCharacters
                .Where(p => p != PlayerCharacter.LocalCharacter)
                .ToList();

            // Remove controls for players that are no longer connected
            var playersToRemove = m_playerVolumeControls.Keys
                .Where(p => !currentPlayers.Contains(p))
                .ToList();

            foreach (var player in playersToRemove)
            {
                if (m_playerVolumeControls[player])
                {
                    Destroy(m_playerVolumeControls[player]);
                }
                m_playerVolumeControls.Remove(player);
            }

            // Add controls for new players with delay until PlayerName is available
            foreach (var player in currentPlayers)
            {
                if (m_playerVolumeControls.ContainsKey(player)) continue;
                // Wait until the character has a valid PlayerName
                float timeout = 10f; // 10 second timeout
                float elapsed = 0f;

                while (elapsed < timeout && (bool)player && string.IsNullOrEmpty(player.Player?.PlayerName))
                {
                    yield return new WaitForSeconds(0.1f);
                    elapsed += 0.1f;
                }

                // Only create the control if the character still exists and has a name
                if ((bool)player && !string.IsNullOrEmpty(player.Player?.PlayerName))
                {
                    CreatePlayerVolumeControl(player);
                }
            }
        }

        private void CreatePlayerVolumeControl(PlayerCharacter character)
        {
            if (!character) return;

            // Instantiate the prefab
            GameObject controlObj = Instantiate(playerVolumeControlPrefab, transform);

            // Find and set the player name text
            TMP_Text[] textComponents = controlObj.GetComponentsInChildren<TMP_Text>();
            TMP_Text playerNameText = textComponents.FirstOrDefault(t => t.gameObject.name == "PlayerName");

            if (playerNameText)
            {
                playerNameText.text = character.Player?.PlayerName ?? "Unknown";
            }

            // Find and configure the volume slider
            if (controlObj.TryGetComponentInChildren(out UISlider volumeSlider))
            {
                // Configure slider range and formatter
                volumeSlider.SetRange(0f, 1f);
                volumeSlider.SetPrecision(2);
                volumeSlider.SetCustomFormatter(v => $"{Mathf.RoundToInt(v * 100)}%");

                // Get current volume and initialize slider
                float currentVolume = 0.5f;
                VcAudioSourceOutput audioOutput = character.VoiceChat?.audioOutput as VcAudioSourceOutput;
                if (audioOutput != null && audioOutput.audioSource != null)
                {
                    currentVolume = audioOutput.audioSource.volume;
                }
                volumeSlider.SetValue(currentVolume);

                // Subscribe to value changes
                volumeSlider.OnValueChanged += (value) =>
                {
                    OnPlayerVolumeChanged(character, value);
                };
            }

            // Store the control
            m_playerVolumeControls[character] = controlObj;
        }

        private void OnPlayerVolumeChanged(PlayerCharacter character, float volume)
        {
            if (!character || !character.VoiceChat) return;

            // Set audio source volume
            VcAudioSourceOutput audioOutput = character.VoiceChat.audioOutput as VcAudioSourceOutput;
            if (audioOutput != null && audioOutput.audioSource != null)
            {
                audioOutput.audioSource.volume = volume;
            }

            // Set mute state when volume is 0
            character.VoiceChat.isOutputMuted.Value = (volume == 0f);
        }
    }
}
