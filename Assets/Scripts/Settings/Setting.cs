using System;

namespace RooseLabs.Settings
{
    public abstract class Setting
    {
        public abstract string DisplayName { get; }
        public abstract SettingCategory Category { get; }
        protected string PrefsKey { get; }

        protected Setting()
        {
            PrefsKey = "Setting_" + GetType().Name.Replace("Setting", "");
        }

        public abstract void Load();

        public abstract void ApplyValue();

        public abstract void Save();
    }

    public abstract class Setting<T> : Setting
    {
        public event Action<T> OnSettingChanged;

        protected T Value { get; set; }

        public virtual T GetValue() => Value;

        public void ApplyValue(T value)
        {
            ApplyValueInternal(ref value);
            Value = value;
            OnSettingChanged?.Invoke(Value);
        }

        public override void ApplyValue() => ApplyValue(Value);

        protected abstract void ApplyValueInternal(ref T value);

        public abstract T GetDefaultValue();
    }
}
