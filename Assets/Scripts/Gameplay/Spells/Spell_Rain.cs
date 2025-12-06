using FishNet.Object;
using RooseLabs.Player;
using UnityEngine;

namespace RooseLabs.Gameplay.Spells
{
    /// <summary>
    /// Rain spell - Player aims to place a cloud that grows, rains, then despawns.
    /// </summary>
    public class Spell_Rain : SpellBase
    {
        #region Serialized Fields
        [Header("Rain Spell Configuration")]
        [SerializeField] private GameObject rainCloudPrefab;

        [Header("Placement Settings")]
        [SerializeField] private float maxPlacementRange = 50f;
        [SerializeField] private LayerMask placementLayerMask = ~0;
        [SerializeField] private float cloudSpawnHeight = 10f;

        [Header("Placement Indicators")]
        [SerializeField] private GameObject groundIndicatorPrefab;
        [SerializeField] private GameObject ghostCloudPrefab;

        [Header("Cloud Lifecycle Durations")]
        [SerializeField] private float growthDuration = 2f;
        [SerializeField] private float rainDuration = 5f;
        [SerializeField] private float shrinkDuration = 1.5f;

        [Header("Cloud Appearance")]
        [SerializeField] private Color growthColorStart = Color.white;
        [SerializeField] private Color growthColorEnd = Color.grey;
        [SerializeField] private float scaleStart = 0.3f;
        [SerializeField] private float scaleEnd = 1f;
        #endregion

        #region Private Fields
        private GameObject m_groundIndicatorInstance;
        private GameObject m_ghostCloudInstance;
        private bool m_hasValidPlacement;
        private Vector3 m_targetGroundPosition;
        private Vector3 m_targetCloudPosition;
        private Quaternion m_targetCloudRotation;
        private Vector3 m_cloudMeshLocalOffset;
        #endregion

        #region Spell Lifecycle
        protected override void OnStartCast()
        {
            base.OnStartCast();
            // Casting started, keep showing indicators
        }

        protected override void OnContinueCast()
        {
            base.OnContinueCast();
            // Continue showing indicators during cast
        }

        protected override bool OnCastFinished()
        {
            base.OnCastFinished();

            if (!m_hasValidPlacement)
            {
                Logger.Warning("[Rain] Cast finished but no valid placement found.");
                return false;
            }

            // Spawn the rain cloud at the target position with correct rotation
            SpawnRainCloud(m_targetCloudPosition, m_targetCloudRotation);

            // Hide indicators after successful cast
            HideIndicators();

            return true;
        }

        protected override void OnCancelCast()
        {
            base.OnCancelCast();
            HideIndicators();
        }
        #endregion

