using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;

public class MenuinGame : MonoBehaviour
{
    private const string ButtonName = "Buttonlocation";
    private const string TextAvailableName = "Textavailablejob";
    private const string TextSearchingName = "Textsearching";
    private const string MenuVisualElementObjectName = "MenuinGame";

    [Header("Braking")]
    public float brakeDuration = 0.5f;           // время плавного набора тормоза (сек)
    public float maxBrakeTorque = 2000f;
    [Range(0f, 1f)] public float frontBrakeFactor = 0.6f;
    public float rpmLockThreshold = 5f;
    [Range(0f, 1f)] public float minBrakeFactorAtLowRpm = 0.5f;
    [Range(0f, 1f)] public float maxBrakeFraction = 1f;
    [Tooltip("Во сколько раз агрессивнее применять тормоз по сравнению с базовой величиной")]
    public float brakeAggression = 5f;

    [Tooltip("Дополнительный линейный демпфинг, добавляемый к Rigidbody при открытом меню")]
    public float additionalLinearDamping = 2f;
    [Tooltip("Дополнительный angular drag, добавляемый к Rigidbody при открытом меню")]
    public float additionalAngularDrag = 1f;

    // Корни/объекты, в инспекторе укажите корень машины (или массив машин).
    // Если пусто — автопоиск по WheelCollider'ам.
    public GameObject[] controlsToDisable;

    // Статический флаг: блокировать обработку пробела (ручник) для других скриптов
    public static bool BlockSpaceInput = false;

    // UI (поиск/кнопка)
    private UnityEngine.UI.Button locationButton;
    private UnityEngine.UIElements.Button toolkitButton;
    private GameObject textAvailableGO;
    private Text textAvailable;
    private GameObject textSearchingGO;
    private Text textSearching;
    private VisualElement textAvailableVE;
    private Label textSearchingVE;
    private Coroutine searchingCoroutine;
    private bool textsShown;

    // UI Document
    private UIDocument menuUIDocument;
    private VisualElement menuRoot;
    private GameObject menuGameObject;
    private bool menuVisible;

    // Физика колес
    private List<WheelCollider> controlledWheels = new List<WheelCollider>();
    private HashSet<WheelCollider> frontWheels = new HashSet<WheelCollider>();
    private List<Rigidbody> controlledRigidbodies = new List<Rigidbody>();
    private Dictionary<Rigidbody, float> originalLinearDrags = new Dictionary<Rigidbody, float>();
    private Dictionary<Rigidbody, float> originalAngularDrags = new Dictionary<Rigidbody, float>();

    // Сохранение оригинальных constraints для восстановления
    private Dictionary<Rigidbody, RigidbodyConstraints> originalConstraints = new Dictionary<Rigidbody, RigidbodyConstraints>();

    private Coroutine toggleCoroutine;

    // сохраняем выключенные компоненты машины, чтобы восстановить их позже
    private Dictionary<MonoBehaviour, bool> vehicleControlsPrev = new Dictionary<MonoBehaviour, bool>();

    // сохраняем отключённые UIBehaviour вне меню, чтобы восстановить позже
    private Dictionary<UIBehaviour, bool> uiPrevEnabled = new Dictionary<UIBehaviour, bool>();

    // Взаимодействие с конкретным скриптом RearWheelDrive
    private List<MonoBehaviour> rearWheelDriveInstances = new List<MonoBehaviour>(); // хранит компоненты RearWheelDrive
    private Dictionary<MonoBehaviour, float> origMaxAngle = new Dictionary<MonoBehaviour, float>();
    private Dictionary<MonoBehaviour, float> origMaxTorque = new Dictionary<MonoBehaviour, float>();
    private Dictionary<MonoBehaviour, float> origHandbrakeTorque = new Dictionary<MonoBehaviour, float>();
    private Dictionary<MonoBehaviour, bool> origHandbrakeOn = new Dictionary<MonoBehaviour, bool>();

    // время открытия меню (для плавного нарастания тормоза)
    private float menuOpenedAt = 0f;

