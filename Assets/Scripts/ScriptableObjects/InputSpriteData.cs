using RooseLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "InputSpriteData", menuName = "RooseLabs/Data/InputSpriteData")]
    public class InputSpriteData : SingletonAsset<InputSpriteData>
    {
        public TMP_SpriteAsset kbmSprites;
        public TMP_SpriteAsset xboxSprites;
        public TMP_SpriteAsset psSprites;

        public string GetSpriteTag(InputAction action, InputScheme scheme)
        {
            return GetSpriteTagFromInputPath(GetBindingPath(action, scheme, out _));
        }

        private static string GetBindingPath(InputAction action, InputScheme scheme, out bool hasOverride)
        {
            hasOverride = false;
            foreach (InputBinding binding in action.bindings)
            {
                if (scheme == InputScheme.KeyboardMouse && (binding.effectivePath.StartsWith("<Keyboard>") || binding.effectivePath.StartsWith("<Mouse>")))
                {
                    hasOverride = !string.IsNullOrEmpty(binding.overridePath);
                    return binding.effectivePath;
                }
                if (scheme is InputScheme.Gamepad or InputScheme.Unknown && binding.effectivePath.StartsWith("<Gamepad>"))
                {
                    hasOverride = !string.IsNullOrEmpty(binding.overridePath);
                    return binding.effectivePath;
                }
            }
            return string.Empty;
        }

        private string GetSpriteTagFromInputPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath))
                return string.Empty;

            // Extract the last component of the binding path (e.g., "a" from "<Keyboard>/a")
            return inputPath.Split('/')[^1];
        }

        public TMP_SpriteAsset GetSpriteAssetForInputDevice(InputDevice device)
        {
            if (device is Keyboard or Pointer)
                return kbmSprites;
            if (device is Gamepad)
            {
                switch (InputHandler.GetGamepadType(device))
                {
                    default:
                    case GamepadType.Unknown:
                    case GamepadType.Xbox:
                    case GamepadType.SwitchPro:
                        return xboxSprites;
                    case GamepadType.DualShock:
                    case GamepadType.DualSense:
                        return psSprites;
                }
            }
            return null;
        }
    }
}
