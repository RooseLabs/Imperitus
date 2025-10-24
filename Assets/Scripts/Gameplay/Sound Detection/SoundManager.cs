using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs
{
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Detection")]
        [Tooltip("Layer mask to include possible listeners (players/AI). Should include listener colliders.")]
        public LayerMask listenerOverlapMask = ~0;

        [Tooltip("Optional layer mask for occluders (walls, environment). Raycast hits in this mask will count as blocking.")]
        public LayerMask occlusionMask;

        [Tooltip("Minimum intensity threshold to notify a listener (0..1).")]
        [Range(0f, 1f)]
        public float minIntensityToNotify = 0.05f;

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

            // Register for debug visualization
            _activeSounds.Add(new ActiveSound()
            {
                position = position,
                radius = soundType.radius * Mathf.Max(0.0001f, soundType.volume),
                startTime = Time.time,
                duration = soundType.duration,
                type = soundType
            });

            float effectiveRadius = soundType.radius * soundType.volume;
            Collider[] hits = Physics.OverlapSphere(position, effectiveRadius, listenerOverlapMask, QueryTriggerInteraction.Collide);

            foreach (Collider c in hits)
            {
                if (c == null) continue;

                ISoundListener listener = c.GetComponentInParent<ISoundListener>() as ISoundListener;
                if (listener == null) continue;

                // Sample multiple points along the collider to improve detection
                Vector3[] sampleOffsets = { Vector3.up * 0.5f, Vector3.zero, Vector3.down * 0.5f };
                float maxIntensity = 0f;

                foreach (var offset in sampleOffsets)
                {
                    Vector3 samplePoint = c.ClosestPoint(position) + offset;
                    float distance = Vector3.Distance(position, samplePoint);
                    float normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(0.0001f, effectiveRadius));
                    float intensity = 1f - (normalizedDistance * normalizedDistance); // quadratic falloff

                    // Occlusion check
                    if (occlusionMask != 0)
                    {
                        Vector3 dir = (samplePoint - position).normalized;
                        float rayDist = distance - 0.05f;

                        if (rayDist > 0f && Physics.Raycast(position, dir, out RaycastHit hitInfo, rayDist, occlusionMask, QueryTriggerInteraction.Ignore))
                        {
                            // Soft occlusion: reduce intensity instead of zeroing
                            intensity *= 0.2f;
                            Debug.DrawLine(position, hitInfo.point, Color.red, 0.1f);
                        }
                        else
                        {
                            Debug.DrawLine(position, samplePoint, Color.green, 0.1f);
                        }
                    }

                    maxIntensity = Mathf.Max(maxIntensity, intensity);
                }

                if (maxIntensity >= minIntensityToNotify)
                {
                    try
                    {
                        listener.OnSoundHeard(position, soundType, maxIntensity);
                        Debug.Log($"[SoundManager] Notifying '{c.name}' with intensity {maxIntensity}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Exception when notifying listener '{c.name}': {ex}");
                    }
                }
                else
                {
                    Debug.Log($"[SoundManager] Listener '{c.name}' ignored, max intensity {maxIntensity} below threshold {minIntensityToNotify}");
                }
            }
        }



        private void Update()
        {
            // Clean up expired active sounds
            if (_activeSounds.Count == 0) return;

            float now = Time.time;
            for (int i = _activeSounds.Count - 1; i >= 0; i--)
            {
                if (now - _activeSounds[i].startTime > _activeSounds[i].duration)
                    _activeSounds.RemoveAt(i);
            }
        }

#if UNITY_EDITOR
        // Editor/debug-only. Draw spheres for active sounds with fade-out alpha.
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

                // small filled sphere at origin for visibility
                Color c2 = Color.red;
                c2.a = alpha;
                Gizmos.color = c2;
                Gizmos.DrawSphere(s.position, 0.05f);
            }
        }
#endif
    }
}
