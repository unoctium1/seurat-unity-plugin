using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Reflects status updates back to CaptureWindow, and allows CaptureWindow to
// notify capture/baking tasks to cancel.
class AutomatorStatus : CaptureStatus
{
    bool task_cancelled_ = false;
    AutomateWindow bake_gui_;

    public override void SendProgress(string message, float fraction_complete)
    {
        bake_gui_.SetProgressBar(message, fraction_complete);
    }

    public override bool TaskContinuing()
    {
        return !task_cancelled_;
    }

    public void SetGUI(AutomateWindow bake_gui) { bake_gui_ = bake_gui; }

    public void CancelTask()
    {
        Debug.Log("User canceled capture processing.");
        task_cancelled_ = true;
    }
}

// Provides an interactive modeless GUI during the capture and bake process.
class AutomateWindow : EditorWindow
{
    // Defines a state machine flow for the capture and bake process.
    enum AutomateStage
    {
        kInitialization,
        kCapture,
        // This stage indicates the GUI is waiting for user to dismiss the window
        // by pressing a "Done" button.
        kWaitForDoneButton,
        kComplete,
    }

    int num_captures_;
    int curr_capture_;

    const float kTimerInterval = 0.016f;
    const int kTimerExpirationsPerCapture = 4;

    float last_time_;
    float ui_timer_ = 0.25f;
    int capture_timer_;

    string progress_message_;
    float progress_complete_;
    // The headbox component receives notification that capture is complete to
    // update the Inspector GUI, e.g. unlock the Capture button.
    CaptureHeadbox[] capture_notification_component_;
    CaptureBuilder[] monitored_capture_;
    AutomatorStatus capture_status_;

    AutomateStage bake_stage_ = AutomateStage.kInitialization;

    public void SetupStatus(AutomatorStatus capture_status)
    {
        capture_status_ = capture_status;
        capture_status_.SetGUI(this);
    }

    public void SetupCaptureProcess(CaptureHeadbox[] capture_notification_component,
      CaptureBuilder[] capture)
    {
        capture_timer_ = kTimerExpirationsPerCapture;
        bake_stage_ = AutomateStage.kCapture;
        last_time_ = Time.realtimeSinceStartup;
        capture_notification_component_ = capture_notification_component;
        num_captures_ = capture_notification_component.Length;
        monitored_capture_ = capture;
        curr_capture_ = 0;
    }

    public void SetProgressBar(string message, float fraction_complete)
    {
        progress_message_ = message;
        progress_complete_ = fraction_complete;
    }

    public void OnGUI()
    {
        // Reserve layout space for the progress bar, equal to the space for a
        // textfield:
        Rect progress_rect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(progress_rect, progress_complete_, progress_message_);
        EditorGUILayout.Space();

        if (bake_stage_ != AutomateStage.kWaitForDoneButton)
        {
            if (GUILayout.Button("Cancel"))
            {
                if (capture_status_ != null)
                {
                    capture_status_.CancelTask();
                }
            }
        }

        if (bake_stage_ == AutomateStage.kWaitForDoneButton)
        {
            if (GUILayout.Button("Done"))
            {
                bake_stage_ = AutomateStage.kComplete;
            }
        }
    }

    private bool UpdateAndCheckUiTimerReady()
    {
        bool ui_timer_ready = false;
        float delta_time = Time.realtimeSinceStartup - last_time_;
        last_time_ = Time.realtimeSinceStartup;
        ui_timer_ -= delta_time;
        if (ui_timer_ <= 0.0f)
        {
            ui_timer_ready = true;
            // Prevent the timer from going infinitely negative due to large real time
            // intervals, e.g. from slow frame capture rendering.
            if (ui_timer_ <= -kTimerInterval)
            {
                ui_timer_ = 0.0f;
            }
            ui_timer_ += kTimerInterval;
        }
        return ui_timer_ready;
    }

