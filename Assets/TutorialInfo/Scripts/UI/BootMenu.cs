using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public class BootMenu : MonoBehaviour
{
    [Header("Settings")]
    public float delayBeforeClose = 10f;

    // Имена элементов, которые будем искать и переключать
    public string bootContainerName = "BootMenuContainer";
    public string mainMenuName = "MainMenu";
    public string overlayName = "Boot_BlackOverlay";

    private void Start()
    {
        StartCoroutine(SwitchUiAfterDelay());
    }

    private IEnumerator SwitchUiAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeClose);

        bool changed = false;

        var docs = Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var doc in docs)
        {
            if (doc == null) continue;
            var root = doc.rootVisualElement;
            if (root == null) continue;

            // Скрыть/удалить явно именованные элементы
            var overlay = root.Q<VisualElement>(overlayName);
            if (overlay != null)
            {
                TryHide(overlay);
                Debug.Log($"BootMenu: removed overlay \"{overlayName}\" from document {doc.name}");
                changed = true;
            }

            var boot = root.Q<VisualElement>(bootContainerName);
            if (boot != null)
            {
                TryHide(boot);
                Debug.Log($"BootMenu: hidden/removed \"{bootContainerName}\" in document {doc.name}");
                changed = true;
            }

            // Показать основное меню
            var main = root.Q<VisualElement>(mainMenuName);
            if (main != null)
            {
                try { main.style.display = DisplayStyle.Flex; } catch { }
                try { main.pickingMode = PickingMode.Position; } catch { }
                Debug.Log($"BootMenu: shown \"{mainMenuName}\" in document {doc.name}");
                changed = true;
            }

            // Дополнительно: если где-то остаётся крупный чёрный фон — найти элементы с чёрным фоном,
            // занимающие значительную часть экрана, и скрыть их.
            var all = root.Query<VisualElement>().ToList();
            foreach (var ve in all)
            {
                if (ve == null) continue;

                // проверка на чёрный фон по вычисленному стилю
                var bg = ve.resolvedStyle.backgroundColor;
                bool isBlack = IsBlack(bg);

                // размер в мировых координатах (покрывает ли экран значительную часть)
                var wb = ve.worldBound;
                bool coversLargeArea = wb.width >= Screen.width * 0.5f && wb.height >= Screen.height * 0.5f;

                if (isBlack && coversLargeArea)
                {
                    TryHide(ve);
                    Debug.Log($"BootMenu: hidden large black VisualElement \"{ve.name}\" (size {wb.width}x{wb.height}) in document {doc.name}");
                    changed = true;
                }
            }
        }

        Debug.Log(changed ? "BootMenu: UI switch completed." : "BootMenu: ничего не изменено — элементы не найдены.");
    }

    private static void TryHide(VisualElement ve)
    {
        if (ve == null) return;
        try { ve.style.display = DisplayStyle.None; } catch { }
        try { ve.pickingMode = PickingMode.Ignore; } catch { }
        try { ve.style.opacity = 0f; } catch { }
        try { ve.RemoveFromHierarchy(); } catch { }
    }

    private static bool IsBlack(Color c)
    {
        // порог небольшого оттенка серого/чёрного
        const float thr = 0.06f;
        return c.a > 0.8f && c.r <= thr && c.g <= thr && c.b <= thr;
    }
}