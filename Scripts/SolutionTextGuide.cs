using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// L'inclusion TTS a été retirée ici car elle provoquait l'erreur CS0234

public class SolutionTextGuide : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private float dureeParEtapeS = 7f;

    private List<string> _mouvements;
    private int _index;
    private Coroutine _defilement;

    public void Initialiser(List<string> mouvements)
    {
        _mouvements = mouvements;
        _index = 0;

        // Initialisation TTS désactivée pour corriger la compilation Android

        if (_defilement != null) StopCoroutine(_defilement);
        _defilement = StartCoroutine(DefilementAuto());
    }

    private IEnumerator DefilementAuto()
    {
        string intro = "Reprenez le cube exactement comme à la fin du scan, " +
                       "et gardez-la pendant toute la résolution.";
        instructionText.text = intro;
        Parler(intro);
        yield return new WaitForSeconds(5f);

        while (_index < _mouvements.Count)
        {
            AfficherEtape(_index);
            yield return new WaitForSeconds(dureeParEtapeS);
            _index++;
        }

        string fin = "Cube résolu ! Félicitations.";
        instructionText.text = fin;
        Parler(fin);
    }

    private void AfficherEtape(int i)
    {
        string mvt = _mouvements[i];
        string description = DescriptionMouvement(mvt);
        string texte = $"Étape {i + 1}/{_mouvements.Count}\n\n{description}\n\nMouvement : {mvt}";

        instructionText.text = texte;

        // Texte parlé : version simplifiée sans les sauts de ligne
        string texteParlé = $"Étape {i + 1} sur {_mouvements.Count}. {description}.";
        Parler(texteParlé);
    }

    private void Parler(string texte)
    {
        // On redirige temporairement tout dans le Debug.Log pour éviter les erreurs de build
        Debug.Log($"[TTS Log] {texte}");
    }

    private string DescriptionMouvement(string mvt)
    {
        string baseFace = mvt.TrimEnd('\'', '2');
        string suffixe = mvt.Substring(baseFace.Length);
        string nomFace = baseFace switch
        {
            "U" => "face du dessus",
            "D" => "face du dessous",
            "F" => "face avant",
            "B" => "face arrière",
            "R" => "face droite",
            "L" => "face gauche",
            _   => baseFace,
        };
        string sens = suffixe switch
        {
            "'" => "quart de tour horaire",
            "2" => "demi-tour",
            _   => "quart de tour anti-horaire",
        };
        return $"Tournez la {nomFace}, {sens}";
    }

    private void OnDestroy()
    {
        // Nettoyage TTS désactivé
    }
}