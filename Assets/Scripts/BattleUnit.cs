using System.Collections.Generic;
using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    public enum Team
    {
        Ally,
        Enemy
    }

    public enum Role
    {
        Infantry,
        Spearman,
        Cavalry
    }

    [Header("진영")]
    [SerializeField] private Team team;
    [SerializeField] private Role role;

    [Header("임시 전투 수치")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(1f)] private float attackDamage = 20f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1f;
    [SerializeField, Min(0.1f)] private float moveSpeed = 2f;

    // 캡슐 중심 사이의 거리입니다.
    [SerializeField, Min(1f)] private float attackRange = 1.5f;

    private static readonly List<BattleUnit> ActiveUnits = new();

    private float currentHealth;
    private float attackTimer;
    private Camera battleCamera;

    public bool IsAlive => currentHealth > 0f;
    public Team UnitTeam => team;
    public Role UnitRole => role;

    // 배치할 위치에 다른 병사가 있는지 확인합니다.
    // 기본 캡슐 크기를 기준으로 한 임시 간격입니다.
    public static bool IsPositionFree(
        BattleUnit movingUnit,
        Vector3 position,
        float minimumDistance = 1.2f)
    {
        foreach (BattleUnit other in ActiveUnits)
        {
            if (other == null || other == movingUnit || !other.IsAlive)
            {
                continue;
            }

            Vector3 difference = other.transform.position - position;
            difference.y = 0f;

            if (difference.sqrMagnitude < minimumDistance * minimumDistance)
            {
                return false;
            }
        }

        return true;
    }

    public static int CountAlive(Team targetTeam)
    {
        int count = 0;

        foreach (BattleUnit unit in ActiveUnits)
        {
            if (unit != null && unit.IsAlive && unit.team == targetTeam)
            {
                count++;
            }
        }

        return count;
    }

    // Play를 새로 시작할 때 이전 목록을 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveUnits.Clear();
    }

    public void Initialize(
        Team assignedTeam,
        Role assignedRole,
        float health,
        float damage,
        float speed,
        float interval,
        float range)
    {
        team = assignedTeam;
        role = assignedRole;
        maxHealth = Mathf.Max(1f, health);
        currentHealth = maxHealth;

        attackDamage = Mathf.Max(1f, damage);
        moveSpeed = Mathf.Max(0.1f, speed);
        attackInterval = Mathf.Max(0.1f, interval);
        attackRange = Mathf.Max(1f, range);

        // 접근 직후 공격할 수 있도록 준비합니다.
        attackTimer = 0f;
    }

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        if (!ActiveUnits.Contains(this))
        {
            ActiveUnits.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveUnits.Remove(this);
    }

    private void Update()
    {

        if (!IsAlive || !BattleController.IsFighting)
        {
            return;
        }

        attackTimer = Mathf.Max(0f, attackTimer - Time.deltaTime);

        BattleUnit target = FindNearestEnemy();

        if (target == null)
        {
            return;
        }

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;

        float distance = direction.magnitude;

        if (distance > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        if (distance > attackRange)
        {
            // 적에게 너무 가까이 들어가지 않도록 이동 거리를 제한합니다.
            float movement = Mathf.Min(
                moveSpeed * Time.deltaTime,
                distance - attackRange);

            transform.position += direction.normalized * movement;
        }
        else if (attackTimer <= 0f)
        {
            attackTimer = attackInterval;
            target.TakeDamage(attackDamage);
        }
    }

    private BattleUnit FindNearestEnemy()
    {
        BattleUnit nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (BattleUnit other in ActiveUnits)
        {
            if (other == null || other == this ||
                !other.IsAlive || other.team == team)
            {
                continue;
            }

            Vector3 difference = other.transform.position - transform.position;
            difference.y = 0f;

            float distanceSquared = difference.sqrMagnitude;

            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = other;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest;
    }

    private void TakeDamage(float damage)
    {
        if (!IsAlive)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        if (!IsAlive)
        {
            // 이번 단계에서는 사망한 병사를 화면에서 숨깁니다.
            // OnDisable에서 전투 대상 목록에서도 제거됩니다.
            gameObject.SetActive(false);
        }
    }

    private void OnGUI()
    {
        if (!IsAlive)
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

        // 캡슐 머리 위 위치를 화면 좌표로 변환합니다.
        Vector3 screenPosition = battleCamera.WorldToScreenPoint(
            transform.position + Vector3.up * 1.4f);

        if (screenPosition.z <= 0f)
        {
            return;
        }

        const float width = 48f;
        const float height = 7f;

        Rect background = new Rect(
            screenPosition.x - width / 2f,
            Screen.height - screenPosition.y,
            width,
            height);

        Color originalColor = GUI.color;

        GUI.color = Color.black;
        GUI.DrawTexture(background, Texture2D.whiteTexture);

        float healthRatio = Mathf.Clamp01(currentHealth / maxHealth);

        Rect fill = new Rect(
            background.x + 1f,
            background.y + 1f,
            (width - 2f) * healthRatio,
            height - 2f);

        GUI.color = team == Team.Ally
            ? new Color(0.3f, 1f, 0.4f)
            : new Color(1f, 0.4f, 0.3f);

        GUI.DrawTexture(fill, Texture2D.whiteTexture);
        GUI.color = originalColor;
    }
}
