using RooseLabs.Utils;
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    /// <summary>
    /// Defines how the button should auto-fit to the text size.
    /// </summary>
    public enum TextAutoFitMode
    {
        /// <summary>No auto-fit. Manual size control.</summary>
        None,
        /// <summary>Fit to the minimum size the text can be.</summary>
        MinSize,
        /// <summary>Fit to the preferred (actual) size of the text.</summary>
        PreferredSize,
        /// <summary>Fit to the maximum size the text can take.</summary>
        MaxSize
    }

    /// <summary>
    /// Structure that stores the state of font size transitions on a TextButton.
    /// </summary>
    [Serializable]
    public struct FontSizeBlock : IEquatable<FontSizeBlock>
    {
        [SerializeField] private float m_NormalSize;
        [SerializeField] private float m_HighlightedSize;
        [SerializeField] private float m_PressedSize;
        [SerializeField] private float m_SelectedSize;
        [SerializeField] private float m_DisabledSize;
        [SerializeField] private float m_SizeDuration;

        public float normalSize { get => m_NormalSize; set => m_NormalSize = value; }
        public float highlightedSize { get => m_HighlightedSize; set => m_HighlightedSize = value; }
        public float pressedSize { get => m_PressedSize; set => m_PressedSize = value; }
        public float selectedSize { get => m_SelectedSize; set => m_SelectedSize = value; }
        public float disabledSize { get => m_DisabledSize; set => m_DisabledSize = value; }
        public float sizeDuration { get => m_SizeDuration; set => m_SizeDuration = value; }

        public static FontSizeBlock defaultFontSizeBlock;

        static FontSizeBlock()
        {
            defaultFontSizeBlock = new FontSizeBlock
            {
                m_NormalSize = 36f,
                m_HighlightedSize = 38f,
                m_PressedSize = 34f,
                m_SelectedSize = 38f,
                m_DisabledSize = 32f,
                m_SizeDuration = 0.1f
            };
        }

        public override bool Equals(object obj)
        {
            return obj is FontSizeBlock block && Equals(block);
        }

        public bool Equals(FontSizeBlock other)
        {
            return normalSize == other.normalSize &&
                   highlightedSize == other.highlightedSize &&
                   pressedSize == other.pressedSize &&
                   selectedSize == other.selectedSize &&
                   disabledSize == other.disabledSize &&
                   sizeDuration == other.sizeDuration;
        }

        public static bool operator==(FontSizeBlock point1, FontSizeBlock point2)
        {
            return point1.Equals(point2);
        }

        public static bool operator!=(FontSizeBlock point1, FontSizeBlock point2)
        {
            return !point1.Equals(point2);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// A button that applies color tint transitions to both the button graphic and its text.
    /// </summary>
    [AddComponentMenu("RooseLabs/UI/Text Button")]
    public class TextButton : Button
    {
        #region Serialized
        [SerializeField, HideInInspector]
        private TMP_Text m_TargetText;

        [SerializeField]
        private ColorBlock m_TextColors = ColorBlock.defaultColorBlock;
        [SerializeField]
        private FontSizeBlock m_FontSizes = FontSizeBlock.defaultFontSizeBlock;

        [SerializeField]
        private TextAutoFitMode m_AutoFitMode = TextAutoFitMode.None;
        [SerializeField]
        private Vector2 m_AutoFitPadding = Vector2.zero;
        #endregion

        private RectTransform m_RectTransform;
        private Coroutine m_FontSizeTweenCoroutine;

        protected override void Awake()
        {
            base.Awake();

            // Try to find text component if not already set
            m_TargetText ??= GetComponentInChildren<TMP_Text>();

            // Cache RectTransform
            m_RectTransform = GetComponent<RectTransform>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ApplyAutoFit();
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            // Call base implementation for button graphic
            base.DoStateTransition(state, instant);

            // Apply text color transition
            DoTextStateTransition(state, instant);
        }

        /// <summary>
        /// Apply color transition to the text element based on the selection state.
        /// </summary>
        /// <param name="state">The current selection state</param>
        /// <param name="instant">Whether to apply the transition instantly</param>
        protected virtual void DoTextStateTransition(SelectionState state, bool instant)
        {
            if (!m_TargetText) return;

            Color tintColor = state switch
            {
                SelectionState.Normal => m_TextColors.normalColor,
                SelectionState.Highlighted => m_TextColors.highlightedColor,
                SelectionState.Pressed => m_TextColors.pressedColor,
                SelectionState.Selected => m_TextColors.selectedColor,
                SelectionState.Disabled => m_TextColors.disabledColor,
                _ => Color.black
            };

            StartTextColorTween(tintColor * m_TextColors.colorMultiplier, instant);

            // Apply font size transition
            float targetSize = state switch
            {
                SelectionState.Normal => m_FontSizes.normalSize,
                SelectionState.Highlighted => m_FontSizes.highlightedSize,
                SelectionState.Pressed => m_FontSizes.pressedSize,
                SelectionState.Selected => m_FontSizes.selectedSize,
                SelectionState.Disabled => m_FontSizes.disabledSize,
                _ => 36f
            };

            StartFontSizeTween(targetSize, instant);
        }

        /// <summary>
        /// Tweens the text color.
        /// </summary>
        /// <param name="targetColor">Target color to tween to</param>
        /// <param name="instant">Should the transition be instant</param>
        private void StartTextColorTween(Color targetColor, bool instant)
        {
            if (!m_TargetText) return;

            m_TargetText.CrossFadeColor(targetColor, instant ? 0f : m_TextColors.fadeDuration, true, true);
        }

        /// <summary>
        /// Tweens the font size.
        /// </summary>
        /// <param name="targetSize">Target absolute font size</param>
        /// <param name="instant">Should the transition be instant</param>
        private void StartFontSizeTween(float targetSize, bool instant)
        {
            if (!m_TargetText) return;

            // If target size is 0 or less, don't change the font size
            if (targetSize <= 0f) return;

            // Stop any existing tween
            if (m_FontSizeTweenCoroutine != null)
            {
                StopCoroutine(m_FontSizeTweenCoroutine);
                m_FontSizeTweenCoroutine = null;
            }

            if (instant || m_FontSizes.sizeDuration <= 0f)
            {
                m_TargetText.fontSize = targetSize;
            }
            else
            {
                m_FontSizeTweenCoroutine = StartCoroutine(TweenFontSize(m_TargetText.fontSize, targetSize, m_FontSizes.sizeDuration));
            }
        }

        /// <summary>
        /// Coroutine that smoothly transitions font size over time.
        /// </summary>
        private IEnumerator TweenFontSize(float startSize, float endSize, float duration)
        {
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                m_TargetText.fontSize = Mathf.Lerp(startSize, endSize, t);
                yield return null;
            }

            m_TargetText.fontSize = endSize;
            m_FontSizeTweenCoroutine = null;
        }

        protected override void InstantClearState()
        {
            base.InstantClearState();

            // Clear text color state
            if (m_TargetText)
            {
                StartTextColorTween(Color.white, true);

                // Stop any font size tween
                if (m_FontSizeTweenCoroutine != null)
                {
                    StopCoroutine(m_FontSizeTweenCoroutine);
                    m_FontSizeTweenCoroutine = null;
                }

                // Reset to normal font size
                if (m_FontSizes.normalSize > 0f)
                {
                    m_TargetText.fontSize = m_FontSizes.normalSize;
                }
            }
        }

        /// <summary>
        /// Applies auto-fit sizing to the button based on the text's dimensions.
        /// </summary>
        private void ApplyAutoFit()
        {
            if (!m_RectTransform || !m_TargetText) return;

            Vector2 textSize;

            switch (m_AutoFitMode)
            {
                case TextAutoFitMode.PreferredSize:
                    // Use current font size
                    textSize = GetTextPreferredSize(m_TargetText.fontSize);
                    break;
                case TextAutoFitMode.MinSize:
                {
                    // Find minimum font size from FontSizeBlock
                    float minSize = GetMinFontSize();
                    textSize = GetTextPreferredSize(minSize);
                    break;
                }
                // MaxSize
                case TextAutoFitMode.MaxSize:
                {
                    // Find maximum font size from FontSizeBlock
                    float maxSize = GetMaxFontSize();
                    textSize = GetTextPreferredSize(maxSize);
                    break;
                }
                case TextAutoFitMode.None:
                default:
                    return;
            }

            // Add padding
            Vector2 newSize = textSize + m_AutoFitPadding;

            // Apply the new size
            #if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        if (m_RectTransform)
                        {
                            m_RectTransform.sizeDelta = newSize;
                        }
                    };
                }
                else
                {
                    m_RectTransform.sizeDelta = newSize;
                }
            #else
                m_RectTransform.sizeDelta = newSize;
            #endif
        }

        /// <summary>
        /// Gets the preferred size of the text at a specific font size without actually changing the displayed font size.
        /// </summary>
        private Vector2 GetTextPreferredSize(float fontSize)
        {
            if (!m_TargetText) return Vector2.zero;

            // Store current font size
            float originalSize = m_TargetText.fontSize;

            // Temporarily set the font size
            m_TargetText.fontSize = fontSize;

            // Force the text to update
            m_TargetText.ForceMeshUpdate();

            // Get preferred values
            Vector2 preferredSize = m_TargetText.GetPreferredValues();

            // Restore original font size
            m_TargetText.fontSize = originalSize;

            // Force update again to restore
            m_TargetText.ForceMeshUpdate();

            return preferredSize;
        }

        /// <summary>
        /// Gets the minimum font size from the FontSizeBlock.
        /// </summary>
        private float GetMinFontSize()
        {
            float min = float.MaxValue;

            if (m_FontSizes.normalSize > 0f) min = Mathf.Min(min, m_FontSizes.normalSize);
            if (m_FontSizes.highlightedSize > 0f) min = Mathf.Min(min, m_FontSizes.highlightedSize);
            if (m_FontSizes.pressedSize > 0f) min = Mathf.Min(min, m_FontSizes.pressedSize);
            if (m_FontSizes.selectedSize > 0f) min = Mathf.Min(min, m_FontSizes.selectedSize);
            if (m_FontSizes.disabledSize > 0f) min = Mathf.Min(min, m_FontSizes.disabledSize);

            // If no valid sizes found, return current font size
            return min == float.MaxValue ? m_TargetText.fontSize : min;
        }

        /// <summary>
        /// Gets the maximum font size from the FontSizeBlock.
        /// </summary>
        private float GetMaxFontSize()
        {
            float max = 0f;

            max = Mathf.Max(max, m_FontSizes.normalSize);
            max = Mathf.Max(max, m_FontSizes.highlightedSize);
            max = Mathf.Max(max, m_FontSizes.pressedSize);
            max = Mathf.Max(max, m_FontSizes.selectedSize);
            max = Mathf.Max(max, m_FontSizes.disabledSize);

            // If no valid sizes found, return current font size
            return max == 0f ? m_TargetText.fontSize : max;
        }

        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            m_TextColors.fadeDuration = Mathf.Max(m_TextColors.fadeDuration, 0.0f);
            m_FontSizes.sizeDuration = Mathf.Max(m_FontSizes.sizeDuration, 0.0f);

            // OnValidate can be called before OnEnable, so check if we're active
            if (isActiveAndEnabled)
            {
                // Clear text color and go to the right state
                if ((bool)m_TargetText || this.TryGetComponentInChildren(out m_TargetText))
                {

                    StartTextColorTween(Color.white, true);
                    DoTextStateTransition(currentSelectionState, true);
                    ApplyAutoFit();
                }
            }
        }

        protected override void Reset()
        {
            base.Reset();

            // Try to find text component
            m_TargetText = GetComponentInChildren<TMP_Text>();
        }
        #endif
    }
}
