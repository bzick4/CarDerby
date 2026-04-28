// Assets/Scripts/Gameplay/MatchData.cs
using System.Collections.Generic;

namespace CarDerby.Gameplay
{
    /// <summary>
    /// Static container that survives scene transitions without DontDestroyOnLoad.
    /// LobbyUI fills it before loading GameScene; PlayerSpawner reads it on spawn.
    /// </summary>
    public static class MatchData
    {
        public struct PlayerLoadout
        {
            public ulong ClientId;
            public int   CarIndex;
            public int   WeaponIndex;
            public int   ScoopIndex;   // -1 = none
        }

        private static readonly Dictionary<ulong, PlayerLoadout> _loadouts = new();

        public static IReadOnlyDictionary<ulong, PlayerLoadout> Loadouts => _loadouts;

        public static void Clear() => _loadouts.Clear();

        public static void SetPlayerLoadout(ulong clientId, int carIndex, int weaponIndex, int scoopIndex)
        {
            _loadouts[clientId] = new PlayerLoadout
            {
                ClientId    = clientId,
                CarIndex    = carIndex,
                WeaponIndex = weaponIndex,
                ScoopIndex  = scoopIndex,
            };
        }

        public static bool TryGetLoadout(ulong clientId, out PlayerLoadout loadout)
            => _loadouts.TryGetValue(clientId, out loadout);
    }
}
