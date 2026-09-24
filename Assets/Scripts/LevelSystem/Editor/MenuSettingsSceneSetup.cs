#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LegoPuzzle.Runtime;

namespace LegoPuzzle.Editor
{
    [InitializeOnLoad]
    public static class MenuSettingsSceneSetup
    {
        static MenuSettingsSceneSetup()
        {
            // Do NOT auto-run on delayCall so user changes in Scenes/Menu.unity are never overwritten!
        }

        [MenuItem("LEGO/Setup Settings Panel in Menu")]
        public static void SetupMenuSettings()
        {
            CheckAndSetupMenuSettings();
        }

        private static void CheckAndSetupMenuSettings()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene == null || !activeScene.isLoaded) return;

            Canvas canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            bool isMenuScene = activeScene.name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               Object.FindAnyObjectByType<MenuHeartsUI>() != null ||
                               Object.FindAnyObjectByType<MenuCoinsUI>() != null;
            if (!isMenuScene) return;

            // 1. Ensure [GameSettingsManager] in scene
            GameSettingsManager settingsManager = Object.FindAnyObjectByType<GameSettingsManager>();
            if (settingsManager == null)
            {
                GameObject smObj = new GameObject("[GameSettingsManager]");
                settingsManager = smObj.AddComponent<GameSettingsManager>();
                Undo.RegisterCreatedObjectUndo(smObj, "Create GameSettingsManager");
            }

            if (settingsManager != null)
            {
                settingsManager.EnsureAudioSources();
                if (settingsManager.ButtonClickSound == null || settingsManager.ButtonClickSound.name != "Button_Click")
                {
                    settingsManager.ButtonClickSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Button_Click.wav");
                }
                if (settingsManager.MenuMusicClip == null)
                {
                    settingsManager.MenuMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/main-menu.wav");
                }
                if (settingsManager.GameMusicClip == null)
                {
                    settingsManager.GameMusicClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/lvlsound.mp3");
                }
                EditorUtility.SetDirty(settingsManager);
            }

            // 2. Locate or create Settings Panel
            Transform settingsPanelTr = canvas.transform.Find("Settings Panel");
            if (settingsPanelTr == null)
            {
                settingsPanelTr = canvas.transform.Find("SettingsPanel");
            }
            if (settingsPanelTr == null)
            {
                GameObject panelObj = new GameObject("Settings Panel", typeof(RectTransform));
                panelObj.transform.SetParent(canvas.transform, false);
                settingsPanelTr = panelObj.transform;
                Undo.RegisterCreatedObjectUndo(panelObj, "Create Settings Panel");
            }

            // 3. Ensure MenuSettingsPanel component
            MenuSettingsPanel panelComponent = settingsPanelTr.GetComponent<MenuSettingsPanel>();
            if (panelComponent == null)
            {
                panelComponent = Undo.AddComponent<MenuSettingsPanel>(settingsPanelTr.gameObject);
            }

            // 4. Build or update full hierarchy
            BuildSettingsPanelHierarchy(settingsPanelTr.gameObject, panelComponent, canvas);

