using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AppStateMachine : MonoBehaviour
{
    [SerializeField] private FaceScanner scanner;
    [SerializeField] private ARUIManager ui;
    [SerializeField] private SolutionTextGuide solutionGuide;
    [SerializeField] private GameObject cameraPreviewRoot;
    [SerializeField] private GameObject instructionTextRoot;
    [SerializeField] private GameObject statusTextRoot;

    private const float PauseEntreFacesS = 6.0f;

    private readonly (CubeColor couleur, string nomAffiche)[] _sequenceCalibration = new[]
    {
        (CubeColor.Blanc,  "BLANC"),
        (CubeColor.Jaune,  "JAUNE"),
        (CubeColor.Orange, "ORANGE"),
        (CubeColor.Rouge,  "ROUGE"),
        (CubeColor.Vert,   "VERT"),
        (CubeColor.Bleu,   "BLEU"),
    };

    private readonly (string faceId, string direction, string instruction)[] _sequenceScan = new[]
    {
        ("F", (string)null, "Présentez la première face du cube (face AVANT) dans le carré."),
        ("R", "DROITE",     "Tournez le cube de 90° vers la DROITE."),
        ("L", "GAUCHE",     "Revenez à la position initiale, puis tournez vers la GAUCHE de 90°."),
        ("U", "HAUT",       "Inclinez vers le BAS de 90° (la face du HAUT vient face à vous)."),
        ("D", "BAS",        "Inclinez vers le HAUT de 90° (la face du BAS vient face à vous)."),
        ("B", "ARRIÈRE",    "Retournez le cube de 180° (la face ARRIÈRE vient face à vous)."),
    };

    private readonly Dictionary<string, string[]> _facesScannees = new();

    private void Start() => StartCoroutine(LancerApplication());

    private void Update()
    {
        if (_facesScannees.Count != 6) return;
        if (Keyboard.current == null) return;

        // Touche R : recherche automatique de la bonne combinaison d'orientations
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartCoroutine(RechercheAutomatiqueOrientation());
        }
    }

    /// <summary>
    /// Teste automatiquement les 4096 combinaisons possibles de corrections
    /// d'orientation (4 valeurs x 6 faces) et s'arrête dès qu'une combinaison
    /// rend le cube résoluble par Kociemba. Les combinaisons invalides sont
    /// rejetées quasi instantanément par le solveur (avant le calcul lourd),
    /// donc l'ensemble du balayage prend généralement moins d'une seconde.
    /// </summary>
    private IEnumerator RechercheAutomatiqueOrientation()
    {
        ui.ShowStatus("Recherche automatique de l'orientation en cours...");
        yield return null; // laisse l'UI se rafraîchir avant le calcul

        var (succesMap, mapping, msgMap) = CubeMapping.ConstruireMappingDepuisCentres(_facesScannees);
        if (!succesMap)
        {
            ui.ShowError($"Mapping impossible : {msgMap}");
            yield break;
        }

        int[] valeurs = { 0, 1, 2, 3 };
        bool trouve = false;

        foreach (int u in valeurs)
        foreach (int r in valeurs)
        foreach (int f in valeurs)
        foreach (int d in valeurs)
        foreach (int l in valeurs)
        foreach (int b in valeurs)
        {
            CubeAssembler.CorrectionU = u;
            CubeAssembler.CorrectionR = r;
            CubeAssembler.CorrectionF = f;
            CubeAssembler.CorrectionD = d;
            CubeAssembler.CorrectionL = l;
            CubeAssembler.CorrectionB = b;

            Dictionary<string, string[][]> faces;
            try { faces = CubeAssembler.ConstruireFaces(_facesScannees); }
            catch { continue; }

            var (succesRes, solution, _) = KociembaBridge.ResoudreCube(faces, mapping);
            if (succesRes)
            {
                trouve = true;
                var mouvements = KociembaBridge.DecompterMouvements(solution);
                ui.ShowStatus($"TROUVÉ ! U={u} R={r} F={f} D={d} L={l} B={b} " +
                               $"-> Solution : {mouvements.Count} mouvement(s)");
                Debug.Log($"[RechercheAuto] Combinaison gagnante : U={u} R={r} F={f} D={d} L={l} B={b}");
                Debug.Log("Solution complète : " + solution);
                yield break;
            }
        }

        if (!trouve)
        {
            ui.ShowError("Aucune combinaison d'orientation ne rend ce cube valide. " +
                          "Le problème vient probablement d'une couleur mal lue, pas de l'orientation. " +
                          "Refaites un scan complet (calibration + 6 faces).");
        }
    }

    private IEnumerator LancerApplication()
    {
        yield return StartCoroutine(CalibrationCouleurs());
        yield return StartCoroutine(ScannerLesSixFaces());
    }

    /// <summary>Phase de calibration en 6 étapes : pour chaque couleur, l'utilisateur
    /// présente la face dont le CENTRE est cette couleur exacte. Recommence
    /// indéfiniment une étape tant qu'elle échoue (pas de limite de tentatives).</summary>
    private IEnumerator CalibrationCouleurs()
    {
        ColorCalibration.Reinitialiser();

        foreach (var (couleur, nomAffiche) in _sequenceCalibration)
        {
            string precision = couleur == CubeColor.Blanc
                ? " (le logo coloré au centre est normal, il sera automatiquement ignoré)"
                : "";
            ui.ShowInstruction($"CALIBRATION — Préparez la face dont le CENTRE est {nomAffiche}{precision}");
            for (int i = 3; i >= 1; i--)
            {
                ui.ShowStatus($"Présentez-la dans le carré... {i}");
                yield return new WaitForSeconds(1f);
            }

            bool ok = false;
            while (!ok)
            {
                ui.ShowInstruction($"CALIBRATION — Présentez la face dont le CENTRE est {nomAffiche}{precision}\n" +
                                    "dans le carré, et maintenez-la immobile.");
                yield return StartCoroutine(scanner.CalibrerCouleur(couleur));

                if (scanner.CalibrationEtapeSucces)
                {
                    ui.ShowStatus($"Couleur {nomAffiche} calibrée.");
                    ok = true;
                }
                else
                {
                    ui.ShowError(scanner.CalibrationEtapeMessageErreur);
                }
            }
            yield return new WaitForSeconds(1f);
        }

        // Vérification finale : les 6 couleurs sont-elles bien distinctes ?
        string warning = ColorCalibration.FinaliserCalibration();

        var refsFinales = ColorCalibration.GetReferencesCopy();
        string dR = refsFinales.TryGetValue(CubeColor.Rouge,  out var rR) ? $"R({rR.r:F0},{rR.g:F0},{rR.b:F0})" : "?";
        string dO = refsFinales.TryGetValue(CubeColor.Orange, out var rO) ? $"O({rO.r:F0},{rO.g:F0},{rO.b:F0})" : "?";
        string dJ = refsFinales.TryGetValue(CubeColor.Jaune,  out var rJ) ? $"J({rJ.r:F0},{rJ.g:F0},{rJ.b:F0})" : "?";

        ui.ShowInstruction("Calibration terminée !");
        ui.ShowStatus(string.IsNullOrEmpty(warning)
            ? $"Calibration OK\n{dR} | {dO} | {dJ}"
            : warning);
        yield return new WaitForSeconds(3f);
    }    
    private IEnumerator ScannerLesSixFaces()
    {
        foreach (var (faceId, direction, instruction) in _sequenceScan)
        {
            ui.ShowInstruction(direction != null ? $"Tournez le cube : {direction}\n{instruction}" : instruction);
            yield return new WaitForSeconds(direction != null ? PauseEntreFacesS : 4f);

            bool faceValidee = false;
            int tentatives = 0;
            while (!faceValidee)
            {
                tentatives++;
                ui.ShowStatus($"Face {faceId} — tentative {tentatives}");

                yield return StartCoroutine(scanner.ScannerFace(faceId));

                if (!scanner.Succes) { ui.ShowError(scanner.MessageErreur); continue; }

                var (valide, msgVal) = ValiderEtatPartiel(scanner.ResultatCouleurs9);
                if (!valide) { ui.ShowError(msgVal); continue; }

                _facesScannees[faceId] = scanner.ResultatCouleurs9;
                ui.ShowFaceResult(faceId, scanner.ResultatCouleurs9);
                faceValidee = true;
            }
        }

        yield return StartCoroutine(AssemblerEtResoudre());
    }

    private (bool, string) ValiderEtatPartiel(string[] couleurs9)
    {
        foreach (var c in couleurs9) if (c == "INCONNU") return (false, "Facette(s) non classifiée(s).");
        var compte = new Dictionary<string,int>();
        foreach (var c in couleurs9) compte[c] = compte.GetValueOrDefault(c) + 1;
        foreach (var kv in compte) if (kv.Value > 9) return (false, $"Couleur '{kv.Key}' détectée {kv.Value} fois (max 9).");
        return (true, "OK");
    }

    private IEnumerator AssemblerEtResoudre()
    {
        ui.ShowStatus("Mapping et validation...");
        var (succesMap, mapping, msgMap) = CubeMapping.ConstruireMappingDepuisCentres(_facesScannees);
        if (!succesMap) { ui.ShowError($"Mapping impossible : {msgMap}"); yield break; }

        Dictionary<string, string[][]> faces;
        try { faces = CubeAssembler.ConstruireFaces(_facesScannees); }
        catch (System.Exception exc) { ui.ShowError($"Erreur assemblage : {exc.Message}"); yield break; }

        var (valide, msgVal) = CubeValidator.ValiderCube(faces);
        if (!valide) { ui.ShowError($"Cube invalide : {msgVal}"); yield break; }

        ui.ShowStatus("Résolution Kociemba (peut prendre quelques minutes au premier essai)...");
        var (succesRes, solution, msgRes) = KociembaBridge.ResoudreCube(faces, mapping);
        if (!succesRes)
        {
            Debug.LogError($"Résolution impossible : {msgRes}");
            ui.ShowError($"Résolution impossible : {msgRes}");
            yield break;
        }

        var mouvements = KociembaBridge.DecompterMouvements(solution);
        ui.ShowSolutionSummary(mouvements.Count);
        Debug.Log("Solution complète : " + solution);
        if (cameraPreviewRoot != null) cameraPreviewRoot.SetActive(false);
        if (instructionTextRoot != null) instructionTextRoot.SetActive(false);
        if (statusTextRoot != null) statusTextRoot.SetActive(false);
        if (solutionGuide != null) solutionGuide.Initialiser(mouvements);
    }
}