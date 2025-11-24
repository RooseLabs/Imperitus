using System;
using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using RooseLabs.Player;

namespace RooseLabs.Network
{
    public static class PlayerHandler
    {
        private static readonly Dictionary<NetworkConnection, PlayerConnection> Players = new();
        private static readonly Dictionary<NetworkConnection, PlayerCharacter> Characters = new();

        private static NetworkObject[] s_allCharacterNetworkObjects = Array.Empty<NetworkObject>();
        private static bool s_charactersDirty = true;

        public static void RegisterPlayer(NetworkConnection conn, PlayerConnection playerConnection)
        {
            Players[conn] = playerConnection;
        }

        public static void UnregisterPlayer(NetworkConnection conn)
        {
            Players.Remove(conn);
        }

        public static void RegisterCharacter(NetworkConnection conn, PlayerCharacter playerCharacter)
        {
            Characters[conn] = playerCharacter;
            s_charactersDirty = true;
        }

        public static void UnregisterCharacter(NetworkConnection conn)
        {
            Characters.Remove(conn);
            s_charactersDirty = true;
        }

        public static PlayerConnection GetPlayer(NetworkConnection conn) => Players.GetValueOrDefault(conn);
        public static PlayerCharacter GetCharacter(NetworkConnection conn) => Characters.GetValueOrDefault(conn);

        public static IReadOnlyCollection<PlayerConnection> AllPlayers => Players.Values;
        public static IReadOnlyCollection<PlayerCharacter> AllCharacters => Characters.Values;
        public static IEnumerable<PlayerConnection> AllConnectedPlayers => Players.Values.Where(p => p.Owner.IsActive);
        public static IEnumerable<PlayerCharacter> AllConnectedCharacters => Characters.Values.Where(c => c.Owner.IsActive);

        public static NetworkObject[] CharacterNetworkObjects
        {
            get
            {
                if (s_charactersDirty)
                {
                    s_allCharacterNetworkObjects = new NetworkObject[Characters.Count];
                    int index = 0;
                    foreach (var character in Characters.Values)
                    {
                        s_allCharacterNetworkObjects[index++] = character.NetworkObject;
                    }
                    s_charactersDirty = false;
                }
                return s_allCharacterNetworkObjects;
            }
        }
    }
}