    void Start()
    {
        BlockSpaceInput = false;

        // Поиск UI-меню (UIDocument) и элементов
        var menuGO = GameObject.Find(MenuVisualElementObjectName);
        if (menuGO != null)
        {
            menuGameObject = menuGO;
            menuUIDocument = menuGO.GetComponent<UIDocument>();
            if (menuUIDocument != null)
            {
                menuRoot = menuUIDocument.rootVisualElement;
                if (menuRoot != null)
                    menuRoot.style.display = DisplayStyle.None;
                menuVisible = false;

                // UIElements кнопка
                toolkitButton = menuRoot.Q<UnityEngine.UIElements.Button>(ButtonName) ?? FindButtonInTree(menuRoot, ButtonName);
                if (toolkitButton != null)
                    toolkitButton.clicked += OnLocationButtonClicked;

                textAvailableVE = menuRoot.Q<VisualElement>(TextAvailableName);
                textSearchingVE = menuRoot.Q<Label>(TextSearchingName);
                if (textAvailableVE != null) textAvailableVE.style.display = DisplayStyle.None;
                if (textSearchingVE != null) textSearchingVE.style.display = DisplayStyle.None;
            }
        }

        // Старый UI Button (UnityEngine.UI)
        var btnGO = GameObject.Find(ButtonName);
        if (btnGO != null)
        {
            var uiBtn = btnGO.GetComponent<UnityEngine.UI.Button>();
            if (uiBtn != null && toolkitButton == null)
            {
                locationButton = uiBtn;
                locationButton.onClick.AddListener(OnLocationButtonClicked);
            }
        }

        textAvailableGO = GameObject.Find(TextAvailableName);
        if (textAvailableGO != null)
        {
            textAvailable = textAvailableGO.GetComponent<Text>();
            textAvailableGO.SetActive(false);
        }

        textSearchingGO = GameObject.Find(TextSearchingName);
        if (textSearchingGO != null)
        {
            textSearching = textSearchingGO.GetComponent<Text>();
            textSearchingGO.SetActive(false);
        }

        textsShown = false;

        // скрываем курсор по умолчанию
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Tab — открыть/закрыть меню
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (toggleCoroutine != null) StopCoroutine(toggleCoroutine);
            toggleCoroutine = StartCoroutine(ToggleMenuRoutine());
        }

        // ESC: если меню открыто — закрываем его
        if (menuVisible && Input.GetKeyDown(KeyCode.Escape))
        {
            if (toggleCoroutine != null) StopCoroutine(toggleCoroutine);
            toggleCoroutine = StartCoroutine(CloseMenuRoutine());
        }

