using System.Collections.Generic;
using RooseLabs.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RooseLabs.ScriptableObjects
{
    [CreateAssetMenu(fileName = "InputSpriteData", menuName = "RooseLabs/Data/InputSpriteData")]
    public class InputSpriteData : SingletonAsset<InputSpriteData>
    {
        [SerializeField] private TMP_SpriteAsset kbmSprites;
        [SerializeField] private TMP_SpriteAsset xboxSprites;
        [SerializeField] private TMP_SpriteAsset psSprites;

        public static TMP_SpriteAsset KeyboardMouseSprites => Instance.kbmSprites;
        public static TMP_SpriteAsset XboxSprites => Instance.xboxSprites;
        public static TMP_SpriteAsset PlayStationSprites => Instance.psSprites;

        /// <summary>
        /// Gets the sprite tag for the given input action and input scheme.
        /// </summary>
        /// <returns>The sprite tag for the first binding of the action that matches the input scheme.</returns>
        public static string GetSpriteTag(InputAction action, InputScheme scheme)
        {
            return GetSpriteTagFromInputPath(GetBindingPath(action, scheme, out _));
        }

        /// <summary>
        /// Gets all sprite tags for the given input action and input scheme.
        /// </summary>
        /// <returns>The sprite tags for all the bindings of the action that match the input scheme.</returns>
        public static IEnumerable<string> GetAllSpriteTags(InputAction action, InputScheme scheme)
        {
            foreach (InputBinding binding in action.bindings)
            {
                if (scheme == InputScheme.KeyboardMouse && (binding.effectivePath.StartsWith("<Keyboard>") || binding.effectivePath.StartsWith("<Mouse>")) ||
                    scheme is InputScheme.Gamepad or InputScheme.Unknown && binding.effectivePath.StartsWith("<Gamepad>"))
                {
                    string tag = GetSpriteTagFromInputPath(binding.effectivePath);
                    if (!string.IsNullOrEmpty(tag))
                        yield return tag;
                }
            }
        }

        /// <returns>
        /// The effective path of the first binding of the given action that matches the input scheme.<br/>
        /// If no binding matches the input scheme, returns string.Empty.
        /// </returns>
        /// <remarks>The effective path is the override path if it exists, otherwise the original path.</remarks>
        public static string GetBindingPath(InputAction action, InputScheme scheme, out bool hasOverride)
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

        /// <summary>
        /// Gets the sprite tag from an input binding path.<br/>
        /// In our implementation, this is the last component of the path.
        /// </summary>
        /// <param name="inputPath">The input binding path (e.g., "&lt;Keyboard&gt;/a").</param>
        /// <returns>>The sprite tag (e.g., "a").</returns>
        public static string GetSpriteTagFromInputPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath))
                return string.Empty;

            // Extract the last component of the binding path (e.g., "a" from "<Keyboard>/a")
            return inputPath.Split('/')[^1];
        }

        /// <summary>
        /// Gets the appropriate TextMeshPro Sprite Asset for the given input device.
        /// </summary>
        /// <param name="device">The input device.</param>
        /// <returns>The corresponding sprite asset, or null if none matches.</returns>
        public static TMP_SpriteAsset GetSpriteAssetForInputDevice(InputDevice device)
        {
            if (device is Keyboard or Pointer)
                return KeyboardMouseSprites;
            if (device is Gamepad)
            {
                switch (InputHandler.GetGamepadType(device))
                {
                    default:
                    case GamepadType.Unknown:
                    case GamepadType.Xbox:
                    case GamepadType.SwitchPro:
                        return XboxSprites;
                    case GamepadType.DualShock:
                    case GamepadType.DualSense:
                        return PlayStationSprites;
                }
            }
            return null;
        }
    }
}
