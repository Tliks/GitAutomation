using UnityEditor;

namespace com.aoyon.git_automation
{
    public class GitAutomationMenuItems
    {
        [MenuItem("Tools/Git Automation/Settings")]
        public static void Settings()
        {
            GitAutomationSettingsWindow.ShowWindow();
        }
        
        [MenuItem("Tools/Git Automation/Manual Execution")]
        public static void ManualExecution()
        {
            string commitMessage = $"Manual commit";
            _ = ExecuteGitCommand.CommitAndPushAsync(commitMessage);
        }

        [MenuItem("Tools/Git Automation/Show Commit Log and Restore Window")]
        public static void ShowCommitLogandRestoreWindow()
        {
            CommitsWindow.ShowWindow();
        }

    }
}