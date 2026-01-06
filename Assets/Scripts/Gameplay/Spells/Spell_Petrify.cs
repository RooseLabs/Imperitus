using System.Collections.Generic;
using FishNet.Object;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    public class Petrify : SpellBase
    {
        #region Serialized
        [Header("Petrify Spell Data")]
        [SerializeField] private Animator snakeAnimator;
        [Tooltip("Name of the animator state that contains the snake animation.")]
        [SerializeField] private string snakeAnimatorStateName = "SnakeAnimation";
        [Tooltip("Number of frames (inclusive) to scrub during the cast.")]
        [SerializeField] private int snakeScrubFrames = 115;
        [Tooltip("How long (seconds) the reverse animation plays when cancelling a cast.")]
        [SerializeField] private float reverseAnimDuration = 0.75f;

        [Tooltip("Transform from which the beam is shot.")]
        [SerializeField] private Transform beamShootTransform;
        [Tooltip("Maximum range of the beam in world units.")]
        [SerializeField] private float beamMaxRange = 20f;
        [Tooltip("How long the beam visual remains visible after firing (seconds).")]
        [SerializeField] private float beamDuration = 1f;
        [Tooltip("How long targets remain petrified (seconds).")]
        [SerializeField] private float petrifyDuration = 5f;

        [Header("Beam Visual")]
        [SerializeField] private GameObject beamObject;
        [SerializeField] private GameObject particles;
        #endregion

        private enum PetrifySpellCastingState
        {
            Casting = 0,
            CastFinished = 1,
            Reversing = 2
        }

        private PetrifySpellCastingState m_currentState;
        private float m_stateElapsed;

        private float m_currentScrubNormalized;
        private int m_snakeStateHash = -1;
        private bool m_hasPlayedPastScrub;
        private float m_fireNormalized = 1f;

        private bool m_isBeamActive;
        private readonly HashSet<Collider> m_petrifiedTargets = new();

        // Reverse playback
        private bool m_reverseRunning;
        private float m_reverseDuration;
        private float m_reverseElapsed;
        private float m_reverseFromNormalized;

        private void Awake()
        {
            if (!beamObject) this.LogWarning("No beamObject assigned in inspector.");

            m_snakeStateHash = Animator.StringToHash(snakeAnimatorStateName);
            if (!snakeAnimator || !snakeAnimator.runtimeAnimatorController) return;

            snakeAnimator.speed = 0f;
            foreach (var clip in snakeAnimator.runtimeAnimatorController.animationClips)
            {
                if (!clip) continue;
                if (clip.name.ToLower().Contains(snakeAnimatorStateName.ToLower()))
                {
                    int totalFrames = (int)(clip.length * clip.frameRate);
                    float frameIndex = Mathf.Clamp(snakeScrubFrames - 1f, 0f, totalFrames - 1f);
                    m_fireNormalized = Mathf.Clamp01(frameIndex / (totalFrames - 1f));
                    break;
                }
            }
        }

        protected override void OnStartCast()
        {
            SetState(PetrifySpellCastingState.Casting);
        }

        protected override void OnContinueCast()
        {
            float normalizedTarget = (CastTime > 0f) ? Mathf.Clamp01(CastProgress / CastTime) : 1f;
            PlaySnakeAnimation(normalizedTarget * m_fireNormalized);
        }

        protected override bool OnCastFinished()
        {
            SetState(PetrifySpellCastingState.CastFinished);
            return true;
        }

        protected override void OnCancelCast()
        {
            SetState(PetrifySpellCastingState.Reversing);
        }

        protected override void OnStopAim()
        {
            SetState(PetrifySpellCastingState.Reversing);
        }

        private void Update()
        {
            // State machine
            m_stateElapsed += Time.deltaTime;
            switch (m_currentState)
            {
                case PetrifySpellCastingState.Casting:
                    if (IsOwner) break;
                    // Observers simulate cast progress
                    float castProgress = (CastTime > 0f) ? Mathf.Clamp01(m_stateElapsed / CastTime) : 1f;
                    float targetNormalized = castProgress * m_fireNormalized;
                    PlaySnakeAnimation(targetNormalized);
                    break;
                case PetrifySpellCastingState.CastFinished:
                    if (m_isBeamActive) DoBeamRaycast();
                    if (m_stateElapsed >= beamDuration)
                    {
                        StopBeam();
                        // After stopping the beam, transition to reversing state
                        SetState(PetrifySpellCastingState.Reversing);
                    }
                    break;
                case PetrifySpellCastingState.Reversing:
                    if (m_reverseRunning)
                    {
                        m_reverseElapsed += Time.deltaTime;
                        if (m_reverseDuration <= 0f)
                        {
                            PlaySnakeAnimation(0f);
                            m_reverseRunning = false;
                        }
                        else
                        {
                            float t = Mathf.Clamp01(m_reverseElapsed / m_reverseDuration);
                            float normalized = Mathf.Lerp(m_reverseFromNormalized, 0f, t);
                            PlaySnakeAnimation(normalized);
                            if (t >= 1f)
                            {
                                m_reverseRunning = false;
                            }
                        }
                    }
                    break;
            }
        }

        private void ApplyPetrifyEffect(Collider hitCollider, Vector3 hitPoint)
        {
            if (!hitCollider) return;
            // Avoid applying to the same collider repeatedly
            if (!m_petrifiedTargets.Add(hitCollider)) return;

            // Try to find IPetrifiable on the hit object or its parents
            IPetrifiable petrifiable = hitCollider.GetComponentInParent<IPetrifiable>();
            if (petrifiable != null)
            {
                petrifiable.Petrify(petrifyDuration);
                this.LogInfo($"[Petrify] Applied petrify to {hitCollider.name} for {petrifyDuration}s");
            }
            else
            {
                this.LogInfo($"[Petrify] Hit {hitCollider.name} but target is not petrifiable (immune or not implemented)");
            }
        }

        private void PlaySnakeAnimation(float normalized, float speed = 0f)
        {
            snakeAnimator.Play(m_snakeStateHash, 0, normalized);
            snakeAnimator.speed = speed;
            m_currentScrubNormalized = normalized;
        }

        private void StartBeam()
        {
            if (!beamObject) return;
            beamObject.SetActive(true);
            m_isBeamActive = true;
            DoBeamRaycast();
        }

        private void StopBeam()
        {
            m_isBeamActive = false;
            beamObject?.SetActive(false);
            m_petrifiedTargets.Clear();
        }

        private void UpdateBeam(Vector3 hitPoint)
        {
            if (!beamObject) return;
            beamObject.transform.position = beamShootTransform.position;
            beamObject.transform.LookAt(hitPoint);
            Vector3 localScale = beamObject.transform.localScale;
            localScale.z = Vector3.Distance(beamObject.transform.position, hitPoint) + 0.1f; // Penetrate slightly
            beamObject.transform.localScale = localScale;
        }

        private void DoBeamRaycast()
        {
            Vector3 origin;
            Vector3 dir;
            if (IsOwner)
            {
                origin = CasterCharacter.Camera.transform.position;
                dir = CasterCharacter.Data.lookDirection;
            }
            else
            {
                origin = CasterCharacter.ModelTransform.position;
                origin.y = beamObject.transform.position.y;
                dir = CasterCharacter.ModelTransform.forward;
            }
            if (CasterCharacter.RaycastIgnoreSelf(origin, dir, out RaycastHit hitInfo,
                    beamMaxRange, HelperFunctions.AllPhysicalLayerMask))
            {
                UpdateBeam(hitInfo.point);
                if (IsServerInitialized)
                {
                    ApplyPetrifyEffect(hitInfo.collider, hitInfo.point);
                }
            }
            else
            {
                UpdateBeam(origin + dir * beamMaxRange);
            }
        }

        private void ToggleParticles(bool enable)
        {
            if (!particles) return;
            if (enable)
            {
                // Restart particles by toggling off then on
                particles.SetActive(false);
                particles.SetActive(true);
            }
            else
            {
                particles.SetActive(false);
            }
        }

        private void SetState(PetrifySpellCastingState newState)
        {
            if (newState == m_currentState) return;
            if (IsServerInitialized)
            {
                SetState_ObserversRpc((int)newState);
            }
            else
            {
                SetState_ServerRpc((int)newState);
                SetState_Internal(newState);
            }
        }

        private void SetState_Internal(PetrifySpellCastingState newState)
        {
            switch (newState)
            {
                case PetrifySpellCastingState.Casting:
                    PlaySnakeAnimation(0f);
                    break;
                case PetrifySpellCastingState.CastFinished:
                    // Mark that we've entered the post-scrub playback segment
                    m_hasPlayedPastScrub = true;

                    // Force animator to the exact scrub-end normalized marker and resume playback
                    PlaySnakeAnimation(m_fireNormalized, 1f);

                    StartBeam();
                    ToggleParticles(true);

                    m_reverseRunning = false;
                    break;
                case PetrifySpellCastingState.Reversing:
                    // Stop any ongoing post-fire behaviour
                    // If we've already progressed past the scrub frame, snap back to scrub-end before reversing
                    if (m_hasPlayedPastScrub) PlaySnakeAnimation(m_fireNormalized);

                    // Initialize reverse playback parameters
                    float inferredIndex = m_currentScrubNormalized * ((snakeScrubFrames - 1f) / m_fireNormalized);
                    float currentFrameNumber = Mathf.Clamp(inferredIndex + 1f, 0f, snakeScrubFrames);
                    float ratio = Mathf.Clamp01(currentFrameNumber / Mathf.Max(1f, snakeScrubFrames));
                    m_reverseDuration = reverseAnimDuration * ratio;
                    m_reverseFromNormalized = m_currentScrubNormalized;
                    m_reverseElapsed = 0f;
                    m_reverseRunning = true;

                    ToggleParticles(false);
                    StopBeam();
                    m_hasPlayedPastScrub = false;
                    break;
            }
            m_currentState = newState;
            m_stateElapsed = 0f;
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetState_ServerRpc(int state)
        {
            SetState_ObserversRpc(state);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void SetState_ObserversRpc(int state)
        {
            SetState_Internal((PetrifySpellCastingState)state);
        }
    }
}
