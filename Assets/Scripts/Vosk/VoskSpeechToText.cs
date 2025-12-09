using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Vosk;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Vosk
{
    public class VoskSpeechToText : MonoBehaviour
    {
        private static Logger Logger => Logger.GetLogger("SpeechToText");
        [SerializeField, Tooltip("The source of the microphone input.")]
        private VoiceProcessor voiceProcessor;

        [SerializeField, Tooltip("The maximum number of alternatives that will be processed.")]
        private int maxAlternatives = 3;

        [SerializeField, Tooltip("Should the recognizer start automatically?")]
        private bool autoStart = true;

        [SerializeField, Tooltip("The phrases that will be detected. If left empty, all words will be detected.")]
        private string[] keyPhrases = Array.Empty<string>();

        // The path to the model folder relative to StreamingAssets.
        private string m_modelPath = "LanguageModels/en-US";

        // Cached version of the Vosk Model.
        private Model m_model;

        // Cached version of the Vosk recognizer.
        private VoskRecognizer m_recognizer;

        // Conditional flag to see if a recognizer has already been created.
        private bool m_recognizerReady;

        // Called when the state of the controller changes.
        public event Action<string> OnStatusUpdated;

        // Called after the user is done speaking and Vosk processes the audio.
        public event Action<string> OnTranscriptionResult;

        // The absolute path to the decompressed model folder.
        private string m_decompressedModelPath;

        // A string that contains the keywords in JSON Array format
        private string m_grammar = "";

        // Flag that is used to wait for the script to start successfully.
        private bool m_isInitializing;

        // Flag that is used to check if Vosk was started.
        private bool m_didInit;

        // Threading Logic

        // Flag to signal we are ending
        private bool m_running;

        /// <summary>
        /// Indicates whether speech recognition is currently active
        /// </summary>
        public bool IsRecording => m_running;

        // Thread safe queue of microphone data.
        private readonly ConcurrentQueue<short[]> m_threadedBufferQueue = new();

        // Thread safe queue of results
        private readonly ConcurrentQueue<string> m_threadedResultQueue = new();

        // If Auto Start is enabled, starts Vosk speech to text.
        private void Start()
        {
            if (autoStart)
                StartVoskStt();
        }

        /// <summary>
        /// Start Vosk Speech to text
        /// </summary>
        /// <param name="keyPhrases">A list of keywords/phrases. Keywords need to exist in the models dictionary, so some words like "webview" are better detected as two more common words "web view".</param>
        /// <param name="modelPath">The path to the model folder relative to StreamingAssets.</param>
        /// <param name="startRecording">Should start recording immediately after initialization?</param>
        /// <param name="maxAlternatives">The maximum number of alternative phrases detected</param>
        public void StartVoskStt(string[] keyPhrases = null, string modelPath = null, bool startRecording = false, int maxAlternatives = 3)
        {
            if (m_isInitializing)
            {
                Logger.Error("Initialization in progress!");
                return;
            }

            if (m_didInit)
            {
                Logger.Error("Vosk has already been initialized!");
                return;
            }

            if (!string.IsNullOrEmpty(modelPath))
            {
                m_modelPath = modelPath;
            }

            if (keyPhrases != null)
            {
                this.keyPhrases = keyPhrases;
            }

            this.maxAlternatives = maxAlternatives;
            StartCoroutine(DoStartVoskStt(startRecording));
        }

        // Load model and start Vosk with optional recording start
        private IEnumerator DoStartVoskStt(bool startRecording)
        {
            m_isInitializing = true;
            yield return WaitForMicrophoneInput();

            m_decompressedModelPath = Path.Combine(Application.streamingAssetsPath, m_modelPath);

            OnStatusUpdated?.Invoke("Loading Model from: " + m_decompressedModelPath);
            m_model = new Model(m_decompressedModelPath);

            yield return null;

            OnStatusUpdated?.Invoke("Initialized");
            voiceProcessor.OnFrameCaptured += VoiceProcessorOnOnFrameCaptured;
            voiceProcessor.OnRecordingStop += VoiceProcessorOnOnRecordingStop;

            m_isInitializing = false;
            m_didInit = true;

            if (startRecording)
                StartRecording();
        }

        // Translates the keyPhrases into a JSON array and appends the `[unk]` keyword at the end to tell Vosk to filter other phrases.
        private void UpdateGrammar()
        {
            if (keyPhrases.Length == 0)
            {
                m_grammar = "";
                return;
            }

            JArray keywords = new JArray();
            foreach (string keyphrase in keyPhrases)
            {
                keywords.Add(keyphrase.ToLower());
            }

            keywords.Add("[unk]");

            m_grammar = keywords.ToString();
        }

        // Wait until microphones are initialized
        private IEnumerator WaitForMicrophoneInput()
        {
            while (Microphone.devices.Length <= 0)
                yield return null;
        }

        /// <summary>
        /// Starts recording and speech recognition
        /// </summary>
        public void StartRecording()
        {
            if (!m_didInit)
            {
                Logger.Warning("VoskSpeechToText has not been initialized yet!");
                return;
            }

            if (m_running) return;

            Logger.Info("Started Recording");
            m_running = true;
            voiceProcessor.StartRecording();
            Task.Run(ThreadedWork).ConfigureAwait(false);
        }

        /// <summary>
        /// Stops recording and speech recognition
        /// </summary>
        public void StopRecording()
        {
            if (!m_running) return;
            Logger.Info("Stopped Recording");
            m_running = false;
            voiceProcessor.StopRecording();
        }

        /// <summary>
        /// Toggles recording on/off
        /// </summary>
        public void ToggleRecording()
        {
            if (voiceProcessor.IsRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        // Calls the On Phrase Recognized event on the Unity Thread
        private void Update()
        {
            if (m_threadedResultQueue.TryDequeue(out string voiceResult))
            {
                OnTranscriptionResult?.Invoke(voiceResult);
            }
        }

        // Callback from the voice processor when new audio is detected
        private void VoiceProcessorOnOnFrameCaptured(short[] samples)
        {
            m_threadedBufferQueue.Enqueue(samples);
        }

        // Callback from the voice processor when recording stops
        private void VoiceProcessorOnOnRecordingStop()
        {
            m_running = false;
        }

        private async Task ThreadedWork()
        {
            if (!m_recognizerReady)
            {
                UpdateGrammar();

                //Only detect defined keywords if they are specified.
                if (string.IsNullOrEmpty(m_grammar))
                {
                    m_recognizer = new VoskRecognizer(m_model, 16000.0f);
                }
                else
                {
                    m_recognizer = new VoskRecognizer(m_model, 16000.0f, m_grammar);
                }

                m_recognizer.SetMaxAlternatives(maxAlternatives);
                // m_recognizer.SetWords(true);
                m_recognizerReady = true;

                Logger.Info("Vosk Recognizer Ready");
            }

            while (m_running)
            {
                if (m_threadedBufferQueue.TryDequeue(out short[] voiceResult))
                {
                    if (m_recognizer.AcceptWaveform(voiceResult, voiceResult.Length))
                    {
                        var result = m_recognizer.Result();
                        m_threadedResultQueue.Enqueue(result);
                    }
                }
                else
                {
                    // Wait for some data
                    await Task.Delay(100);
                }
            }
        }
    }
}
