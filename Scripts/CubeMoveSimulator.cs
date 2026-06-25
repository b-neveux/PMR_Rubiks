using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Simule l'effet d'un mouvement (R, U', F2, etc.) sur un état de cube,
/// pour pouvoir calculer l'état "avant/après" de chaque étape de la solution
/// sans avoir besoin de re-scanner physiquement le cube.
/// </summary>
public static class CubeMoveSimulator
{
    private static string[][] DeepCopy(string[][] m) => m.Select(row => (string[])row.Clone()).ToArray();

    private static string[][] RotationCw(string[][] m) => new[]
    {
        new[]{ m[2][0], m[1][0], m[0][0] },
        new[]{ m[2][1], m[1][1], m[0][1] },
        new[]{ m[2][2], m[1][2], m[0][2] },
    };

    private static void QuartCw(Dictionary<string, string[][]> f, string baseFace)
    {
        switch (baseFace)
        {
            case "U":
                f["U"] = RotationCw(f["U"]);
                var tF = (string[])f["F"][0].Clone(); var tR = (string[])f["R"][0].Clone();
                var tB = (string[])f["B"][0].Clone(); var tL = (string[])f["L"][0].Clone();
                f["F"][0] = tR; f["R"][0] = tB; f["B"][0] = tL; f["L"][0] = tF;
                break;
            case "D":
                f["D"] = RotationCw(f["D"]);
                var dF = (string[])f["F"][2].Clone(); var dR = (string[])f["R"][2].Clone();
                var dB = (string[])f["B"][2].Clone(); var dL = (string[])f["L"][2].Clone();
                f["F"][2] = dL; f["L"][2] = dB; f["B"][2] = dR; f["R"][2] = dF;
                break;
            case "F":
                f["F"] = RotationCw(f["F"]);
                var fU = new[]{ f["U"][2][0], f["U"][2][1], f["U"][2][2] };
                var fR = new[]{ f["R"][0][0], f["R"][1][0], f["R"][2][0] };
                var fD = new[]{ f["D"][0][0], f["D"][0][1], f["D"][0][2] };
                var fL = new[]{ f["L"][0][2], f["L"][1][2], f["L"][2][2] };
                f["U"][2][0] = fL[2]; f["U"][2][1] = fL[1]; f["U"][2][2] = fL[0];
                f["R"][0][0] = fU[0]; f["R"][1][0] = fU[1]; f["R"][2][0] = fU[2];
                f["D"][0][0] = fR[2]; f["D"][0][1] = fR[1]; f["D"][0][2] = fR[0];
                f["L"][0][2] = fD[0]; f["L"][1][2] = fD[1]; f["L"][2][2] = fD[2];
                break;
            case "B":
                f["B"] = RotationCw(f["B"]);
                var bU = new[]{ f["U"][0][0], f["U"][0][1], f["U"][0][2] };
                var bR = new[]{ f["R"][0][2], f["R"][1][2], f["R"][2][2] };
                var bD = new[]{ f["D"][2][0], f["D"][2][1], f["D"][2][2] };
                var bL = new[]{ f["L"][0][0], f["L"][1][0], f["L"][2][0] };
                f["U"][0][0] = bR[2]; f["U"][0][1] = bR[1]; f["U"][0][2] = bR[0];
                f["R"][0][2] = bD[2]; f["R"][1][2] = bD[1]; f["R"][2][2] = bD[0];
                f["D"][2][0] = bL[2]; f["D"][2][1] = bL[1]; f["D"][2][2] = bL[0];
                f["L"][0][0] = bU[0]; f["L"][1][0] = bU[1]; f["L"][2][0] = bU[2];
                break;
            case "R":
                f["R"] = RotationCw(f["R"]);
                var rU = new[]{ f["U"][0][2], f["U"][1][2], f["U"][2][2] };
                var rF = new[]{ f["F"][0][2], f["F"][1][2], f["F"][2][2] };
                var rD = new[]{ f["D"][0][2], f["D"][1][2], f["D"][2][2] };
                var rB = new[]{ f["B"][0][0], f["B"][1][0], f["B"][2][0] };
                f["U"][0][2] = rF[0]; f["U"][1][2] = rF[1]; f["U"][2][2] = rF[2];
                f["F"][0][2] = rD[0]; f["F"][1][2] = rD[1]; f["F"][2][2] = rD[2];
                f["D"][0][2] = rB[2]; f["D"][1][2] = rB[1]; f["D"][2][2] = rB[0];
                f["B"][0][0] = rU[2]; f["B"][1][0] = rU[1]; f["B"][2][0] = rU[0];
                break;
            case "L":
                f["L"] = RotationCw(f["L"]);
                var lU = new[]{ f["U"][0][0], f["U"][1][0], f["U"][2][0] };
                var lF = new[]{ f["F"][0][0], f["F"][1][0], f["F"][2][0] };
                var lD = new[]{ f["D"][0][0], f["D"][1][0], f["D"][2][0] };
                var lB = new[]{ f["B"][0][2], f["B"][1][2], f["B"][2][2] };
                f["U"][0][0] = lB[2]; f["U"][1][0] = lB[1]; f["U"][2][0] = lB[0];
                f["B"][0][2] = lD[2]; f["B"][1][2] = lD[1]; f["B"][2][2] = lD[0];
                f["D"][0][0] = lF[0]; f["D"][1][0] = lF[1]; f["D"][2][0] = lF[2];
                f["F"][0][0] = lU[0]; f["F"][1][0] = lU[1]; f["F"][2][0] = lU[2];
                break;
        }
    }

    /// <summary>Applique un mouvement (ex: "R", "U'", "F2") sur l'état faces. Retourne
    /// un NOUVEL état, le dictionnaire d'origine n'est jamais modifié.</summary>
    public static Dictionary<string, string[][]> AppliquerMouvement(
        Dictionary<string, string[][]> faces, string mouvement)
    {
        string baseFace = mouvement.TrimEnd('\'', '2');
        string suffixe = mouvement.Substring(baseFace.Length);
        int nbQuarts = suffixe == "'" ? 3 : (suffixe == "2" ? 2 : 1);

        var f = faces.ToDictionary(kv => kv.Key, kv => DeepCopy(kv.Value));
        for (int i = 0; i < nbQuarts; i++) QuartCw(f, baseFace);
        return f;
    }
}