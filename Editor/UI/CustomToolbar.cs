using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.aoyon.git_automation
{
    public class CustomToolbar
    {
        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            EditorApplication.delayCall += AddToolBar;
        }

        private static void AddToolBar()
        {
            var toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            var toolbar = toolbars.Length > 0 ? toolbars[0] : null;
            if (toolbar == null) return;

            var windowBackendProperty = toolbarType.GetProperty("windowBackend", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var windowBackend = windowBackendProperty?.GetValue(toolbar);
            if (windowBackend == null) return;

            var visualTreeProperty = windowBackend.GetType().GetProperty("visualTree", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var visualTree = visualTreeProperty?.GetValue(windowBackend) as VisualElement;
            if (visualTree == null) return;

            if (visualTree.Q("ToolbarZoneLeftAlign") is not { } leftZone) return;

            // VisualElementの追加

            var commitButton = new ToolbarButton();
            commitButton.clicked += () =>
            {
                string commitMessage = $"Manual commit";
                _ = ExecuteGitCommand.CommitAndPushAsync(commitMessage);
            };
            commitButton.Add(new Image()
            {
                image = EditorGUIUtility.IconContent("RotateTool").image 
            });
            leftZone.Add(commitButton);

            var historyButton = new ToolbarButton();
            historyButton.clicked += () =>
            {
                CommitsWindow.ShowWindow();
            };
            historyButton.Add(new Image()
            {
                image = EditorGUIUtility.IconContent("d_UnityEditor.AnimationWindow").image 
            });
            leftZone.Add(historyButton);
        }
    }
}
