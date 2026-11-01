using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class FakeGameSettingsPlatform : IGameSettingsPlatform
{
    public GameResolution CurrentResolution { get; set; } = new GameResolution(1920,1080);
    public bool Fullscreen { get; set; }
    public IEnumerable<GameResolution> SupportedResolutions { get; set; } = new[]
        {new GameResolution(1280,720),new GameResolution(1600,900),new GameResolution(1920,1080)};
    public GameSettingsState? Saved;
    public int Saves;
    public int DisplayChanges;
    public int QuitRequests;
    public float Master;
    public GameSettingsState Load(GameSettingsState defaults) => Saved ?? defaults;
    public void Save(GameSettingsState state) { Saved=state; Saves++; }
    public void SetMasterVolume(float volume) { Master=volume; }
    public void SetDisplay(GameResolution resolution,bool fullscreen) { CurrentResolution=resolution; Fullscreen=fullscreen; DisplayChanges++; }
    public void Quit() { QuitRequests++; }
}

public class GameSettingsTests
{
    [Test]
    public void LoadingClampsVolumesAndRejectsUnsupportedSavedResolutionWithoutWritingPreferences()
    {
        var platform=new FakeGameSettingsPlatform { Saved=new GameSettingsState
        {masterVolume=2,musicVolume=-1,effectsVolume=float.NaN,resolution=new GameResolution(9999,9999),fullscreen=true} };
        var service=new GameSettingsService(platform);
        Assert.That(service.Current.masterVolume,Is.EqualTo(1));
        Assert.That(service.Current.musicVolume,Is.Zero);
        Assert.That(service.Current.effectsVolume,Is.EqualTo(1));
        Assert.That(service.Current.resolution,Is.EqualTo(new GameResolution(1920,1080)));
        Assert.That(service.Current.fullscreen,Is.True);
        Assert.That(platform.Saves,Is.Zero);
        Assert.That(platform.DisplayChanges,Is.Zero);
    }

    [Test]
    public void SupportedSizesDeduplicateRefreshRatesAndKeepTheCurrentWindowSize()
    {
        var platform=new FakeGameSettingsPlatform {CurrentResolution=new GameResolution(1440,900),SupportedResolutions=new[]
        {new GameResolution(1920,1080),new GameResolution(1280,720),new GameResolution(1920,1080),new GameResolution(0,0)}};
        var service=new GameSettingsService(platform);
        CollectionAssert.AreEqual(new[] {new GameResolution(1280,720),new GameResolution(1440,900),new GameResolution(1920,1080)},service.Resolutions);
    }

    [Test]
    public void VolumeAndVideoSettingsPersistAndReloadIndependently()
    {
        var platform=new FakeGameSettingsPlatform();
        var service=new GameSettingsService(platform);
        service.SetVolumes(.4f,.3f,.2f);
        Assert.That(platform.Master,Is.EqualTo(.4f));
        Assert.That(platform.DisplayChanges,Is.Zero);
        service.SetDisplay(new GameResolution(1280,720),true);
        Assert.That(platform.DisplayChanges,Is.EqualTo(1));
        Assert.That(platform.Fullscreen,Is.True);
        var reloaded=new GameSettingsService(platform);
        Assert.That(reloaded.Current.masterVolume,Is.EqualTo(.4f));
        Assert.That(reloaded.Current.musicVolume,Is.EqualTo(.3f));
        Assert.That(reloaded.Current.effectsVolume,Is.EqualTo(.2f));
        Assert.That(reloaded.Current.resolution,Is.EqualTo(new GameResolution(1280,720)));
        Assert.That(reloaded.Current.fullscreen,Is.True);
        Assert.That(platform.Saves,Is.EqualTo(2));
    }

    [Test]
    public void InvalidVideoChoiceHasNoSideEffectsAndResetRestoresStartupDefaults()
    {
        var platform=new FakeGameSettingsPlatform();
        var service=new GameSettingsService(platform);
        Assert.Throws<ArgumentOutOfRangeException>(()=>service.SetDisplay(new GameResolution(999,777),true));
        Assert.That(platform.Saves,Is.Zero);
        Assert.That(platform.DisplayChanges,Is.Zero);
        service.SetVolumes(.1f,.2f,.3f);
        service.SetDisplay(new GameResolution(1280,720),true);
        service.ResetDefaults();
        Assert.That(service.Current.masterVolume,Is.EqualTo(1));
        Assert.That(service.Current.musicVolume,Is.EqualTo(1));
        Assert.That(service.Current.effectsVolume,Is.EqualTo(1));
        Assert.That(service.Current.resolution,Is.EqualTo(new GameResolution(1920,1080)));
        Assert.That(service.Current.fullscreen,Is.False);
    }

    [Test]
    public void QuitSavesCurrentValuesAndUsesThePlatformRequest()
    {
        var platform=new FakeGameSettingsPlatform();
        var service=new GameSettingsService(platform);
        service.Quit();
        Assert.That(platform.Saves,Is.EqualTo(1));
        Assert.That(platform.QuitRequests,Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator CategoryAudioPreservesOriginalGainAndDoesNotCompoundOnChangesOrReenable()
    {
        yield return new EnterPlayMode();
        yield return VerifyAudio();
        yield return new ExitPlayMode();
    }
    private static IEnumerator VerifyAudio()
    {
        var platform=new FakeGameSettingsPlatform();
        var service=new GameSettingsService(platform);
        var root=new GameObject("Settings category audio");
        AudioSource source=root.AddComponent<AudioSource>();source.volume=.8f;
        SettingsAudioSource routing=root.AddComponent<SettingsAudioSource>();routing.BindSettings(service);
        routing.SetBus(SettingsAudioBus.Music);
        service.SetVolumes(.5f,.25f,.4f);
        Assert.That(source.volume,Is.EqualTo(.2f).Within(.0001f));
        service.SetVolumes(.5f,.25f,.4f);
        Assert.That(source.volume,Is.EqualTo(.2f).Within(.0001f));
        routing.SetBus(SettingsAudioBus.Effects);
        Assert.That(source.volume,Is.EqualTo(.32f).Within(.0001f));
        routing.enabled=false;
        Assert.That(source.volume,Is.EqualTo(.8f).Within(.0001f));
        routing.enabled=true;
        Assert.That(source.volume,Is.EqualTo(.32f).Within(.0001f));
        UnityEngine.Object.Destroy(root);
        yield return null;
        AudioClip clip=AudioClip.Create("Settings silent test",512,1,44100,false);
        using(var context=new CardPresentationContext(null))
        {
            AudioSource presentation=context.Sound(clip,.6f);
            Assert.That(presentation.GetComponent<SettingsAudioSource>(),Is.Not.Null,"Card sounds must use the effects bus.");
            presentation.GetComponent<SettingsAudioSource>().BindSettings(service);
            Assert.That(presentation.volume,Is.EqualTo(.24f).Within(.0001f));
        }
        UnityEngine.Object.Destroy(clip);
        yield return null;
    }
}
