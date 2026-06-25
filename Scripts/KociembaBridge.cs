using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Construit la chaîne de 54 caractères attendue par le solveur, puis appelle
/// le portage gratuit Megalomatt/Kociemba récupéré à la Partie 3.3.
/// </summary>
public static class KociembaBridge
{
    public static string ConstruireChaineKociemba(
        Dictionary<string, string[][]> faces,
        Dictionary<string, string> mappingCouleurVersFace)
    {
        var chaine = new System.Text.StringBuilder(54);
        foreach (var faceId in CubeMapping.OrdreFacesKociemba)
        foreach (var ligne in faces[faceId])
        foreach (var couleur in ligne)
        {
            if (!mappingCouleurVersFace.TryGetValue(couleur, out string faceKociemba))
                throw new System.Exception($"Couleur '{couleur}' absente du mapping.");
            chaine.Append(faceKociemba);
        }
        return chaine.ToString();
    }

    public static (bool succes, string solution, string erreur) ResoudreCube(
        Dictionary<string, string[][]> faces,
        Dictionary<string, string> mappingCouleurVersFace)
    {
        var (valide, msg) = CubeValidator.ValiderCube(faces);
        if (!valide) return (false, "", $"Validation échouée : {msg}");

        string cubeString;
        try { cubeString = ConstruireChaineKociemba(faces, mappingCouleurVersFace); }
        catch (System.Exception exc) { return (false, "", exc.Message); }

        string resultat;
        try
        {
            // <<< Point d'intégration du solveur Megalomatt/Kociemba (Partie 3.3) >>>
            // Si Unity signale que "SearchRunTime" n'existe pas, ouvrez les fichiers
            // .cs du dépôt téléchargé pour vérifier le nom exact de la classe/namespace
            // (il peut varier légèrement selon la version du dépôt).
            resultat = Kociemba.SearchRunTime.solution(cubeString, out string info, buildTables: true);
        }
        catch (System.Exception exc)
        {
            return (false, "", $"Erreur Kociemba (chaîne : '{cubeString}') : {exc.Message}");
        }

        if (string.IsNullOrEmpty(resultat) || resultat.StartsWith("Error"))
            return (false, "", $"Résolution impossible (chaîne : '{cubeString}') : {resultat}\n" +
                                "Vérifiez que les 6 faces correspondent bien au même cube physique.");

        return (true, resultat.Trim(), "");
    }

    public static List<string> DecompterMouvements(string solution) =>
        solution.Split(' ').Where(m => m.Trim().Length > 0).ToList();
}