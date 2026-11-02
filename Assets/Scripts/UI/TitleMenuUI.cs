using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour
{
    [Serializable]
    public struct TutorialPage
    {
        public string title;
        [TextArea(4,12)] public string body;
    }

    [SerializeField] private RunDefinition runDefinition;
    [SerializeField] private MapContentType[] supportedContent = Array.Empty<MapContentType>();
    [SerializeField] private CanvasGroup mainMenu;
    [SerializeField] private Button startButton, tutorialButton, optionsButton, quitButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private SettingsUI settingsUI;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TMP_Text tutorialTitle, tutorialBody, pageIndicator;
    [SerializeField] private Button previousButton, nextButton, closeTutorialButton;
    [SerializeField] private TutorialPage[] tutorialPages = Array.Empty<TutorialPage>();
    [SerializeField] private GameObject quitPanel;
    [SerializeField] private Button cancelQuitButton, confirmQuitButton;
    [SerializeField] private TMP_Text statusText;

    private GameSettingsService service;
    private int pageIndex;
    private int blockedUntilFrame = -1;
    private bool starting, quitRequested;
    private bool TutorialOpen => tutorialPanel!=null && tutorialPanel.activeSelf;
    private bool QuitOpen => quitPanel!=null && quitPanel.activeSelf;
    private bool CanActivate => isActiveAndEnabled && !starting && !TutorialOpen && !QuitOpen &&
        (settingsUI==null || !settingsUI.IsOpen) && !SettingsUI.BlocksBattleInput && Time.frameCount>blockedUntilFrame;

    private void Awake()
    {
        tutorialPanel.SetActive(false);
        quitPanel.SetActive(false);
        if(statusText!=null)statusText.text=string.Empty;
    }
    private void Start()
    {
        bool canContinue=RunSaves.HasContinue;
        if(continueButton!=null)continueButton.gameObject.SetActive(canContinue);
        RefreshMenu();ClearSelection();
    }
    public void ContinueGame()
    {
        if(!CanActivate || continueButton==null || !continueButton.gameObject.activeSelf)return;
        starting=true;RefreshMenu();
        RunSession session=RunSession.GetOrCreate();
        if(session.ResumeSavedRun())return;
        starting=false;RefreshMenu();
        if(statusText!=null)statusText.text="저장된 진행을 불러오지 못했습니다. 저장 데이터와 실행 설정을 확인해 주세요.";
        continueButton.gameObject.SetActive(RunSaves.HasContinue);ClearSelection();
    }
    public void BindSettings(GameSettingsService settings)
    {
        service=settings ?? GameSettings.Service;
        if(settingsUI!=null)settingsUI.BindSettings(service);
    }
    public void StartGame()
    {
        if(!CanActivate)return;
        starting=true;RefreshMenu();
        try
        {
            RunSession.GetOrCreate().StartNewRun(runDefinition,Environment.TickCount,supportedContent);
        }
        catch(Exception error) when(error is ArgumentException || error is InvalidOperationException)
        {
            starting=false;RefreshMenu();
            if(statusText!=null)statusText.text="게임을 시작하지 못했습니다. 실행 설정을 확인해 주세요.";
            Debug.LogException(error,this);
            ClearSelection();
        }
    }
    public void OpenTutorial()
    {
        if(!CanActivate || tutorialPages.Length==0)return;
        pageIndex=0;tutorialPanel.SetActive(true);tutorialPanel.transform.SetAsLastSibling();
        RefreshTutorial();RefreshMenu();Select(closeTutorialButton);
    }
    public void PreviousPage()
    {
        if(!TutorialOpen || pageIndex<=0)return;
        pageIndex--;RefreshTutorial();
        if(!previousButton.interactable)Select(nextButton);
    }
    public void NextPage()
    {
        if(!TutorialOpen || pageIndex>=tutorialPages.Length-1)return;
        pageIndex++;RefreshTutorial();
        if(!nextButton.interactable)Select(previousButton);
    }
    private void RefreshTutorial()
    {
        tutorialTitle.text=tutorialPages[pageIndex].title;
        tutorialBody.text=tutorialPages[pageIndex].body;
        pageIndicator.text=$"{pageIndex+1} / {tutorialPages.Length}";
        previousButton.interactable=pageIndex>0;nextButton.interactable=pageIndex<tutorialPages.Length-1;
    }
    public void CloseTutorial()
    {
        if(!TutorialOpen)return;
        tutorialPanel.SetActive(false);FinishModal();
    }
    public void OpenOptions()
    {
        if(!CanActivate || settingsUI==null)return;
        settingsUI.Toggle();RefreshMenu();
    }
    public void RequestQuit()
    {
        if(!CanActivate)return;
        quitRequested=false;confirmQuitButton.interactable=true;
        quitPanel.SetActive(true);quitPanel.transform.SetAsLastSibling();RefreshMenu();Select(cancelQuitButton);
    }
    public void CancelQuit()
    {
        if(!QuitOpen)return;
        quitPanel.SetActive(false);quitRequested=false;FinishModal();
    }
    public void ConfirmQuit()
    {
        if(!isActiveAndEnabled || !QuitOpen || quitRequested)return;
        quitRequested=true;confirmQuitButton.interactable=false;
        (service ?? GameSettings.Service).Quit();
    }
    public void HandleEscape()
    {
        // SettingsUI handles its own frontmost popup and closing frame.
        if(settingsUI!=null && settingsUI.IsOpen)return;
        if(QuitOpen)CancelQuit();else if(TutorialOpen)CloseTutorial();
    }
    private void FinishModal()
    {blockedUntilFrame=Time.frameCount;RefreshMenu();ClearSelection();}
    private void RefreshMenu()
    {if(mainMenu!=null){mainMenu.interactable=CanActivate;mainMenu.blocksRaycasts=CanActivate;}}
    private static void Select(Button button)
    {if(EventSystem.current!=null && button!=null)EventSystem.current.SetSelectedGameObject(button.gameObject);}
    private static void ClearSelection()
    {if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);}
    private void Update() { if(Input.GetKeyDown(KeyCode.Escape))HandleEscape(); }
    private void LateUpdate() { RefreshMenu(); }
}
