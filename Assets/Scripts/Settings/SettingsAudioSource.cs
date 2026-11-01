using UnityEngine;

public enum SettingsAudioBus { Music, Effects }

[RequireComponent(typeof(AudioSource))]
public class SettingsAudioSource : MonoBehaviour
{
    [SerializeField] private SettingsAudioBus bus = SettingsAudioBus.Effects;
    private AudioSource source;
    private float originalVolume;
    private GameSettingsService service;

    private void Awake() { Initialize(); }
    private void Initialize()
    {
        if (source != null) return;
        source = GetComponent<AudioSource>();
        originalVolume = source.volume;
    }

    private void OnEnable() { BindSettings(service ?? GameSettings.Service); }

    public void BindSettings(GameSettingsService settings)
    {
        Initialize();
        if (service != null) service.Changed -= Apply;
        service = settings ?? GameSettings.Service;
        if (isActiveAndEnabled) service.Changed += Apply;
        Apply();
    }

    public void SetBus(SettingsAudioBus value) { bus = value; Apply(); }
    private void Apply()
    {
        if (source == null || service == null) return;
        source.volume = originalVolume * (bus == SettingsAudioBus.Music ? service.Current.musicVolume : service.Current.effectsVolume);
    }

    private void OnDisable()
    {
        if (service != null) service.Changed -= Apply;
        if (source != null) source.volume = originalVolume;
    }
}
