using System;
using UnityEngine;

[DefaultExecutionOrder(-50), DisallowMultipleComponent]
public class BattleCharacterPresentation : MonoBehaviour
{
    [SerializeField] private CharacterUIProfile profile;
    [SerializeField] private RectTransform visual;
    [SerializeField] private UISpriteAnimator spriteAnimator;
    [SerializeField] private UISpriteAnimation idleAnimation;
    [SerializeField] private UISpriteAnimation defeatAnimation;
    [SerializeField] private CharacterInfoUI infoUI;
    [SerializeField] private EnemyActionUI actionUI;
    [SerializeField, HideInInspector] private CharacterInfoUI appliedInfoPrefab;
    [SerializeField, HideInInspector] private EnemyActionUI appliedActionPrefab;
    private CharacterUIProfile appliedProfile;
    private Vector2 lastRootSize;
    private int lastLayoutHash;
    private bool initialized;
    private Vector2 visualBasePosition;
    private Vector2 presentationOffset;
    private float presentationRotation;
    private BlockGainFeedback blockGainFeedback;
    private BattleCharacter character;
    private HealthChangeFeedback healthChangeFeedback;
    private Vector2 healthNumberOrigin;
    private bool defeatStarted;
    private bool playingDefeat;
    public Vector2 PresentationOffset => presentationOffset;
    public float PresentationRotation => presentationRotation;

    public void SetPresentationOffset(Vector2 offset, float rotation)
    {
        presentationOffset = offset; presentationRotation = rotation;
        ApplyVisualMotion();
    }
    private void ApplyVisualMotion()
    {
        if (visual == null) return;
        visual.anchoredPosition = visualBasePosition + presentationOffset;
        visual.localRotation = Quaternion.Euler(0, 0, presentationRotation);
    }
    public CharacterUIProfile Profile => profile;
    public UISpriteAnimation IdleAnimation => idleAnimation;
    public CharacterInfoUI InfoUI => infoUI;
    public EnemyActionUI ActionUI { get { EnsureInitialized(); return actionUI; } }
    public RectTransform Visual => visual;
    internal bool HasDefeatMotion => defeatAnimation != null && spriteAnimator != null &&
        idleAnimation != null && profile != null;
    internal bool IsPlayingDefeat => isActiveAndEnabled && playingDefeat;

    internal void PlayDefeat()
    {
        if (!isActiveAndEnabled || !HasDefeatMotion || defeatStarted || visual == null ||
            !spriteAnimator.isActiveAndEnabled) return;

        spriteAnimator.Play(defeatAnimation, false, useUnscaledTime: true);
        defeatStarted = true;
        playingDefeat = true;
        if (actionUI != null) actionUI.ClearAction();
        ApplyLayout();
    }

    private void Awake()
    {
        character = GetComponent<BattleCharacter>();
        EnsureInitialized();
    }

    private void OnEnable()
    {
        if (character != null) character.HealthChanged += OnHealthChanged;
    }

    private void LateUpdate()
    {
        if (profile != null && visual != null)
        {
            if (!initialized || appliedProfile != profile || lastLayoutHash != profile.LayoutHash()) ApplyProfile(profile);
            else if (lastRootSize != ((RectTransform)transform).rect.size) ApplyLayout();
        }
        if (playingDefeat && (spriteAnimator == null || !spriteAnimator.isActiveAndEnabled ||
            spriteAnimator.Animation != defeatAnimation || spriteAnimator.IsComplete))
        {
            playingDefeat = false;
            if (visual != null && visual.TryGetComponent(out UnityEngine.UI.Image image)) image.enabled = false;
            if (infoUI != null) infoUI.gameObject.SetActive(false);
        }
        blockGainFeedback?.Update();
        healthChangeFeedback?.Update(healthNumberOrigin);
    }

    internal void PlayBlockGain(int amount)
    {
        if (!isActiveAndEnabled || visual == null || infoUI == null) return;
        var number = infoUI.BlockNumber;
        if (number == null || number.transform.parent == null ||
            !number.transform.parent.TryGetComponent(out UnityEngine.UI.Image badge) || badge.sprite == null) return;

        if (blockGainFeedback == null || !blockGainFeedback.Matches(visual, badge))
        {
            blockGainFeedback?.Dispose();
            blockGainFeedback = new BlockGainFeedback(visual, badge, number);
        }

        Rect bounds = idleAnimation != null ? idleAnimation.contentBounds : new Rect(0f, 0f, 1f, 1f);
        blockGainFeedback.Play(amount, bounds);
    }

    private void OnHealthChanged(int delta)
    {
        if (!Application.isPlaying || !initialized) return;
        healthChangeFeedback ??= new HealthChangeFeedback((RectTransform)transform);
        healthChangeFeedback.Play(delta, healthNumberOrigin);
    }

    private void OnDisable()
    {
        if (character != null) character.HealthChanged -= OnHealthChanged;
        blockGainFeedback?.Hide();
        healthChangeFeedback?.Hide();
    }

    public void Configure(RectTransform artwork, UISpriteAnimator animator, UISpriteAnimation idle, CharacterUIProfile selectedProfile)
    {
        if (artwork == null || animator == null) throw new ArgumentNullException("Artwork and animator are required.");
        if (selectedProfile == null) throw new ArgumentNullException(nameof(selectedProfile));
        selectedProfile.Validate();
        if (idle != null) idle.Validate();
        visual = artwork; spriteAnimator = animator; idleAnimation = idle;
        if (spriteAnimator.Animation != idle) spriteAnimator.SetAnimation(idle);
        ApplyProfile(selectedProfile);
    }