    public void Update()
    {
        if (capture_status_ != null && capture_status_.TaskContinuing() && !UpdateAndCheckUiTimerReady())
        {
            return;
        }

        // Refresh the Editor GUI to finish the task.
        EditorUtility.SetDirty(capture_notification_component_[curr_capture_]);

        if (bake_stage_ == AutomateStage.kCapture)
        {
            --capture_timer_;
            if (capture_timer_ == 0)
            {
                capture_timer_ = kTimerExpirationsPerCapture;

                monitored_capture_[curr_capture_].RunCapture();

                if (monitored_capture_[curr_capture_].IsCaptureComplete() &&
                  capture_status_.TaskContinuing())
                {
                    monitored_capture_[curr_capture_].EndCapture();
                    monitored_capture_[curr_capture_] = null;
                    if (curr_capture_ == (num_captures_ - 1))
                    {
                        Debug.Log("Finished");
                        bake_stage_ = AutomateStage.kWaitForDoneButton;
                    }
                    else
                    {
                        capture_status_.SendProgress("Beginning capture " + (curr_capture_+1), (curr_capture_+1)/num_captures_);
                        Debug.Log("Moving on to capture " + (curr_capture_ + 1));
                        curr_capture_++;
                    }
                }
            }

            if (capture_status_ != null && !capture_status_.TaskContinuing())
            {
                bake_stage_ = AutomateStage.kComplete;
                if (monitored_capture_ != null)
                {
                    for (int i = 0; i < num_captures_; i++)
                    {
                        if (monitored_capture_[i] != null)
                        {
                            monitored_capture_[i].EndCapture();
                            monitored_capture_[i] = null;
                        }
                    }
                }
            }
        }

        // Repaint with updated progress the GUI on each wall-clock time tick.
        Repaint();
    }

    public bool IsComplete()
    {
        return bake_stage_ == AutomateStage.kComplete;
    }
};


// Implements the Capture Headbox component Editor panel.
[CustomEditor(typeof(SeuratAutomator))]
public class SeuratAutomatorEditor : Editor
{
    public static readonly string kSeuratCaptureDir = "SeuratCapture";

    SerializedProperty output_folder_;
    SerializedProperty exec_path_;
    SerializedProperty samples_;
    SerializedProperty center_resolution_;
    SerializedProperty resolution_;
    SerializedProperty dynamic_range_;
    SerializedProperty override_all_;

    AutomatorStatus capture_status_;
    AutomateWindow bake_progress_window_;
    CaptureBuilder[] capture_builder_;

    void OnEnable()
    {
        output_folder_ = serializedObject.FindProperty("output_folder_");
        exec_path_ = serializedObject.FindProperty("seurat_executable_path_");
        samples_ = serializedObject.FindProperty("samples_per_face_");
        center_resolution_ = serializedObject.FindProperty("center_resolution_");
        resolution_ = serializedObject.FindProperty("resolution_");
        dynamic_range_ = serializedObject.FindProperty("dynamic_range_");
        override_all_ = serializedObject.FindProperty("override_all_");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(output_folder_, new GUIContent(
          "Output Folder"));
        if (GUILayout.Button("Choose Output Folder"))
        {
            string path = EditorUtility.SaveFolderPanel(
              "Choose Capture Output Folder", output_folder_.stringValue, "");
            if (path.Length != 0)
            {
                output_folder_.stringValue = path;
            }
        }

        EditorGUILayout.PropertyField(exec_path_, new GUIContent(
         "Executable Path"));
        if (GUILayout.Button("Choose Executable Location"))
        {
            string path = EditorUtility.OpenFilePanel(
                "Choose Seurat Executable Location", Application.dataPath, "exe");
            if (path.Length != 0)
            {
                exec_path_.stringValue = path;
            }
        }

        EditorGUILayout.PropertyField(override_all_, new GUIContent(
            "Override All"));

        if (override_all_.boolValue)
        {

            EditorGUILayout.PropertyField(samples_, new GUIContent("Sample Count"));
            EditorGUILayout.PropertyField(center_resolution_, new GUIContent(
              "Center Capture Resolution"));
            EditorGUILayout.PropertyField(resolution_, new GUIContent(
              "Default Resolution"));
            EditorGUILayout.PropertyField(dynamic_range_, new GUIContent(
              "Dynamic Range"));
        }

        if (capture_status_ != null)
        {
            GUI.enabled = false;
        }
        if (GUILayout.Button("Capture All"))
        {
            Capture();
        }
        GUI.enabled = true;
        if (exec_path_.stringValue.Length == 0)
        {
            GUI.enabled = false;
        }
        if (GUILayout.Button("Run Seurat"))
        {
            RunSeurat();
        }
        GUI.enabled = true;
        

        serializedObject.ApplyModifiedProperties();

        if (bake_progress_window_ != null && bake_progress_window_.IsComplete())
        {
            bake_progress_window_.Close();
            bake_progress_window_ = null;
            capture_builder_ = null;
            capture_status_ = null;
        }
    }

