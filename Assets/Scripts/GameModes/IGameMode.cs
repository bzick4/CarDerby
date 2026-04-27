// Assets/Scripts/GameModes/IGameMode.cs
namespace CarDerby.GameModes
{
    public interface IGameMode
    {
        bool IsMatchActive { get; }

        void StartMatch();
        void EndMatch();

        /// <param name="killerId">OwnerClientId of whoever scored the kill; ulong.MaxValue = environment.</param>
        void OnPlayerDied(ulong playerId, ulong killerId);

        void OnPlayerConnected(ulong playerId);
        void OnPlayerDisconnected(ulong playerId);
    }
}
