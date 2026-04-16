using System;
using NUnit.Framework.Interfaces;
using UnityEngine.TestTools;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework.Internal.Builders;
using NUnit.Framework;
using UnityEngine.TestTools.Graphics;
using UnityEngine.Scripting;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.TestTools.Graphics.TestCases;

#if UNITY_EDITOR
using UnityEditor;
using System.Linq;
using UnityEditor.TestTools.Graphics;
#endif

namespace UnityEngine.VFX.PerformanceTest
{
    public class VfxPerformanceGraphicsTestAttribute : SceneGraphicsTestAttribute
    {
        public VfxPerformanceGraphicsTestAttribute(params string[] scenePaths) : base(typeof(VFXPerformanceGraphicsTestCaseSource), scenePaths) { }
    }

    public class VFXPerformanceGraphicsTestCaseSource : SceneGraphicsTestCaseSource
    {
        public static string GetPrefix()
        {
            //Can't use SRPBinder here, this code is also runtime
            var currentSRP = QualitySettings.renderPipeline ?? GraphicsSettings.currentRenderPipeline;
            if (currentSRP == null)
                return "BRP";
            if (currentSRP.name.Contains("HDRenderPipeline"))
                return "HDRP";
            return currentSRP.name;
        }

        static readonly string[] kVisualEffectAssetSelectionForPerformance = new string[]
        {
            "009_MultiCamera",
            "009_ReadAttributeInSpawner",
            "015_FixedTime",
            "018_CollisionScaledPrimitive",
            "025_Flipbook",
            "025_ShaderKeywords",
            "026_InstancingGPUevents",
            "028_BaseColorMap",
            "08_Shadows",
            "100_Fog",
            "104_ShaderGraphGenerationFTP",
            "105_MotionVectors",
            "106_URPDecals",
            "106_URPDecalsReceiver",
            "24_MotionVector",
            "28_CameraProject",
            "35_ShaderGraphGenerationFTP",
            "38_SortingKeys",
            "39_SmokeLighting",
            "40_InstancingSplitCompute",
            "43_OddNegativeScale",
            "Collision",
            "HDRP_VolumetricOutput",
            "Instancing",
            "SimpleLit",
            "SubgraphContexts",
            "Timeline"
        };

        public override IEnumerable<GraphicsTestCase> GetTestCases(IMethodInfo methodInfo, ITest suite)
        {
            var testCases = base.GetTestCases(methodInfo, suite);
            foreach (var testCase in testCases)
            {
                bool found = false;
                foreach (var filteredAsset in kVisualEffectAssetSelectionForPerformance)
                {
                    if (testCase.Name.Contains(filteredAsset, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    yield return testCase with
                    {
                        Name = GetPrefix() + "." + testCase.Name,
                        FullName = testCase.FullName.Replace(testCase.Name, GetPrefix() + "." + testCase.Name),
                    };
                }
            }
        }
    }
}
