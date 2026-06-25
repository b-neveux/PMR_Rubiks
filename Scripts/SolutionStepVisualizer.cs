using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class SolutionStepVisualizer : MonoBehaviour
{
    [SerializeField] private Cube3DView cubeAvant;
    [SerializeField] private Cube3DView cubeApres;
    [SerializeField] private TextMeshProUGUI labelEtape;

    private List<string> _mouvements;
    private List<Dictionary<string, string[][]>> _etats;
    private int _indexCourant;

    public void Initialiser(Dictionary<string, string[][]> etatInitial, List<string> mouvements)
    {
        _mouvements = mouvements;
        _etats = new List<Dictionary<string, string[][]>> { etatInitial };
        var courant = etatInitial;
        foreach (var m in mouvements)
        {
            courant = CubeMoveSimulator.AppliquerMouvement(courant, m);
            _etats.Add(courant);
        }
        AfficherEtape(0);
    }

    public void EtapeSuivante() => AfficherEtape(Mathf.Min(_indexCourant + 1, _mouvements.Count - 1));
    public void EtapePrecedente() => AfficherEtape(Mathf.Max(_indexCourant - 1, 0));

    private void AfficherEtape(int index)
    {
        _indexCourant = index;
        cubeAvant.AppliquerEtat(_etats[index]);
        cubeApres.AppliquerEtat(_etats[index + 1]);
        string mvt = _mouvements[index];
        labelEtape.text = $"Étape {index + 1}/{_mouvements.Count} : {mvt} ({DescriptionMouvement(mvt)})";
    }

    private string DescriptionMouvement(string mvt)
    {
        string suffixe = mvt.Length > 1 ? mvt.Substring(1) : "";
        return suffixe switch
        {
            "'" => "90° anti-horaire",
            "2" => "180° demi-tour",
            _   => "90° horaire",
        };
    }
}