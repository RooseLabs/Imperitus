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
        public virtual int Precision => 2;

        public virtual string FormatValue(float value)
        {
            return value.ToString(Precision == 0 ? "F0" : $"F{Precision}");
        }

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
