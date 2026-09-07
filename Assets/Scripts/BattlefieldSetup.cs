using UnityEngine;

public class BattlefieldSetup : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [Header("임시 전장 설정")]
    [SerializeField] private Vector2 fieldSize = new Vector2(20f, 14f);

    [Header("임시 병력 설정")]
    [SerializeField, Range(1, 8)] private int unitsPerTeam = 3;
    [SerializeField, Min(1.2f)] private float unitSpacing = 2f;
    [SerializeField, Min(2f)] private float teamDistance = 10f;

    [Header("아군 임시 전투 수치")]
    [SerializeField, Min(1f)] private float allyHealth = 100f;
    [SerializeField, Min(1f)] private float allyDamage = 20f;

    [Header("적 임시 전투 수치")]
    [SerializeField, Min(1f)] private float enemyHealth = 100f;
    [SerializeField, Min(1f)] private float enemyDamage = 20f;

    [Header("공통 임시 전투 수치")]
    [SerializeField, Min(0.1f)] private float movementSpeed = 2f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1f;
    [SerializeField, Min(1f)] private float attackRange = 1.5f;

    [Header("병종별 임시 보정")]
    [SerializeField, Min(0.1f)] private float spearmanRange = 2.8f;
    [SerializeField, Min(0.1f)] private float cavalrySpeed = 4.5f;
    [SerializeField, Min(0.1f)] private float cavalryHealth = 120f;

    [Header("웨이브 설정")]
    [SerializeField, Min(1)] private int startingWave = 1;
    [SerializeField, Min(1)] private int maximumAllies = 8;
    [SerializeField, Min(1)] private int enemyCountPerWave = 3;

    private Material groundMaterial;
    private Material allyMaterial;
    private Material enemyMaterial;
    private GameObject alliesRoot;
    private GameObject enemiesRoot;
    private int currentWave;
    private void Start()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            Debug.LogError("URP Lit 셰이더를 찾을 수 없습니다.", this);
            return;
        }

        groundMaterial = CreateMaterial(
            shader, new Color(0.30f, 0.38f, 0.27f));

        allyMaterial = CreateMaterial(
            shader, new Color(0.15f, 0.45f, 0.95f));

        enemyMaterial = CreateMaterial(
            shader, new Color(0.90f, 0.20f, 0.15f));

        currentWave = startingWave;
        CreateGround();
        alliesRoot = CreateArmy("Allies", -teamDistance / 2f, allyMaterial, 90f, true, unitsPerTeam);
        enemiesRoot = CreateArmy("Enemies", teamDistance / 2f, enemyMaterial, -90f, false, enemyCountPerWave);

        SetupCamera();
        SetupLight();

        BattleController controller = GetComponent<BattleController>();

        if (controller == null)
        {
            controller = gameObject.AddComponent<BattleController>();
        }

        controller.BeginDeployment(fieldSize, this);
    }

    private Material CreateMaterial(Shader shader, Color color)
    {
        Material material = new Material(shader);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.15f);
        return material;
    }

    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.SetParent(transform, false);

        // 地面의 윗면을 Y = 0에 맞춥니다.
        ground.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        ground.transform.localScale =
            new Vector3(fieldSize.x, 0.5f, fieldSize.y);

        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
    }

    private GameObject CreateArmy(
        string armyName,
        float xPosition,
        Material material,
        float rotation,
        bool isAlly,
        int count)
    {
        GameObject army = new GameObject(armyName);
        army.transform.SetParent(transform, false);

        float startZ = -(count - 1) * unitSpacing / 2f;

        for (int i = 0; i < count; i++)
        {
            GameObject unit =
                GameObject.CreatePrimitive(PrimitiveType.Capsule);

            unit.name = $"{armyName}_Unit_{i + 1}";
            unit.transform.SetParent(army.transform, false);

            // 기본 캡슐의 높이는 2이므로 중심을 Y = 1에 둡니다.
            unit.transform.localPosition =
                new Vector3(xPosition, 1f, startZ + i * unitSpacing);

            unit.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);
            unit.GetComponent<Renderer>().sharedMaterial = material;

            BattleUnit.Role role = isAlly
                ? GetAllyRole(i)
                : BattleUnit.Role.Infantry;

            float unitRange = role == BattleUnit.Role.Spearman
                ? spearmanRange
                : attackRange;

            float unitSpeed = role == BattleUnit.Role.Cavalry
                ? cavalrySpeed
                : movementSpeed;

            float unitHealth = role == BattleUnit.Role.Cavalry
                ? cavalryHealth
                : allyHealth;

            if (!isAlly)
            {
                unitHealth = enemyHealth * (1f + 0.25f * (currentWave - 1));
            }

            BattleUnit battleUnit = unit.AddComponent<BattleUnit>();

            battleUnit.Initialize(
                isAlly ? BattleUnit.Team.Ally : BattleUnit.Team.Enemy,
                role,
                unitHealth,
                isAlly ? allyDamage : enemyDamage * (1f + 0.2f * (currentWave - 1)),
                unitSpeed,
                attackInterval,
                unitRange);

            SetRoleColor(unit, role, isAlly);
        }

        return army;
    }

    private BattleUnit.Role GetAllyRole(int index)
    {
        return index switch
        {
            1 => BattleUnit.Role.Spearman,
            2 => BattleUnit.Role.Cavalry,
            _ => BattleUnit.Role.Infantry
        };
    }

    public int CurrentWave => currentWave;

    public bool Recruit(BattleUnit.Role role)
    {
        if (alliesRoot == null || alliesRoot.transform.childCount >= maximumAllies)
        {
            return false;
        }

        int index = alliesRoot.transform.childCount;
        float startZ = -(maximumAllies - 1) * unitSpacing / 2f;
        GameObject unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        unit.name = $"Allies_Unit_{index + 1}";
        unit.transform.SetParent(alliesRoot.transform, false);
        unit.transform.localPosition = new Vector3(
            -teamDistance / 2f, 1f, startZ + index * unitSpacing);
        unit.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        unit.GetComponent<Renderer>().sharedMaterial = allyMaterial;

        float range = role == BattleUnit.Role.Spearman ? spearmanRange : attackRange;
        float speed = role == BattleUnit.Role.Cavalry ? cavalrySpeed : movementSpeed;
        float health = role == BattleUnit.Role.Cavalry ? cavalryHealth : allyHealth;
        BattleUnit battleUnit = unit.AddComponent<BattleUnit>();
        battleUnit.Initialize(BattleUnit.Team.Ally, role, health, allyDamage,
            speed, attackInterval, range);
        SetRoleColor(unit, role, true);
        return true;
    }

    public void StartNextWave()
    {
        currentWave++;
        if (enemiesRoot != null)
        {
            enemiesRoot.SetActive(false);
            Destroy(enemiesRoot);
        }

        int count = enemyCountPerWave + currentWave - 1;
        enemiesRoot = CreateArmy("Enemies", teamDistance / 2f,
            enemyMaterial, -90f, false, count);

        BattleController controller = GetComponent<BattleController>();
        if (controller != null)
        {
            controller.BeginDeployment(fieldSize, this);
        }
    }

    private void SetRoleColor(
        GameObject unit,
        BattleUnit.Role role,
        bool isAlly)
    {
        Color color = isAlly
            ? role switch
            {
                BattleUnit.Role.Spearman => new Color(0.1f, 0.85f, 0.95f),
                BattleUnit.Role.Cavalry => new Color(0.95f, 0.75f, 0.1f),
                _ => new Color(0.15f, 0.45f, 0.95f)
            }
            : new Color(0.9f, 0.2f, 0.15f);

        unit.GetComponent<Renderer>().material.SetColor("_BaseColor", color);
    }

    private void SetupCamera()
    {
        Camera battleCamera = Camera.main;

        if (battleCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            battleCamera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        battleCamera.transform.position =
            transform.position + new Vector3(0f, 18f, -16f);

        battleCamera.transform.LookAt(transform.position);

        battleCamera.orthographic = true;

        // 가로 화면과 세로 화면 모두 기본 전장이 들어오도록 조정합니다.
        float aspect = Mathf.Max(battleCamera.aspect, 0.1f);
        battleCamera.orthographicSize =
            Mathf.Max(fieldSize.y * 0.6f, fieldSize.x / (2f * aspect)) + 2f;

        battleCamera.nearClipPlane = 0.1f;
        battleCamera.farClipPlane = 100f;
        battleCamera.clearFlags = CameraClearFlags.SolidColor;
        battleCamera.backgroundColor = new Color(0.12f, 0.16f, 0.21f);
    }

    private void SetupLight()
    {
        // 기본 씬의 방향광이 있으면 그대로 사용합니다.
        foreach (Light sceneLight in FindObjectsByType<Light>(
                     FindObjectsSortMode.None))
        {
            if (sceneLight.type == LightType.Directional &&
                sceneLight.isActiveAndEnabled)
            {
                return;
            }
        }

        GameObject lightObject = new GameObject("Battlefield Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        Light sunlight = lightObject.AddComponent<Light>();
        sunlight.type = LightType.Directional;
        sunlight.intensity = 1.2f;
        sunlight.shadows = LightShadows.Soft;
    }

    private void OnDestroy()
    {
        if (groundMaterial != null) Destroy(groundMaterial);
        if (allyMaterial != null) Destroy(allyMaterial);
        if (enemyMaterial != null) Destroy(enemyMaterial);
    }
}
