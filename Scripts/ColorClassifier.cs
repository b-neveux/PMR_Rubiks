using System;
using System.Collections.Generic;

public enum CubeColor { Blanc, Jaune, Orange, Rouge, Vert, Bleu, Inconnu }

public struct HsvRange { public float hMin, hMax, sMin, sMax, vMin, vMax; }
public class ColorDef { public CubeColor Name; public HsvRange[] Plages; }

public static class ColorClassifier
{
    // Seuils HSV — utilisés UNIQUEMENT avant calibration et pendant l'échantillonnage
    public static readonly ColorDef[] Defs = new[]
    {
        new ColorDef { Name = CubeColor.Blanc,
            Plages = new[]{ new HsvRange{hMin=0,hMax=179,sMin=0,sMax=80,vMin=170,vMax=255} } },
        new ColorDef { Name = CubeColor.Jaune,
            Plages = new[]{ new HsvRange{hMin=20,hMax=40,sMin=80,sMax=255,vMin=80,vMax=255} } },
        new ColorDef { Name = CubeColor.Orange,
            Plages = new[]{
                new HsvRange{hMin=0,hMax=5,  sMin=90,sMax=255,vMin=130,vMax=255},
                new HsvRange{hMin=6,hMax=25, sMin=90,sMax=255,vMin=60, vMax=255} } },
        new ColorDef { Name = CubeColor.Rouge,
            Plages = new[]{
                new HsvRange{hMin=0,  hMax=5,  sMin=130,sMax=255,vMin=40,vMax=140},
                new HsvRange{hMin=155,hMax=179,sMin=130,sMax=255,vMin=40,vMax=255} } },
        new ColorDef { Name = CubeColor.Vert,
            Plages = new[]{ new HsvRange{hMin=40,hMax=95,sMin=40,sMax=255,vMin=20,vMax=255} } },
        new ColorDef { Name = CubeColor.Bleu,
            Plages = new[]{ new HsvRange{hMin=80,hMax=140,sMin=80,sMax=255,vMin=50,vMax=255} } },
    };

    public static void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        float rf = r/255f, gf = g/255f, bf = b/255f;
        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;
        float hue;
        if (delta < 1e-6f) hue = 0f;
        else if (max == rf) hue = 60f * (((gf - bf) / delta) % 6f);
        else if (max == gf) hue = 60f * (((bf - rf) / delta) + 2f);
        else hue = 60f * (((rf - gf) / delta) + 4f);
        if (hue < 0) hue += 360f;
        h = hue / 2f;
        s = (max <= 0f) ? 0f : (delta / max) * 255f;
        v = max * 255f;
    }

    private static bool EstBlancSurexpose(byte r, byte g, byte b)
    {
        int minC = Math.Min(r, Math.Min(g, b));
        int maxC = Math.Max(r, Math.Max(g, b));
        return minC > 160 && (maxC - minC) < 60;
    }

    /// <summary>Classification sans calibration (fallback HSV). Utilisée pendant
    /// l'échantillonnage de calibration pour identifier les pixels majoritaires.</summary>
    public static CubeColor ClassifyPixelRaw(byte r, byte g, byte b)
    {
        if (EstBlancSurexpose(r, g, b)) return CubeColor.Blanc;
        RgbToHsv(r, g, b, out float h, out float s, out float v);
        foreach (var def in Defs)
            foreach (var p in def.Plages)
                if (h >= p.hMin && h <= p.hMax && s >= p.sMin && s <= p.sMax
                    && v >= p.vMin && v <= p.vMax)
                    return def.Name;
        return CubeColor.Inconnu;
    }

    /// <summary>Classification principale pendant le scan. Avec calibration : RGB
    /// plus proche voisin (simple, robuste). Sans calibration : seuils HSV.</summary>
    public static CubeColor ClassifyPixel(byte rRaw, byte gRaw, byte bRaw)
    {
        var (r, g, b) = ColorCalibration.AppliquerGains(rRaw, gRaw, bRaw);

        if (!ColorCalibration.EstCalibre)
            return ClassifyPixelRaw(r, g, b);

        // Blanc surexposé avant même la comparaison aux références
        if (EstBlancSurexpose(r, g, b)) return CubeColor.Blanc;

        float bestDist = float.MaxValue;
        CubeColor bestColor = CubeColor.Inconnu;

        foreach (CubeColor col in new[]
            { CubeColor.Blanc, CubeColor.Jaune, CubeColor.Orange,
              CubeColor.Rouge, CubeColor.Vert, CubeColor.Bleu })
        {
            if (!ColorCalibration.TryGetReference(col, out var refColor)) continue;
            float dr = r - refColor.r;
            float dg = g - refColor.g;
            float db = b - refColor.b;
            // Canal vert légèrement amplifié : sépare mieux rouge/orange/jaune
            float dist = (float)Math.Sqrt(dr*dr + 1.5f*dg*dg + db*db);
            if (dist < bestDist) { bestDist = dist; bestColor = col; }
        }

        // Rejette si trop loin de toutes les références (pixel ambigu ou reflet)
        return bestDist < 90f ? bestColor : CubeColor.Inconnu;
    }

    public static CubeColor VoteMajoritaire(List<CubeColor> pixels)
    {
        var compte = new int[Enum.GetValues(typeof(CubeColor)).Length];
        foreach (var c in pixels) compte[(int)c]++;
        int totalConnus = 0;
        for (int i = 0; i < compte.Length - 1; i++) totalConnus += compte[i];
        if (totalConnus == 0) return CubeColor.Inconnu;
        int bestIdx = 0, bestCount = -1;
        for (int i = 0; i < compte.Length - 1; i++)
            if (compte[i] > bestCount) { bestCount = compte[i]; bestIdx = i; }
        var gagnant = (CubeColor)bestIdx;
        if (gagnant == CubeColor.Bleu)
        {
            if ((float)compte[(int)CubeColor.Blanc] / totalConnus >= 0.15f)
                return CubeColor.Blanc;
        }
        return gagnant;
    }

    public static CubeColor VoteMajoritaireAvecAntiReflet(List<CubeColor> pixels,
        float fractionMinBlanc = 0.25f)
    {
        var compte = new int[Enum.GetValues(typeof(CubeColor)).Length];
        foreach (var c in pixels) compte[(int)c]++;
        int totalConnus = 0;
        for (int i = 0; i < compte.Length - 1; i++) totalConnus += compte[i];
        if (totalConnus == 0) return CubeColor.Inconnu;
        int bestIdx = 0, bestCount = -1;
        for (int i = 0; i < compte.Length - 1; i++)
            if (compte[i] > bestCount) { bestCount = compte[i]; bestIdx = i; }
        var gagnant = (CubeColor)bestIdx;
        if (gagnant == CubeColor.Bleu)
        {
            if ((float)compte[(int)CubeColor.Blanc] / totalConnus >= 0.15f)
                return CubeColor.Blanc;
        }
        if (gagnant == CubeColor.Blanc)
        {
            if ((float)compte[(int)CubeColor.Blanc] / totalConnus < fractionMinBlanc)
                return CubeColor.Inconnu;
        }
        return gagnant;
    }
}