#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace FrostDuel {
    // Explicit development command-line smoke run. Excluded from Android.
    public sealed class PreviewCapture : MonoBehaviour {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot(){if(Array.IndexOf(Environment.GetCommandLineArgs(),"-capturePreview")>=0)new GameObject("Preview capture").AddComponent<PreviewCapture>();}
        IEnumerator Start() {
            string folder=Path.GetFullPath("Builds/Preview");Directory.CreateDirectory(folder);
            yield return new WaitForSeconds(2);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"menu.png"));
            yield return new WaitForSeconds(1);
            var app=FindFirstObjectByType<DuelApp>();app.Session.StartLocal();
            yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"match.png"));
            yield return new WaitForSeconds(1);app.Session.Act(ActionKind.PrepareIce);
            yield return new WaitForSeconds(1);app.Session.Act(ActionKind.Snowball,45,.8f);
            yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"flight.png"));
            yield return new WaitForSeconds(6);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"after-shot.png"));
            yield return new WaitForSeconds(1);
            File.WriteAllText(Path.Combine(folder,"smoke.txt"),"Runtime scene boot, local match, preparation, animated throw completed. Turn="+app.Session.Display.turnId);
            Application.Quit();
        }
    }
}
#endif
