using System;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Simple data container for quest information.
    /// Shared across all players in the heist.
    /// </summary>
    [Serializable]
    public class QuestData
    {
        public string questTitle;
        public string questDescription;
        public List<ObjectiveData> objectives = new();
    }

    /// <summary>
    /// Individual objective within a quest.
    /// </summary>
    [Serializable]
    public class ObjectiveData
    {
        public string description;
        public bool isCompleted;
    }

    /// <summary>
    /// Player-specific data about which spells they brought into this heist.
    /// This is NOT synchronized - each player has their own local list.
    /// </summary>
    [Serializable]
    public class PlayerSpellLoadout
    {
        public List<int> equippedSpellIndices = new();

        public PlayerSpellLoadout()
        {
            equippedSpellIndices = new List<int>();
        }
    }
}