            EditorUtility.SetDirty(settingsPanelTr.gameObject);
            EditorUtility.SetDirty(canvas.gameObject);
            if (!Application.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
            }
        }

        public static void BuildSettingsPanelHierarchy(GameObject panelObj, MenuSettingsPanel panelComp, Canvas canvas)
        {
            TMP_FontAsset font = MenuSettingsPanel.GetGameFontAsset();

            // Root RectTransform (fullscreen stretch)
            RectTransform rootRt = panelObj.GetComponent<RectTransform>();
            if (rootRt == null) rootRt = panelObj.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;
            rootRt.pivot = new Vector2(0.5f, 0.5f);

            // Remove legacy background image on root if present (it should be on DialogCard)
            Image rootImg = panelObj.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.color = new Color(0, 0, 0, 0);
                rootImg.raycastTarget = false;
            }

            // 1. Backdrop
            Transform bdTr = panelObj.transform.Find("Backdrop");
            GameObject bdObj;
            if (bdTr == null)
            {
                bdObj = new GameObject("Backdrop");
                bdObj.transform.SetParent(panelObj.transform, false);
                bdObj.transform.SetAsFirstSibling();
            }
            else
            {
                bdObj = bdTr.gameObject;
            }

            RectTransform bdRt = bdObj.GetComponent<RectTransform>();
            if (bdRt == null) bdRt = bdObj.AddComponent<RectTransform>();
            bdRt.anchorMin = Vector2.zero;
            bdRt.anchorMax = Vector2.one;
            bdRt.offsetMin = Vector2.zero;
            bdRt.offsetMax = Vector2.zero;

            Image bdImg = bdObj.GetComponent<Image>();
            if (bdImg == null) bdImg = bdObj.AddComponent<Image>();
            bdImg.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);
            bdImg.raycastTarget = true;

            Button bdBtn = bdObj.GetComponent<Button>();
            if (bdBtn == null) bdBtn = bdObj.AddComponent<Button>();
            bdBtn.transition = Selectable.Transition.None;

            // 2. DialogCard
            Transform cardTr = panelObj.transform.Find("DialogCard");
            GameObject cardObj;
            if (cardTr == null)
            {
                cardObj = new GameObject("DialogCard");
                cardObj.transform.SetParent(panelObj.transform, false);
            }
            else
            {
                cardObj = cardTr.gameObject;
            }

            RectTransform cardRt = cardObj.GetComponent<RectTransform>();
            if (cardRt == null) cardRt = cardObj.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(680f, 860f);

            Image cardImg = cardObj.GetComponent<Image>();
            if (cardImg == null) cardImg = cardObj.AddComponent<Image>();
            Sprite cardSprite = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png", "UI-pack_Sprite_2_2");
            if (cardSprite != null) cardImg.sprite = cardSprite;
            cardImg.type = Image.Type.Simple;
            cardImg.raycastTarget = true;

            // 3. Move Upper Panel under DialogCard if it was on root
            Transform upperOnRoot = panelObj.transform.Find("Upper Panel");
            if (upperOnRoot != null)
            {
                upperOnRoot.SetParent(cardObj.transform, false);
            }

            Transform upperTr = cardObj.transform.Find("Upper Panel");
            if (upperTr != null)
            {
                RectTransform upperRt = upperTr.GetComponent<RectTransform>();
                upperRt.anchorMin = new Vector2(0.5f, 1f);
                upperRt.anchorMax = new Vector2(0.5f, 1f);
                upperRt.pivot = new Vector2(0.5f, 0.5f);
                upperRt.anchoredPosition = new Vector2(0f, -25f);
                upperRt.sizeDelta = new Vector2(420f, 100f);

                TMP_Text titleTMP = upperTr.GetComponentInChildren<TMP_Text>();
                if (titleTMP != null)
                {
                    titleTMP.text = "SETTINGS";
                    titleTMP.fontSize = 44f;
                    titleTMP.fontStyle = FontStyles.Bold;
                    titleTMP.alignment = TextAlignmentOptions.Center;
                    titleTMP.color = Color.white;
                    if (font != null) titleTMP.font = font;
                }
            }

            // 4. Close Button (round red X)
            Transform closeTr = cardObj.transform.Find("CloseButton");
            GameObject closeObj = closeTr != null ? closeTr.gameObject : new GameObject("CloseButton");
            closeObj.transform.SetParent(cardObj.transform, false);

            RectTransform closeRt = closeObj.GetComponent<RectTransform>();
            if (closeRt == null) closeRt = closeObj.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(0.5f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-28f, -28f);
            closeRt.sizeDelta = new Vector2(74f, 74f);

            Image closeImg = closeObj.GetComponent<Image>();
            if (closeImg == null) closeImg = closeObj.AddComponent<Image>();
            Sprite closeSprite = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png", "UI-pack_Sprite_1_79");
            if (closeSprite != null) closeImg.sprite = closeSprite;
            closeImg.preserveAspect = true;

            Button closeBtn = closeObj.GetComponent<Button>();
            if (closeBtn == null) closeBtn = closeObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(closeObj);

            // 5. Build 4 Toggle Rows
            Sprite soundIcon = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png", "UI-pack_Sprite_1_71");
            Sprite musicIcon = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png", "UI-pack_Sprite_1_73");
            Sprite vibIcon = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png", "UI-pack_Sprite_1_78");
            Sprite eyeIcon = MenuSettingsPanel.GetOrCreateEyeIconSprite();
            Sprite rowBgSprite = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png", "UI-pack_Sprite_2_3");

            CreateOrUpdateRow(cardObj.transform, "SoundRow", "Sounds", soundIcon, rowBgSprite, 180f, font);
            CreateOrUpdateRow(cardObj.transform, "MusicRow", "Music", musicIcon, rowBgSprite, 70f, font);
            CreateOrUpdateRow(cardObj.transform, "VibrationRow", "Vibration", vibIcon, rowBgSprite, -40f, font);

            Transform legacyNotif = cardObj.transform.Find("NotificationsRow");
            if (legacyNotif != null)
            {
                legacyNotif.name = "ColorblindRow";
            }
            CreateOrUpdateRow(cardObj.transform, "ColorblindRow", "Color Blind", eyeIcon, rowBgSprite, -150f, font);

            // 6. Reset Progress Button
            Transform resetTr = cardObj.transform.Find("ResetProgressButton");
            GameObject resetObj = resetTr != null ? resetTr.gameObject : new GameObject("ResetProgressButton");
            resetObj.transform.SetParent(cardObj.transform, false);

            RectTransform resetRt = resetObj.GetComponent<RectTransform>();
            if (resetRt == null) resetRt = resetObj.AddComponent<RectTransform>();
            resetRt.anchorMin = new Vector2(0.5f, 0.5f);
            resetRt.anchorMax = new Vector2(0.5f, 0.5f);
            resetRt.pivot = new Vector2(0.5f, 0.5f);
            resetRt.anchoredPosition = new Vector2(0f, -260f);
            resetRt.sizeDelta = new Vector2(400f, 76f);

            Image resetImg = resetObj.GetComponent<Image>();
            if (resetImg == null) resetImg = resetObj.AddComponent<Image>();
            Sprite resetSprite = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png", "UI-pack_Sprite_2_9");
            if (resetSprite != null) resetImg.sprite = resetSprite;

            Button resetBtn = resetObj.GetComponent<Button>();
            if (resetBtn == null) resetBtn = resetObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(resetObj);

            Transform resetTxtTr = resetObj.transform.Find("Text (TMP)");
            GameObject resetTxtObj = resetTxtTr != null ? resetTxtTr.gameObject : new GameObject("Text (TMP)");
            resetTxtObj.transform.SetParent(resetObj.transform, false);
            RectTransform resetTxtRt = resetTxtObj.GetComponent<RectTransform>();
            if (resetTxtRt == null) resetTxtRt = resetTxtObj.AddComponent<RectTransform>();
            resetTxtRt.anchorMin = Vector2.zero;
            resetTxtRt.anchorMax = Vector2.one;
            resetTxtRt.offsetMin = Vector2.zero;
            resetTxtRt.offsetMax = Vector2.zero;

            TMP_Text resetTMP = resetTxtObj.GetComponent<TMP_Text>();
            if (resetTMP == null) resetTMP = resetTxtObj.AddComponent<TextMeshProUGUI>();
            resetTMP.text = "RESET PROGRESS";
            resetTMP.fontSize = 28f;
            resetTMP.fontStyle = FontStyles.Bold;
            resetTMP.alignment = TextAlignmentOptions.Center;
            resetTMP.color = Color.white;
            if (font != null) resetTMP.font = font;

            // 7. Version Label
            Transform verTr = cardObj.transform.Find("VersionText");
            GameObject verObj = verTr != null ? verTr.gameObject : new GameObject("VersionText");
            verObj.transform.SetParent(cardObj.transform, false);
            RectTransform verRt = verObj.GetComponent<RectTransform>();
            if (verRt == null) verRt = verObj.AddComponent<RectTransform>();
            verRt.anchorMin = new Vector2(0.5f, 0.5f);
            verRt.anchorMax = new Vector2(0.5f, 0.5f);
            verRt.pivot = new Vector2(0.5f, 0.5f);
            verRt.anchoredPosition = new Vector2(0f, -345f);
            verRt.sizeDelta = new Vector2(300f, 35f);

            TMP_Text verTMP = verObj.GetComponent<TMP_Text>();
            if (verTMP == null) verTMP = verObj.AddComponent<TextMeshProUGUI>();
            verTMP.text = "LEGO Puzzle v1.0";
            verTMP.fontSize = 20f;
            verTMP.alignment = TextAlignmentOptions.Center;
            verTMP.color = new Color(0.7f, 0.75f, 0.85f, 0.8f);
            if (font != null) verTMP.font = font;

            // 8. Confirmation Dialog Sub-panel
            BuildConfirmationDialog(cardObj.transform, font);

            // 9. Toast Notification Root
            BuildToastRoot(cardObj.transform, font);

            // 10. Wire MenuSettingsPanel component references
            panelComp.EnsureUIHierarchy();
            panelComp.BindButtonListeners();
            panelComp.RefreshAllToggleStates(false);

            // 11. Wire SettingsButton on Canvas to open settings
            Transform settingsBtnTr = canvas.transform.Find("SettingsButton");
            if (settingsBtnTr != null)
            {
                Button btn = settingsBtnTr.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(panelComp.Open);
                    UIButtonPressEffect.AttachTo(settingsBtnTr.gameObject);
                    EditorUtility.SetDirty(btn);
                }
            }
        }

        private static void CreateOrUpdateRow(Transform parent, string rowName, string label, Sprite iconSprite, Sprite rowBgSprite, float posY, TMP_FontAsset font)
        {
            Transform rowTr = parent.Find(rowName);
            GameObject rowObj = rowTr != null ? rowTr.gameObject : new GameObject(rowName);
            rowObj.transform.SetParent(parent, false);

            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            if (rowRt == null) rowRt = rowObj.AddComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, posY);
            rowRt.sizeDelta = new Vector2(560f, 84f);

            Image rowImg = rowObj.GetComponent<Image>();
            if (rowImg == null) rowImg = rowObj.AddComponent<Image>();
            if (rowBgSprite != null) rowImg.sprite = rowBgSprite;
            rowImg.color = new Color(1f, 1f, 1f, 0.96f);

            // Icon
            Transform iconTr = rowObj.transform.Find("Icon");
            GameObject iconObj = iconTr != null ? iconTr.gameObject : new GameObject("Icon");
            iconObj.transform.SetParent(rowObj.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            if (iconRt == null) iconRt = iconObj.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(52f, 0f);
            iconRt.sizeDelta = new Vector2(54f, 54f);

            Image iconImg = iconObj.GetComponent<Image>();
            if (iconImg == null) iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;
            iconImg.color = new Color(0.18f, 0.22f, 0.32f, 1f);

            // Label
            Transform labelTr = rowObj.transform.Find("Label");
            GameObject labelObj = labelTr != null ? labelTr.gameObject : new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);
            RectTransform labelRt = labelObj.GetComponent<RectTransform>();
            if (labelRt == null) labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(96f, 0f);
            labelRt.sizeDelta = new Vector2(280f, 50f);

            TMP_Text labelTMP = labelObj.GetComponent<TMP_Text>();
            if (labelTMP == null) labelTMP = labelObj.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 32f;
            labelTMP.fontStyle = FontStyles.Bold;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            labelTMP.color = new Color(0.15f, 0.18f, 0.25f, 1f);
            if (font != null) labelTMP.font = font;

            // Toggle Switch
            Transform switchTr = rowObj.transform.Find("ToggleSwitch");
            GameObject switchObj = switchTr != null ? switchTr.gameObject : new GameObject("ToggleSwitch");
            switchObj.transform.SetParent(rowObj.transform, false);
            RectTransform switchRt = switchObj.GetComponent<RectTransform>();
            if (switchRt == null) switchRt = switchObj.AddComponent<RectTransform>();
            switchRt.anchorMin = new Vector2(1f, 0.5f);
            switchRt.anchorMax = new Vector2(1f, 0.5f);
            switchRt.pivot = new Vector2(1f, 0.5f);
            switchRt.anchoredPosition = new Vector2(-28f, 0f);
            switchRt.sizeDelta = new Vector2(104f, 52f);

            Image switchBg = switchObj.GetComponent<Image>();
            if (switchBg == null) switchBg = switchObj.AddComponent<Image>();
            switchBg.color = new Color(0.2f, 0.82f, 0.48f, 1f);

            Button switchBtn = switchObj.GetComponent<Button>();
            if (switchBtn == null) switchBtn = switchObj.AddComponent<Button>();
            switchBtn.transition = Selectable.Transition.None;

            // State Text (Defaults to LEFT side for ON state)
            Transform stateTr = switchObj.transform.Find("StateText");
            GameObject stateObj = stateTr != null ? stateTr.gameObject : new GameObject("StateText");
            stateObj.transform.SetParent(switchObj.transform, false);
            RectTransform stateRt = stateObj.GetComponent<RectTransform>();
            if (stateRt == null) stateRt = stateObj.AddComponent<RectTransform>();
            stateRt.anchorMin = new Vector2(0.5f, 0.5f);
            stateRt.anchorMax = new Vector2(0.5f, 0.5f);
            stateRt.pivot = new Vector2(0.5f, 0.5f);
            stateRt.anchoredPosition = new Vector2(-22f, 0f);
            stateRt.sizeDelta = new Vector2(48f, 36f);

            TMP_Text stateTMP = stateObj.GetComponent<TMP_Text>();
            if (stateTMP == null) stateTMP = stateObj.AddComponent<TextMeshProUGUI>();
            stateTMP.text = "ON";
            stateTMP.fontSize = 20f;
            stateTMP.fontStyle = FontStyles.Bold;
            stateTMP.alignment = TextAlignmentOptions.Center;
            stateTMP.color = Color.white;
            stateTMP.raycastTarget = false;
            if (font != null) stateTMP.font = font;

            // Knob (Defaults to RIGHT side for ON state)
            Transform knobTr = switchObj.transform.Find("Knob");
            GameObject knobObj = knobTr != null ? knobTr.gameObject : new GameObject("Knob");
            knobObj.transform.SetParent(switchObj.transform, false);
            RectTransform knobRt = knobObj.GetComponent<RectTransform>();
            if (knobRt == null) knobRt = knobObj.AddComponent<RectTransform>();
            knobRt.anchorMin = new Vector2(0.5f, 0.5f);
            knobRt.anchorMax = new Vector2(0.5f, 0.5f);
            knobRt.pivot = new Vector2(0.5f, 0.5f);
            knobRt.anchoredPosition = new Vector2(26f, 0f);
            knobRt.sizeDelta = new Vector2(44f, 44f);

            Image knobImg = knobObj.GetComponent<Image>();
            if (knobImg == null) knobImg = knobObj.AddComponent<Image>();
            knobImg.color = Color.white;
            knobImg.raycastTarget = false;
        }

        private static void BuildConfirmationDialog(Transform parent, TMP_FontAsset font)
        {
            Transform confTr = parent.Find("ConfirmationDialog");
            GameObject confObj = confTr != null ? confTr.gameObject : new GameObject("ConfirmationDialog");
            confObj.transform.SetParent(parent, false);

            RectTransform confRt = confObj.GetComponent<RectTransform>();
            if (confRt == null) confRt = confObj.AddComponent<RectTransform>();
            confRt.anchorMin = new Vector2(0.5f, 0.5f);
            confRt.anchorMax = new Vector2(0.5f, 0.5f);
            confRt.pivot = new Vector2(0.5f, 0.5f);
            confRt.anchoredPosition = Vector2.zero;
            confRt.sizeDelta = new Vector2(580f, 360f);

            Image confImg = confObj.GetComponent<Image>();
            if (confImg == null) confImg = confObj.AddComponent<Image>();
            Sprite confSprite = MenuSettingsPanel.LoadAtlasSprite("Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png", "UI-pack_Sprite_2_10");
            if (confSprite != null) confImg.sprite = confSprite;
            confImg.color = new Color(0.12f, 0.16f, 0.25f, 0.98f);

            // Title
            Transform titleTr = confObj.transform.Find("Title");
            GameObject titleObj = titleTr != null ? titleTr.gameObject : new GameObject("Title");
            titleObj.transform.SetParent(confObj.transform, false);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            if (titleRt == null) titleRt = titleObj.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            titleRt.sizeDelta = new Vector2(500f, 50f);

            TMP_Text titleTMP = titleObj.GetComponent<TMP_Text>();
            if (titleTMP == null) titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "Reset Progress?";
            titleTMP.fontSize = 36f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = Color.white;
            if (font != null) titleTMP.font = font;

            // Message
            Transform msgTr = confObj.transform.Find("Message");
            GameObject msgObj = msgTr != null ? msgTr.gameObject : new GameObject("Message");
            msgObj.transform.SetParent(confObj.transform, false);
            RectTransform msgRt = msgObj.GetComponent<RectTransform>();
            if (msgRt == null) msgRt = msgObj.AddComponent<RectTransform>();
            msgRt.anchorMin = new Vector2(0.5f, 0.5f);
            msgRt.anchorMax = new Vector2(0.5f, 0.5f);
            msgRt.pivot = new Vector2(0.5f, 0.5f);
            msgRt.anchoredPosition = new Vector2(0f, 10f);
            msgRt.sizeDelta = new Vector2(480f, 90f);

            TMP_Text msgTMP = msgObj.GetComponent<TMP_Text>();
            if (msgTMP == null) msgTMP = msgObj.AddComponent<TextMeshProUGUI>();
            msgTMP.text = "Your level progress will be reset back to Level 1. Are you sure?";
            msgTMP.fontSize = 24f;
            msgTMP.alignment = TextAlignmentOptions.Center;
            msgTMP.color = new Color(0.85f, 0.9f, 0.98f, 1f);
            if (font != null) msgTMP.font = font;

            // Cancel Button
            Transform cancelTr = confObj.transform.Find("CancelButton");
            GameObject cancelObj = cancelTr != null ? cancelTr.gameObject : new GameObject("CancelButton");
            cancelObj.transform.SetParent(confObj.transform, false);
            RectTransform cancelRt = cancelObj.GetComponent<RectTransform>();
            if (cancelRt == null) cancelRt = cancelObj.AddComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.5f, 0f);
            cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0.5f, 0f);
            cancelRt.anchoredPosition = new Vector2(-120f, 30f);
            cancelRt.sizeDelta = new Vector2(200f, 66f);

            Image cancelImg = cancelObj.GetComponent<Image>();
            if (cancelImg == null) cancelImg = cancelObj.AddComponent<Image>();
            cancelImg.color = new Color(0.35f, 0.4f, 0.5f, 1f);

            Button cancelBtn = cancelObj.GetComponent<Button>();
            if (cancelBtn == null) cancelBtn = cancelObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(cancelObj);

            Transform cancelTxtTr = cancelObj.transform.Find("Text");
            GameObject cancelTxtObj = cancelTxtTr != null ? cancelTxtTr.gameObject : new GameObject("Text");
            cancelTxtObj.transform.SetParent(cancelObj.transform, false);
            RectTransform cancelTxtRt = cancelTxtObj.GetComponent<RectTransform>();
            if (cancelTxtRt == null) cancelTxtRt = cancelTxtObj.AddComponent<RectTransform>();
            cancelTxtRt.anchorMin = Vector2.zero;
            cancelTxtRt.anchorMax = Vector2.one;
            cancelTxtRt.offsetMin = Vector2.zero;
            cancelTxtRt.offsetMax = Vector2.zero;

            TMP_Text cancelTMP = cancelTxtObj.GetComponent<TMP_Text>();
            if (cancelTMP == null) cancelTMP = cancelTxtObj.AddComponent<TextMeshProUGUI>();
            cancelTMP.text = "CANCEL";
            cancelTMP.fontSize = 26f;
            cancelTMP.fontStyle = FontStyles.Bold;
            cancelTMP.alignment = TextAlignmentOptions.Center;
            cancelTMP.color = Color.white;
            if (font != null) cancelTMP.font = font;

            // Confirm Button
            Transform confirmTr = confObj.transform.Find("ConfirmButton");
            GameObject confirmObj = confirmTr != null ? confirmTr.gameObject : new GameObject("ConfirmButton");
            confirmObj.transform.SetParent(confObj.transform, false);
            RectTransform confirmRt = confirmObj.GetComponent<RectTransform>();
            if (confirmRt == null) confirmRt = confirmObj.AddComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.5f, 0f);
            confirmRt.anchorMax = new Vector2(0.5f, 0f);
            confirmRt.pivot = new Vector2(0.5f, 0f);
            confirmRt.anchoredPosition = new Vector2(120f, 30f);
            confirmRt.sizeDelta = new Vector2(200f, 66f);

            Image confirmImg = confirmObj.GetComponent<Image>();
            if (confirmImg == null) confirmImg = confirmObj.AddComponent<Image>();
            confirmImg.color = new Color(0.92f, 0.28f, 0.32f, 1f);

            Button confirmBtn = confirmObj.GetComponent<Button>();
            if (confirmBtn == null) confirmBtn = confirmObj.AddComponent<Button>();
            UIButtonPressEffect.AttachTo(confirmObj);

            Transform confirmTxtTr = confirmObj.transform.Find("Text");
            GameObject confirmTxtObj = confirmTxtTr != null ? confirmTxtTr.gameObject : new GameObject("Text");
            confirmTxtObj.transform.SetParent(confirmObj.transform, false);
            RectTransform confirmTxtRt = confirmTxtObj.GetComponent<RectTransform>();
            if (confirmTxtRt == null) confirmTxtRt = confirmTxtObj.AddComponent<RectTransform>();
            confirmTxtRt.anchorMin = Vector2.zero;
            confirmTxtRt.anchorMax = Vector2.one;
            confirmTxtRt.offsetMin = Vector2.zero;
            confirmTxtRt.offsetMax = Vector2.zero;

            TMP_Text confirmTMP = confirmTxtObj.GetComponent<TMP_Text>();
            if (confirmTMP == null) confirmTMP = confirmTxtObj.AddComponent<TextMeshProUGUI>();
            confirmTMP.text = "RESET";
            confirmTMP.fontSize = 26f;
            confirmTMP.fontStyle = FontStyles.Bold;
            confirmTMP.alignment = TextAlignmentOptions.Center;
            confirmTMP.color = Color.white;
            if (font != null) confirmTMP.font = font;

            confObj.SetActive(false);
        }

        private static void BuildToastRoot(Transform parent, TMP_FontAsset font)
        {
            Transform tTr = parent.Find("ToastRoot");
            GameObject tObj = tTr != null ? tTr.gameObject : new GameObject("ToastRoot");
            tObj.transform.SetParent(parent, false);

            RectTransform tRt = tObj.GetComponent<RectTransform>();
            if (tRt == null) tRt = tObj.AddComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 0.5f);
            tRt.anchorMax = new Vector2(0.5f, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.anchoredPosition = new Vector2(0f, -200f);
            tRt.sizeDelta = new Vector2(480f, 60f);

            Image tImg = tObj.GetComponent<Image>();
            if (tImg == null) tImg = tObj.AddComponent<Image>();
            tImg.color = new Color(0.12f, 0.68f, 0.32f, 0.95f);

            Transform tTxtTr = tObj.transform.Find("Text");
            GameObject tTxtObj = tTxtTr != null ? tTxtTr.gameObject : new GameObject("Text");
            tTxtObj.transform.SetParent(tObj.transform, false);
            RectTransform tTxtRt = tTxtObj.GetComponent<RectTransform>();
            if (tTxtRt == null) tTxtRt = tTxtObj.AddComponent<RectTransform>();
            tTxtRt.anchorMin = Vector2.zero;
            tTxtRt.anchorMax = Vector2.one;
            tTxtRt.offsetMin = Vector2.zero;
            tTxtRt.offsetMax = Vector2.zero;

            TMP_Text tTMP = tTxtObj.GetComponent<TMP_Text>();
            if (tTMP == null) tTMP = tTxtObj.AddComponent<TextMeshProUGUI>();
            tTMP.text = "Progress reset to Level 1!";
            tTMP.fontSize = 24f;
            tTMP.fontStyle = FontStyles.Bold;
            tTMP.alignment = TextAlignmentOptions.Center;
            tTMP.color = Color.white;
            if (font != null) tTMP.font = font;

            tObj.SetActive(false);
        }

        [MenuItem("Tools/Lego/Setup Button Click Audio in Scenes")]
        public static void SetupButtonClickAudioInScenes()
        {
            // 1. Setup in Menu scene
            var menuScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            if (menuScene.IsValid())
            {
                AudioClip clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Button_Click.wav");
                var manager = Object.FindObjectOfType<GameSettingsManager>();
                if (manager != null)
                {
                    manager.EnsureAudioSources();
                    manager.ButtonClickSound = clickClip;
                    EditorUtility.SetDirty(manager);
                }

                int menuCount = 0;
                var allButtons = Resources.FindObjectsOfTypeAll<Button>();
                foreach (var btn in allButtons)
                {
                    if (btn != null && btn.gameObject.scene == menuScene)
                    {
                        var click = btn.GetComponent<UIButtonAudioClick>();
                        if (click == null)
                        {
                            click = Undo.AddComponent<UIButtonAudioClick>(btn.gameObject);
                            EditorUtility.SetDirty(btn.gameObject);
                        }
                        menuCount++;
                    }
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menuScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(menuScene);
                Debug.Log($"<color=green>Menu Scene: Configured {menuCount} buttons with Button_Click audio.</color>");
            }

            // 2. Setup in Game scene
            var gameScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            if (gameScene.IsValid())
            {
                int gameCount = 0;
                var allButtons = Resources.FindObjectsOfTypeAll<Button>();
                foreach (var btn in allButtons)
                {
                    if (btn != null && btn.gameObject.scene == gameScene && GameSettingsManager.IsGameExitButton(btn))
                    {
                        var click = btn.GetComponent<UIButtonAudioClick>();
                        if (click == null)
                        {
                            click = Undo.AddComponent<UIButtonAudioClick>(btn.gameObject);
                            EditorUtility.SetDirty(btn.gameObject);
                        }
                        gameCount++;
                    }
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(gameScene);
                Debug.Log($"<color=green>Game Scene: Configured {gameCount} exit buttons with Button_Click audio.</color>");
            }
        }

        [MenuItem("Tools/Lego/Setup Bottom Navigation Jelly Buttons")]
        public static void SetupBottomNavigationJellyButtonsMenu()
        {
            var menuScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Menu.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            if (menuScene.IsValid())
            {
                var controller = Object.FindObjectOfType<MenuButtonController>();
                if (controller != null)
                {
                    controller.SetupBottomNavigationJellyButtons();
                }

                var allButtons = Resources.FindObjectsOfTypeAll<Button>();
                int count = 0;
                foreach (var btn in allButtons)
                {
                    if (btn != null && btn.gameObject.scene == menuScene)
                    {
                        string name = btn.gameObject.name.ToLowerInvariant();
                        Transform parent = btn.transform.parent;
                        bool isDownPanel = (parent != null && parent.name == "DownPanel");

                        if (isDownPanel || name.Contains("play") || name.Contains("main menu") || name.Contains("shop"))
                        {
                            UIButtonPressEffect.AttachHorizontalJelly(btn.gameObject, 1.25f, 0.80f, 0.45f);
                            EditorUtility.SetDirty(btn.gameObject);
                            count++;
                        }
                    }
                }

                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(menuScene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(menuScene);
                Debug.Log($"<color=green>Successfully configured horizontal jelly effect on {count} bottom buttons in Menu scene!</color>");
            }
        }
    }
}
#endif
