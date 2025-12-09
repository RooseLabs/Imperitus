using System.Collections;
using FishNet.Object.Synchronizing;
using RooseLabs.Player;
using RooseLabs.Utils;
using TMPro;
using UnityEngine;

namespace RooseLabs.Gameplay.Interactables
{
    public class WordCarrierObject : Item
    {
        [SerializeField, Tooltip("Text component to display the carried word.")]
        private TMP_Text wordText;

        [SerializeField, Tooltip("Duration for the glow power animation in seconds.")]
        private float glowAnimationDuration = 2f;

        private readonly SyncVar<string> m_word = new();
        private readonly SyncVar<int> m_visibleToClientId = new(-1);

        private Coroutine m_glowAnimationCoroutine;
        private Renderer m_textRenderer;

        private static readonly int GlowPowerPropertyId = Shader.PropertyToID("_GlowPower");

        protected override void Awake()
        {
            base.Awake();
            wordText.TryGetComponent(out m_textRenderer);
        }

        public override void OnStartClient()
        {
            // Subscribe to word changes to update visual
            m_word.OnChange += OnWordChanged;
            m_visibleToClientId.OnChange += OnVisibilityChanged;
            UpdateWordDisplay(m_word.Value);
            UpdateVisibility();
        }

        private void OnDestroy()
        {
            m_word.OnChange -= OnWordChanged;
            m_visibleToClientId.OnChange -= OnVisibilityChanged;
        }

        public void SetWord(string word)
        {
            m_word.Value = word;
            UpdateWordDisplay(word);
        }

        public string GetWord()
        {
            return m_word.Value;
        }

        public void SetVisibleToClientId(int clientId)
        {
            m_visibleToClientId.Value = clientId;
        }

        private void OnWordChanged(string prev, string next, bool asServer)
        {
            UpdateWordDisplay(next);
        }

        private void OnVisibilityChanged(int prev, int next, bool asServer)
        {
            UpdateVisibility();
        }

        private void UpdateWordDisplay(string word)
        {
            if (wordText != null)
            {
                wordText.text = word;
            }
        }

        private void UpdateVisibility()
        {
            // If visibleToClientId is -1, the object is visible to everyone
            // Otherwise, only visible to the specified client
            bool isVisible = m_visibleToClientId.Value == -1 ||
                           (LocalConnection != null && LocalConnection.ClientId == m_visibleToClientId.Value);

            wordText.gameObject.SetActive(isVisible);

            if (isVisible)
                StartGlowAnimation();
            else
                StopGlowAnimation();
        }

        private void StartGlowAnimation()
        {
            if (!wordText || !m_textRenderer) return;
            StopGlowAnimation();
            m_glowAnimationCoroutine = StartCoroutine(AnimateGlowPower());
        }

        private void StopGlowAnimation()
        {
            if (m_glowAnimationCoroutine == null) return;
            StopCoroutine(m_glowAnimationCoroutine);
            m_glowAnimationCoroutine = null;
        }

        private IEnumerator AnimateGlowPower()
        {
            const float minGlowPower = 0.15f;
            const float maxGlowPower = 0.5f;
            float elapsedTime = 0f;
            bool goingUp = true;
            Camera playerCamera = PlayerCharacter.LocalCharacter.Camera;

            while (true)
            {
                if (!CameraUtils.VisibleFromCamera(playerCamera, m_textRenderer))
                {
                    yield return null;
                    continue;
                }
                elapsedTime += Time.deltaTime;

                // Determine which direction we're animating
                if (goingUp && elapsedTime >= glowAnimationDuration)
                {
                    goingUp = false;
                    elapsedTime = 0f;
                }
                else if (!goingUp && elapsedTime >= glowAnimationDuration)
                {
                    goingUp = true;
                    elapsedTime = 0f;
                }

                // Calculate normalized time (0 to 1)
                float normalizedTime = elapsedTime / glowAnimationDuration;

                // Apply ease-in-out interpolation (smoothstep)
                float easeInOut = Mathf.SmoothStep(0f, 1f, normalizedTime);

                // Interpolate between min and max glow power
                float currentGlowPower = goingUp
                    ? Mathf.Lerp(minGlowPower, maxGlowPower, easeInOut)
                    : Mathf.Lerp(maxGlowPower, minGlowPower, easeInOut);

                wordText.fontMaterial.SetFloat(GlowPowerPropertyId, currentGlowPower);
                yield return null;
            }
        }
    }
}
