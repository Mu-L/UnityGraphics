#if SURFACE_CACHE

using System;
using System.Collections.Generic;
using UnityEngine.PathTracing.Core;
using UnityEngine.Rendering.LiveGI;
using InstanceHandle = UnityEngine.PathTracing.Core.Handle<UnityEngine.Rendering.SurfaceCacheWorld.Instance>;
using LightHandle = UnityEngine.PathTracing.Core.Handle<UnityEngine.Rendering.SurfaceCacheWorld.Light>;
using MaterialHandle = UnityEngine.PathTracing.Core.Handle<UnityEngine.PathTracing.Core.MaterialPool.MaterialDescriptor>;

namespace UnityEngine.Rendering.Universal
{
    class SurfaceCacheWorldAdapter : IDisposable
    {
        // This dictionary maps from Unity EntityID for MeshRenderer or Terrain, to corresponding InstanceHandle for accessing World.
        private readonly Dictionary<EntityId, InstanceHandle> _entityIDToWorldInstanceHandles = new();

        // Same as above but for Lights
        private readonly Dictionary<EntityId, LightHandle> _entityIDToWorldLightHandles = new();

        // Same as above but for Materials
        private Dictionary<EntityId, MaterialHandle> _entityIDToWorldMaterialHandles = new();

        // We also keep track of associated material descriptors, so we can free temporary temporary textures when a material is removed
        private Dictionary<EntityId, MaterialPool.MaterialDescriptor> _entityIDToWorldMaterialDescriptors = new();

        private Material _fallbackMaterial;
        private MaterialPool.MaterialDescriptor _fallbackMaterialDescriptor;
        private MaterialHandle _fallbackMaterialHandle;

#if ENABLE_TERRAIN_MODULE
        // Maps TerrainData EntityID to list of Terrains that use that TerrainData
        private readonly Dictionary<EntityId, List<Terrain>> _terrainDataToTerrains = new();

#if UNITY_EDITOR
        private class TerrainRebuild
        {
            public Terrain terrain;
            public double timeSinceLastChange;
            public EntityId materialEntityId;
        }

        private readonly Dictionary<EntityId, TerrainRebuild> _deferredTerrainRebuilds = new();
        private const double k_TerrainRebuildDelay = 0.5;
#endif
#endif

        public SurfaceCacheWorldAdapter(SurfaceCacheWorld world, Material fallbackMaterial)
        {
            _fallbackMaterial = fallbackMaterial;
            _fallbackMaterialDescriptor = MaterialPool.ConvertUnityMaterialToMaterialDescriptor(fallbackMaterial, EmissionMode.Realtime);
            _fallbackMaterialHandle = world.AddMaterial(in _fallbackMaterialDescriptor, UVChannel.UV0);
            _entityIDToWorldMaterialHandles.Add(fallbackMaterial.GetEntityId(), _fallbackMaterialHandle);
            _entityIDToWorldMaterialDescriptors.Add(fallbackMaterial.GetEntityId(), _fallbackMaterialDescriptor);
        }

        internal void Update(SceneUpdatesTracker sceneTracker, AmbientMode ambientMode, Material skyboxMaterial,
            Color ambientSkycolor, Color ambientEquatorColor, Color ambientGroundColor, float envIntensityMultiplier,
            SurfaceCacheWorld world)
        {
            const bool filterBakedLights = true;
            var changes = sceneTracker.GetChanges(filterBakedLights);

            UpdateMaterials(world, changes.addedMaterials, changes.removedMaterials, changes.changedMaterials);
            UpdateMeshRenderers(
                world,
                changes.addedMeshRenderers,
                changes.changedMeshRenderers,
                changes.removedMeshRenderers);
#if ENABLE_TERRAIN_MODULE
            UpdateTerrains(
                world,
                changes.addedTerrains,
                changes.changedTerrains,
                changes.removedTerrains,
                changes.addedTerrainData,
                changes.changedTerrainData,
                changes.removedTerrainData);
#endif

            const bool multiplyPunctualLightIntensityByPI = false;
            UpdateLights(world, changes.addedLights, changes.removedLights, changes.changedLights, multiplyPunctualLightIntensityByPI);

            switch (ambientMode)
            {
                case AmbientMode.Skybox:
                    world.SetEnvironmentMode(CubemapRender.Mode.Material);
                    world.SetEnvironmentMaterial(skyboxMaterial);
                    world.SetEnvironmentIntensityMultiplier(envIntensityMultiplier);
                    break;

                case AmbientMode.Flat:
                    world.SetEnvironmentMode(CubemapRender.Mode.Color);
                    world.SetEnvironmentColor(ambientSkycolor);
                    world.SetEnvironmentIntensityMultiplier(1.0f);
                    break;

                case AmbientMode.Trilight:
                    world.SetEnvironmentMode(CubemapRender.Mode.Color);
                    world.SetEnvironmentGradientColors(ambientSkycolor, ambientEquatorColor, ambientGroundColor);
                    world.SetEnvironmentIntensityMultiplier(1.0f);
                    break;

                default:
                    world.SetEnvironmentMode(CubemapRender.Mode.Color);
                    world.SetEnvironmentColor(Color.black);
                    world.SetEnvironmentIntensityMultiplier(1.0f);
                    break;
            }
        }

