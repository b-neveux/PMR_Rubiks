using TMPro;
using UnityEngine;

public class ARUIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI instructionText;
    [SerializeField] private TextMeshProUGUI statusText;

    public void ShowInstruction(string texte) => instructionText.text = texte;

    public void ShowStatus(string texte)
    {
        statusText.color = Color.white;
        statusText.text = texte;
    }

    public void ShowError(string texte)
    {
        statusText.color = Color.red;
        statusText.text = "⚠ " + texte;
    }

    public void ShowFaceResult(string faceId, string[] couleurs9) =>
        ShowStatus($"Face {faceId} validée : {string.Join(",", couleurs9)}");

    public void ShowSolutionSummary(int nbMouvements) =>
        ShowStatus($"Solution trouvée : {nbMouvements} mouvement(s).");
}