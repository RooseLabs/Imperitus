using UnityEngine;

namespace RooseLabs.Settings
{
    public abstract class BoolSetting : Setting<bool>
    {
        public override void Load()
        {
            Value = PlayerPrefs.GetInt(PrefsKey, GetDefaultValue() ? 1 : 0) == 1;
        }

        public override void Save()
        {
            PlayerPrefs.SetInt(PrefsKey, GetValue() ? 1 : 0);
            PlayerPrefs.Save();
        }

        public virtual string[] GetChoices()
        {
            return new[] { "Off", "On" };
        }
    }
}
