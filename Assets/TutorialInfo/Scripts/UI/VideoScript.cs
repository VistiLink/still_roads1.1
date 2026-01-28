using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using System.Collections;

public class VideoScript : MonoBehaviour
{
    [Tooltip("Сцена, которую нужно загрузить по окончании ролика")]
    public string sceneToLoad = "SampleScene";

    [Tooltip("Время затухания (секунд) перед концом ролика (и после)")]
    public float fadeDuration = 2f;

    private VideoPlayer videoPlayer;
    private Image overlayImage;
    private GameObject overlayCanvasGO;
    private bool fadeStarted = false;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        if (videoPlayer == null)
        {
            Debug.LogError("[VideoScript] VideoPlayer компонент не найден на GameObject.");
            return;
        }

        if (videoPlayer.isPrepared)
        {
            SetupOverlayAndListeners();
        }
        else
        {
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.Prepare();
        }
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        videoPlayer.prepareCompleted -= OnVideoPrepared;
        SetupOverlayAndListeners();
    }

    private void SetupOverlayAndListeners()
    {
        CreateOverlay();
        videoPlayer.loopPointReached += OnVideoEnded;
        StartCoroutine(MonitorFadeStart());
    }

    private void CreateOverlay()
    {
        overlayCanvasGO = new GameObject("VideoOverlayCanvas");
        var canvas = overlayCanvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        overlayCanvasGO.AddComponent<CanvasScaler>();
        overlayCanvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("OverlayImage");
        imgGO.transform.SetParent(overlayCanvasGO.transform, false);
        overlayImage = imgGO.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0f);

        var rt = overlayImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator MonitorFadeStart()
    {
        // Дождёмся начала воспроизведения или хотя бы наличия длины
        while (!videoPlayer.isPlaying)
        {
            if (videoPlayer.length > 0 && videoPlayer.isPrepared)
                break;
            yield return null;
        }

        // Следим за оставшимся временем и запускаем предфейд за fadeDuration секунд
        while (videoPlayer != null && (videoPlayer.isPlaying || videoPlayer.isPrepared))
        {
            double remaining = videoPlayer.length - videoPlayer.time;
            if (!fadeStarted && remaining <= fadeDuration)
            {
                fadeStarted = true;
                // Сделаем Canvas неразрушаемым, чтобы оверлей пережил загрузку сцены
                DontDestroyOnLoad(overlayCanvasGO);
                float preDuration = Mathf.Max(0.01f, Mathf.Min(fadeDuration, (float)remaining));
                StartCoroutine(FadeImage(overlayImage, 0f, 1f, preDuration));
                yield break;
            }
            yield return null;
        }
    }

    // Универсальная корутина фейда для Image (может вызываться до загрузки сцены)
    private IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (img == null)
            yield break;

        if (duration <= 0f)
        {
            SetOverlayAlpha(to);
            yield break;
        }

        float elapsed = 0f;
        SetOverlayAlpha(from);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            SetOverlayAlpha(alpha);
            yield return null;
        }
        SetOverlayAlpha(to);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayImage != null)
        {
            var c = overlayImage.color;
            c.a = alpha;
            overlayImage.color = c;
        }
    }

    private void OnVideoEnded(VideoPlayer vp)
    {
        // Отпишемся от события, чтобы не вызвать повторно.
        videoPlayer.loopPointReached -= OnVideoEnded;

        // Запускаем обратный фейд (1 -> 0) на объекте, который переживёт загрузку сцены.
        if (overlayCanvasGO != null)
        {
            // Добавим контроллер фейда к overlayCanvasGO, чтобы корутина выполнялась после загрузки сцены
            var fader = overlayCanvasGO.AddComponent<OverlayFader>();
            fader.StartFade(overlayImage, 1f, 0f, fadeDuration);
        }

        // Загружаем сцену сразу после окончания ролика
        SceneManager.LoadScene(sceneToLoad);
    }
}

// Вспомогательный компонент, который остаётся на overlayCanvasGO и выполняет обратный фейд + удаление
public class OverlayFader : MonoBehaviour
{
    public void StartFade(Image image, float from, float to, float duration)
    {
        StartCoroutine(FadeAndDestroy(image, from, to, duration));
    }

    private IEnumerator FadeAndDestroy(Image img, float from, float to, float duration)
    {
        if (img == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (duration <= 0f)
        {
            var c = img.color;
            c.a = to;
            img.color = c;
            Destroy(gameObject);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(from, to, t);
            var c = img.color;
            c.a = alpha;
            img.color = c;
            yield return null;
        }

        var final = img.color;
        final.a = to;
        img.color = final;

        Destroy(gameObject);
    }
}