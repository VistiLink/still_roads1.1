using UnityEngine;
using UnityEngine.UIElements;

public class HUDMenu : MonoBehaviour
{
    [Header("Clock Settings")]
    [Tooltip("Сколько реальных секунд = 1 игровой минуте")]
    [SerializeField] private float secondsPerMinute = 1f;

    private Label clockLabel;

    private int hours;
    private int minutes;
    private float accumulator;

    private const string ContainerName = "HandbrakeUI";
    private const string ClockName = "Clock";

    private const string PrefHoursKey = "Clock_Hours";
    private const string PrefMinutesKey = "Clock_Minutes";

    // ================================
    void Awake()
    {
        FindClockLabel();
        LoadTime();
        UpdateClockText();
    }

    // ================================
    void Update()
    {
        // Работает даже при Time.timeScale = 0
        accumulator += Time.unscaledDeltaTime;

        while (accumulator >= secondsPerMinute)
        {
            accumulator -= secondsPerMinute;
            IncrementMinute();
        }
    }

    // ================================
    private void FindClockLabel()
    {
        UIDocument doc = FindObjectOfType<UIDocument>();
        if (doc == null)
        {
            Debug.LogError("[CLOCK] UIDocument не найден в сцене");
            return;
        }

        VisualElement root = doc.rootVisualElement;
        if (root == null)
        {
            Debug.LogError("[CLOCK] rootVisualElement == null");
            return;
        }

        VisualElement container = root.Q<VisualElement>(ContainerName);
        if (container == null)
        {
            Debug.LogError($"[CLOCK] Контейнер '{ContainerName}' не найден. Проверь name в UI Builder");
            return;
        }

        clockLabel = container.Q<Label>(ClockName);
        if (clockLabel == null)
        {
            Debug.LogError($"[CLOCK] Label '{ClockName}' не найден. Проверь name в UI Builder");
            return;
        }

        Debug.Log("[CLOCK] Clock успешно найден и подключён");
    }

    // ================================
    private void IncrementMinute()
    {
        minutes++;

        if (minutes >= 60)
        {
            minutes = 0;
            hours++;

            if (hours >= 24)
                hours = 0;
        }

        UpdateClockText();
        SaveTime();
    }

    // ================================
    private void UpdateClockText()
    {
        if (clockLabel == null)
            return;

        clockLabel.text = $"{hours:00}:{minutes:00}";
    }

    // ================================
    private void LoadTime()
    {
        hours = PlayerPrefs.GetInt(PrefHoursKey, 0);
        minutes = PlayerPrefs.GetInt(PrefMinutesKey, 0);
        accumulator = 0f;
    }

    private void SaveTime()
    {
        PlayerPrefs.SetInt(PrefHoursKey, hours);
        PlayerPrefs.SetInt(PrefMinutesKey, minutes);
        PlayerPrefs.Save();
    }

    void OnApplicationQuit() => SaveTime();
    void OnApplicationPause(bool pause) { if (pause) SaveTime(); }
}
