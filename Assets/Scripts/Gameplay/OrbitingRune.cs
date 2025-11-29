using System.Collections;
using RooseLabs.Player;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class OrbitingRune : MonoBehaviour
    {
        private SpriteRenderer m_spriteRenderer;
        private Vector3 m_initialPosition;

        private const float NoiseSpeed = 0.5f;
        private const float NoiseAmplitude = 1f;
        private Vector2 m_noiseSeed;
        private bool m_isVisible = false;

        private void Awake()
        {
            TryGetComponent(out m_spriteRenderer);
            m_spriteRenderer.flipX = true;
            m_spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
            m_spriteRenderer.sortingOrder = 100;
            m_noiseSeed = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));
        }

        private void Update()
        {
            if (!m_isVisible) return;
            float noiseX = Mathf.PerlinNoise(Time.time * NoiseSpeed + m_noiseSeed.x, 0f) - 0.5f;
            float noiseY = Mathf.PerlinNoise(Time.time * NoiseSpeed + m_noiseSeed.y, 0f) - 0.5f;

            Vector3 offset = new Vector3(noiseX * NoiseAmplitude, noiseY * NoiseAmplitude, 0f);
            transform.localPosition = m_initialPosition + offset;
        }

        private void LateUpdate()
        {
            if (!m_isVisible) return;
            if (PlayerCharacter.LocalCharacter)
                transform.LookAt(PlayerCharacter.LocalCharacter.Camera.transform);
        }

        public void SetRune(RuneSO rune)
        {
            m_spriteRenderer.sprite = rune.Sprite;
        }

        public void SetPosition(Vector3 position)
        {
            m_initialPosition = position;
        }

        public void SetVisible(bool isVisible)
        {
            if (m_isVisible == isVisible) return;
            m_isVisible = isVisible;
            StopAllCoroutines();
            StartCoroutine(FadeCoroutine(isVisible, .25f));
        }

        private IEnumerator FadeCoroutine(bool fadeIn, float duration)
        {
            float elapsed = 0f;
            Color startColor = m_spriteRenderer.color;
            Color targetColor = fadeIn ? Color.white : new Color(1f, 1f, 1f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                m_spriteRenderer.color = Color.Lerp(startColor, targetColor, elapsed / duration);
                yield return null;
            }

            m_spriteRenderer.color = targetColor;
        }
    }
}
