using System;
using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace RooseLabs.Player
{
    public class PlayerRagdoll : NetworkBehaviour
    {
        private struct BoneTransform
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        private struct NetworkBoneState
        {
            public uint Tick;
            public BoneTransform[] Transforms;
        }

        private PlayerCharacter m_character;
        private Rigidbody m_rigidbody;
        private CapsuleCollider m_collider;
        private Rigidbody[] m_ragdollRigidbodies;
        private Collider[] m_ragdollColliders;
        private Transform[] m_bones;
        private Rigidbody[] m_boneRigidbodies;
        private static bool s_standUpAnimationsSampled;
        private static BoneTransform[] s_faceUpStandUpBoneTransforms;
        private static BoneTransform[] s_faceDownStandUpBoneTransforms;
        private BoneTransform[] m_ragdollBoneTransforms;
        private BoneTransform[] m_interpolatedBoneTransforms;
        private BoneTransform[] m_previousBoneTransforms;

        public readonly Dictionary<HumanBodyBones, Transform> partDict = new();
        public Transform HipsBone => partDict[HumanBodyBones.Hips];
        private Rigidbody HipsRigidbody => m_ragdollRigidbodies[0];
        private Animator Animator => m_character.Animations.Animator;
        private GameObject ModelGameObject => Animator.gameObject;
        private Transform ModelTransform => ModelGameObject.transform;

        private bool m_isFacingUp;
        private bool m_isWaitingForRagdollToSettle;

        private bool m_shouldSyncRagdoll;
        private readonly int m_syncInterval = 1;
        private int m_syncCounter = 0;

        // Interpolation state
        private readonly List<NetworkBoneState> m_boneStateBuffer = new(10);
        private int m_interpolationTicks = 2; // Dynamic delay based on latency
        private const int MaxBufferSize = 10;
        private const float MaxExtrapolationTicks = 1.5f;
        private uint m_lastReceivedTick;

        private void Start()
        {
            m_character = GetComponent<PlayerCharacter>();
            m_rigidbody = GetComponent<Rigidbody>();
            m_collider = GetComponent<CapsuleCollider>();

            // Initialize bone dictionary
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                Transform boneTransform = Animator.GetBoneTransform(bone);
                if (boneTransform != null)
                    partDict[bone] = boneTransform;
            }

            m_ragdollRigidbodies = HipsBone.parent.GetComponentsInChildren<Rigidbody>();
            m_ragdollColliders = HipsBone.parent.GetComponentsInChildren<Collider>();
            m_bones = HipsBone.parent.GetComponentsInChildren<Transform>();

            int boneCount = m_bones.Length;
            m_ragdollBoneTransforms = new BoneTransform[boneCount];
            m_interpolatedBoneTransforms = new BoneTransform[boneCount];
            m_previousBoneTransforms = new BoneTransform[boneCount];

            m_boneRigidbodies = new Rigidbody[boneCount];
            for (int i = 0; i < boneCount; ++i)
            {
                m_boneRigidbodies[i] = m_bones[i].GetComponent<Rigidbody>();
            }

            // Pre-sample stand-up animation transforms
            if (!s_standUpAnimationsSampled)
            {
                s_faceUpStandUpBoneTransforms = new BoneTransform[boneCount];
                s_faceDownStandUpBoneTransforms = new BoneTransform[boneCount];
                PopulateAnimationStartBoneTransforms(s_faceUpStandUpBoneTransforms, true);
                PopulateAnimationStartBoneTransforms(s_faceDownStandUpBoneTransforms, false);
                s_standUpAnimationsSampled = true;
            }
        }

        public override void OnStartServer()
        {
            TimeManager.OnTick += SyncRagdollOnTick;
        }

        public override void OnStopServer()
        {
            TimeManager.OnTick -= SyncRagdollOnTick;
        }

        private void Update()
        {
            // For testing purposes: Trigger ragdoll on aim + drop input
            // if (m_character.Input.aimIsPressed && m_character.Input.dropWasPressed)
            // {
            //     TriggerRagdoll(Vector3.back * 500f, HipsBone.position);
            // }

            // Update interpolation delay based on latency (~once per second)
            if (Time.time % 1f < Time.deltaTime)
            {
                float oneWayLatency = TimeManager.RoundTripTime / 2f;
                m_interpolationTicks = Mathf.Max(2, Mathf.CeilToInt((float)(oneWayLatency / TimeManager.TickDelta)) + 1);
            }
        }

        private void FixedUpdate()
        {
            if (IsServerInitialized) return;
            if (!m_character.Data.IsRagdollActive || m_boneStateBuffer.Count == 0)
                return;

            // Sample previous transforms
            SampleBoneTransforms(m_previousBoneTransforms);

            // Calculate target render tick
            uint targetTick = TimeManager.LocalTick > (uint)m_interpolationTicks ? TimeManager.LocalTick - (uint)m_interpolationTicks : 0;

            int boneCount = m_bones.Length;
            if (m_boneStateBuffer.Count < 2)
            {
                // Underrun: Smoothly lerp to latest state
                var latestState = m_boneStateBuffer[^1];
                BoneTransform[] latest = latestState.Transforms;
                float smoothFactor = Mathf.Clamp01(Time.fixedDeltaTime / 0.1f);
                for (int boneIndex = 0; boneIndex < boneCount; ++boneIndex)
                {
                    m_interpolatedBoneTransforms[boneIndex].Position = Vector3.Lerp(
                        m_previousBoneTransforms[boneIndex].Position,
                        latest[boneIndex].Position,
                        smoothFactor
                    );
                    m_interpolatedBoneTransforms[boneIndex].Rotation = Quaternion.Lerp(
                        m_previousBoneTransforms[boneIndex].Rotation,
                        latest[boneIndex].Rotation,
                        smoothFactor
                    );
                }
                ApplyBoneTransforms(m_interpolatedBoneTransforms);
            }
            else
            {
                // Find bracketing states
                int prevIndex = -1;
                for (int i = 0; i < m_boneStateBuffer.Count - 1; ++i)
                {
                    if (m_boneStateBuffer[i].Tick <= targetTick && targetTick <= m_boneStateBuffer[i + 1].Tick)
                    {
                        prevIndex = i;
                        break;
                    }
                }

                if (prevIndex == -1)
                {
                    // No bracket: Use latest two states
                    prevIndex = m_boneStateBuffer.Count - 2;
                    var prevState = m_boneStateBuffer[prevIndex];
                    var nextState = m_boneStateBuffer[prevIndex + 1];

                    float extrapFrac = (targetTick - nextState.Tick) / (float)(nextState.Tick - prevState.Tick);
                    if (extrapFrac is > 0 and <= MaxExtrapolationTicks)
                    {
                        // Extrapolate based on delta
                        for (int boneIndex = 0; boneIndex < boneCount; ++boneIndex)
                        {
                            Vector3 posDelta = nextState.Transforms[boneIndex].Position - prevState.Transforms[boneIndex].Position;
                            Quaternion rotDelta = nextState.Transforms[boneIndex].Rotation * Quaternion.Inverse(prevState.Transforms[boneIndex].Rotation);

                            m_interpolatedBoneTransforms[boneIndex].Position = nextState.Transforms[boneIndex].Position + posDelta * extrapFrac;
                            m_interpolatedBoneTransforms[boneIndex].Rotation = nextState.Transforms[boneIndex].Rotation * Quaternion.Slerp(Quaternion.identity, rotDelta, extrapFrac);
                        }
                    }
                    else
                    {
                        // Clamp to latest state
                        Array.Copy(nextState.Transforms, m_interpolatedBoneTransforms, boneCount);
                    }
                }
                else
                {
                    // Interpolate between bracketing states
                    var prevState = m_boneStateBuffer[prevIndex];
                    var nextState = m_boneStateBuffer[prevIndex + 1];
                    float frac = (targetTick - prevState.Tick) / (float)(nextState.Tick - prevState.Tick);
                    for (int boneIndex = 0; boneIndex < boneCount; ++boneIndex)
                    {
                        m_interpolatedBoneTransforms[boneIndex].Position = Vector3.Lerp(
                            prevState.Transforms[boneIndex].Position,
                            nextState.Transforms[boneIndex].Position,
                            frac
                        );
                        m_interpolatedBoneTransforms[boneIndex].Rotation = Quaternion.Lerp(
                            prevState.Transforms[boneIndex].Rotation,
                            nextState.Transforms[boneIndex].Rotation,
                            frac
                        );
                    }
                }
                ApplyBoneTransforms(m_interpolatedBoneTransforms);
            }

            // Update previous transforms to current applied state
            SampleBoneTransforms(m_previousBoneTransforms);

            // Clean up old states
            while (m_boneStateBuffer.Count > 2 && m_boneStateBuffer[0].Tick < targetTick - (uint)m_interpolationTicks)
            {
                m_boneStateBuffer.RemoveAt(0);
            }
        }

        private void ApplyBoneTransforms(BoneTransform[] boneTransforms)
        {
            for (int boneIndex = 0; boneIndex < m_bones.Length; ++boneIndex)
            {
                Transform bone = m_bones[boneIndex];
                bone.localPosition = boneTransforms[boneIndex].Position;
                bone.localRotation = boneTransforms[boneIndex].Rotation;

                Rigidbody rb = m_boneRigidbodies[boneIndex];
                if (rb != null)
                {
                    rb.MovePosition(bone.position);
                    rb.MoveRotation(bone.rotation);
                }
            }
        }

        [Server]
        public void TriggerRagdoll(Vector3 force, Vector3 hitPoint, bool shouldStandUp = true)
        {
            StopAllCoroutines();
            if (m_character.Data.IsRagdollActive && m_isWaitingForRagdollToSettle)
            {
                Rigidbody hitRigidbody = FindHitRigidbody(hitPoint);
                hitRigidbody.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
            }
            else
            {
                ToggleRagdoll(true);
                m_character.Data.IsRagdollActive = true;
                TriggerRagdoll_ObserversRPC();

                Rigidbody hitRigidbody = FindHitRigidbody(hitPoint);
                hitRigidbody.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
                m_shouldSyncRagdoll = true;
            }

            if(shouldStandUp)
                StartCoroutine(StandUpRoutine());
        }

        [ObserversRpc(ExcludeServer = true)]
        private void TriggerRagdoll_ObserversRPC()
        {
            m_boneStateBuffer.Clear();
            m_lastReceivedTick = 0;

            m_rigidbody.isKinematic = true;
            m_collider.enabled = false;
            Animator.enabled = false;

            m_character.Data.IsRagdollActive = true;
        }

        private void SyncRagdollOnTick()
        {
            if (!m_shouldSyncRagdoll) return;
            if (++m_syncCounter % m_syncInterval != 0) return;
            SampleBoneTransforms(m_ragdollBoneTransforms);
            SyncRagdoll_ObserversRPC(m_ragdollBoneTransforms, TimeManager.Tick);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void SyncRagdoll_ObserversRPC(BoneTransform[] boneTransforms, uint serverTick, Channel channel = Channel.Unreliable)
        {
            if (serverTick <= m_lastReceivedTick) return;
            m_lastReceivedTick = serverTick;

            var newState = new NetworkBoneState
            {
                Tick = serverTick,
                Transforms = new BoneTransform[m_bones.Length]
            };
            Array.Copy(boneTransforms, newState.Transforms, m_bones.Length);

            m_boneStateBuffer.Add(newState);
            m_boneStateBuffer.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            if (m_boneStateBuffer.Count > MaxBufferSize)
                m_boneStateBuffer.RemoveAt(0);
        }

        private IEnumerator StandUpRoutine()
        {
            yield return WaitForRagdollToSettle();
            m_shouldSyncRagdoll = false;
            StandUp_ObserversRPC();
        }

        [ObserversRpc]
        private void StandUp_ObserversRPC()
        {
            m_isFacingUp = HipsBone.forward.y > 0;
            AlignRotationToHips();
            AlignPositionToHips();
            SampleBoneTransforms(m_ragdollBoneTransforms);
            SetRagdollPhysicsActive(false);

            StartCoroutine(StandUpRoutine_Internal());
            m_boneStateBuffer.Clear();
            m_lastReceivedTick = 0;
        }

        private IEnumerator StandUpRoutine_Internal()
        {
            yield return RealignBonesToStandUpPose();
            ToggleRagdoll(false);
            Animator.Play(PlayerAnimations.S_StandUpFaceDown);
            yield return WaitForStandUpAnimationComplete();
            m_character.Data.IsRagdollActive = false;
        }

        public void ToggleRagdoll(bool enable)
        {
            SetRagdollPhysicsActive(enable);
            m_rigidbody.isKinematic = !IsOwner;
            m_collider.enabled = !enable;
            Animator.enabled = !enable;
        }

        private void SetRagdollPhysicsActive(bool active)
        {
            foreach (var rb in m_ragdollRigidbodies)
                rb.isKinematic = !active;
            foreach (var col in m_ragdollColliders)
                col.isTrigger = !active;
        }

        private IEnumerator WaitForRagdollToSettle()
        {
            m_isWaitingForRagdollToSettle = true;
            const float ragdollTimeout = 5f;
            float startTime = Time.time;
            while (true)
            {
                yield return new WaitForFixedUpdate();
                if (Time.time - startTime > ragdollTimeout)
                    break;
                if (HipsRigidbody.linearVelocity.magnitude < 0.1f && HipsRigidbody.angularVelocity.magnitude < 0.1f)
                    break;
            }
            yield return new WaitForSeconds(1f);
            m_isWaitingForRagdollToSettle = false;
        }

        private IEnumerator RealignBonesToStandUpPose()
        {
            BoneTransform[] standUpBoneTransforms = GetStandUpBoneTransformsStart();
            const float realignDuration = 0.2f;
            float elapsed = 0f;
            while (elapsed < realignDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / realignDuration;
                for (int i = 0; i < m_bones.Length; ++i)
                {
                    m_bones[i].localPosition = Vector3.Lerp(
                        m_ragdollBoneTransforms[i].Position,
                        standUpBoneTransforms[i].Position,
                        progress
                    );
                    m_bones[i].localRotation = Quaternion.Lerp(
                        m_ragdollBoneTransforms[i].Rotation,
                        standUpBoneTransforms[i].Rotation,
                        progress
                    );
                }
                yield return null;
            }
        }

        private IEnumerator WaitForStandUpAnimationComplete()
        {
            // Wait for a frame to ensure animation state is updated
            yield return null;
            // Wait until the current animation state is no longer the stand-up animation
            yield return new WaitUntil(() =>
            {
                AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(0);
                return stateInfo.shortNameHash != PlayerAnimations.S_StandUpFaceUp &&
                       stateInfo.shortNameHash != PlayerAnimations.S_StandUpFaceDown;
            });
        }

        private Rigidbody FindHitRigidbody(Vector3 hitPoint)
        {
            Rigidbody closestRigidbody = null;
            float closestDistance = float.MaxValue;
            foreach (Rigidbody rb in m_ragdollRigidbodies)
            {
                float distance = Vector3.Distance(rb.position, hitPoint);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRigidbody = rb;
                }
            }
            return closestRigidbody;
        }

        private void SampleBoneTransforms(BoneTransform[] boneTransforms)
        {
            for (int i = 0; i < m_bones.Length; ++i)
            {
                boneTransforms[i].Position = m_bones[i].localPosition;
                boneTransforms[i].Rotation = m_bones[i].localRotation;
            }
        }

        private void PopulateAnimationStartBoneTransforms(BoneTransform[] boneTransforms, bool faceUp)
        {
            AnimationClip clip = GetStandUpAnimation(faceUp);
            if (clip == null) return;
            clip.SampleAnimation(ModelGameObject, 0);
            SampleBoneTransforms(boneTransforms);
        }

        private void AlignRotationToHips()
        {
            Vector3 originalHipsPosition = HipsBone.position;
            Quaternion originalHipsRotation = HipsBone.rotation;

            Vector3 desiredDirection = m_isFacingUp ? -HipsBone.up : HipsBone.up;
            desiredDirection.y = 0;
            desiredDirection.Normalize();
            Quaternion fromToRotation = Quaternion.FromToRotation(ModelTransform.forward, desiredDirection);
            ModelTransform.rotation *= fromToRotation;
            m_character.Data.lookValues.x = ModelTransform.rotation.eulerAngles.y;
            m_character.UpdateLookDirection();

            HipsBone.position = originalHipsPosition;
            HipsBone.rotation = originalHipsRotation;
        }

        private void AlignPositionToHips()
        {
            Vector3 cameraOriginalPosition = m_character.Camera.transform.position;
            Vector3 originalHipsPosition = HipsBone.position;

            transform.position = HipsBone.position;
            Vector3 positionOffset = GetStandUpBoneTransformsStart()[0].Position;
            positionOffset.y = 0;
            positionOffset = ModelTransform.rotation * positionOffset;
            transform.position -= positionOffset;

            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hitInfo))
            {
                transform.position = new Vector3(transform.position.x, hitInfo.point.y, transform.position.z);
            }
            m_rigidbody.position = transform.position;

            m_character.Camera.transform.position = cameraOriginalPosition;
            HipsBone.position = originalHipsPosition;
        }

        private BoneTransform[] GetStandUpBoneTransformsStart()
        {
            return m_isFacingUp ? s_faceUpStandUpBoneTransforms : s_faceDownStandUpBoneTransforms;
        }

        private AnimationClip GetStandUpAnimation(bool faceUp)
        {
            string clipName = PlayerAnimations.C_StandUpFaceDown;
            // string clipName = faceUp ? PlayerAnimations.C_StandUpFaceUp : PlayerAnimations.C_StandUpFaceDown;
            foreach (var clip in Animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                    return clip;
            }
            return null;
        }
    }
}
