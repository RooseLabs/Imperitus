using System;
using System.Collections.Generic;
using RooseLabs.ScriptableObjects;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    public class RuneObjectSpawnPoint : ObjectSpawnPoint
    {
        [Serializable]
        private enum Mode
        {
            Any,
            Blacklist,
            Whitelist
        }

        [Tooltip("Mode for filtering the runes that can be spawned at this spawn point.\n\n" +
                 "<b>Any</b>: Any rune from the database can be spawned.\n" +
                 "<b>Blacklist</b>: All runes from the database except the specified ones can be spawned.\n" +
                 "<b>Whitelist</b>: Only the specified runes can be spawned.")]
        [SerializeField] private Mode mode;
        [SerializeField] private RuneSO[] runes = Array.Empty<RuneSO>();

        public IEnumerable<RuneSO> GetPossibleRunes()
        {
            if (!GameManager.Instance || !GameManager.Instance.RuneDatabase)
            {
                Debug.LogWarning("[RuneObjectSpawnPoint] GameManager or RuneDatabase is null.");
                return Array.Empty<RuneSO>();
            }
            if (runes.Length == 0 && mode != Mode.Any)
            {
                Debug.LogWarning("[RuneObjectSpawnPoint] No runes specified for filtering. Will allow any rune.");
                return GameManager.Instance.RuneDatabase;
            }
            switch (mode)
            {
                case Mode.Blacklist:
                    var blacklistSet = new HashSet<RuneSO>(runes);
                    blacklistSet.Remove(null);
                    var filteredBlacklist = new List<RuneSO>();
                    foreach (var rune in GameManager.Instance.RuneDatabase)
                    {
                        if (!blacklistSet.Contains(rune))
                            filteredBlacklist.Add(rune);
                    }
                    return filteredBlacklist;
                case Mode.Whitelist:
                    var whitelistSet = new HashSet<RuneSO>(runes);
                    whitelistSet.Remove(null);
                    var filteredWhitelist = new List<RuneSO>();
                    foreach (var rune in GameManager.Instance.RuneDatabase)
                    {
                        if (whitelistSet.Contains(rune))
                            filteredWhitelist.Add(rune);
                    }
                    return filteredWhitelist;
                case Mode.Any:
                default:
                    return GameManager.Instance.RuneDatabase;
            }
        }

        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (mode == Mode.Any)
            {
                runes = Array.Empty<RuneSO>();
            }
        }
        #endif
    }
}
