using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace com.aoyon.git_automation
{

    // Todo: 全体的にキモイ
    public static class ExecuteGitCommand
    {
        [SerializeField]
        private static bool processing = false;

        public async static Task<bool> CommitAndPushAsync(string commitMessage)
        {
            if (!await CommitAsync(commitMessage)) {
                return false;
            }

            if (!await PushAsync()) {
                return false;
            }

            return true;
        }

        public static bool CommitAndPush(string commitMessage)
        {
            if (!Commit(commitMessage)) {
                return false;
            }

            if (!Push()) {
                return false;
            }

            return true;
        }

        public static bool Commit(string commitMessage)
        {
            return CommitImpl(commitMessage, false).Result;
        }

        public static async Task<bool> CommitAsync(string commitMessage)
        {
            return await CommitImpl(commitMessage, true);
        }

        private static async Task<bool> CommitImpl(string commitMessage, bool isAsync)
        {
            AssetDatabase.Refresh();
            Utils.SaveScenesWithoutHook();

            var enableCommit = GitAutomationSettings.EnableAutoCommit;

            if (enableCommit)
            {
                // addは同期的な実行
                if (!ExecuteGitCommandBase("add ."))
                {
                    return false;
                }

                if (isAsync) {
                    if (!await ExecuteGitCommandBaseAsync($"commit -m \"{commitMessage}\""))
                    {
                        return false;
                    }
                }
                else {
                    if (!ExecuteGitCommandBase($"commit -m \"{commitMessage}\""))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool Push()
        {
            return PushImpl(false).Result;
        }

        public static async Task<bool> PushAsync()
        {
            return await PushImpl(true);
        }

        private static async Task<bool> PushImpl(bool isAsync)
        {
            var enablePush = GitAutomationSettings.EnableAutoPush;
            var remoteName = GitAutomationSettings.RemoteName;

            if (enablePush)
            {
                if (isAsync) {
                    if (!await ExecuteGitCommandBaseAsync($"push {remoteName} HEAD"))
                    {
                        return false;
                    }
                }
                else {
                    if (!ExecuteGitCommandBase($"push {remoteName} HEAD"))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static (bool, List<Commit>) GetCommitLog()
        {
            string command = $"log -n 300 --date=format:\"%Y-%m-%d %H:%M\" --pretty=format:\"%h___%ad___%s\"";
            List<Commit> commits = new();
            bool result = ExecuteGitCommandBase(command, stdoutCallback: ReceiveCommits);

            return (result, commits);

            void ReceiveCommits(object sender, DataReceivedEventArgs args)
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    var parts = args.Data.Split(new string[] { "___" }, StringSplitOptions.None);
                    if (parts.Length >= 3)
                    {
                        commits.Add(new Commit
                        {
                            Hash = parts[0],
                            Date = parts[1],
                            message = parts[2]
                        });
                    }
                }
            }
        }

        public static bool Restore(Commit src, Commit dst)
        {   
            if (!ThreadHelper.IsMainThread()) {
                throw new Exception();
            }

            UnityEngine.Debug.Log($"Restoring was started");

            string autoCommitMessage = $"Auto commit before restoring";
            // nothing to commitでもfalseを返すのでハンドリングしない
            _ = Commit(autoCommitMessage);

            using (new Utils.PreventAutoAssetRefreshScope())
            {      
                try
                {
                    if (!ExecuteGitCommandBase($"checkout {dst.Hash} -- ."))
                    {
                        throw new Exception("failed to checkout");
                    };

                    AssetDatabase.Refresh();

                    string revertCommitMessage = $"Restore to {dst.Hash}";
                    // nothing to commitは想定しづらい上に、仮にその場合はRestore自体不要
                    // よってこっちはハンドリング
                    if (!Commit(revertCommitMessage))
                    {
                        throw new Exception("faile to commit restoreing");
                    };

                    // pushは一連の実行が成功した上で最後に行う
                    // ブランチのズレを防ぐ
                    if (!Push())
                    {
                        // pushの失敗はResetを呼び出すほどではない
                        UnityEngine.Debug.LogWarning("failed to push");
                    };

                    UnityEngine.Debug.Log($"Restoring was successfully completed");
                    return true;
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError(ex);

                    UnityEngine.Debug.LogError($"Restoring was was failed, trying to reset source commit.");
                    if (!ExecuteGitCommandBase($"reset --hard {src.Hash}"))
                    {
                        UnityEngine.Debug.LogError($"failed to reset source commit.");
                    };

                    AssetDatabase.Refresh();
                    UnityEngine.Debug.LogError($"Restoring was failed and reseting was succeed, see above");
                    return false;
                }
            }
        }


        private static async Task<bool> ExecuteGitCommandBaseAsync(string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            return await ExecuteGitCommandBaseImpl(command, true, stdoutCallback, stderrCallback);
        }

        private static bool ExecuteGitCommandBase(string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            // awaitしていなのでResultを用いてもデッドロックしない
            return ExecuteGitCommandBaseImpl(command, false, stdoutCallback, stderrCallback).Result;
        }

        private static async Task<bool> ExecuteGitCommandBaseImpl(string command, bool isAsync, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            if (processing) {
                UnityEngine.Debug.Log($"Already Processing Git");
                return false;
            }

            processing = true;
            bool success = false;
            try
            {
                string workingDirectory = GitAutomationSettings.WorkingDirectory;
                var startInfo = CreateStartInfo("git", workingDirectory);

                if (isAsync)
                {
                    await Task.Run(() =>
                    {
                        ExecuteCommand(startInfo, command, stdoutCallback, stderrCallback);
                    });
                }
                else
                {
                    ExecuteCommand(startInfo, command, stdoutCallback, stderrCallback);
                }

                success = true;
            }
            catch (OperationCanceledException ex)
            {
                UnityEngine.Debug.LogWarning($"Git command execution was cancelled: {ex.Message}\n{ex.StackTrace}");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Git command execution was failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                processing = false;
            }
            return success;
        }

        private static ProcessStartInfo CreateStartInfo(string fileName, string workingDirectory)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            return startInfo;
        }

        private static void ExecuteCommand(ProcessStartInfo startInfo, string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            startInfo.Arguments = command;
            using (var process = new Process() { StartInfo = startInfo })
            {
                process.OutputDataReceived += stdoutCallback ?? DefaultOutputDataReceived;
                process.ErrorDataReceived += stderrCallback ?? DefaultErrorDataReceived;

                process.Start();
                UnityEngine.Debug.Log($"Excuted command: {command}");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // 終了まで同期的に待機
                process.WaitForExit();

                var duration = process.ExitTime - process.StartTime;
                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log($"succeed in {duration.TotalSeconds}s");
                }
                else
                {
                    throw new OperationCanceledException($"See above: {duration.TotalSeconds}s");
                }
            }

            return;

            static void DefaultOutputDataReceived(object sender, DataReceivedEventArgs args)
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    UnityEngine.Debug.Log($"stdout: {args.Data}");
                }
            }

            static void DefaultErrorDataReceived(object sender, DataReceivedEventArgs args)
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    UnityEngine.Debug.Log($"stderr: {args.Data}");
                }
            }
        }
    }
    

    public struct Commit
    {
        public string Hash;
        public string Date;
        public string message;
    }

}
