using System.Collections.Generic;
using System.Linq;

public static class CubeMapping
{
    public static readonly string[] OrdreFacesKociemba = { "U", "R", "F", "D", "L", "B" };
    public static readonly HashSet<string> CouleursValides = new()
        { "BLANC", "ROUGE", "VERT", "JAUNE", "ORANGE", "BLEU" };

    public static (bool succes, Dictionary<string,string> mapping, string erreur)
        ConstruireMappingDepuisCentres(Dictionary<string, string[]> facesScannees)
    {
        var mapping = new Dictionary<string, string>();
        var centresVus = new Dictionary<string, string>();

        foreach (var (faceSpatiale, couleurs9) in facesScannees)
        {
            if (couleurs9.Length != 9)
                return (false, null, $"Face '{faceSpatiale}' : {couleurs9.Length} couleurs au lieu de 9.");

            string couleurCentre = couleurs9[4];
            if (!CouleursValides.Contains(couleurCentre))
                return (false, null, $"Centre de la face '{faceSpatiale}' non reconnu : '{couleurCentre}'.");

            if (centresVus.ContainsKey(couleurCentre))
                return (false, null, $"Couleur de centre '{couleurCentre}' présente sur deux faces : " +
                                      $"'{centresVus[couleurCentre]}' et '{faceSpatiale}'.");

            centresVus[couleurCentre] = faceSpatiale;
            mapping[couleurCentre] = faceSpatiale;
        }

        if (mapping.Count != 6)
        {
            var manquantes = OrdreFacesKociemba.Except(mapping.Values);
            return (false, null, $"Mapping incomplet : faces Kociemba manquantes : {string.Join(",", manquantes)}.");
        }
        return (true, mapping, "");
    }
}