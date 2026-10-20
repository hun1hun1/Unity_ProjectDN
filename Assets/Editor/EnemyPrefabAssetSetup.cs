using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyPrefabAssetSetup
{
    public const string PrefabPath = "Assets/Prefabs/Enemy/TestEnemy.prefab";
    private const string ScenePath = "Assets/Scenes/BattleScene.unity";

    [MenuItem("ProjectDN/Setup Enemy Prefabs")]
    public static void CreateAssets()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Enemy")) AssetDatabase.CreateFolder("Assets/Prefabs", "Enemy");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            BattleManager battle = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BattleManager>(true)).Single();
            Enemy actor = battle.GetTarget();
            if (actor == null || !(actor.transform is RectTransform))
                throw new InvalidOperationException("BattleScene needs its existing Canvas enemy.");
            EnemyActionUI actionUI = (EnemyActionUI)new SerializedObject(actor).FindProperty("actionUI").objectReferenceValue;
            if (actionUI == null)
                actionUI = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EnemyActionUI>(true)).Single();

            var config = new SerializedObject(battle);
            RectTransform slot = config.FindProperty("enemySpawnRoot").objectReferenceValue as RectTransform;
            if (slot == null)
            {
                RectTransform source = (RectTransform)actor.transform;
                slot = new GameObject("EnemySpawnRoot", typeof(RectTransform)).GetComponent<RectTransform>();
                slot.gameObject.layer = actor.gameObject.layer;
                slot.SetParent(source.parent, false);
                slot.SetSiblingIndex(source.GetSiblingIndex());
                slot.anchorMin = source.anchorMin; slot.anchorMax = source.anchorMax;
                slot.pivot = source.pivot; slot.sizeDelta = source.sizeDelta;
                slot.anchoredPosition3D = source.anchoredPosition3D;
                slot.localRotation = source.localRotation; slot.localScale = source.localScale;
            }
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                GameObject copy = UnityEngine.Object.Instantiate(actor.gameObject);
                try
                {
                    copy.name = "TestEnemy";
                    copy.transform.SetParent(null, false);
                    Fill((RectTransform)copy.transform);
                    Bind(copy.GetComponent<Enemy>(), "actionUI", null);
                    prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabPath);
                    if (prefab == null) throw new InvalidOperationException("Could not save enemy prefab.");
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
            }
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(actor) != PrefabPath)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, slot);
                Enemy replacement = instance.GetComponent<Enemy>();
                Bind(replacement, "actionUI", actionUI);
                Bind(battle, "target", replacement);
                UnityEngine.Object.DestroyImmediate(actor.gameObject);
                actor = replacement;
            }
            else actor.transform.SetParent(slot, false);
            Fill((RectTransform)actor.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);
            Bind(battle, "enemySpawnRoot", slot);
            Bind(battle, "enemyActionUI", actionUI);
            BattleEncounterDefinition encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>("Assets/MapData/NormalEncounter.asset");
            if (encounter != null && encounter.enemyPrefab == null)
            {
                encounter.enemyPrefab = prefab.GetComponent<Enemy>();
                EditorUtility.SetDirty(encounter);
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save enemy scene bindings.");
            Debug.Log("Enemy prefab and battle spawn slot are ready.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void Fill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition3D = Vector3.zero; rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
    }
    private static void Bind(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
