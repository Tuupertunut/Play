using System;
using System.Collections.Generic;
using System.Linq;
using ProTrans;
using UniInject;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class OptionsOverviewSceneControl : MonoBehaviour, INeedInjection, ITranslator
{
    public List<OptionSceneRecipe> optionSceneRecipes = new();
    
    [Inject(UxmlName = R.UxmlNames.sceneTitle)]
    private Label sceneTitle;

    [Inject(UxmlName = R.UxmlNames.gameOptionsButton)]
    private Button gameOptionsButton;

    [Inject(UxmlName = R.UxmlNames.songsOptionsButton)]
    private Button songsOptionsButton;

    [Inject(UxmlName = R.UxmlNames.graphicsOptionsButton)]
    private Button graphicsOptionsButton;

    [Inject(UxmlName = R.UxmlNames.soundOptionsButton)]
    private Button soundOptionsButton;

    [Inject(UxmlName = R.UxmlNames.recordingOptionsButton)]
    private Button recordingOptionsButton;

    [Inject(UxmlName = R.UxmlNames.profileOptionsButton)]
    private Button profileOptionsButton;

    [Inject(UxmlName = R.UxmlNames.designOptionsButton)]
    private Button designOptionsButton;

    [Inject(UxmlName = R.UxmlNames.internetOptionsButton)]
    private Button internetOptionsButton;

    [Inject(UxmlName = R.UxmlNames.appOptionsButton)]
    private Button appOptionsButton;

    [Inject(UxmlName = R.UxmlNames.developerOptionsButton)]
    private Button developerOptionsButton;

    [Inject(UxmlName = R.UxmlNames.webcamOptionsButton)]
    private Button webcamOptionsButton;

    [Inject(UxmlName = R.UxmlNames.languageChooser)]
    private ItemPicker languageChooser;

    [Inject(UxmlName = R.UxmlNames.songSettingsProblemHintIcon)]
    private VisualElement songSettingsProblemHintIcon;

    [Inject(UxmlName = R.UxmlNames.recordingSettingsProblemHintIcon)]
    private VisualElement recordingSettingsProblemHintIcon;

    [Inject(UxmlName = R.UxmlNames.playerProfileSettingsProblemHintIcon)]
    private VisualElement playerProfileSettingsProblemHintIcon;

    [Inject(UxmlName = R.UxmlNames.optionsSceneItemPicker)]
    private ItemPicker optionsSceneItemPicker;
    
    [Inject(UxmlName = R.UxmlNames.loadedSceneUi)]
    private VisualElement loadedSceneUi;
    
    [Inject]
    private SceneNavigator sceneNavigator;

    [Inject]
    private TranslationManager translationManager;

    [Inject]
    private Settings settings;

    [Inject]
    private SongMetaManager songMetaManager;

    [Inject]
    private Injector injector;

    private LabeledItemPickerControl<OptionSceneRecipe> optionsSceneItemPickerControl;

    private readonly List<GameObject> loadedGameObjects = new();
    
    private void Start()
    {
        gameOptionsButton.Focus();

        gameOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.OptionsGameScene));
        songsOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.SongLibraryOptionsScene));
        graphicsOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.OptionsGraphicsScene));
        soundOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.OptionsSoundScene));
        recordingOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.RecordingOptionsScene));
        profileOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.PlayerProfileSetupScene));
        designOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.ThemeOptionsScene));
        internetOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.NetworkOptionsScene));
        appOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.CompanionAppOptionsScene));
        developerOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.DevelopmentOptionsScene));
        webcamOptionsButton.RegisterCallbackButtonTriggered(() => sceneNavigator.LoadScene(EScene.WebcamOptionsSecene));

        optionsSceneItemPickerControl = new(optionsSceneItemPicker, optionSceneRecipes);
        optionsSceneItemPickerControl.SelectItem(optionSceneRecipes.FirstOrDefault());
        optionsSceneItemPickerControl.Selection.Subscribe(newValue => LoadOptionsScene(newValue));
        
        InitSettingsProblemHints();
        InitLanguageChooser();
    }

    private void LoadOptionsScene(OptionSceneRecipe sceneRecipe)
    {
        // Remove objects of old scene
        loadedGameObjects.ForEach(Destroy);
        loadedGameObjects.Clear();
        
        // Load scene UI
        loadedSceneUi.Clear();
        VisualElement loadedSceneVisualElement = sceneRecipe.visualTreeAsset.CloneTree().Children().FirstOrDefault();
        loadedSceneUi.Add(loadedSceneVisualElement);

        VisualElement scoreModeContainer = loadedSceneVisualElement.Q<VisualElement>(R.UxmlNames.scoreModeContainer);

        // Load scene scripts
        foreach (var gameObjectRecipe in sceneRecipe.sceneGameObjects)
        {
            GameObject loadedGameObject = Instantiate(gameObjectRecipe);
            loadedGameObjects.Add(loadedGameObject);
            
            // Inject new game object
            injector
                .WithRootVisualElement(loadedSceneVisualElement)
                .InjectAllComponentsInChildren(loadedGameObject);
        }
    }

    private void InitSettingsProblemHints()
    {
        new SettingsProblemHintControl(
            songSettingsProblemHintIcon,
            SettingsProblemHintControl.GetSongLibrarySettingsProblems(settings, songMetaManager),
            injector);

        new SettingsProblemHintControl(
            recordingSettingsProblemHintIcon,
            SettingsProblemHintControl.GetRecordingSettingsProblems(settings),
            injector);

        new SettingsProblemHintControl(
            playerProfileSettingsProblemHintIcon,
            SettingsProblemHintControl.GetPlayerSettingsProblems(settings),
            injector);
    }

    private void InitLanguageChooser()
    {
        new LabeledItemPickerControl<SystemLanguage>(
                languageChooser,
                translationManager.GetTranslatedLanguages())
            .Bind(() => translationManager.currentLanguage,
                newValue => SetLanguage(newValue));
    }

    public void UpdateTranslation()
    {
        sceneTitle.text = TranslationManager.GetTranslation(R.Messages.options);
        gameOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_game_button);
        songsOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_songLibrary_button);
        soundOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_sound_button);
        graphicsOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_graphics_button);
        recordingOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_recording_button);
        profileOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_playerProfiles_button);
        designOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_design_button);
        internetOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_internet_button);
        appOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_companionApp_button);
        developerOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_development_button);
        webcamOptionsButton.Q<Label>(R.UxmlNames.label).text = TranslationManager.GetTranslation(R.Messages.options_webcam_button);
    }

    private void SetLanguage(SystemLanguage newValue)
    {
        if (settings.GameSettings.language == newValue
            && translationManager.currentLanguage == newValue)
        {
            return;
        }

        settings.GameSettings.language = newValue;
        translationManager.currentLanguage = settings.GameSettings.language;
        translationManager.ReloadTranslationsAndUpdateScene();
    }
}