    public void EnsureInitialized()
    {
        if (initialized || profile == null || visual == null || !gameObject.scene.IsValid()) return;
        if (idleAnimation != null && spriteAnimator != null && spriteAnimator.Animation != idleAnimation)
            spriteAnimator.SetAnimation(idleAnimation);
        ApplyProfile(profile);
    }

    public void ApplyProfile(CharacterUIProfile selectedProfile)
    {
        if (selectedProfile == null) throw new ArgumentNullException(nameof(selectedProfile));
        selectedProfile.Validate();
        if (idleAnimation != null) idleAnimation.Validate();
        if (visual == null) throw new InvalidOperationException("A presentation requires its own Visual child.");
        BattleCharacter owner = GetComponent<BattleCharacter>();
        if (owner == null) throw new InvalidOperationException("A presentation must belong to a battle character.");
        character = owner;
        if (selectedProfile.infoPrefab != null && NeedsReplacement(infoUI, appliedInfoPrefab, selectedProfile.infoPrefab))
        {
            CharacterInfoUI old = infoUI;
            infoUI = CreateUI(selectedProfile.infoPrefab);
            infoUI.name = "CharacterInfoUI";
            appliedInfoPrefab = selectedProfile.infoPrefab;
            owner.BindCharacterInfoUI(infoUI);
            infoUI.gameObject.SetActive(!defeatStarted || playingDefeat);
            Remove(old);
        }
        else if (infoUI != null) owner.BindCharacterInfoUI(infoUI);
        Enemy enemy = owner as Enemy;
        if (enemy != null && selectedProfile.actionPrefab != null && NeedsReplacement(actionUI, appliedActionPrefab, selectedProfile.actionPrefab))
        {
            EnemyActionUI old = actionUI;
            actionUI = CreateUI(selectedProfile.actionPrefab);
            actionUI.name = "EnemyActionUI";
            appliedActionPrefab = selectedProfile.actionPrefab;
            actionUI.gameObject.SetActive(true);
            Remove(old);
        }
        profile = selectedProfile;
        appliedProfile = profile;
        initialized = true;
        lastLayoutHash = profile.LayoutHash();
        ImageSettings();
        ApplyLayout();
        if (enemy != null && actionUI != null) enemy.BindActionUI(actionUI);
    }

    private void ImageSettings()
    {
        UnityEngine.UI.Image image = visual.GetComponent<UnityEngine.UI.Image>();
        if (image != null) { image.preserveAspect = true; image.raycastTarget = false; image.type = UnityEngine.UI.Image.Type.Simple; }
    }

    private void ApplyLayout()
    {
        Vector2 pivot = idleAnimation != null ? idleAnimation.groundPivot : new Vector2(0.5f, 0.0625f);
        Vector2 cell = idleAnimation != null && idleAnimation.frames.Length > 0 ? idleAnimation.frames[0].rect.size : Vector2.one;
        float fit = Mathf.Min(profile.visualSize.x / cell.x, profile.visualSize.y / cell.y);
        Vector2 size = cell * fit;
        float contentTop = idleAnimation != null ? idleAnimation.contentBounds.yMax : 1;
        float visibleHeight = Mathf.Max(0.01f, contentTop - pivot.y) * size.y;
        RectTransform root = (RectTransform)transform;
        float intentHeight = actionUI != null ? profile.actionSize.y + profile.actionGap : 0;
        if (root.rect.height > 32)
        {
            float available = Mathf.Max(32, root.rect.height - Mathf.Max(0, profile.visualOffset.y) - intentHeight);
            float reduction = Mathf.Min(1, available / visibleHeight);
            size *= reduction;
            visibleHeight *= reduction;
        }
        if (defeatStarted)
        {
            Sprite idleFrame = idleAnimation.frames[0];
            Sprite defeatFrame = defeatAnimation.frames[0];
            Vector2 framedSize = defeatFrame.rect.size * (idleFrame.pixelsPerUnit / defeatFrame.pixelsPerUnit);
            size = Vector2.Scale(size, new Vector2(framedSize.x / cell.x, framedSize.y / cell.y));
            pivot = defeatAnimation.groundPivot;
        }
        SetRect(visual, pivot, size, profile.visualOffset);
        visualBasePosition = profile.visualOffset;
        float towardCenter = character is Enemy ? -1f : 1f;
        healthNumberOrigin = visualBasePosition + new Vector2(towardCenter * 78f, visibleHeight * .78f);
        ApplyVisualMotion();
        if (infoUI != null) SetRect((RectTransform)infoUI.transform, new Vector2(0.5f, 1), profile.infoSize, profile.infoOffset);
        if (actionUI != null)
            SetRect((RectTransform)actionUI.transform, new Vector2(0.5f, 0), profile.actionSize,
                profile.visualOffset + new Vector2(0, visibleHeight + profile.actionGap));
        lastRootSize = root.rect.size;
    }

    private static void SetRect(RectTransform rect, Vector2 pivot, Vector2 size, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
        rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
        rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
    }

    private static bool NeedsReplacement<T>(T current, T previousSource, T nextSource) where T : Component
    {
        if (current == null || previousSource != nextSource) return true;
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(nextSource))
            return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(current) != nextSource;
#endif
        return false;
    }

    private T CreateUI<T>(T source) where T : Component
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabAsset(source))
            return ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(source.gameObject, transform)).GetComponent<T>();
#endif
        return Instantiate(source, transform, false);
    }

    private static void Remove(Component previous)
    {
        if (previous == null) return;
        previous.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(previous.gameObject); else DestroyImmediate(previous.gameObject);
    }
}
