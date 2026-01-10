using System.Collections;
using System.Collections.Generic;
using RooseLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI
{
    public class CutsceneManager : MonoBehaviour
    {
        [SerializeField] private Graphic fadeGraphic;
        [SerializeField] private TMP_Text text1;
        [SerializeField] private TMP_Text text2;
        [SerializeField] private TMP_Text text3;

        private const float TextAnimationDuration = 2f;

        private Coroutine m_cutsceneCoroutine;

        private void OnEnable()
        {
            fadeGraphic.color = new Color(0f, 0f, 0f, 0f);
            text1.text = text2.text = text3.text = string.Empty;
            text1.color = new Color(1f, 1f, 1f, 1f);
            text2.color = new Color(1f, 1f, 1f, 1f);
            text3.color = new Color(1f, 1f, 1f, 1f);
        }

        public void PlayCutscene(string line1, string line2, string line3)
        {
            gameObject.SetActive(true);
            InputHandler.Instance.DisableAllInput();
            if (m_cutsceneCoroutine != null)
                StopCoroutine(m_cutsceneCoroutine);

            m_cutsceneCoroutine = StartCoroutine(PlayCutsceneCoroutine(line1, line2, line3));
        }

        private IEnumerator PlayCutsceneCoroutine(string line1, string line2, string line3)
        {
            // Fade in background to black over 1s
            yield return FadeCoroutine(Color.black, 1f);

            // Animate line1 character by character
            yield return AnimateTextCoroutine(text1, line1, TextAnimationDuration);
            // Animate line2 character by character
            yield return AnimateTextCoroutine(text2, line2, TextAnimationDuration);
            // Animate line3 character by character
            yield return AnimateTextCoroutine(text3, line3, TextAnimationDuration);

            // Wait 1.5 seconds
            yield return new WaitForSeconds(1.5f);

            // Fade all text alpha to 0 over 0.5s
            yield return FadeTextAlphaCoroutine(0.5f);

            // Fade out background over 1s
            yield return FadeCoroutine(new Color(0f, 0f, 0f, 0f), 1f);

            gameObject.SetActive(false);
            InputHandler.Instance.EnableGameplayInput();
            m_cutsceneCoroutine = null;
        }

        private IEnumerator AnimateTextCoroutine(TMP_Text textComponent, string fullText, float duration)
        {
            // Parse text into visible characters and tags
            var textElements = ParseTextWithTags(fullText);
            int visibleCharCount = CountVisibleCharacters(textElements);

            // Set all text with transparent color tags
            textComponent.text = WrapTextWithTransparentTags(textElements);

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                int revealedCharCount = Mathf.FloorToInt((elapsed / duration) * visibleCharCount);
                revealedCharCount = Mathf.Min(revealedCharCount, visibleCharCount);

                string displayText = RebuildTextWithReveal(textElements, revealedCharCount);
                textComponent.text = displayText;
                yield return null;
            }

            // Ensure full text is displayed without transparent tags
            textComponent.text = fullText;
        }

        private List<string> ParseTextWithTags(string text)
        {
            var elements = new List<string>();
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] == '<')
                {
                    // Find the closing >
                    int closeIndex = text.IndexOf('>', i);
                    if (closeIndex != -1)
                    {
                        // Extract the entire tag
                        string markupTag = text.Substring(i, closeIndex - i + 1);
                        elements.Add(markupTag);
                        i = closeIndex + 1;
                        continue;
                    }
                }

                // Regular character
                elements.Add(text[i].ToString());
                i++;
            }

            return elements;
        }

        private int CountVisibleCharacters(List<string> elements)
        {
            int count = 0;
            foreach (var element in elements)
            {
                // Only count actual characters, not tags
                if (!element.StartsWith("<"))
                {
                    count++;
                }
            }
            return count;
        }

        private string WrapTextWithTransparentTags(List<string> elements)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (var element in elements)
            {
                if (element.StartsWith("<"))
                {
                    // Keep existing tags as-is
                    sb.Append(element);
                }
                else
                {
                    // Wrap character in transparent color tag
                    sb.Append("<color=#00000000>");
                    sb.Append(element);
                    sb.Append("</color>");
                }
            }

            return sb.ToString();
        }

        private string RebuildTextWithReveal(List<string> elements, int revealedCount)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int visibleCharIndex = 0;

            foreach (var element in elements)
            {
                if (element.StartsWith("<"))
                {
                    // Keep existing tags as-is
                    sb.Append(element);
                }
                else
                {
                    // Reveal character if within revealed count, otherwise keep transparent
                    if (visibleCharIndex < revealedCount)
                    {
                        sb.Append(element);
                    }
                    else
                    {
                        sb.Append("<color=#00000000>");
                        sb.Append(element);
                        sb.Append("</color>");
                    }
                    visibleCharIndex++;
                }
            }

            return sb.ToString();
        }

        private IEnumerator FadeTextAlphaCoroutine(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);

                Color text1Color = text1.color;
                text1Color.a = alpha;
                text1.color = text1Color;

                Color text2Color = text2.color;
                text2Color.a = alpha;
                text2.color = text2Color;

                Color text3Color = text3.color;
                text3Color.a = alpha;
                text3.color = text3Color;

                yield return null;
            }

            // Ensure final alpha is 0
            Color finalColor1 = text1.color;
            finalColor1.a = 0f;
            text1.color = finalColor1;

            Color finalColor2 = text2.color;
            finalColor2.a = 0f;
            text2.color = finalColor2;

            Color finalColor3 = text3.color;
            finalColor3.a = 0f;
            text3.color = finalColor3;
        }

        private IEnumerator FadeCoroutine(Color targetColor, float duration)
        {
            Color initialColor = fadeGraphic.color;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                fadeGraphic.color = Color.Lerp(initialColor, targetColor, elapsed / duration);
                yield return null;
            }

            fadeGraphic.color = targetColor;
        }
    }
}
