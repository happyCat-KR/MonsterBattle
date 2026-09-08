using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Shieldfront
{
    public class DefenseGame : MonoBehaviour
    {
        public DefenseRules rules;
        public DefenseSimulation Sim { get; private set; }
        public DefenseArt Art { get; private set; }
        public Camera BattleCamera { get; private set; }
        public int SelectedKind { get; private set; } = -1;
        public int SelectedSquad { get; private set; } = -1;
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public bool Muted { get; private set; }
        public string Notice { get; private set; }
        public bool HasSave => PlayerPrefs.HasKey(SaveKey);
        public const string SaveKey = "Shieldfront.Campaign.v1";
        readonly Dictionary<int, SoldierView> views = new Dictionary<int, SoldierView>();
        readonly List<Flight> flights = new List<Flight>();
        Transform soldiers, effects, markers;
        GameObject preview, rangeRing;
        DefenseHUD hud;
        AudioSource audioSource;
        AudioClip clickSound, strikeSound, hornSound, victorySound, gateSound;
        float soundClock, accumulator, restartUntil;
        Vector3 cameraFocus = new Vector3(1, 0, 0);
        float zoom = 1;
        bool focusPaused;
        bool ownsRules;
        class Flight { public Transform t; public Vector3 from, to; public float age, duration; public bool arrow; }

        void Start()
        {
            if (rules == null) { rules = ScriptableObject.CreateInstance<DefenseRules>(); ownsRules = true; }
            Art = gameObject.AddComponent<DefenseArt>();
            Art.BuildWorld();
            soldiers = new GameObject("Armies").transform; soldiers.SetParent(transform, false);
            effects = new GameObject("Battle effects").transform; effects.SetParent(transform, false);
            markers = new GameObject("Deployment markers").transform; markers.SetParent(transform, false);
            SetupCamera(); SetupAudio();
            hud = gameObject.AddComponent<DefenseHUD>(); hud.Build(this);
            Muted = PlayerPrefs.GetInt("Shieldfront.Muted", 0) == 1;
            NewCampaign();
        }
        void SetupCamera()
        {
            var go = new GameObject("Battle Camera"); go.transform.SetParent(transform, false); go.tag = "MainCamera";
            BattleCamera = go.AddComponent<Camera>(); BattleCamera.orthographic = true;
            BattleCamera.backgroundColor = new Color(.085f, .13f, .16f); BattleCamera.clearFlags = CameraClearFlags.SolidColor;
            BattleCamera.nearClipPlane = .3f; BattleCamera.farClipPlane = 180;
            BattleCamera.cullingMask = ~(1 << 30); go.AddComponent<AudioListener>();
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>(); sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional; sun.intensity = 1.5f; sun.color = new Color(1, .88f, .69f);
            sun.transform.rotation = Quaternion.Euler(48, -35, 0); sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.60f, .67f, .69f);
            RenderSettings.fog = false; QualitySettings.shadowDistance = 100;
            Application.targetFrameRate = 60;
        }
        void Bind(DefenseSimulation sim)
        {
            Sim = sim;
            Sim.Hit += OnHit;
            Sim.GateHit += damage => { Play(gateSound, .55f); Notify($"성문이 공격받았습니다! 내구도 -{damage}"); };
            Sim.WaveEnded += OnWaveEnded;
        }
        public void NewCampaign()
        {
            ClearVisuals(); Bind(new DefenseSimulation(rules));
            SelectedKind = -1; SelectedSquad = -1; Paused = false; Speed = 1; restartUntil = 0;
            cameraFocus = new Vector3(1, 0, 0); zoom = 1;
            Notify("아래 부대 카드를 선택한 뒤, 점선 안의 전장을 클릭하세요. 방패 보병과 궁수 조합으로 시작해 보세요.");
            RebuildMarkers();
        }
        public void Restart()
        {
            if (Sim.phase == CampaignPhase.Won || Sim.phase == CampaignPhase.Lost || Time.unscaledTime < restartUntil)
            { PlayerPrefs.DeleteKey(SaveKey); NewCampaign(); return; }
            restartUntil = Time.unscaledTime + 4;
            Notify("새 원정을 시작하면 현재 진행이 초기화됩니다. 4초 안에 [새 원정]을 한 번 더 누르세요.");
        }
        public void LoadCampaign()
        {
            if (!HasSave || Sim.phase != CampaignPhase.Preparation) return;
            try
            {
                var save = JsonUtility.FromJson<CampaignSave>(PlayerPrefs.GetString(SaveKey));
                var next = new DefenseSimulation(rules);
                if (!next.Restore(save)) { Notify("저장 데이터가 현재 규칙과 맞지 않습니다."); return; }
                ClearVisuals(); Bind(next); SelectedKind = SelectedSquad = -1; Paused = false; RebuildMarkers();
                Notify($"제 {Sim.wave}차 공세 준비 상태를 불러왔습니다.");
            }
            catch (Exception) { Notify("저장 데이터를 읽지 못했습니다. 새 원정을 시작할 수 있습니다."); }
        }
        void SaveCampaign()
        {
            if (Sim.phase != CampaignPhase.Preparation) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Sim.Save())); PlayerPrefs.Save();
        }
        public void SelectCard(int kind)
        {
            if (Sim.phase != CampaignPhase.Preparation) return;
            SelectedKind = SelectedKind == kind ? -1 : kind; SelectedSquad = -1;
            Play(clickSound, .3f); RebuildMarkers();
            Notify(SelectedKind < 0 ? "선택을 해제했습니다." : $"{rules.troops[kind].title}: 전장을 클릭해 배치하세요. 우클릭으로 취소합니다.");
        }
        public bool Place(Vector3 point)
        {
            if (Sim.phase != CampaignPhase.Preparation) return false;
            bool placed = false;
            if (SelectedKind >= 0)
            {
                if (Sim.gold < rules.troops[SelectedKind].cost) { Notify("골드가 부족합니다. 다른 부대를 선택하거나 기존 부대를 판매하세요."); return false; }
                if (Sim.squads.Count >= rules.squadLimit) { Notify("최대 부대 수에 도달했습니다. 기존 부대를 강화하세요."); return false; }
                placed = Sim.Buy((TroopKind)SelectedKind, point);
            }
            else if (SelectedSquad >= 0) placed = Sim.Move(SelectedSquad, point);
            if (!placed) { Notify("배치 구역 안에서 다른 부대와 간격을 두고 배치하세요."); return false; }
            Play(clickSound, .4f); ClearUnitViews(); RebuildMarkers(); SaveCampaign();
            Notify("배치 완료. 부대를 클릭하면 이동·강화·판매할 수 있습니다."); return true;
        }
        public void SelectSquad(int index)
        {
            if (Sim.phase != CampaignPhase.Preparation || index < 0 || index >= Sim.squads.Count) return;
            SelectedKind = -1; SelectedSquad = index; RebuildMarkers();
            Notify("빈 전장을 클릭하면 이 부대가 이동합니다. 하단에서 강화·판매할 수 있습니다.");
        }
        public void Upgrade()
        {
            if (!Sim.Upgrade(SelectedSquad)) { Notify("강화할 부대를 선택하세요. 최대 3등급이며 골드가 필요합니다."); return; }
            Play(hornSound, .2f); ClearUnitViews(); RebuildMarkers(); SaveCampaign(); Notify("부대 강화! 체력과 공격력이 증가했습니다.");
        }
        public void Sell()
        {
            if (!Sim.Sell(SelectedSquad)) return;
            SelectedSquad = -1; ClearUnitViews(); RebuildMarkers(); SaveCampaign(); Play(clickSound, .4f); Notify("부대를 해산하고 투자 골드의 70%를 돌려받았습니다.");
        }
        public void Repair()
        {
            if (!Sim.Repair()) { Notify("성문 내구도가 가득 차 있거나 수리 골드가 부족합니다."); return; }
            SaveCampaign(); Play(clickSound, .4f); Notify($"성문 내구도를 {rules.repairAmount} 회복했습니다.");
        }
        public void StartBattle()
        {
            if (!Sim.StartWave()) { Notify("부대를 하나 이상 배치한 뒤 출전하세요."); return; }
            ClearUnitViews(); SelectedKind = SelectedSquad = -1; Paused = false;
            RebuildMarkers(); Play(hornSound, .6f); Notify("적 공세가 시작됐습니다. 위급할 때 [전장의 함성]으로 병력을 지원하세요!");
        }
        public void Rally()
        {
            if (Paused || !Sim.Rally()) return;
            Play(hornSound, .6f); Notify("전장의 함성! 생존 아군 체력 20% 회복 · 8초 동안 공격력 +50%.");
            foreach (var f in Sim.fighters) if (!f.enemy && f.Alive) Flash(f.position + Vector3.up, DefenseArt.Gold, false);
        }
        public void TogglePause() { if (Sim.phase == CampaignPhase.Battle) Paused = !Paused; }
        public void ToggleSpeed() { Speed = Speed == 1 ? 2 : 1; }
        public void ToggleAudio() { Muted = !Muted; PlayerPrefs.SetInt("Shieldfront.Muted", Muted ? 1 : 0); }
        public void Notify(string text) { Notice = text; }
        void OnWaveEnded()
        {
            Paused = false;
            if (Sim.phase == CampaignPhase.Preparation)
            {
                Play(victorySound, .5f);
                Notify($"제 {Sim.wave - 1}차 공세 격퇴! +{rules.rewardBase + rules.rewardPerWave * (Sim.wave - 1)} 골드 · 전 부대 회복. 다음 방어를 준비하세요.");
                Sim.PrepareArmy(); ClearUnitViews(); SaveCampaign(); RebuildMarkers();
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save();
                Play(Sim.phase == CampaignPhase.Won ? victorySound : gateSound, .65f);
                Notify(Sim.phase == CampaignPhase.Won ? "앰버 고개를 지켜냈습니다. 모든 공세를 격퇴했습니다! 새 원정으로 다시 도전할 수 있습니다." : "성문이 무너졌습니다. 방패 전열과 후방 궁수, 측면 방어를 조합해 다시 도전해 보세요.");
            }
        }
        void Update()
        {
            if (Sim == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f); soundClock -= dt;
            UpdateCamera(dt); HandleInput();
            if (!Paused && Sim.phase == CampaignPhase.Battle)
            {
                accumulator += dt * Speed;
                while (accumulator >= 1f / 30f) { Sim.Step(1f / 30f); accumulator -= 1f / 30f; }
            }
            else accumulator = 0;
            foreach (var f in Sim.fighters)
            {
                if (!views.TryGetValue(f.id, out var view))
                {
                    if (!f.Alive) continue;
                    view = Art.Soldier(f.kind, soldiers); views.Add(f.id, view);
                    view.root.transform.position = f.position; view.lastPosition = f.position;
                }
                if (view.root.activeSelf) Art.Pose(view, f, Paused ? 0 : dt * Speed, BattleCamera);
            }
            for (int i = flights.Count - 1; i >= 0; i--)
            {
                Flight f = flights[i]; f.age += Paused ? 0 : dt * Speed;
                float t = Mathf.Clamp01(f.age / f.duration);
                f.t.position = Vector3.Lerp(f.from, f.to, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * (f.arrow ? 2.2f : .3f);
                if (!f.arrow) f.t.localScale = Vector3.one * (.12f + t * .25f);
                if (t >= 1) { Destroy(f.t.gameObject); flights.RemoveAt(i); }
            }
            hud.Refresh();
        }
        void UpdateCamera(float dt)
        {
            float scale = Screen.width / 1600f;
            float lower = Mathf.Clamp(260 * scale / Screen.height, .16f, .48f);
            float upper = Mathf.Clamp(140 * scale / Screen.height, .1f, .24f);
            BattleCamera.rect = new Rect(0, lower, 1, 1 - lower - upper);
            BattleCamera.transform.position = cameraFocus + new Vector3(10, 30, -28);
            BattleCamera.transform.LookAt(cameraFocus);
            float fit = Mathf.Max(15.2f, 25f / BattleCamera.aspect);
            BattleCamera.orthographicSize = fit * zoom;
        }
        void HandleInput()
        {
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.escapeKey.wasPressedThisFrame) { if (Sim.phase == CampaignPhase.Battle) TogglePause(); else { SelectedKind = SelectedSquad = -1; RebuildMarkers(); } }
                if (k.spaceKey.wasPressedThisFrame) { if (Sim.phase == CampaignPhase.Preparation) StartBattle(); else TogglePause(); }
                if (k.rKey.wasPressedThisFrame) Rally();
                var keys = new[] { k.digit1Key, k.digit2Key, k.digit3Key, k.digit4Key, k.digit5Key };
                for (int i = 0; i < keys.Length; i++) if (keys[i].wasPressedThisFrame) SelectCard(i);
                Vector3 pan = new Vector3((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), 0, (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                cameraFocus += pan * Time.unscaledDeltaTime * 10;
                cameraFocus.x = Mathf.Clamp(cameraFocus.x, -6, 8); cameraFocus.z = Mathf.Clamp(cameraFocus.z, -5, 5);
                if (k.homeKey.wasPressedThisFrame) { cameraFocus = new Vector3(1, 0, 0); zoom = 1; }
            }
            Mouse m = Mouse.current; if (m == null) return;
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overUI) zoom = Mathf.Clamp(zoom - m.scroll.ReadValue().y * .0008f, .6f, 1.25f);
            if (Sim.phase != CampaignPhase.Preparation) return;
            if (m.rightButton.wasPressedThisFrame) { SelectedKind = SelectedSquad = -1; RebuildMarkers(); }
            Vector2 screen = m.position.ReadValue();
            bool inView = BattleCamera.pixelRect.Contains(screen) && !overUI;
            var plane = new Plane(Vector3.up, Vector3.zero); var ray = BattleCamera.ScreenPointToRay(screen);
            if (!inView || !plane.Raycast(ray, out float distance)) { if (preview != null) preview.SetActive(false); return; }
            Vector3 point = ray.GetPoint(distance); point.y = 0;
            point.x = Mathf.Round(point.x * 2) * .5f; point.z = Mathf.Round(point.z * 2) * .5f;
            if (preview != null)
            {
                preview.SetActive(SelectedKind >= 0 || SelectedSquad >= 0); preview.transform.position = point;
                preview.GetComponent<LineRenderer>().sharedMaterial = Art.Mat(Sim.CanPlace(point, SelectedSquad) ? DefenseArt.Gold : new Color(.9f, .25f, .2f));
            }
            if (!m.leftButton.wasPressedThisFrame) return;
            if (SelectedKind >= 0) { Place(point); return; }
            for (int i = 0; i < Sim.squads.Count; i++)
                if ((Sim.squads[i].position - point).sqrMagnitude < 3f) { SelectSquad(i); return; }
            if (SelectedSquad >= 0) Place(point);
        }
        void RebuildMarkers()
        {
            foreach (Transform t in markers) Destroy(t.gameObject);
            if (preview != null) Destroy(preview); if (rangeRing != null) Destroy(rangeRing);
            if (Sim.phase != CampaignPhase.Preparation) return;
            // Dashed boundary, kept separate from the battlefield geometry.
            for (int z = -9; z <= 9; z++)
                foreach (float x in new[] { -11f, 3f }) Art.Line(markers, "Deploy boundary", new Vector3(x, .15f, z - .28f), new Vector3(x, .15f, z + .28f), .055f, DefenseArt.Gold);
            for (int x = -11; x <= 3; x++)
                foreach (float z in new[] { -9f, 9f }) Art.Line(markers, "Deploy boundary", new Vector3(x - .28f, .15f, z), new Vector3(x + .28f, .15f, z), .055f, DefenseArt.Gold);
            for (int i = 0; i < Sim.squads.Count; i++)
                Art.Ring(markers, Sim.squads[i].position + Vector3.up * .1f, 1.5f, i == SelectedSquad ? DefenseArt.Gold : DefenseArt.Blue);
            preview = Art.Ring(markers, Vector3.zero, 1.5f, DefenseArt.Gold); preview.SetActive(false);
            if (SelectedSquad >= 0 && SelectedSquad < Sim.squads.Count)
            {
                var s = Sim.squads[SelectedSquad];
                rangeRing = Art.Ring(markers, s.position + Vector3.up * .08f, rules.Get(s.kind).range, DefenseArt.Accent(s.kind));
            }
        }
        void OnHit(Fighter attacker, Fighter target, float damage)
        {
            if (attacker.kind == TroopKind.Archer || attacker.kind == TroopKind.Tower)
            {
                Vector3 from = attacker.position + Vector3.up * (attacker.kind == TroopKind.Tower ? 3.2f : 1.4f);
                Vector3 to = target.position + Vector3.up;
                var arrow = Art.Box(effects, "Arrow", from, new Vector3(.045f, .045f, .65f), DefenseArt.Gold);
                arrow.rotation = Quaternion.LookRotation(to - from);
                flights.Add(new Flight { t = arrow, from = from, to = to, duration = .22f, arrow = true });
            }
            else Flash(target.position + Vector3.up, target.enemy ? DefenseArt.Gold : Color.white, false);
            if (soundClock <= 0) { Play(strikeSound, .12f); soundClock = .10f; }
        }
        void Flash(Vector3 position, Color color, bool arrow)
        {
            var spark = Art.Box(effects, "Impact", position, Vector3.one * .15f, color);
            spark.rotation = UnityEngine.Random.rotation;
            flights.Add(new Flight { t = spark, from = position, to = position + Vector3.up * .5f, duration = .23f });
        }
        void ClearUnitViews()
        { foreach (var v in views.Values) if (v.root != null) { v.root.SetActive(false); Destroy(v.root); } views.Clear(); }
        void ClearVisuals()
        {
            ClearUnitViews(); foreach (var f in flights) if (f.t != null) Destroy(f.t.gameObject); flights.Clear();
        }
        void SetupAudio()
        {
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 0;
            clickSound = Tone("Selection", .07f, 720, false);
            strikeSound = Tone("Steel impact", .12f, 165, true);
            hornSound = Tone("War horn", .8f, 196, false);
            victorySound = Tone("Victory chime", 1.2f, 392, false);
            gateSound = Tone("Gate impact", .5f, 65, true);
        }
        AudioClip Tone(string name, float duration, float hz, bool noise)
        {
            const int rate = 22050; float[] data = new float[(int)(rate * duration)]; var random = new System.Random(91);
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / rate, envelope = Mathf.Min(1, t * 80) * Mathf.Pow(1 - t / duration, 2);
                float note = name == "Victory chime" ? hz * (t < .3f ? 1 : t < .6f ? 1.25f : 1.5f) : hz;
                data[i] = envelope * .35f * (Mathf.Sin(2 * Mathf.PI * note * t) + .25f * Mathf.Sin(4 * Mathf.PI * note * t) + (noise ? (float)(random.NextDouble() * 2 - 1) * .7f : 0));
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void Play(AudioClip clip, float volume) { if (!Muted && clip != null) audioSource.PlayOneShot(clip, volume); }
        void OnApplicationFocus(bool focus)
        {
            if (Sim == null || Sim.phase != CampaignPhase.Battle) return;
            if (!focus && !Paused) { focusPaused = true; Paused = true; }
            else if (focus && focusPaused) { focusPaused = false; Paused = false; }
        }
        void OnDestroy()
        {
            foreach (var clip in new[] { clickSound, strikeSound, hornSound, victorySound, gateSound }) if (clip != null) Destroy(clip);
            if (ownsRules && rules != null) Destroy(rules);
        }
    }
}
