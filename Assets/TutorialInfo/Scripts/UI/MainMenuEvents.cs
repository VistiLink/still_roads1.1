using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UIE = UnityEngine.UIElements;
using UnityEngine.UIElements; // нужен для extension-методов Q, Query и ChangeEvent/type names
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuEvents : MonoBehaviour
{
    // singleton для доступа извне и чтобы объект был один между сценами
    public static MainMenuEvents Instance { get; private set; }

    private UnityEngine.UIElements.UIDocument _document;

    public AudioSource musicSource;
    public AudioSource sfxSource;

    private UIE.Button _startButton;
    private UIE.Button _settingsButton;
    private UIE.Button _backButton;

    private UIE.VisualElement settingsMenu;
    private UIE.VisualElement mainMenu;
    private UIE.Button _settingsGameButton;
    private UIE.Button _controlButton;

    private UIE.VisualElement gameSettingsMenu;
    private UIE.Toggle _fullscreenToggle;
    private UIE.Toggle _vsyngToggle;
    private UIE.DropdownField _resolutionDropdown;
    private UIE.Slider _masterVolumeSlider;
    private UIE.Slider _musicVolumeSlider;
    private UIE.Button _backToSettingsButton;

    // Slider может быть либо стандартный Slider (float), либо кастомный SliderInt в UXML.
    private UIE.VisualElement _fpsSliderElement; // любой элемент с именем LockFPSSlider
    private UIE.Slider _fpsSlider;               // стандартный Slider если есть
    private UIE.Label _fpsText;                  // теперь Label — только отображение

    // New: FPS counter UI elements
    private UIE.Button _counterFpsButton; // CounterFPSButton
    private UIE.Label _fpsGameLabel;      // FPSGame (отображает сам FPS)
    private bool _fpsCounterEnabled = false;
    private const string PrefFpsCounter = "FpsCounterEnabled";

    // FPS sampling using timestamps (more accurate than accumulated deltas)
    private readonly float _fpsUpdateInterval = 1f; // обновлять счётчик каждые 1 сек
    private float _fpsLastSampleTime;
    private int _fpsFrameCount;

    // Persistent overlay (uGUI) чтобы отображать FPS между сценами (опционально)
    private static GameObject _persistentFpsGO;
    private static Text _persistentFpsText;
    private const string PersistentFpsName = "_PersistentFPSOverlay";

    private List<UIE.Button> _menuButtons = new List<UIE.Button>();
    private UIE.VisualElement controlMenu;

    private float _masterVolume = 1f;
    private float _musicVolume = 1f;

    private bool _vSyncEnabled = true;

    private const string PrefMaster = "MasterVolume";
    private const string PrefMusic = "MusicVolume";
    private const string PrefVSync = "VSyncEnabled";
    private const string PrefFps = "FpsLimit"; // -1 = Unlimited

    // Флаг — найден ли SliderInt (чтобы отписаться корректно)
    private bool _hasSliderInt = false;

    // вспомогательные поля для in-place редактора (оставлены, если понадобятся позже)
    private UIE.TextField _inplaceEditor;
    private UIE.EventCallback<UIE.KeyDownEvent> _inplaceKeyHandler;
    private Action _removeInplaceEditor;

    // Корутина измерения фактического FPS (защита от параллельных запущенных проверок)
    private Coroutine _fpsMeasureCoroutine;

    // Новый флаг: меню подавлено после нажатия Start — не показывать автоматически до нажатия Esc
    private bool _menuSuppressed = false;

    // Временная отладочная опция — форсировать показ меню при старте (можно убрать после отладки)
    [SerializeField] private bool _debugForceShowMenu = true;

    private void Awake()
    {
        // singleton + persist
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // подписка на смену сцен — чтобы переназначать UI Toolkit Label "FPSGame"
        SceneManager.sceneLoaded += OnSceneLoaded;

        _document = GetComponent<UnityEngine.UIElements.UIDocument>();
        if (_document == null)
        {
            Debug.LogError("UIDocument не найден на объекте!");
            // не возвращаемся — скрипт всё равно будет обслуживать persistent overlay и UI, если нужен
        }
        else
        {
            var ss = Resources.Load<UIE.StyleSheet>("UI/no-select");
            if (ss != null)
                _document.rootVisualElement.styleSheets.Add(ss);
            else
                Debug.LogWarning("StyleSheet UI/no-select.uss не найден в Assets/Resources/UI");

            Debug.Log($"no-select stylesheet loaded: {ss != null}");

            // Q() extension-метод доступен благодаря using UnityEngine.UIElements;
            settingsMenu = _document.rootVisualElement.Q<UIE.VisualElement>("SettingsMenu") ?? _document.rootVisualElement.Q<UIE.VisualElement>("SettingsGame");
            mainMenu = _document.rootVisualElement.Q<UIE.VisualElement>("MainMenu");

            gameSettingsMenu = _document.rootVisualElement.Q<UIE.VisualElement>("GameSettings") ?? _document.rootVisualElement.Q<UIE.VisualElement>("MenuSettingsGame");
            controlMenu = _document.rootVisualElement.Q<UIE.VisualElement>("ControlMenu");

            // Скрываем вспомогательные панели
            if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.None;
            if (gameSettingsMenu != null) gameSettingsMenu.style.display = UIE.DisplayStyle.None;
            if (controlMenu != null) controlMenu.style.display = UIE.DisplayStyle.None;

            // ВАЖНО: по умолчанию скрываем главное меню — оно будет показано через OpenMainMenu().
            // Это даёт совместимость с BootMenu, который ожидает, что меню скрыто в начале.
            if (mainMenu != null)
            {
                mainMenu.style.display = UIE.DisplayStyle.None;
                Debug.Log("MainMenuEvents: mainMenu initially hidden; will be shown by OpenMainMenu().");
            }

            _startButton = _document.rootVisualElement.Q<UIE.Button>("StartGameButton");
            _settingsButton = _document.rootVisualElement.Q<UIE.Button>("Settings");

            _settingsGameButton = _document.rootVisualElement.Q<UIE.Button>("SettingsGame");
            _controlButton = _document.rootVisualElement.Q<UIE.Button>("control");
            _backButton = _document.rootVisualElement.Q<UIE.Button>("backButton");

            _fullscreenToggle = _document.rootVisualElement.Q<UIE.Toggle>("FullscreenToggle");
            _vsyngToggle = _document.rootVisualElement.Q<UIE.Toggle>("VsyngToggle");
            _resolutionDropdown = _document.rootVisualElement.Q<UIE.DropdownField>("ResolutionDropdown");
            _masterVolumeSlider = _document.rootVisualElement.Q<UIE.Slider>("MasterVolumeSlider");
            _musicVolumeSlider = _document.rootVisualElement.Q<UIE.Slider>("MusicVolumeSlider");
            _backToSettingsButton = _document.rootVisualElement.Q<UIE.Button>("BackToSettingsMenu");

            // Поиск слайдера: сначала как стандартный Slider, иначе как любой элемент с этим именем
            _fpsSlider = _document.rootVisualElement.Q<UIE.Slider>("LockFPSSlider");
            _fpsSliderElement = _document.rootVisualElement.Q<UIE.VisualElement>("LockFPSSlider");

            // FPSText — теперь Label (только отображение)
            _fpsText = _document.rootVisualElement.Q<UIE.Label>("FPSText");

            // New: CounterFPSButton и FPSGame
            _counterFpsButton = _document.rootVisualElement.Q<UIE.Button>("CounterFPSButton");
            _fpsGameLabel = _document.rootVisualElement.Q<UIE.Label>("FPSGame");

            // Регистрация остальных обработчиков (частично — для совместимости)
            if (_startButton != null) _startButton.RegisterCallback<UIE.ClickEvent>(OnPlayGameClick);
            if (_settingsButton != null) _settingsButton.RegisterCallback<UIE.ClickEvent>(OpenSettings);
            if (_backButton != null) _backButton.RegisterCallback<UIE.ClickEvent>(CloseSettings);
            if (_settingsGameButton != null) _settingsGameButton.RegisterCallback<UIE.ClickEvent>(OpenGameSettings);
            if (_controlButton != null) _controlButton.RegisterCallback<UIE.ClickEvent>(OpenControlMenu);
            if (_backToSettingsButton != null) _backToSettingsButton.RegisterCallback<UIE.ClickEvent>(BackToSettingsFromGame);

            if (_counterFpsButton != null)
                _counterFpsButton.RegisterCallback<UIE.ClickEvent>(OnCounterFpsButtonClick);

            // Получаем все кнопки один раз (Query -> ToList один раз)
            _menuButtons = _document.rootVisualElement.Query<UIE.Button>().ToList();
            for (int i = 0; i < _menuButtons.Count; i++)
                _menuButtons[i].RegisterCallback<UIE.ClickEvent>(OnAllButtonsClick);

            if (_fullscreenToggle != null) _fullscreenToggle.RegisterCallback<UIE.ChangeEvent<bool>>(OnFullscreenToggled);
            if (_vsyngToggle != null) _vsyngToggle.RegisterCallback<UIE.ChangeEvent<bool>>(OnVSyncToggled);
            // Регистрация OnResolutionChanged перенесена ниже после заполнения списка разрешений
            if (_masterVolumeSlider != null) _masterVolumeSlider.RegisterCallback<UIE.ChangeEvent<float>>(OnMasterVolumeChanged);
            if (_musicVolumeSlider != null) _musicVolumeSlider.RegisterCallback<UIE.ChangeEvent<float>>(OnMusicVolumeChanged);

            if (_fpsSlider != null) _fpsSlider.RegisterCallback<UIE.ChangeEvent<float>>(OnFpsSliderChanged);

            // Populate resolution dropdown with available resolutions and set current value
            if (_resolutionDropdown != null)
            {
                try
                {
                    var systemResolutions = Screen.resolutions;
                    var options = systemResolutions
                        .Select(r => $"{r.width}x{r.height}")
                        .Distinct()
                        .ToList();

                    // Fallback если Screen.resolutions пуст
                    if (options.Count == 0)
                        options.Add($"{Screen.width}x{Screen.height}");

                    _resolutionDropdown.choices = options;

                    // Определяем текущий выбор (предпочитаем currentResolution)
                    var current = $"{Screen.currentResolution.width}x{Screen.currentResolution.height}";
                    if (!options.Contains(current))
                        current = $"{Screen.width}x{Screen.height}";

                    _resolutionDropdown.SetValueWithoutNotify(current);

                    // Подписываемся на изменения
                    _resolutionDropdown.RegisterCallback<UIE.ChangeEvent<string>>(OnResolutionChanged);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Ошибка при заполнении ResolutionDropdown: " + ex.Message);
                }
            }
        }

        // Defaults
        if (!PlayerPrefs.HasKey(PrefMaster)) PlayerPrefs.SetFloat(PrefMaster, 0.5f);
        if (!PlayerPrefs.HasKey(PrefMusic)) PlayerPrefs.SetFloat(PrefMusic, 0.5f);
        if (!PlayerPrefs.HasKey(PrefVSync)) PlayerPrefs.SetInt(PrefVSync, 1);
        if (!PlayerPrefs.HasKey(PrefFps)) PlayerPrefs.SetInt(PrefFps, -1);
        if (!PlayerPrefs.HasKey(PrefFpsCounter)) PlayerPrefs.SetInt(PrefFpsCounter, 0); // выключено по умолчанию
        PlayerPrefs.Save();

        _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMaster, 0.5f));
        _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefMusic, 0.5f));
        _vSyncEnabled = PlayerPrefs.GetInt(PrefVSync, 1) != 0;
        _fpsCounterEnabled = PlayerPrefs.GetInt(PrefFpsCounter, 0) != 0;

        AudioListener.volume = _masterVolume;
        QualitySettings.vSyncCount = _vSyncEnabled ? 1 : 0;

        if (_masterVolumeSlider != null)
            _masterVolumeSlider.SetValueWithoutNotify(NormalizedToSliderValue(_masterVolumeSlider, _masterVolume));
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.SetValueWithoutNotify(NormalizedToSliderValue(_musicVolumeSlider, _musicVolume));
        if (_fullscreenToggle != null)
            _fullscreenToggle.SetValueWithoutNotify(Screen.fullScreen);
        if (_vsyngToggle != null)
            _vsyngToggle.SetValueWithoutNotify(_vSyncEnabled);

        // FPS init for sliders (if present)
        int storedFps = PlayerPrefs.GetInt(PrefFps, -1);
        if (_fpsSlider != null)
        {
            _fpsSlider.lowValue = 15f;
            _fpsSlider.highValue = 301f; // sentinel = Unlimited
            if (storedFps <= 0) _fpsSlider.SetValueWithoutNotify(_fpsSlider.highValue);
            else _fpsSlider.SetValueWithoutNotify(Mathf.Clamp(storedFps, 15, 300));
        }
        else if (_fpsSliderElement != null)
        {
            var typeName = _fpsSliderElement.GetType().Name;
            if (typeName == "SliderInt" || typeName == "IntSlider" || typeName.ToLower().Contains("sliderint"))
            {
                _hasSliderInt = TrySetupSliderInt(_fpsSliderElement, storedFps);
                if (!_hasSliderInt)
                    Debug.LogWarning($"LockFPSSlider найден как {typeName}, но не удалось получить доступ через reflection.");
            }
            else
            {
                Debug.LogWarning($"LockFPSSlider найден как элемент типа {_fpsSliderElement.GetType().Name} — если хотите работать с ним, пришлите фрагмент UXML или замените на стандартный Slider.");
            }
        }

        // Устанавливаем FPSText только для отображения — пользователь меняет значение через ползунок.
        if (_fpsText != null)
        {
            if (storedFps <= 0) _fpsText.text = "Unlimited";
            else _fpsText.text = storedFps.ToString();

            _fpsText.AddToClassList("no-select");
        }

        ApplyStoredFps(storedFps);

        // init fps sampling
        _fpsLastSampleTime = Time.unscaledTime;
        _fpsFrameCount = 0;
        if (_fpsGameLabel != null && _fpsCounterEnabled)
            _fpsGameLabel.text = "0 FPS";

        // Создать persistent overlay (если еще не создан) — будет жить между сценами
        EnsurePersistentFpsOverlay();

        ApplyMusicVolume();

        // Apply initial state of FPS counter UI (button text + label visibility)
        ApplyFpsCounterState();

        // если UIDocument есть, попытаться назначить label из текущей сцены/документа
        AssignLabelInCurrentScene();

        // гарантируем, что UIDocument имеет корректную Event Camera (если PanelSettings требует её)
        EnsureUIDocumentEventCamera();

        // гарантируем, что EventSystem есть (нужно для фокуса/клика в UI Toolkit в рантайме)
        if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            DontDestroyOnLoad(esGO);
        }

        // Диагностика состояния UIDocument / rootVisualElement — логируем состав и видимость
        LogUIDocumentState();

        // Временное: принудительно показать главное меню для проверки (можно отключить после отладки)
        if (_debugForceShowMenu && mainMenu != null)
        {
            try
            {
                _menuSuppressed = false;
                mainMenu.style.display = UIE.DisplayStyle.Flex;
                Debug.Log("DEBUG: mainMenu принудительно показано (debugForceShowMenu = true).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("DEBUG: не удалось принудительно показать mainMenu: " + ex.Message);
            }
        }
    }

    private void OnDestroy()
    {
        // отписка от события загрузки сцены
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // Переназначение метки при загрузке новой сцены
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AssignLabelInCurrentScene();
    }

    // Ищем Label "FPSGame" во всех UIDocument в сцене и сохраняем ссылку
    private void AssignLabelInCurrentScene()
    {
        _fpsGameLabel = null;

        // Use newer API when available to avoid deprecation warnings
#if UNITY_2023_2_OR_NEWER
        var documents = UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsSortMode.None);
