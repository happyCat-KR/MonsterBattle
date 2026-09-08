using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Shieldfront
{
    // Anchored uGUI HUD: no IMGUI windows and no centered modal panels.
    public class DefenseHUD : MonoBehaviour
    {
        DefenseGame game;
        Font font;
        Canvas canvas;
        Text stats, status, notice, waveInfo, selection, primaryLabel, rallyLabel, pauseLabel, speedLabel, soundLabel;
        Text resumeLabel;
        Image healthFill;
        Button primary, upgrade, sell, repair, rally, pause, resume;
        readonly List<Button> cards = new List<Button>();
        readonly List<Image> cardAccents = new List<Image>();
        readonly List<RenderTexture> portraits = new List<RenderTexture>();
        readonly List<Image> wavePips = new List<Image>();
        readonly Color background = new Color(.055f, .083f, .105f);
        readonly Color panel = new Color(.085f, .12f, .145f);
        readonly Color ink = new Color(.91f, .92f, .87f);
        readonly Color muted = new Color(.56f, .65f, .67f);

        public void Build(DefenseGame owner)
        {
            game = owner;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular", "Malgun Gothic", "Noto Sans CJK KR", "Arial Unicode MS", "Arial" }, 22);
            var root = new GameObject("Shieldfront · Korean HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = 0;
            if (EventSystem.current == null)
            {
                var es = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule)); es.transform.SetParent(transform, false);
            }
            var top = Strip(root.transform, "Campaign header", 90, true, background);
            Label(top, "SHIELDFRONT", 28, 15, 320, 31, 29, ink, true);
            Label(top, "앰버 고개 방어전   /   THE AMBER PASS", 30, 52, 400, 23, 14, muted);
            status = Label(top, "방어 준비", 445, 17, 200, 25, 20, DefenseArt.Gold, true);
            for (int i = 0; i < game.rules.waveCount; i++)
                wavePips.Add(Image(top, "Wave marker", 448 + i * 24, 55, 17, 5, muted));
            Label(top, "성문 내구도", 700, 18, 170, 20, 14, muted);
            Image(top, "Gate health track", 700, 50, 190, 9, panel);
            healthFill = Image(top, "Gate health fill", 700, 50, 190, 9, new Color(.49f, .73f, .49f));
            stats = Label(top, "", 930, 18, 460, 50, 21, ink, true);
            var sound = Action(top, "소리 켜짐", 1420, 20, 154, 46, game.ToggleAudio, panel, out soundLabel);

            var ribbon = Strip(root.transform, "Field briefing", 47, true, panel);
            ribbon.anchoredPosition = new Vector2(0, -90);
            notice = Label(ribbon, "", 28, 10, 1530, 29, 17, ink);

            var deck = Strip(root.transform, "Army deck", 250, false, background);
            Image(deck, "Gold rule", 0, 0, 1600, 2, new Color(.51f, .41f, .23f));
            Label(deck, "부대 편성", 28, 15, 200, 26, 21, ink, true);
            Label(deck, "카드 선택 → 전장 클릭으로 배치", 182, 20, 530, 20, 14, muted);
            for (int i = 0; i < 5; i++) BuildCard(deck, i);
            Label(deck, "1–5  부대 선택     우클릭  취소     WASD  시점 이동     휠  확대     Home  시점 초기화", 28, 225, 950, 20, 13, muted);

            Image(deck, "Action separator", 964, 22, 1, 205, new Color(.22f, .27f, .27f));
            waveInfo = Label(deck, "", 990, 21, 255, 83, 17, ink);
            repair = Action(deck, "성문 수리", 990, 110, 245, 43, game.Repair, panel, out _);
            resume = Action(deck, "저장한 원정 계속", 990, 163, 245, 42, game.LoadCampaign, panel, out resumeLabel);
            primary = Action(deck, "전투 시작", 1260, 28, 313, 67, game.StartBattle, new Color(.66f, .48f, .22f), out primaryLabel);
            rally = Action(deck, "전장의 함성 · R", 1260, 105, 313, 47, game.Rally, new Color(.15f, .32f, .35f), out rallyLabel);
            pause = Action(deck, "일시정지", 1260, 163, 149, 42, game.TogglePause, panel, out pauseLabel);
            Action(deck, "속도 ×1", 1422, 163, 151, 42, game.ToggleSpeed, panel, out speedLabel);
            Action(deck, "새 원정", 1440, 212, 132, 28, game.Restart, background, out _);
            Label(deck, "Space  출전 / 일시정지", 1260, 218, 190, 20, 12, muted);

            var context = Strip(root.transform, "Selected formation", 58, false, new Color(.085f, .12f, .145f, .97f));
            context.anchoredPosition = new Vector2(0, 250);
            selection = Label(context, "", 28, 16, 950, 28, 17, ink);
            upgrade = Action(context, "부대 강화", 1010, 10, 260, 38, game.Upgrade, new Color(.15f, .26f, .29f), out _);
            sell = Action(context, "부대 해산", 1285, 10, 288, 38, game.Sell, panel, out _);
        }
        void BuildCard(RectTransform deck, int index)
        {
            int capture = index; var stat = game.rules.troops[index]; float x = 28 + index * 184;
            var button = Action(deck, "", x, 51, 174, 163, () => game.SelectCard(capture), panel, out _);
            cards.Add(button);
            cardAccents.Add(Image(button.transform, "Unit stripe", 0, 0, 174, 3, DefenseArt.Accent((TroopKind)index)));
            var imageObj = new GameObject("Unit portrait", typeof(RectTransform), typeof(RawImage)); imageObj.transform.SetParent(button.transform, false);
            Position(imageObj.GetComponent<RectTransform>(), 1, 4, 172, 104);
            var raw = imageObj.GetComponent<RawImage>(); raw.raycastTarget = false; raw.texture = Portrait((TroopKind)index);
            Label(button.transform, (index + 1).ToString("00"), 10, 9, 30, 20, 12, muted);
            Label(button.transform, stat.title, 12, 113, 106, 22, 19, ink, true);
            Label(button.transform, stat.cost.ToString(), 115, 113, 47, 22, 19, DefenseArt.Gold, true, TextAnchor.MiddleRight);
            Label(button.transform, stat.description, 10, 139, 157, 20, 11, muted);
        }
        RenderTexture Portrait(TroopKind kind)
        {
            var model = game.Art.Soldier(kind, game.Art.transform); model.root.transform.position = new Vector3(0, 1000, 0);
            model.root.transform.rotation = Quaternion.Euler(0, 155, 0);
            foreach (Transform t in model.root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
            if (model.healthFill != null) model.healthFill.parent.gameObject.SetActive(false);
            var go = new GameObject("Portrait capture"); var camera = go.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = kind == TroopKind.Tower ? 2.6f : kind == TroopKind.Cavalry ? 1.6f : 1.13f;
            float center = kind == TroopKind.Tower ? 2.1f : kind == TroopKind.Cavalry ? 1.4f : 1;
            camera.transform.position = new Vector3(0, 1000 + center + 1, -6);
            camera.transform.LookAt(new Vector3(0, 1000 + center, 0));
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.105f, .15f, .17f);
            camera.cullingMask = 1 << 30; camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            var texture = new RenderTexture(344, 208, 24); texture.name = kind + " Portrait"; texture.Create();
            camera.targetTexture = texture; camera.Render(); camera.targetTexture = null;
            Destroy(go); model.root.SetActive(false); Destroy(model.root); portraits.Add(texture); return texture;
        }
        public void Refresh()
        {
            var sim = game.Sim; if (sim == null) return;
            bool prepare = sim.phase == CampaignPhase.Preparation, battle = sim.phase == CampaignPhase.Battle;
            status.text = sim.phase switch { CampaignPhase.Preparation => $"제 {sim.wave}차 · 방어 준비", CampaignPhase.Battle => game.Paused ? "일시정지" : $"제 {sim.wave}차 · 교전 중", CampaignPhase.Won => "원정 승리", _ => "성문 함락" };
            stats.text = $"{sim.health} / {game.rules.fortressHealth}     |     {sim.gold} 골드     |     {sim.squads.Count} / {game.rules.squadLimit} 부대";
            healthFill.rectTransform.sizeDelta = new Vector2(190f * sim.health / game.rules.fortressHealth, 9);
            notice.text = game.Notice;
            for (int i = 0; i < wavePips.Count; i++) wavePips[i].color = i < sim.wave - 1 || sim.phase == CampaignPhase.Won ? DefenseArt.Gold : i == sim.wave - 1 ? ink : muted * .5f;
            int enemy = 0, friendly = 0;
            foreach (var f in sim.fighters) if (f.Alive) { if (f.enemy) enemy++; else friendly++; }
            string route = sim.wave <= 2 ? "중앙 도로" : "중앙 + 양쪽 측면";
            waveInfo.text = prepare ? $"다음 공세  {sim.EnemyCountForWave}명\n진입로  {route}\n{((sim.wave == 4 || sim.wave == game.rules.waveCount) ? "경고: 오우거 출현" : "공세 후 전 부대 회복")}" : $"생존 아군 {friendly}명\n남은 적 {enemy + sim.remainingSpawns}명\n누적 처치 {sim.kills}명";
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].interactable = prepare;
                cards[i].GetComponent<Image>().color = game.SelectedKind == i ? new Color(.23f, .30f, .29f) : panel;
                cardAccents[i].color = game.SelectedKind == i ? DefenseArt.Gold : DefenseArt.Accent((TroopKind)i);
            }
            primary.interactable = prepare && sim.squads.Count > 0;
            primaryLabel.text = sim.phase switch { CampaignPhase.Preparation => "전투 시작  →", CampaignPhase.Battle => game.Paused ? "전투 일시정지" : "성문을 지켜라", CampaignPhase.Won => "모든 공세 격퇴!", _ => "원정 종료" };
            rally.interactable = battle && !sim.rallyUsed && !game.Paused;
            rallyLabel.text = sim.rallyTime > 0 ? $"함성 적용 중 · {Mathf.CeilToInt(sim.rallyTime)}초" : sim.rallyUsed ? "이번 공세에 사용 완료" : "전장의 함성 · R";
            pause.interactable = battle; pauseLabel.text = game.Paused ? "계속하기" : "일시정지";
            speedLabel.text = $"속도 ×{game.Speed}"; soundLabel.text = game.Muted ? "소리 꺼짐" : "소리 켜짐";
            repair.interactable = prepare && sim.gold >= game.rules.repairCost && sim.health < game.rules.fortressHealth;
            repair.GetComponentInChildren<Text>().text = $"성문 +{game.rules.repairAmount} 수리 · {game.rules.repairCost} 골드";
            resume.interactable = prepare && game.HasSave;
            resumeLabel.text = game.HasSave ? "저장한 원정 계속" : "공세 준비 시 자동 저장";
            bool selected = prepare && game.SelectedSquad >= 0 && game.SelectedSquad < sim.squads.Count;
            upgrade.gameObject.SetActive(selected); sell.gameObject.SetActive(selected);
            if (selected)
            {
                var squad = sim.squads[game.SelectedSquad];
                selection.text = $"선택: {game.rules.Get(squad.kind).title}   ·   {squad.level}등급   |   빈 위치 클릭으로 이동";
                upgrade.GetComponentInChildren<Text>().text = squad.level >= 3 ? "최고 등급" : $"강화 · {sim.UpgradeCost(game.SelectedSquad)} 골드";
                upgrade.interactable = squad.level < 3 && sim.gold >= sim.UpgradeCost(game.SelectedSquad);
                sell.GetComponentInChildren<Text>().text = $"해산 · +{Mathf.FloorToInt(squad.invested * .7f)} 골드";
            }
            else selection.text = prepare ? "전술: 방패 전열로 적을 묶고, 후방 궁수로 지원하세요. 3차 공세부터 측면도 방어해야 합니다." :
                sim.phase == CampaignPhase.Won ? "승리! 앰버 고개의 수호자로 기록되었습니다. [새 원정]으로 다시 플레이하세요." :
                sim.phase == CampaignPhase.Lost ? "패배 · [새 원정]으로 다시 도전하세요. 성문 수리와 측면 배치를 활용해 보세요." : "전장의 함성: 생존 아군 체력 20% 회복 · 8초 공격력 +50% · 공세당 한 번";
        }
        RectTransform Strip(Transform parent, string name, float height, bool top, Color color)
        {
            var image = Image(parent, name, 0, 0, 0, height, color); var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0, top ? 1 : 0); rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.pivot = new Vector2(.5f, top ? 1 : 0); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(0, height); return rect;
        }
        static void Position(RectTransform rect, float x, float y, float w, float h)
        { rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); }
        Image Image(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, w, h); var image = go.GetComponent<Image>(); image.color = color; return image;
        }
        Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
            Position(go.GetComponent<RectTransform>(), x, y, w, h);
            var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = color; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; text.raycastTarget = false; return text;
        }
        Button Action(Transform parent, string title, float x, float y, float w, float h, UnityEngine.Events.UnityAction callback, Color color, out Text label)
        {
            var image = Image(parent, title, x, y, w, h, color); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.2f, 1.18f, 1.08f);
            colors.pressedColor = new Color(.72f, .82f, .8f); colors.disabledColor = new Color(.53f, .57f, .57f, .65f); button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(callback);
            label = Label(image.transform, title, 6, 2, w - 12, h - 4, 17, ink, true, TextAnchor.MiddleCenter); return button;
        }
        void OnDestroy()
        {
            foreach (var texture in portraits) { texture.Release(); Destroy(texture); }
            if (font != null) Destroy(font);
        }
    }
}
