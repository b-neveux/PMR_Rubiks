using UnityEngine;
using UnityEngine.UI;

public class CameraPreviewUI : MonoBehaviour
{
    [SerializeField] private RawImage rawImage;
    [SerializeField] private CameraFeedManager cameraFeed;
    [SerializeField] private AspectRatioFitter aspectFitter;

    private Texture2D _previewTex;

    private void Awake()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Sur le casque : on masque la retransmission caméra (passthrough natif),
        // mais on garde le GameObject actif pour que GuideSquare reste visible.
        if (rawImage != null) rawImage.enabled = false;
        if (aspectFitter != null) aspectFitter.enabled = false;
#endif
    }

    private void Update()
    {
#if UNITY_EDITOR
        // Retransmission caméra uniquement en éditeur PC
        if (!cameraFeed.TryGetLatestFrame(out Color32[] pixels, out int w, out int h)) return;
        if (_previewTex == null || _previewTex.width != w || _previewTex.height != h)
        {
            _previewTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            rawImage.texture = _previewTex;
            if (aspectFitter != null) aspectFitter.aspectRatio = (float)w / h;
        }
        _previewTex.SetPixels32(pixels);
        _previewTex.Apply();
#endif
    }
}