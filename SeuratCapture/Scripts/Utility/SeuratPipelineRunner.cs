using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;

namespace Seurat
{
    public class PipelineStatus
    {
        public virtual bool TaskContinuing() { return true; }

        public virtual void SetProgressBar(string message) { }

        public virtual void SetName(string name) { }

        public virtual void SendMessage(string message) { }
        public virtual void SendErrorMessage(string message) { }
        public virtual void SendInfoMessage(string message) { }
    }
    public class SeuratPipelineRunner
    {
        string args;
        string seurat_exec_path;

        protected Process process;
        protected Thread runningThread;
        protected static PipelineStatus status;

        protected bool hasStarted;
        protected bool hasFinished = false;

        public int exitCode;

        public SeuratPipelineRunner(string args, string seurat_exec_path, PipelineStatus status_interface)
        {
            this.args = args;
            this.seurat_exec_path = seurat_exec_path;
            status = status_interface;
            hasStarted = false;
            exitCode = -1;
            SetupProcess();
        }

        private void SetupProcess()
        {
            process = new Process();

            // redirect the output stream of the child process.

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.FileName = seurat_exec_path;
            process.StartInfo.Arguments = args;

            process.OutputDataReceived += OutputHandler;
            process.ErrorDataReceived += ErrorHandler;

        }

        public virtual void Run()
        {
            hasStarted = true;
            status.SendMessage("Beginning process: " + process.StartInfo.FileName + " with arguments " + process.StartInfo.Arguments);
            runningThread = new Thread(delegate () { RunProcess(status); });
            runningThread.Start();
        }

        public bool IsProcessRunning()
        {
            return hasStarted && !hasFinished;
        }

        public void InterruptProcess()
        {
            if (IsProcessRunning())
            {
                runningThread.Abort();
            }
        }

        public int RunProcess(PipelineStatus status)
        {
            try
            {
                process.Start();

                // do not wait for the child process to exit before
                // reading to the end of its redirected stream.
                // process.WaitForExit();

                // read the output stream first and then wait.
                //output = process.StandardOutput.ReadToEnd();
                //Debug.Log(output);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }
            catch (ThreadAbortException e)
            {
                process.Kill();
                status.SendMessage("Process exited by user");
            }
            catch (System.Exception e)
            {
                status.SendErrorMessage("Run error" + e.ToString()); // or throw new Exception
                exitCode = process.ExitCode;
            }
            finally
            {
                status.SendMessage("Process finished with exit code " + exitCode);
                process.Dispose();
                hasFinished = true;
                process = null;
            }
            return exitCode;
        }

        private static void OutputHandler(object sendingProcess,
                DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!string.IsNullOrEmpty(outLine.Data))
            {
                if (outLine.Data.StartsWith("INFO"))
                {
                    status.SendInfoMessage(outLine.Data);
                }
                else
                {
                    status.SetProgressBar(outLine.Data);
                }
            }
        }
        private static void ErrorHandler(object sendingProcess,
                DataReceivedEventArgs outLine)
        {
            // Collect the sort command output.
            if (!string.IsNullOrEmpty(outLine.Data))
            {
                if (outLine.Data.StartsWith("INFO"))
                {
                    status.SendInfoMessage(outLine.Data);
                }
                else
                {
                    status.SendErrorMessage(outLine.Data);
                }


            }
        }
    }
}
