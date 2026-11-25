using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public sealed class UIButtonSound : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    private static AudioSource source;
    private Button button;

    private void Awake() { button = GetComponent<Button>(); }

    private void OnEnable() { button.onClick.AddListener(PlayClick); }
    private void OnDisable() { button.onClick.RemoveListener(PlayClick); }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isActiveAndEnabled && button.isActiveAndEnabled && button.IsInteractable())
            Play(hoverClip);
    }

    private void PlayClick() { Play(clickClip); }

    private static void Play(AudioClip clip)
    {
        if (!Application.isPlaying || clip == null) return;
        if (source == null)
        {
            // Let a click finish after its button closes a panel or changes scenes.
            var player = new GameObject("UI Button Audio", typeof(AudioSource), typeof(SettingsAudioSource));
            DontDestroyOnLoad(player);
            source = player.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.PlayOneShot(clip);
    }
}
