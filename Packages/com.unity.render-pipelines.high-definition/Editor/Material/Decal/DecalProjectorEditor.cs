using System;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Rendering.HighDefinition.ShaderGraph;
using UnityEditor.ShaderGraph;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using static UnityEditorInternal.EditMode;
using RenderingLayerMask = UnityEngine.RenderingLayerMask;

namespace UnityEditor.Rendering.HighDefinition
{
    [CustomEditor(typeof(DecalProjector))]
    [CanEditMultipleObjects]
    partial class DecalProjectorEditor : Editor
    {
        const float k_Limit = 100000;
        const float k_LimitInv = 1 / k_Limit;


        static public readonly GUIContent k_NewDecalMaterialButtonText = EditorGUIUtility.TrTextContent("New", "Creates a new Decal material.");
        static public readonly string k_NewDecalText = "HDRP Decal";
        static public readonly string k_NewSGDecalText = "ShaderGraph Decal";

        internal enum DefaultDecal
        {
            HDRPDecal,
            SGDecal
        }
        static Color fullColor
        {
            get
            {
                Color c = s_LastColor;
                c.a = 1;
                return c;
            }
        }
        static Color s_LastColor;
        static void UpdateColorsInHandlesIfRequired()
        {
            Color c = DecalPreferences.decalGizmoColor;
            if (c != s_LastColor)
            {
                if (s_BoxHandle != null && !s_BoxHandle.Equals(null))
                    s_BoxHandle = null;

                if (s_uvHandles != null && !s_uvHandles.Equals(null))
                    s_uvHandles.baseColor = c;

                s_LastColor = c;
            }
        }

        MaterialEditor m_MaterialEditor = null;
        SerializedProperty m_MaterialProperty;
        SerializedProperty m_DrawDistanceProperty;
        SerializedProperty m_FadeScaleProperty;
        SerializedProperty m_StartAngleFadeProperty;
        SerializedProperty m_EndAngleFadeProperty;
        SerializedProperty m_UVScaleProperty;
        SerializedProperty m_UVBiasProperty;
        SerializedProperty m_AffectsTransparencyProperty;
        SerializedScalableSettingValue m_TransparentTextureResolution;
        SerializedProperty m_ScaleMode;
        SerializedProperty m_Size;
        SerializedProperty[] m_SizeValues;
        SerializedProperty m_Offset;
        SerializedProperty[] m_OffsetValues;
        SerializedProperty m_FadeFactor;
        SerializedProperty m_DecalLayerMask;

        int layerMask => (target as DecalProjector).cachedEditorLayer;
        bool layerMaskHasMultipleValue
        {
            get
            {
                if (targets.Length < 2)
                    return false;
                int layerMask = (targets[0] as DecalProjector).cachedEditorLayer;
                for (int index = 0; index < targets.Length; ++index)
                {
                    if ((targets[index] as DecalProjector).cachedEditorLayer != layerMask)
                        return true;
                }
                return false;
            }
        }

        private bool affectTransparency(DecalProjector decalProjector)
        {
            Material material = decalProjector.material;
            if (material == null)
                return false;

            if (material.IsShaderGraph())
            {
                return DecalSystem.IsDecalMaterial(material);
            }
            else
                return DecalSystem.IsHDRenderPipelineDecal(material.shader);
        }

        bool showAffectTransparency => affectTransparency(target as DecalProjector);

        bool showAffectTransparencyHaveMultipleDifferentValue
        {
            get
            {
                if (targets.Length < 2)
                    return false;
                DecalProjector decalProjector0 = (targets[0] as DecalProjector);
                bool show = affectTransparency(decalProjector0);
                for (int index = 0; index < targets.Length; ++index)
                {
                    if ((targets[index] as DecalProjector).material != null)
                    {
                        DecalProjector decalProjectori = (targets[index] as DecalProjector);
                        if (decalProjectori != null && affectTransparency(decalProjectori) ^ show)
                            return true;
                    }
                }
                return false;
            }
        }

        bool showTransparentTextureResolution
        {
            get
            {
                DecalProjector projector = target as DecalProjector;
                if (!affectTransparency(projector))
                    return false;

                return projector.material.IsShaderGraph() && m_AffectsTransparencyProperty.boolValue;
            }
        }

        static HierarchicalBox s_BoxHandle;
        static HierarchicalBox boxHandle
        {
            get
            {
                if (s_BoxHandle == null || s_BoxHandle.Equals(null))
                {
                    Color c = fullColor;
                    s_BoxHandle = new HierarchicalBox(s_LastColor, new[] { c, c, c, c, c, c });
                    s_BoxHandle.SetBaseColor(s_LastColor);
                    s_BoxHandle.monoHandle = false;
                }
                return s_BoxHandle;
            }
        }

        static DisplacableRectHandles s_uvHandles;
        static DisplacableRectHandles uvHandles
        {
            get
            {
                if (s_uvHandles == null || s_uvHandles.Equals(null))
                    s_uvHandles = new DisplacableRectHandles(s_LastColor, allowsNegative: true);
                return s_uvHandles;
            }
        }

