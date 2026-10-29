using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class CardInspectionTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void Cleanup()
    {
        foreach (Object value in created) if (value != null) Object.DestroyImmediate(value);
        created.Clear();
    }

    [Test]
    public void GeneratedInspectionPrefabPreservesTheActualCardVisual()
    {
        string path = "Assets/Prefabs/UI/CardInspectionTest_" + System.Guid.NewGuid().ToString("N") + ".prefab";
        GameObject contents = null;
        try
        {
            typeof(CardInspectionAssetSetup).GetMethod("CreatePrefab", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { path });
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            contents = PrefabUtility.LoadPrefabContents(path);
            RectTransform root = (RectTransform)contents.transform;
            Assert.That(root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(root.anchorMax, Is.EqualTo(Vector2.one));
            RectTransform visual = (RectTransform)contents.transform.Find("Backdrop/CardView/HoverVisual");
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(visual.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(visual.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(visual.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(visual.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(contents.GetComponentInChildren<DeckCardHoverUI>(true), Is.Null);
        }
        finally
        {
            if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
            AssetDatabase.DeleteAsset(path);
        }
    }

    [UnityTest]
    public IEnumerator HandInspectionShowsStatusesImmediatelyAndBlocksGameplayUntilTheClosingFrameEnds()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI card = Cards()[0];
        CardData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<CardData>(
            "Assets/CardData/Soldier/Starting/Nightmare/Attack/02.Card_Corrosion/Corrosion.asset"));
        created.Add(data);
        data.requiredAmmo = 0;
        data.cost.dreamCost = data.cost.nightmareCost = 0;
        card.SetCard(data, battle);
        Assert.That(battle.CanUseCard(data), Is.True);
        Transform parent = card.transform.parent;
        Vector3 position = card.transform.position;
        int sibling = card.transform.GetSiblingIndex();
        int hp = battle.GetTarget().GetCurrentHP();
        int playerStatuses = battle.GetPlayer().StatusEffects.ActiveEffects.Count;
        int enemyStatuses = battle.GetTarget().StatusEffects.ActiveEffects.Count;
        Button endTurn = GameObject.Find("TurnEndButton").GetComponent<Button>();
        EventSystem.current.SetSelectedGameObject(endTurn.gameObject);
        RightClick(card.gameObject);
        GameObject window = Window();
        Assert.That(window, Is.Not.Null, "Right-click must open the card inspection window.");
        Assert.That(window.activeInHierarchy, Is.True);
        DeckCardUI display = window.GetComponentInChildren<DeckCardUI>();
        Assert.That(display, Is.Not.Null);
        Assert.That(window.GetComponentInChildren<CardUI>(), Is.Null);
        Assert.That(Text(display, "descriptionText").text, Is.EqualTo(CardDescriptionBuilder.Build(data, battle, applyStatusModifiers: false)));
        Assert.That(window.transform.Find("StatusDescriptions/StatusText").GetComponent<TMP_Text>().text,
            Does.Contain(AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset").DisplayName));
        Assert.That(card.transform.parent, Is.EqualTo(parent));
        Assert.That(card.transform.position, Is.EqualTo(position));
        Assert.That(card.transform.GetSiblingIndex(), Is.EqualTo(sibling));
        Assert.That(battle.GetTarget().GetCurrentHP(), Is.EqualTo(hp));
        Assert.That(battle.GetPlayer().StatusEffects.ActiveEffects.Count, Is.EqualTo(playerStatuses));
        Assert.That(battle.GetTarget().StatusEffects.ActiveEffects.Count, Is.EqualTo(enemyStatuses));
        Assert.That(battle.CanUseCard(data), Is.False);
        Assert.That(battle.UseCard(card), Is.False);
        battle.EndPlayerTurn();
        Assert.That(State(battle), Is.EqualTo(BattleState.PlayerTurn));
        Assert.That(endTurn.interactable, Is.False);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
        var down = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        card.OnPointerDown(down);
        Assert.That(card.transform.parent, Is.EqualTo(parent));
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(candidate => candidate.name == "DeckListPanel");
        list.ShowCards(new[] { data });
        Assert.That(new SerializedObject(list).FindProperty("panel").objectReferenceValue,
            Is.Not.Null);
        Assert.That(((GameObject)new SerializedObject(list).FindProperty("panel").objectReferenceValue).activeSelf, Is.False);
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        ClickAt((RectTransform)display.transform);
        Assert.That(window.activeInHierarchy, Is.True, "Clicking the displayed card must keep the window open.");
        ClickAt((RectTransform)window.transform.Find("StatusDescriptions"));
        Assert.That(window.activeSelf, Is.False, "Clicking outside the card, including its status explanation, closes the window.");
        Assert.That(battle.CanUseCard(data), Is.False, "The close click cannot trigger gameplay in the same frame.");
        yield return null;
        yield return null;
        Assert.That(battle.CanUseCard(data), Is.True);
        Assert.That(endTurn.interactable, Is.True);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(endTurn.gameObject));
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AllFourPileInspectionsPreserveTheOpenListAndThePile()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        DeckController deck = Object.FindFirstObjectByType<DeckController>();
        foreach (CardDeckType type in new[] { CardDeckType.TypeDream, CardDeckType.TypeNightmare })
            deck.DiscardCard(deck.GetDeck(type)[0], type);
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(candidate => candidate.name == "DeckListPanel");
        Transform content = (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;
        foreach (DeckPileButton button in Object.FindObjectsByType<DeckPileButton>(FindObjectsSortMode.None))
        {
            button.GetComponent<Button>().onClick.Invoke();
            yield return null;
            DeckCardUI card = content.GetComponentInChildren<DeckCardUI>();
            var originals = content.GetComponentsInChildren<DeckCardUI>();
            var names = originals.Select(value => Text(value, "nameText").text).ToArray();
            var dream = deck.GetDrawPile(CardDeckType.TypeDream).ToArray();
            var nightmare = deck.GetDiscardPile(CardDeckType.TypeNightmare).ToArray();
            RightClick(card.gameObject);
            GameObject window = Window();
            Assert.That(window, Is.Not.Null);
            Assert.That(Text(window.GetComponentInChildren<DeckCardUI>(), "nameText").text,
                Is.EqualTo(Text(card, "nameText").text));
            list.Close();
            button.GetComponent<Button>().onClick.Invoke();
            Assert.That(content.GetComponentsInChildren<DeckCardUI>(), Is.EqualTo(originals));
            Assert.That(content.GetComponentsInChildren<DeckCardUI>().Select(value => Text(value, "nameText").text), Is.EqualTo(names));
            Assert.That(deck.GetDrawPile(CardDeckType.TypeDream), Is.EqualTo(dream));
            Assert.That(deck.GetDiscardPile(CardDeckType.TypeNightmare), Is.EqualTo(nightmare));
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            ClickBackground(window);
            yield return null;
            yield return null;
            Assert.That(content.GetComponentsInChildren<DeckCardUI>(), Is.EqualTo(originals));
            list.Close();
            yield return null;
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InspectionFitsResizedCanvasesAndDisablingItRestoresExistingButtonStates()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        Button endTurn = GameObject.Find("TurnEndButton").GetComponent<Button>();
        endTurn.interactable = false;
        CardUI card = Cards()[0];
        CardData data = ScriptableObject.CreateInstance<CardData>();
        SpendAmmoBonusEffect conditional = ScriptableObject.CreateInstance<SpendAmmoBonusEffect>();
        created.Add(data);
        created.Add(conditional);
        CardEffect rust = AssetDatabase.LoadAssetAtPath<CardEffect>("Assets/CardData/Status/Examples/Rust_EnemyEffect.asset");
        CardEffect exposure = AssetDatabase.LoadAssetAtPath<CardEffect>("Assets/CardData/Status/Examples/WeaknessExposure_EnemyEffect.asset");
        typeof(SpendAmmoBonusEffect).GetField("bonusEffects", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(conditional, new[] { rust, exposure, rust });
        data.cardName = "상태이상 설명";
        data.description = "조건부 상태이상";
        data.effects = new CardEffect[] { conditional };
        data.requiredAmmo = 99;
        card.SetCard(data, Object.FindFirstObjectByType<BattleManager>());
        RightClick(card.gameObject);
        GameObject window = Window();
        Assert.That(window, Is.Not.Null);
        TMP_Text statusText = window.transform.Find("StatusDescriptions/StatusText").GetComponent<TMP_Text>();
        Assert.That(statusText.text.Split(new[] { "<b>" }, System.StringSplitOptions.None).Length - 1, Is.EqualTo(2),
            "Conditional statuses must always be described, with duplicates removed.");
        DeckCardUI display = window.GetComponentInChildren<DeckCardUI>();
        Canvas canvas = window.GetComponentInParent<Canvas>().rootCanvas;
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.transform.localScale = Vector3.one;
        RectTransform canvasRect = (RectTransform)canvas.transform;
        foreach (Vector2 size in new[] { new Vector2(1920, 1080), new Vector2(1920, 540), new Vector2(420, 720) })
        {
            canvasRect.sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
            Assert.That(Vector3.Distance(display.transform.position, canvasRect.TransformPoint(Vector3.zero)), Is.LessThan(0.05f));
            AssertInsideCanvas((RectTransform)display.transform, canvasRect);
            if (window.transform.Find("StatusDescriptions").gameObject.activeSelf)
                AssertInsideCanvas((RectTransform)window.transform.Find("StatusDescriptions"), canvasRect);
            Assert.That(display.GetComponent<DeckCardHoverUI>(), Is.Null);
            RectTransform visual = (RectTransform)display.transform.Find("HoverVisual");
            Assert.That(visual.localScale, Is.EqualTo(Vector3.one), "The actual card graphics must remain visible.");
            Assert.That(visual.rect.size, Is.EqualTo(((RectTransform)display.transform).rect.size));
            Assert.That(visual.GetComponent<Canvas>().overrideSorting, Is.False);
        }
        GameObject owner = window.transform.parent.gameObject;
        owner.SetActive(false);
        yield return null;
        yield return null;
        Assert.That(endTurn.interactable, Is.False, "Restore the previous button state, not an unconditional true.");
        owner.SetActive(true);
        Assert.That(window.activeSelf, Is.False);
        RightClick(card.gameObject);
        Assert.That(window.activeSelf, Is.True, "Disabling the window owner must release the input lock.");
        owner.SetActive(false);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InspectionCannotOpenDuringDraggingEffectResolutionOrEnemyTurn()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        CardUI[] cards = Cards();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        cards[0].OnPointerDown(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        RightClick(cards[1].gameObject);
        Assert.That(Window(), Is.Null);
        typeof(CardUI).GetField("isDragging", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(cards[0], false);
        typeof(CardUI).GetMethod("ReturnToHand", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(cards[0], null);
        yield return WaitForCards();
        typeof(BattleManager).GetField("isResolvingEffects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(battle, true);
        RightClick(cards[1].gameObject);
        Assert.That(Window(), Is.Null);
        typeof(BattleManager).GetField("isResolvingEffects", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(battle, false);
        typeof(BattleManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(battle, BattleState.EnemyTurn);
        RightClick(cards[1].gameObject);
        Assert.That(Window(), Is.Null);
        typeof(BattleManager).GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(battle, BattleState.PlayerTurn);
        RightClick(cards[1].gameObject);
        Assert.That(Window(), Is.Not.Null);
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator HandAndPileInspectionShowBaseDamageWhileNormalCardsKeepCombatPreviews()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("BattleScene");
        yield return WaitForCards();
        BattleManager battle = Object.FindFirstObjectByType<BattleManager>();
        CardUI hand = Cards()[0];
        DamageEffect enemyDamage = ScriptableObject.CreateInstance<DamageEffect>();
        DamageEffect selfDamage = ScriptableObject.CreateInstance<DamageEffect>();
        DamageEffect bonusDamage = ScriptableObject.CreateInstance<DamageEffect>();
        SpendAmmoBonusEffect bonus = ScriptableObject.CreateInstance<SpendAmmoBonusEffect>();
        CardData data = ScriptableObject.CreateInstance<CardData>();
        created.AddRange(new Object[] { enemyDamage, selfDamage, bonusDamage, bonus, data });
        enemyDamage.damage = 20;
        selfDamage.damage = 10;
        bonusDamage.damage = 30;
        SetTarget(enemyDamage, CardTarget.Enemy);
        SetTarget(selfDamage, CardTarget.Player);
        SetTarget(bonusDamage, CardTarget.Enemy);
        CardEffect exposure = AssetDatabase.LoadAssetAtPath<CardEffect>(
            "Assets/CardData/Status/Examples/WeaknessExposure_EnemyEffect.asset");
        typeof(SpendAmmoBonusEffect).GetField("ammoCost", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(bonus, 1);
        typeof(SpendAmmoBonusEffect).GetField("bonusEffects", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(bonus, new CardEffect[] { exposure, bonusDamage });
        data.cardName = "기본 수치 검사";
        data.description = "적 {damage:0}, 자신 {damage:1}, 추가 {damage:2.1}; {ammoCost:2}알; {ammoCondition:2}";
        data.effects = new CardEffect[] { enemyDamage, selfDamage, bonus };
        hand.SetCard(data, battle);
        DeckListUI list = Object.FindObjectsByType<DeckListUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(candidate => candidate.name == "DeckListPanel");
        CardHandController sourceHand = hand.GetHandController();
        foreach (bool fromPile in new[] { false, true })
        {
            battle.GetPlayer().StatusEffects.Apply(AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/Rust.asset"), 2);
            battle.GetTarget().StatusEffects.Apply(AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/WeaknessExposure.asset"), 2);
            AmmoSystem ammo = battle.GetPlayer().GetAmmoSystem();
            ammo.Load(1);
            GameObject source = hand.gameObject;
            if (fromPile)
            {
                list.ShowCards(new[] { data }, sourceHand);
                Transform content = (Transform)new SerializedObject(list).FindProperty("content").objectReferenceValue;
                source = content.GetComponentInChildren<DeckCardUI>().gameObject;
            }
            string combatDescription = CardDescriptionBuilder.Build(data, battle);
            Assert.That(combatDescription.Split(';')[0], Is.Not.EqualTo("적 20, 자신 10, 추가 30"));
            RightClick(source);
            GameObject window = Window();
            Assert.That(window, Is.Not.Null);
            TMP_Text description = Text(window.GetComponentInChildren<DeckCardUI>(), "descriptionText");
            Assert.That(description.text.Split(';')[0], Is.EqualTo("적 20, 자신 10, 추가 30"),
                "The inspection must show original damage, including self damage and conditional bonus damage.");
            Assert.That(description.text, Does.Contain("현재 발동 가능"));
            Assert.That(window.transform.Find("StatusDescriptions/StatusText").GetComponent<TMP_Text>().text,
                Does.Contain(AssetDatabase.LoadAssetAtPath<StatusEffectData>("Assets/StatusData/WeaknessExposure.asset").DisplayName));
            Assert.That((fromPile ? Text(source.GetComponent<DeckCardUI>(), "descriptionText") :
                (TMP_Text)new SerializedObject(hand).FindProperty("descriptionText").objectReferenceValue).text,
                Is.EqualTo(combatDescription));
            battle.GetPlayer().StatusEffects.Clear();
            battle.GetTarget().StatusEffects.Clear();
            Assert.That(description.text.Split(';')[0], Is.EqualTo("적 20, 자신 10, 추가 30"),
                "A status applied earlier in the card's own effect order must not modify inspection damage either.");
            ammo.TrySpend(1);
            Assert.That(description.text.Split(';')[0], Is.EqualTo("적 20, 자신 10, 추가 30"));
            Assert.That(description.text, Does.Contain("현재 총알 부족"));
            Assert.That(CardDescriptionBuilder.Build(data, battle), Does.Contain("45"));
            Object.FindFirstObjectByType<CardInspectionUI>().Close();
            yield return null;
            yield return null;
            if (fromPile) list.Close();
        }
        Assert.That(enemyDamage.damage, Is.EqualTo(20));
        Assert.That(selfDamage.damage, Is.EqualTo(10));
        Assert.That(bonusDamage.damage, Is.EqualTo(30));
        yield return new ExitPlayMode();
    }

    private static void SetTarget(CardEffect effect, CardTarget target)
    {
        SerializedObject state = new SerializedObject(effect);
        state.FindProperty("target").enumValueIndex = (int)target;
        state.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text Text(DeckCardUI card, string name) =>
        (TMP_Text)new SerializedObject(card).FindProperty(name).objectReferenceValue;

    private static BattleState State(BattleManager battle) => (BattleState)typeof(BattleManager)
        .GetField("currentState", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(battle);

    private static CardUI[] Cards() => Object.FindObjectsByType<CardUI>(FindObjectsSortMode.None);
    private static GameObject Window()
    {
        CardInspectionUI inspection = Object.FindFirstObjectByType<CardInspectionUI>();
        if (inspection == null) return null;
        GameObject panel = (GameObject)new SerializedObject(inspection).FindProperty("panel").objectReferenceValue;
        return panel != null && panel.activeInHierarchy ? panel : null;
    }

    private static void RightClick(GameObject card) => ExecuteEvents.Execute(card,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right }, ExecuteEvents.pointerClickHandler);

    private static void ClickAt(RectTransform rect) => Click(RectTransformUtility.WorldToScreenPoint(null, rect.position));

    private static void ClickBackground(GameObject window)
    {
        RectTransform rect = (RectTransform)window.transform;
        Click(RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector2(rect.rect.xMin + 5f, rect.rect.yMin + 5f))));
    }

    private static void Click(Vector2 position)
    {
        var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits, Is.Not.Empty);
        Canvas layer = Window().GetComponentInParent<Canvas>();
        Assert.That(hits[0].gameObject.transform.IsChildOf(Window().transform), Is.True,
            "The modal must receive the click before the underlying UI. Layer: " + layer.renderMode + "/" + layer.overrideSorting + "/" + layer.sortingOrder +
            ", background depth: " + Window().GetComponent<Image>().depth + ", hits: " + string.Join(", ", hits.Select(hit => hit.gameObject.name + ":" + hit.sortingOrder)));
        ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
    }

    private static IEnumerator WaitForCards()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while ((Cards().Length < 6 || Cards().Any(card => !card.CanHover)) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Cards().Length, Is.GreaterThanOrEqualTo(6));
        Assert.That(Cards().All(card => card.CanHover), Is.True);
    }

    private static void AssertInsideCanvas(RectTransform visual, RectTransform canvas)
    {
        var corners = new Vector3[4];
        visual.GetWorldCorners(corners);
        foreach (Vector3 corner in corners)
        {
            Vector3 point = canvas.InverseTransformPoint(corner);
            Assert.That(point.x, Is.InRange(canvas.rect.xMin - 0.05f, canvas.rect.xMax + 0.05f));
            Assert.That(point.y, Is.InRange(canvas.rect.yMin - 0.05f, canvas.rect.yMax + 0.05f));
        }
    }
}
