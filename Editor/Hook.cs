using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#if GA_VRCSDK_BASE
using VRC.SDKBase.Editor.BuildPipeline;
#endif

namespace com.aoyon.git_automation
{
    public class Hook
    {
        public class OnScene
        {   
            public static bool IsRegistered = false;

            [InitializeOnLoadMethod]
            private static void Init()
            {
                Register();
            }

            public static void Register()
            {
                if (!IsRegistered)
                {
                    EditorSceneManager.sceneSaved += Process;
                    IsRegistered = true;
                }
            }

            public static void UnRegister()
            {
                if (IsRegistered)
                {
                    EditorSceneManager.sceneSaved -= Process;
                    IsRegistered = false;
                }
            }

            static async void Process(Scene scene)
            {
                if (GitAutomationSettings.EnableOnSceneSaved)
                {
                    string commitMessage = $"Auto commit on Scene Saved";
                    _ = await ExecuteGitCommand.TryCommitAndPushAsync(commitMessage);
                }
            }
        }

        public class OnBuild
#if GA_VRCSDK_BASE
            : IVRCSDKBuildRequestedCallback
#else
            : IPreprocessBuildWithReport
#endif
        {
            public int callbackOrder => -int.MaxValue;

#if GA_VRCSDK_BASE
            public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
            {
                Process();
                return true;
            }
#else
            public void OnPreprocessBuild(BuildReport report)
            {
                Process();
            }
#endif

            static void Process()
            {
                if (GitAutomationSettings.EnableOnBuild)
                {
                    string commitMessage = $"Auto commit on Build";
                    _ = ExecuteGitCommand.TryCommitAndPushAsync(commitMessage);
                }
            }
        }

        private static void OnBeforeBuild()
            {
            if (GitAutomationSettings.EnableOnBuild)
            {
                string commitMessage = $"Auto commit on Build";
                _ = ExecuteGitCommand.CommitAndPushAsync(commitMessage);
            }
        }
    }
}