#else
        var documents = FindObjectsOfType<UnityEngine.UIElements.UIDocument>();
#endif
        if (documents == null || documents.Length == 0) return;

        foreach (var doc in documents)
        {
            try
            {
                var root = doc.rootVisualElement;
                if (root == null) continue;
                var label = root.Q<UIE.Label>("FPSGame");
                if (label != null)
                {
                    _fpsGameLabel = label;
                    // применить текущее состояние видимости и текст
                    if (_fpsCounterEnabled)
                    {
                        _fpsGameLabel.style.display = DisplayStyle.Flex;
                        _fpsGameLabel.text = "0 FPS";
                    }
                    else
                    {
                        _fpsGameLabel.style.display = DisplayStyle.None;
                    }
                    return;
                }
            }
            catch { /* безопасно игнорируем возможные ошибки раннего доступа */ }
        }
    }

    private void EnsurePersistentFpsOverlay()
    {
        if (_persistentFpsGO != null) return;

        var existing = GameObject.Find(PersistentFpsName);
        if (existing != null)
        {
            _persistentFpsGO = existing;
            _persistentFpsText = existing.GetComponentInChildren<Text>();
            return;
        }

        _persistentFpsGO = new GameObject(PersistentFpsName);
        DontDestroyOnLoad(_persistentFpsGO);

        var canvas = _persistentFpsGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _persistentFpsGO.AddComponent<CanvasScaler>();
        _persistentFpsGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("FPSText");
        textGO.transform.SetParent(_persistentFpsGO.transform, false);
        _persistentFpsText = textGO.AddComponent<Text>();

        // Попытка загрузить шрифт из Assets/Other/sans (Editor) или использовать fallback.
        Font chosenFont = null;
#if UNITY_EDITOR
        try
        {
            // ищем файл шрифта с именем "sans" в папке Assets/Other
            var guids = AssetDatabase.FindAssets("sans t:font", new[] { "Assets/Other" });
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                chosenFont = AssetDatabase.LoadAssetAtPath<Font>(path);
            }
        }
        catch { chosenFont = null; }