        static readonly BoxBoundsHandle s_AreaLightHandle =
            new BoxBoundsHandle { axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y };

        internal const SceneViewEditMode k_EditShapeWithoutPreservingUV = (SceneViewEditMode)90;
        internal const SceneViewEditMode k_EditShapePreservingUV = (SceneViewEditMode)91;
        internal const SceneViewEditMode k_EditUVAndPivot = (SceneViewEditMode)92;

        static Func<Vector3, Quaternion, Vector3> s_DrawPivotHandle;

        static List<DecalProjectorEditor> s_Instances = new List<DecalProjectorEditor>();

        static DecalProjectorEditor FindEditorFromSelection()
        {
            GameObject[] selection = Selection.gameObjects;
            DecalProjector[] selectionTargets = Selection.GetFiltered<DecalProjector>(SelectionMode.Unfiltered);

            foreach (DecalProjectorEditor editor in s_Instances)
            {
                if (selectionTargets.Length != editor.targets.Length)
                    continue;
                bool allOk = true;
                foreach (DecalProjector selectionTarget in selectionTargets)
                    if (!Array.Find(editor.targets, t => t == selectionTarget))
                    {
                        allOk = false;
                        break;
                    }
                if (!allOk)
                    continue;
                return editor;
            }
            return null;
        }

        private void OnEnable()
        {
            s_Instances.Add(this);

            // Create an instance of the MaterialEditor
            UpdateMaterialEditor();
            foreach (var decalProjector in targets)
            {
                (decalProjector as DecalProjector).OnMaterialChange += RequireUpdateMaterialEditor;
            }

            // Fetch serialized properties
            m_MaterialProperty = serializedObject.FindProperty("m_Material");
            m_DrawDistanceProperty = serializedObject.FindProperty("m_DrawDistance");
            m_FadeScaleProperty = serializedObject.FindProperty("m_FadeScale");
            m_StartAngleFadeProperty = serializedObject.FindProperty("m_StartAngleFade");
            m_EndAngleFadeProperty = serializedObject.FindProperty("m_EndAngleFade");
            m_UVScaleProperty = serializedObject.FindProperty("m_UVScale");
            m_UVBiasProperty = serializedObject.FindProperty("m_UVBias");
            m_AffectsTransparencyProperty = serializedObject.FindProperty("m_AffectsTransparency");
            m_TransparentTextureResolution = new SerializedScalableSettingValue(serializedObject.Find((DecalProjector p) => p.TransparentTextureResolution));
            m_ScaleMode = serializedObject.FindProperty("m_ScaleMode");
            m_Size = serializedObject.FindProperty("m_Size");
            m_SizeValues = new[]
            {
                m_Size.FindPropertyRelative("x"),
                m_Size.FindPropertyRelative("y"),
                m_Size.FindPropertyRelative("z"),
            };
            m_Offset = serializedObject.FindProperty("m_Offset");
            m_OffsetValues = new[]
            {
                m_Offset.FindPropertyRelative("x"),
                m_Offset.FindPropertyRelative("y"),
                m_Offset.FindPropertyRelative("z"),
            };
            m_FadeFactor = serializedObject.FindProperty("m_FadeFactor");
            m_DecalLayerMask = serializedObject.FindProperty("m_DecalLayerMask");

            ReinitSavedRatioSizePivotPosition();
        }

        private void OnDisable()
        {
            foreach (DecalProjector decalProjector in targets)
            {
                if (decalProjector != null)
                    decalProjector.OnMaterialChange -= RequireUpdateMaterialEditor;
            }

            s_Instances.Remove(this);
        }

        private void OnDestroy() =>
            DestroyImmediate(m_MaterialEditor);

        public bool HasFrameBounds()
        {
            return true;
        }

        public Bounds OnGetFrameBounds()
        {
            DecalProjector decalProjector = target as DecalProjector;

            return new Bounds(decalProjector.transform.position, boxHandle.size);
        }

        private bool m_RequireUpdateMaterialEditor = false;

        private void RequireUpdateMaterialEditor() => m_RequireUpdateMaterialEditor = true;

        public void UpdateMaterialEditor()
        {
            int validMaterialsCount = 0;
            for (int index = 0; index < targets.Length; ++index)
            {
                DecalProjector decalProjector = (targets[index] as DecalProjector);
                if ((decalProjector != null) && (decalProjector.material != null))
                    validMaterialsCount++;
            }
            // Update material editor with the new material
            UnityEngine.Object[] materials = new UnityEngine.Object[validMaterialsCount];
            validMaterialsCount = 0;
            for (int index = 0; index < targets.Length; ++index)
            {
                DecalProjector decalProjector = (targets[index] as DecalProjector);

                if ((decalProjector != null) && (decalProjector.material != null))
                    materials[validMaterialsCount++] = (targets[index] as DecalProjector).material;
            }
            m_MaterialEditor = (MaterialEditor)CreateEditor(materials);
        }

        void OnSceneGUI()
        {
            //called on each targets
            DrawHandles();
        }

