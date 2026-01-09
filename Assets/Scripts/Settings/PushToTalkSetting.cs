namespace RooseLabs.Settings
{
    public enum PushToTalkMode
    {
        Off,
        PushToTalk,
        PushToMute
    }

    public class PushToTalkSetting : EnumSetting<PushToTalkMode>
    {
        public override string DisplayName => "Push to Talk";
        public override SettingCategory Category => SettingCategory.Audio;

        public override PushToTalkMode GetDefaultValue() => PushToTalkMode.Off;

        protected override void ApplyValueInternal(ref PushToTalkMode value)
        {
            // Push to talk is managed by the voice chat system
        }
    }
}