        private void UpdateMaterials(SurfaceCacheWorld world, List<Material> addedMaterials, List<EntityId> removedMaterials, List<Material> changedMaterials)
        {
            UpdateMaterials(world, _entityIDToWorldMaterialHandles, _entityIDToWorldMaterialDescriptors, addedMaterials, removedMaterials, changedMaterials);
        }

        private static void UpdateMaterials(SurfaceCacheWorld world, Dictionary<EntityId, MaterialHandle> entityIDToHandle, Dictionary<EntityId, MaterialPool.MaterialDescriptor> entityIDToDescriptor, List<Material> addedMaterials, List<EntityId> removedMaterials, List<Material> changedMaterials)
        {
            static void DeleteTemporaryTextures(ref MaterialPool.MaterialDescriptor desc)
            {
                CoreUtils.Destroy(desc.Albedo);
                CoreUtils.Destroy(desc.Emission);
                CoreUtils.Destroy(desc.Transmission);
            }

            foreach (var entityID in removedMaterials)
            {
                // Clean up temporary textures in the descriptor
                Debug.Assert(entityIDToDescriptor.ContainsKey(entityID));
                var descriptor = entityIDToDescriptor[entityID];
                DeleteTemporaryTextures(ref descriptor);
                entityIDToDescriptor.Remove(entityID);

                // Remove the material from the world
                Debug.Assert(entityIDToHandle.ContainsKey(entityID));
                world.RemoveMaterial(entityIDToHandle[entityID]);
                entityIDToHandle.Remove(entityID);
            }

            foreach (var material in addedMaterials)
            {
                // Add material to the world
                var descriptor = MaterialPool.ConvertUnityMaterialToMaterialDescriptor(material, EmissionMode.Realtime);
                var handle = world.AddMaterial(in descriptor, UVChannel.UV0);
                entityIDToHandle.Add(material.GetEntityId(), handle);

                // Keep track of the descriptor
                entityIDToDescriptor.Add(material.GetEntityId(), descriptor);
            }

            foreach (var material in changedMaterials)
            {
                // Clean up temporary textures in the old descriptor
                Debug.Assert(entityIDToDescriptor.ContainsKey(material.GetEntityId()));
                var oldDescriptor = entityIDToDescriptor[material.GetEntityId()];
                DeleteTemporaryTextures(ref oldDescriptor);

                // Update the material in the world using the new descriptor
                Debug.Assert(entityIDToHandle.ContainsKey(material.GetEntityId()));
                var newDescriptor = MaterialPool.ConvertUnityMaterialToMaterialDescriptor(material, EmissionMode.Realtime);
                world.UpdateMaterial(entityIDToHandle[material.GetEntityId()], in newDescriptor, UVChannel.UV0);
                entityIDToDescriptor[material.GetEntityId()] = newDescriptor;
            }
        }

        private void UpdateLights(SurfaceCacheWorld world, List<Light> addedLights, List<EntityId> removedLights,
            List<Light> changedLights, bool multiplyPunctualLightIntensityByPI)
        {
            UpdateLights(world, _entityIDToWorldLightHandles, addedLights, removedLights, changedLights, multiplyPunctualLightIntensityByPI);
        }

