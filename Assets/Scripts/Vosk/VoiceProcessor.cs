using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs.Vosk
{
    /// <summary>
    /// Records audio and delivers frames for real-time audio processing
    /// </summary>
    public class VoiceProcessor : MonoBehaviour
    {
        private static Logger Logger => Logger.GetLogger("SpeechToText");
        /// <summary>
        /// Indicates whether microphone is capturing or not
        /// </summary>
        public bool IsRecording => (bool)m_audioClip && Microphone.IsRecording(CurrentDeviceName);

        [SerializeField] private int microphoneIndex;

        /// <summary>
        /// Sample rate of recorded audio
        /// </summary>
        public int SampleRate { get; private set; }

        /// <summary>
        /// Size of audio frames that are delivered
        /// </summary>
        public int FrameLength { get; private set; }

        /// <summary>
        /// Event where frames of audio are delivered
        /// </summary>
        public event Action<short[]> OnFrameCaptured;

        /// <summary>
        /// Event when audio capture thread stops
        /// </summary>
        public event Action OnRecordingStop;

        /// <summary>
        /// Event when audio capture thread starts
        /// </summary>
        public event Action OnRecordingStart;

        /// <summary>
        /// Available audio recording devices
        /// </summary>
        public List<string> Devices { get; private set; }

        /// <summary>
        /// Index of selected audio recording device
        /// </summary>
        public int CurrentDeviceIndex { get; private set; }

        /// <summary>
        /// Name of selected audio recording device
        /// </summary>
        public string CurrentDeviceName
        {
            get
            {
                if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Microphone.devices.Length)
                    return string.Empty;
                return Devices[CurrentDeviceIndex];
            }
        }

        [Header("Voice Detection Settings")]
        [SerializeField, Tooltip("The minimum volume to detect voice input for"), Range(0.0f, 1.0f)]
        private float minimumSpeakingSampleValue = 0.05f;

        [SerializeField, Tooltip("Time in seconds of detected silence before voice request is sent")]
        private float silenceTimer = 1.0f;

        [SerializeField, Tooltip("Auto detect speech using the volume threshold.")]
        private bool autoDetect;

        private float m_timeAtSilenceBegan;
        private bool m_audioDetected;
        private bool m_didDetect;
        private bool m_transmit;

        private AudioClip m_audioClip;
        private event Action RestartRecording;

        private void Awake()
        {
            UpdateDevices();
        }

        #if UNITY_EDITOR
        private void Update()
        {
            if (CurrentDeviceIndex != microphoneIndex)
            {
                ChangeDevice(microphoneIndex);
            }
        }
        #endif

        /// <summary>
        /// Updates list of available audio devices
        /// </summary>
        public void UpdateDevices()
        {
            Devices = new List<string>();
            foreach (var device in Microphone.devices)
                Devices.Add(device);

            if (Devices == null || Devices.Count == 0)
            {
                CurrentDeviceIndex = -1;
                Logger.Warning("There is no valid recording device connected");
                return;
            }

            CurrentDeviceIndex = microphoneIndex;
        }

        /// <summary>
        /// Change audio recording device
        /// </summary>
        /// <param name="deviceIndex">Index of the new audio capture device</param>
        public void ChangeDevice(int deviceIndex)
        {
            if (deviceIndex < 0 || deviceIndex >= Devices.Count)
            {
                Logger.Warning($"Specified device index {deviceIndex} is not a valid recording device");
                return;
            }

            if (IsRecording)
            {
                // one time event to restart recording with the new device
                // the moment the last session has completed
                RestartRecording += () =>
                {
                    CurrentDeviceIndex = deviceIndex;
                    StartRecording(SampleRate, FrameLength);
                    RestartRecording = null;
                };
                StopRecording();
            }
            else
            {
                CurrentDeviceIndex = deviceIndex;
            }
        }

        /// <summary>
        /// Start recording audio
        /// </summary>
        /// <param name="sampleRate">Sample rate to record at</param>
        /// <param name="frameSize">Size of audio frames to be delivered</param>
        /// <param name="autoDetect">Should the audio continuously record based on the volume</param>
        public void StartRecording(int sampleRate = 16000, int frameSize = 512, bool? autoDetect = null)
        {
            if (autoDetect != null)
            {
                this.autoDetect = (bool)autoDetect;
            }

            if (IsRecording)
            {
                // if sample rate or frame size have changed, restart recording
                if (sampleRate != SampleRate || frameSize != FrameLength)
                {
                    RestartRecording += () =>
                    {
                        StartRecording(SampleRate, FrameLength, autoDetect);
                        RestartRecording = null;
                    };
                    StopRecording();
                }

                return;
            }

            SampleRate = sampleRate;
            FrameLength = frameSize;

            m_audioClip = Microphone.Start(CurrentDeviceName, true, 1, sampleRate);

            StartCoroutine(RecordData());
        }

        /// <summary>
        /// Stops recording audio
        /// </summary>
        public void StopRecording()
        {
            if (!IsRecording)
                return;

            Microphone.End(CurrentDeviceName);
            Destroy(m_audioClip);
            m_audioClip = null;
            m_didDetect = false;

            StopCoroutine(RecordData());
        }

        /// <summary>
        /// Loop for buffering incoming audio data and delivering frames
        /// </summary>
        IEnumerator RecordData()
        {
            float[] sampleBuffer = new float[FrameLength];
            int startReadPos = 0;

            OnRecordingStart?.Invoke();

            while (IsRecording)
            {
                int curClipPos = Microphone.GetPosition(CurrentDeviceName);
                if (curClipPos < startReadPos)
                    curClipPos += m_audioClip.samples;

                int samplesAvailable = curClipPos - startReadPos;
                if (samplesAvailable < FrameLength)
                {
                    yield return null;
                    continue;
                }

                int endReadPos = startReadPos + FrameLength;
                if (endReadPos > m_audioClip.samples)
                {
                    // fragmented read (wraps around to beginning of clip)
                    // read bit at end of clip
                    int numSamplesClipEnd = m_audioClip.samples - startReadPos;
                    float[] endClipSamples = new float[numSamplesClipEnd];
                    m_audioClip.GetData(endClipSamples, startReadPos);

                    // read bit at start of clip
                    int numSamplesClipStart = endReadPos - m_audioClip.samples;
                    float[] startClipSamples = new float[numSamplesClipStart];
                    m_audioClip.GetData(startClipSamples, 0);

                    // combine to form full frame
                    Buffer.BlockCopy(endClipSamples, 0, sampleBuffer, 0, numSamplesClipEnd);
                    Buffer.BlockCopy(startClipSamples, 0, sampleBuffer, numSamplesClipEnd, numSamplesClipStart);
                }
                else
                {
                    m_audioClip.GetData(sampleBuffer, startReadPos);
                }

                startReadPos = endReadPos % m_audioClip.samples;
                if (!autoDetect)
                {
                    m_transmit = m_audioDetected = true;
                }
                else
                {
                    float maxVolume = 0.0f;

                    for (int i = 0; i < sampleBuffer.Length; i++)
                    {
                        if (sampleBuffer[i] > maxVolume)
                        {
                            maxVolume = sampleBuffer[i];
                        }
                    }

                    if (maxVolume >= minimumSpeakingSampleValue)
                    {
                        m_transmit = m_audioDetected = true;
                        m_timeAtSilenceBegan = Time.time;
                    }
                    else
                    {
                        m_transmit = false;

                        if (m_audioDetected && Time.time - m_timeAtSilenceBegan > silenceTimer)
                        {
                            m_audioDetected = false;
                        }
                    }
                }

                if (m_audioDetected)
                {
                    m_didDetect = true;
                    // converts to 16-bit int samples
                    short[] pcmBuffer = new short[sampleBuffer.Length];
                    for (int i = 0; i < FrameLength; i++)
                    {
                        pcmBuffer[i] = (short)Math.Floor(sampleBuffer[i] * short.MaxValue);
                    }

                    // raise buffer event
                    if (m_transmit)
                        OnFrameCaptured?.Invoke(pcmBuffer);
                }
                else
                {
                    if (m_didDetect)
                    {
                        OnRecordingStop?.Invoke();
                        m_didDetect = false;
                    }
                }
            }


            OnRecordingStop?.Invoke();
            RestartRecording?.Invoke();
        }
    }
}
