using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Shieldfront;

public static class DefenseProjectTools
{
    public const string ScenePath = "Assets/Scenes/Shieldfront.unity";
    const string RulesPath = "Assets/Shieldfront/Resources/DefenseRules.asset";

    [MenuItem("Shieldfront/Open Game")]
    public static void OpenGame()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before opening the game.");
        if (EditorSceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene first; existing edits are preserved.");
        var rules = AssetDatabase.LoadAssetAtPath<DefenseRules>(RulesPath);
        if (rules == null) { rules = ScriptableObject.CreateInstance<DefenseRules>(); AssetDatabase.CreateAsset(rules, RulesPath); }
        if (!File.Exists(ScenePath))
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Shieldfront Game"); go.AddComponent<DefenseGame>().rules = rules;
            // Explicit shader reference keeps the procedural model shader in player builds.
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            string materialPath = "Assets/Shieldfront/Resources/DefenseSurface.mat";
            if (!File.Exists(materialPath)) AssetDatabase.CreateAsset(material, materialPath);
            else UnityEngine.Object.DestroyImmediate(material);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("SHIELDFRONT_READY: " + ScenePath);
    }

    [MenuItem("Shieldfront/Validate Campaign")]
    public static void ValidateCampaign()
    {
        var rules = AssetDatabase.LoadAssetAtPath<DefenseRules>(RulesPath);
        if (rules == null) rules = ScriptableObject.CreateInstance<DefenseRules>();
        var results = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new Exception("VALIDATION FAILED: " + name); results.Add("PASS " + name); }
        var sim = new DefenseSimulation(rules);
        Check(!sim.StartWave(), "Empty army cannot start");
        Check(!sim.Buy(TroopKind.Shield, new Vector3(18, 0, 0)), "Enemy zone rejects deployment");
        Check(sim.Buy(TroopKind.Shield, new Vector3(0, 0, 0)), "Buy formation");
        int money = sim.gold;
        Check(!sim.Buy(TroopKind.Archer, Vector3.zero) && sim.gold == money, "Overlapping purchase does not spend gold");
        Check(sim.Move(0, new Vector3(-3, 0, 0)), "Reposition formation");
        Check(sim.Upgrade(0), "Upgrade formation");
        string json = JsonUtility.ToJson(sim.Save());
        var loaded = new DefenseSimulation(rules);
        Check(loaded.Restore(JsonUtility.FromJson<CampaignSave>(json)), "Save round trip");
        Check(loaded.gold == sim.gold && loaded.squads[0].level == 2, "Save preserves economy and upgrades");
        int refund = Mathf.FloorToInt(sim.squads[0].invested * .7f);
        Check(sim.Sell(0) && sim.gold == money - Mathf.RoundToInt(rules.Get(TroopKind.Shield).cost * .65f) + refund, "Sell refunds investment once");
        Check(!sim.Sell(0), "Cannot sell removed squad twice");
        Check(!loaded.Restore(new CampaignSave { wave = 999, health = 100 }), "Reject invalid save");
        Check(loaded.StartWave(), "Begin battle");
        Check(!loaded.Move(0, Vector3.zero) && !loaded.Sell(0) && !loaded.Upgrade(0), "Combat locks army editing");
        Check(loaded.Rally() && !loaded.Rally(), "Rally only once per wave");
        for (int i = 0; i < 18000 && loaded.phase == CampaignPhase.Battle; i++) loaded.Step(1f / 30f);
        Check(loaded.phase != CampaignPhase.Battle, "Battle terminates without stalemate");

        var failure = new DefenseSimulation(rules);
        failure.Buy(TroopKind.Archer, new Vector3(-11, 0, 9));
        int safety = 0;
        while (failure.phase != CampaignPhase.Lost && failure.phase != CampaignPhase.Won && safety++ < 9)
        {
            failure.StartWave();
            for (int i = 0; i < 18000 && failure.phase == CampaignPhase.Battle; i++) failure.Step(1f / 30f);
        }
        Check(failure.phase == CampaignPhase.Lost && failure.health == 0, "Undefended fortress can lose");
        float[] lanes = { 0, -6, 6 };
        var campaign = new DefenseSimulation(rules);
        campaign.Buy(TroopKind.Shield, new Vector3(-1, 0, 0));
        campaign.Buy(TroopKind.Archer, new Vector3(-6, 0, 0));
        campaign.Buy(TroopKind.Shield, new Vector3(-1, 0, -6));
        safety = 0;
        while (campaign.phase == CampaignPhase.Preparation && safety++ < rules.waveCount)
        {
            foreach (float lane in lanes) campaign.Buy(TroopKind.Shield, new Vector3(-1, 0, lane));
            foreach (float lane in lanes) campaign.Buy(TroopKind.Archer, new Vector3(-6, 0, lane));
            foreach (float lane in lanes) campaign.Buy(TroopKind.Spear, new Vector3(-3.5f, 0, lane + 2.5f));
            for (int j = 0; j < campaign.squads.Count; j++) campaign.Upgrade(j);
            if (campaign.health < 70) campaign.Repair();
            campaign.StartWave();
            for (int i = 0; i < 18000 && campaign.phase == CampaignPhase.Battle; i++)
            { if (i == 420) campaign.Rally(); campaign.Step(1f / 30f); }
            results.Add($"WAVE {safety}: phase={campaign.phase}, gate={campaign.health}, gold={campaign.gold}, time={campaign.elapsed:F1}s");
            Check(campaign.phase != CampaignPhase.Battle, "Campaign wave " + safety + " finishes");
        }
        Check(campaign.phase == CampaignPhase.Won, "Affordable mixed army completes all eight waves");
        string path = "docs/verification/campaign-validation.txt";
        Directory.CreateDirectory("docs/verification"); File.WriteAllLines(path, results);
        Debug.Log("SHIELDFRONT_VALIDATION_PASSED\n" + string.Join("\n", results));
    }

    [MenuItem("Shieldfront/Build macOS Game")]
    public static void BuildMac()
    {
        OpenGame();
        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = "Builds/Shieldfront.app",
            target = BuildTarget.StandaloneOSX, options = BuildOptions.None });
        string summary = $"{report.summary.result}: {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, {report.summary.totalSize} bytes";
        Directory.CreateDirectory("docs/verification"); File.WriteAllText("docs/verification/build.txt", summary);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception(summary);
        Debug.Log("SHIELDFRONT_BUILD_PASSED: " + summary);
    }
}
