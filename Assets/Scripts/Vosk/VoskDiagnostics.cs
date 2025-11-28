using UnityEngine;
using RooseLabs.Vosk;

namespace RooseLabs.Vosk
{
    /// <summary>
    /// Diagnostic tool to test Vosk speech recognition.
    /// Attach this to the same GameObject as VoskSpeechToText to debug voice recognition.
    /// </summary>
    public class VoskDiagnostics : MonoBehaviour
    {
        [SerializeField] private VoskSpeechToText speechToText;
        [SerializeField] private VoiceProcessor voiceProcessor;

        private void Start()
        {
            if (!speechToText)
            {
                speechToText = GetComponent<VoskSpeechToText>();
            }

            if (!voiceProcessor)
            {
                voiceProcessor = FindFirstObjectByType<VoiceProcessor>();
            }

            if (speechToText)
            {
                speechToText.OnTranscriptionResult += OnTranscriptionReceived;
                speechToText.OnStatusUpdated += OnStatusUpdate;
                Debug.Log("[VoskDiagnostics] Subscribed to VoskSpeechToText events.");
            }
            else
            {
                Debug.LogError("[VoskDiagnostics] VoskSpeechToText not found!");
            }

            if (voiceProcessor)
            {
                voiceProcessor.OnFrameCaptured += OnAudioFrameCaptured;
                voiceProcessor.OnRecordingStart += OnRecordingStarted;
                voiceProcessor.OnRecordingStop += OnRecordingStopped;
                Debug.Log("[VoskDiagnostics] Subscribed to VoiceProcessor events.");
            }
            else
            {
                Debug.LogError("[VoskDiagnostics] VoiceProcessor not found!");
            }
        }

        private void OnDestroy()
        {
            if (speechToText)
            {
                speechToText.OnTranscriptionResult -= OnTranscriptionReceived;
                speechToText.OnStatusUpdated -= OnStatusUpdate;
            }

            if (voiceProcessor)
            {
                voiceProcessor.OnFrameCaptured -= OnAudioFrameCaptured;
                voiceProcessor.OnRecordingStart -= OnRecordingStarted;
                voiceProcessor.OnRecordingStop -= OnRecordingStopped;
            }
        }

        private int frameCount = 0;
        private void OnAudioFrameCaptured(short[] audioData)
        {
            frameCount++;
            if (frameCount % 100 == 0) // Log every 100 frames to avoid spam
            {
                Debug.Log($"[VoskDiagnostics] Audio frame captured ({frameCount} total frames). Sample count: {audioData.Length}");
            }
        }

        private void OnRecordingStarted()
        {
            Debug.Log("[VoskDiagnostics] *** RECORDING STARTED ***");
            frameCount = 0;
        }

        private void OnRecordingStopped()
        {
            Debug.Log($"[VoskDiagnostics] *** RECORDING STOPPED *** (Total frames captured: {frameCount})");
        }

        private void OnTranscriptionReceived(string jsonResult)
        {
            Debug.Log($"[VoskDiagnostics] *** TRANSCRIPTION RECEIVED *** Raw JSON: {jsonResult}");

            // Try to parse it
            try
            {
                var result = new RecognitionResult(jsonResult);
                Debug.Log($"[VoskDiagnostics] Parsed result - Partial: {result.Partial}, Phrases count: {result.Phrases.Length}");

                foreach (var phrase in result.Phrases)
                {
                    Debug.Log($"[VoskDiagnostics]   -> Text: '{phrase.Text}', Confidence: {phrase.Confidence:F2}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VoskDiagnostics] Failed to parse result: {e.Message}");
            }
        }

        private void OnStatusUpdate(string status)
        {
            Debug.Log($"[VoskDiagnostics] Status: {status}");
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== VOSK DIAGNOSTICS ===");

            if (voiceProcessor)
            {
                GUILayout.Label($"Recording: {voiceProcessor.IsRecording}");
                GUILayout.Label($"Device: {voiceProcessor.CurrentDeviceName}");
                GUILayout.Label($"Frames captured: {frameCount}");
            }
            else
            {
                GUILayout.Label("VoiceProcessor: NOT FOUND");
            }

            GUILayout.EndArea();
        }
    }
}