    public void Capture()
    {
        SeuratAutomator automator = (SeuratAutomator)target;

        string capture_output_folder = automator.output_folder_;
        if (capture_output_folder.Length <= 0)
        {
            capture_output_folder = FileUtil.GetUniqueTempPathInProject();
        }
        Directory.CreateDirectory(capture_output_folder);

        int numCaptures = automator.transform.childCount;

        capture_status_ = new AutomatorStatus();
        capture_builder_ = new CaptureBuilder[numCaptures];
        CaptureHeadbox[] headboxes = new CaptureHeadbox[numCaptures];
        bake_progress_window_ = (AutomateWindow)EditorWindow.GetWindow(typeof(AutomateWindow));
        bake_progress_window_.SetupStatus(capture_status_);

        for (int i = 0; i < numCaptures; i++)
        {
            capture_builder_[i] = new CaptureBuilder();
            headboxes[i] = automator.transform.GetChild(i).GetComponent<CaptureHeadbox>();
            automator.OverrideHeadbox(headboxes[i]);
            string output = capture_output_folder + "\\" + (i+1);
            headboxes[i].output_folder_ = output;
            Directory.CreateDirectory(output);
            capture_builder_[i].BeginCapture(headboxes[i], output, 1, capture_status_, "Capture " + (i+1) +": ");
        }

        bake_progress_window_.SetupCaptureProcess(headboxes, capture_builder_);

    }

    public void RunSeurat()
    {
        SeuratAutomator automate = (SeuratAutomator)target;
        string capture_output_folder = automate.output_folder_;
        string exec_path = automate.seurat_executable_path_;
        int numCaptures = automate.transform.childCount;
        var thread = new Thread(delegate () { SeuratProcess(numCaptures, capture_output_folder, exec_path); });
        thread.Start();
    }

    public static void SeuratProcess(int number_captures, string output_folder, string exec_path)
    {
        for (int i = 1; i <= number_captures; i++)
        {
            Debug.Log("Beginning processing of capture " + i);
            Process process = new Process();

            // redirect the output stream of the child process.
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.FileName = exec_path;
            string input_path = Path.Combine(output_folder, i.ToString(), "input", "manifest.json");
            string output_path = Path.Combine(output_folder, "output_" + i);
            process.StartInfo.Arguments = "-input_path=" + input_path + " -output_path=" + output_path + " -premultiply_alpha=false";

            process.OutputDataReceived += OutputHandler;

            int exitCode = -1;
            //string output = null;

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
                process.WaitForExit();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Run error" + e.ToString()); // or throw new Exception
            }
            finally
            {
                exitCode = process.ExitCode;

                process.Dispose();
                process = null;
                Debug.Log("Finished processing capture " + i + " with exit code " + exitCode);
            }
        }
    }

    private static void OutputHandler(object sendingProcess,
            DataReceivedEventArgs outLine)
    {
        // Collect the sort command output.
        if (!string.IsNullOrEmpty(outLine.Data))
        {
            Debug.Log(outLine.Data);
        }
    }
}
