using System;
using System.Collections;
using RooseLabs.Settings;
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
                return Microphone.devices[CurrentDeviceIndex];
            }
        }

        private AudioClip m_audioClip;
        private event Action RestartRecording;
        private MicrophoneDeviceSetting m_microphoneDeviceSetting;

        private void OnEnable()
        {
            CurrentDeviceIndex = -1;
            m_microphoneDeviceSetting = SettingsHandler.GetSetting<MicrophoneDeviceSetting>();
            m_microphoneDeviceSetting.OnSettingChanged += OnMicrophoneDeviceChanged;
        }

        private void OnDisable()
        {
            if (m_microphoneDeviceSetting != null)
            {
                m_microphoneDeviceSetting.OnSettingChanged -= OnMicrophoneDeviceChanged;
            }
        }

        private void OnMicrophoneDeviceChanged(int newDeviceIndex)
        {
            ChangeDevice(newDeviceIndex);
        }

        /// <summary>
        /// Change audio recording device
        /// </summary>
        /// <param name="deviceIndex">Index of the new audio capture device</param>
        public void ChangeDevice(int deviceIndex)
        {
            // Fallback to device 0 if out of bounds
            if (deviceIndex < 0 || deviceIndex >= Microphone.devices.Length)
            {
                Logger.Warning($"Specified device index {deviceIndex} is not a valid recording device, falling back to device 0");
                deviceIndex = 0;
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
        public void StartRecording(int sampleRate = 16000, int frameSize = 512)
        {
            if (IsRecording)
            {
                // if sample rate or frame size have changed, restart recording
                if (sampleRate != SampleRate || frameSize != FrameLength)
                {
                    RestartRecording += () =>
                    {
                        StartRecording(sampleRate, frameSize);
                        RestartRecording = null;
                    };
                    StopRecording();
                }

                return;
            }

            SampleRate = sampleRate;
            FrameLength = frameSize;

            // Initialize device from settings if not already set
            if (CurrentDeviceIndex < 0 && m_microphoneDeviceSetting != null)
            {
                int settingsDeviceIndex = m_microphoneDeviceSetting.GetValue();
                // Validate bounds
                if (settingsDeviceIndex >= 0 && settingsDeviceIndex < Microphone.devices.Length)
                {
                    CurrentDeviceIndex = settingsDeviceIndex;
                }
                else
                {
                    CurrentDeviceIndex = 0;
                }
            }

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

            StopCoroutine(RecordData());
        }

        /// <summary>
        /// Loop for buffering incoming audio data and delivering frames
        /// </summary>
        private IEnumerator RecordData()
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
                    Buffer.BlockCopy(endClipSamples, 0, sampleBuffer, 0, numSamplesClipEnd * sizeof(float));
                    Buffer.BlockCopy(startClipSamples, 0, sampleBuffer, numSamplesClipEnd * sizeof(float), numSamplesClipStart * sizeof(float));
                }
                else
                {
                    m_audioClip.GetData(sampleBuffer, startReadPos);
                }

                startReadPos = endReadPos % m_audioClip.samples;

                // converts to 16-bit int samples
                short[] pcmBuffer = new short[sampleBuffer.Length];
                for (int i = 0; i < FrameLength; i++)
                {
                    pcmBuffer[i] = (short)Math.Floor(sampleBuffer[i] * short.MaxValue);
                }

                // raise buffer event
                OnFrameCaptured?.Invoke(pcmBuffer);

                yield return null;
            }

            OnRecordingStop?.Invoke();
            RestartRecording?.Invoke();
        }
    }
}
