using System.Collections;
using FishNet.Object;
using RooseLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.Gameplay
{
    public class EndGameTrigger : NetworkBehaviour
    {
        [Header("End Game UI")]
        [SerializeField] private Canvas endGameCanvas;
        [SerializeField] private Image fadePanel;
        [SerializeField] private TMP_Text endText;
        [SerializeField] private float fadeDuration = 2f;

        private void Awake()
        {
            if (endGameCanvas != null)
                endGameCanvas.gameObject.SetActive(false);
        }

        private void OnTriggerEnter(Collider other)
        {
            // if (!IsServerInitialized)
            //     return;
            //
            // if (!other.CompareTag("Player"))
            //     return;
            //
            // if (GameManager.Instance.CollectedRunes.Count == 3)
            // {
            //     Debug.Log("Game Ended! Player entered the area with all runes.");
            //     RpcEndGame();
            // }
            // else
            // {
            //     Debug.Log("Player entered the area but does not have all runes.");
            // }
        }

        [ObserversRpc]
        private void RpcEndGame()
        {
            Debug.Log("Game Ended! Showing end screen on clients.");

            if (endGameCanvas != null)
            {
                StartCoroutine(FadeToEnd());
            }
            else
                Debug.LogWarning("EndGameCanvas is not assigned.");
        }

        private IEnumerator FadeToEnd()
        {
            endGameCanvas.gameObject.SetActive(true);

            // Reset alpha
            SetAlpha(0f);
            endText.alpha = 0f;

            float elapsed = 0f;

            // PHASE 1 � Fade screen to black
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                SetAlpha(alpha);
                yield return null;
            }

            // Ensure fully black before text starts fading
            SetAlpha(1f);

            // Optional: short pause before showing text
            yield return new WaitForSeconds(0.5f);

            // PHASE 2 � Fade in the text
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / fadeDuration);
                endText.alpha = alpha;
                yield return null;
            }

            endText.alpha = 1f;

            InputHandler.Instance.DisableAllInput();
            CloseGameAfterSeconds(5f);
        }

        private void SetAlpha(float a)
        {
            if (fadePanel == null)
                return;

            var color = fadePanel.color;
            color.a = a;
            fadePanel.color = color;
        }

        private void CloseGameAfterSeconds(float delay)
        {
            StartCoroutine(CloseGameCoroutine(delay));
        }

        private IEnumerator CloseGameCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            #if UNITY_EDITOR
            // Stop play mode if in the editor
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            // Quit the built game
            Application.Quit();
            #endif
        }
    }
}
