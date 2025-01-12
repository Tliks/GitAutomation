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
        public static bool processing = false;

        public async static Task<bool> TryCommitAndPushAsync(string commitMessage)
        {
            if (!await TryCommitAsync(commitMessage)) {
                return false;
            }

            if (!await TryPushAsync()) {
                return false;
            }

            return true;
        }

        public static bool TryCommitAndPush(string commitMessage)
        {
            if (!TryCommit(commitMessage)) {
                return false;
            }

            if (!TryPush()) {
                return false;
            }

            return true;
        }

        public static bool TryCommit(string commitMessage)
        {
            return TryCommitImpl(commitMessage, false).Result;
        }

        public static async Task<bool> TryCommitAsync(string commitMessage)
        {
            return await TryCommitImpl(commitMessage, true);
        }

        private static async Task<bool> TryCommitImpl(string commitMessage, bool isAsync)
        {
            AssetDatabase.Refresh();
            Utils.SaveScenesWithoutHook();

            var enableCommit = GitAutomationSettings.EnableAutoCommit;

            if (enableCommit)
            {
                // addは同期的な実行
                if (!TryExecuteGitCommand("add ."))
                {
                    return false;
                }

                if (isAsync) {
                    if (!await TryExecuteGitCommandAsync($"commit -m \"{commitMessage}\""))
                    {
                        return false;
                    }
                }
                else {
                    if (!TryExecuteGitCommand($"commit -m \"{commitMessage}\""))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static bool TryPush()
        {
            return TryPushImpl(false).Result;
        }

        public static async Task<bool> TryPushAsync()
        {
            return await TryPushImpl(true);
        }

        private static async Task<bool> TryPushImpl(bool isAsync)
        {
            var enablePush = GitAutomationSettings.EnableAutoPush;
            var remoteName = GitAutomationSettings.RemoteName;

            if (enablePush)
            {
                if (isAsync) {
                    if (!await TryExecuteGitCommandAsync($"push {remoteName} HEAD"))
                    {
                        return false;
                    }
                }
                else {
                    if (!TryExecuteGitCommand($"push {remoteName} HEAD"))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static (bool, List<Commit>) TryGetCommitLog()
        {
            string command = $"log -n 300 --date=format:\"%Y-%m-%d %H:%M\" --pretty=format:\"%h___%ad___%s\"";
            List<Commit> commits = new();
            bool result = TryExecuteGitCommand(command, stdoutCallback: ReceiveCommits);

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

        public static bool TryRestore(Commit src, Commit dst)
        {   
            if (!ThreadHelper.IsMainThread()) {
                throw new Exception();
            }

            UnityEngine.Debug.Log($"Restoring was started");

            string autoCommitMessage = $"Auto commit before restoring";
            // nothing to commitでもfalseを返すのでハンドリングしない
            _ = TryCommit(autoCommitMessage);

            using (new Utils.PreventAutoAssetRefreshScope())
            {      
                try
                {
                    if (!TryExecuteGitCommand($"checkout {dst.Hash} -- ."))
                    {
                        throw new Exception("failed to checkout");
                    };

                    AssetDatabase.Refresh();

                    string revertCommitMessage = $"Restore to {dst.Hash}";
                    // nothing to commitは想定しづらい上に、仮にその場合はRestore自体不要
                    // よってこっちはハンドリング
                    if (!TryCommit(revertCommitMessage))
                    {
                        throw new Exception("faile to commit restoreing");
                    };

                    // pushは一連の実行が成功した上で最後に行う
                    // ブランチのズレを防ぐ
                    if (!TryPush())
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
                    if (!TryExecuteGitCommand($"reset --hard {src.Hash}"))
                    {
                        UnityEngine.Debug.LogError($"failed to reset source commit.");
                    };

                    AssetDatabase.Refresh();
                    UnityEngine.Debug.LogError($"Restoring was failed and reseting was succeed, see above");
                    return false;
                }
            }
        }


        private static async Task<bool> TryExecuteGitCommandAsync(string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            return await TryExecuteGitCommandImpl(command, true, stdoutCallback, stderrCallback);
        }

        private static bool TryExecuteGitCommand(string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            // awaitしていなのでResultを用いてもデッドロックしない
            return TryExecuteGitCommandImpl(command, false, stdoutCallback, stderrCallback).Result;
        }

        private static async Task<bool> TryExecuteGitCommandImpl(string command, bool isAsync, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            if (processing) {
                UnityEngine.Debug.Log($"Already Processing Git");
                return false;
            }

            processing = true;
            bool success = false;
            try
            {
                string fileName = "git";
                string workingDirectory = GitAutomationSettings.WorkingDirectory;

                if (isAsync)
                {
                    await Task.Run(() =>
                    {
                        ExecuteCommand(fileName, workingDirectory, command, stdoutCallback, stderrCallback);
                    });
                }
                else
                {
                    ExecuteCommand(fileName, workingDirectory, command, stdoutCallback, stderrCallback);
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

        private static void ExecuteCommand(string fileName, string workingDirectory, string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process() { StartInfo = startInfo })
            {
                process.OutputDataReceived += stdoutCallback ?? DefaultOutputDataReceived;
                process.ErrorDataReceived += stderrCallback ?? DefaultErrorDataReceived;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                UnityEngine.Debug.Log($"Excuted command ({fileName}): {command}");

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