        private static void UpdateLights(
            SurfaceCacheWorld world,
            Dictionary<EntityId, LightHandle> entityIDToHandle, List<Light> addedLights, List<EntityId> removedLights,
            List<Light> changedLights,
            bool multiplyPunctualLightIntensityByPI)
        {
            // Remove deleted lights
            LightHandle[] handlesToRemove = new LightHandle[removedLights.Count];
            for (int i = 0; i < removedLights.Count; i++)
            {
                var lightEntityID = removedLights[i];
                handlesToRemove[i] = entityIDToHandle[lightEntityID];
                entityIDToHandle.Remove(lightEntityID);
            }
            world.RemoveLights(handlesToRemove);

            // Add new lights
            var lightDescriptors = ConvertUnityLightsToLightDescriptors(addedLights.ToArray(), multiplyPunctualLightIntensityByPI);
            LightHandle[] addedHandles = world.AddLights(lightDescriptors);
            for (int i = 0; i < addedLights.Count; ++i)
                entityIDToHandle.Add(addedLights[i].GetEntityId(), addedHandles[i]);

            // Update changed lights
            LightHandle[] handlesToUpdate = new LightHandle[changedLights.Count];
            for (int i = 0; i < changedLights.Count; i++)
                handlesToUpdate[i] = entityIDToHandle[changedLights[i].GetEntityId()];

            world.UpdateLights(handlesToUpdate, ConvertUnityLightsToLightDescriptors(changedLights.ToArray(), multiplyPunctualLightIntensityByPI));
        }

        private void UpdateMeshRenderers(
            SurfaceCacheWorld world,
            List<MeshRenderer> addedMeshRenderers,
            List<MeshRendererChanges> changedMeshRenderers,
            List<EntityId> removedMeshRenderers)
        {
            UpdateMeshRenderers(world, _entityIDToWorldInstanceHandles, _entityIDToWorldMaterialHandles, addedMeshRenderers, changedMeshRenderers, removedMeshRenderers, _fallbackMaterial);
        }

        private static void UpdateMeshRenderers(
            SurfaceCacheWorld world,
            Dictionary<EntityId, InstanceHandle> entityIDToInstanceHandle,
            Dictionary<EntityId, MaterialHandle> entityIDToMaterialHandle,
            List<MeshRenderer> addedMeshRenderers,
            List<MeshRendererChanges> changedMeshRenderers,
            List<EntityId> removedMeshRenderers,
            Material fallbackMaterial)
        {
            foreach (var meshRendererEntityID in removedMeshRenderers)
            {
                if (entityIDToInstanceHandle.TryGetValue(meshRendererEntityID, out var instanceHandle))
                {
                    world.RemoveInstance(instanceHandle);
                    entityIDToInstanceHandle.Remove(meshRendererEntityID);
                }
            }

            foreach (var meshRenderer in addedMeshRenderers)
            {
                Debug.Assert(!meshRenderer.isPartOfStaticBatch, "Static Batching is not supported by Surface Cache GI.");

                var mesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;

                if (mesh == null || mesh.vertexCount == 0)
                    continue;

                var localToWorldMatrix = meshRenderer.transform.localToWorldMatrix;

                var materials = Util.GetMaterials(meshRenderer);
                var materialHandles = new MaterialHandle[materials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    var matEntityId = materials[i] == null ? fallbackMaterial.GetEntityId() : materials[i].GetEntityId();
                    materialHandles[i] = entityIDToMaterialHandle[matEntityId];
                }
                uint[] masks = new uint[materials.Length];
                for (int i = 0; i < masks.Length; i++)
                {
                    masks[i] = materials[i] != null ? 1u : 0u;
                }

                InstanceHandle instance = world.AddInstance(mesh, materialHandles, masks, in localToWorldMatrix);
                var entityID = meshRenderer.GetEntityId();
                Debug.Assert(!entityIDToInstanceHandle.ContainsKey(entityID));
                entityIDToInstanceHandle.Add(entityID, instance);
            }

            foreach (var meshRendererUpdate in changedMeshRenderers)
            {
                var meshRenderer = meshRendererUpdate.meshRenderer;
                var gameObject = meshRenderer.gameObject;

                Debug.Assert(entityIDToInstanceHandle.ContainsKey(meshRenderer.GetEntityId()));
                var instanceHandle = entityIDToInstanceHandle[meshRenderer.GetEntityId()];

                if ((meshRendererUpdate.changes & ModifiedProperties.Transform) != 0)
                {
                    world.UpdateInstanceTransform(instanceHandle, gameObject.transform.localToWorldMatrix);
                }

                if ((meshRendererUpdate.changes & ModifiedProperties.Material) != 0)
                {
                    var materials = Util.GetMaterials(meshRenderer);
                    var materialHandles = new MaterialHandle[materials.Length];
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var matEntityId = materials[i] == null ? fallbackMaterial.GetEntityId() : materials[i].GetEntityId();
                        materialHandles[i] = entityIDToMaterialHandle[matEntityId];
                    }

                    world.UpdateInstanceMaterials(instanceHandle, materialHandles);

                    uint[] masks = new uint[materials.Length];
                    for (int i = 0; i < masks.Length; i++)
                    {
                        masks[i] = materials[i] != null ? 1u : 0u;
                    }

                    world.UpdateInstanceMask(instanceHandle, masks);
                }
            }
        }

#if ENABLE_TERRAIN_MODULE
        private void UpdateTerrains(
            SurfaceCacheWorld world,
            List<Terrain> addedTerrains,
            List<TerrainChanges> changedTerrains,
            List<EntityId> removedTerrains,
            List<TerrainData> addedTerrainData,
            List<TerrainDataChanges> changedTerrainData,
            List<EntityId> removedTerrainData)
        {
            UpdateTerrains(world, _entityIDToWorldInstanceHandles, _entityIDToWorldMaterialHandles, addedTerrains, changedTerrains, removedTerrains, addedTerrainData, changedTerrainData, removedTerrainData, _terrainDataToTerrains
#if UNITY_EDITOR
                , _deferredTerrainRebuilds
#endif
                , _fallbackMaterial);
        }

