using UnityEngine.Experimental.GlobalIllumination;
using Unity.Collections;

namespace UnityEngine.Rendering.HighDefinition
{
    internal static class GlobalIlluminationUtils
    {
        // Return true if the light must be added to the baking
        public static bool LightDataGIExtract(Light light, ref LightDataGI lightDataGI)
        {
            if (!light.TryGetComponent<HDAdditionalLightData>(out var add))
            {
                add = HDUtils.s_DefaultHDAdditionalLightData;
            }

            Cookie cookie;
            LightmapperUtils.Extract(light, out cookie);
            lightDataGI.cookieTextureEntityId = cookie.entityId;
            lightDataGI.cookieScale = cookie.scale;

            Color cct = new Color(1.0f, 1.0f, 1.0f);

            if (add.useColorTemperature)
                cct = Mathf.CorrelatedColorTemperatureToRGB(light.colorTemperature);

#if UNITY_EDITOR
            LightMode lightMode = LightmapperUtils.Extract(light.lightmapBakeType);
#else
            LightMode lightMode = LightmapperUtils.Extract(light.bakingOutput.lightmapBakeType);
#endif

            float lightDimmer = 1;

            if (lightMode == LightMode.Realtime || lightMode == LightMode.Mixed)
                lightDimmer = add.lightDimmer;

            lightDataGI.entityId = light.GetEntityId();
            LinearColor directColor, indirectColor;
            directColor = add.affectDiffuse ? LinearColor.Convert(light.color, light.intensity) : LinearColor.Black();
            directColor.red *= cct.r;
            directColor.green *= cct.g;
            directColor.blue *= cct.b;
            directColor.intensity *= lightDimmer;
            indirectColor = add.affectDiffuse ? LightmapperUtils.ExtractIndirect(light) : LinearColor.Black();
            indirectColor.red *= cct.r;
            indirectColor.green *= cct.g;
            indirectColor.blue *= cct.b;
            indirectColor.intensity *= lightDimmer;

            lightDataGI.color = directColor;
            lightDataGI.indirectColor = indirectColor;

            if (add.interactsWithSky)
            {
                var staticSkySettings = SkyManager.GetStaticLightingSky()?.skySettings;
                if (staticSkySettings != null)
                {
                    Vector3 atmosphericAttenuation = staticSkySettings.EvaluateAtmosphericAttenuation(-light.transform.forward, Vector3.zero);
                    lightDataGI.color.red *= atmosphericAttenuation.x;
                    lightDataGI.color.green *= atmosphericAttenuation.y;
                    lightDataGI.color.blue *= atmosphericAttenuation.z;

                    lightDataGI.indirectColor.red *= atmosphericAttenuation.x;
                    lightDataGI.indirectColor.green *= atmosphericAttenuation.y;
                    lightDataGI.indirectColor.blue *= atmosphericAttenuation.z;
                }
            }

            // Note that the HDRI is correctly integrated in the GlobalIllumination system, we don't need to do anything regarding it.

            // The difference is that `l.lightmapBakeType` is the intent, e.g.you want a mixed light with shadowmask. But then the overlap test might detect more than 4 overlapping volumes and force a light to fallback to baked.
            // In that case `l.bakingOutput.lightmapBakeType` would be baked, instead of mixed, whereas `l.lightmapBakeType` would still be mixed. But this difference is only relevant in editor builds
#if UNITY_EDITOR
            lightDataGI.mode = LightmapperUtils.Extract(light.lightmapBakeType);
#else
            lightDataGI.mode = LightmapperUtils.Extract(light.bakingOutput.lightmapBakeType);
#endif

            lightDataGI.shadow = (byte)(light.shadows != LightShadows.None ? 1 : 0);

            LightType lightType = add.legacyLight.type;
            if (!lightType.IsArea())
            {
                // For HDRP we need to divide the analytic light color by PI (HDRP do explicit PI division for Lambert, but built in Unity and the GI don't for punctual lights)
                // We apply it on both direct and indirect are they are separated, seems that direct is no used if we used mixed mode with indirect or shadowmask bake.
                lightDataGI.color.intensity /= Mathf.PI;
                lightDataGI.indirectColor.intensity /= Mathf.PI;
                directColor.intensity /= Mathf.PI;
                indirectColor.intensity /= Mathf.PI;
            }

            switch (lightType)
            {
                case LightType.Directional:
                    lightDataGI.orientation = light.transform.rotation;
                    lightDataGI.position = light.transform.position;
                    lightDataGI.range = 0.0f;
#if UNITY_EDITOR
                    lightDataGI.shape0 = light.shadows != LightShadows.None ? (Mathf.Deg2Rad * light.shadowAngle) : 0.0f;
#else
                    lightDataGI.shape0 = 0.0f;
#endif
                    lightDataGI.shape1 = 0.0f;
                    lightDataGI.type = UnityEngine.Experimental.GlobalIllumination.LightType.Directional;
                    lightDataGI.falloff = FalloffType.Undefined;
                    lightDataGI.coneAngle = light.cookieSize2D.x;
                    lightDataGI.innerConeAngle = light.cookieSize2D.y;
                    break;

                case LightType.Spot:
                {
                    SpotLight spot;
                    spot.entityId = light.GetEntityId();
                    spot.shadow = light.shadows != LightShadows.None;
                    spot.mode = lightMode;
#if UNITY_EDITOR
                    spot.sphereRadius = light.shadows != LightShadows.None ? light.shapeRadius : 0.0f;
#else
                    spot.sphereRadius = 0.0f;
#endif
                    spot.position = light.transform.position;
                    spot.orientation = light.transform.rotation;
                    spot.color = directColor;
                    spot.indirectColor = indirectColor;
                    spot.range = light.range;
                    spot.coneAngle = light.spotAngle * Mathf.Deg2Rad;
                    spot.innerConeAngle = Mathf.Deg2Rad * light.innerSpotAngle;
                    spot.falloff = add.applyRangeAttenuation ? FalloffType.InverseSquared : FalloffType.InverseSquaredNoRangeAttenuation;
                    spot.angularFalloff = AngularFalloffType.AnalyticAndInnerAngle;
                    lightDataGI.Init(ref spot, ref cookie);
                    lightDataGI.shape1 = (float)AngularFalloffType.AnalyticAndInnerAngle;
                    if (light.cookie != null)
                        lightDataGI.cookieTextureEntityId = light.cookie.GetEntityId();
                    else if (add.IESSpot != null)
                        lightDataGI.cookieTextureEntityId = add.IESSpot.GetEntityId();
                    else
                        lightDataGI.cookieTextureEntityId = EntityId.None;
                }
                break;

                case LightType.Pyramid:
                {
                    SpotLightPyramidShape pyramid;
                    pyramid.entityId = light.GetEntityId();
                    pyramid.shadow = light.shadows != LightShadows.None;
                    pyramid.mode = lightMode;
                    pyramid.position = light.transform.position;
                    pyramid.orientation = light.transform.rotation;
                    pyramid.color = directColor;
                    pyramid.indirectColor = indirectColor;
                    pyramid.range = light.range;
                    pyramid.angle = light.spotAngle * Mathf.Deg2Rad;
                    pyramid.aspectRatio = Mathf.Tan(light.innerSpotAngle * Mathf.PI / 360f) / Mathf.Tan(light.spotAngle * Mathf.PI / 360f);
                    pyramid.falloff = add.applyRangeAttenuation ? FalloffType.InverseSquared : FalloffType.InverseSquaredNoRangeAttenuation;
                    lightDataGI.Init(ref pyramid, ref cookie);
                    if (light.cookie != null)
                        lightDataGI.cookieTextureEntityId = light.cookie.GetEntityId();
                    else if (add.IESSpot != null)
                        lightDataGI.cookieTextureEntityId = add.IESSpot.GetEntityId();
                    else
                        lightDataGI.cookieTextureEntityId = EntityId.None;
                }
                break;

                case LightType.Box:
                {
                    SpotLightBoxShape box;
                    box.entityId = light.GetEntityId();
                    box.shadow = light.shadows != LightShadows.None;
                    box.mode = lightMode;
                    box.position = light.transform.position;
                    box.orientation = light.transform.rotation;
                    box.color = directColor;
                    box.indirectColor = indirectColor;
                    box.range = light.range;
                    box.width = light.areaSize.x;
                    box.height = light.areaSize.y;
                    lightDataGI.Init(ref box, ref cookie);
                    if (light.cookie != null)
                        lightDataGI.cookieTextureEntityId = light.cookie.GetEntityId();
                    else if (add.IESSpot != null)
                        lightDataGI.cookieTextureEntityId = add.IESSpot.GetEntityId();
                    else
                        lightDataGI.cookieTextureEntityId = EntityId.None;
                }
                break;

                case LightType.Point:
                {
                    lightDataGI.orientation = light.transform.rotation;
                    lightDataGI.position = light.transform.position;
                    lightDataGI.range = light.range;
                    lightDataGI.coneAngle = 0.0f;
                    lightDataGI.innerConeAngle = 0.0f;

#if UNITY_EDITOR
                    lightDataGI.shape0 = light.shadows != LightShadows.None ? light.shapeRadius : 0.0f;
#else
                    lightDataGI.shape0 = 0.0f;
#endif
                    lightDataGI.shape1 = 0.0f;
                    lightDataGI.type = UnityEngine.Experimental.GlobalIllumination.LightType.Point;
                    lightDataGI.falloff = add.applyRangeAttenuation
                        ? FalloffType.InverseSquared
                        : FalloffType.InverseSquaredNoRangeAttenuation;
                }
                break;

                case LightType.Rectangle:
                {
                    lightDataGI.orientation = light.transform.rotation;
                    lightDataGI.position = light.transform.position;
                    lightDataGI.range = light.range;
                    lightDataGI.coneAngle = 0.0f;
                    lightDataGI.innerConeAngle = 0.0f;
                    lightDataGI.shape0 = light.areaSize.x;
                    lightDataGI.shape1 = light.areaSize.y;

                    // TEMP: for now, if we bake a rectangle type this will disable the light for runtime, need to speak with GI team about it!
                    lightDataGI.type = UnityEngine.Experimental.GlobalIllumination.LightType.Rectangle;
                    lightDataGI.falloff = add.applyRangeAttenuation ? FalloffType.InverseSquared : FalloffType.InverseSquaredNoRangeAttenuation;
                    if (add.areaLightCookie != null)
                        lightDataGI.cookieTextureEntityId = add.areaLightCookie.GetEntityId();
                    else if (add.IESSpot != null)
                        lightDataGI.cookieTextureEntityId = add.IESSpot.GetEntityId();
                    else
                        lightDataGI.cookieTextureEntityId = EntityId.None;
                }
                break;

                case LightType.Tube:
                {
                    lightDataGI.InitNoBake(lightDataGI.entityId);
                }
                break;

                case LightType.Disc:
                {
                    lightDataGI.orientation = light.transform.rotation;
                    lightDataGI.position = light.transform.position;
                    lightDataGI.range = light.range;
                    lightDataGI.coneAngle = 0.0f;
                    lightDataGI.innerConeAngle = 0.0f;
                    lightDataGI.shape0 = light.areaSize.x;
                    lightDataGI.shape1 = light.areaSize.y;

                    // TEMP: for now, if we bake a rectangle type this will disable the light for runtime, need to speak with GI team about it!
                    lightDataGI.type = UnityEngine.Experimental.GlobalIllumination.LightType.Disc;
                    lightDataGI.falloff = add.applyRangeAttenuation ? FalloffType.InverseSquared : FalloffType.InverseSquaredNoRangeAttenuation;
                    lightDataGI.cookieTextureEntityId = add.areaLightCookie ? add.areaLightCookie.GetEntityId() : EntityId.None;
                }
                break;

                default:
                    Debug.Assert(false, "Encountered an unknown LightType.");
                    break;
            }

            return true;
        }

