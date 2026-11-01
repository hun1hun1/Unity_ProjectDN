using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class SettingsAssetSetup
{
    private const string ScenePath="Assets/Scenes/BattleScene.unity";
    private const string PrefabPath="Assets/Prefabs/UI/SettingsPanel.prefab";
    private static readonly Color Ivory=new Color(.95f,.92f,.82f);
    private static readonly Color Brass=new Color(.65f,.52f,.3f);
    private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NotoSansKR-Bold SDF.asset");

    [MenuItem("ProjectDN/Setup Settings Window")]
    public static void CreateAssets()
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null)prefab=CreatePrefab();
        Scene scene=SceneManager.GetSceneByPath(ScenePath);bool openedHere=!scene.isLoaded;
        if(openedHere)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            Transform canvas=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Canvas>(true)).First(layer=>layer.isRootCanvas).transform;
            BattleManager battle=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<BattleManager>(true)).Single();
            UnityEngine.UI.Button settingButton=canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button=>button.name=="SettingButton");
            CreateController(canvas,battle,settingButton,true);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("Large settings window wired to existing SettingButton; visual prefab, controls and confirmation created.");
        }
        finally{if(openedHere)EditorSceneManager.CloseScene(scene,true);}
    }

    public static SettingsUI CreateController(Transform canvas,BattleManager battle,UnityEngine.UI.Button settingButton,bool requireBattle)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if(prefab==null)prefab=CreatePrefab();
        SettingsUI settings=canvas.GetComponentInChildren<SettingsUI>(true);
        if(settings==null)settings=Rect(canvas,"Settings",Vector2.zero,Vector2.zero).gameObject.AddComponent<SettingsUI>();
        Transform panel=canvas.Find("SettingsPanel");
        if(panel==null)panel=((GameObject)PrefabUtility.InstantiatePrefab(prefab,canvas)).transform;
        Bind(settings,"battle",battle);
        Bind(settings,"deckOverview",canvas.GetComponentInChildren<DeckOverviewUI>(true));
        Bind(settings,"pileList",canvas.GetComponentsInChildren<DeckListUI>(true).FirstOrDefault(list=>list.name=="DeckListPanel"));
        Bind(settings,"settingButton",settingButton);
        Bind(settings,"panel",panel.gameObject);
        Bind(settings,"mainControls",panel.Find("MainContent").GetComponent<CanvasGroup>());
        string audio="MainContent/AudioSection/";
        foreach(string channel in new[]{"Master","Music","Effects"})
        {
            string field=char.ToLowerInvariant(channel[0])+channel.Substring(1);
            Bind(settings,field+"Slider",panel.Find(audio+channel+"Row/Slider").GetComponent<UnityEngine.UI.Slider>());
            Bind(settings,field+"Value",panel.Find(audio+channel+"Row/Value").GetComponent<TMP_Text>());
        }
        Bind(settings,"fullscreenToggle",panel.Find("MainContent/VideoSection/FullscreenToggle").GetComponent<UnityEngine.UI.Toggle>());
        Bind(settings,"resolutionDropdown",panel.Find("MainContent/VideoSection/ResolutionDropdown").GetComponent<TMP_Dropdown>());
        Bind(settings,"quitConfirmation",panel.Find("QuitConfirmation").gameObject);
        Connect(settings,panel,"closeButton","MainContent/CloseButton",settings.Close);
        Connect(settings,panel,"returnButton","MainContent/Footer/ReturnButton",settings.Close);
        Connect(settings,panel,"resetButton","MainContent/Footer/ResetButton",settings.ResetDefaults);
        Connect(settings,panel,"quitButton","MainContent/Footer/QuitButton",settings.RequestQuit);
        Connect(settings,panel,"applyDisplayButton","MainContent/VideoSection/ApplyButton",settings.ApplyDisplay);
        Connect(settings,panel,"cancelQuitButton","QuitConfirmation/Dialog/CancelButton",settings.CancelQuit);
        Connect(settings,panel,"confirmQuitButton","QuitConfirmation/Dialog/ConfirmButton",settings.ConfirmQuit);
        if(requireBattle)Event(settingButton,settings,settings.Toggle);
        var state=new SerializedObject(settings);state.FindProperty("requireBattle").boolValue=requireBattle;state.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
        return settings;
    }

    private static GameObject CreatePrefab()
    {
        RectTransform root=Rect(null,"SettingsPanel",Vector2.zero,new Vector2(1,.9f));
        try
        {
            Background(root,new Color(.025f,.035f,.03f,.99f),true);
            RectTransform main=Rect(root,"MainContent",Vector2.zero,Vector2.one);main.gameObject.AddComponent<CanvasGroup>();
            Label(main,"Title","설정",36,TextAlignmentOptions.Center,new Vector2(0,1),Vector2.one,new Vector2(0,-84),Vector2.zero);
            Button(main,"CloseButton","닫기",Vector2.one,new Vector2(-28,-16),new Vector2(112,48),Vector2.one);
            RectTransform audio=Section(main,"AudioSection",true,"사운드");
            VolumeRow(audio,"MasterRow","전체 음량",.77f);
            VolumeRow(audio,"MusicRow","배경음",.49f);
            VolumeRow(audio,"EffectsRow","효과음",.21f);
            RectTransform video=Section(main,"VideoSection",false,"화면");
            Label(video,"ResolutionLabel","해상도",28,TextAlignmentOptions.MidlineLeft,new Vector2(.08f,.77f),new Vector2(.92f,.86f),Vector2.zero,Vector2.zero);
            TMP_Dropdown dropdown=Dropdown(video);
            RectTransform dr=(RectTransform)dropdown.transform;dr.anchorMin=new Vector2(.08f,.64f);dr.anchorMax=new Vector2(.92f,.64f);dr.sizeDelta=new Vector2(0,60);dr.anchoredPosition=Vector2.zero;
            Toggle(video);
            Button(video,"ApplyButton","화면 설정 적용",new Vector2(.5f,.2f),Vector2.zero,new Vector2(320,60),new Vector2(.5f,.5f));
            RectTransform footer=Rect(main,"Footer",Vector2.zero,new Vector2(1,0));footer.pivot=Vector2.zero;footer.sizeDelta=new Vector2(0,128);
            Button(footer,"ResetButton","설정 초기화",new Vector2(0,.5f),new Vector2(40,0),new Vector2(260,64),new Vector2(0,.5f));
            Button(footer,"ReturnButton","게임으로 돌아가기",new Vector2(.5f,.5f),Vector2.zero,new Vector2(360,64),new Vector2(.5f,.5f));
            Button(footer,"QuitButton","게임 종료",new Vector2(1,.5f),new Vector2(-40,0),new Vector2(260,64),new Vector2(1,.5f));
            RectTransform confirmation=Rect(root,"QuitConfirmation",Vector2.zero,Vector2.one);Background(confirmation,new Color(0,0,0,.75f),true);
            RectTransform dialog=Rect(confirmation,"Dialog",new Vector2(.5f,.5f),new Vector2(.5f,.5f));dialog.sizeDelta=new Vector2(680,340);Background(dialog,new Color(.08f,.095f,.07f),true);Border(dialog);
            Label(dialog,"Title","게임 종료",32,TextAlignmentOptions.Center,new Vector2(0,.72f),Vector2.one,Vector2.zero,Vector2.zero);
            Label(dialog,"Message","게임을 종료하시겠습니까?",28,TextAlignmentOptions.Center,new Vector2(0,.36f),new Vector2(1,.72f),Vector2.zero,Vector2.zero);
            Button(dialog,"CancelButton","취소",new Vector2(.28f,.2f),Vector2.zero,new Vector2(220,60),new Vector2(.5f,.5f));
            Button(dialog,"ConfirmButton","종료",new Vector2(.72f,.2f),Vector2.zero,new Vector2(220,60),new Vector2(.5f,.5f));
            StyleControls(root);
            confirmation.gameObject.SetActive(false);root.gameObject.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(root.gameObject,PrefabPath);
        }
        finally{Object.DestroyImmediate(root.gameObject);}
    }

    public static void UpdateGeneratedControls()
    {
        GameObject root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try{StyleControls(root.transform);PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    private static void StyleControls(Transform root)
    {
        TMP_Dropdown dropdown=root.GetComponentInChildren<TMP_Dropdown>(true);
        UnityEngine.UI.Toggle item=dropdown.itemText.GetComponentInParent<UnityEngine.UI.Toggle>(true);
        item.targetGraphic.color=Color.white;
        UnityEngine.UI.ColorBlock colors=item.colors;
        colors.normalColor=new Color(.075f,.085f,.065f);
        colors.highlightedColor=new Color(.23f,.25f,.17f);
        colors.selectedColor=new Color(.27f,.24f,.14f);
        colors.pressedColor=new Color(.13f,.15f,.1f);
        item.colors=colors;
        item.graphic.color=Brass;
        dropdown.transform.Find("Arrow").GetComponent<UnityEngine.UI.Image>().color=Ivory;
        foreach(UnityEngine.UI.Slider slider in root.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
        {
            RectTransform handle=slider.handleRect;
            RectTransform area=(RectTransform)handle.parent;
            area.anchorMin=new Vector2(area.anchorMin.x,.5f);area.anchorMax=new Vector2(area.anchorMax.x,.5f);
            area.sizeDelta=new Vector2(area.sizeDelta.x,24);area.anchoredPosition=new Vector2(area.anchoredPosition.x,0);
            handle.sizeDelta=new Vector2(24,0);
        }
        Transform fullscreen=root.Find("MainContent/VideoSection/FullscreenToggle");
        UnityEngine.UI.Image hit=fullscreen.GetComponent<UnityEngine.UI.Image>();
        if(hit==null)hit=fullscreen.gameObject.AddComponent<UnityEngine.UI.Image>();
        hit.color=new Color(0,0,0,.001f);hit.raycastTarget=true;
    }

    private static RectTransform Section(Transform parent,string name,bool left,string title)
    {
        RectTransform rect=Rect(parent,name,new Vector2(left?0:.5f,0),new Vector2(left?.5f:1,1));
        rect.offsetMin=new Vector2(left?32:16,144);rect.offsetMax=new Vector2(left?-16:-32,-100);
        Background(rect,new Color(.055f,.07f,.055f),false);Border(rect);
        Label(rect,"Title",title,32,TextAlignmentOptions.Center,new Vector2(0,.9f),Vector2.one,Vector2.zero,Vector2.zero);
        return rect;
    }
    private static void VolumeRow(Transform parent,string name,string title,float y)
    {
        RectTransform row=Rect(parent,name,new Vector2(.08f,y),new Vector2(.92f,y));row.sizeDelta=new Vector2(0,112);
        Label(row,"Label",title,28,TextAlignmentOptions.MidlineLeft,new Vector2(0,.56f),Vector2.one,Vector2.zero,new Vector2(-120,0));
        Label(row,"Value","100%",26,TextAlignmentOptions.MidlineRight,new Vector2(1,.56f),Vector2.one,new Vector2(-120,0),Vector2.zero);
        var resources=new UnityEngine.UI.DefaultControls.Resources {standard=Sprite("UISprite.psd"),background=Sprite("Background.psd"),knob=Sprite("Knob.psd")};
        GameObject sliderObject=UnityEngine.UI.DefaultControls.CreateSlider(resources);sliderObject.name="Slider";sliderObject.transform.SetParent(row,false);
        RectTransform sliderRect=(RectTransform)sliderObject.transform;sliderRect.anchorMin=Vector2.zero;sliderRect.anchorMax=new Vector2(1,.4f);sliderRect.sizeDelta=Vector2.zero;sliderRect.anchoredPosition=Vector2.zero;
        UnityEngine.UI.Slider slider=sliderObject.GetComponent<UnityEngine.UI.Slider>();slider.minValue=0;slider.maxValue=1;slider.value=1;
        slider.fillRect.GetComponent<UnityEngine.UI.Image>().color=Brass;
        sliderObject.transform.Find("Background").GetComponent<UnityEngine.UI.Image>().color=new Color(.1f,.12f,.1f);
    }
    private static TMP_Dropdown Dropdown(Transform parent)
    {
        var resources=new TMP_DefaultControls.Resources {standard=Sprite("UISprite.psd"),background=Sprite("Background.psd"),inputField=Sprite("InputFieldBackground.psd"),knob=Sprite("Knob.psd"),checkmark=Sprite("Checkmark.psd"),dropdown=Sprite("DropdownArrow.psd"),mask=Sprite("UIMask.psd")};
        GameObject root=TMP_DefaultControls.CreateDropdown(resources);root.name="ResolutionDropdown";root.transform.SetParent(parent,false);
        root.GetComponent<UnityEngine.UI.Image>().color=new Color(.18f,.2f,.15f);
        TMP_Dropdown dropdown=root.GetComponent<TMP_Dropdown>();
        dropdown.template.sizeDelta=new Vector2(dropdown.template.sizeDelta.x,280);
        dropdown.template.GetComponent<UnityEngine.UI.Image>().color=new Color(.075f,.085f,.065f);
        RectTransform item=dropdown.template.Find("Viewport/Content/Item") as RectTransform;
        item.sizeDelta=new Vector2(item.sizeDelta.x,44);
        ((RectTransform)item.parent).sizeDelta=new Vector2(0,44);
        foreach(TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)){text.font=Font;text.fontSize=26;text.color=Ivory;text.raycastTarget=false;}
        return dropdown;
    }
    private static void Toggle(Transform parent)
    {
        RectTransform root=Rect(parent,"FullscreenToggle",new Vector2(.08f,.44f),new Vector2(.92f,.44f));root.sizeDelta=new Vector2(0,60);
        RectTransform square=Rect(root,"Background",new Vector2(0,.5f),new Vector2(0,.5f));square.pivot=new Vector2(0,.5f);square.sizeDelta=new Vector2(42,42);Background(square,new Color(.18f,.2f,.15f),true);Border(square);
        RectTransform mark=Rect(square,"Checkmark",Vector2.zero,Vector2.one);mark.offsetMin=new Vector2(7,7);mark.offsetMax=new Vector2(-7,-7);
        UnityEngine.UI.Image check=Background(mark,Ivory,false);check.sprite=Sprite("Checkmark.psd");
        UnityEngine.UI.Toggle toggle=root.gameObject.AddComponent<UnityEngine.UI.Toggle>();toggle.targetGraphic=square.GetComponent<UnityEngine.UI.Image>();toggle.graphic=check;
        Label(root,"Label","전체 화면",28,TextAlignmentOptions.MidlineLeft,Vector2.zero,Vector2.one,new Vector2(60,0),Vector2.zero);
    }
    private static UnityEngine.UI.Button Button(Transform parent,string name,string text,Vector2 anchor,Vector2 position,Vector2 size,Vector2 pivot)
    {
        RectTransform rect=Rect(parent,name,anchor,anchor);rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;
        UnityEngine.UI.Image image=Background(rect,new Color(.2f,.22f,.16f),true);Border(rect);
        UnityEngine.UI.Button button=rect.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=image;
        Label(rect,"Text",text,26,TextAlignmentOptions.Center,Vector2.zero,Vector2.one,new Vector2(8,0),new Vector2(-8,0));return button;
    }
    private static void Border(RectTransform rect)
    {UnityEngine.UI.Outline outline=rect.gameObject.AddComponent<UnityEngine.UI.Outline>();outline.effectColor=Brass;outline.effectDistance=new Vector2(1,-1);outline.useGraphicAlpha=false;}
    private static Sprite Sprite(string name)=>AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/"+name);
    private static RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max)
    {
        RectTransform rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.gameObject.layer=5;rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.sizeDelta=Vector2.zero;rect.anchoredPosition=Vector2.zero;return rect;
    }
    private static UnityEngine.UI.Image Background(RectTransform rect,Color color,bool raycast)
    {UnityEngine.UI.Image image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=color;image.raycastTarget=raycast;return image;}
    private static void Label(Transform parent,string name,string text,int size,TextAlignmentOptions alignment,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {RectTransform rect=Rect(parent,name,min,max);rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;TextMeshProUGUI label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=Font;label.text=text;label.fontSize=size;label.color=Ivory;label.alignment=alignment;label.raycastTarget=false;}
    private static void Bind(Object owner,string name,Object value)
    {var state=new SerializedObject(owner);state.FindProperty(name).objectReferenceValue=value;state.ApplyModifiedPropertiesWithoutUndo();}
    private static void Connect(SettingsUI settings,Transform panel,string field,string path,UnityAction action)
    {UnityEngine.UI.Button button=panel.Find(path).GetComponent<UnityEngine.UI.Button>();Bind(settings,field,button);Event(button,settings,action);}
    private static void Event(UnityEngine.UI.Button button,SettingsUI settings,UnityAction action)
    {
        for(int i=button.onClick.GetPersistentEventCount()-1;i>=0;i--)
            if(button.onClick.GetPersistentTarget(i)==settings && button.onClick.GetPersistentMethodName(i)==action.Method.Name)UnityEventTools.RemovePersistentListener(button.onClick,i);
        UnityEventTools.AddPersistentListener(button.onClick,action);EditorUtility.SetDirty(button);
        if(PrefabUtility.IsPartOfPrefabInstance(button))PrefabUtility.RecordPrefabInstancePropertyModifications(button);
    }
}
