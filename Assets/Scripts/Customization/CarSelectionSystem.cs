// Assets/Scripts/Customization/CarSelectionSystem.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using CarDerby.SO;

namespace CarDerby.Customization
{
    /// <summary>
    /// Holds the roster of available cars as CarDataSO assets and tracks
    /// the local player's current selection.
    /// Populate _carRoster in the Inspector by dragging CarDataSO assets.
    /// </summary>
    public class CarSelectionSystem : MonoBehaviour
    {
        [SerializeField] private List<CarDataSO> _carRoster = new();

        public IReadOnlyList<CarDataSO> Cars  => _carRoster;
        public int                      Count => _carRoster.Count;

        private int _selectedIndex;

        public int       SelectedIndex => _selectedIndex;
        public CarDataSO SelectedCar   => _carRoster.Count > 0 ? _carRoster[_selectedIndex] : null;

        public event Action<CarDataSO> OnSelectionChanged;

        // ── Selection API ────────────────────────────────────────────────────

        public void SelectCar(int index)
        {
            if (_carRoster.Count == 0) return;
            _selectedIndex = Mathf.Clamp(index, 0, _carRoster.Count - 1);
            OnSelectionChanged?.Invoke(SelectedCar);
        }

        public void SelectNext() => SelectCar((_selectedIndex + 1) % _carRoster.Count);
        public void SelectPrev() => SelectCar((_selectedIndex - 1 + _carRoster.Count) % _carRoster.Count);

        // ── Prefab access ────────────────────────────────────────────────────

        /// <summary>Returns the NetworkPrefab for the car at the given roster index.</summary>
        public GameObject GetPrefab(int index)
        {
            if (index < 0 || index >= _carRoster.Count) return null;
            return _carRoster[index].NetworkPrefab;
        }
    }
}
