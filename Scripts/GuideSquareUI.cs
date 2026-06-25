using UnityEngine;
using UnityEngine.UI;

public class GuideSquareUI : MonoBehaviour
{
    [SerializeField] private Image[] bordures;
    [SerializeField] private Image[] lignesSubdivision;
    [SerializeField] private Color couleurNoir = Color.black;
    [SerializeField] private Color couleurVert = new Color(0.24f, 0.86f, 0.24f);
    [SerializeField] private Image[] celluleSwatches;

    [Header("Doit être identique à FaceScanner.guideCarreRatio")]
    [SerializeField] private float guideCarreRatio = 0.55f;

    private RectTransform _rt;

    private void Awake() => _rt = GetComponent<RectTransform>();

    // Recalcule la taille du carré APRÈS que le parent (CameraPreview) ait fini
    // de s'ajuster à l'aspect ratio réel de la caméra (AspectRatioFitter) :
    // le carré reste ainsi toujours une zone exacte de l'image, jamais déformée.
    private void LateUpdate()
    {
        var parentRt = transform.parent as RectTransform;
        if (parentRt == null || _rt == null) return;
        float side = guideCarreRatio * Mathf.Min(parentRt.rect.width, parentRt.rect.height);
        _rt.sizeDelta = new Vector2(side, side);
    }

    public void SetStable(bool stable)
    {
        Color c = stable ? couleurVert : couleurNoir;
        foreach (var img in bordures) if (img != null) img.color = c;
    }

    public void SetCellulesCouleurs(string[] couleurs9)
    {
        if (celluleSwatches == null || couleurs9 == null) return;
        for (int i = 0; i < celluleSwatches.Length && i < couleurs9.Length; i++)
        {
            if (celluleSwatches[i] == null) continue;
            celluleSwatches[i].color = CouleurDepuisNom(couleurs9[i]);
        }
    }

    public void SetCubeDetecte(bool detecte) { }

    private Color CouleurDepuisNom(string nom)
    {
        switch (nom)
        {
            case "BLANC":  return new Color(1f, 1f, 1f, 0.85f);
            case "JAUNE":  return new Color(1f, 0.92f, 0f, 0.85f);
            case "ORANGE": return new Color(1f, 0.55f, 0f, 0.85f);
            case "ROUGE":  return new Color(0.85f, 0.1f, 0.1f, 0.85f);
            case "VERT":   return new Color(0.1f, 0.7f, 0.2f, 0.85f);
            case "BLEU":   return new Color(0.1f, 0.3f, 0.9f, 0.85f);
            default:       return new Color(0.5f, 0.5f, 0.5f, 0.3f);
        }
    }
}