using UnityEditor;
using UnityEngine;
using S = com.aoyon.git_automation.GitAutomationSettings;

namespace com.aoyon.git_automation
{
    public class GitAutomationSettingsWindow : EditorWindow
    {
        public static void ShowWindow()
        {
            GetWindow<GitAutomationSettingsWindow>("Git Automation Settings");
        }

        private void OnGUI()
        {
            GUILayout.Label("Git Automation Settings", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            S.WorkingDirectory = EditorGUILayout.TextField("Git Root Directory", S.WorkingDirectory);
            if (GUILayout.Button("Select Folder"))
            {
                string path = EditorUtility.OpenFolderPanel("Select Directory", S.WorkingDirectory, "");
                if (!string.IsNullOrEmpty(path))
                {
                    S.WorkingDirectory = path;
                }
            }

            EditorGUILayout.Space();

            S.EnableOnSceneSaved = EditorGUILayout.Toggle("Enabled On Scene Saved", S.EnableOnSceneSaved);
            S.EnableOnBuild = EditorGUILayout.Toggle("Enabled On Build", S.EnableOnBuild);

            EditorGUILayout.Space();

            S.EnableAutoCommit = EditorGUILayout.Toggle("Enable Auto Commit", S.EnableAutoCommit);
            S.EnableAutoPush = EditorGUILayout.Toggle("Enable Auto Push", S.EnableAutoPush);

            EditorGUILayout.Space();
            
            GUI.enabled = S.EnableAutoPush;
            S.RemoteName = EditorGUILayout.TextField("Remote Name", S.RemoteName);
            GUI.enabled = true;
        }
    }
}