using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class SettingsUI : MonoBehaviour
{
    [SerializeField] private BattleManager battle;
    [SerializeField] private bool requireBattle = true;
    [SerializeField] private bool returnToTitle;
    [SerializeField] private TMP_Text navigationError;
    [SerializeField] private DeckOverviewUI deckOverview;
    [SerializeField] private DeckListUI pileList;
    [SerializeField] private GameObject panel;
    [SerializeField] private CanvasGroup mainControls;
    [SerializeField] private UnityEngine.UI.Button settingButton;
    [SerializeField] private UnityEngine.UI.Button closeButton;
    [SerializeField] private UnityEngine.UI.Button returnButton;
    [SerializeField] private UnityEngine.UI.Button resetButton;
    [SerializeField] private UnityEngine.UI.Button quitButton;
    [SerializeField] private UnityEngine.UI.Button applyDisplayButton;
    [SerializeField] private UnityEngine.UI.Slider masterSlider;
    [SerializeField] private UnityEngine.UI.Slider musicSlider;
    [SerializeField] private UnityEngine.UI.Slider effectsSlider;
    [SerializeField] private TMP_Text masterValue;
    [SerializeField] private TMP_Text musicValue;
    [SerializeField] private TMP_Text effectsValue;
    [SerializeField] private UnityEngine.UI.Toggle fullscreenToggle;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private GameObject quitConfirmation;
    [SerializeField] private UnityEngine.UI.Button cancelQuitButton;
    [SerializeField] private UnityEngine.UI.Button confirmQuitButton;

    private static SettingsUI instance;
    private static int blockedUntilFrame=-1;
    private GameSettingsService service;
    private bool opened;
    private bool dropdownWasExpanded;
    private GameObject previousSelection;

    private bool CanChangeSettings => IsOpen && (quitConfirmation==null || !quitConfirmation.activeSelf);
    public bool IsOpen => isActiveAndEnabled && panel!=null && panel.activeInHierarchy;
    public static bool BlocksBattleInput => (Application.isPlaying && Time.frameCount<=blockedUntilFrame) || (instance!=null && instance.IsOpen);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance=null;blockedUntilFrame=-1; }
    private void Awake() { if(panel!=null)panel.SetActive(false);if(quitConfirmation!=null)quitConfirmation.SetActive(false); }
    private void OnEnable()
    {
        instance=this;
        BindSettings(service ?? GameSettings.Service);
        if(battle!=null)battle.BattleEnded+=OnBattleEnded;
        if(masterSlider!=null)masterSlider.onValueChanged.AddListener(OnVolumeChanged);
        if(musicSlider!=null)musicSlider.onValueChanged.AddListener(OnVolumeChanged);
        if(effectsSlider!=null)effectsSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    public void BindSettings(GameSettingsService settings)
    {
        if(service!=null)service.Changed-=RefreshAudio;
        service=settings ?? GameSettings.Service;
        if(isActiveAndEnabled)service.Changed+=RefreshAudio;
        RefreshAll();
    }

    public void Toggle()
    {
        if(!isActiveAndEnabled || (deckOverview != null && deckOverview.IsChoosingUpgrade))return;
        if(IsOpen){HandleEscape();return;}
        if(CardInspectionUI.BlocksGameplayInput || Time.frameCount<=blockedUntilFrame || panel==null ||
            (RunSession.Instance!=null && RunSession.Instance.IsTransitioning) ||
            (requireBattle && (battle==null || (!battle.CanInspectCards && !CanOpenAfterBattle))))return;
        if(deckOverview!=null)deckOverview.Close();
        if(pileList!=null)pileList.Close();
        foreach(CardHoverUI hover in FindObjectsByType<CardHoverUI>(FindObjectsSortMode.None))hover.ResetHover();
        foreach(DeckCardHoverUI hover in FindObjectsByType<DeckCardHoverUI>(FindObjectsSortMode.None))hover.ResetHover();
        foreach(DeckPileHoverUI hover in FindObjectsByType<DeckPileHoverUI>(FindObjectsSortMode.None))hover.ResetHover();
        previousSelection=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        panel.SetActive(true);panel.transform.SetAsLastSibling();opened=true;
        if(navigationError!=null)navigationError.text="";
        RefreshAll();
        if(EventSystem.current!=null && closeButton!=null)EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
    }

    private void OnVolumeChanged(float value)
    {
        if(CanChangeSettings && service!=null && masterSlider!=null && musicSlider!=null && effectsSlider!=null)
            service.SetVolumes(masterSlider.value,musicSlider.value,effectsSlider.value);
    }
    private void RefreshAudio()
    {
        if(service==null)return;
        GameSettingsState state=service.Current;
        if(masterSlider!=null)masterSlider.SetValueWithoutNotify(state.masterVolume);
        if(musicSlider!=null)musicSlider.SetValueWithoutNotify(state.musicVolume);
        if(effectsSlider!=null)effectsSlider.SetValueWithoutNotify(state.effectsVolume);
        if(masterValue!=null)masterValue.text=$"{Mathf.RoundToInt(state.masterVolume*100)}%";
        if(musicValue!=null)musicValue.text=$"{Mathf.RoundToInt(state.musicVolume*100)}%";
        if(effectsValue!=null)effectsValue.text=$"{Mathf.RoundToInt(state.effectsVolume*100)}%";
    }
    private void RefreshAll()
    {
        RefreshAudio();
        if(service==null || resolutionDropdown==null || fullscreenToggle==null)return;
        var options=new System.Collections.Generic.List<string>();int selected=0;
        for(int i=0;i<service.Resolutions.Count;i++)
        {
            options.Add(service.Resolutions[i].ToString());
            if(service.Resolutions[i].Equals(service.Current.resolution))selected=i;
        }
        resolutionDropdown.ClearOptions();resolutionDropdown.AddOptions(options);
        resolutionDropdown.SetValueWithoutNotify(selected);resolutionDropdown.RefreshShownValue();
        fullscreenToggle.SetIsOnWithoutNotify(service.Current.fullscreen);
    }
    public void ApplyDisplay()
    {
        if(!CanChangeSettings || service==null || resolutionDropdown==null || fullscreenToggle==null)return;
        int index=Mathf.Clamp(resolutionDropdown.value,0,service.Resolutions.Count-1);
        service.SetDisplay(service.Resolutions[index],fullscreenToggle.isOn);RefreshAll();
    }
    public void ResetDefaults() { if(!CanChangeSettings || service==null)return;service.ResetDefaults();RefreshAll(); }

    public void RequestQuit()
    {
        if(!IsOpen || quitConfirmation==null)return;
        quitConfirmation.SetActive(true);quitConfirmation.transform.SetAsLastSibling();
        if(mainControls!=null){mainControls.interactable=false;mainControls.blocksRaycasts=false;}
        if(EventSystem.current!=null && cancelQuitButton!=null)EventSystem.current.SetSelectedGameObject(cancelQuitButton.gameObject);
    }
    public void CancelQuit()
    {
        if(quitConfirmation!=null)quitConfirmation.SetActive(false);
        if(mainControls!=null){mainControls.interactable=true;mainControls.blocksRaycasts=true;}
        if(IsOpen && EventSystem.current!=null && quitButton!=null)EventSystem.current.SetSelectedGameObject(quitButton.gameObject);
    }
    public void ConfirmQuit()
    {
        if(!IsOpen || quitConfirmation==null || !quitConfirmation.activeSelf)return;
        if(!returnToTitle){service?.Quit();return;}
        RunSession session=RunSession.GetOrCreate();
        if(session.ReturnToTitle())Close();
        else if(navigationError!=null)navigationError.text="저장 또는 이동에 실패했습니다. 다시 시도해 주세요.";
    }
    private bool CanOpenAfterBattle => returnToTitle && battle!=null && battle.Result!=BattleResult.None &&
        RunSession.Instance!=null && RunSession.Instance.Progress!=null &&
        (RunSession.Instance.Progress.Phase==RunPhase.Rewards || (RunSession.Instance.Progress.IsFinished && !string.IsNullOrEmpty(RunSession.Instance.SaveError)));
    public void HandleEscape()
    {
        if(!IsOpen)return;
        if(quitConfirmation!=null && quitConfirmation.activeSelf){CancelQuit();return;}
        if(resolutionDropdown!=null && (resolutionDropdown.IsExpanded || dropdownWasExpanded))
        {resolutionDropdown.Hide();dropdownWasExpanded=false;return;}
        Close();
    }
    public void Close() { ForceClose(); }
    private void ForceClose()
    {
        bool wasOpen=opened;
        if(opened)blockedUntilFrame=Time.frameCount;
        opened=false;dropdownWasExpanded=false;
        if(resolutionDropdown!=null)resolutionDropdown.Hide();
        if(quitConfirmation!=null)quitConfirmation.SetActive(false);
        if(mainControls!=null){mainControls.interactable=true;mainControls.blocksRaycasts=true;}
        if(panel!=null)panel.SetActive(false);
        if(wasOpen && !requireBattle && EventSystem.current!=null)
            EventSystem.current.SetSelectedGameObject(null);
        else if(previousSelection!=null && previousSelection.activeInHierarchy && EventSystem.current!=null)
            EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection=null;
    }
    private void Update() { if(IsOpen && Input.GetKeyDown(KeyCode.Escape))HandleEscape(); }
    private void LateUpdate()
    {
        if(opened && (!IsOpen || (requireBattle && (battle==null || !battle.isActiveAndEnabled || (!battle.CanContinueEffects && !CanOpenAfterBattle)))))ForceClose();
        dropdownWasExpanded=IsOpen && resolutionDropdown!=null && resolutionDropdown.IsExpanded;
    }
    private void OnBattleEnded(BattleResult result) { ForceClose(); }
    private void OnDisable()
    {
        if(battle!=null)battle.BattleEnded-=OnBattleEnded;
        if(service!=null)service.Changed-=RefreshAudio;
        if(masterSlider!=null)masterSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if(musicSlider!=null)musicSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if(effectsSlider!=null)effectsSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        ForceClose();if(instance==this)instance=null;
    }
}
