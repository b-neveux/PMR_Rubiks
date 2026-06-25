using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class ColorPalette : ScriptableObject
{
    [System.Serializable] public struct Entry { public string nom; public Color couleur; }
    [SerializeField] private Entry[] entries;
    private Dictionary<string, Color> _map;

    public Color GetColor(string nom)
    {
        if (_map == null)
        {
            _map = new Dictionary<string, Color>();
            foreach (var e in entries) _map[e.nom] = e.couleur;
        }
        return _map.TryGetValue(nom, out var c) ? c : Color.gray;
    }
}