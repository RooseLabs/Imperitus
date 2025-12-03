using System;
using System.Collections.Generic;
using FishNet.Object;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs
{
    [RequireComponent(typeof(NetworkObject))]
    public class SoundManager : NetworkBehaviour
    {
        private static Logger Logger => Logger.GetLogger("SoundManager");

        public static SoundManager Instance { get; private set; }

        [Header("Detection")]
        [Tooltip("Layer mask to include possible listeners (players/AI). Should include listener colliders.")]
        public LayerMask listenerOverlapMask = ~0;

        [Tooltip("Optional layer mask for occluders (walls, environment). Raycast hits in this mask will count as blocking.")]
        public LayerMask occlusionMask;

        [Header("Audio Playback")]
        [Tooltip("Initial pool size for 3D AudioSources")]
        public int audioSourcePoolSize = 20;

        [Tooltip("Tag used to identify the local player (e.g., 'Player'). Used to find the correct AudioListener.")]
        public string localPlayerTag = "Player";

        [Tooltip("2D AudioSource for UI/non-spatial sounds. Created automatically if null.")]
        public AudioSource audioSource2D;

        // Cache for local player's AudioListener (found dynamically)
        private AudioListener _cachedAudioListener;
        private float _lastListenerSearchTime;
        private const float LISTENER_SEARCH_INTERVAL = 0.5f; // Search every 0.5s if not found

        [Header("Database Reference")]
        [Tooltip("Reference to SoundTypeDatabase. MUST be assigned in inspector!")]
        public SoundTypeDatabase soundDatabase;

        // Audio source pooling
        private Queue<PooledAudioSource> _availableAudioSources = new Queue<PooledAudioSource>();
        private List<PooledAudioSource> _allAudioSources = new List<PooledAudioSource>();

        // Debug visualization
        private readonly List<ActiveSound> _activeSounds = new List<ActiveSound>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Validate database reference
            if (soundDatabase == null)
            {
                Debug.LogError("SoundTypeDatabase is not assigned! Please assign it in the inspector.");
                return;
            }

            // Initialize database
            soundDatabase.Initialize();

            InitializeAudioSystem();
        }

        /// <summary>
        /// Initialize audio source pool and 2D audio source
        /// </summary>
        private void InitializeAudioSystem()
        {
            // Create 2D AudioSource for UI sounds
            if (audioSource2D == null)
            {
                GameObject audio2DObj = new GameObject("AudioSource2D");
                audio2DObj.transform.SetParent(transform);
                audioSource2D = audio2DObj.AddComponent<AudioSource>();
                audioSource2D.spatialBlend = 0f; // 2D
                audioSource2D.playOnAwake = false;
            }

            // Create audio source pool
            for (int i = 0; i < audioSourcePoolSize; i++)
            {
                CreatePooledAudioSource();
            }

            Debug.Log($"Audio system initialized with {audioSourcePoolSize} pooled AudioSources");
        }

        /// <summary>
        /// Get the local player's AudioListener. Searches dynamically and caches result.
        /// </summary>
        private AudioListener GetLocalAudioListener()
        {
            // Return cached listener if still valid
            if (_cachedAudioListener != null && _cachedAudioListener.enabled)
                return _cachedAudioListener;

            // Throttle search to avoid performance issues
            if (Time.time - _lastListenerSearchTime < LISTENER_SEARCH_INTERVAL)
                return _cachedAudioListener; // Return even if null to avoid spam

            _lastListenerSearchTime = Time.time;

            // Try to find local player's AudioListener
            AudioListener[] allListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

            foreach (AudioListener listener in allListeners)
            {
                // Check if this listener belongs to the local player
                // Option 1: Check if it's owned by this client (FishNet)
                NetworkObject netObj = listener.GetComponentInParent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    _cachedAudioListener = listener;
                    Debug.Log($"Found local player's AudioListener on '{listener.gameObject.name}'");
                    return _cachedAudioListener;
                }

                // Option 2: Fallback - check by tag if no NetworkObject found
                if (!string.IsNullOrEmpty(localPlayerTag) && listener.CompareTag(localPlayerTag))
                {
                    _cachedAudioListener = listener;
                    Debug.Log($"Found AudioListener by tag '{localPlayerTag}' on '{listener.gameObject.name}'");
                    return _cachedAudioListener;
                }
            }

            // Option 3: Ultimate fallback - just use any active listener
            if (allListeners.Length > 0)
            {
                _cachedAudioListener = allListeners[0];
                Debug.LogWarning($"Using first available AudioListener on '{_cachedAudioListener.gameObject.name}' (could not determine local player)");
                return _cachedAudioListener;
            }

            // No listener found at all
            if (_cachedAudioListener == null)
            {
                Debug.LogWarning("No AudioListener found. 3D audio occlusion will not work.");
            }

            return _cachedAudioListener;
        }

        /// <summary>
        /// Create a new pooled audio source
        /// </summary>
        private PooledAudioSource CreatePooledAudioSource()
        {
            GameObject audioObj = new GameObject($"PooledAudioSource_{_allAudioSources.Count}");
            audioObj.transform.SetParent(transform);

            AudioSource source = audioObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f; // 3D by default

            // Add low-pass filter for occlusion
            AudioLowPassFilter lowPass = audioObj.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f; // Max frequency (no filtering)

            PooledAudioSource pooled = new PooledAudioSource
            {
                audioSource = source,
                lowPassFilter = lowPass,
                gameObject = audioObj
            };

            _allAudioSources.Add(pooled);
            _availableAudioSources.Enqueue(pooled);

            return pooled;
        }

        /// <summary>
        /// Get an available audio source from the pool
        /// </summary>
        private PooledAudioSource GetAudioSource()
        {
            if (_availableAudioSources.Count == 0)
            {
                // Pool exhausted, create a new one
                Debug.LogWarning("AudioSource pool exhausted, creating additional source");
                return CreatePooledAudioSource();
            }

            return _availableAudioSources.Dequeue();
        }

        /// <summary>
        /// Return an audio source to the pool
        /// </summary>
        private void ReturnAudioSource(PooledAudioSource pooled)
        {
            if (pooled == null || pooled.audioSource == null) return;

            pooled.audioSource.Stop();
            pooled.audioSource.clip = null;
            pooled.lowPassFilter.cutoffFrequency = 22000f;
            pooled.isPlaying = false;

            _availableAudioSources.Enqueue(pooled);
        }

        /// <summary>
        /// Represents a currently active sound for debugging/fade visualization.
        /// </summary>
        private class ActiveSound
        {
            public Vector3 position;
            public float radius;
            public float startTime;
            public float duration;
            public SoundType type;
        }

        /// <summary>
        /// Pooled audio source container
        /// </summary>
        private class PooledAudioSource
        {
            public AudioSource audioSource;
            public AudioLowPassFilter lowPassFilter;
            public GameObject gameObject;
            public bool isPlaying;
            public float playStartTime;
        }

        /// <summary>
        /// Emit a sound on the server at 'position' using the provided SoundType.
        /// This performs overlap detection and notifies listeners that implement ISoundListener.
        /// </summary>
        public void EmitSound(SoundType soundType, Vector3 position, MonoBehaviour sourceEmitter = null)
        {
            if (soundType == null) return;

            // Add to debug visualization
            _activeSounds.Add(new ActiveSound()
            {
                position = position,
                radius = soundType.radius,
                startTime = Time.time,
                duration = soundType.duration,
                type = soundType
            });

            // AUDIO PLAYBACK: Play sound locally and sync to clients
            if (IsServerInitialized)
            {
                // Play on server/host
                PlaySoundLocal(soundType, position);

                // Sync to all clients via RPC
                if (soundDatabase != null)
                {
                    int soundIndex = soundDatabase.GetIndex(soundType);
                    if (soundIndex >= 0)
                    {
                        ObserversPlaySound(soundIndex, position);
                    }
                    else
                    {
                        Debug.LogWarning($"Sound type '{soundType.key}' not found in database!");
                    }
                }
            }
            else
            {
                // Client-only (shouldn't happen normally, but fallback)
                PlaySoundLocal(soundType, position);
            }

            // GAMEPLAY DETECTION: Notify ISoundListeners
            float effectiveRadius = Mathf.Max(0.01f, soundType.radius);
            Collider[] hits = Physics.OverlapSphere(position, effectiveRadius, listenerOverlapMask, QueryTriggerInteraction.Collide);

            bool isItemDrop = soundType.key == "ItemDropped";

            foreach (Collider c in hits)
            {
                if (c == null) continue;

                ISoundListener listener = c.GetComponentInParent<ISoundListener>();
                if (listener == null) continue;

                Vector3[] sampleOffsets = { Vector3.up * 0.5f, Vector3.zero, Vector3.down * 0.5f };
                float maxIntensity = 0f;

                foreach (var offset in sampleOffsets)
                {
                    Vector3 samplePoint = c.ClosestPoint(position) + offset;
                    float distance = Vector3.Distance(position, samplePoint);

                    // Exponential falloff
                    float intensity = Mathf.Exp(-distance / soundType.radius);

                    if (!isItemDrop && occlusionMask != 0)
                    {
                        Vector3 dir = (samplePoint - position).normalized;
                        float rayDist = distance - 0.05f;

                        if (rayDist > 0f)
                        {
                            RaycastHit[] hitsInfo = Physics.RaycastAll(position, dir, rayDist, occlusionMask, QueryTriggerInteraction.Collide);
                            float blockedDistance = 0f;

                            foreach (var hit in hitsInfo)
                                blockedDistance += hit.distance;

                            // Reduce intensity based on total distance through obstacles
                            float occlusionFactor = Mathf.Exp(-blockedDistance / (soundType.radius * 0.5f));
                            intensity *= occlusionFactor;

                            // Debug lines
                            if (hitsInfo.Length > 0)
                                Debug.DrawLine(position, hitsInfo[hitsInfo.Length - 1].point, Color.red, 1.5f);
                            else
                                Debug.DrawLine(position, samplePoint, Color.green, 1.5f);
                        }
                    }

                    maxIntensity = Mathf.Max(maxIntensity, intensity);
                }

                try
                {
                    listener.OnSoundHeard(position, soundType, maxIntensity);
                    Debug.Log($"Notifying '{c.name}' with intensity {maxIntensity:F2} (isItemDrop: {isItemDrop})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception when notifying listener '{c.name}': {ex}");
                }
            }
        }

        /// <summary>
        /// Network RPC to play sound on all observers (clients only, not host)
        /// </summary>
        [ObserversRpc(BufferLast = false)]
        private void ObserversPlaySound(int soundTypeIndex, Vector3 position)
        {
            // Skip on server since it already played locally
            if (IsServerInitialized) return;

            if (soundDatabase == null)
            {
                Debug.LogError("SoundTypeDatabase is null on client!");
                return;
            }

            SoundType soundType = soundDatabase.GetByIndex(soundTypeIndex);
            if (soundType != null)
            {
                PlaySoundLocal(soundType, position);
            }
            else
            {
                Debug.LogWarning($"Client could not find sound at index {soundTypeIndex}");
            }
        }

        /// <summary>
        /// Play sound locally with occlusion and 3D spatialization
        /// </summary>
        private void PlaySoundLocal(SoundType soundType, Vector3 position)
        {
            if (soundType == null)
            {
                Debug.LogWarning("PlaySoundLocal called with null soundType");
                return;
            }

            AudioClip clip = soundType.GetRandomClip();
            if (clip == null)
            {
                Debug.LogWarning($"No audio clip found for sound type '{soundType.key}'");
                return;
            }

            // Handle 2D sounds separately
            if (!soundType.is3D)
            {
                Play2DSound(soundType, clip);
                return;
            }

            // Get pooled audio source
            PooledAudioSource pooled = GetAudioSource();
            if (pooled == null || pooled.audioSource == null)
            {
                Debug.LogError("Failed to get pooled audio source!");
                return;
            }

            AudioSource source = pooled.audioSource;

            // Position the audio source
            pooled.gameObject.transform.position = position;

            // Configure audio source from SoundType
            source.clip = clip;
            source.volume = soundType.volume;
            source.pitch = 1f + UnityEngine.Random.Range(-soundType.pitchVariation, soundType.pitchVariation);
            source.spatialBlend = soundType.spatialBlend;
            source.dopplerLevel = soundType.dopplerLevel;
            source.rolloffMode = soundType.rolloffMode;
            source.minDistance = soundType.minDistance;
            source.maxDistance = soundType.maxDistance;

            if (soundType.rolloffMode == AudioRolloffMode.Custom && soundType.customRolloff != null)
            {
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, soundType.customRolloff);
            }

            if (soundType.mixerGroup != null)
            {
                source.outputAudioMixerGroup = soundType.mixerGroup;
            }

            // Apply occlusion if enabled and listener exists
            if (soundType.enableOcclusion)
            {
                AudioListener listener = GetLocalAudioListener();
                if (listener != null)
                {
                    ApplyOcclusion(pooled, position, soundType, listener);
                }
                else
                {
                    // No listener yet - reset filter
                    pooled.lowPassFilter.cutoffFrequency = 22000f;
                }
            }
            else
            {
                // No occlusion - reset filter
                pooled.lowPassFilter.cutoffFrequency = 22000f;
            }

            // Play the sound
            source.Play();
            pooled.isPlaying = true;
            pooled.playStartTime = Time.time;

            //Debug.Log($"Playing 3D sound '{soundType.key}' at {position}");
        }

        /// <summary>
        /// Play a 2D sound (UI, music, etc.)
        /// </summary>
        private void Play2DSound(SoundType soundType, AudioClip clip)
        {
            if (audioSource2D == null)
            {
                Debug.LogWarning("audioSource2D is null!");
                return;
            }

            audioSource2D.pitch = 1f + UnityEngine.Random.Range(-soundType.pitchVariation, soundType.pitchVariation);
            audioSource2D.volume = soundType.volume;

            if (soundType.mixerGroup != null)
            {
                audioSource2D.outputAudioMixerGroup = soundType.mixerGroup;
            }

            audioSource2D.PlayOneShot(clip);

            Debug.Log($"Playing 2D sound '{soundType.key}'");
        }

        /// <summary>
        /// Apply occlusion effects (low-pass filter and volume reduction)
        /// </summary>
        private void ApplyOcclusion(PooledAudioSource pooled, Vector3 soundPosition, SoundType soundType, AudioListener listener)
        {
            if (listener == null) return;

            Vector3 listenerPos = listener.transform.position;
            Vector3 direction = (listenerPos - soundPosition).normalized;
            float distance = Vector3.Distance(soundPosition, listenerPos);

            if (distance < 0.1f || occlusionMask == 0)
            {
                // Too close or no occlusion mask - no occlusion
                pooled.lowPassFilter.cutoffFrequency = 22000f;
                return;
            }

            // Raycast to check for occlusion
            RaycastHit[] hits = Physics.RaycastAll(soundPosition, direction, distance, occlusionMask, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0)
            {
                // No occlusion - clear sound
                pooled.lowPassFilter.cutoffFrequency = 22000f;
                return;
            }

            // Calculate total distance through obstacles
            float blockedDistance = 0f;
            foreach (var hit in hits)
            {
                blockedDistance += hit.distance * 0.5f; // Approximate thickness
            }

            // Calculate occlusion factor (0 = fully occluded, 1 = clear)
            float occlusionFactor = Mathf.Exp(-blockedDistance / (soundType.radius * 0.5f));
            occlusionFactor = Mathf.Clamp01(occlusionFactor);

            // Apply low-pass filter
            float targetCutoff = Mathf.Lerp(soundType.occludedCutoffFrequency, 22000f, occlusionFactor);
            pooled.lowPassFilter.cutoffFrequency = targetCutoff;

            // Apply volume reduction
            float volumeMultiplier = Mathf.Lerp(soundType.occludedVolumeMultiplier, 1f, occlusionFactor);
            pooled.audioSource.volume = soundType.volume * volumeMultiplier;
        }

        /// <summary>
        /// Emit voice sound detection. Called by VoiceSoundEmitter.
        /// </summary>
        public void EmitVoiceSound(Vector3 position, float radius, MonoBehaviour sourceEmitter = null)
        {
            float effectiveRadius = Mathf.Max(0.01f, radius);
            Collider[] hits = Physics.OverlapSphere(position, effectiveRadius, listenerOverlapMask, QueryTriggerInteraction.Collide);

            foreach (Collider c in hits)
            {
                if (c == null) continue;

                ISoundListener listener = c.GetComponentInParent<ISoundListener>();
                if (listener == null) continue;

                Vector3 samplePoint = c.ClosestPoint(position);
                float distance = Vector3.Distance(position, samplePoint);

                // Exponential falloff
                float intensity = Mathf.Exp(-distance / radius);

                if (occlusionMask != 0)
                {
                    Vector3 dir = (samplePoint - position).normalized;
                    float rayDist = distance - 0.05f;

                    if (rayDist > 0f)
                    {
                        RaycastHit[] hitsInfo = Physics.RaycastAll(position, dir, rayDist, occlusionMask, QueryTriggerInteraction.Collide);
                        float blockedDistance = 0f;

                        foreach (var hit in hitsInfo)
                            blockedDistance += hit.distance;

                        float occlusionFactor = Mathf.Exp(-blockedDistance / (radius * 0.5f));
                        intensity *= occlusionFactor;
                    }
                }

                try
                {
                    SoundType voiceType = ScriptableObject.CreateInstance<SoundType>();
                    voiceType.key = "Voice";
                    voiceType.radius = radius;

                    listener.OnSoundHeard(position, voiceType, intensity);

                    Destroy(voiceType);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception when notifying listener '{c.name}' about voice: {ex}");
                }
            }
        }

        private void Update()
        {
            // Clean up expired debug visualizations
            if (_activeSounds.Count > 0)
            {
                float now = Time.time;
                for (int i = _activeSounds.Count - 1; i >= 0; i--)
                {
                    if (now - _activeSounds[i].startTime > _activeSounds[i].duration)
                        _activeSounds.RemoveAt(i);
                }
            }

            // Return finished audio sources to pool
            for (int i = 0; i < _allAudioSources.Count; i++)
            {
                PooledAudioSource pooled = _allAudioSources[i];
                if (pooled.isPlaying && pooled.audioSource != null && !pooled.audioSource.isPlaying)
                {
                    ReturnAudioSource(pooled);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_activeSounds == null || _activeSounds.Count == 0) return;

            foreach (var s in _activeSounds)
            {
                float elapsed = Time.time - s.startTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, s.duration));
                float alpha = 1f - t;
                Color col = Color.yellow;
                col.a = alpha * 0.6f;
                Gizmos.color = col;
                Gizmos.DrawWireSphere(s.position, s.radius);

                Color c2 = Color.red;
                c2.a = alpha;
                Gizmos.color = c2;
                Gizmos.DrawSphere(s.position, 0.05f);
            }
        }
#endif
    }
}