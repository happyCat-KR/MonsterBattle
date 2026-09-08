#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Shieldfront;

// Editor-only integration checks. Does not ship in the player.
public class DefensePlayVerification : MonoBehaviour
{
    readonly List<string> checks = new List<string>();
    string previousSave;
    bool hadSave;

    [MenuItem("Shieldfront/Verify Running Game")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play mode first.");
        var game = UnityEngine.Object.FindAnyObjectByType<DefenseGame>();
        if (game == null) throw new Exception("Open Shieldfront scene first.");
        game.gameObject.AddComponent<DefensePlayVerification>();
    }
    IEnumerator Start()
    {
        var game = GetComponent<DefenseGame>();
        hadSave = PlayerPrefs.HasKey(DefenseGame.SaveKey);
        previousSave = PlayerPrefs.GetString(DefenseGame.SaveKey, "");
        game.NewCampaign();
        yield return null;
        try
        {
            ClickCard(game, 0); Check(game.SelectedKind == 0, "GraphicRaycaster + button click selects Shield card");
            Check(game.Place(new Vector3(-1, 0, 0)), "Place shield formation through controller");
            ClickCard(game, 2); Check(game.Place(new Vector3(-6, 0, 0)), "Archer card purchase and deployment");
            ClickCard(game, 0); Check(game.Place(new Vector3(-1, 0, -6)), "Second shield formation deployment");
            Check(game.Sim.gold == 0 && game.Sim.fighters.Count == 11, "Displayed army matches purchased members and gold");
            game.SelectCard(0);
        }
        catch (Exception e) { Fail(e); yield break; }
        yield return null;
        yield return null;
        try
        {
            Physics.SyncTransforms();
            var soldier = game.Sim.fighters[0];
            Vector3 screen = game.BattleCamera.WorldToScreenPoint(soldier.position + Vector3.up);
            Check(Physics.Raycast(game.BattleCamera.ScreenPointToRay(screen), out _, 160, 1 << 29), "Visible soldier body can be selected by raycast");
        }
        catch (Exception e) { Fail(e); yield break; }
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("docs/verification/deployment.png");
        yield return null;
        try
        {
            ClickNamedButton("전투 시작");
            Check(game.Sim.phase == CampaignPhase.Battle, "Start button begins wave");
            game.TogglePause();
        }
        catch (Exception e) { Fail(e); yield break; }
        float before = game.Sim.elapsed;
        yield return new WaitForSecondsRealtime(.15f);
        try
        {
            Check(Mathf.Approximately(before, game.Sim.elapsed), "Pause freezes simulation");
            // Same simulation used by Update, stepped quickly for repeatable screen sampling.
            for (int i = 0; i < 230; i++) game.Sim.Step(1f / 30f);
            Check(game.Sim.fighters.Exists(f => f.enemy && f.Alive), "Wave spawns visible enemies");
        }
        catch (Exception e) { Fail(e); yield break; }
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot("docs/verification/battle.png");
        yield return null;
        try
        {
            for (int i = 0; i < 18000 && game.Sim.phase == CampaignPhase.Battle; i++) game.Sim.Step(1f / 30f);
            Check(game.Sim.phase == CampaignPhase.Preparation && game.Sim.wave == 2, "Live wave completion triggers preparation");
            Check(game.Sim.fighters.TrueForAll(f => !f.enemy && Mathf.Approximately(f.hp, f.maxHp)), "All purchased units return with full health");
            Check(game.HasSave, "Preparation saves automatically");
            int gold = game.Sim.gold;
            game.LoadCampaign(); Check(game.Sim.wave == 2 && game.Sim.gold == gold, "Continue restores actual saved campaign");
            game.SelectSquad(0); game.Upgrade();
            Check(game.Sim.squads[0].level == 2, "Controller upgrades selected formation");
            game.Sell(); Check(game.Sim.squads.Count == 2, "Controller sells selected formation");
            game.NewCampaign();
            Check(game.Sim.wave == 1 && game.Sim.gold == game.rules.startingGold && game.Sim.fighters.Count == 0, "Restart removes old combatants and resets economy");
            File.WriteAllLines("docs/verification/play-validation.txt", checks);
            Debug.Log("SHIELDFRONT_PLAY_VALIDATION_PASSED\n" + string.Join("\n", checks));
        }
        catch (Exception e) { Fail(e); yield break; }
        RestoreSave(); Destroy(this);
    }
    void ClickCard(DefenseGame game, int index)
    {
        var buttons = game.GetComponentsInChildren<Button>();
        foreach (var button in buttons)
            if (button.GetComponentInChildren<RawImage>() != null)
            { if (index-- == 0) { Click(button); return; } }
        throw new Exception("Card not found");
    }
    void ClickNamedButton(string name)
    {
        foreach (var button in GetComponentsInChildren<Button>()) if (button.name == name) { Click(button); return; }
        throw new Exception("Button not found: " + name);
    }
    void Click(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect = (RectTransform)button.transform;
        Vector2 p = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = p, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<Button>() != button)
            throw new Exception("Button is occluded at screen point: " + p);
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
    }
    void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks.Add("PASS " + label); }
    void Fail(Exception e)
    {
        checks.Add("FAIL " + e.Message); File.WriteAllLines("docs/verification/play-validation.txt", checks);
        Debug.LogError("SHIELDFRONT_PLAY_VALIDATION_FAILED: " + e); RestoreSave(); Destroy(this);
    }
    void RestoreSave()
    { if (hadSave) PlayerPrefs.SetString(DefenseGame.SaveKey, previousSave); else PlayerPrefs.DeleteKey(DefenseGame.SaveKey); PlayerPrefs.Save(); }
}
#endif
