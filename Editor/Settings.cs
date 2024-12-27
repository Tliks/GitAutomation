using System.IO;
using UnityEditor;
using UnityEngine;

namespace com.aoyon.git_automation
{
    [FilePath("ProjectSettings/Packages/com.aoyon.git-automation/settings.json", FilePathAttribute.Location.ProjectFolder)]
    public class GitAutomationSettings : ScriptableSingleton<GitAutomationSettings>
    {
        // instanceの初期化はメインスレッドでのみ可能なのでここで実行しておく
        [InitializeOnLoadMethod]
        private static void Init()
        {
            _ = instance;
        }

        [SerializeField]
        private bool enableAutoCommit = false;
        [SerializeField]
        private bool enableAutoPush = false;
        [SerializeField]
        private bool enableOnSceneSaved = false;
        [SerializeField]
        private bool enableOnBuild = false;
        [SerializeField]
        private string remoteName = "origin";
        [SerializeField]
        private string workingDirectory = Directory.GetParent(Application.dataPath).FullName;

        private static void SetValue<T>(ref T field, T value)
        {
            if (!Equals(field, value))
            {
                field = value;
                instance.Save(true);
            }
        }

        public static bool EnableAutoCommit
        {
            get => instance.enableAutoCommit;
            set => SetValue(ref instance.enableAutoCommit, value);
        }

        public static bool EnableAutoPush
        {
            get => instance.enableAutoPush;
            set => SetValue(ref instance.enableAutoPush, value);
        }

        public static bool EnableOnSceneSaved
        {
            get => instance.enableOnSceneSaved;
            set => SetValue(ref instance.enableOnSceneSaved, value);
        }

        public static bool EnableOnBuild
        {
            get => instance.enableOnBuild;
            set => SetValue(ref instance.enableOnBuild, value);
        }

        public static string RemoteName
        {
            get => instance.remoteName;
            set => SetValue(ref instance.remoteName, value);
        }

        public static string WorkingDirectory
        {
            get => instance.workingDirectory;
            set => SetValue(ref instance.workingDirectory, value);
        }
    }
}