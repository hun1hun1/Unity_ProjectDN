using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct GameResolution : IEquatable<GameResolution>
{
    public int width;
    public int height;
    public GameResolution(int width, int height) { this.width = width; this.height = height; }
    public bool IsValid => width >= 320 && height >= 200 && width <= 16384 && height <= 16384;
    public bool Equals(GameResolution other) => width == other.width && height == other.height;
    public override bool Equals(object obj) => obj is GameResolution other && Equals(other);
    public override int GetHashCode() => unchecked(width * 397 ^ height);
    public override string ToString() => $"{width} × {height}";
}

[Serializable]
public struct GameSettingsState
{
    public float masterVolume;
    public float musicVolume;
    public float effectsVolume;
    public GameResolution resolution;
    public bool fullscreen;
}

public interface IGameSettingsPlatform
{
    GameResolution CurrentResolution { get; }
    bool Fullscreen { get; }
    IEnumerable<GameResolution> SupportedResolutions { get; }
    GameSettingsState Load(GameSettingsState defaults);
    void Save(GameSettingsState state);
    void SetMasterVolume(float volume);
    void SetDisplay(GameResolution resolution, bool fullscreen);
    void Quit();
}

public sealed class GameSettingsService
{
    private readonly IGameSettingsPlatform platform;
    private readonly List<GameResolution> resolutions = new List<GameResolution>();
    public GameSettingsState Current { get; private set; }
    public GameSettingsState Defaults { get; }
    public IReadOnlyList<GameResolution> Resolutions { get; }
    public event Action Changed;

    public GameSettingsService(IGameSettingsPlatform platform)
    {
        this.platform = platform ?? throw new ArgumentNullException(nameof(platform));
        GameResolution current = platform.CurrentResolution;
        if (!current.IsValid) current = new GameResolution(1280, 720);
        if (platform.SupportedResolutions != null)
            foreach (GameResolution resolution in platform.SupportedResolutions)
                if (resolution.IsValid && !resolutions.Contains(resolution)) resolutions.Add(resolution);
        if (!resolutions.Contains(current)) resolutions.Add(current);
        resolutions.Sort((left, right) => left.width != right.width
            ? left.width.CompareTo(right.width) : left.height.CompareTo(right.height));
        Resolutions = resolutions.AsReadOnly();
        Defaults = new GameSettingsState { masterVolume = 1f, musicVolume = 1f, effectsVolume = 1f,
            resolution = current, fullscreen = platform.Fullscreen };
        GameSettingsState saved = platform.Load(Defaults);
        saved.masterVolume = Clamp(saved.masterVolume);
        saved.musicVolume = Clamp(saved.musicVolume);
        saved.effectsVolume = Clamp(saved.effectsVolume);
        if (!resolutions.Contains(saved.resolution)) saved.resolution = Defaults.resolution;
        Current = saved;
    }

    private static float Clamp(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);

    public void SetVolumes(float master, float music, float effects)
    {
        GameSettingsState value = Current;
        value.masterVolume = Clamp(master);
        value.musicVolume = Clamp(music);
        value.effectsVolume = Clamp(effects);
        Current = value;
        platform.SetMasterVolume(value.masterVolume);
        platform.Save(value);
        Changed?.Invoke();
    }

    public void SetDisplay(GameResolution resolution, bool fullscreen)
    {
        if (!resolutions.Contains(resolution)) throw new ArgumentOutOfRangeException(nameof(resolution));
        GameSettingsState value = Current;
        value.resolution = resolution;
        value.fullscreen = fullscreen;
        Current = value;
        platform.SetDisplay(resolution, fullscreen);
        platform.Save(value);
        Changed?.Invoke();
    }

    public void ApplyCurrent()
    {
        platform.SetMasterVolume(Current.masterVolume);
        platform.SetDisplay(Current.resolution, Current.fullscreen);
        Changed?.Invoke();
    }

    public void ResetDefaults()
    {
        Current = Defaults;
        ApplyCurrent();
        platform.Save(Current);
    }

    public void Quit() { platform.Save(Current); platform.Quit(); }
}
