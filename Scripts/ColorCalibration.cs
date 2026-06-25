using System.Collections.Generic;
using UnityEngine;

public static class ColorCalibration
{
    // Références RGB mesurées sous l'éclairage réel (0-255)
    private static readonly Dictionary<CubeColor, (float r, float g, float b)> _refs = new();

    public static float GainR = 1f, GainG = 1f, GainB = 1f;
    public static bool EstCalibre { get; private set; } = false;
    public static string DernierAvertissement { get; private set; } = "";

    public static void Reinitialiser()
    {
        _refs.Clear();
        GainR = GainG = GainB = 1f;
        EstCalibre = false;
        DernierAvertissement = "";
    }

    public static (byte r, byte g, byte b) AppliquerGains(byte r, byte g, byte b)
    {
        int rc = Mathf.Clamp(Mathf.RoundToInt(r * GainR), 0, 255);
        int gc = Mathf.Clamp(Mathf.RoundToInt(g * GainG), 0, 255);
        int bc = Mathf.Clamp(Mathf.RoundToInt(b * GainB), 0, 255);
        return ((byte)rc, (byte)gc, (byte)bc);
    }

    public static bool TryGetReference(CubeColor c, out (float r, float g, float b) refColor) =>
        _refs.TryGetValue(c, out refColor);

    /// <summary>Calibre le BLANC en premier : calcule la balance des blancs
    /// et stocke la référence blanche après correction.</summary>
    public static void CalibrerBlanc(float r, float g, float b)
    {
        const float Target = 220f;
        GainR = Mathf.Clamp(Target / Mathf.Max(1f, r), 0.4f, 3f);
        GainG = Mathf.Clamp(Target / Mathf.Max(1f, g), 0.4f, 3f);
        GainB = Mathf.Clamp(Target / Mathf.Max(1f, b), 0.4f, 3f);
        float ar = Mathf.Clamp(r * GainR, 0, 255);
        float ag = Mathf.Clamp(g * GainG, 0, 255);
        float ab = Mathf.Clamp(b * GainB, 0, 255);
        _refs[CubeColor.Blanc] = (ar, ag, ab);
        Debug.Log($"[Calib] BLANC gains R={GainR:F2} G={GainG:F2} B={GainB:F2} → ({ar:F0},{ag:F0},{ab:F0})");
    }

    /// <summary>Calibre une couleur non-blanche : applique les gains déjà
    /// calculés depuis le blanc, puis stocke la référence RGB.</summary>
    public static void CalibrerCouleur(CubeColor couleur, float r, float g, float b)
    {
        float ar = Mathf.Clamp(r * GainR, 0, 255);
        float ag = Mathf.Clamp(g * GainG, 0, 255);
        float ab = Mathf.Clamp(b * GainB, 0, 255);
        _refs[couleur] = (ar, ag, ab);
        Debug.Log($"[Calib] {couleur} → ({ar:F0},{ag:F0},{ab:F0})");
    }

    /// <summary>Appelée après les 6 calibrations. Vérifie que les couleurs
    /// sont suffisamment distinctes et retourne un avertissement si besoin.</summary>
    public static string FinaliserCalibration()
    {
        if (_refs.Count < 6)
        {
            DernierAvertissement = $"Calibration incomplète ({_refs.Count}/6).";
            return DernierAvertissement;
        }

        float minDist = float.MaxValue;
        CubeColor warnA = CubeColor.Inconnu, warnB = CubeColor.Inconnu;
        var keys = new List<CubeColor>(_refs.Keys);
        for (int i = 0; i < keys.Count; i++)
        for (int j = i + 1; j < keys.Count; j++)
        {
            float d = RgbDist(keys[i], keys[j]);
            if (d < minDist) { minDist = d; warnA = keys[i]; warnB = keys[j]; }
        }

        EstCalibre = true;
        DernierAvertissement = minDist < 35f
            ? $"⚠ {warnA} et {warnB} sont trop similaires (dist={minDist:F0}). " +
              "Recommencez avec un meilleur éclairage ou repositionnez la face."
            : "";

        Debug.Log($"[Calib] OK — dist min {warnA}/{warnB} = {minDist:F0}. {DernierAvertissement}");
        return DernierAvertissement;
    }

    private static float RgbDist(CubeColor a, CubeColor b)
    {
        if (!_refs.TryGetValue(a, out var ra) || !_refs.TryGetValue(b, out var rb))
            return float.MaxValue;
        float dr = ra.r - rb.r, dg = ra.g - rb.g, db = ra.b - rb.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    public static Dictionary<CubeColor, (float r, float g, float b)> GetReferencesCopy() =>
        new Dictionary<CubeColor, (float r, float g, float b)>(_refs);
}