        void DrawBoxTransformationHandles(DecalProjector decalProjector)
        {
            Vector3 scale = decalProjector.effectiveScale;
            using (new Handles.DrawingScope(fullColor, Matrix4x4.TRS(decalProjector.transform.position, decalProjector.transform.rotation, scale)))
            {
                Vector3 centerStart = decalProjector.pivot;
                boxHandle.center = centerStart;
                boxHandle.size = decalProjector.size;

                Vector3 boundsSizePreviousOS = boxHandle.size;
                Vector3 boundsMinPreviousOS = boxHandle.size * -0.5f + boxHandle.center;

                EditorGUI.BeginChangeCheck();
                boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    // Adjust decal transform if handle changed.
                    Undo.RecordObject(decalProjector, "Decal Projector Change");

                    bool xChangeIsValid = scale.x != 0f;
                    bool yChangeIsValid = scale.y != 0f;
                    bool zChangeIsValid = scale.z != 0f;

                    // Preserve serialized state for axes with scale 0.
                    decalProjector.size = new Vector3(
                        xChangeIsValid ? boxHandle.size.x : decalProjector.size.x,
                        yChangeIsValid ? boxHandle.size.y : decalProjector.size.y,
                        zChangeIsValid ? boxHandle.size.z : decalProjector.size.z);
                    decalProjector.pivot = new Vector3(
                        xChangeIsValid ? boxHandle.center.x : decalProjector.pivot.x,
                        yChangeIsValid ? boxHandle.center.y : decalProjector.pivot.y,
                        zChangeIsValid ? boxHandle.center.z : decalProjector.pivot.z);

                    Vector3 boundsSizeCurrentOS = boxHandle.size;
                    Vector3 boundsMinCurrentOS = boxHandle.size * -0.5f + boxHandle.center;

                    if (editMode == k_EditShapePreservingUV)
                    {
                        // Treat decal projector bounds as a crop tool, rather than a scale tool.
                        // Compute a new uv scale and bias terms to pin decal projection pixels in world space, irrespective of projector bounds.
                        // Preserve serialized state for axes with scale 0.
                        Vector2 uvScale = decalProjector.uvScale;
                        Vector2 uvBias = decalProjector.uvBias;
                        if (xChangeIsValid)
                        {
                            uvScale.x *= Mathf.Max(k_LimitInv, boundsSizeCurrentOS.x) / Mathf.Max(k_LimitInv, boundsSizePreviousOS.x);
                            uvBias.x += (boundsMinCurrentOS.x - boundsMinPreviousOS.x) / Mathf.Max(k_LimitInv, boundsSizeCurrentOS.x) * uvScale.x;
                        }
                        if (yChangeIsValid)
                        {
                            uvScale.y *= Mathf.Max(k_LimitInv, boundsSizeCurrentOS.y) / Mathf.Max(k_LimitInv, boundsSizePreviousOS.y);
                            uvBias.y += (boundsMinCurrentOS.y - boundsMinPreviousOS.y) / Mathf.Max(k_LimitInv, boundsSizeCurrentOS.y) * uvScale.y;
                        }
                        decalProjector.uvScale = uvScale;
                        decalProjector.uvBias = uvBias;
                    }

                    if (PrefabUtility.IsPartOfNonAssetPrefabInstance(decalProjector))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(decalProjector);
                    }

                    // Smoothly update the decal image projected
                    DecalSystem.instance.UpdateCachedData(decalProjector.Handle, decalProjector);
                }
            }
        }

        void DrawPivotHandles(DecalProjector decalProjector)
        {
            Vector3 scale = decalProjector.effectiveScale;
            Vector3 scaledPivot = Vector3.Scale(decalProjector.pivot, scale);
            Vector3 scaledSize = Vector3.Scale(decalProjector.size, scale);

            using (new Handles.DrawingScope(fullColor, Matrix4x4.TRS(Vector3.zero, decalProjector.transform.rotation, Vector3.one)))
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPosition = ProjectedTransform.DrawHandles(decalProjector.transform.position, .5f * scaledSize.z - scaledPivot.z, decalProjector.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { decalProjector, decalProjector.transform }, "Decal Projector Change");

                    scaledPivot += Quaternion.Inverse(decalProjector.transform.rotation) * (decalProjector.transform.position - newPosition);
                    decalProjector.pivot = new Vector3(
                        scale.x != 0f ? scaledPivot.x / scale.x : decalProjector.pivot.x,
                        scale.y != 0f ? scaledPivot.y / scale.y : decalProjector.pivot.y,
                        scale.z != 0f ? scaledPivot.z / scale.z : decalProjector.pivot.z);
                    decalProjector.transform.position = newPosition;

                    ReinitSavedRatioSizePivotPosition();
                }
            }
        }

        void DrawUVHandles(DecalProjector decalProjector)
        {
            Vector3 scale = decalProjector.effectiveScale;
            Vector3 scaledPivot = Vector3.Scale(decalProjector.pivot, scale);
            Vector3 scaledSize = Vector3.Scale(decalProjector.size, scale);

            using (new Handles.DrawingScope(Matrix4x4.TRS(decalProjector.transform.position + decalProjector.transform.rotation * (scaledPivot - .5f * scaledSize), decalProjector.transform.rotation, scale)))
            {
                Vector2 uvScale = decalProjector.uvScale;
                Vector2 uvBias = decalProjector.uvBias;

                Vector2 uvSize = new Vector2(
                    (uvScale.x > k_Limit || uvScale.x < -k_Limit) ? 0f : decalProjector.size.x / uvScale.x,
                    (uvScale.y > k_Limit || uvScale.y < -k_Limit) ? 0f : decalProjector.size.y / uvScale.y
                );
                Vector2 uvCenter = uvSize * .5f - new Vector2(uvBias.x * uvSize.x, uvBias.y * uvSize.y);

                uvHandles.center = uvCenter;
                uvHandles.size = uvSize;

                EditorGUI.BeginChangeCheck();
                uvHandles.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(decalProjector, "Decal Projector Change");

                    for (int channel = 0; channel < 2; channel++)
                    {
                        // Preserve serialized state for axes with the scaled size 0.
                        if (scaledSize[channel] != 0f)
                        {
                            float handleSize = uvHandles.size[channel];
                            float minusNewUVStart = .5f * handleSize - uvHandles.center[channel];
                            float decalSize = decalProjector.size[channel];
                            float limit = k_LimitInv * decalSize;
                            if (handleSize > limit || handleSize < -limit)
                            {
                                uvScale[channel] = decalSize / handleSize;
                                uvBias[channel] = minusNewUVStart / handleSize;
                            }
                            else
                            {
                                // TODO: Decide if uvHandles.size should ever have negative value. It can't currently.
                                uvScale[channel] = k_Limit * Mathf.Sign(handleSize);
                                uvBias[channel] = k_Limit * minusNewUVStart / decalSize;
                            }
                        }
                    }

                    decalProjector.uvScale = uvScale;
                    decalProjector.uvBias = uvBias;
                }
            }
        }

        void DrawHandles()
        {
            DecalProjector decalProjector = target as DecalProjector;

            if (editMode == k_EditShapePreservingUV || editMode == k_EditShapeWithoutPreservingUV)
                DrawBoxTransformationHandles(decalProjector);
            else if (editMode == k_EditUVAndPivot)
            {
                DrawPivotHandles(decalProjector);
                DrawUVHandles(decalProjector);
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawGizmosSelected(DecalProjector decalProjector, GizmoType gizmoType)
        {
            float lod = Gizmos.CalculateLOD(decalProjector.transform.position, decalProjector.size.magnitude * 0.25f);

            // skip drawing anything if it will be too small or behind the camera on screen
            if (lod < 0.1f)
                return;

            UpdateColorsInHandlesIfRequired();

            const float k_DotLength = 5f;

            // Draw them with scale applied to size and pivot instead of the matrix to keep the proportions of the arrow and lines.
            using (new Handles.DrawingScope(fullColor, Matrix4x4.TRS(decalProjector.transform.position, decalProjector.transform.rotation, Vector3.one)))
            {
                Vector3 scale = decalProjector.effectiveScale;
                Vector3 scaledPivot = Vector3.Scale(decalProjector.pivot, scale);
                Vector3 scaledSize = Vector3.Scale(decalProjector.size, scale);

                boxHandle.center = scaledPivot;
                boxHandle.size = scaledSize;
                bool isVolumeEditMode = editMode == k_EditShapePreservingUV || editMode == k_EditShapeWithoutPreservingUV;
                bool isPivotEditMode = editMode == k_EditUVAndPivot;
                if (lod > 0.5f)
                {
                    boxHandle.DrawHull(isVolumeEditMode);
                }
                else
                    Handles.DrawWireCube(scaledPivot, scaledSize); // simplify the drawing if too small on screen

                if (lod == 1.0f) // only draw when big enough on screen to be useable
                {
                    Vector3 pivot = Vector3.zero;
                    Vector3 projectedPivot = new Vector3(0, 0, scaledPivot.z - .5f * scaledSize.z);

                    if (isPivotEditMode)
                    {
                        Handles.DrawDottedLines(new[] { projectedPivot, pivot }, k_DotLength);
                    }
                    else
                    {
                        float arrowSize = scaledSize.z * 0.25f;
                        Handles.ArrowHandleCap(0, projectedPivot, Quaternion.identity, arrowSize, EventType.Repaint);
                    }

                    //draw UV and bolder edges
                    using (new Handles.DrawingScope(Matrix4x4.TRS(decalProjector.transform.position + decalProjector.transform.rotation * new Vector3(scaledPivot.x, scaledPivot.y, scaledPivot.z - .5f * scaledSize.z), decalProjector.transform.rotation, Vector3.one)))
                    {
                        Vector2 UVSize = new Vector2(
                            (decalProjector.uvScale.x > k_Limit || decalProjector.uvScale.x < -k_Limit) ? 0f : scaledSize.x / decalProjector.uvScale.x,
                            (decalProjector.uvScale.y > k_Limit || decalProjector.uvScale.y < -k_Limit) ? 0f : scaledSize.y / decalProjector.uvScale.y
                        );
                        Vector2 UVCenter = UVSize * .5f - new Vector2(decalProjector.uvBias.x * UVSize.x, decalProjector.uvBias.y * UVSize.y) - (Vector2)scaledSize * .5f;

                        uvHandles.center = UVCenter;
                        uvHandles.size = UVSize;
                        uvHandles.DrawRect(dottedLine: true, screenSpaceSize: k_DotLength);

                        uvHandles.center = default;
                        uvHandles.size = scaledSize;
                        uvHandles.DrawRect(dottedLine: false, thickness: 3f);
                    }
                }
            }
        }

        static Func<Bounds> GetBoundsGetter(DecalProjector decalProjector)
        {
            return () =>
            {
                var bounds = new Bounds();
                var decalTransform = decalProjector.transform;
                bounds.Encapsulate(decalTransform.position);
                return bounds;
            };
        }

        // Temporarilly save ratio beetwin size and pivot position while editing in inspector.
        // null or NaN is used to say that there is no saved ratio.
        // Aim is to keep propotion while sliding the value to 0 in Inspector and then go back to something else.
        // Current solution only work for the life of this editor, but is enough in most case.
        // Wich means if you go to there, selection something else and go back on it, pivot position is thus null.
        Dictionary<DecalProjector, Vector3> ratioSizePivotPositionSaved = null;

        void ReinitSavedRatioSizePivotPosition()
        {
            ratioSizePivotPositionSaved = null;
        }

        void UpdateSize(int axe, float newSize)
        {
            void UpdateSizeOfOneTarget(DecalProjector currentTarget)
            {
                //lazy init on demand as targets array cannot be accessed from OnSceneGUI so in edit mode.
                if (ratioSizePivotPositionSaved == null)
                {
                    ratioSizePivotPositionSaved = new Dictionary<DecalProjector, Vector3>();
                    foreach (DecalProjector projector in targets)
                        ratioSizePivotPositionSaved[projector] = new Vector3(float.NaN, float.NaN, float.NaN);
                }

                // Save old ratio if not registered
                // Either or are NaN or no one, check only first
                Vector3 saved = ratioSizePivotPositionSaved[currentTarget];
                if (float.IsNaN(saved[axe]))
                {
                    float oldSize = currentTarget.m_Size[axe];
                    saved[axe] = Mathf.Abs(oldSize) <= Mathf.Epsilon ? 0f : currentTarget.m_Offset[axe] / oldSize;
                    ratioSizePivotPositionSaved[currentTarget] = saved;
                }

                currentTarget.m_Size[axe] = newSize;
                currentTarget.m_Offset[axe] = saved[axe] * newSize;

                // refresh DecalProjector to update projection
                currentTarget.OnValidate();
            }

            // Manually register Undo as we work directly on the target
            Undo.RecordObjects(targets, "Change DecalProjector Size or Depth");

            // Apply any change on target first
            serializedObject.ApplyModifiedProperties();

            // update each target
            foreach (DecalProjector decalProjector in targets)
            {
                UpdateSizeOfOneTarget(decalProjector);

                // Fix for UUM-29105 (Changes made to Decal Project Prefab in the Inspector are not saved)
                // This editor doesn't use serializedObject to modify the target objects, explicitly mark the prefab
                // asset dirty to ensure the new data is saved.
                if (PrefabUtility.IsPartOfPrefabAsset(decalProjector))
                    EditorUtility.SetDirty(decalProjector);
            }

            // update again serialize object to register change in targets
            serializedObject.Update();

            // change was not tracked by SerializeReference so force repaint the scene views and game views
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            // strange: we need to force it throu serialization to update multiple differente value state (value are right but still detected as different)
            if (m_SizeValues[axe].hasMultipleDifferentValues)
                m_SizeValues[axe].floatValue = newSize;
        }

        internal void MinMaxSliderWithFields(Rect rect, GUIContent label, ref float minValue, ref float maxValue, float minLimit, float maxLimit)
        {
            // Reserve label space and push the slider rect to the right
            rect = EditorGUI.PrefixLabel(rect, label);

            const float fieldWidth = 40, padding = 4;
            if (rect.width < 3 * fieldWidth + 2 * padding)
            {
                EditorGUI.MinMaxSlider(rect, ref minValue, ref maxValue, minLimit, maxLimit);
            }
            else
            {
                var tmpRect = new Rect(rect);
                tmpRect.width = fieldWidth;

                EditorGUI.BeginChangeCheck();
                float value = EditorGUI.FloatField(tmpRect, minValue);
                if (EditorGUI.EndChangeCheck())
                    minValue = Mathf.Max(value, minLimit);

                tmpRect.x = rect.xMax - fieldWidth;
                EditorGUI.BeginChangeCheck();
                value = EditorGUI.FloatField(tmpRect, maxValue);
                if (EditorGUI.EndChangeCheck())
                    maxValue = Mathf.Min(value, maxLimit);

                tmpRect.xMin = rect.xMin + (fieldWidth + padding);
                tmpRect.xMax = rect.xMax - (fieldWidth + padding);
                EditorGUI.MinMaxSlider(tmpRect, ref minValue, ref maxValue, minLimit, maxLimit);
            }
        }

        void DoRenderingLayerMask()
        {
            Rect rect = EditorGUILayout.GetControlRect(true, 18f);
            EditorGUI.BeginProperty(rect, k_DecalLayerMaskContent, m_DecalLayerMask);

            var mask = m_DecalLayerMask.uintValue;
            EditorGUI.BeginChangeCheck();
            mask = EditorGUI.RenderingLayerMaskField(rect, k_DecalLayerMaskContent, (RenderingLayerMask)mask, EditorStyles.layerMaskField);
            if (EditorGUI.EndChangeCheck())
            {
                m_DecalLayerMask.intValue = unchecked((int) mask);
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
        }

        void DoAngleFade()
        {
            // The slider edits 2 different properties. Both can be overridden separately.
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.BeginProperty(rect, k_AngleFadeContent, m_StartAngleFadeProperty);
            EditorGUI.BeginProperty(rect, k_AngleFadeContent, m_EndAngleFadeProperty);

            float angleFadeMinValue = m_StartAngleFadeProperty.floatValue;
            float angleFadeMaxValue = m_EndAngleFadeProperty.floatValue;
            EditorGUI.BeginChangeCheck();
            MinMaxSliderWithFields(rect,k_AngleFadeContent, ref angleFadeMinValue, ref angleFadeMaxValue, 0.0f, 180.0f);
            if (EditorGUI.EndChangeCheck())
            {
                m_StartAngleFadeProperty.floatValue = angleFadeMinValue;
                m_EndAngleFadeProperty.floatValue = angleFadeMaxValue;
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.EndProperty();
            EditorGUI.EndProperty();
        }

        public override void OnInspectorGUI()
        {
            bool supportDecals = false;
            HDRenderPipelineAsset hdrp = HDRenderPipeline.currentAsset;
            if (hdrp != null)
                supportDecals = hdrp.currentPlatformRenderPipelineSettings.supportDecals;

            if (!supportDecals)
            {
                HDEditorUtils.QualitySettingsHelpBox("The current HDRP Asset does not support Decals.", MessageType.Error,
                    HDRenderPipelineUI.ExpandableGroup.Rendering,
                    HDRenderPipelineUI.ExpandableRendering.Decal, "m_RenderPipelineSettings.supportDecals");
                EditorGUILayout.Space();
            }

            serializedObject.Update();

            if (m_RequireUpdateMaterialEditor)
            {
                UpdateMaterialEditor();
                m_RequireUpdateMaterialEditor = false;
            }

            EditorGUI.BeginChangeCheck();
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField(m_ScaleMode, k_ScaleMode);

                Rect rect = EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(SerializedPropertyType.Vector2, k_SizeContent));
                EditorGUI.BeginProperty(rect, k_SizeSubContent[0], m_SizeValues[0]);
                EditorGUI.BeginProperty(rect, k_SizeSubContent[1], m_SizeValues[1]);
                bool savedHasMultipleDifferentValue = EditorGUI.showMixedValue;
                EditorGUI.showMixedValue = m_SizeValues[0].hasMultipleDifferentValues || m_SizeValues[1].hasMultipleDifferentValues;
                float[] size = new float[2] { m_SizeValues[0].floatValue, m_SizeValues[1].floatValue };
                EditorGUI.BeginChangeCheck();
                EditorGUI.MultiFloatField(rect, k_SizeContent, k_SizeSubContent, size);
                if (EditorGUI.EndChangeCheck())
                {
                    for (int i = 0; i < 2; ++i)
                        UpdateSize(i, Mathf.Max(0, size[i]));
                }
                EditorGUI.showMixedValue = savedHasMultipleDifferentValue;
                EditorGUI.EndProperty();
                EditorGUI.EndProperty();

                EditorGUI.BeginProperty(rect, k_ProjectionDepthContent, m_SizeValues[2]);
                EditorGUI.BeginChangeCheck();
                float newSizeZ = EditorGUILayout.FloatField(k_ProjectionDepthContent, m_SizeValues[2].floatValue);
                if (EditorGUI.EndChangeCheck())
                    UpdateSize(2, Mathf.Max(0, newSizeZ));

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_Offset, k_Offset);
                if (EditorGUI.EndChangeCheck())
                    ReinitSavedRatioSizePivotPosition();
                EditorGUI.EndProperty();

                DecalMaterialFieldWithButton(m_MaterialProperty);

                bool decalLayerEnabled = false;
                if (hdrp != null)
                {
                    decalLayerEnabled = supportDecals && hdrp.currentPlatformRenderPipelineSettings.supportDecalLayers;
                    using (new EditorGUI.DisabledScope(!decalLayerEnabled))
                    {
                        DoRenderingLayerMask();
                    }
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(m_DrawDistanceProperty, k_DistanceContent);
                if (EditorGUI.EndChangeCheck() && m_DrawDistanceProperty.floatValue < 0f)
                    m_DrawDistanceProperty.floatValue = 0f;
                if (m_DrawDistanceProperty.floatValue > DecalSystem.instance.DrawDistance)
                {
                    EditorGUILayout.HelpBox(String.Format(DecalSystem.s_GlobalDrawDistanceWarning, DecalSystem.instance.DrawDistance), MessageType.Warning);
                }

                EditorGUILayout.PropertyField(m_FadeScaleProperty, k_FadeScaleContent);
                using (new EditorGUI.DisabledScope(!decalLayerEnabled))
                {
                    DoAngleFade();
                }

                if (!decalLayerEnabled)
                {
                    HDEditorUtils.QualitySettingsHelpBox("Enable 'Decal Layers' in your HDRP Asset if you want to control the Angle Fade. There is a performance cost of enabling this option.",
                        MessageType.Info,
                        HDRenderPipelineUI.ExpandableGroup.Rendering,
                        HDRenderPipelineUI.ExpandableRendering.Decal, "m_RenderPipelineSettings.supportDecalLayers");
                    EditorGUILayout.Space();
                }

                EditorGUILayout.PropertyField(m_UVScaleProperty, k_UVScaleContent);
                EditorGUILayout.PropertyField(m_UVBiasProperty, k_UVBiasContent);
                EditorGUILayout.PropertyField(m_FadeFactor, k_FadeFactorContent);

                // only display the affects transparent property if material is HDRP/decal
                if (showAffectTransparencyHaveMultipleDifferentValue)
                {
                    using (new EditorGUI.DisabledScope(true))
                        EditorGUILayout.LabelField(EditorGUIUtility.TrTextContent("Multiple material type in selection"));
                }
                else if (showAffectTransparency)
                {
                    EditorGUILayout.PropertyField(m_AffectsTransparencyProperty, k_AffectTransparentContent);
                    if (m_AffectsTransparencyProperty.boolValue && !DecalSystem.instance.IsAtlasAllocatedSuccessfully())
                        EditorGUILayout.HelpBox(DecalSystem.s_AtlasSizeWarningMessage, MessageType.Warning);
                }

                if (showTransparentTextureResolution)
                {
                    var scalableSetting = hdrp.currentPlatformRenderPipelineSettings.decalSettings.transparentTextureResolution;
                    m_TransparentTextureResolution.LevelAndIntGUILayout(k_TransparentTextureResolutionContent, scalableSetting, hdrp.name);
                }
            }
            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();

            if (layerMaskHasMultipleValue || layerMask != (target as Component).gameObject.layer)
            {
                foreach (var decalProjector in targets)
                {
                    (decalProjector as DecalProjector).OnValidate();
                }
            }

            if (m_MaterialEditor != null)
            {
                // We need to prevent the user to edit default decal materials
                bool isDefaultMaterial = false;
                bool isValidDecalMaterial = true;
                if (hdrp != null)
                {
                    foreach (var decalProjector in targets)
                    {
                        var mat = (decalProjector as DecalProjector).material;

                        isDefaultMaterial |= mat == hdrp.GetDefaultDecalMaterial();
                        isValidDecalMaterial &= mat != null && DecalSystem.IsDecalMaterial(mat);
                    }
                }

                if (isValidDecalMaterial)
                {
                    // Draw the material's foldout and the material shader field
                    // Required to call m_MaterialEditor.OnInspectorGUI ();
                    m_MaterialEditor.DrawHeader();

                    using (new EditorGUI.DisabledGroupScope(isDefaultMaterial))
                    {
                        // Draw the material properties
                        // Works only if the foldout of m_MaterialEditor.DrawHeader () is open
                        m_MaterialEditor.OnInspectorGUI();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Decal only work with Decal Material. Decal Material can be selected in the shader list HDRP/Decal or can be created from a Decal Master Node.",
                        MessageType.Error);
                }
            }
        }

        internal void DecalMaterialFieldWithButton(SerializedProperty prop)
        {
            const int k_NewFieldWidth = 70;

            var rect = EditorGUILayout.GetControlRect();
            rect.xMax -= k_NewFieldWidth + 2;

            EditorGUI.PropertyField(rect, prop);

            var newFieldRect = rect;
            newFieldRect.x = rect.xMax + 2;
            newFieldRect.width = k_NewFieldWidth;

            if (!EditorGUI.DropdownButton(newFieldRect, k_NewDecalMaterialButtonText, FocusType.Keyboard))
                return;

            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent(k_NewDecalText), false, () => CreateDefaultDecalMaterial(target as MonoBehaviour, DefaultDecal.HDRPDecal));
            menu.AddItem(new GUIContent(k_NewSGDecalText), false, () => CreateDefaultDecalMaterial(target as MonoBehaviour, DefaultDecal.SGDecal));
            menu.DropDown(newFieldRect);
        }

        public static void CreateDefaultDecalMaterial(MonoBehaviour obj, DefaultDecal defaultDecal)
        {
            string materialName = "";
            var materialIcon = AssetPreview.GetMiniTypeThumbnail(typeof(Material));

            var action = ScriptableObject.CreateInstance<DoCreateDecalDefaultMaterial>();
            action.decalProjector = obj as DecalProjector;

            switch (defaultDecal)
            {
                case DefaultDecal.HDRPDecal:
                    materialName = "New " + k_NewDecalText;
                    action.isShaderGraph = false;
                    break;
                case DefaultDecal.SGDecal:
                    materialName = "New " + k_NewSGDecalText;
                    action.isShaderGraph = true;
                    break;
                default:
                    Debug.LogError("Decal creation failed.");
                    break;
            }

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, action, materialName, materialIcon, null);
        }

        [Shortcut("HDRP/Decal: Handle changing size stretching UV", typeof(SceneView), KeyCode.Keypad1, ShortcutModifiers.Action)]
        static void EnterEditModeWithoutPreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            ChangeEditMode(k_EditShapeWithoutPreservingUV, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Handle changing size cropping UV", typeof(SceneView), KeyCode.Keypad2, ShortcutModifiers.Action)]
        static void EnterEditModePreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            ChangeEditMode(k_EditShapePreservingUV, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Handle changing pivot position and UVs", typeof(SceneView), KeyCode.Keypad3, ShortcutModifiers.Action)]
        static void EnterEditModePivotPreservingUV(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            ChangeEditMode(k_EditUVAndPivot, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Handle swap between cropping and stretching UV", typeof(SceneView), KeyCode.Keypad4, ShortcutModifiers.Action)]
        static void SwappingEditUVMode(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            SceneViewEditMode targetMode = SceneViewEditMode.None;
            switch (editMode)
            {
                case k_EditShapePreservingUV:
                case k_EditUVAndPivot:
                    targetMode = k_EditShapeWithoutPreservingUV;
                    break;
                case k_EditShapeWithoutPreservingUV:
                    targetMode = k_EditShapePreservingUV;
                    break;
            }
            if (targetMode != SceneViewEditMode.None)
                ChangeEditMode(targetMode, GetBoundsGetter(activeDecalProjector)(), FindEditorFromSelection());
        }

        [Shortcut("HDRP/Decal: Stop Editing", typeof(SceneView), KeyCode.Keypad0, ShortcutModifiers.Action)]
        static void ExitEditMode(ShortcutArguments args)
        {
            //If editor is not there, then the selected GameObject does not contains a DecalProjector
            DecalProjector activeDecalProjector = Selection.activeGameObject?.GetComponent<DecalProjector>();
            if (activeDecalProjector == null || activeDecalProjector.Equals(null))
                return;

            QuitEditMode();
        }
    }

    class DoCreateDecalDefaultMaterial : ProjectWindowCallback.EndNameEditAction
    {
        public DecalProjector decalProjector;
        public bool isShaderGraph = false;
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            string shaderGraphName = AssetDatabase.GenerateUniqueAssetPath(pathName + ".shadergraph");
            string materialName = AssetDatabase.GenerateUniqueAssetPath(pathName + ".mat");
            Shader shader = null;

            if (isShaderGraph)
            {
                shader = DecalSubTarget.CreateDecalGraphAtPath(shaderGraphName);
            }
            else
            {
                shader = Shader.Find("HDRP/Decal");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialName);
                ProjectWindowUtil.ShowCreatedAsset(material);
                decalProjector.material = material;
            }
        }
    }

    [EditorTool(Description, typeof(DecalProjector), toolPriority = (int)Mode)]
    internal class DecalProjectorModifyScaleTool : GenericEditorTool<DecalProjector>
    {
        private const string Description = DecalProjectorEditor.k_EditShapeWithoutPreservingUVTooltip;
        private const EditMode.SceneViewEditMode Mode = DecalProjectorEditor.k_EditShapeWithoutPreservingUV;
        private const string IconName = "d_ScaleTool";

        protected DecalProjectorModifyScaleTool() : base(Description, Mode, IconName) { }
    }

    [EditorTool(Description, typeof(DecalProjector), toolPriority = (int)Mode)]
    internal class DecalProjectorEditShapeTool : GenericEditorTool<DecalProjector>
    {
        private const string Description = DecalProjectorEditor.k_EditShapePreservingUVTooltip;
        private const EditMode.SceneViewEditMode Mode = DecalProjectorEditor.k_EditShapePreservingUV;
        private const string IconName = "d_RectTool";

        protected DecalProjectorEditShapeTool() : base(Description, Mode, IconName) { }
    }

    [EditorTool(Description, typeof(DecalProjector), toolPriority = (int)Mode)]
    internal class DecalProjectorEditTool : GenericEditorTool<DecalProjector>
    {
        private const string Description = DecalProjectorEditor.k_EditUVTooltip;
        private const EditMode.SceneViewEditMode Mode = DecalProjectorEditor.k_EditUVAndPivot;
        private const string IconName = "d_MoveTool";

        protected DecalProjectorEditTool() : base(Description, Mode, IconName) { }
    }
}
