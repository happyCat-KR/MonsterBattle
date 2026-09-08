using System;
using UnityEngine;

namespace Shieldfront
{
    public enum TroopKind { Shield, Spear, Archer, Cavalry, Tower, Goblin, Raider, Brute }
    public enum CampaignPhase { Preparation, Battle, Won, Lost }

    [Serializable]
    public class TroopStats
    {
        public string title;
        public string description;
        public int cost, count;
        public float health, damage, range, speed, interval;
        public TroopStats(string name, string detail, int price, int members, float hp, float hit, float reach, float movement, float cooldown)
        { title = name; description = detail; cost = price; count = members; health = hp; damage = hit; range = reach; speed = movement; interval = cooldown; }
    }

    [CreateAssetMenu(menuName = "Shieldfront/Defense Rules")]
    public class DefenseRules : ScriptableObject
    {
        [Header("조정 가능한 캠페인 수치")]
        public int startingGold = 180;
        public int fortressHealth = 100;
        public int waveCount = 8;
        public int squadLimit = 12;
        public int rewardBase = 65;
        public int rewardPerWave = 12;
        public int repairCost = 35;
        public int repairAmount = 25;
        public float waveSpawnInterval = .68f;
        public int baseEnemies = 9;
        public int enemiesPerWave = 4;
        public float enemyGrowth = .13f;
        public TroopStats[] troops = Defaults();
        public TroopStats Get(TroopKind kind) => troops[(int)kind];
        public static TroopStats[] Defaults() => new[]
        {
            new TroopStats("방패 보병", "4인 방진 · 피해 35% 감소", 55, 4, 125, 12, 1.35f, 2.5f, 1.05f),
            new TroopStats("장창병", "4인 전열 · 돌격병에 강함", 65, 4, 88, 19, 2.7f, 2.4f, 1.25f),
            new TroopStats("궁수", "3인 후열 · 원거리 지원", 70, 3, 60, 18, 9f, 2.2f, 1.5f),
            new TroopStats("기마대", "2인 기동대 · 첫 타격 돌격", 95, 2, 210, 29, 1.7f, 4.8f, 1.05f),
            new TroopStats("감시탑", "고정 방어 · 넓은 사거리", 100, 1, 350, 33, 12f, 0, 1.15f),
            new TroopStats("고블린", "보병", 0, 1, 65, 9, 1.15f, 2.1f, 1.2f),
            new TroopStats("늑대 돌격병", "빠른 돌파", 0, 1, 85, 14, 1.4f, 3.8f, 1.05f),
            new TroopStats("오우거", "중장갑 우두머리", 0, 1, 550, 34, 2.1f, 1.5f, 1.6f)
        };
    }
}
