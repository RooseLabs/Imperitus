using System;
using System.Collections;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RooseLabs.UI.Elements
{
    /// <summary>
    /// Defines how a selectable should auto-fit to the text size.
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
    /// Selection state for text transitions. Mirrors Selectable.SelectionState.
    /// </summary>
    public enum TextSelectionState
    {
        Normal,
        Highlighted,
        Pressed,
        Selected,
        Disabled
    }

    /// <summary>
    /// Structure that stores the state of font size transitions.
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

        public static bool operator ==(FontSizeBlock point1, FontSizeBlock point2)
        {
            return point1.Equals(point2);
        }

        public static bool operator !=(FontSizeBlock point1, FontSizeBlock point2)
        {
            return !point1.Equals(point2);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Helper class that handles text color and size transitions for selectables.
    /// Used by TextButton and TextToggle to share transition logic.
    /// </summary>
    [Serializable]
    public class TextTransitionHelper
    {
        #region Serialized Fields
        [SerializeField]
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

        #region Properties
        public TMP_Text TargetText
        {
            get => m_TargetText;
            set => m_TargetText = value;
        }

        public ColorBlock TextColors
        {
            get => m_TextColors;
            set => m_TextColors = value;
        }

        public FontSizeBlock FontSizes
        {
            get => m_FontSizes;
            set => m_FontSizes = value;
        }

        public TextAutoFitMode AutoFitMode
        {
            get => m_AutoFitMode;
            set => m_AutoFitMode = value;
        }

        public Vector2 AutoFitPadding
        {
            get => m_AutoFitPadding;
            set => m_AutoFitPadding = value;
        }
        #endregion

        #region Private State
        private MonoBehaviour m_Owner;
        private RectTransform m_RectTransform;
        private Coroutine m_FontSizeTweenCoroutine;
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the helper with the owning MonoBehaviour.
        /// Must be called in Awake.
        /// </summary>
        public void Initialize(MonoBehaviour owner)
        {
            m_Owner = owner;
            m_RectTransform = owner.GetComponent<RectTransform>();

            if (!m_TargetText)
            {
                m_TargetText = owner.GetComponentInChildren<TMP_Text>();
            }
        }

        /// <summary>
        /// Called when the owner is enabled. Applies auto-fit.
        /// </summary>
        public void OnEnable()
        {
            ApplyAutoFit();
        }
        #endregion

        #region State Transitions
        /// <summary>
        /// Apply color and size transition to the text element based on the selection state.
        /// </summary>
        public void DoStateTransition(TextSelectionState state, bool instant)
        {
            if (!m_TargetText) return;

            Color tintColor = state switch
            {
                TextSelectionState.Normal => m_TextColors.normalColor,
                TextSelectionState.Highlighted => m_TextColors.highlightedColor,
                TextSelectionState.Pressed => m_TextColors.pressedColor,
                TextSelectionState.Selected => m_TextColors.selectedColor,
                TextSelectionState.Disabled => m_TextColors.disabledColor,
                _ => Color.black
            };

            StartTextColorTween(tintColor * m_TextColors.colorMultiplier, instant);

            float targetSize = state switch
            {
                TextSelectionState.Normal => m_FontSizes.normalSize,
                TextSelectionState.Highlighted => m_FontSizes.highlightedSize,
                TextSelectionState.Pressed => m_FontSizes.pressedSize,
                TextSelectionState.Selected => m_FontSizes.selectedSize,
                TextSelectionState.Disabled => m_FontSizes.disabledSize,
                _ => 36f
            };

            StartFontSizeTween(targetSize, instant);
        }

        /// <summary>
        /// Clears the text state instantly.
        /// </summary>
        public void InstantClearState()
        {
            if (!m_TargetText) return;

            StartTextColorTween(Color.white, true);

            if (m_FontSizeTweenCoroutine != null && m_Owner)
            {
                m_Owner.StopCoroutine(m_FontSizeTweenCoroutine);
                m_FontSizeTweenCoroutine = null;
            }

            if (m_FontSizes.normalSize > 0f)
            {
                m_TargetText.fontSize = m_FontSizes.normalSize;
            }
        }
        #endregion

        #region Color Tweening
        private void StartTextColorTween(Color targetColor, bool instant)
        {
            if (!m_TargetText) return;

            m_TargetText.CrossFadeColor(targetColor, instant ? 0f : m_TextColors.fadeDuration, true, true);
        }
        #endregion

        #region Font Size Tweening
        private void StartFontSizeTween(float targetSize, bool instant)
        {
            if (!m_TargetText || !m_Owner) return;

            if (targetSize <= 0f) return;

            if (m_FontSizeTweenCoroutine != null)
            {
                m_Owner.StopCoroutine(m_FontSizeTweenCoroutine);
                m_FontSizeTweenCoroutine = null;
            }

            if (instant || m_FontSizes.sizeDuration <= 0f)
            {
                m_TargetText.fontSize = targetSize;
            }
            else
            {
                m_FontSizeTweenCoroutine = m_Owner.StartCoroutine(TweenFontSize(targetSize));
            }
        }

        private IEnumerator TweenFontSize(float endSize)
        {
            float startSize = m_TargetText.fontSize;
            float elapsedTime = 0f;

            while (elapsedTime < m_FontSizes.sizeDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsedTime / m_FontSizes.sizeDuration);
                m_TargetText.fontSize = Mathf.Lerp(startSize, endSize, t);
                yield return null;
            }

            m_TargetText.fontSize = endSize;
            m_FontSizeTweenCoroutine = null;
        }
        #endregion

        #region Auto-Fit
        public void ApplyAutoFit()
        {
            if (!m_RectTransform || !m_TargetText) return;

            Vector2 textSize;

            switch (m_AutoFitMode)
            {
                case TextAutoFitMode.PreferredSize:
                    textSize = GetTextPreferredSize(m_TargetText.fontSize);
                    break;
                case TextAutoFitMode.MinSize:
                    textSize = GetTextPreferredSize(GetMinFontSize());
                    break;
                case TextAutoFitMode.MaxSize:
                    textSize = GetTextPreferredSize(GetMaxFontSize());
                    break;
                case TextAutoFitMode.None:
                default:
                    return;
            }

            Vector2 newSize = textSize + m_AutoFitPadding;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var rectTransform = m_RectTransform;
                EditorApplication.delayCall += () =>
                {
                    if (rectTransform)
                    {
                        rectTransform.sizeDelta = newSize;
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

        private Vector2 GetTextPreferredSize(float fontSize)
        {
            if (!m_TargetText) return Vector2.zero;

            float originalSize = m_TargetText.fontSize;
            m_TargetText.fontSize = fontSize;
            m_TargetText.ForceMeshUpdate();

            Vector2 preferredSize = m_TargetText.GetPreferredValues();

            m_TargetText.fontSize = originalSize;
            m_TargetText.ForceMeshUpdate();

            return preferredSize;
        }

        private float GetMinFontSize()
        {
            float min = float.MaxValue;

            if (m_FontSizes.normalSize > 0f) min = Mathf.Min(min, m_FontSizes.normalSize);
            if (m_FontSizes.highlightedSize > 0f) min = Mathf.Min(min, m_FontSizes.highlightedSize);
            if (m_FontSizes.pressedSize > 0f) min = Mathf.Min(min, m_FontSizes.pressedSize);
            if (m_FontSizes.selectedSize > 0f) min = Mathf.Min(min, m_FontSizes.selectedSize);
            if (m_FontSizes.disabledSize > 0f) min = Mathf.Min(min, m_FontSizes.disabledSize);

            return min == float.MaxValue ? m_TargetText.fontSize : min;
        }

        private float GetMaxFontSize()
        {
            float max = 0f;

            max = Mathf.Max(max, m_FontSizes.normalSize);
            max = Mathf.Max(max, m_FontSizes.highlightedSize);
            max = Mathf.Max(max, m_FontSizes.pressedSize);
            max = Mathf.Max(max, m_FontSizes.selectedSize);
            max = Mathf.Max(max, m_FontSizes.disabledSize);

            return max == 0f ? m_TargetText.fontSize : max;
        }
        #endregion

        #region Editor Support
        #if UNITY_EDITOR
        /// <summary>
        /// Called from OnValidate to update the helper state.
        /// </summary>
        public void OnValidate(MonoBehaviour owner, TextSelectionState currentState)
        {
            m_TextColors.fadeDuration = Mathf.Max(m_TextColors.fadeDuration, 0.0f);
            m_FontSizes.sizeDuration = Mathf.Max(m_FontSizes.sizeDuration, 0.0f);

            if (!m_TargetText)
            {
                m_TargetText = owner.GetComponentInChildren<TMP_Text>();
            }

            if (m_TargetText)
            {
                StartTextColorTween(Color.white, true);
                DoStateTransition(currentState, true);
                ApplyAutoFit();
            }
        }

        /// <summary>
        /// Called from Reset to find the text component.
        /// </summary>
        public void Reset(MonoBehaviour owner)
        {
            m_TargetText = owner.GetComponentInChildren<TMP_Text>();
        }
        #endif
        #endregion
    }
}
