using System;
using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using UnityEngine;
using Logger = RooseLabs.Core.Logger;

namespace RooseLabs
{
    public class SoundManager : MonoBehaviour
    {
        private static Logger Logger => Logger.GetLogger("SoundManager");

        public static SoundManager Instance { get; private set; }

        [Header("Detection")]
        [Tooltip("Layer mask to include possible listeners (players/AI). Should include listener colliders.")]
        public LayerMask listenerOverlapMask = ~0;

        [Tooltip("Optional layer mask for occluders (walls, environment). Raycast hits in this mask will count as blocking.")]
        public LayerMask occlusionMask;

        // Debug visualization
        private readonly List<ActiveSound> _activeSounds = new List<ActiveSound>();

        private void Awake()
        {
            Instance = this;
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
        /// Emit a sound on the server at 'position' using the provided SoundType.
        /// This performs overlap detection and notifies listeners that implement ISoundListener.
        /// </summary>
        public void EmitSound(SoundType soundType, Vector3 position, MonoBehaviour sourceEmitter = null)
        {
            if (soundType == null) return;

            _activeSounds.Add(new ActiveSound()
            {
                position = position,
                radius = soundType.radius,
                startTime = Time.time,
                duration = soundType.duration,
                type = soundType
            });

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
                    Logger.Info($"Notifying '{c.name}' with intensity {maxIntensity:F2} (isItemDrop: {isItemDrop})");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Exception when notifying listener '{c.name}': {ex}");
                }
            }
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
                    Logger.Error($"Exception when notifying listener '{c.name}' about voice: {ex}");
                }
            }
        }


        private void Update()
        {
            if (_activeSounds.Count == 0) return;

            float now = Time.time;
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                if (now - _activeSounds[i].startTime > _activeSounds[i].duration)
                    _activeSounds.RemoveAt(i);
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
