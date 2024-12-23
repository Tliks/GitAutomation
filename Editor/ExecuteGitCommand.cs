using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;

namespace com.aoyon.git_automation
{

    public static class ExecuteGitCommand
    {
        [SerializeField]
        private static bool processing = false;

        public async static Task<bool> CommitAndPushAsync(string commitMessage)
        {
            return await Task.Run(() =>
            {
                return CommitAndPush(commitMessage);
            });
        }

        public static bool CommitAndPush(string commitMessage, bool mainThread = false)
        {
            if (!Commit(commitMessage, mainThread)) {
                return false;
            }

            if (!Push(mainThread)) {
                return false;
            }

            return true;
        }

        public static bool Commit(string commitMessage, bool mainThread = false)
        {
            ThreadHelper.RefreshOnMainThread(true);

            var enableCommit = GitAutomationSettings.EnableAutoCommit;

            if (enableCommit)
            {
                // addはメインスレッドにおける実行を保証する
                if (!ExecuteGitCommandBase("add .", true))
                {
                    return false;
                }
                if (!ExecuteGitCommandBase($"commit -m \"{commitMessage}\"", mainThread))
                {
                    return false;
                }
            }
            return true;
        }

        public static bool Push(bool mainThread = false)
        {
            var enablePush = GitAutomationSettings.EnableAutoPush;
            var remoteName = GitAutomationSettings.RemoteName;

            if (enablePush)
            {
                if (!ExecuteGitCommandBase($"push {remoteName} HEAD", mainThread))
                {
                    return false;
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
            UnityEngine.Debug.Log($"Restoring was started");

            // 全てメインスレッドで実行する
            return ThreadHelper.ExecuteOnMainThread(() =>
            {
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

                        ThreadHelper.RefreshOnMainThread();

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

                        ThreadHelper.RefreshOnMainThread();
                        UnityEngine.Debug.LogError($"Restoring was failed and reseting was succeed, see above");
                        return false;
                    }
                }
            });
        }

        private static bool ExecuteGitCommandBase(string command, bool mainThread = false, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            if (processing)
            {
                UnityEngine.Debug.Log($"Already Processing Git");
                return false;
            }

            if (mainThread) {
                return ThreadHelper.ExecuteOnMainThread(() => ExecuteGitCommandBaseImpl(command, stdoutCallback, stderrCallback));
            }
            else {
                return ExecuteGitCommandBaseImpl(command, stdoutCallback, stderrCallback);
            }
        }

        private static bool ExecuteGitCommandBaseImpl(string command, DataReceivedEventHandler stdoutCallback = null, DataReceivedEventHandler stderrCallback = null)
        {
            processing = true;
            bool success = false;
            try
            {
                string workingDirectory = GitAutomationSettings.WorkingDirectory;
                var startInfo = CreateStartInfo("git", workingDirectory);

                ExecuteCommand(startInfo, command, stdoutCallback, stderrCallback);

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
