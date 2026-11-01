using System.Collections.Generic;
using UnityEngine;

public static class GameSettings
{
    private static GameSettingsService service;
    public static GameSettingsService Service => service ??= new GameSettingsService(new UnityGameSettingsPlatform());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { service = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() { Service.ApplyCurrent(); }
}

public sealed class UnityGameSettingsPlatform : IGameSettingsPlatform
{
    private const string Prefix = "ProjectDN.Settings.v1.";
    public GameResolution CurrentResolution => new GameResolution(Screen.width, Screen.height);
    public bool Fullscreen => Screen.fullScreen;
    public IEnumerable<GameResolution> SupportedResolutions
    {
        get
        {
            foreach (Resolution resolution in Screen.resolutions) yield return new GameResolution(resolution.width, resolution.height);
            if (Application.isEditor || Screen.resolutions.Length == 0)
            {
                yield return new GameResolution(1280,720);
                yield return new GameResolution(1600,900);
                yield return new GameResolution(1920,1080);
            }
        }
    }

    public GameSettingsState Load(GameSettingsState defaults) => new GameSettingsState
    {
        masterVolume = PlayerPrefs.GetFloat(Prefix + "MasterVolume", defaults.masterVolume),
        musicVolume = PlayerPrefs.GetFloat(Prefix + "MusicVolume", defaults.musicVolume),
        effectsVolume = PlayerPrefs.GetFloat(Prefix + "EffectsVolume", defaults.effectsVolume),
        resolution = new GameResolution(PlayerPrefs.GetInt(Prefix + "Width", defaults.resolution.width),
            PlayerPrefs.GetInt(Prefix + "Height", defaults.resolution.height)),
        fullscreen = PlayerPrefs.GetInt(Prefix + "Fullscreen", defaults.fullscreen ? 1 : 0) != 0
    };

    public void Save(GameSettingsState state)
    {
        PlayerPrefs.SetFloat(Prefix + "MasterVolume", state.masterVolume);
        PlayerPrefs.SetFloat(Prefix + "MusicVolume", state.musicVolume);
        PlayerPrefs.SetFloat(Prefix + "EffectsVolume", state.effectsVolume);
        PlayerPrefs.SetInt(Prefix + "Width", state.resolution.width);
        PlayerPrefs.SetInt(Prefix + "Height", state.resolution.height);
        PlayerPrefs.SetInt(Prefix + "Fullscreen", state.fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMasterVolume(float volume) { AudioListener.volume = volume; }
    public void SetDisplay(GameResolution resolution, bool fullscreen)
    {
        // A standalone player owns its window; the Editor's Game view is controlled by the Editor.
        if (Application.isEditor) return;
        Screen.SetResolution(resolution.width, resolution.height,
            fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }
    public void Quit() { Application.Quit(); }
}