        #region Lifecycle
        private void Update()
        {
            if (!CasterCharacter) return;
            if (CasterCharacter != PlayerCharacter.LocalCharacter) return;

            // Only update placement while player is aiming (not necessarily casting yet)
            if (CasterCharacter.Data.isAiming)
            {
                UpdatePlacementIndicators();
            }
            else
            {
                HideIndicators();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Only local player needs indicators
            if (IsOwner)
            {
                InitializeIndicators();
            }
        }
        #endregion

        #region Placement Indicators
        private void InitializeIndicators()
        {
            // Get cloud mesh local offset from the rain cloud prefab's RainCloud component
            if (rainCloudPrefab && rainCloudPrefab.TryGetComponent(out RainCloud rainCloudComponent))
            {
                m_cloudMeshLocalOffset = rainCloudComponent.CloudMeshLocalOffset;
                Logger.Info($"[Rain] Cloud mesh local offset from prefab: {m_cloudMeshLocalOffset}");
            }
            else
            {
                Logger.Warning("[Rain] Could not get cloud mesh offset from prefab - using zero offset");
                m_cloudMeshLocalOffset = Vector3.zero;
            }

            // Create ground indicator
            if (groundIndicatorPrefab)
            {
                m_groundIndicatorInstance = Instantiate(groundIndicatorPrefab);
                m_groundIndicatorInstance.SetActive(false);
            }

            // Create ghost cloud indicator
            if (ghostCloudPrefab)
            {
                m_ghostCloudInstance = Instantiate(ghostCloudPrefab);
                m_ghostCloudInstance.SetActive(false);
            }
        }

        private void UpdatePlacementIndicators()
        {
            // Raycast from camera to find placement position
            Ray ray = new Ray(CasterCharacter.Camera.transform.position, CasterCharacter.Data.lookDirection);

            if (Physics.Raycast(ray, out RaycastHit hit, maxPlacementRange, placementLayerMask))
            {
                m_hasValidPlacement = true;
                m_targetGroundPosition = hit.point;

                // Calculate where the parent should spawn so the cloud mesh ends up at the desired height
                Vector3 desiredCloudMeshPosition = hit.point + Vector3.up * cloudSpawnHeight;
                m_targetCloudPosition = desiredCloudMeshPosition - m_cloudMeshLocalOffset;

                // Calculate rotation to face the player (cloud's forward faces player)
                Vector3 directionToPlayer = CasterCharacter.Camera.transform.position - m_targetCloudPosition;
                directionToPlayer.y = 0; // Keep rotation only on horizontal plane
                if (directionToPlayer.sqrMagnitude > 0.001f)
                {
                    m_targetCloudRotation = Quaternion.LookRotation(directionToPlayer);
                }
                else
                {
                    m_targetCloudRotation = Quaternion.identity;
                }

                // Show and position ground indicator
                if (m_groundIndicatorInstance)
                {
                    m_groundIndicatorInstance.SetActive(true);
                    m_groundIndicatorInstance.transform.position = m_targetGroundPosition + Vector3.up * 0.01f;
                    // Align with surface normal
                    m_groundIndicatorInstance.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
                }

                // Show and position ghost cloud
                if (m_ghostCloudInstance)
                {
                    m_ghostCloudInstance.SetActive(true);
                    // Position and rotate the ghost cloud
                    m_ghostCloudInstance.transform.position = m_targetCloudPosition;
                    m_ghostCloudInstance.transform.rotation = m_targetCloudRotation;
                }
            }
            else
            {
                m_hasValidPlacement = false;
                HideIndicators();
            }
        }

        private void HideIndicators()
        {
            if (m_groundIndicatorInstance)
                m_groundIndicatorInstance.SetActive(false);

            if (m_ghostCloudInstance)
                m_ghostCloudInstance.SetActive(false);
        }
        #endregion

        #region Cloud Spawning
        private void SpawnRainCloud(Vector3 position, Quaternion rotation)
        {
            if (IsServerInitialized)
            {
                SpawnRainCloud_ObserversRpc(position, rotation);
            }
            else
            {
                SpawnRainCloud_ServerRpc(position, rotation);
                SpawnRainCloudLocal(position, rotation);
            }
        }

        private void SpawnRainCloudLocal(Vector3 position, Quaternion rotation)
        {
            if (!rainCloudPrefab)
            {
                Logger.Error("[Rain] Rain cloud prefab is not assigned!");
                return;
            }

            GameObject cloudInstance = Instantiate(rainCloudPrefab, position, rotation);

            if (cloudInstance.TryGetComponent(out RainCloud rainCloud))
            {
                // Configure the cloud with our settings
                rainCloud.Initialize(
                    growthDuration,
                    rainDuration,
                    shrinkDuration,
                    growthColorStart,
                    growthColorEnd,
                    scaleStart,
                    scaleEnd
                );
            }
            else
            {
                Logger.Error("[Rain] Rain cloud prefab is missing RainCloud component!");
                Destroy(cloudInstance);
            }
        }
        #endregion

        #region Network Sync
        [ServerRpc(RequireOwnership = true)]
        private void SpawnRainCloud_ServerRpc(Vector3 position, Quaternion rotation)
        {
            SpawnRainCloud_ObserversRpc(position, rotation);
        }

        [ObserversRpc(ExcludeOwner = true, ExcludeServer = true, RunLocally = true)]
        private void SpawnRainCloud_ObserversRpc(Vector3 position, Quaternion rotation)
        {
            SpawnRainCloudLocal(position, rotation);
        }
        #endregion

        #region Cleanup
        protected override void ResetData()
        {
            base.ResetData();
            HideIndicators();
            m_hasValidPlacement = false;
        }

        private void OnDestroy()
        {
            // Clean up indicators when spell is destroyed
            if (m_groundIndicatorInstance)
                Destroy(m_groundIndicatorInstance);

            if (m_ghostCloudInstance)
                Destroy(m_ghostCloudInstance);
        }
        #endregion
    }
}