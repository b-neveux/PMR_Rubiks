using System;
using System.Collections.Generic;

public static class CubeAssembler
{
    // Nombre de quarts de tour HORAIRE appliqués à la lecture brute de chaque face
    // avant assemblage. Par défaut, seule B a une correction (180°). Ces valeurs
    // sont testables une par une via AppStateMachine (touches 1-6), sans rescanner.
    public static int CorrectionU = 2;
    public static int CorrectionR = 2;
    public static int CorrectionF = 2;
    public static int CorrectionD = 2;
    public static int CorrectionL = 2;
    public static int CorrectionB = 0;

    public static string[][] ListeVersMatrice(string[] couleurs9)
    {
        if (couleurs9.Length != 9) throw new ArgumentException($"Attendu 9 éléments, reçu {couleurs9.Length}.");
        return new[]
        {
            new[]{ couleurs9[0], couleurs9[1], couleurs9[2] },
            new[]{ couleurs9[3], couleurs9[4], couleurs9[5] },
            new[]{ couleurs9[6], couleurs9[7], couleurs9[8] },
        };
    }

    private static string[][] RotationCw(string[][] m) => new[]
    {
        new[]{ m[2][0], m[1][0], m[0][0] },
        new[]{ m[2][1], m[1][1], m[0][1] },
        new[]{ m[2][2], m[1][2], m[0][2] },
    };

    public static string[][] AppliquerRotation(string[][] m, int nbQuarts)
    {
        nbQuarts = ((nbQuarts % 4) + 4) % 4;
        for (int i = 0; i < nbQuarts; i++) m = RotationCw(m);
        return m;
    }

    public static Dictionary<string, string[][]> ConstruireFaces(Dictionary<string, string[]> facesScannees)
    {
        var faces = new Dictionary<string, string[][]>();
        var corrections = new Dictionary<string, int>
        {
            ["U"] = CorrectionU, ["R"] = CorrectionR, ["F"] = CorrectionF,
            ["D"] = CorrectionD, ["L"] = CorrectionL, ["B"] = CorrectionB,
        };
        foreach (var faceId in CubeMapping.OrdreFacesKociemba)
        {
            if (!facesScannees.ContainsKey(faceId))
                throw new KeyNotFoundException($"Données manquantes pour la face '{faceId}'.");
            var matrice = ListeVersMatrice(facesScannees[faceId]);
            faces[faceId] = AppliquerRotation(matrice, corrections[faceId]);
        }
        return faces;
    }
}