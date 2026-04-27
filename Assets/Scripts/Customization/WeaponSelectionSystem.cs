// Assets/Scripts/Customization/WeaponSelectionSystem.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using CarDerby.SO;

namespace CarDerby.Customization
{
    /// <summary>
    /// Holds the roster of available weapons and scoops as WeaponDataSO assets.
    /// Roof weapons and front scoops live in the same list — differentiated by IsFrontScoop.
    /// Populate _weaponRoster in the Inspector by dragging WeaponDataSO assets.
    /// </summary>
    public class WeaponSelectionSystem : MonoBehaviour
    {
        [SerializeField] private List<WeaponDataSO> _weaponRoster = new();

        // Cached sub-lists built once in Awake
        private readonly List<WeaponDataSO> _roofWeapons = new();
        private readonly List<WeaponDataSO> _scoops      = new();

        private int _weaponIdx;
        private int _scoopIdx = -1; // -1 = no scoop

        public WeaponDataSO SelectedWeapon => _roofWeapons.Count > 0 ? _roofWeapons[_weaponIdx] : null;
        public WeaponDataSO SelectedScoop  => _scoopIdx >= 0 && _scoopIdx < _scoops.Count
                                                ? _scoops[_scoopIdx]
                                                : null;

        public IReadOnlyList<WeaponDataSO> RoofWeapons => _roofWeapons;
        public IReadOnlyList<WeaponDataSO> Scoops      => _scoops;

        public event Action<WeaponDataSO> OnWeaponChanged;
        public event Action<WeaponDataSO> OnScoopChanged;

        private void Awake()
        {
            foreach (var w in _weaponRoster)
            {
                if (w == null) continue;
                if (w.IsFrontScoop) _scoops.Add(w);
                else                _roofWeapons.Add(w);
            }
        }

        // ── Weapon cycling ───────────────────────────────────────────────────

        public void SelectWeapon(int index)
        {
            if (_roofWeapons.Count == 0) return;
            _weaponIdx = Mathf.Clamp(index, 0, _roofWeapons.Count - 1);
            OnWeaponChanged?.Invoke(SelectedWeapon);
        }

        public void SelectWeaponNext() => SelectWeapon((_weaponIdx + 1) % Mathf.Max(1, _roofWeapons.Count));
        public void SelectWeaponPrev() => SelectWeapon((_weaponIdx - 1 + Mathf.Max(1, _roofWeapons.Count)) % Mathf.Max(1, _roofWeapons.Count));

        // ── Scoop cycling ────────────────────────────────────────────────────

        /// <summary>index -1 means no scoop equipped.</summary>
        public void SelectScoop(int index)
        {
            _scoopIdx = Mathf.Clamp(index, -1, _scoops.Count - 1);
            OnScoopChanged?.Invoke(SelectedScoop);
        }

        public void SelectScoopNext() => SelectScoop(_scoopIdx + 1 > _scoops.Count - 1 ? -1 : _scoopIdx + 1);
        public void SelectScoopPrev() => SelectScoop(_scoopIdx - 1 < -1 ? _scoops.Count - 1 : _scoopIdx - 1);

        // ── Prefab access ────────────────────────────────────────────────────

        public GameObject GetWeaponPrefab(int index) =>
            index >= 0 && index < _roofWeapons.Count ? _roofWeapons[index].WeaponPrefab : null;

        public GameObject GetScoopPrefab(int index) =>
            index >= 0 && index < _scoops.Count ? _scoops[index].WeaponPrefab : null;
    }
}
