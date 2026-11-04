using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TitleSceneAssetSetup
{
    private const string Path="Assets/Scenes/TitleScene.unity";
    private static TMP_FontAsset Font=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Bold SDF.asset");

    [MenuItem("ProjectDN/Setup Title Scene")]
    public static void CreateAssets()
    {
        if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Path)!=null)
        {Debug.Log("TitleScene already exists; preserving its authored contents.");return;}
        Scene original=SceneManager.GetActiveScene();
        Scene map=SceneManager.GetSceneByPath("Assets/Scenes/MapScene.unity");bool openedMap=!map.isLoaded;
        if(openedMap)map=EditorSceneManager.OpenScene("Assets/Scenes/MapScene.unity",OpenSceneMode.Additive);
        // A clean untitled bootstrap scene prevents Editor additive scene creation.
        // Never discard an authored scene with unsaved changes.
        if(original.IsValid() && string.IsNullOrEmpty(original.path) && !original.isDirty)
            EditorSceneManager.CloseScene(original,true);
        Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            MapController source=map.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MapController>(true)).Single();
            var sourceState=new SerializedObject(source);
            var handlers=sourceState.FindProperty("contentHandlers");
            var supported=new System.Collections.Generic.HashSet<MapContentType>();
            for(int i=0;i<handlers.arraySize;i++)
            {
                MapContentHandler handler=handlers.GetArrayElementAtIndex(i).objectReferenceValue as MapContentHandler;
                if(handler!=null&&handler.isActiveAndEnabled)supported.Add(handler.ContentType);
            }
            Camera camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
            camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.03f);camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
            RectTransform canvas=Rect(null,"Canvas",Vector2.zero,Vector2.one);
            Canvas layer=canvas.gameObject.AddComponent<Canvas>();layer.renderMode=RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            TitleMenuUI menu=Rect(canvas,"TitleMenu",Vector2.zero,Vector2.one).gameObject.AddComponent<TitleMenuUI>();
            CanvasGroup main=Rect(menu.transform,"MainMenu",Vector2.zero,Vector2.one).gameObject.AddComponent<CanvasGroup>();
            Button start=Button(main.transform,"StartButton","게임 시작",new Vector2(.5f,.5f),new Vector2(0,24),new Vector2(380,68));
            Button tutorial=Button(main.transform,"TutorialButton","튜토리얼",new Vector2(.5f,.5f),new Vector2(0,-64),new Vector2(380,68));
            Button options=Button(main.transform,"OptionsButton","옵션",new Vector2(.5f,.5f),new Vector2(0,-152),new Vector2(380,68));
            Button quit=Button(main.transform,"QuitButton","게임 종료",new Vector2(.5f,.5f),new Vector2(0,-240),new Vector2(380,68));
            TMP_Text status=Label(main.transform,"Status",string.Empty,24,new Vector2(.15f,.12f),new Vector2(.85f,.2f));
            Bind(menu,"mainMenu",main);Bind(menu,"runDefinition",source.Definition);Bind(menu,"statusText",status);
            var state=new SerializedObject(menu);var types=state.FindProperty("supportedContent");types.arraySize=supported.Count;int n=0;
            foreach(var type in supported.OrderBy(t=>t))types.GetArrayElementAtIndex(n++).intValue=(int)type;
            state.ApplyModifiedPropertiesWithoutUndo();
            Connect(menu,"startButton",start,menu.StartGame);Connect(menu,"tutorialButton",tutorial,menu.OpenTutorial);
            Connect(menu,"optionsButton",options,menu.OpenOptions);Connect(menu,"quitButton",quit,menu.RequestQuit);
            SettingsUI settings=SettingsAssetSetup.CreateController(canvas,null,options,false);Bind(menu,"settingsUI",settings);
            GameObject settingsPanel=(GameObject)new SerializedObject(settings).FindProperty("panel").objectReferenceValue;
            RectTransform settingsRect=(RectTransform)settingsPanel.transform;settingsRect.anchorMax=Vector2.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(settingsRect);
            TMP_Text returnLabel=settingsPanel.transform.Find("MainContent/Footer/ReturnButton/Text").GetComponent<TMP_Text>();
            returnLabel.text="타이틀로 돌아가기";PrefabUtility.RecordPrefabInstancePropertyModifications(returnLabel);
            CreateTutorial(menu,canvas);
            CreateQuit(menu,canvas);
            ConnectCharacterSelection(menu, canvas);
            EditorSceneManager.SaveScene(scene,Path);
            var entries=EditorBuildSettings.scenes.Where(s=>s.path!=Path).ToList();entries.Insert(0,new EditorBuildSettingsScene(Path,true));EditorBuildSettings.scenes=entries.ToArray();
            AssetDatabase.SaveAssets();Debug.Log("TitleScene created with Start, Tutorial, shared Options and confirmed Quit.");
        }
        finally
        {
            if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene,true);if(openedMap)EditorSceneManager.CloseScene(map,true);
        }
    }

    internal static void ConnectCharacterSelection(TitleMenuUI menu, Transform canvas)
    {
        const string panelPath = "Assets/Prefabs/UI/CharacterSelectionPanel.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(panelPath);
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
        var selection = panel.GetComponent<CharacterSelectionUI>();
        Bind(menu, "characterSelection", selection);
        var state = new SerializedObject(selection);
        var back = (Button)state.FindProperty("backButton").objectReferenceValue;
        var adventure = (Button)state.FindProperty("adventureButton").objectReferenceValue;
        UnityEventTools.AddPersistentListener(back.onClick, menu.CloseCharacterSelection);
        UnityEventTools.AddPersistentListener(adventure.onClick, menu.ConfirmAdventure);
        PrefabUtility.RecordPrefabInstancePropertyModifications(back);
        PrefabUtility.RecordPrefabInstancePropertyModifications(adventure);
    }

    private static void CreateTutorial(TitleMenuUI menu,Transform canvas)
    {
        RectTransform root=Rect(canvas,"TutorialPanel",Vector2.zero,Vector2.one);Fill(root,new Color(.025f,.035f,.03f,.99f));
        RectTransform content=Rect(root,"Content",new Vector2(.15f,.18f),new Vector2(.85f,.82f));Fill(content,new Color(.06f,.075f,.055f));Border(content);
        Bind(menu,"tutorialPanel",root.gameObject);
        Bind(menu,"tutorialTitle",Label(content,"Title","",38,new Vector2(.06f,.76f),new Vector2(.94f,.94f)));
        TMP_Text body=Label(content,"Body","",28,new Vector2(.08f,.28f),new Vector2(.92f,.74f));body.alignment=TextAlignmentOptions.MidlineLeft;body.lineSpacing=16;
        Bind(menu,"tutorialBody",body);
        Bind(menu,"pageIndicator",Label(content,"PageIndicator","1 / 4",24,new Vector2(.4f,.14f),new Vector2(.6f,.24f)));
        Connect(menu,"previousButton",Button(content,"PreviousButton","이전",new Vector2(.22f,.13f),Vector2.zero,new Vector2(220,60)),menu.PreviousPage);
        Connect(menu,"nextButton",Button(content,"NextButton","다음",new Vector2(.78f,.13f),Vector2.zero,new Vector2(220,60)),menu.NextPage);
        Connect(menu,"closeTutorialButton",Button(root,"CloseButton","닫기",new Vector2(.93f,.94f),Vector2.zero,new Vector2(140,56)),menu.CloseTutorial);
        string[] titles={"꿈과 악몽","카드 사용과 턴","탄약과 카드 효과","덱 확인과 진행"};
        string[] bodies={
            "꿈과 악몽은 각각의 덱과 손패를 사용합니다.\n카드를 사용할 때는 해당 덱의 코스트와 카드 설명을 확인하세요.",
            "사용할 카드를 선택하고 필요한 대상을 지정합니다.\n공격과 방어를 조합하고, 행동을 마쳤다면 턴 종료 버튼을 누르세요.\n적의 행동 예고를 확인하며 다음 턴을 준비하세요.",
            "일부 카드는 탄약이 필요하거나 탄약을 소모합니다.\n현재 탄약과 카드에 표시된 조건을 확인하세요.\n상태 효과와 지속 효과는 카드 설명과 툴팁에서 확인할 수 있습니다.",
            "상단의 덱 버튼으로 꿈과 악몽의 전체 보유 카드를 확인합니다.\n뽑을 카드 더미와 버린 카드 더미도 눌러 목록을 볼 수 있습니다.\n전투 후 보상을 받고 맵에서 다음 행동을 선택하세요."};
        var state=new SerializedObject(menu);var pages=state.FindProperty("tutorialPages");pages.arraySize=4;
        for(int i=0;i<4;i++){pages.GetArrayElementAtIndex(i).FindPropertyRelative("title").stringValue=titles[i];pages.GetArrayElementAtIndex(i).FindPropertyRelative("body").stringValue=bodies[i];}
        state.ApplyModifiedPropertiesWithoutUndo();root.gameObject.SetActive(false);
    }
    private static void CreateQuit(TitleMenuUI menu,Transform canvas)
    {
        RectTransform root=Rect(canvas,"TitleQuitPanel",Vector2.zero,Vector2.one);Fill(root,new Color(0,0,0,.75f));
        RectTransform dialog=Rect(root,"Dialog",Vector2.one*.5f,Vector2.one*.5f);dialog.sizeDelta=new Vector2(680,340);Fill(dialog,new Color(.08f,.095f,.07f));Border(dialog);
        Label(dialog,"Title","게임 종료",32,new Vector2(0,.72f),Vector2.one);
        Label(dialog,"Message","게임을 종료하시겠습니까?",28,new Vector2(0,.36f),new Vector2(1,.72f));
        Connect(menu,"cancelQuitButton",Button(dialog,"CancelButton","취소",new Vector2(.28f,.2f),Vector2.zero,new Vector2(220,60)),menu.CancelQuit);
        Connect(menu,"confirmQuitButton",Button(dialog,"ConfirmButton","종료",new Vector2(.72f,.2f),Vector2.zero,new Vector2(220,60)),menu.ConfirmQuit);
        Bind(menu,"quitPanel",root.gameObject);root.gameObject.SetActive(false);
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.gameObject.layer=5;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.sizeDelta=Vector2.zero;return rect;
    }
    private static Image Fill(RectTransform rect,Color color)
    {Image image=rect.gameObject.AddComponent<Image>();image.color=color;return image;}
    private static void Border(RectTransform rect)
    {var outline=rect.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.65f,.52f,.3f);outline.effectDistance=new Vector2(1,-1);}
    private static TMP_Text Label(Transform parent,string name,string text,int size,Vector2 min,Vector2 max)
    {
        var label=Rect(parent,name,min,max).gameObject.AddComponent<TextMeshProUGUI>();label.font=Font;label.fontSize=size;label.text=text;label.color=new Color(.95f,.92f,.82f);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;return label;
    }
    private static Button Button(Transform parent,string name,string text,Vector2 anchor,Vector2 position,Vector2 size)
    {
        RectTransform rect=Rect(parent,name,anchor,anchor);rect.sizeDelta=size;rect.anchoredPosition=position;
        Image image=Fill(rect,new Color(.16f,.19f,.13f));Border(rect);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;
        ColorBlock colors=button.colors;colors.highlightedColor=new Color(1.35f,1.3f,1.12f);colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.8f,.8f,.7f);button.colors=colors;
        Label(rect,"Text",text,28,Vector2.zero,Vector2.one);return button;
    }
    private static void Bind(Object owner,string field,Object value)
    {var state=new SerializedObject(owner);state.FindProperty(field).objectReferenceValue=value;state.ApplyModifiedPropertiesWithoutUndo();}
    private static void Connect(TitleMenuUI owner,string field,Button button,UnityAction action)
    {Bind(owner,field,button);UnityEventTools.AddPersistentListener(button.onClick,action);EditorUtility.SetDirty(button);}
}