#endif
        if (chosenFont == null)
        {
            try { chosenFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { chosenFont = null; }
            if (chosenFont == null)
            {
                try { chosenFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { chosenFont = null; }
            }
            if (chosenFont == null)
            {
                try { chosenFont = Font.CreateDynamicFontFromOSFont("Arial", 14); } catch { chosenFont = null; }
            }
        }

        if (chosenFont != null) _persistentFpsText.font = chosenFont;

        // стиль: чёрный, поменьше, слева-снизу
        _persistentFpsText.fontSize = 12;
        _persistentFpsText.color = Color.black;
        _persistentFpsText.alignment = TextAnchor.LowerLeft;
        _persistentFpsText.raycastTarget = false;

        var rt = _persistentFpsText.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(10f, 10f);
        rt.sizeDelta = new Vector2(200f, 30f);

        // Изначально скрыт — управление видимостью через ApplyFpsCounterState
        _persistentFpsGO.SetActive(_fpsCounterEnabled);
    }

    private void OnDisable()
    {
        // отписка UI callbacks (лучше оставить, если объект может быть отключён)
        try
        {
            if (_startButton != null) _startButton.UnregisterCallback<UIE.ClickEvent>(OnPlayGameClick);
            if (_settingsButton != null) _settingsButton.UnregisterCallback<UIE.ClickEvent>(OpenSettings);
            if (_backButton != null) _backButton.UnregisterCallback<UIE.ClickEvent>(CloseSettings);
            if (_settingsGameButton != null) _settingsGameButton.UnregisterCallback<UIE.ClickEvent>(OpenGameSettings);
            if (_controlButton != null) _controlButton.UnregisterCallback<UIE.ClickEvent>(OpenControlMenu);
            if (_backToSettingsButton != null) _backToSettingsButton.UnregisterCallback<UIE.ClickEvent>(BackToSettingsFromGame);

            if (_counterFpsButton != null)
                _counterFpsButton.UnregisterCallback<UIE.ClickEvent>(OnCounterFpsButtonClick);

            for (int i = 0; i < _menuButtons.Count; i++)
                _menuButtons[i].UnregisterCallback<UIE.ClickEvent>(OnAllButtonsClick);

            if (_fullscreenToggle != null) _fullscreenToggle.UnregisterCallback<UIE.ChangeEvent<bool>>(OnFullscreenToggled);
            if (_vsyngToggle != null) _vsyngToggle.UnregisterCallback<UIE.ChangeEvent<bool>>(OnVSyncToggled);
            if (_resolutionDropdown != null) _resolutionDropdown.UnregisterCallback<UIE.ChangeEvent<string>>(OnResolutionChanged);
            if (_masterVolumeSlider != null) _masterVolumeSlider.UnregisterCallback<UIE.ChangeEvent<float>>(OnMasterVolumeChanged);
            if (_musicVolumeSlider != null) _musicVolumeSlider.UnregisterCallback<UIE.ChangeEvent<float>>(OnMusicVolumeChanged);

            if (_fpsSlider != null) _fpsSlider.UnregisterCallback<UIE.ChangeEvent<float>>(OnFpsSliderChanged);

            if (_hasSliderInt && _fpsSliderElement != null)
                _fpsSliderElement.UnregisterCallback<UIE.ChangeEvent<int>>(OnFpsSliderIntChanged);
        }
        catch { /* игнорируем ошибки при отключении */ }
    }

    // --- Button handlers ---
    // Изменено: при нажатии Start загружается сцена "test1", меню скрывается и подавляется до нажатия Esc.
    private void OnPlayGameClick(UIE.ClickEvent evt)
    {
        if (sfxSource != null) sfxSource.Play();

        // Спрятать и подавить меню, затем загрузить сцену test1
        _menuSuppressed = true;
        if (mainMenu != null) mainMenu.style.display = UIE.DisplayStyle.None;

        try
        {
            SceneManager.LoadScene("test1");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Не удалось загрузить сцену 'test1': " + ex.Message);
        }
    }

    private void OpenSettings(UIE.ClickEvent evt)
    {
        if (mainMenu != null) mainMenu.style.display = UIE.DisplayStyle.None;
        if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.Flex;
    }

    private void CloseSettings(UIE.ClickEvent evt)
    {
        if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.None;
        if (mainMenu != null) mainMenu.style.display = UIE.DisplayStyle.Flex;
    }

    private void OpenGameSettings(UIE.ClickEvent evt)
    {
        if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.None;
        if (gameSettingsMenu != null) gameSettingsMenu.style.display = UIE.DisplayStyle.Flex;
    }

    private void BackToSettingsFromGame(UIE.ClickEvent evt)
    {
        if (gameSettingsMenu != null) gameSettingsMenu.style.display = UIE.DisplayStyle.None;
        if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.Flex;
    }

    private void OpenControlMenu(UIE.ClickEvent evt)
    {
        if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.None;
        if (controlMenu != null) controlMenu.style.display = UIE.DisplayStyle.Flex;
        else Debug.Log("ControlMenu не найден в UXML.");
    }

    private void OnAllButtonsClick(UIE.ClickEvent evt) { if (sfxSource != null) sfxSource.Play(); }

    // New: обработчик кнопки включения/выключения счётчика FPS (вызывается из UI)
    private void OnCounterFpsButtonClick(UIE.ClickEvent evt)
    {
        ToggleFpsCounter();
    }

    // toggle реализован отдельно — можно вызывать из кода
    public void ToggleFpsCounter()
    {
        _fpsCounterEnabled = !_fpsCounterEnabled;
        PlayerPrefs.SetInt(PrefFpsCounter, _fpsCounterEnabled ? 1 : 0);
        PlayerPrefs.Save();
        ApplyFpsCounterState();
    }

    private void ApplyFpsCounterState()
    {
        if (_fpsGameLabel != null)
            _fpsGameLabel.style.display = _fpsCounterEnabled ? UIE.DisplayStyle.Flex : UIE.DisplayStyle.None;

        if (_counterFpsButton != null)
            _counterFpsButton.text = _fpsCounterEnabled ? "Вкл" : "Выкл";

        if (_persistentFpsGO != null)
            _persistentFpsGO.SetActive(_fpsCounterEnabled);
    }

    // --- Game settings handlers ---
    private void OnFullscreenToggled(UIE.ChangeEvent<bool> evt) { Screen.fullScreen = evt.newValue; }

    private void OnVSyncToggled(UIE.ChangeEvent<bool> evt)
    {
        _vSyncEnabled = evt.newValue;
        QualitySettings.vSyncCount = _vSyncEnabled ? 1 : 0;
        PlayerPrefs.SetInt(PrefVSync, _vSyncEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void OnResolutionChanged(UIE.ChangeEvent<string> evt)
    {
        if (string.IsNullOrEmpty(evt.newValue)) return;
        var parts = evt.newValue.Split('x');
        if (parts.Length >= 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
            Screen.SetResolution(w, h, Screen.fullScreenMode);
        else Debug.LogWarning($"Не удалось распарсить разрешение: {evt.newValue}");
    }

    private void OnMasterVolumeChanged(UIE.ChangeEvent<float> evt)
    {
        float norm = SliderToNormalized(_masterVolumeSlider, evt.newValue);
        _masterVolume = norm;
        AudioListener.volume = _masterVolume;
        PlayerPrefs.SetFloat(PrefMaster, _masterVolume);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    private void OnMusicVolumeChanged(UIE.ChangeEvent<float> evt)
    {
        float norm = SliderToNormalized(_musicVolumeSlider, evt.newValue);
        _musicVolume = norm;
        PlayerPrefs.SetFloat(PrefMusic, _musicVolume);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    // FPS handlers for standard Slider (float)
    private void OnFpsSliderChanged(UIE.ChangeEvent<float> evt)
    {
        if (_fpsSlider == null || _fpsText == null) return;
        int fpsValue = Mathf.RoundToInt(evt.newValue);
        int highVal = Mathf.RoundToInt(_fpsSlider.highValue);
        if (fpsValue >= highVal)
        {
            // Unlimited — восстанавливаем VSync по настройке пользователя
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = _vSyncEnabled ? 1 : 0;
            _fpsText.text = "Unlimited";
            PlayerPrefs.SetInt(PrefFps, -1);
            PlayerPrefs.Save();
            Debug.Log("FPS: Unlimited selected; restored vSync = " + QualitySettings.vSyncCount);

            // измерим фактический FPS после смены
            StartFpsMeasure(-1);
        }
        else
        {
            fpsValue = Mathf.Clamp(fpsValue, 15, 300);
            // При ручном лимите явно отключаем VSync, иначе ограничение может не соблюдаться
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fpsValue;
            _fpsText.text = fpsValue.ToString();
            PlayerPrefs.SetInt(PrefFps, fpsValue);
            PlayerPrefs.Save();
            Debug.Log($"FPS limit set to {fpsValue}; vSync forced off to apply targetFrameRate.");

            // измерим фактический FPS после смены
            StartFpsMeasure(fpsValue);
        }
    }

    // FPS handler for SliderInt (int) via reflection callback
    private void OnFpsSliderIntChanged(UIE.ChangeEvent<int> evt)
    {
        if (_fpsText == null) return;
        int fpsValue = evt.newValue;
        int high = 301;
        var hv = TryGetSliderIntHighValue(_fpsSliderElement);
        if (hv.HasValue) high = hv.Value;

        if (fpsValue >= high)
        {
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = _vSyncEnabled ? 1 : 0;
            _fpsText.text = "Unlimited";
            PlayerPrefs.SetInt(PrefFps, -1);
            PlayerPrefs.Save();
            Debug.Log("FPS (int): Unlimited selected; restored vSync = " + QualitySettings.vSyncCount);

            StartFpsMeasure(-1);
        }
        else
        {
            fpsValue = Mathf.Clamp(fpsValue, 15, 300);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = fpsValue;
            _fpsText.text = fpsValue.ToString();
            PlayerPrefs.SetInt(PrefFps, fpsValue);
            PlayerPrefs.Save();
            Debug.Log($"FPS (int) limit set to {fpsValue}; vSync forced off to apply targetFrameRate.");

            StartFpsMeasure(fpsValue);
        }
    }

    private void StartFpsMeasure(int expected)
    {
        if (_fpsMeasureCoroutine != null) StopCoroutine(_fpsMeasureCoroutine);
        _fpsMeasureCoroutine = StartCoroutine(MeasureAndLogFps(expected));
    }

    private IEnumerator MeasureAndLogFps(int expected)
    {
        // короткий прогрев — даём системе пару кадров, затем измеряем 1.2 секунды
        yield return null;
        yield return null;

        int frames = 0;
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < 1.2f)
        {
            frames++;
            yield return null;
        }
        float elapsed = Time.unscaledTime - start;
        float avg = elapsed > 0f ? frames / elapsed : 0f;

        Debug.Log($"FPS test: expected={(expected <= 0 ? "Unlimited" : expected.ToString())}, measured={avg:F1} fps, Application.targetFrameRate={Application.targetFrameRate}, vSync={QualitySettings.vSyncCount}, resolution={Screen.width}x{Screen.height}, platform={Application.platform}");
#if UNITY_EDITOR
        int ed = GetEditorTargetFrameRate();
        Debug.Log($"EditorApplication.targetFrameRate (editor via reflection) = {ed}");
#endif

        _fpsMeasureCoroutine = null;
    }

    private void ApplyFpsFromTextField()
    {
        // не используется — FPSText теперь только отображает значение от ползунка
    }

    private void ApplyStoredFps(int storedFps)
    {
        if (storedFps <= 0)
        {
            Application.targetFrameRate = -1;
            if (_fpsText != null) _fpsText.text = "Unlimited";
            // restore vSync according to setting
            QualitySettings.vSyncCount = _vSyncEnabled ? 1 : 0;
#if UNITY_EDITOR
            SetEditorTargetFrameRate(-1);
#endif
            StartFpsMeasure(-1);
        }
        else
        {
            Application.targetFrameRate = Mathf.Clamp(storedFps, 15, 300);
            if (_fpsText != null) _fpsText.text = Application.targetFrameRate.ToString();
            // when stored fps is limited, vSync must be off
            QualitySettings.vSyncCount = 0;
#if UNITY_EDITOR
            SetEditorTargetFrameRate(Mathf.Clamp(storedFps, 15, 300));
#endif
            StartFpsMeasure(storedFps);
        }
    }

    private void ApplyMusicVolume()
    {
        if (musicSource != null) musicSource.volume = Mathf.Clamp01(_musicVolume * _masterVolume);
    }

    private float SliderToNormalized(UIE.Slider slider, float value)
    {
        if (slider == null) return Mathf.Clamp01(value);
        float low = slider.lowValue;
        float high = slider.highValue;
        return (high - low) > 0f ? Mathf.InverseLerp(low, high, value) : Mathf.Clamp01(value);
    }

    private float NormalizedToSliderValue(UIE.Slider slider, float normalized)
    {
        if (slider == null) return Mathf.Clamp01(normalized);
        return slider.lowValue + Mathf.Clamp01(normalized) * (slider.highValue - slider.lowValue);
    }

    // ---------------- Reflection helpers для SliderInt ----------------
    private bool TrySetupSliderInt(UIE.VisualElement elem, int storedFps)
    {
        if (elem == null) return false;
        var t = elem.GetType();

        TrySetIntProperty(t, elem, new[] { "lowValue", "low", "min", "minValue" }, 15);
        TrySetIntProperty(t, elem, new[] { "highValue", "high", "max", "maxValue" }, 301);

        if (storedFps <= 0)
            TryInvokeSetValueWithoutNotifyInt(elem, 301);
        else
            TryInvokeSetValueWithoutNotifyInt(elem, Mathf.Clamp(storedFps, 15, 300));

        try
        {
            elem.RegisterCallback<UIE.ChangeEvent<int>>(OnFpsSliderIntChanged);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TrySetIntProperty(System.Type t, object target, string[] names, int value)
    {
        foreach (var name in names)
        {
            var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi != null && pi.CanWrite && pi.PropertyType == typeof(int))
            {
                pi.SetValue(target, value);
                return;
            }

            var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (fi != null && fi.FieldType == typeof(int))
            {
                fi.SetValue(target, value);
                return;
            }
        }
    }

    private int? TryGetSliderIntHighValue(UIE.VisualElement elem)
    {
        if (elem == null) return null;
        var t = elem.GetType();
        var names = new[] { "highValue", "high", "max", "maxValue" };
        foreach (var name in names)
        {
            var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (pi != null && pi.PropertyType == typeof(int))
                return (int)pi.GetValue(elem);
            var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (fi != null && fi.FieldType == typeof(int))
                return (int)fi.GetValue(elem);
        }
        return null;
    }

    private bool TryInvokeSetValueWithoutNotifyInt(object elem, int value)
    {
        if (elem == null) return false;
        var t = elem.GetType();
        var mi = t.GetMethod("SetValueWithoutNotify", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(int) }, null);
        if (mi != null)
        {
            mi.Invoke(elem, new object[] { value });
            return true;
        }
        var prop = t.GetProperty("value", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(int))
        {
            prop.SetValue(elem, value);
            return true;
        }
        var field = t.GetField("m_Value", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null && field.FieldType == typeof(int))
        {
            field.SetValue(elem, value);
            return true;
        }
        return false;
    }

    // New: обновление счётчика FPS (accuracy improved) + обработка Esc для восстановления меню
    private void Update()
    {
        // Esc: если меню подавлено (после нажатия Start), то Esc отменяет подавление и открывает меню.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_menuSuppressed)
            {
                _menuSuppressed = false;
                try { OpenMainMenu(); } catch { }
            }
            else
            {
                // Если меню не подавлено — переключаем видимость локального mainMenu (если есть)
                if (mainMenu != null)
                {
                    var isVisible = mainMenu.style.display == UIE.DisplayStyle.Flex;
                    mainMenu.style.display = isVisible ? UIE.DisplayStyle.None : UIE.DisplayStyle.Flex;
                }
            }
        }

        if (!_fpsCounterEnabled) return;

        _fpsFrameCount++;

        float now = Time.unscaledTime;
        float elapsed = now - _fpsLastSampleTime;
        if (elapsed >= _fpsUpdateInterval)
        {
            // вычисляем FPS как количество кадров / прошедшее реальное время
            float fps = (_fpsFrameCount > 0f && elapsed > 0f) ? (_fpsFrameCount / elapsed) : 0f;
            int fpsInt = Mathf.RoundToInt(fps);
            string text = $"FPS {fpsInt}";

            // Обновляем метку в сцене (если есть)
            if (_fpsGameLabel != null)
                _fpsGameLabel.text = text;

            // Обновляем persistent overlay (если есть)
            if (_persistentFpsText != null)
                _persistentFpsText.text = text;

            // сброс для следующего интервала
            _fpsLastSampleTime = now;
            _fpsFrameCount = 0;
        }
    }

    public void OpenMainMenu()
    {
        // Если меню подавлено (после нажатия Start), не показывать через OpenMainMenu
        if (_menuSuppressed) return;

        try
        {
            if (mainMenu != null)
                mainMenu.style.display = UIE.DisplayStyle.Flex;

            if (settingsMenu != null) settingsMenu.style.display = UIE.DisplayStyle.None;
            if (gameSettingsMenu != null) gameSettingsMenu.style.display = UIE.DisplayStyle.None;
            if (controlMenu != null) controlMenu.style.display = UIE.DisplayStyle.None;

            if (_startButton != null)
            {
                try { _startButton.Focus(); } catch { }
            }

            // Use newer API when available
#if UNITY_2023_2_OR_NEWER
            var es = UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
#else
            var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
#endif
            if (es != null && !es.gameObject.activeInHierarchy) es.gameObject.SetActive(true);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("OpenMainMenu failed: " + ex.Message);
        }
    }

#if UNITY_EDITOR
    // Безопасная установка EditorApplication.targetFrameRate (если свойство/поле доступно в текущей версии Unity)
    private void SetEditorTargetFrameRate(int value)
    {
        try
        {
            var t = typeof(EditorApplication);
            var pi = t.GetProperty("targetFrameRate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null)
            {
                pi.SetValue(null, value, null);
                return;
            }

            var fi = t.GetField("targetFrameRate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null)
            {
                fi.SetValue(null, value);
            }
        }
        catch
        {
            // безопасно игнорируем — функциональность редактора не критична для билда
        }
    }

    private int GetEditorTargetFrameRate()
    {
        try
        {
            var t = typeof(EditorApplication);
            var pi = t.GetProperty("targetFrameRate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (pi != null)
            {
                var v = pi.GetValue(null, null);
                if (v is int) return (int)v;
            }
            var fi = t.GetField("targetFrameRate", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (fi != null)
            {
                var v = fi.GetValue(null);
                if (v is int) return (int)v;
            }
        }
        catch { }
        return int.MinValue;
    }
#endif

    // --- Новые вспомогательные методы для гарантии отображения UI Toolkit ---
    private void EnsureUIDocumentEventCamera()
    {
        if (_document == null) return;
        var panelSettings = _document.panelSettings;
        if (panelSettings == null) return;

        var t = panelSettings.GetType();

        // Попытка найти свойство Camera (разные версии Unity используют разные имена)
        var camProp = t.GetProperty("eventCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                      ?? t.GetProperty("targetCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                      ?? t.GetProperty("camera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (camProp != null && camProp.PropertyType == typeof(Camera))
        {
            var cur = camProp.GetValue(panelSettings) as Camera;
            if (cur == null)
            {
                var cam = FindOrCreateUICamera();
                try { camProp.SetValue(panelSettings, cam); Debug.Log($"Assigned camera to PanelSettings property '{camProp.Name}'."); } catch (Exception ex) { Debug.LogWarning("EnsureUIDocumentEventCamera: failed to set camera property: " + ex.Message); }
            }
            return;
        }

        // Если нет свойства — пробуем поля (private/public)
        var camField = t.GetField("eventCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                       ?? t.GetField("targetCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                       ?? t.GetField("camera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);

        if (camField != null && camField.FieldType == typeof(Camera))
        {
            var cur = camField.GetValue(panelSettings) as Camera;
            if (cur == null)
            {
                var cam = FindOrCreateUICamera();
                try { camField.SetValue(panelSettings, cam); Debug.Log($"Assigned camera to PanelSettings field '{camField.Name}'."); } catch (Exception ex) { Debug.LogWarning("EnsureUIDocumentEventCamera: failed to set camera field: " + ex.Message); }
            }
        }
    }

    private Camera FindOrCreateUICamera()
    {
        // Попытки найти подходящую камеру: main, current, любой существующий
        Camera cam = Camera.main;
        if (cam == null) cam = Camera.current;
        if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();

        if (cam != null)
        {
            // если камера найдена, повысим глубину, чтобы UI рисовался поверх, и убедимся, что она активна
            try
            {
                cam.depth = Mathf.Max(cam.depth, 10f);
                cam.gameObject.SetActive(true);
            }
            catch { }
            return cam;
        }

        // Нельзя найти камеру — создаём новую минимальную камеру для UI
        var go = new GameObject("UICamera_Runtime");
        DontDestroyOnLoad(go);
        cam = go.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Depth;
        cam.cullingMask = -1; // рендерим всё (безопасно для UI Toolkit)
        cam.depth = 100f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        // Не мешаем основной логике сцены
        go.hideFlags = HideFlags.DontSave;
        Debug.Log("Создана UICamera_Runtime для рендеринга UI Toolkit (кнопки/меню).");
        return cam;
    }

    // Диагностика состояния UIDocument / rootVisualElement
    private void LogUIDocumentState()
    {
        try
        {
            if (_document == null)
            {
                Debug.LogWarning("LogUIDocumentState: UIDocument == null");
                return;
            }

            Debug.Log($"LogUIDocumentState: UIDocument found on '{gameObject.name}'.");

            var panelSettings = _document.panelSettings;
            Debug.Log($"LogUIDocumentState: panelSettings = {(panelSettings == null ? "null" : panelSettings.GetType().Name)}");

            if (panelSettings != null)
            {
                var t = panelSettings.GetType();
                var camProp = t.GetProperty("eventCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                              ?? t.GetProperty("targetCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                              ?? t.GetProperty("camera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (camProp != null)
                {
                    var cam = camProp.GetValue(panelSettings) as Camera;
                    Debug.Log($"LogUIDocumentState: PanelSettings.{camProp.Name} = {(cam == null ? "null" : cam.name + " (depth=" + cam.depth + ")")}");
                }
                else
                {
                    var camField = t.GetField("eventCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                                   ?? t.GetField("targetCamera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
                                   ?? t.GetField("camera", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
                    if (camField != null)
                    {
                        var cam = camField.GetValue(panelSettings) as Camera;
                        Debug.Log($"LogUIDocumentState: PanelSettings.{camField.Name} = {(cam == null ? "null" : cam.name + " (depth=" + cam.depth + ")")}");
                    }
                    else Debug.Log("LogUIDocumentState: не найдено поле/свойство камеры в PanelSettings (возможно версия Unity использует другой механизм).");
                }
            }

            var root = _document.rootVisualElement;
            if (root == null)
            {
                Debug.LogWarning("LogUIDocumentState: rootVisualElement == null");
                return;
            }

            Debug.Log($"LogUIDocumentState: rootVisualElement childCount = {root.hierarchy.childCount}");
            var children = root.Children().ToList();
            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                string name = string.IsNullOrEmpty(child.name) ? "(no name)" : child.name;
                Debug.Log($"LogUIDocumentState: root child[{i}] = '{name}', type={child.GetType().Name}, display={child.style.display}");
            }

            if (mainMenu == null)
            {
                Debug.LogWarning("LogUIDocumentState: mainMenu элемент не найден (Q('MainMenu') вернул null).");
            }
            else
            {
                Debug.Log($"LogUIDocumentState: mainMenu found; display={mainMenu.style.display}; visible in hierarchy? (no direct API)");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("LogUIDocumentState failed: " + ex.Message);
        }
    }
}