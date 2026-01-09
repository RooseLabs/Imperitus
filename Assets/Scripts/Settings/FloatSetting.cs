using UnityEngine;

namespace RooseLabs.Settings
{
    public abstract class FloatSetting : Setting<float>
    {
        protected abstract float MinValue { get; }
        protected abstract float MaxValue { get; }
        public virtual float ExposedMinValue => MinValue;
        public virtual float ExposedMaxValue => MaxValue;
        protected virtual bool ClampOnLoad => true;

        public override void Load()
        {
            float loaded = PlayerPrefs.GetFloat(PrefsKey, GetDefaultValue());
            Value = ClampOnLoad ? Mathf.Clamp(loaded, MinValue, MaxValue) : loaded;
        }

        public override void Save()
        {
            PlayerPrefs.SetFloat(PrefsKey, GetValue());
            PlayerPrefs.Save();
        }
    }
}