        // Когда меню открыто — подавляем ручник и блокируем управление ручником каждую Update-кадру
        if (menuVisible)
        {
            // потребляем Space (лишний) — ничего не делаем
            if (Input.GetKeyDown(KeyCode.Space)) { }

            // Жёстко запрещаем ручник в каждом Update, чтобы не дать RearWheelDrive его включить
            ForceDisableRearWheelHandbrakeAndOverride();
        }
    }

    void LateUpdate()
    {
        // Дополнительная защита: после всех Update-ов принудительно сбросим ручник и его эффекты.
        if (!menuVisible) return;

        // Обнуляем флаг и параметры ручника в RearWheelDrive после того как его Update мог сработать.
        foreach (var c in rearWheelDriveInstances)
        {
            if (c == null) continue;
            var t = c.GetType();

            var hbField = t.GetField("handbrakeOn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hbField != null)
            {
                try { hbField.SetValue(c, false); } catch { }
            }

            var handbrakeTorqueF = t.GetField("handbrakeTorque");
            if (handbrakeTorqueF != null)
            {
                try { handbrakeTorqueF.SetValue(c, 0f); } catch { }
            }

            var maxTorqueF = t.GetField("maxTorque");
            if (maxTorqueF != null)
            {
                try { maxTorqueF.SetValue(c, 0f); } catch { }
            }
        }

        // Перезапишем brakeTorque и drag, чтобы гарантировать поведение меню
        float elapsed = Mathf.Max(0f, Time.unscaledTime - menuOpenedAt);
        float raw = Mathf.Clamp01(elapsed / brakeDuration);
        float f = 1f - Mathf.Pow(1f - raw, 3.2f);

        foreach (var w in controlledWheels)
        {
            if (w == null) continue;
            try { w.motorTorque = 0f; } catch { }

            float baseMax = frontWheels.Contains(w) ? maxBrakeTorque * frontBrakeFactor : maxBrakeTorque;
            float targetMax = baseMax * Mathf.Clamp01(maxBrakeFraction) * brakeAggression;
            float rpmAbs = Mathf.Abs(w.rpm);
            float brakeFactor = rpmAbs < rpmLockThreshold ? minBrakeFactorAtLowRpm : 1f;
            float desired = targetMax * brakeFactor * f;

            // Жёстко ставим ожидаемый тормоз (перезаписываем возможный ручник)
            try { w.brakeTorque = desired; } catch { }
        }

        foreach (var rb in controlledRigidbodies)
        {
            if (rb == null) continue;
            float origLinear = originalLinearDrags.ContainsKey(rb) ? originalLinearDrags[rb] : rb.linearDamping;
            float origAngular = originalAngularDrags.ContainsKey(rb) ? originalAngularDrags[rb] : rb.angularDamping;
            try
            {
                rb.linearDamping = Mathf.Lerp(origLinear, origLinear + additionalLinearDamping, f);
                rb.angularDamping = Mathf.Lerp(origAngular, origAngular + additionalAngularDrag, f);
            }
            catch { }
        }
    }

    // Физика: обеспечиваем торможение и повышение drag в FixedUpdate для корректной работы с физикой.
    private void FixedUpdate()
    {
        if (!menuVisible) return;

        // время от открытия
        float elapsed = Mathf.Max(0f, Time.unscaledTime - menuOpenedAt);
        float raw = Mathf.Clamp01(elapsed / brakeDuration);
        float f = 1f - Mathf.Pow(1f - raw, 3.2f);

        // принудительно отключаем моторный вклад и плавно увеличиваем brakeTorque
        foreach (var w in controlledWheels)
        {
            if (w == null) continue;
            try { w.motorTorque = 0f; } catch { }

            float baseMax = frontWheels.Contains(w) ? maxBrakeTorque * frontBrakeFactor : maxBrakeTorque;
            float targetMax = baseMax * Mathf.Clamp01(maxBrakeFraction) * brakeAggression;
            float rpmAbs = Mathf.Abs(w.rpm);
            float brakeFactor = rpmAbs < rpmLockThreshold ? minBrakeFactorAtLowRpm : 1f;
            float desired = targetMax * brakeFactor * f;

            // Плавное приближение: используем Lerp с небольшой скорости
            float current = w.brakeTorque;
            float newBrake = Mathf.Lerp(current, desired, Mathf.Clamp01(Time.fixedDeltaTime * 10f));
            w.brakeTorque = newBrake;
        }

        // увеличиваем damping у rigidbody, чтобы машина реально замедлялась
        foreach (var rb in controlledRigidbodies)
        {
            if (rb == null) continue;
            float origLinear = originalLinearDrags.ContainsKey(rb) ? originalLinearDrags[rb] : rb.linearDamping;
            float origAngular = originalAngularDrags.ContainsKey(rb) ? originalAngularDrags[rb] : rb.angularDamping;

            rb.linearDamping = Mathf.Lerp(origLinear, origLinear + additionalLinearDamping, f);
            rb.angularDamping = Mathf.Lerp(origAngular, origAngular + additionalAngularDrag, f);
        }

        // На физическом шаге дополнительно принудительно сбрасываем ручник в RearWheelDrive (защита)
        ForceDisableRearWheelHandbrakeAndOverride();
    }

    private IEnumerator ToggleMenuRoutine()
    {
        menuVisible = !menuVisible;

        // блокируем/разблокируем пробел для других скриптов
        BlockSpaceInput = menuVisible;

        if (menuRoot != null) menuRoot.style.display = menuVisible ? DisplayStyle.Flex : DisplayStyle.None;

        UnityEngine.Cursor.visible = menuVisible;
        UnityEngine.Cursor.lockState = menuVisible ? CursorLockMode.None : CursorLockMode.Locked;

        if (menuVisible)
        {
            CollectControlledPhysics();
            CollectRearWheelDriveInstances();
            ApplyRearWheelDriveLock();
            DisableVehicleControlsExceptRearWheelDrive();
            DisableUIExceptMenu();
            menuOpenedAt = Time.unscaledTime;
        }
        else
        {
            RestorePhysicsImmediate();
            RestoreVehicleControls();
            RestoreRearWheelDrive();
            RestoreUI();
        }

        toggleCoroutine = null;
        yield break;
    }

    private IEnumerator CloseMenuRoutine()
    {
        menuVisible = false;

        // снять блокировку пробела
        BlockSpaceInput = false;

        if (menuRoot != null) menuRoot.style.display = DisplayStyle.None;
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;

        RestorePhysicsImmediate();
        RestoreVehicleControls();
        RestoreRearWheelDrive();
        RestoreUI();

        toggleCoroutine = null;
        yield break;
    }

    private void CollectControlledPhysics()
    {
        controlledWheels.Clear();
        frontWheels.Clear();
        controlledRigidbodies.Clear();
        originalLinearDrags.Clear();
        originalAngularDrags.Clear();
        originalConstraints.Clear();

        HashSet<WheelCollider> addedWheels = new HashSet<WheelCollider>();
        HashSet<Rigidbody> addedRbs = new HashSet<Rigidbody>();

        if (controlsToDisable != null && controlsToDisable.Length > 0)
        {
            foreach (var go in controlsToDisable)
            {
                if (go == null) continue;
                var wheels = go.GetComponentsInChildren<WheelCollider>(true);
                foreach (var w in wheels)
                {
                    if (w == null) continue;
                    if (addedWheels.Add(w)) controlledWheels.Add(w);
                    float localZ = w.transform.localPosition.z;
                    if (localZ > 0.01f) frontWheels.Add(w);
                }

                var rb = go.GetComponent<Rigidbody>() ?? go.GetComponentInParent<Rigidbody>();
                if (rb != null && addedRbs.Add(rb))
                {
                    controlledRigidbodies.Add(rb);
#if UNITY_2020_1_OR_NEWER
                    originalLinearDrags[rb] = rb.linearDamping;
#else
                    originalLinearDrags[rb] = rb.drag;
#endif
                    originalAngularDrags[rb] = rb.angularDamping;
                    originalConstraints[rb] = rb.constraints;
                }
            }
        }

        if (controlledWheels.Count == 0)
        {
            // Универсальный вызов, совместимый с разными версиями Unity:
            var allWheels = FindObjectsOfType<WheelCollider>(true);

            foreach (var w in allWheels)
            {
                if (w == null) continue;
                if (addedWheels.Add(w)) controlledWheels.Add(w);
                var rb = w.GetComponentInParent<Rigidbody>();
                if (rb != null && addedRbs.Add(rb))
                {
                    controlledRigidbodies.Add(rb);
#if UNITY_2020_1_OR_NEWER
                    originalLinearDrags[rb] = rb.linearDamping;
#else
                    originalLinearDrags[rb] = rb.drag;
#endif
                    originalAngularDrags[rb] = rb.angularDamping;
                    originalConstraints[rb] = rb.constraints;
                }

                float localZ = w.transform.localPosition.z;
                if (localZ > 0.01f) frontWheels.Add(w);
            }
        }
    }

    private void RestorePhysicsImmediate()
    {
        // снимаем тормоза с колёс (сбрасываем)
        foreach (var w in controlledWheels)
            if (w != null) w.brakeTorque = 0f;

        // восстанавливаем drags
        foreach (var rb in controlledRigidbodies)
            if (rb != null)
            {
                if (originalLinearDrags.ContainsKey(rb))
                {
#if UNITY_2020_1_OR_NEWER
                    rb.linearDamping = originalLinearDrags[rb];
#else
                    rb.drag = originalLinearDrags[rb];
#endif
                }
                if (originalAngularDrags.ContainsKey(rb))
                {
                    rb.angularDamping = originalAngularDrags[rb];
                }
            }

        // восстановить сохранённые constraints
        foreach (var kv in originalConstraints)
        {
            var rb = kv.Key;
            if (rb == null) continue;
            try { rb.constraints = kv.Value; } catch { }
        }
        originalConstraints.Clear();

        controlledWheels.Clear();
        frontWheels.Clear();
        controlledRigidbodies.Clear();
        originalLinearDrags.Clear();
        originalAngularDrags.Clear();
    }

    // --- RearWheelDrive specific handling ---
    private void CollectRearWheelDriveInstances()
    {
        rearWheelDriveInstances.Clear();
        origMaxAngle.Clear();
        origMaxTorque.Clear();
        origHandbrakeTorque.Clear();
        origHandbrakeOn.Clear();

        List<GameObject> roots = new List<GameObject>();
        if (controlsToDisable != null && controlsToDisable.Length > 0)
        {
            foreach (var go in controlsToDisable) if (go != null) roots.Add(go);
        }
        else
        {
            foreach (var rb in controlledRigidbodies)
                if (rb != null && !roots.Contains(rb.gameObject)) roots.Add(rb.gameObject);
        }

        foreach (var root in roots)
        {
            if (root == null) continue;
            var comps = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var c in comps)
            {
                if (c == null) continue;
                if (c.GetType().Name == "RearWheelDrive")
                {
                    if (!rearWheelDriveInstances.Contains(c))
                    {
                        rearWheelDriveInstances.Add(c);

                        // сохранить публичные поля maxAngle/maxTorque/handbrakeTorque
                        var t = c.GetType();
                        var maxAngleF = t.GetField("maxAngle");
                        var maxTorqueF = t.GetField("maxTorque");
                        var handbrakeTorqueF = t.GetField("handbrakeTorque");

                        if (maxAngleF != null) origMaxAngle[c] = (float)maxAngleF.GetValue(c);
                        if (maxTorqueF != null) origMaxTorque[c] = (float)maxTorqueF.GetValue(c);
                        if (handbrakeTorqueF != null) origHandbrakeTorque[c] = (float)handbrakeTorqueF.GetValue(c);

                        // приватный флаг handbrakeOn сохраняем через GetField на конкретном типе
                        var hbField = t.GetField("handbrakeOn", BindingFlags.Instance | BindingFlags.NonPublic);
                        if (hbField != null)
                        {
                            var value = hbField.GetValue(c);
                            if (value is bool b) origHandbrakeOn[c] = b;
                            else origHandbrakeOn[c] = false;
                        }
                        else origHandbrakeOn[c] = false;
                    }
                }
            }
        }
    }

    private void ApplyRearWheelDriveLock()
    {
        foreach (var c in rearWheelDriveInstances)
        {
            if (c == null) continue;
            var t = c.GetType();

            // обнуляем угол и крутящий момент — двигатель и поворот отключены
            var maxAngleF = t.GetField("maxAngle");
            var maxTorqueF = t.GetField("maxTorque");
            var handbrakeTorqueF = t.GetField("handbrakeTorque");

            try
            {
                if (maxAngleF != null) maxAngleF.SetValue(c, 0f);
                if (maxTorqueF != null) maxTorqueF.SetValue(c, 0f);
                if (handbrakeTorqueF != null) handbrakeTorqueF.SetValue(c, 0f);
            }
            catch { }

            // сбрасываем приватный флаг ручника (на всякий случай)
            var hbField = t.GetField("handbrakeOn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hbField != null)
            {
                try { hbField.SetValue(c, false); } catch { }
            }
        }
    }

    private void ForceDisableRearWheelHandbrakeAndOverride()
    {
        // вызывается каждый Update/FixedUpdate при menuVisible: дополнительно обнуляем приватный флаг handbrakeOn,
        // обнуляем maxTorque и handbrakeTorque, и восстанавливаем сцепление у задних колёс
        foreach (var c in rearWheelDriveInstances)
        {
            if (c == null) continue;
            var t = c.GetType();

            // приватный флаг handbrakeOn
            var hbField = t.GetField("handbrakeOn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hbField != null)
            {
                try { hbField.SetValue(c, false); } catch { }
            }

            // обнулить публичные параметры, чтобы скрипт не ставил ручник/не давал мотор
            var maxTorqueF = t.GetField("maxTorque");
            var handbrakeTorqueF = t.GetField("handbrakeTorque");
            if (maxTorqueF != null)
            {
                try { maxTorqueF.SetValue(c, 0f); } catch { }
            }
            if (handbrakeTorqueF != null)
            {
                try { handbrakeTorqueF.SetValue(c, 0f); } catch { }
            }

            // Восстановим сцепление задних колёс на значения, которые указаны в скрипте
            float forwardStiff = 1.5f;
            float sideStiff = 2f;
            var forwardField = t.GetField("forwardStiffness");
            var sideField = t.GetField("sidewaysStiffness");
            if (forwardField != null)
            {
                try { forwardStiff = (float)forwardField.GetValue(c); } catch { }
            }
            if (sideField != null)
            {
                try { sideStiff = (float)sideField.GetValue(c); } catch { }
            }

            // Apply to rear wheels (localPosition.z < 0)
            foreach (var w in controlledWheels)
            {
                if (w == null) continue;
                if (w.transform.localPosition.z < 0f)
                {
                    try
                    {
                        var f = w.forwardFriction;
                        var s = w.sidewaysFriction;
                        f.stiffness = forwardStiff;
                        s.stiffness = sideStiff;
                        w.forwardFriction = f;
                        w.sidewaysFriction = s;
                    }
                    catch { }
                }
            }
        }
    }

    private void RestoreRearWheelDrive()
    {
        foreach (var c in rearWheelDriveInstances)
        {
            if (c == null) continue;
            var t = c.GetType();

            var maxAngleF = t.GetField("maxAngle");
            var maxTorqueF = t.GetField("maxTorque");
            var handbrakeTorqueF = t.GetField("handbrakeTorque");

            try
            {
                if (maxAngleF != null && origMaxAngle.ContainsKey(c)) maxAngleF.SetValue(c, origMaxAngle[c]);
                if (maxTorqueF != null && origMaxTorque.ContainsKey(c)) maxTorqueF.SetValue(c, origMaxTorque[c]);
                if (handbrakeTorqueF != null && origHandbrakeTorque.ContainsKey(c)) handbrakeTorqueF.SetValue(c, origHandbrakeTorque[c]);
            }
            catch { }

            var hbField = t.GetField("handbrakeOn", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hbField != null && origHandbrakeOn.ContainsKey(c))
            {
                try { hbField.SetValue(c, origHandbrakeOn[c]); } catch { }
            }
        }

        rearWheelDriveInstances.Clear();
        origMaxAngle.Clear();
        origMaxTorque.Clear();
        origHandbrakeTorque.Clear();
        origHandbrakeOn.Clear();
    }

    // --- Отключаем всё, кроме RearWheelDrive (его мы не выключаем — только частично модифицируем) ---
    private void DisableVehicleControlsExceptRearWheelDrive()
    {
        vehicleControlsPrev.Clear();

        List<GameObject> roots = new List<GameObject>();
        if (controlsToDisable != null && controlsToDisable.Length > 0)
        {
            foreach (var go in controlsToDisable) if (go != null) roots.Add(go);
        }
        else
        {
            foreach (var rb in controlledRigidbodies)
            {
                if (rb == null) continue;
                var root = rb.gameObject;
                if (root != null && !roots.Contains(root)) roots.Add(root);
            }
        }

        foreach (var root in roots)
        {
            var mbs = root.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in mbs)
            {
                if (mb == null) continue;
                if (mb == this) continue;

                var t = mb.GetType();
                string typeName = t.Name.ToLowerInvariant();

                // Не отключаем RearWheelDrive — мы модифицируем его отдельно
                if (typeName == "rearwheeldrive") continue;

                // Пропускаем UI-скрипты и камеры/фолловеры — их держим включёнными
                if (typeof(UIBehaviour).IsAssignableFrom(t)) continue;
                if (typeName.Contains("camera") || typeName.Contains("follow") || typeName.Contains("cinemachine") || typeName.Contains("cam")) continue;

                // Не трогаем рендеры/аудио — только логические контроллеры
                if (typeName.Contains("audio") || typeName.Contains("renderer")) continue;

                // Не отключаем явно компоненты колес
                if (typeName.Contains("wheel") || typeName.Contains("suspension") || typeName.Contains("wheelcollider")) continue;

                try
                {
                    vehicleControlsPrev[mb] = mb.enabled;
                    if (mb.enabled) mb.enabled = false;
                }
                catch { }
            }
        }
    }

    private void RestoreVehicleControls()
    {
        foreach (var kv in vehicleControlsPrev)
        {
            var mb = kv.Key;
            if (mb == null) continue;
            try { mb.enabled = kv.Value; } catch { }
        }
        vehicleControlsPrev.Clear();
    }

    // Блокируем все UIBehaviour'ы вне меню (чтобы кнопки/другие UI элементы были неактивны),
    // но не отключаем EventSystem и не трогаем UI элементы внутри menuGameObject.
    private void DisableUIExceptMenu()
    {
        uiPrevEnabled.Clear();

        var all = FindObjectsOfType<UIBehaviour>(true);
        foreach (var ub in all)
        {
            if (ub == null) continue;
            // оставляем EventSystem всегда включённым
            if (ub is EventSystem) continue;
            // не трогаем UI компоненты внутри меню UIDocument GameObject
            if (menuGameObject != null && (ub.gameObject == menuGameObject || ub.gameObject.transform.IsChildOf(menuGameObject.transform))) continue;

            try
            {
                uiPrevEnabled[ub] = ub.enabled;
                if (ub.enabled) ub.enabled = false;
            }
            catch { }
        }
    }

    private void RestoreUI()
    {
        foreach (var kv in uiPrevEnabled)
        {
            var ub = kv.Key;
            if (ub == null) continue;
            try { ub.enabled = kv.Value; } catch { }
        }
        uiPrevEnabled.Clear();
    }

    // UI: toggle поиска в меню
    private void OnLocationButtonClicked()
    {
        textsShown = !textsShown;
        SetTextsVisible(textsShown);

        if (textsShown)
        {
            if (searchingCoroutine == null) searchingCoroutine = StartCoroutine(SearchingAnimation());
        }
        else
        {
            StopSearchingCoroutineIfAny();
        }
    }

    private void SetTextsVisible(bool visible)
    {
        if (textAvailableGO != null) textAvailableGO.SetActive(visible);
        if (textSearchingGO != null) textSearchingGO.SetActive(visible);

        if (textAvailableVE != null) textAvailableVE.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (textSearchingVE != null) textSearchingVE.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private IEnumerator SearchingAnimation()
    {
        string[] variants = new string[] { "поиск.", "поиск..", "поиск..." };
        int idx = 0;
        while (true)
        {
            if (textSearching != null) textSearching.text = variants[idx];
            if (textSearchingVE != null) textSearchingVE.text = variants[idx];
            idx = (idx + 1) % variants.Length;
            yield return new WaitForSeconds(0.7f);
        }
    }

    private void StopSearchingCoroutineIfAny()
    {
        if (searchingCoroutine != null)
        {
            StopCoroutine(searchingCoroutine);
            searchingCoroutine = null;
        }
    }

    private UnityEngine.UIElements.Button FindButtonInTree(VisualElement root, string name)
    {
        if (root == null) return null;
        if (root is UnityEngine.UIElements.Button b && root.name == name) return b;

        for (int i = 0; i < root.hierarchy.childCount; i++)
        {
            var child = root.hierarchy[i];
            var found = FindButtonInTree(child, name);
            if (found != null) return found;
        }

        return null;
    }

    void OnDestroy()
    {
        if (locationButton != null) locationButton.onClick.RemoveListener(OnLocationButtonClicked);
        if (toolkitButton != null) toolkitButton.clicked -= OnLocationButtonClicked;
    }
}