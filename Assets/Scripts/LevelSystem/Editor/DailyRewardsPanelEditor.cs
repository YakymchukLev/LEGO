#if UNITY_EDITOR
using LegoPuzzle.Runtime;
using UnityEditor;
using UnityEngine;

namespace LegoPuzzle.Editor
{
    [CustomEditor(typeof(DailyRewardsPanel))]
    public class DailyRewardsPanelEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var panel = (DailyRewardsPanel)target;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Test Mode", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Test Mode One Minute Days: each reward day lasts 1 minute (or Test Day Duration Seconds). Turning it on/off in Play Mode restarts the cycle at day 1.",
                MessageType.Info);

            bool testOn = panel.TestModeOneMinuteDays;
            bool newTestOn = EditorGUILayout.ToggleLeft("Test Mode (1-minute days)", testOn);
            if (newTestOn != testOn)
                panel.TestModeOneMinuteDays = newTestOn;

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Debug / QA", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Works in Play Mode. Reset clears claims so you can take rewards again. Unlock All opens every unclaimed day as free CLAIM.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                if (GUILayout.Button("Reset Claims (claim again)", GUILayout.Height(28)))
                    panel.DebugResetClaims();

                if (GUILayout.Button("Unlock All Rewards", GUILayout.Height(28)))
                    panel.DebugUnlockAllRewards();
            }

            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play Mode to use Reset / Unlock All. Test Mode toggle can be set anytime.", MessageType.None);
        }
    }
}
#endif
