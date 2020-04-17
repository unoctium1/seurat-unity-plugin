using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Seurat
{
    public class SeuratPipelineCollectionRunner : SeuratPipelineRunner
    {
        SeuratPipelineRunner[] toRun;
        int[] exitCodes;

        //toRun should be constructed beforehand
        public SeuratPipelineCollectionRunner(SeuratPipelineRunner[] toRun, PipelineStatus status_interface) : base("", "", status_interface)
        {
            this.toRun = toRun;
            process = null;
            exitCodes = new int[toRun.Length];
            for (int i = 0; i < exitCodes.Length; i++)
            {
                exitCodes[i] = -1;
            }
        }

        public override void Run()
        {
            hasStarted = true;
            status.SendMessage("Running seurat process on " + toRun.Length + " captures");
            runningThread = new Thread(delegate () { RunAll(); });
            runningThread.Start();
        }

        private void RunAll()
        {
            int i = 0;
            //float startTime = Time.realtimeSinceStartup;
            try
            {
                while (i < toRun.Length)
                {
                    status.SetName("Capture " + (i + 1) + " - ");
                    if (toRun[i] != null)
                    {
                        status.SendMessage("Begin processing capture");
                        exitCodes[i] = toRun[i].RunProcess(status);
                        status.SendMessage("Finish processing capture");
                    }
                    else
                    {
                        status.SendInfoMessage("Inactive in hierarchy, skipping...");
                    }
                    i++;
                }
            }
            catch (ThreadAbortException e)
            {
                status.SendMessage("User exited early, process finished during capture " + (i + 1) + " out of " + toRun.Length);
            }
            finally
            {
                //float totalRunTime = Time.realtimeSinceStartup - startTime;
                status.SetName("");
                for (int j = 0; j < toRun.Length; j++)
                {
                    status.SendMessage("Process " + (j + 1) + " finished with exit code " + exitCodes[j]);
                }
                //int minutes = (int)(totalRunTime / 60);
                //float seconds = totalRunTime - (minutes * 60);
                //
                //status.SendMessage("Process finished in " + minutes + " minutes and " + seconds + " seconds");
                hasFinished = true;
            }

        }
    }
}
