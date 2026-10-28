using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace FrostDuel.Editor {
    public static class ProjectBuilder {
        [MenuItem("Frost Duel/Configure project")]
        public static void Configure() {
            Directory.CreateDirectory("Assets/Scenes");Directory.CreateDirectory("Assets/Resources");Directory.CreateDirectory("Builds");
            var balance=AssetDatabase.LoadAssetAtPath<BalanceSettings>("Assets/Resources/Balance.asset");
            if(balance==null){balance=ScriptableObject.CreateInstance<BalanceSettings>();AssetDatabase.CreateAsset(balance,"Assets/Resources/Balance.asset");}
            if(!File.Exists("Assets/Scenes/Duel.unity")) {var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.SaveScene(scene,"Assets/Scenes/Duel.unity");}
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Duel.unity",true)};
            PlayerSettings.companyName="Coursework";PlayerSettings.productName="Снежная дуэль";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"by.psu.frostduel");
            PlayerSettings.bundleVersion="0.3.0";PlayerSettings.Android.bundleVersionCode=3;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
            PlayerSettings.Android.minSdkVersion=(AndroidSdkVersions)27;PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)35;
            PlayerSettings.Android.applicationEntry=AndroidApplicationEntry.Activity;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64|AndroidArchitecture.ARMv7;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Minimal);
            PlayerSettings.Android.useCustomKeystore=false;PlayerSettings.Android.forceInternetPermission=false;
            PlayerSettings.runInBackground=true;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3});
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=0;settings.ApplyModifiedPropertiesWithoutUndo();}
            EditorSettings.serializationMode=SerializationMode.ForceText;
            AssetDatabase.SaveAssets();Debug.Log("FROST: Project configured");
        }
        [MenuItem("Frost Duel/Build Android APK")]
        public static void Android() {
            Configure();EditorUserBuildSettings.buildAppBundle=false;
            Build(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Duel.unity"},locationPathName="Builds/FrostDuel.apk",target=BuildTarget.Android,options=BuildOptions.None});
        }
        public static void VerifyAll() { CoreVerification.Run();ProtocolVerification.Run();AimVerification.Run();ViewVerification.Run(); }
        public static void VerifyAndLinux() { VerifyAll();Linux(); }
        public static void Linux() {
            Configure();Build(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Duel.unity"},locationPathName="Builds/Linux/FrostDuel",target=BuildTarget.StandaloneLinux64,options=BuildOptions.Development});
        }
        static void Build(BuildPlayerOptions options) {
            var report=BuildPipeline.BuildPlayer(options);
            File.WriteAllText("Builds/last-build.txt",report.summary.result+"\n"+report.summary.totalErrors+" errors\n"+report.summary.totalWarnings+" warnings\n"+report.summary.totalSize+" bytes\n");
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("FROST: BUILD SUCCESS "+options.locationPathName);
        }
    }
}
