using System;
using UnityEditor;

namespace HarvestPanic.AI.Editor
{
    /// <summary>
    /// Import rules for the CC0 Quaternius animals under <c>Assets/ThirdParty/Animals/</c>:
    /// Generic rig (not Humanoid), animations imported, locomotion/idle clips looped, no cameras/lights.
    /// The FBX files themselves stay unmodified (third-party rule); only importer settings change.
    /// </summary>
    public sealed class AnimalModelPostprocessor : AssetPostprocessor
    {
        /// <summary>Root folder the rules apply to.</summary>
        public const string AnimalsRoot = "Assets/ThirdParty/Animals/";

        static readonly string[] LoopKeywords = { "Idle", "Walk", "Run", "Gallop", "Eating", "Peck", "Jump_Loop", "Swim", "Fly" };

        /// <summary>True for clip names that should loop (idle, locomotion).</summary>
        public static bool ShouldLoop(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return false;
            foreach (var k in LoopKeywords)
                if (clipName.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(AnimalsRoot, StringComparison.Ordinal)) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importBlendShapes = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimalsRoot, StringComparison.Ordinal)) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                bool loop = ShouldLoop(c.name);
                c.loopTime = loop;
                c.loopPose = loop;
                // Root motion is not used: NavMeshAgent drives position; bake root into pose.
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalPositionY = true;
            }
            mi.clipAnimations = clips;
        }
    }
}
