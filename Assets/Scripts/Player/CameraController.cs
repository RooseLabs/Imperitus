using System.Collections.Generic;
using System.Linq;
using RooseLabs.Network;
using RooseLabs.Utils;
using UnityEngine;

namespace RooseLabs.Player
{
    public class CameraController : MonoBehaviour
    {
        #region Serialized
        [SerializeField] private Transform standPosition;
        [SerializeField] private Transform sprintPosition;
        [SerializeField] private Transform crouchPosition;
        [SerializeField] private Transform crawlPosition;

        [Header("Spectating Settings")]
        [SerializeField] private float spectateMinDistance = 0.75f;
        [SerializeField] private float spectateMaxDistance = 3f;
        [SerializeField] private float spectateDefaultDistance = 2f;
        [SerializeField] private float spectateZoomSpeed = 3f;
        [SerializeField] private float spectateDelayAfterDeath = 3f;
        [SerializeField] private float spectateMinVerticalAngle = -60f;
        [SerializeField] private float spectateMaxVerticalAngle = 40f;
        #endregion

        public static CameraController Instance { get; private set; }
        public static PlayerCharacter SpectatedCharacter { get; private set; }

        private const float RagdollTransitionTime = 0.25f;
        private float m_ragdollTransitionTimer;
        private Quaternion m_ragdollRotation;

        private readonly List<PlayerCharacter> m_spectatableCharacters = new();
        private bool m_isSpectating;
        private int m_spectateIndex;
        private float m_spectateDistance;
        private float m_deathTimer;
        private Vector3 m_currentSpecTargetPosition;
        private Vector2 m_spectateClampedLookValues;
        private Vector2 m_prevSpecLookValues;

        private void OnEnable()
        {
            Instance = this;
            m_spectateDistance = spectateDefaultDistance;
        }

        private void LateUpdate()
        {
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            if (character.Data.isDead && !m_isSpectating)
            {
                m_deathTimer += Time.deltaTime;
                if (m_deathTimer >= spectateDelayAfterDeath)
                {
                    EnterSpectateMode();
                }
            }
            else if (!character.Data.isDead)
            {
                m_deathTimer = 0f;
                if (m_isSpectating)
                {
                    ExitSpectateMode();
                }
            }

            if (m_isSpectating)
            {
                HandleSpectateInput();
                if (!SpectatedCharacter)
                {
                    UpdateSpectatableCharacters();
                    SpectatedCharacter = character;
                    m_spectateIndex = m_spectatableCharacters.IndexOf(SpectatedCharacter);
                }
                UpdateSpectateCamera(SpectatedCharacter);
            }
            else
            {
                UpdateFirstPersonCamera(character);
            }
        }

        private void UpdateFirstPersonCamera(PlayerCharacter character)
        {
            if (character.Data.IsRagdollActive)
            {
                // When ragdoll is active, camera should follow head position and orientation
                Transform headTransform = character.GetBodypart(HumanBodyBones.Head);
                Vector3 targetPosition = new Vector3(headTransform.position.x, headTransform.position.y + 0.1f, headTransform.position.z);
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, Time.deltaTime * 10f);
                Quaternion targetRotation = Quaternion.LookRotation(headTransform.forward);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
                m_ragdollTransitionTimer = RagdollTransitionTime;
                m_ragdollRotation = transform.rotation;
            }
            else
            {
                Vector3 desiredPosition = character.Data.IsSprinting ? sprintPosition.position
                    : character.Data.IsCrouching ? crouchPosition.position
                    : character.Data.IsCrawling ? crawlPosition.position
                    : standPosition.position;
                if (transform.position != desiredPosition)
                {
                    transform.position = Vector3.MoveTowards(transform.position, desiredPosition, Time.deltaTime * 2.5f);
                }

                if (m_ragdollTransitionTimer > 0.0f)
                {
                    m_ragdollTransitionTimer -= Time.deltaTime;
                    float progress = Mathf.Clamp01(1 - (m_ragdollTransitionTimer / RagdollTransitionTime));
                    transform.rotation = Quaternion.Slerp(m_ragdollRotation, Quaternion.LookRotation(character.Data.lookDirection), progress);
                }
                else
                {
                    transform.rotation = Quaternion.LookRotation(character.Data.lookDirection);
                }
            }
        }

