using System;
using System.Threading;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;


namespace com.aoyon.git_automation
{
    public class Utils
    {   
        // 0: disabled, 1: enabled, 2: enableoutsideplaymode
        public static int AutoAssetRefreshPref
        {
            get => EditorPrefs.GetInt("kAutoRefreshMode", 1);
            set => EditorPrefs.SetInt("kAutoRefreshMode", value);
        }

        public class PreventAutoAssetRefreshScope : IDisposable
        {   
            [SerializeField]
            private readonly int _currentValue;

            public PreventAutoAssetRefreshScope()
            {
                _currentValue = AutoAssetRefreshPref;
                AutoAssetRefreshPref = 0;
            }

            public void Dispose()
            {
                AutoAssetRefreshPref = _currentValue;
            }
        }

        public class PreventAutoSceneCommitScope : IDisposable
        {   
            [SerializeField]
            private readonly bool _wasRegistered;

            public PreventAutoSceneCommitScope()
            {
                _wasRegistered = Hook.OnScene.IsRegistered;
                if (_wasRegistered)
                {
                    Hook.OnScene.UnRegister();
                }
            }

            public void Dispose()
            {
                if (_wasRegistered)
                {
                    Hook.OnScene.Register();
                }
            }
        }
        
        public static void SaveScenesWithoutHook()
        {
            using (new PreventAutoSceneCommitScope())
            {
                EditorSceneManager.SaveOpenScenes();
            }
        }
    }

    public class ThreadHelper
    {
        private static SynchronizationContext _mainThreadContext;
        private static int _mainThreadId;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            _mainThreadContext = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        public static bool ExecuteOnMainThread(Func<bool> action)
        {
            if (IsMainThread())
            {
                return action();
            }

            bool result = false;
            _mainThreadContext.Send(_ =>
            {
                result = action();
            }, null);

            return result;
        }

        public static void RefreshOnMainThread(bool saveScene = false)
        {
            ExecuteOnMainThread(() =>
            {
                AssetDatabase.Refresh();
                if (saveScene) Utils.SaveScenesWithoutHook();
                return true;
            });
        }

    }
}
