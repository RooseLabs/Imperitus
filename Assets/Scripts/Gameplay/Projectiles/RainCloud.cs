using System.Collections;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Manages the lifecycle of a rain cloud: growth, rain, shrink, despawn.
    /// </summary>
    public class RainCloud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject cloudMesh;
        [SerializeField] private ParticleSystem rainParticleSystem;
        [SerializeField] private string cloudColorPropertyName = "_Cloud_Color";

        public Vector3 CloudMeshLocalOffset => cloudMesh ? cloudMesh.transform.localPosition : Vector3.zero;

        private Material[] m_cloudMaterialInstances;
        private Renderer[] m_cloudRenderers;
        private Transform m_cloudMeshTransform;
        private Vector3 m_initialScale;

        // Lifecycle parameters (set via Initialize)
        private float m_growthDuration;
        private float m_rainDuration;
        private float m_shrinkDuration;
        private Color m_growthColorStart;
        private Color m_growthColorEnd;
        private float m_scaleStart;
        private float m_scaleEnd;

        private void Awake()
        {
            // Get cloud mesh transform and store initial scale
            if (cloudMesh)
            {
                m_cloudMeshTransform = cloudMesh.transform;
                m_initialScale = m_cloudMeshTransform.localScale;
            }
            else
            {
                Debug.LogError("[RainCloud] Cloud Mesh is not assigned in the Inspector!");
            }

            // Get all renderers in cloud mesh (including children for multi-part meshes)
            if (cloudMesh)
            {
                m_cloudRenderers = cloudMesh.GetComponentsInChildren<Renderer>();

                if (m_cloudRenderers.Length > 0)
                {
                    // Create material instances for each renderer
                    m_cloudMaterialInstances = new Material[m_cloudRenderers.Length];
                    for (int i = 0; i < m_cloudRenderers.Length; i++)
                    {
                        m_cloudMaterialInstances[i] = m_cloudRenderers[i].material; // Creates instance automatically
                    }
                    Debug.Log($"[RainCloud] Found {m_cloudRenderers.Length} renderers in cloud mesh");
                }
                else
                {
                    Debug.LogError("[RainCloud] Cloud Mesh has no Renderer components (checked children too)!");
                }
            }

            // Ensure rain is stopped initially
            if (rainParticleSystem)
            {
                rainParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else
            {
                Debug.LogWarning("[RainCloud] Rain Particle System is not assigned!");
            }
        }

        /// <summary>
        /// Initialize the cloud with configuration and start its lifecycle.
        /// </summary>
        public void Initialize(
            float growthDuration,
            float rainDuration,
            float shrinkDuration,
            Color growthColorStart,
            Color growthColorEnd,
            float scaleStart,
            float scaleEnd)
        {
            m_growthDuration = growthDuration;
            m_rainDuration = rainDuration;
            m_shrinkDuration = shrinkDuration;
            m_growthColorStart = growthColorStart;
            m_growthColorEnd = growthColorEnd;
            m_scaleStart = scaleStart;
            m_scaleEnd = scaleEnd;

            // Set initial scale and color before starting lifecycle
            if (m_cloudMeshTransform)
            {
                m_cloudMeshTransform.localScale = m_initialScale * m_scaleStart;
            }

            if (m_cloudMaterialInstances != null && m_cloudMaterialInstances.Length > 0)
            {
                foreach (var material in m_cloudMaterialInstances)
                {
                    if (material)
                        material.SetColor(cloudColorPropertyName, m_growthColorStart);
                }
            }

            // Start the cloud lifecycle
            StartCoroutine(CloudLifecycle());
        }

        private IEnumerator CloudLifecycle()
        {
            // Phase 1: Growth (small + white -> large + grey)
            yield return StartCoroutine(GrowthPhase());

            // Phase 2: Rain (stay at full size, rain active)
            yield return StartCoroutine(RainPhase());

            // Phase 3: Shrink (large -> small, rain gradually stops)
            yield return StartCoroutine(ShrinkPhase());

            // Phase 4: Despawn
            Destroy(gameObject);
        }

        private IEnumerator GrowthPhase()
        {
            if (!m_cloudMeshTransform || m_cloudMaterialInstances == null || m_cloudMaterialInstances.Length == 0)
            {
                Debug.LogError("[RainCloud] Missing cloud mesh or materials!");
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < m_growthDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_growthDuration);

                // Smooth interpolation (ease in/out)
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Scale: small -> large
                float currentScale = Mathf.Lerp(m_scaleStart, m_scaleEnd, smoothT);
                m_cloudMeshTransform.localScale = m_initialScale * currentScale;

                // Color: white -> grey (apply to all materials)
                Color currentColor = Color.Lerp(m_growthColorStart, m_growthColorEnd, smoothT);
                foreach (var material in m_cloudMaterialInstances)
                {
                    if (material)
                        material.SetColor(cloudColorPropertyName, currentColor);
                }

                yield return null;
            }

            // Ensure final values are set
            m_cloudMeshTransform.localScale = m_initialScale * m_scaleEnd;
            foreach (var material in m_cloudMaterialInstances)
            {
                if (material)
                    material.SetColor(cloudColorPropertyName, m_growthColorEnd);
            }
        }

        private IEnumerator RainPhase()
        {
            // Start rain
            if (rainParticleSystem)
            {
                rainParticleSystem.Play();
            }

            // Wait for rain duration
            yield return new WaitForSeconds(m_rainDuration);
        }

        private IEnumerator ShrinkPhase()
        {
            // Stop rain immediately when shrinking starts
            if (rainParticleSystem)
            {
                rainParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (!m_cloudMeshTransform)
            {
                yield break;
            }

            float elapsed = 0f;

            while (elapsed < m_shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / m_shrinkDuration);

                // Smooth interpolation
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                // Scale: large -> small
                float currentScale = Mathf.Lerp(m_scaleEnd, m_scaleStart, smoothT);
                m_cloudMeshTransform.localScale = m_initialScale * currentScale;

                yield return null;
            }

            // Ensure final scale is set
            m_cloudMeshTransform.localScale = m_initialScale * m_scaleStart;
        }

        private void OnDestroy()
        {
            // Clean up material instances to prevent memory leaks
            if (m_cloudMaterialInstances != null)
            {
                foreach (var material in m_cloudMaterialInstances)
                {
                    if (material)
                        Destroy(material);
                }
            }
        }
    }
}