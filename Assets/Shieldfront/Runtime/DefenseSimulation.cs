using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shieldfront
{
    [Serializable]
    public class SquadOrder
    {
        public TroopKind kind;
        public Vector3 position;
        public int level = 1;
        public int invested;
    }

    [Serializable]
    public class CampaignSave
    {
        public int version = 1;
        public int wave, gold, health;
        public List<SquadOrder> squads = new List<SquadOrder>();
    }

    // Simulation is independent of scene objects: visual frames and automation use the same rules.
    public sealed class Fighter
    {
        public int id, squad;
        public TroopKind kind;
        public bool enemy, charged;
        public Vector3 position, home, facing;
        public float hp, maxHp, damage, range, speed, interval, cooldown, attackPulse;
        public bool Alive => hp > 0;
    }

    public class DefenseSimulation
    {
        public readonly DefenseRules rules;
        public readonly List<SquadOrder> squads = new List<SquadOrder>();
        public readonly List<Fighter> fighters = new List<Fighter>();
        public CampaignPhase phase { get; private set; }
        public int gold { get; private set; }
        public int health { get; private set; }
        public int wave { get; private set; } = 1;
        public int remainingSpawns { get; private set; }
        public int kills { get; private set; }
        public float elapsed { get; private set; }
        public float rallyTime { get; private set; }
        public bool rallyUsed { get; private set; }
        public event Action<Fighter, Fighter, float> Hit;
        public event Action<int> GateHit;
        public event Action WaveEnded;
        float spawnClock;
        int spawnIndex, nextId;

        public DefenseSimulation(DefenseRules settings)
        { rules = settings; gold = settings.startingGold; health = settings.fortressHealth; }

        public bool CanPlace(Vector3 p, int ignore = -1)
        {
            if (p.x < -11 || p.x > 3 || p.z < -9 || p.z > 9) return false;
            for (int i = 0; i < squads.Count; i++)
                if (i != ignore && (squads[i].position - p).sqrMagnitude < 10f) return false;
            return true;
        }
        public bool Buy(TroopKind kind, Vector3 p)
        {
            if (phase != CampaignPhase.Preparation || (int)kind > 4 || (int)kind < 0 ||
                squads.Count >= rules.squadLimit || !CanPlace(p)) return false;
            int cost = rules.Get(kind).cost;
            if (gold < cost) return false;
            gold -= cost;
            squads.Add(new SquadOrder { kind = kind, position = p, invested = cost });
            PrepareArmy();
            return true;
        }
        public bool Move(int index, Vector3 p)
        {
            if (phase != CampaignPhase.Preparation || index < 0 || index >= squads.Count || !CanPlace(p, index)) return false;
            squads[index].position = p; PrepareArmy(); return true;
        }
        public int UpgradeCost(int index) => index >= 0 && index < squads.Count ? Mathf.RoundToInt(rules.Get(squads[index].kind).cost * .65f * squads[index].level) : 0;
        public bool Upgrade(int index)
        {
            if (phase != CampaignPhase.Preparation || index < 0 || index >= squads.Count || squads[index].level >= 3) return false;
            int price = UpgradeCost(index);
            if (gold < price) return false;
            gold -= price; squads[index].invested += price; squads[index].level++; PrepareArmy(); return true;
        }
        public bool Sell(int index)
        {
            if (phase != CampaignPhase.Preparation || index < 0 || index >= squads.Count) return false;
            gold += Mathf.FloorToInt(squads[index].invested * .7f); squads.RemoveAt(index); PrepareArmy(); return true;
        }
        public bool Repair()
        {
            if (phase != CampaignPhase.Preparation || gold < rules.repairCost || health >= rules.fortressHealth) return false;
            gold -= rules.repairCost; health = Mathf.Min(rules.fortressHealth, health + rules.repairAmount); return true;
        }
        public void PrepareArmy()
        {
            fighters.Clear();
            for (int s = 0; s < squads.Count; s++)
            {
                SquadOrder order = squads[s];
                var stat = rules.Get(order.kind);
                for (int j = 0; j < stat.count; j++)
                {
                    Vector3 offset = stat.count == 1 ? Vector3.zero : new Vector3((j / 2) * 1.15f - .55f, 0, (j % 2) * 1.3f - .65f);
                    Add(order.kind, false, order.position + offset, s, 1 + .4f * (order.level - 1));
                }
            }
        }
        Fighter Add(TroopKind kind, bool enemy, Vector3 pos, int squad, float multiplier)
        {
            var stat = rules.Get(kind);
            Fighter f = new Fighter { id = nextId++, kind = kind, enemy = enemy, squad = squad,
                position = pos, home = pos, facing = enemy ? Vector3.left : Vector3.right,
                hp = stat.health * multiplier, maxHp = stat.health * multiplier,
                damage = stat.damage * multiplier, range = stat.range, speed = stat.speed, interval = stat.interval,
                cooldown = (nextId % 5) * .08f };
            fighters.Add(f); return f;
        }
        public int EnemyCountForWave => rules.baseEnemies + (wave - 1) * rules.enemiesPerWave;
        public bool StartWave()
        {
            if (phase != CampaignPhase.Preparation || squads.Count == 0) return false;
            PrepareArmy(); phase = CampaignPhase.Battle;
            remainingSpawns = EnemyCountForWave; spawnClock = 0; spawnIndex = 0; elapsed = 0;
            rallyUsed = false; rallyTime = 0; return true;
        }
        public bool Rally()
        {
            if (phase != CampaignPhase.Battle || rallyUsed) return false;
            rallyUsed = true; rallyTime = 8;
            foreach (var f in fighters) if (!f.enemy && f.Alive) f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * .2f);
            return true;
        }
        public void Step(float dt)
        {
            if (phase != CampaignPhase.Battle || dt <= 0) return;
            elapsed += dt; spawnClock -= dt; rallyTime = Mathf.Max(0, rallyTime - dt);
            if (remainingSpawns > 0 && spawnClock <= 0)
            {
                int lane = wave <= 2 ? 0 : (spawnIndex % 3) - 1;
                TroopKind kind = wave >= 3 && spawnIndex % 4 == 2 ? TroopKind.Raider : TroopKind.Goblin;
                if ((wave == 4 || wave == rules.waveCount) && remainingSpawns == 1) kind = TroopKind.Brute;
                Add(kind, true, new Vector3(18f + (spawnIndex % 2) * 1.1f, 0, lane * 6f + (spawnIndex % 3 - 1) * .8f), -1, 1 + (wave - 1) * rules.enemyGrowth);
                remainingSpawns--; spawnIndex++; spawnClock += rules.waveSpawnInterval;
            }
            for (int i = 0; i < fighters.Count; i++)
            {
                Fighter f = fighters[i]; if (!f.Alive) continue;
                f.cooldown -= dt; f.attackPulse = Mathf.Max(0, f.attackPulse - dt * 3);
                Fighter target = null; float nearest = float.MaxValue;
                for (int j = 0; j < fighters.Count; j++)
                {
                    Fighter other = fighters[j];
                    if (!other.Alive || f.enemy == other.enemy) continue;
                    float d = (other.position - f.position).sqrMagnitude;
                    // Melee defenders protect a region. Cavalry intercept further away.
                    if (!f.enemy && (other.position - f.home).sqrMagnitude > (f.kind == TroopKind.Cavalry ? 225 : 144)) continue;
                    if (d < nearest) { nearest = d; target = other; }
                }
                Vector3 goal = f.enemy ? new Vector3(-15f, 0, f.position.z) : f.home;
                if (target != null)
                {
                    goal = target.position;
                    Vector3 facing = goal - f.position;
                    if (facing.sqrMagnitude > .01f) f.facing = facing.normalized;
                    if (nearest <= f.range * f.range)
                    {
                        if (f.cooldown <= 0)
                        {
                            float damage = f.damage * (!f.enemy && rallyTime > 0 ? 1.5f : 1);
                            if (f.kind == TroopKind.Spear && target.kind == TroopKind.Raider) damage *= 1.85f;
                            if (f.kind == TroopKind.Cavalry && !f.charged) { damage *= 2.4f; f.charged = true; }
                            if (target.kind == TroopKind.Shield) damage *= .65f;
                            target.hp = Mathf.Max(0, target.hp - damage);
                            if (!target.Alive && target.enemy) kills++;
                            f.cooldown = f.interval; f.attackPulse = 1;
                            Hit?.Invoke(f, target, damage);
                        }
                        continue;
                    }
                }
                if (f.enemy && f.position.x <= -14.4f)
                {
                    int damage = f.kind == TroopKind.Brute ? 25 : f.kind == TroopKind.Raider ? 9 : 5;
                    health = Mathf.Max(0, health - damage); f.hp = 0; GateHit?.Invoke(damage); continue;
                }
                if (f.speed <= 0) continue;
                Vector3 direction = goal - f.position;
                float move = Mathf.Min(f.speed * dt, direction.magnitude);
                if (direction.sqrMagnitude > .01f) f.facing = direction.normalized;
                Vector3 separation = Vector3.zero;
                foreach (var other in fighters)
                {
                    if (other == f || !other.Alive) continue;
                    Vector3 diff = f.position - other.position;
                    float sq = diff.sqrMagnitude;
                    if (sq > .001f && sq < .72f) separation += diff.normalized * (1 - Mathf.Sqrt(sq) / .85f);
                }
                f.position += direction.normalized * move + Vector3.ClampMagnitude(separation, 1) * dt * 1.8f;
                f.position = new Vector3(Mathf.Clamp(f.position.x, -15.2f, 20), 0, Mathf.Clamp(f.position.z, -10.5f, 10.5f));
            }
            if (health <= 0) { phase = CampaignPhase.Lost; WaveEnded?.Invoke(); return; }
            if (remainingSpawns > 0) return;
            foreach (var f in fighters) if (f.enemy && f.Alive) return;
            gold += rules.rewardBase + rules.rewardPerWave * wave;
            if (wave >= rules.waveCount) phase = CampaignPhase.Won;
            else { wave++; phase = CampaignPhase.Preparation; }
            WaveEnded?.Invoke();
        }
        public CampaignSave Save() => new CampaignSave { wave = wave, gold = gold, health = health, squads = squads };
        public bool Restore(CampaignSave save)
        {
            if (save == null || save.version != 1 || save.wave < 1 || save.wave > rules.waveCount || save.health <= 0 ||
                save.health > rules.fortressHealth || save.gold < 0 || save.squads == null || save.squads.Count > rules.squadLimit) return false;
            foreach (var s in save.squads)
                if (s == null || (int)s.kind < 0 || (int)s.kind > 4 || s.level < 1 || s.level > 3 || s.invested < 0 ||
                    float.IsNaN(s.position.x) || float.IsNaN(s.position.z) || s.position.y != 0 ||
                    s.position.x < -11 || s.position.x > 3 || s.position.z < -9 || s.position.z > 9) return false;
            squads.Clear(); squads.AddRange(save.squads); wave = save.wave; gold = save.gold; health = save.health;
            phase = CampaignPhase.Preparation; PrepareArmy(); return true;
        }
    }
}
