// Assets/Scripts/Car/ServerAuthNetworkTransform.cs
using Unity.Netcode.Components;

namespace CarDerby.Car
{
    /// <summary>
    /// NetworkTransform где авторитет всегда сервер — независимо от того
    /// кто владеет NetworkObject. Нужно для серверной физики с клиентским владением.
    /// Замени стандартный NetworkTransform на этот компонент на префабе машины.
    /// </summary>
    public class ServerAuthNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => true;
    }
}
