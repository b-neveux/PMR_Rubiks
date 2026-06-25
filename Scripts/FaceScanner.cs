using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FaceScanner : MonoBehaviour
{
    [SerializeField] private CameraFeedManager cameraFeed;
    [SerializeField] private GuideSquareUI guideSquare;

    [Header("Réglages généraux (scan)")]
    [SerializeField] private float guideCarreRatio   = 0.55f;
    [SerializeField] private float roiShrinkFactor    = 0.25f;
    [SerializeField] private float stabiliteFenetreS  = 0.4f;
    [SerializeField] private float stabiliteSeuil     = 0.6f;
    [SerializeField] private int   stabiliteNbMin     = 3;
    [SerializeField] private float autoTimeoutS       = 60.0f;

    [Header("Détection de présence du cube (joints noirs entre facettes)")]
    [SerializeField] private float seuilSombreV = 90f;
    [SerializeField] private float seuilFractionGrilleNoire = 0.55f;
    [SerializeField] private int nbLignesGrilleRequises = 4;

    [Header("Confirmation par essais répétés (scan)")]
    [SerializeField] private float essaiDureeS = 0.9f;
    [SerializeField] private float essaiSeuilAccordInterne = 0.55f;
    [SerializeField] private int nbEssaisFenetre = 5;
    [SerializeField] private int nbAccordRequis = 3;

    [Header("Calibration — détection de stabilité dédiée (plus stricte/lente)")]
    [Tooltip("Fenêtre de temps observée pour juger la stabilité pendant la calibration")]
    [SerializeField] private float calibStabiliteFenetreS = 1.5f;
    [Tooltip("Fraction de frames stables requise dans la fenêtre (plus haut = plus strict)")]
    [SerializeField] private float calibStabiliteSeuil = 0.85f;
    [Tooltip("Nombre minimum de frames dans la fenêtre avant de considérer que c'est stable")]
    [SerializeField] private int calibStabiliteNbMin = 12;
    [Tooltip("Durée d'échantillonnage des couleurs une fois la stabilité confirmée")]
    [SerializeField] private float dureeEchantillonnageCalibrationS = 1.2f;

    [Header("Debug")]
    [SerializeField] private bool afficherDebugHsv = true;
    [SerializeField] private TMPro.TextMeshProUGUI debugHsvText;

    [Header("Anti-reflet")]
    [Tooltip("Fraction minimale de pixels blancs pour valider BLANC (en dessous = reflet ignoré)")]
    [SerializeField] private float fractionMinBlanc = 0.25f;

    public string[] ResultatCouleurs9 { get; private set; }
    public bool Succes { get; private set; }
    public string MessageErreur { get; private set; }
    public bool DernierePresenceCubeOk { get; private set; }

    public bool CalibrationEtapeSucces { get; private set; }
    public string CalibrationEtapeMessageErreur { get; private set; }

    private readonly Queue<(float t, bool ok)> _historiqueStabilite = new();

    // ==========================================================================
    //  SCAN NORMAL D'UNE FACE
    // ==========================================================================

    public IEnumerator ScannerFace(string nomFace)
    {
        _historiqueStabilite.Clear();
        Succes = false; ResultatCouleurs9 = null; MessageErreur = "";
        guideSquare.SetStable(false);

        float tDebutPhase1 = Time.time;
        bool stable = false;

        while (!stable)
        {
            if (Time.time - tDebutPhase1 >= autoTimeoutS)
            {
                MessageErreur = $"Timeout : aucun cube détecté après {autoTimeoutS:F0}s. " +
                                 "Vérifiez l'éclairage et présentez bien une face complète dans le carré.";
                yield break;
            }

            bool lectureOk = AnalyserZoneGuide(out _);
            float now = Time.time;
            _historiqueStabilite.Enqueue((now, lectureOk));
            while (_historiqueStabilite.Count > 0 && now - _historiqueStabilite.Peek().t > stabiliteFenetreS)
                _historiqueStabilite.Dequeue();

            int nbOk = 0; foreach (var e in _historiqueStabilite) if (e.ok) nbOk++;
            float taux = _historiqueStabilite.Count > 0 ? (float)nbOk / _historiqueStabilite.Count : 0f;
            stable = _historiqueStabilite.Count >= stabiliteNbMin && taux >= stabiliteSeuil;

            guideSquare.SetStable(stable);
            yield return null;
        }

        var historiqueEssais = new List<string[]>();
        float tDebutPhase2 = Time.time;

        while (true)
        {
            if (Time.time - tDebutPhase2 >= autoTimeoutS)
            {
                MessageErreur = "Timeout : la lecture n'est jamais devenue assez cohérente entre les essais. " +
                                 "Maintenez le cube parfaitement immobile et bien éclairé.";
                yield break;
            }

            var framesEssai = new List<string[]>();
            float tEssai = Time.time;
            while (Time.time - tEssai < essaiDureeS)
            {
                if (AnalyserZoneGuide(out string[] couleursFrame) && DernierePresenceCubeOk)
                    framesEssai.Add(couleursFrame);
                yield return null;
            }

            if (framesEssai.Count < 5) continue;

            int minAccordFrame = Mathf.Max(1, Mathf.CeilToInt(essaiSeuilAccordInterne * framesEssai.Count));
            var couleursEssai = new string[9];
            for (int cell = 0; cell < 9; cell++)
            {
                var compte = new Dictionary<string, int>();
                foreach (var r in framesEssai)
                {
                    if (!compte.ContainsKey(r[cell])) compte[r[cell]] = 0;
                    compte[r[cell]]++;
                }
                string topC = "INCONNU"; int topN = 0;
                foreach (var kv in compte) if (kv.Value > topN) { topN = kv.Value; topC = kv.Key; }
                couleursEssai[cell] = topN >= minAccordFrame ? topC : "INCONNU";
            }

            historiqueEssais.Add(couleursEssai);
            if (historiqueEssais.Count > nbEssaisFenetre) historiqueEssais.RemoveAt(0);

            bool toutesConfirmees = true;
            var candidat = new string[9];
            for (int cell = 0; cell < 9; cell++)
            {
                var compte = new Dictionary<string, int>();
                foreach (var essai in historiqueEssais)
                {
                    string c = essai[cell];
                    if (c == "INCONNU") continue;
                    if (!compte.ContainsKey(c)) compte[c] = 0;
                    compte[c]++;
                }
                string topC = "INCONNU"; int topN = 0;
                foreach (var kv in compte) if (kv.Value > topN) { topN = kv.Value; topC = kv.Key; }

                candidat[cell] = topC;
                if (topC == "INCONNU" || topN < nbAccordRequis) toutesConfirmees = false;
            }

            if (toutesConfirmees && historiqueEssais.Count >= nbAccordRequis)
            {
                ResultatCouleurs9 = candidat;
                Succes = true;
                yield break;
            }
        }
    }

    // ==========================================================================
    //  CALIBRATION : une couleur connue à l'avance, robuste au logo coloré
    // ==========================================================================

    public IEnumerator CalibrerCouleur(CubeColor couleurCible)
    {
        CalibrationEtapeSucces = false;
        CalibrationEtapeMessageErreur = "";
        guideSquare.SetStable(false);

        // --- Phase A : attendre une détection stable ---
        float tDebut = Time.time;
        var historique = new Queue<(float t, bool ok)>();
        bool stable = false;

        while (!stable)
        {
            if (Time.time - tDebut >= autoTimeoutS)
            {
                CalibrationEtapeMessageErreur = "Timeout : aucun cube détecté.";
                yield break;
            }
            bool grilleOk = DetecterGrillePresenteSeulement();
            float now = Time.time;
            historique.Enqueue((now, grilleOk));
            while (historique.Count > 0 && now - historique.Peek().t > calibStabiliteFenetreS)
                historique.Dequeue();
            int nbOk = 0; foreach (var e in historique) if (e.ok) nbOk++;
            float taux = historique.Count > 0 ? (float)nbOk / historique.Count : 0f;
            stable = historique.Count >= calibStabiliteNbMin && taux >= calibStabiliteSeuil;
            guideSquare.SetStable(stable);
            if (afficherDebugHsv && debugHsvText != null)
                debugHsvText.text = $"Calibration {couleurCible}\nStabilité : {taux*100:F0}%\nMaintenez le cube immobile...";
            yield return null;
        }

        // --- Phase B : échantillonnage long (au moins 2s) + filtrage des reflets ---
        var allR = new List<float>();
        var allG = new List<float>();
        var allB = new List<float>();

        float duree = Mathf.Max(dureeEchantillonnageCalibrationS, 2.0f);
        float tEch = Time.time;

        while (Time.time - tEch < duree)
        {
            if (cameraFeed.TryGetLatestFrame(out Color32[] pixels, out int width, out int height))
            {
                int taille = Mathf.RoundToInt(Mathf.Min(width, height) * guideCarreRatio);
                int gx = (width - taille) / 2;
                int gy = (height - taille) / 2;
                float cellW = taille / 3f, cellH = taille / 3f;
                // Shrink agressif pendant la calibration : évite les joints noirs aux bords
                float sx = cellW * 0.20f, sy = cellH * 0.20f;
                int x1 = Mathf.Clamp(Mathf.RoundToInt(gx + cellW + sx), 0, width - 1);
                int y1 = Mathf.Clamp(Mathf.RoundToInt(gy + cellH + sy), 0, height - 1);
                int x2 = Mathf.Clamp(Mathf.RoundToInt(gx + 2*cellW - sx), x1+1, width);
                int y2 = Mathf.Clamp(Mathf.RoundToInt(gy + 2*cellH - sy), y1+1, height);

                for (int y = y1; y < y2; y += 2)
                for (int x = x1; x < x2; x += 2)
                {
                    Color32 px = pixels[y * width + x];
                    allR.Add(px.r); allG.Add(px.g); allB.Add(px.b);
                }
            }

            float progress = (Time.time - tEch) / duree;
            if (afficherDebugHsv && debugHsvText != null && allR.Count > 0)
            {
                float mr = Median(allR), mg = Median(allG), mb = Median(allB);
                debugHsvText.text = $"Calibration {couleurCible}\n" +
                                     $"RGB: ({mr:F0}, {mg:F0}, {mb:F0})\n" +
                                     $"{allR.Count} pixels — {progress*100:F0}%";
            }
            yield return null;
        }

        if (allR.Count == 0)
        {
            CalibrationEtapeMessageErreur = "Aucun pixel échantillonné.";
            yield break;
        }

        // Calcul de la médiane (robuste aux reflets contrairement à la moyenne)
        float medR = Median(allR), medG = Median(allG), medB = Median(allB);

        // Filtrage des outliers : rejette les pixels à plus de 50 unités RGB de la médiane
        // (reflets de lumière directe, pixels de joints noirs qui auraient glissé dans la ROI)
        var filtR = new List<float>();
        var filtG = new List<float>();
        var filtB = new List<float>();
        for (int i = 0; i < allR.Count; i++)
        {
            float dr = allR[i] - medR, dg = allG[i] - medG, db = allB[i] - medB;
            if (Mathf.Sqrt(dr*dr + dg*dg + db*db) <= 50f)
            {
                filtR.Add(allR[i]); filtG.Add(allG[i]); filtB.Add(allB[i]);
            }
        }

        // Moyenne sur les pixels filtrés = référence finale
        float refR = filtR.Count > 0 ? Mean(filtR) : medR;
        float refG = filtG.Count > 0 ? Mean(filtG) : medG;
        float refB = filtB.Count > 0 ? Mean(filtB) : medB;

        if (couleurCible == CubeColor.Blanc)
            ColorCalibration.CalibrerBlanc(refR, refG, refB);
        else
            ColorCalibration.CalibrerCouleur(couleurCible, refR, refG, refB);

        CalibrationEtapeSucces = true;

        if (afficherDebugHsv && debugHsvText != null)
        {
            int rejetes = allR.Count - filtR.Count;
            debugHsvText.text = $"{couleurCible} calibré ✓\n" +
                                  $"RGB: ({refR:F0}, {refG:F0}, {refB:F0})\n" +
                                  $"{filtR.Count} pixels retenus, {rejetes} reflets rejetés";
        }
    }

    private static float Median(List<float> values)
    {
        if (values.Count == 0) return 0f;
        var sorted = new List<float>(values);
        sorted.Sort();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[mid-1] + sorted[mid]) / 2f : sorted[mid];
    }

    private static float Mean(List<float> values)
    {
        if (values.Count == 0) return 0f;
        float sum = 0; foreach (var v in values) sum += v;
        return sum / values.Count;
    }

    private static float StdDev(List<float> values)
    {
        if (values.Count < 2) return 0f;
        float mean = Mean(values);
        float sumSq = 0; foreach (var v in values) sumSq += (v - mean) * (v - mean);
        return (float)Math.Sqrt(sumSq / values.Count);
    }

    private bool DetecterGrillePresenteSeulement()
    {
        if (!cameraFeed.TryGetLatestFrame(out Color32[] pixels, out int width, out int height)) return false;
        int taille = Mathf.RoundToInt(Mathf.Min(width, height) * guideCarreRatio);
        int gx = (width - taille) / 2;
        int gy = (height - taille) / 2;
        return DetecterGrilleNoire(pixels, width, height, gx, gy, taille);
    }

    // ==========================================================================
    //  ANALYSE D'UNE FRAME (commun au scan)
    // ==========================================================================

    private bool AnalyserZoneGuide(out string[] couleurs9)
    {
        couleurs9 = new string[9];
        for (int i = 0; i < 9; i++) couleurs9[i] = "INCONNU";
        DernierePresenceCubeOk = false;

        if (!cameraFeed.TryGetLatestFrame(out Color32[] pixels, out int width, out int height))
            return false;

        int taille = Mathf.RoundToInt(Mathf.Min(width, height) * guideCarreRatio);
        int gx = (width - taille) / 2;
        int gy = (height - taille) / 2;

        bool grilleNoireDetectee = DetecterGrilleNoire(pixels, width, height, gx, gy, taille);
        guideSquare.SetCubeDetecte(grilleNoireDetectee);

        if (!grilleNoireDetectee)
        {
            guideSquare.SetCellulesCouleurs(couleurs9);
            return false;
        }

        float cellW = taille / 3f, cellH = taille / 3f;
        float shrinkX = cellW * (roiShrinkFactor / 2f), shrinkY = cellH * (roiShrinkFactor / 2f);

        int nbInconnues = 0;
        for (int lig = 0; lig < 3; lig++)
        for (int col = 0; col < 3; col++)
        {
            int x1 = Mathf.Clamp(Mathf.RoundToInt(gx + col * cellW + shrinkX), 0, width - 1);
            int y1 = Mathf.Clamp(Mathf.RoundToInt(gy + lig * cellH + shrinkY), 0, height - 1);
            int x2 = Mathf.Clamp(Mathf.RoundToInt(gx + (col + 1) * cellW - shrinkX), x1 + 1, width);
            int y2 = Mathf.Clamp(Mathf.RoundToInt(gy + (lig + 1) * cellH - shrinkY), y1 + 1, height);

            var votes = new List<CubeColor>();
            for (int y = y1; y < y2; y += 2)
            for (int x = x1; x < x2; x += 2)
            {
                Color32 px = pixels[y * width + x];
                votes.Add(ColorClassifier.ClassifyPixel(px.r, px.g, px.b));
            }

            CubeColor gagnant = ColorClassifier.VoteMajoritaireAvecAntiReflet(votes, fractionMinBlanc);
            int idx = lig * 3 + col;
            couleurs9[idx] = gagnant == CubeColor.Inconnu ? "INCONNU" : gagnant.ToString().ToUpperInvariant();
            if (gagnant == CubeColor.Inconnu) nbInconnues++;
        }

        guideSquare.SetCellulesCouleurs(couleurs9);
        DernierePresenceCubeOk = true;

        if (afficherDebugHsv && debugHsvText != null)
        {
            int cxDebug = gx + taille / 2;
            int cyDebug = gy + taille / 2;
            if (cxDebug >= 0 && cxDebug < width && cyDebug >= 0 && cyDebug < height)
            {
                Color32 pxDebug = pixels[cyDebug * width + cxDebug];
                ColorClassifier.RgbToHsv(pxDebug.r, pxDebug.g, pxDebug.b, out float hD, out float sD, out float vD);
                string calibInfo = ColorCalibration.EstCalibre
                    ? $"[calibré R{ColorCalibration.GainR:F2} G{ColorCalibration.GainG:F2} B{ColorCalibration.GainB:F2}]"
                    : "[non calibré]";
                debugHsvText.text = $"H={hD:F0} S={sD:F0} V={vD:F0} -> {couleurs9[4]} {calibInfo}";
            }
        }

        return nbInconnues == 0;
    }

    private bool DetecterGrilleNoire(Color32[] pixels, int width, int height, int gx, int gy, int taille)
    {
        int epaisseurBande = Mathf.Max(2, Mathf.RoundToInt(taille * 0.012f));

        float FractionSombreLigneVerticale(int x)
        {
            int nbSombres = 0, nbTotal = 0;
            for (int dx = -epaisseurBande; dx <= epaisseurBande; dx++)
            {
                int xi = x + dx;
                if (xi < 0 || xi >= width) continue;
                for (int y = gy + 4; y < gy + taille - 4; y += 3)
                {
                    if (y < 0 || y >= height) continue;
                    Color32 px = pixels[y * width + xi];
                    int v = Mathf.Max(px.r, Mathf.Max(px.g, px.b));
                    nbTotal++;
                    if (v < seuilSombreV) nbSombres++;
                }
            }
            return nbTotal == 0 ? 0f : (float)nbSombres / nbTotal;
        }

        float FractionSombreLigneHorizontale(int y)
        {
            int nbSombres = 0, nbTotal = 0;
            for (int dy = -epaisseurBande; dy <= epaisseurBande; dy++)
            {
                int yi = y + dy;
                if (yi < 0 || yi >= height) continue;
                for (int x = gx + 4; x < gx + taille - 4; x += 3)
                {
                    if (x < 0 || x >= width) continue;
                    Color32 px = pixels[yi * width + x];
                    int v = Mathf.Max(px.r, Mathf.Max(px.g, px.b));
                    nbTotal++;
                    if (v < seuilSombreV) nbSombres++;
                }
            }
            return nbTotal == 0 ? 0f : (float)nbSombres / nbTotal;
        }

        int xLigne1 = gx + taille / 3;
        int xLigne2 = gx + 2 * taille / 3;
        int yLigne1 = gy + taille / 3;
        int yLigne2 = gy + 2 * taille / 3;

        float fV1 = FractionSombreLigneVerticale(xLigne1);
        float fV2 = FractionSombreLigneVerticale(xLigne2);
        float fH1 = FractionSombreLigneHorizontale(yLigne1);
        float fH2 = FractionSombreLigneHorizontale(yLigne2);

        int nbLignesOk = 0;
        if (fV1 >= seuilFractionGrilleNoire) nbLignesOk++;
        if (fV2 >= seuilFractionGrilleNoire) nbLignesOk++;
        if (fH1 >= seuilFractionGrilleNoire) nbLignesOk++;
        if (fH2 >= seuilFractionGrilleNoire) nbLignesOk++;

        return nbLignesOk >= nbLignesGrilleRequises;
    }
}