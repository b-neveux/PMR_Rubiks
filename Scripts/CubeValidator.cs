using System.Collections.Generic;
using System.Linq;

public static class CubeValidator
{
    public const int FacettesParCouleur = 9;

    public static (bool valide, string message) ValiderCube(Dictionary<string, string[][]> faces)
    {
        var toutesFacettes = new List<string>();
        foreach (var faceId in CubeMapping.OrdreFacesKociemba)
            foreach (var ligne in faces[faceId])
                toutesFacettes.AddRange(ligne);

        if (toutesFacettes.Count != 54)
            return (false, $"Le cube contient {toutesFacettes.Count} facettes (attendu 54).");

        var compteur = toutesFacettes.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());

        var inconnues = compteur.Keys.Except(CubeMapping.CouleursValides).ToList();
        if (inconnues.Count > 0)
            return (false, $"Couleurs invalides détectées : {string.Join(",", inconnues)}.");

        var erreurs = new List<string>();
        foreach (var couleur in CubeMapping.CouleursValides)
        {
            int n = compteur.GetValueOrDefault(couleur, 0);
            if (n != FacettesParCouleur) erreurs.Add($"  {couleur} : {n} facette(s) (attendu 9)");
        }
        if (erreurs.Count > 0) return (false, "Décompte incorrect :\n" + string.Join("\n", erreurs));

        var centres = new Dictionary<string, string>();
        foreach (var faceId in CubeMapping.OrdreFacesKociemba)
        {
            string centreCouleur = faces[faceId][1][1];
            if (centres.ContainsValue(centreCouleur))
            {
                var deja = centres.Where(kv => kv.Value == centreCouleur).Select(kv => kv.Key).Append(faceId);
                return (false, $"Centre '{centreCouleur}' présent sur plusieurs faces : {string.Join(",", deja)}.");
            }
            centres[faceId] = centreCouleur;
        }
        return (true, "");
    }
}