using System;
using System.Collections.Generic;
using UnityEngine;

namespace RooseLabs.Gameplay
{
    /// <summary>
    /// Data container for assignment information.
    /// Shared across all players but remains static during gameplay.
    /// No need for network synchronization since it doesn't change.
    /// </summary>
    [Serializable]
    public class AssignmentData
    {
        public int assignmentNumber;
        public List<AssignmentTask> tasks = new();
    }

    /// <summary>
    /// Individual task within an assignment.
    /// Contains the description and visual representation.
    /// </summary>
    [Serializable]
    public class AssignmentTask
    {
        public string description;
        public Sprite taskImage;
    }

    /// <summary>
    /// Player-specific data about which runes they have collected.
    /// This is NOT synchronized - each player tracks their own runes locally.
    /// Stores indices into GameManager.AllRunes array.
    /// </summary>
    [Serializable]
    public class PlayerRuneCollection
    {
        public List<int> collectedRuneIndices = new();
        public PlayerRuneCollection()
        {
            collectedRuneIndices = new List<int>();
        }
    }

    /// <summary>
    /// Player-specific data about which spells they brought into this heist.
    /// This is NOT synchronized - each player has their own local list.
    /// Stores indices into GameManager.AllSpells array.
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