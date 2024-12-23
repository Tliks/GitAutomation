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
            [SerializeField]
            private static bool _isRegistered = false;

            [InitializeOnLoadMethod]
            private static void Init()
            {
                Register();
            }

            public static void Register()
            {
                if (!_isRegistered)
                {
                    EditorSceneManager.sceneSaved += OnSceneSaved;
                    _isRegistered = true;
                }
            }

            public static void UnRegister()
            {
                if (_isRegistered)
                {
                    EditorSceneManager.sceneSaved -= OnSceneSaved;
                    _isRegistered = false;
                }
            }

            public static bool IsRegistered()
            {
                return _isRegistered;
            }
        }

#if GA_VRCSDK_BASE
        public class OnBuild : IVRCSDKBuildRequestedCallback
        {   
            public int callbackOrder => -int.MaxValue;

            public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
            {   
                OnBeforeBuild();
                return true;
            }

        }
#else
        public class OnBuild : IPreprocessBuildWithReport
        {   
            public int callbackOrder => -int.MaxValue;

            public void OnPreprocessBuild(BuildReport report)
            {
                OnBeforeBuild();
            }
        }
#endif

        private static void OnSceneSaved(Scene scene)
        {
            if (GitAutomationSettings.EnableOnSceneSaved)
            {
                string commitMessage = $"Auto commit on Scene Saved";
                _ = ExecuteGitCommand.CommitAndPushAsync(commitMessage);
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