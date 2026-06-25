using System.Collections.Generic;
using UnityEngine;

public class Cube3DView : MonoBehaviour
{
    [SerializeField] private GameObject cubiePrefab;
    [SerializeField] private float spacing = 1.0f;
    [SerializeField] private ColorPalette palette;

    private readonly Dictionary<Vector3Int, GameObject> _cubies = new();

    private void Awake()
    {
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            if (x == 0 && y == 0 && z == 0) continue;
            var pos = new Vector3Int(x, y, z);
            var go = Instantiate(cubiePrefab, transform);
            go.transform.localPosition = new Vector3(x, y, z) * spacing;
            _cubies[pos] = go;
        }
    }

    public void AppliquerEtat(Dictionary<string, string[][]> faces)
    {
        ApplyFace(faces["F"], (r, c) => new Vector3Int(c - 1, 1 - r, 1),  "F");
        ApplyFace(faces["B"], (r, c) => new Vector3Int(1 - c, 1 - r, -1), "B");
        ApplyFace(faces["U"], (r, c) => new Vector3Int(c - 1, 1, r - 1),  "U");
        ApplyFace(faces["D"], (r, c) => new Vector3Int(c - 1, -1, 1 - r), "D");
        ApplyFace(faces["R"], (r, c) => new Vector3Int(1, 1 - r, 1 - c), "R");
        ApplyFace(faces["L"], (r, c) => new Vector3Int(-1, 1 - r, c - 1), "L");
    }

    private void ApplyFace(string[][] matrice, System.Func<int,int,Vector3Int> versCubiePos, string faceLabel)
    {
        for (int r = 0; r < 3; r++)
        for (int c = 0; c < 3; c++)
        {
            var pos = versCubiePos(r, c);
            if (!_cubies.TryGetValue(pos, out var cubieGo)) continue;
            var sticker = cubieGo.transform.Find($"Sticker_{faceLabel}");
            if (sticker == null) continue;
            var renderer = sticker.GetComponent<Renderer>();
            renderer.material.color = palette.GetColor(matrice[r][c]);
        }
    }
}