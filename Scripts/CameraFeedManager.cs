using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.XR.MagicLeap;
#endif

/// <summary>
/// Fournit la dernière image caméra disponible sous forme de Color32[] + dimensions.
/// Dans l'éditeur Unity (PC), utilise la webcam générique (WebCamTexture).
/// Sur le casque Magic Leap 2, WebCamTexture n'est PAS supporté : on bascule
/// automatiquement sur l'API MLCamera (Main Camera, format RGBA_8888, qui
/// correspond directement à un tableau de Color32 sans conversion supplémentaire).
/// </summary>
public class CameraFeedManager : MonoBehaviour
{
    [SerializeField] private int targetWidth = 1280;
    
    [SerializeField] private int targetHeight = 720;

    private const float FrameMaxAgeS = 1.0f;
    private readonly object _lock = new object();

    private Color32[] _latestPixels;
    private int _latestWidth, _latestHeight;
    private float _latestTimestamp;

    // --- Chemin Editeur : WebCamTexture --------------------------------------
    private WebCamTexture _webCamTexture;
    private Color32[] _editorBuffer;

#if UNITY_ANDROID && !UNITY_EDITOR
    // --- Chemin Casque : MLCamera ---------------------------------------------
    private MLCamera _mlCamera;
    private bool _mlCameraReady;
#endif

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(StartMagicLeapCameraRoutine());
#else
        StartWebCamTexture();
#endif
    }

    private void StartWebCamTexture()
    {
        _webCamTexture = new WebCamTexture(targetWidth, targetHeight, 30);
        _webCamTexture.Play();
        _editorBuffer = new Color32[targetWidth * targetHeight];
    }

    private void Update()
    {
        if (_webCamTexture != null && _webCamTexture.didUpdateThisFrame)
        {
            int w = _webCamTexture.width, h = _webCamTexture.height;
            if (_editorBuffer.Length != w * h) _editorBuffer = new Color32[w * h];
            _webCamTexture.GetPixels32(_editorBuffer);

            lock (_lock)
            {
                _latestPixels = _editorBuffer;
                _latestWidth = w; _latestHeight = h;
                _latestTimestamp = Time.realtimeSinceStartup;
            }
        }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private System.Collections.IEnumerator StartMagicLeapCameraRoutine()
    {
        // CORRECTION 1 : Utilisation des permissions Android standards requises par le nouveau SDK ML2
        string cameraPermission = UnityEngine.Android.Permission.Camera;
        while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(cameraPermission))
        {
            UnityEngine.Android.Permission.RequestUserPermission(cameraPermission);
            yield return new WaitForSeconds(0.5f);
        }

        // CORRECTION 2 : Suppression de IsCaptureAvailable() obsolète. 
        // On attend que l'API système de la caméra soit prête de manière moderne.
        bool isContextReady = false;
        while (!isContextReady)
        {
            MLResult result = MLCamera.GetDeviceAvailabilityStatus(MLCamera.Identifier.Main, out bool isAvailable);
            if (result.IsOk && isAvailable)
            {
                isContextReady = true;
            }
            else
            {
                yield return new WaitForSeconds(0.25f);
            }
        }

        // CORRECTION 3 : Nouvelle structure ConnectContext exigée par le SDK récent
        MLCamera.ConnectContext connectContext = new MLCamera.ConnectContext
        {
            CamId = MLCamera.Identifier.Main,
            Flags = MLCamera.ConnectFlag.CamOnly
        };

        // CORRECTION 4 : CreateAndConnect prend désormais le contexte en paramètre direct
        // CODE CORRIGÉ : Version à 1 seul argument
        // CORRECTION 4 : Version à 1 seul argument
        // CORRECTION 4 : Version à 1 seul argument
        _mlCamera = MLCamera.CreateAndConnect(connectContext);

        // On vérifie si la connexion a réussi en testant si l'objet n'est pas nul
        if (_mlCamera == null)
        {
            Debug.LogError("[CameraFeedManager] Impossible de se connecter à la caméra : l'instance retournée est nulle.");
            yield break;
        }

        var streamConfig = new MLCamera.CaptureStreamConfig[]
        {
            new MLCamera.CaptureStreamConfig
            {
                OutputFormat = MLCamera.OutputFormat.RGBA_8888,
                Width = targetWidth,
                Height = targetHeight,
                CaptureType = MLCamera.CaptureType.Video,
            }
        };
        var captureConfig = new MLCamera.CaptureConfig
        {
            StreamConfigs = streamConfig,
            CaptureFrameRate = MLCamera.CaptureFrameRate._30FPS,
        };

        _mlCamera.PrepareCapture(captureConfig, out MLCamera.Metadata _);
        _mlCamera.OnRawVideoFrameAvailable += OnMlCameraFrame;
        _mlCamera.CaptureVideoStart();
        _mlCameraReady = true;
    }

    private void OnMlCameraFrame(MLCamera.CameraOutput output, MLCamera.ResultExtras extras, MLCamera.Metadata metadata)
    {
        // RGBA_8888 : chaque pixel est déjà 4 octets R,G,B,A -> conversion directe en Color32.
        var plane = output.Planes[0];
        int w = (int)plane.Width, h = (int)plane.Height;
        var pixels = new Color32[w * h];
        byte[] data = plane.Data;
        for (int i = 0; i < pixels.Length; i++)
        {
            int o = i * 4;
            pixels[i] = new Color32(data[o], data[o + 1], data[o + 2], data[o + 3]);
        }

        lock (_lock)
        {
            _latestPixels = pixels;
            _latestWidth = w; _latestHeight = h;
            _latestTimestamp = Time.realtimeSinceStartup;
        }
    }
#endif

    /// <summary>Equivalent de CameraBuffer.lire() : copie thread-safe de la dernière
    /// frame, ou false si absente/périmée (caméra figée).</summary>
    public bool TryGetLatestFrame(out Color32[] pixels, out int width, out int height)
    {
        lock (_lock)
        {
            if (_latestPixels == null || Time.realtimeSinceStartup - _latestTimestamp > FrameMaxAgeS)
            {
                pixels = null; width = 0; height = 0;
                return false;
            }
            pixels = _latestPixels; width = _latestWidth; height = _latestHeight;
            return true;
        }
    }

    private void OnDestroy()
    {
        _webCamTexture?.Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (_mlCamera != null && _mlCameraReady)
        {
            _mlCamera.CaptureVideoStop();
            _mlCamera.Disconnect();
        }
#endif
    }
}