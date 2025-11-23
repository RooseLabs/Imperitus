using RooseLabs.Core;
using RooseLabs.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.UI.Elements
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class InlineButtonPrompt : MonoBehaviour
    {
        [Tooltip("")]
        [SerializeField] private InputActionReference[] inputActions;

        private TMP_Text m_text;
        private string m_originalText;

        private void Awake()
        {
            m_text = GetComponent<TMP_Text>();
            m_originalText = m_text.text;
        }

        private void OnEnable()
        {
            InputHandler.Instance.InputSchemeChanged += UpdateText;
            InputHandler.Instance.InputDeviceChanged += UpdateSprites;
            UpdateText(InputHandler.CurrentInputScheme);
            UpdateSprites(InputHandler.CurrentInputDevice);
        }

        private void OnDisable()
        {
            InputHandler.Instance.InputSchemeChanged -= UpdateText;
            InputHandler.Instance.InputDeviceChanged -= UpdateSprites;
        }

        private void UpdateText(InputScheme scheme)
        {
            // Collect sprite tags for all input actions
            object[] spriteMarkups = new object[inputActions.Length];

            for (int i = 0; i < inputActions.Length; i++)
            {
                string spriteTag = InputSpriteData.Instance.GetSpriteTag(inputActions[i], scheme);

                if (!string.IsNullOrEmpty(spriteTag))
                {
                    spriteMarkups[i] = $"<sprite name=\"{spriteTag}\">";
                }
                else
                {
                    spriteMarkups[i] = $"{{{i}}}"; // Keep placeholder if no sprite found
                }
            }

            m_text.text = string.Format(m_originalText, spriteMarkups);
        }

        private void UpdateSprites(InputDevice device)
        {
            m_text.spriteAsset = InputSpriteData.Instance.GetSpriteAssetForInputDevice(device);
        }
    }
}
