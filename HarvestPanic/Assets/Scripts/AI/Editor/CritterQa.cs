using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarvestPanic.AI.Editor
{
    /// <summary>
    /// Offscreen QA capture of the critters in CritterTest (no window; run WITHOUT -nographics):
    /// <c>Unity -batchmode -quit -projectPath HarvestPanic -executeMethod HarvestPanic.AI.Editor.CritterQa.Capture -qaOut /abs/dir</c>.
    /// Places preview instances (not saved), poses them with their Idle clip, adds 1.10 m and 0.33 m reference bars.
    /// </summary>
    public static class CritterQa
    {
        public static void Capture()
        {
            string outDir = Arg("-qaOut") ?? Path.GetFullPath("Logs");
            EditorSceneManager.OpenScene(CritterAssetBuilder.ScenePath, OpenSceneMode.Single);
            Content.Critters.Invalidate();
            Content.Critters.TryGet("Critter.Pig", out var pig);
            Content.Critters.TryGet("Critter.Chicken", out var chicken);

            Place(pig.Prefab, new Vector3(0f, 0f, 0f), 200f);
            for (int i = 0; i < 6; i++)
                Place(chicken.Prefab, new Vector3(2.2f + (i % 3) * 0.6f, 0f, 0.4f + (i / 3) * 0.7f), 150f + i * 35f);
            var bar = Bar(new Vector3(-0.9f, 0f, 0f), new Vector3(0.05f, 0.05f, 1.10f), Color.red);       // pig length reference
            var bar2 = Bar(new Vector3(1.8f, 0f, 1.2f), new Vector3(0.04f, 0.33f, 0.04f), Color.yellow);   // chicken height reference

            var cam = Camera.main;
            bool ok = QaScreenshot.Capture(cam, 1280, 720, Path.Combine(outDir, "critters_wide.png"));
            cam.transform.position = new Vector3(1.3f, 1.4f, -3.2f);
            cam.transform.LookAt(new Vector3(1.2f, 0.3f, 0.4f));
            ok &= QaScreenshot.Capture(cam, 1280, 720, Path.Combine(outDir, "critters_close.png"));
            cam.transform.position = new Vector3(-4.5f, 0.5f, 0.0f);
            cam.transform.LookAt(new Vector3(0.5f, 0.35f, 0.3f));
            ok &= QaScreenshot.Capture(cam, 1280, 720, Path.Combine(outDir, "critters_side.png"));
            Debug.Log($"[CritterQa] ok={ok} -> {outDir}");
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        static void Place(GameObject prefab, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.animationClips.Length > 0)
                anim.runtimeAnimatorController.animationClips[0].SampleAnimation(anim.gameObject, 0.3f);
        }

        static GameObject Bar(Vector3 pos, Vector3 size, Color c)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.transform.position = pos + Vector3.up * size.y * 0.5f;
            g.transform.localScale = size;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
