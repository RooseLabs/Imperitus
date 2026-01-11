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
        [SerializeField] private bool useSchemeSpecificActions;
        [SerializeField] private InputActionReference inputAction;

        [Header("Scheme-Specific Actions")]
        [SerializeField] private InputActionReference keyboardMouseAction;
        [SerializeField] private InputActionReference gamepadAction;

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
            InputActionReference actionToUse = GetActionForScheme(scheme);

            if (!actionToUse)
            {
                m_text.text = "";
                return;
            }

            string spriteTag = InputSpriteData.GetSpriteTag(actionToUse.action, scheme);

            if (!string.IsNullOrEmpty(spriteTag))
            {
                m_text.text = $"<sprite name=\"{spriteTag}\">";
            }
            else
            {
                Debug.Log($"No sprite tag found for action '{actionToUse.action.name}' on scheme {scheme}", this);
                m_text.text = "";
            }
        }

        private InputActionReference GetActionForScheme(InputScheme scheme)
        {
            if (useSchemeSpecificActions)
            {
                return scheme switch
                {
                    InputScheme.KeyboardMouse => keyboardMouseAction,
                    InputScheme.Gamepad => gamepadAction,
                    _ => inputAction
                };
            }

            return inputAction;
        }

        private void UpdateSprite(InputDevice device)
        {
            m_text.spriteAsset = InputSpriteData.GetSpriteAssetForInputDevice(device);
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            InputActionReference actionToUse = useSchemeSpecificActions ? keyboardMouseAction : inputAction;

            if (!actionToUse || !InputSpriteData.Instance) return;
            if (TryGetComponent(out TMP_Text textComponent))
            {
                string spriteTag = InputSpriteData.GetSpriteTag(actionToUse.action, InputScheme.KeyboardMouse);
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