        private void UpdateSpectateCamera(PlayerCharacter targetCharacter)
        {
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            Vector2 lookDelta = character.Data.lookValues - m_prevSpecLookValues;
            m_prevSpecLookValues = character.Data.lookValues;
            m_spectateClampedLookValues += lookDelta;
            m_spectateClampedLookValues.y = Mathf.Clamp(m_spectateClampedLookValues.y, spectateMinVerticalAngle, spectateMaxVerticalAngle);
            Vector3 clampedLookDirection = HelperFunctions.LookToDirection(m_spectateClampedLookValues, Vector3.forward).normalized;

            Vector3 chestPosition = targetCharacter.GetBodypart(HumanBodyBones.Chest).position;
            Vector3 headPosition = targetCharacter.GetBodypart(HumanBodyBones.Head).position + Vector3.up * 0.1f;

            // Lerp between chest and head based on distance
            float lerpFactor = Mathf.InverseLerp(2f, 1f, m_spectateDistance);
            lerpFactor = Mathf.Clamp01(lerpFactor);
            Vector3 desiredTargetPosition = Vector3.Lerp(chestPosition, headPosition, lerpFactor);

            m_currentSpecTargetPosition = Vector3.Lerp(m_currentSpecTargetPosition, desiredTargetPosition, Time.deltaTime * 10f);

            Vector3 distanceOffset = -clampedLookDirection * m_spectateDistance;
            Vector3 desiredPosition = m_currentSpecTargetPosition + distanceOffset;

            // Raycast to prevent camera clipping through walls
            if (targetCharacter.RaycastIgnoreSelf(m_currentSpecTargetPosition, distanceOffset.normalized, out RaycastHit hit, m_spectateDistance))
            {
                desiredPosition = hit.point + hit.normal * 0.2f;
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 10f);
            if (targetCharacter.Data.IsRagdollActive)
                transform.position += Vector3.up * 0.025f;
            Vector3 directionToTarget = m_currentSpecTargetPosition - transform.position;
            if (directionToTarget.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(directionToTarget);
        }

        private void HandleSpectateInput()
        {
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            if (character.Input.previousWasPressed && character.Input.nextWasPressed)
            {
                // No-op
            }
            else if (character.Input.previousWasPressed)
            {
                SwitchToSpectateTarget(-1);
            }
            else if (character.Input.nextWasPressed)
            {
                SwitchToSpectateTarget(1);
            }

            float scrollDelta = character.Input.scrollInput;
            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                m_spectateDistance = Mathf.Lerp(m_spectateDistance, m_spectateDistance - scrollDelta * spectateZoomSpeed, Time.deltaTime * 10f);
                m_spectateDistance = Mathf.Clamp(m_spectateDistance, spectateMinDistance, spectateMaxDistance);
            }
        }

        public void ResetPosition()
        {
            PlayerCharacter character = PlayerCharacter.LocalCharacter;

            Vector3 desiredPosition = character.Data.IsSprinting ? sprintPosition.position
                : character.Data.IsCrouching ? crouchPosition.position
                : character.Data.IsCrawling ? crawlPosition.position
                : standPosition.position;

            transform.position = desiredPosition;
        }

        private void EnterSpectateMode()
        {
            m_isSpectating = true;
            UpdateSpectatableCharacters();
            SpectatedCharacter = PlayerCharacter.LocalCharacter;
            m_spectateIndex = m_spectatableCharacters.IndexOf(SpectatedCharacter);
            m_spectateDistance = spectateDefaultDistance;
            // Initialize spectate target position and look values to prevent initial snapping
            m_currentSpecTargetPosition = SpectatedCharacter.GetBodypart(HumanBodyBones.Chest).position;
            PlayerCharacter character = PlayerCharacter.LocalCharacter;
            m_spectateClampedLookValues = character.Data.lookValues;
            m_prevSpecLookValues = character.Data.lookValues;
            // Show the player's usually hidden meshes while spectating
            PlayerCharacter.LocalCharacter.ToggleMeshesVisibility(true);
        }

        private void ExitSpectateMode()
        {
            m_isSpectating = false;
            SpectatedCharacter = null;
            m_spectatableCharacters.Clear();
            PlayerCharacter.LocalCharacter.ToggleMeshesVisibility(false);
        }

        private void UpdateSpectatableCharacters()
        {
            m_spectatableCharacters.Clear();
            m_spectatableCharacters.AddRange(PlayerHandler.AllCharacters.Where(c => (bool)c));
        }

        private void SwitchToSpectateTarget(int direction)
        {
            if (m_spectatableCharacters.Count == 0)
                UpdateSpectatableCharacters();

            if (m_spectatableCharacters.Count == 0)
                return;

            m_spectateIndex = (m_spectateIndex + direction + m_spectatableCharacters.Count) % m_spectatableCharacters.Count;
            SpectatedCharacter = m_spectatableCharacters[m_spectateIndex];
        }
    }
}