        static public Lightmapping.RequestLightsDelegate hdLightsDelegate = (Light[] requests, NativeArray<LightDataGI> lightsOutput) =>
        {
            // Get all lights in the scene
            LightDataGI lightDataGI = new LightDataGI();
            for (int i = 0; i < requests.Length; i++)
            {
                Light light = requests[i];

                // For editor we need to discard realtime light as otherwise we get double contribution
                // At runtime for Enlighten we must keep realtime light but we can discard other light as they aren't used.

                // The difference is that `l.lightmapBakeType` is the intent, e.g.you want a mixed light with shadowmask. But then the overlap test might detect more than 4 overlapping volumes and force a light to fallback to baked.
                // In that case `l.bakingOutput.lightmapBakeType` would be baked, instead of mixed, whereas `l.lightmapBakeType` would still be mixed. But this difference is only relevant in editor builds
#if UNITY_EDITOR
                LightDataGIExtract(light, ref lightDataGI);
#else
                if (LightmapperUtils.Extract(light.bakingOutput.lightmapBakeType) == LightMode.Realtime)
                    LightDataGIExtract(light, ref lightDataGI);
                else
                    lightDataGI.InitNoBake(light.GetEntityId());
#endif

                lightsOutput[i] = lightDataGI;
            }
        };
    }
}
