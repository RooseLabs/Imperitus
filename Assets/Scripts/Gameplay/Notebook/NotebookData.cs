using System;
using System.Collections.Generic;

namespace RooseLabs.Gameplay.Notebook
{
    /// <summary>
    /// Data container for assignment information.
    /// Shared across all players
    /// </summary>
    [Serializable]
    public class AssignmentData
    {
        public int assignmentNumber;
        public List<int> tasks = new();
    }

    /// <summary>
    /// Player-specific data about which runes they have collected.
    /// This is NOT synchronized - each player tracks their own runes locally.
    /// </summary>
    [Serializable]
    public class PlayerRuneCollection
    {
        public List<int> collectedRuneIndices = new();
    }

    /// <summary>
    /// Player-specific data about which spells they brought into this heist.
    /// This is NOT synchronized - each player has their own local list.
    /// </summary>
    [Serializable]
    public class PlayerSpellLoadout
    {
        public List<int> equippedSpellIndices = new();
    }
}
