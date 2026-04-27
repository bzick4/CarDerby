// Assets/Scripts/Combat/IWeapon.cs
using UnityEngine;

namespace CarDerby.Combat
{
    public interface IWeapon
    {
        float Damage   { get; }
        float FireRate { get; }
        bool  CanFire  { get; }

        /// <summary>Rotate weapon toward a world-space point (owner client calls this).</summary>
        void AimAt(Vector3 worldPoint);

        /// <summary>Request a shot (owner client; routed to server via ServerRpc in base class).</summary>
        void Fire();
    }
}
