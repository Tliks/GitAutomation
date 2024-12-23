using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace com.aoyon.git_automation
{
    public class CommitsWindow : EditorWindow
    {
        private IEnumerable<Commit> commits;
        private Vector2 scrollPosition;
        private int selectedIndex = -1;

        public static void ShowWindow()
        {
            var window = GetWindow<CommitsWindow>("Git Commits Log");
            var result = ExecuteGitCommand.GetCommitLog();
            window.commits = result.Item2;
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            int index = 0;
            foreach (var commit in commits)
            {
                var style = new GUIStyle(EditorStyles.label);
                bool isSelected = index == selectedIndex;
                if (isSelected)
                {
                    style.normal.textColor = Color.white;
                    style.fontStyle = FontStyle.Bold;
                }

                Color backgroundColor = index % 2 == 0 ? new Color(0.3f, 0.3f, 0.3f, 0.2f) : new Color(0.2f, 0.2f, 0.2f, 0.2f) ;
                if (isSelected)
                {
                    backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);
                }
                
                Rect rowRect = EditorGUILayout.BeginHorizontal();
                
                EditorGUI.DrawRect(rowRect, backgroundColor);

                Rect hashRect = GUILayoutUtility.GetRect(new GUIContent(commit.Hash.Substring(0, 7)), style, GUILayout.Width(70));
                Rect dateRect = GUILayoutUtility.GetRect(new GUIContent(commit.Date), style, GUILayout.Width(120));
                Rect messageRect = GUILayoutUtility.GetRect(new GUIContent(commit.message), style);

                GUI.Label(hashRect, commit.Hash.Substring(0, 7), style);
                GUI.Label(dateRect, commit.Date, style);
                GUI.Label(messageRect, commit.message, style);

                Rect combinedRect = new Rect(rowRect.x, rowRect.y, rowRect.width, rowRect.height);
                
                if (Event.current.type == EventType.Repaint)
                {
                    if (combinedRect.Contains(Event.current.mousePosition))
                    {
                        if (!isSelected)
                        {
                            selectedIndex = index;
                            Repaint();
                        }
                    }
                    else
                    {
                        if (isSelected)
                        {
                            selectedIndex = -1;
                            Repaint();
                        }
                    }
                }
                
                if (GUI.Button(combinedRect, GUIContent.none, GUIStyle.none))
                {
                    ShowRestoreDialog(commit);
                }
                
                EditorGUILayout.EndHorizontal();
                index++;
            }

            EditorGUILayout.EndScrollView();
        }

        private void ShowRestoreDialog(Commit commit)
        {
            string message = $"以下のコミットに復元します。よろしいですか？\n" +
                             $"保存されていない変更は、復元される前に保存、コミットされます。\n\n" +
                             $"Restore to the following commit. Are you sure?\n" +
                             $"Any unsaved changes will be saved and committed before restoring.\n\n" +
                             $"Commit: {commit.Hash} {commit.Date} {commit.message}";
            if (EditorUtility.DisplayDialog("Confirm Restore", message, "OK", "Cancel"))
            {
                ExecuteGitCommand.Restore(commits.First(), commit);
                Close();
            }
        }
    }
}
