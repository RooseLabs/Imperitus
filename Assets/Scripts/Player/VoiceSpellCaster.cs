using RooseLabs.Core;
using RooseLabs.Vosk;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.Player
{
    /// <summary>
    /// Handles voice-activated spell casting by listening to speech recognition
    /// and triggering spells through the PlayerWand system.
    /// Uses input injection to simulate cast button holds.
    /// </summary>
    public class VoiceSpellCaster : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Reference to the player character's wand.")]
        private PlayerWand playerWand;

        [SerializeField, Tooltip("Reference to the Vosk speech-to-text system.")]
        private VoskSpeechToText speechToText;

        [Header("Settings")]
        [SerializeField, Tooltip("Minimum confidence required for voice commands (0.0 - 1.0).")]
        private float minimumConfidence = 0.60f;

        [SerializeField, Tooltip("Cooldown in seconds between voice commands to prevent spam.")]
        private float voiceCastCooldown = 0.8f;

        [Header("Spell Behavior")]
        [SerializeField, Tooltip("Spells that should auto-hold cast when voice activated (like Command).")]
        private string[] spellsWithAutoHold = new string[] { "Command" };

        private float m_lastVoiceCommandTime = -999f;
        private PlayerCharacter m_character;

        // Track if we're simulating a hold for a spell
        private bool m_isSimulatingCastHold = false;
        private string m_currentVoiceActivatedSpell = null;

        // Track first frame of simulation
        private bool m_isFirstFrameOfSimulation = false;

        private void Start()
        {
            // Get player character reference
            m_character = GetComponent<PlayerCharacter>();

            if (!m_character)
            {
                Debug.LogError("[VoiceSpellCaster] No PlayerCharacter component found on this GameObject!");
                enabled = false;
                return;
            }

            // Validate references
            if (!playerWand)
            {
                Debug.LogError("[VoiceSpellCaster] PlayerWand reference is not assigned!");
                enabled = false;
                return;
            }

            if (!speechToText)
            {
                Debug.LogError("[VoiceSpellCaster] VoskSpeechToText reference is not assigned!");
                enabled = false;
                return;
            }

            // Subscribe to speech recognition results
            speechToText.OnTranscriptionResult += HandleVoiceCommand;
            Debug.Log("[VoiceSpellCaster] Voice spell casting system initialized.");
        }

        private void OnDestroy()
        {
            // Clean up any active simulations
            if (m_isSimulatingCastHold)
            {
                StopSimulatedCastHold();
            }

            // Unsubscribe to prevent memory leaks
            if (speechToText)
            {
                speechToText.OnTranscriptionResult -= HandleVoiceCommand;
            }
        }

        private void Update()
        {
            // Only process for local player
            if (!m_character || !m_character.IsOwner)
                return;

            // Handle simulated cast hold
            if (m_isSimulatingCastHold)
            {
                // Check if player stopped aiming (released right mouse button)
                if (!m_character.Data.isAiming)
                {
                    // Player released aim button - stop the spell
                    StopSimulatedCastHold();
                }
            }
        }

        /// <summary>
        /// Handles incoming voice recognition results and attempts to activate spells.
        /// </summary>
        private void HandleVoiceCommand(string jsonResult)
        {
            Debug.Log("[VoiceSpellCaster] Received voice command result.");

            // Only process voice commands for the local player
            if (!m_character || !m_character.IsOwner)
                return;

            // Player must be aiming to use voice commands
            if (!m_character.Data.isAiming)
            {
                Debug.Log("[VoiceSpellCaster] Player not aiming. Ignoring voice command.");
                return;
            }

            // Check cooldown
            if (Time.time - m_lastVoiceCommandTime < voiceCastCooldown)
            {
                Debug.Log("[VoiceSpellCaster] Voice command on cooldown. Ignoring command.");
                return;
            }

            // Parse the recognition result
            var result = new RecognitionResult(jsonResult);

            // Ignore partial results (player still speaking)
            if (result.Partial)
            {
                Debug.Log("[VoiceSpellCaster] Partial result detected. Waiting for complete phrase.");
                return;
            }

            // Check all recognized phrases in order of confidence
            foreach (var phrase in result.Phrases)
            {
                // Skip low confidence results
                if (phrase.Confidence < minimumConfidence)
                {
                    Debug.Log($"[VoiceSpellCaster] Confidence too low: {phrase.Confidence:F2} (min: {minimumConfidence:F2})");
                    continue;
                }

                string spellName = phrase.Text.Trim();

                // Attempt to activate the spell by voice
                if (TryActivateSpellByVoice(spellName, phrase.Confidence))
                {
                    m_lastVoiceCommandTime = Time.time;
                    return; // Successfully activated, stop checking alternatives
                }
            }
        }

        /// <summary>
        /// Attempts to find and activate a spell by its English name.
        /// </summary>
        /// <param name="spellName">The English name of the spell to cast.</param>
        /// <param name="confidence">The confidence level of the voice recognition.</param>
        /// <returns>True if the spell was found and activated successfully.</returns>
        private bool TryActivateSpellByVoice(string spellName, float confidence)
        {
            // Try to switch to the spell by name
            if (!playerWand.TrySetSpellByName(spellName))
            {
                Debug.Log($"[VoiceSpellCaster] Spell '{spellName}' not found in available spells.");
                return false;
            }

            Debug.Log($"[VoiceSpellCaster] Voice command recognized: '{spellName}' (confidence: {confidence:F2})");

            // Check if this spell requires auto-hold behavior
            bool requiresAutoHold = System.Array.Exists(spellsWithAutoHold,
                s => s.Equals(spellName, System.StringComparison.OrdinalIgnoreCase));

            if (requiresAutoHold)
            {
                // Start simulating cast hold for this spell
                StartSimulatedCastHold(spellName);
            }
            else
            {
                // Normal spell - trigger a single cast
                Debug.Log($"[VoiceSpellCaster] Triggering normal cast for '{spellName}'.");
                // For instant spells, we just need to trigger one frame of input
                m_isSimulatingCastHold = true;
                m_isFirstFrameOfSimulation = true;
                m_currentVoiceActivatedSpell = spellName;
            }

            return true;
        }

        /// <summary>
        /// Starts simulating a cast hold (as if left mouse button is being held).
        /// This is done by directly interfacing with PlayerInput AFTER it samples.
        /// </summary>
        private void StartSimulatedCastHold(string spellName)
        {
            if (m_isSimulatingCastHold)
            {
                // Already simulating - cancel previous one first
                StopSimulatedCastHold();
            }

            m_isSimulatingCastHold = true;
            m_isFirstFrameOfSimulation = true;
            m_currentVoiceActivatedSpell = spellName;

            Debug.Log($"[VoiceSpellCaster] Started simulated cast hold for '{spellName}'. Player can aim freely and release RMB to stop.");
        }

        /// <summary>
        /// Stops simulating the cast hold and releases the spell.
        /// </summary>
        private void StopSimulatedCastHold()
        {
            if (!m_isSimulatingCastHold)
                return;

            Debug.Log($"[VoiceSpellCaster] Stopped simulated cast hold for '{m_currentVoiceActivatedSpell}'.");

            m_isSimulatingCastHold = false;
            m_isFirstFrameOfSimulation = false;
            m_currentVoiceActivatedSpell = null;
        }

        /// <summary>
        /// Called by PlayerInput.Sample() AFTER it reads input to inject our simulated input.
        /// This must be called from PlayerInput.Sample() at the end.
        /// </summary>
        public void InjectSimulatedInput(PlayerInput input)
        {
            if (!m_isSimulatingCastHold)
                return;

            // Check if this is an auto-hold spell or instant cast
            bool isAutoHoldSpell = System.Array.Exists(spellsWithAutoHold,
                s => s.Equals(m_currentVoiceActivatedSpell, System.StringComparison.OrdinalIgnoreCase));

            if (m_isFirstFrameOfSimulation)
            {
                // First frame: simulate button press
                input.castWasPressed = true;
                input.castIsPressed = true;
                input.castWasReleased = false;
                m_isFirstFrameOfSimulation = false;

                // If it's not an auto-hold spell, stop after one frame
                if (!isAutoHoldSpell)
                {
                    m_isSimulatingCastHold = false;
                    m_currentVoiceActivatedSpell = null;
                }
            }
            else if (isAutoHoldSpell)
            {
                // Subsequent frames for auto-hold spells: keep button pressed
                input.castWasPressed = false;
                input.castIsPressed = true;
                input.castWasReleased = false;
            }
        }

        #region Public API for Configuration

        /// <summary>
        /// Updates the minimum confidence threshold for voice commands.
        /// </summary>
        public void SetMinimumConfidence(float confidence)
        {
            minimumConfidence = Mathf.Clamp01(confidence);
            Debug.Log($"[VoiceSpellCaster] Minimum confidence set to: {minimumConfidence:F2}");
        }

        /// <summary>
        /// Updates the cooldown between voice commands.
        /// </summary>
        public void SetVoiceCastCooldown(float cooldown)
        {
            voiceCastCooldown = Mathf.Max(0f, cooldown);
            Debug.Log($"[VoiceSpellCaster] Voice cast cooldown set to: {voiceCastCooldown}s");
        }

        /// <summary>
        /// Manually stop any active simulated cast hold.
        /// </summary>
        public void ForceStopSimulatedCast()
        {
            if (m_isSimulatingCastHold)
            {
                StopSimulatedCastHold();
            }
        }

        /// <summary>
        /// Check if currently simulating a cast hold.
        /// </summary>
        public bool IsSimulatingCastHold => m_isSimulatingCastHold;

        #endregion
    }
}