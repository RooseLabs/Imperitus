using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Vosk;

namespace RooseLabs.Vosk
{
    public class VoskSpeechToText : MonoBehaviour
    {
        [SerializeField, Tooltip("The source of the microphone input.")]
        private VoiceProcessor voiceProcessor;

        [SerializeField, Tooltip("The maximum number of alternatives that will be processed.")]
        private int maxAlternatives = 3;

        [SerializeField, Tooltip("Should the recognizer start automatically?")]
        private bool autoStart = true;

        [SerializeField, Tooltip("The phrases that will be detected. If left empty, all words will be detected.")]
        private List<string> keyPhrases = new();

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
        /// <param name="startMicrophone">"Should the microphone after vosk initializes?</param>
        /// <param name="maxAlternatives">The maximum number of alternative phrases detected</param>
        public void StartVoskStt(List<string> keyPhrases = null, string modelPath = null, bool startMicrophone = false, int maxAlternatives = 3)
        {
            if (m_isInitializing)
            {
                Debug.LogError("Initializing in progress!");
                return;
            }

            if (m_didInit)
            {
                Debug.LogError("Vosk has already been initialized!");
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
            StartCoroutine(DoStartVoskStt(startMicrophone));
        }

        // Load model and start Vosk with optional microphone start
        private IEnumerator DoStartVoskStt(bool startMicrophone)
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

            if (startMicrophone)
                voiceProcessor.StartRecording();

            m_isInitializing = false;
            m_didInit = true;

            ToggleRecording();
        }

        // Translates the keyPhrases into a JSON array and appends the `[unk]` keyword at the end to tell Vosk to filter other phrases.
        private void UpdateGrammar()
        {
            if (keyPhrases.Count == 0)
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

        // Can be called from a script or a GUI button to start detection.
        public void ToggleRecording()
        {
            Debug.Log("Toggle Recording");
            if (!voiceProcessor.IsRecording)
            {
                Debug.Log("Start Recording");
                m_running = true;
                voiceProcessor.StartRecording();
                Task.Run(ThreadedWork).ConfigureAwait(false);
            }
            else
            {
                Debug.Log("Stop Recording");
                m_running = false;
                voiceProcessor.StopRecording();
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
            Debug.Log("Stopped");
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

                Debug.Log("Vosk recognizer ready");
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
