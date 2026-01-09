using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RooseLabs.Settings
{
    public abstract class EnumSetting<TEnum> : Setting<TEnum> where TEnum : Enum
    {
        public override void Load()
        {
            int loaded = PlayerPrefs.GetInt(PrefsKey, Convert.ToInt32(GetDefaultValue()));
            Value = (TEnum)Enum.ToObject(typeof(TEnum), loaded);
        }

        public override void Save()
        {
            PlayerPrefs.SetInt(PrefsKey, Convert.ToInt32(GetValue()));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Returns human-readable strings for all enum choices.
        /// </summary>
        public virtual string[] GetChoices()
        {
            return Enum.GetNames(typeof(TEnum))
                .Select(name => name == "None" ? "Off" : SplitCamelCase(name))
                .ToArray();
        }

        private static string SplitCamelCase(string input)
        {
            return Regex.Replace(
                Regex.Replace(
                    input,
                    @"(\P{Ll})(\P{Ll}\p{Ll})",
                    "$1 $2"
                ),
                @"(\p{Ll})(\P{Ll})",
                "$1 $2"
            );
        }
    }
}
