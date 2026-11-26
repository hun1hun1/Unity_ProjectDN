using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

// A new context belongs to one use. Shared definition assets never own playback state.
public sealed class CardPresentationContext : IDisposable
{
    private readonly BattleManager battle;
    private readonly BattleCharacter player;
    private readonly BattleCharacter enemy;
    private readonly Dictionary<BattleCharacterPresentation, Vector3> motion = new Dictionary<BattleCharacterPresentation, Vector3>();
    private readonly Dictionary<UISpriteAnimator, UISpriteAnimator.PlaybackState> playback = new Dictionary<UISpriteAnimator, UISpriteAnimator.PlaybackState>();
    private readonly List<GameObject> temporary = new List<GameObject>();
    private bool disposed;
    private readonly BattleCharacter actor;
    private readonly bool hadActor;
    public bool CanContinue => !disposed && battle != null && battle.CanContinueEffects &&
        (player == null || player.isActiveAndEnabled) && (!hadActor || actor != null && actor.isActiveAndEnabled);
    public CardPresentationContext(BattleManager owner, PresentationTarget actingTarget = PresentationTarget.Player)
    {
        battle = owner;
        player = owner != null ? owner.GetPlayer() : null;
        enemy = owner != null ? owner.GetTarget() : null;
        actor = actingTarget == PresentationTarget.Player ? player : enemy;
        hadActor = actor != null;
    }
    public BattleCharacterPresentation View(PresentationTarget target)
    {
        if (battle == null) return null;
        BattleCharacter character = target == PresentationTarget.Player ? player : enemy;
        return character != null ? character.GetComponent<BattleCharacterPresentation>() : null;
    }
    public RectTransform Visual(PresentationTarget target) => View(target)?.Visual;
    public void Move(BattleCharacterPresentation view, Vector2 offset, float rotation)
    {
        if (view == null || view.Visual == null) return;
        if (!motion.ContainsKey(view))
            motion.Add(view, new Vector3(view.PresentationOffset.x, view.PresentationOffset.y, view.PresentationRotation));
        Vector3 origin = motion[view];
        view.SetPresentationOffset((Vector2)origin + offset, origin.z + rotation);
    }
    public Vector2 Offset(BattleCharacterPresentation view)
    {
        if (view == null) return Vector2.zero;
        return view.PresentationOffset - (motion.TryGetValue(view, out Vector3 origin) ? (Vector2)origin : view.PresentationOffset);
    }
    public float Rotation(BattleCharacterPresentation view)
    {
        if (view == null) return 0;
        return view.PresentationRotation - (motion.TryGetValue(view, out Vector3 origin) ? origin.z : view.PresentationRotation);
    }
    public void Play(UISpriteAnimator animator, UISpriteAnimation animation)
    {
        if (animator == null || animation == null) return;
        if (!playback.ContainsKey(animator)) playback.Add(animator, animator.CaptureState());
        animator.Play(animation, false);
    }
    public IEnumerator Wait(float duration)
    {
        float elapsed = 0;
        while (CanContinue && elapsed < duration) { yield return null; elapsed += Time.deltaTime; }
    }
    public GameObject Spawn(GameObject prefab, Transform parent)
    {
        if (prefab == null) return null;
        GameObject item = Object.Instantiate(prefab, parent, false); temporary.Add(item);
        foreach (UnityEngine.UI.Graphic graphic in item.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            graphic.raycastTarget = false;
        return item;
    }
    public AudioSource Sound(AudioClip clip, float volume)
    {
        if (clip == null) return null;
        GameObject item = new GameObject("Card presentation sound"); temporary.Add(item);
        AudioSource source = item.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.volume = volume; source.clip = clip;
        item.AddComponent<SettingsAudioSource>();
        source.Play();
        return source;
    }
    internal void PlaySoundToEnd(AudioClip clip, float volume)
    {
        if (battle != null) battle.PlayPresentationOneShot(clip, volume);
    }

    public void Release(GameObject item)
    {
        if (item == null) return;
        temporary.Remove(item); Destroy(item);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        foreach (var entry in motion)
            if (entry.Key != null) entry.Key.SetPresentationOffset((Vector2)entry.Value, entry.Value.z);
        foreach (var entry in playback) if (entry.Key != null) entry.Key.RestoreState(entry.Value);
        foreach (GameObject item in temporary) if (item != null) Destroy(item);
        temporary.Clear();
    }
    private static void Destroy(GameObject item)
    {
        if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
    }
}
