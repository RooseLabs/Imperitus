using RooseLabs.Core;
using RooseLabs.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.UI.Elements
{
    [AddComponentMenu("RooseLabs/UI/Button Prompt")]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class ButtonPrompt : MonoBehaviour
    {
        [SerializeField] private InputActionReference inputAction;

        private TMP_Text m_text;

        private void Awake()
        {
            m_text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            InputHandler.Instance.InputSchemeChanged += UpdateText;
            InputHandler.Instance.InputDeviceChanged += UpdateSprite;
            UpdateSprite(InputHandler.CurrentInputDevice);
            UpdateText(InputHandler.CurrentInputScheme);
        }

        private void OnDisable()
        {
            InputHandler.Instance.InputSchemeChanged -= UpdateText;
            InputHandler.Instance.InputDeviceChanged -= UpdateSprite;
        }

        private void UpdateText(InputScheme scheme)
        {
            string spriteTag = InputSpriteData.GetSpriteTag(inputAction.action, scheme);

            if (!string.IsNullOrEmpty(spriteTag))
            {
                m_text.text = $"<sprite name=\"{spriteTag}\">";
            }
            else
            {
                Debug.LogWarning($"No sprite tag found for action '{inputAction.action.name}' on scheme {scheme}", this);
                m_text.text = "";
            }
        }

        private void UpdateSprite(InputDevice device)
        {
            m_text.spriteAsset = InputSpriteData.GetSpriteAssetForInputDevice(device);
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (!inputAction || !InputSpriteData.Instance) return;
            if (TryGetComponent(out TMP_Text textComponent))
            {
                string spriteTag = InputSpriteData.GetSpriteTag(inputAction.action, InputScheme.KeyboardMouse);
                if (!string.IsNullOrEmpty(spriteTag))
                {
                    textComponent.text = $"<sprite name=\"{spriteTag}\">";
                    textComponent.spriteAsset = InputSpriteData.KeyboardMouseSprites;
                }
                else
                {
                    textComponent.text = "";
                }
            }
        }
        #endif
    }
}