        private static void UpdateTerrains(
            SurfaceCacheWorld world,
            Dictionary<EntityId, InstanceHandle> entityIDToInstanceHandle,
            Dictionary<EntityId, MaterialHandle> entityIDToMaterialHandle,
            List<Terrain> addedTerrains,
            List<TerrainChanges> changedTerrains,
            List<EntityId> removedTerrains,
            List<TerrainData> addedTerrainData,
            List<TerrainDataChanges> changedTerrainData,
            List<EntityId> removedTerrainData,
            Dictionary<EntityId, List<Terrain>> terrainDataToTerrains
#if UNITY_EDITOR
            , Dictionary<EntityId, TerrainRebuild> deferredTerrainRebuilds
#endif
            , Material fallbackMaterial)
        {
            foreach (var terrainEntityID in removedTerrains)
            {
#if UNITY_EDITOR
                deferredTerrainRebuilds.Remove(terrainEntityID);
#endif

                if (entityIDToInstanceHandle.TryGetValue(terrainEntityID, out var instanceHandle))
                {
                    world.RemoveInstance(instanceHandle);
                    entityIDToInstanceHandle.Remove(terrainEntityID);
                }

                foreach (var entry in terrainDataToTerrains)
                {
                    var terrainToRemove = entry.Value.Find(t => t.GetEntityId() == terrainEntityID);
                    if (terrainToRemove != null)
                    {
                        entry.Value.Remove(terrainToRemove);
                        break;
                    }
                }
            }
#if UNITY_EDITOR
            // Rebuild terrains whose heightmap/tree changes have been idle past the delay
            ProcessDeferredTerrainRebuilds(world, entityIDToInstanceHandle, entityIDToMaterialHandle, deferredTerrainRebuilds, fallbackMaterial);
#endif
            // Register existing terrains that were reassigned to this newly seen TerrainData
            foreach (var terrainData in addedTerrainData)
            {
                var terrainDataEntityID = terrainData.GetEntityId();
                if (!terrainDataToTerrains.TryGetValue(terrainDataEntityID, out var terrainList))
                {
                    terrainList = new List<Terrain>();
                    terrainDataToTerrains[terrainDataEntityID] = terrainList;
                }

                var toMove = new List<Terrain>();
                foreach (var entry in terrainDataToTerrains)
                {
                    if (entry.Key == terrainDataEntityID)
                        continue;
                    foreach (var terrain in entry.Value)
                    {
                        if (terrain.terrainData == terrainData && entityIDToInstanceHandle.ContainsKey(terrain.GetEntityId()))
                            toMove.Add(terrain);
                    }
                }

                // Remove each reassigned terrain from its old list, add to this TerrainData's list,
                // and rebuild the world instance so geometry matches the new TerrainData
                foreach (var terrain in toMove)
                {
                    foreach (var entry in terrainDataToTerrains)
                    {
                        if (entry.Value.Remove(terrain))
                            break;
                    }
                    terrainList.Add(terrain);
                    var terrainEntityID = terrain.GetEntityId();
                    if (!entityIDToInstanceHandle.TryGetValue(terrainEntityID, out var instanceHandle))
                        continue;
                    var material = terrain.splatBaseMaterial;
                    var matEntityId = material == null ? fallbackMaterial.GetEntityId() : material.GetEntityId();
                    RebuildTerrainInstance(world, entityIDToInstanceHandle, entityIDToMaterialHandle,
                        terrain, terrainEntityID, instanceHandle, matEntityId, fallbackMaterial);
                }
            }

            foreach (var terrain in addedTerrains)
            {
                var localToWorldMatrix = terrain.transform.localToWorldMatrix;

                var material = terrain.splatBaseMaterial;
                var matEntityId = material == null ? fallbackMaterial.GetEntityId() : material.GetEntityId();
                var materialHandle = entityIDToMaterialHandle[matEntityId];
                uint mask = 1u;

                InstanceHandle instance = world.AddInstance(terrain, materialHandle, mask, in localToWorldMatrix);
                var entityID = terrain.GetEntityId();
                Debug.Assert(!entityIDToInstanceHandle.ContainsKey(entityID));
                entityIDToInstanceHandle.Add(entityID, instance);

                var terrainData = terrain.terrainData;
                if (terrainData != null)
                {
                    var terrainDataEntityID = terrainData.GetEntityId();
                    if (!terrainDataToTerrains.TryGetValue(terrainDataEntityID, out var terrainList))
                    {
                        terrainList = new List<Terrain>();
                        terrainDataToTerrains[terrainDataEntityID] = terrainList;
                    }

                    if (!terrainList.Contains(terrain))
                    {
                        terrainList.Add(terrain);
                    }
                }
            }

            foreach (var terrainUpdate in changedTerrains)
            {
                var terrain = terrainUpdate.terrain;
                var gameObject = terrain.gameObject;

                Debug.Assert(entityIDToInstanceHandle.ContainsKey(terrain.GetEntityId()));
                var instanceHandle = entityIDToInstanceHandle[terrain.GetEntityId()];

                if ((terrainUpdate.changes & ModifiedProperties.Transform) != 0)
                {
                    world.UpdateInstanceTransform(instanceHandle, gameObject.transform.localToWorldMatrix);
                }

                if ((terrainUpdate.changes & ModifiedProperties.Material) != 0)
                {
                    var material = terrain.splatBaseMaterial;

                    var matEntityId = material == null ? fallbackMaterial.GetEntityId() : material.GetEntityId();
                    var materialHandle = entityIDToMaterialHandle[matEntityId];

                    world.UpdateInstanceMaterials(instanceHandle, new MaterialHandle[] { materialHandle });

                    var mask = material != null ? 1u : 0u;

                    world.UpdateInstanceMask(instanceHandle, new uint[] { mask });
                }
            }

            foreach (var terrainDataEntityID in removedTerrainData)
            {
                terrainDataToTerrains.Remove(terrainDataEntityID);
            }

            foreach (var terrainDataUpdate in changedTerrainData)
            {
                var terrainData = terrainDataUpdate.terrainData;
                var changes = terrainDataUpdate.changes;

                var terrainDataEntityID = terrainData.GetEntityId();
                if (!terrainDataToTerrains.TryGetValue(terrainDataEntityID, out var affectedTerrains))
                    continue;

                if ((changes & ModifiedProperties.Heightmap) == 0 && (changes & ModifiedProperties.Holes) == 0)
                    continue;

                foreach (var terrain in affectedTerrains)
                {
                    var terrainEntityID = terrain.GetEntityId();

                    if (!entityIDToInstanceHandle.TryGetValue(terrainEntityID, out var instanceHandle))
                        continue;

                    var material = terrain.splatBaseMaterial;
                    var matEntityId = material == null ? fallbackMaterial.GetEntityId() : material.GetEntityId();

#if UNITY_EDITOR
                    // Delay the removing and re-adding the terrain when in the editor
                    // to avoid lag when the user is actively editing the terrain
                    if (deferredTerrainRebuilds.TryGetValue(terrainEntityID, out var pending))
                    {
                        pending.timeSinceLastChange = UnityEditor.EditorApplication.timeSinceStartup;
                        pending.materialEntityId = matEntityId;
                    }
                    else
                    {
                        deferredTerrainRebuilds[terrainEntityID] = new TerrainRebuild
                        {
                            terrain = terrain,
                            timeSinceLastChange = UnityEditor.EditorApplication.timeSinceStartup,
                            materialEntityId = matEntityId
                        };
                    }
#else
                    // Immediately remove and re-add the terrain to World in a Player
                    RebuildTerrainInstance(world, entityIDToInstanceHandle, entityIDToMaterialHandle,
                        terrain, terrainEntityID, instanceHandle, matEntityId, fallbackMaterial);
#endif
                }
            }
        }

#if UNITY_EDITOR
        private static void ProcessDeferredTerrainRebuilds(
            SurfaceCacheWorld world,
            Dictionary<EntityId, InstanceHandle> entityIDToInstanceHandle,
            Dictionary<EntityId, MaterialHandle> entityIDToMaterialHandle,
            Dictionary<EntityId, TerrainRebuild> terrainRebuilds,
            Material fallbackMaterial)
        {
            // Rebuild terrains that have been idle past the delay (avoids lag while editing)
            var currentTime = UnityEditor.EditorApplication.timeSinceStartup;
            var terrainsToRebuild = new List<EntityId>();

            foreach (var entry in terrainRebuilds)
            {
                if (currentTime - entry.Value.timeSinceLastChange >= k_TerrainRebuildDelay)
                {
                    terrainsToRebuild.Add(entry.Key);
                }
            }

            foreach (var terrainEntityID in terrainsToRebuild)
            {
                var pending = terrainRebuilds[terrainEntityID];

                if (entityIDToInstanceHandle.TryGetValue(terrainEntityID, out var instanceHandle))
                {
                    RebuildTerrainInstance(world, entityIDToInstanceHandle, entityIDToMaterialHandle, pending.terrain, terrainEntityID, instanceHandle, pending.materialEntityId, fallbackMaterial);
                }

                terrainRebuilds.Remove(terrainEntityID);
            }
        }
#endif

