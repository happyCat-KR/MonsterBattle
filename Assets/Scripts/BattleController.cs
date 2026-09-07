using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BattleController : MonoBehaviour
{
    private enum BattleState
    {
        Deployment,
        Fighting,
        Victory,
        Defeat,
        Draw
    }

    public static bool IsFighting { get; private set; }

    private BattleState state;
    private bool initialized;
    private bool restarting;
    private BattlefieldSetup battlefield;
    private int gold = 10;
    private int playerLevel = 3;
    private int experience;
    private const int ExperiencePerVictory = 2;
    private const int InfantryCost = 2;
    private const int SpearmanCost = 3;
    private const int CavalryCost = 4;
    private bool rewardGranted;

    public int PlayerLevel => playerLevel;
    public int Experience => experience;

    private int alliesAlive;
    private int enemiesAlive;

    private Vector2 fieldSize;
    private Camera battleCamera;
    private BattleUnit selectedUnit;
    private Renderer selectedRenderer;

    private MaterialPropertyBlock selectionBlock;

    private string message = "파란 병사를 선택한 뒤 왼쪽 전장을 클릭하세요.";

    private GUIStyle titleStyle;
    private GUIStyle infoStyle;
    private GUIStyle buttonStyle;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBattleState()
    {
        IsFighting = false;
    }

    public void BeginDeployment(Vector2 size, BattlefieldSetup setup)
    {
        fieldSize = size;
        battlefield = setup;
        battleCamera = Camera.main;

        IsFighting = false;
        state = BattleState.Deployment;
        initialized = true;
        rewardGranted = false;

        RefreshCounts();
    }

    private void Recruit(BattleUnit.Role role, int cost)
    {
        if (state != BattleState.Deployment || battlefield == null)
        {
            return;
        }

        if (gold < cost)
        {
            message = "골드가 부족합니다.";
            return;
        }

        if (BattleUnit.CountAlive(BattleUnit.Team.Ally) >= playerLevel)
        {
            message = $"배치 한도({playerLevel}명)에 도달했습니다. 레벨을 올리면 더 배치할 수 있습니다.";
            return;
        }

        if (!battlefield.Recruit(role))
        {
            message = "현재 레벨의 배치 한도에 도달했습니다.";
            return;
        }

        gold -= cost;
        RefreshCounts();
        message = $"{RoleName(role)}을(를) 모집했습니다.";
    }

    private void Update()
    {
        if (!initialized || restarting ||
            state != BattleState.Deployment)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        if (mouse == null)
        {
            return;
        }

        if (mouse.rightButton.wasPressedThisFrame)
        {
            ClearSelection();
            message = "선택을 해제했습니다.";
        }

        if (!mouse.leftButton.wasPressedThisFrame)
        {
            return;
        }

        Vector2 mousePosition = mouse.position.ReadValue();

        // UI上のクリックを、地面への配置として扱わないようにします。
        Vector2 guiPosition = new Vector2(
            mousePosition.x,
            Screen.height - mousePosition.y);

        if (ToolbarRect().Contains(guiPosition))
        {
            return;
        }

        if (battleCamera == null)
        {
            battleCamera = Camera.main;
        }

        if (battleCamera == null)
        {
            return;
        }

        Ray ray = battleCamera.ScreenPointToRay(
            new Vector3(mousePosition.x, mousePosition.y, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, 200f))
        {
            message = "전장 안을 클릭하세요.";
            return;
        }

        BattleUnit clickedUnit =
            hit.collider.GetComponentInParent<BattleUnit>();

        if (clickedUnit != null)
        {
            if (clickedUnit.UnitTeam == BattleUnit.Team.Ally &&
                clickedUnit.IsAlive)
            {
                SelectUnit(clickedUnit);
                message = "선택했습니다. 왼쪽의 빈 장소를 클릭하세요.";
            }
            else
            {
                message = "적 병사는 이동할 수 없습니다.";
            }

            return;
        }

        if (selectedUnit == null)
        {
            message = "먼저 파란 아군을 선택하세요.";
            return;
        }

        if (hit.collider.gameObject.name != "Ground")
        {
            return;
        }

        // 전장 오브젝트 기준의 좌표로 배치 영역을 검사합니다.
        Vector3 localPoint = transform.InverseTransformPoint(hit.point);

        const float edgeMargin = 0.6f;

        bool insideAllyArea =
            localPoint.x >= -fieldSize.x / 2f + edgeMargin &&
            localPoint.x <= -edgeMargin &&
            Mathf.Abs(localPoint.z) <= fieldSize.y / 2f - edgeMargin;

        if (!insideAllyArea)
        {
            message = "전장의 왼쪽 절반에 배치하세요.";
            return;
        }

        // 병사의 높이는 유지하고 바닥 위 위치만 바꿉니다.
        Vector3 destination = transform.TransformPoint(
            new Vector3(localPoint.x, 0f, localPoint.z));

        destination.y = selectedUnit.transform.position.y;

        if (!BattleUnit.IsPositionFree(selectedUnit, destination))
        {
            message = "다른 병사와 너무 가깝습니다.";
            return;
        }

        selectedUnit.transform.position = destination;

        // 다음 클릭부터 변경된 콜라이더 위치를 사용합니다.
        Physics.SyncTransforms();

        message = "배치했습니다. 다른 병사를 선택하거나 전투 시작을 누르세요.";
    }

    private void SelectUnit(BattleUnit unit)
    {
        ClearSelection();

        if (selectionBlock == null)
        {
            selectionBlock = new MaterialPropertyBlock();
        }

        selectedUnit = unit;
        selectedRenderer = unit.GetComponent<Renderer>();

        if (selectedRenderer != null)
        {
            selectionBlock.Clear();
            selectionBlock.SetColor("_BaseColor", Color.yellow);
            selectedRenderer.SetPropertyBlock(selectionBlock);
        }
    }

    private void ClearSelection()
    {
        if (selectedRenderer != null && selectionBlock != null)
        {
            selectionBlock.Clear();
            selectedRenderer.SetPropertyBlock(selectionBlock);
        }

        selectedUnit = null;
        selectedRenderer = null;
    }

    private void BeginBattle()
    {
        if (state != BattleState.Deployment)
        {
            return;
        }

        RefreshCounts();

        if (alliesAlive == 0 || enemiesAlive == 0)
        {
            message = "아군과 적이 각각 한 명 이상 필요합니다.";
            return;
        }

        ClearSelection();

        state = BattleState.Fighting;
        IsFighting = true;
    }

    private void LateUpdate()
    {
        if (!initialized || restarting)
        {
            return;
        }

        RefreshCounts();

        if (state != BattleState.Fighting)
        {
            return;
        }

        if (alliesAlive == 0 && enemiesAlive == 0)
        {
            state = BattleState.Draw;
        }
        else if (enemiesAlive == 0)
        {
            state = BattleState.Victory;
        }
        else if (alliesAlive == 0)
        {
            state = BattleState.Defeat;
        }

        if (state == BattleState.Victory && !rewardGranted)
        {
            gold += 5;
            AddExperience(ExperiencePerVictory);
            rewardGranted = true;
        }

        IsFighting = state == BattleState.Fighting;
    }

    private void AddExperience(int amount)
    {
        experience += amount;
        int required = playerLevel * 4;

        while (experience >= required)
        {
            experience -= required;
            playerLevel++;
            required = playerLevel * 4;
        }
    }

    private void RefreshCounts()
    {
        alliesAlive = BattleUnit.CountAlive(BattleUnit.Team.Ally);
        enemiesAlive = BattleUnit.CountAlive(BattleUnit.Team.Enemy);
    }

    private string RoleName(BattleUnit.Role role)
    {
        return role switch
        {
            BattleUnit.Role.Spearman => "창병",
            BattleUnit.Role.Cavalry => "기마병",
            _ => "일반병"
        };
    }

    private string PhaseName(string phase)
    {
        return phase switch
        {
            "DEPLOYMENT" => "배치",
            "BATTLE" => "전투",
            _ => "결과"
        };
    }

    private Rect ToolbarRect()
    {
        float width = Mathf.Min(560f, Screen.width - 20f);

        return new Rect(
            (Screen.width - width) / 2f,
            Mathf.Max(8f, Screen.height - 175f),
            width,
            160f);
    }

    private void OnGUI()
    {
        if (!initialized)
        {
            return;
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20
            };
        }

        Rect toolbar = ToolbarRect();

        Rect hud = new Rect(12f, 12f, Screen.width - 24f, 42f);
        GUI.Box(hud, GUIContent.none);
        GUI.Label(
            new Rect(hud.x + 12f, hud.y + 8f, hud.width - 24f, 26f),
            $"레벨 {playerLevel}    웨이브 {battlefield.CurrentWave}    골드 {gold}    생존 병사 {alliesAlive}",
            infoStyle);

        GUI.Box(toolbar, GUIContent.none);

        string phase = state == BattleState.Deployment
            ? "DEPLOYMENT"
            : state == BattleState.Fighting ? "BATTLE" : "RESULT";

            GUI.Label(
                new Rect(toolbar.x, toolbar.y + 5f, toolbar.width, 25f),
                state == BattleState.Deployment
                    ? "대기석에서 병사를 선택하고 전장에 배치하세요"
                    : "자동 전투 진행 중",
                infoStyle);

        if (state == BattleState.Deployment)
        {
            GUI.Label(
                new Rect(
                    toolbar.x + 8f, toolbar.y + 32f,
                    toolbar.width - 16f, 38f),
                message,
                infoStyle);

            if (GUI.Button(
                    new Rect(
                        toolbar.x + toolbar.width - 205f,
                        toolbar.y + 112f,
                        190f,
                        36f),
                        "전투 시작",
                    buttonStyle))
            {
                BeginBattle();
            }

            if (GUI.Button(new Rect(toolbar.x + 8f, toolbar.y + 112f, 115f, 36f),
                $"일반병 모집 ({InfantryCost})", buttonStyle))
            {
                Recruit(BattleUnit.Role.Infantry, InfantryCost);
            }

            if (GUI.Button(new Rect(toolbar.x + 133f, toolbar.y + 112f, 115f, 36f),
                $"창병 모집 ({SpearmanCost})", buttonStyle))
            {
                Recruit(BattleUnit.Role.Spearman, SpearmanCost);
            }

            if (GUI.Button(new Rect(toolbar.x + 258f, toolbar.y + 112f, 115f, 36f),
                $"기마병 모집 ({CavalryCost})", buttonStyle))
            {
                Recruit(BattleUnit.Role.Cavalry, CavalryCost);
            }

            return;
        }

        if (state == BattleState.Fighting)
        {
            GUI.Label(
                new Rect(toolbar.x, toolbar.y + 45f, toolbar.width, 40f),
                "자동 전투 진행 중",
                infoStyle);

            return;
        }

        Rect panel = new Rect(
            (Screen.width - 320f) / 2f,
            (Screen.height - 190f) / 2f,
            320f,
            190f);

        GUI.Box(panel, GUIContent.none);

        string result = state switch
        {
            BattleState.Victory => "승리",
            BattleState.Defeat => "패배",
            _ => "무승부"
        };

        GUI.Label(
            new Rect(panel.x, panel.y + 20f, panel.width, 45f),
            result,
            titleStyle);

        GUI.Label(
            new Rect(panel.x, panel.y + 70f, panel.width, 30f),
            $"골드 +5    현재 골드: {gold}",
            infoStyle);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = previousEnabled && !restarting;

        if (GUI.Button(
                new Rect(panel.x + 60f, panel.y + 120f, 200f, 45f),
                restarting ? "불러오는 중..." : "다시 시작",
                buttonStyle))
        {
            RestartBattle();
        }

        if (state == BattleState.Victory && GUI.Button(
                new Rect(panel.x + 60f, panel.y + 170f, 200f, 40f),
                "다음 웨이브",
                buttonStyle))
        {
            state = BattleState.Deployment;
            IsFighting = false;
            battlefield.StartNextWave();
        }

        GUI.enabled = previousEnabled;
    }

    private void RestartBattle()
    {
        int sceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (sceneIndex < 0)
        {
            Debug.LogError(
                "현재 씬이 빌드 씬 목록에 없습니다.",
                this);
            return;
        }

        restarting = true;
        IsFighting = false;
        ClearSelection();

        SceneManager.LoadScene(sceneIndex);
    }

    private void OnDestroy()
    {
        IsFighting = false;
        selectedUnit = null;
        selectedRenderer = null;
        selectionBlock = null;
    }
}
