using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Debug = UnityEngine.Debug;

/// <summary>
/// A helper class for executing external processes asynchronously.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Start an external process asynchronously.
    /// </summary>
    /// <param name="startInfo">A <see cref="ProcessStartInfo"/> object.</param>
    /// <param name="token">A <see cref="CancellationToken"/> used to propagate a cancellation request.</param>
    /// <param name="timeout">The amount of time, in milliseconds, to wait for the associated process to exit. A value of 0 specifies an immediate return. A value of -1 specifies an infinite wait (default).</param>
    /// <returns>A <see cref="Results"/> object.</returns>
    /// <exception cref="TaskCanceledException">Thrown when a <see cref="CancellationTokenSource.Cancel()"/> call successfully propagates.</exception>
    public static async Task<Results> RunProcessAsync(ProcessStartInfo startInfo, CancellationToken token = default, int timeout = -1)
    {
        bool isStarted = false;

        var tcs = new TaskCompletionSource<Results>();

        if (!token.IsCancellationRequested)
        {

            using (Process process = new Process() { StartInfo = startInfo, EnableRaisingEvents = true })
            {
                try
                {
                    isStarted = process.Start();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }

                if (isStarted)
                {
                    StringBuilder stdErr = new StringBuilder();
                    StringBuilder stdOut = new StringBuilder();

                    // Register callback in the event of token cancellization
                    token.Register(() =>
                    {
                        try { process.Kill(); } catch { /* ignored */ }
                        tcs.TrySetCanceled(token);
                    });

                    // Optionally capture Standard Error
                    if (process.StartInfo.RedirectStandardError)
                    {
                        process.ErrorDataReceived += (s, e) => { if (e.Data != null) { stdErr.AppendLine(e.Data); } };

                        process.BeginErrorReadLine();
                    }

                    // Optionally capture Standard Output
                    if (process.StartInfo.RedirectStandardOutput)
                    {
                        process.OutputDataReceived += (s, e) => { if (e.Data != null) { stdOut.AppendLine(e.Data); } };

                        process.BeginOutputReadLine();
                    }

                    var timeoutTask = Task.Delay(timeout);
                    var waitForExitTask = Task.Run(() => { process.WaitForExit(); });
                    var completedFirst = await Task.WhenAny(timeoutTask, waitForExitTask);

                    // If timeout, cancel task when timeout expires.
                    // Otherwise, set result and continue.
                    if (completedFirst == timeoutTask)
                    {
                        try { process.Kill(); } catch { /* ignored */ }
                        tcs.TrySetResult(null);
                    }
                    else
                    {
                        await waitForExitTask;
                        tcs.TrySetResult(new Results(process.ExitCode, stdErr.ToString(), stdOut.ToString()));
                    }
                }
            }
        }
        else
        {
            tcs.TrySetResult(null);
        }

        // If task was successfully cancelled, throw TaskCanceledException so caller can handle it.
        if (tcs.Task.IsCanceled) { throw new TaskCanceledException(tcs.Task); }

        // If faulted, throw the last exception encountered, if any.
        // This is most likely an exception related to Process.Start() failing.
        if (tcs.Task.IsFaulted) { throw tcs.Task.Exception.InnerException; }

        // If everything went ok, return immutable Results object.
        return tcs.Task.Result;
    }
}

/// <summary>
/// A class that represents the final results from a <see cref="ProcessAyncHelper.RunProcessAsync()"/> call.
/// </summary>
public class Results
{
    /// <summary>
    /// Get the exit code from the process.
    /// </summary>
    public int ExitCode { get; }
    /// <summary>
    /// Get the Standard Error output from the process.
    /// </summary>
    public string StandardError { get; }
    /// <summary>
    /// Get the Standard Output from the process.
    /// </summary>
    public string StandardOutput { get; }

    /// <summary>
    /// Initialize a new instance of <see cref="Results"/>.
    /// </summary>
    /// <param name="exitCode">The exit code from the process.</param>
    /// <param name="standardError">The Standard Error output from the process.</param>
    /// <param name="standardOutput">The Standard Output from the process.</param>
    public Results(int exitCode, string standardError, string standardOutput)
    {
        ExitCode = exitCode;
        StandardError = standardError;
        StandardOutput = standardOutput;
    }
}