        private static void RebuildTerrainInstance(
            SurfaceCacheWorld world,
            Dictionary<EntityId, InstanceHandle> entityIDToInstanceHandle,
            Dictionary<EntityId, MaterialHandle> entityIDToMaterialHandle,
            Terrain terrain,
            EntityId terrainEntityID,
            InstanceHandle instanceHandle,
            EntityId materialEntityId,
            Material fallbackMaterial)
        {
            world.RemoveInstance(instanceHandle);
            entityIDToInstanceHandle.Remove(terrainEntityID);

            var localToWorldMatrix = terrain.transform.localToWorldMatrix;
            var fallbackMaterialHandle = entityIDToMaterialHandle[fallbackMaterial.GetEntityId()];
            var materialHandle = entityIDToMaterialHandle.GetValueOrDefault(materialEntityId, fallbackMaterialHandle);
            uint mask = 1u;

            InstanceHandle instance = world.AddInstance(terrain, materialHandle, mask, in localToWorldMatrix);
            Debug.Assert(!entityIDToInstanceHandle.ContainsKey(terrainEntityID));
            entityIDToInstanceHandle.Add(terrainEntityID, instance);
        }
#endif // ENABLE_TERRAIN_MODULE

        public void Dispose()
        {
            CoreUtils.Destroy(_fallbackMaterialDescriptor.Albedo);
            CoreUtils.Destroy(_fallbackMaterialDescriptor.Emission);
            CoreUtils.Destroy(_fallbackMaterialDescriptor.Transmission);
        }

        internal static SurfaceCacheWorld.LightDescriptor[] ConvertUnityLightsToLightDescriptors(Light[] lights, bool multiplyPunctualLightIntensityByPI)
        {
            var descriptors = new SurfaceCacheWorld.LightDescriptor[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                ref SurfaceCacheWorld.LightDescriptor descriptor = ref descriptors[i];
                descriptor.Type = light.type;
                descriptor.LinearLightColor = Util.GetLinearLightColor(light, light.bounceIntensity);
                if (multiplyPunctualLightIntensityByPI && Util.IsPunctualLightType(light.type))
                    descriptor.LinearLightColor *= Mathf.PI;
                descriptor.Transform = light.transform.localToWorldMatrix;
                descriptor.ColorTemperature = light.colorTemperature;
                descriptor.OuterSpotAngle = light.spotAngle;
                descriptor.InnerSpotAngle = light.innerSpotAngle;
                descriptor.Range = light.range;
            }
            return descriptors;
        }
    }
}

#endif
