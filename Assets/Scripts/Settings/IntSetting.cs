using UnityEngine;

namespace RooseLabs.Settings
{
    public abstract class IntSetting : Setting<int>
    {
        public override void Load()
        {
            Value = PlayerPrefs.GetInt(PrefsKey, GetDefaultValue());
        }

        public override void Save()
        {
            PlayerPrefs.SetInt(PrefsKey, GetValue());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns human-readable strings for all choices.
        /// </summary>
        public abstract string[] GetChoices();
    }
}
