using System;
using RooseLabs.Player;
using RooseLabs.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RooseLabs.Gameplay
{
    [RequireComponent(typeof(Renderer))]
    public class PlanarReflection : MonoBehaviour
    {
        private static readonly int ReflectionTex = Shader.PropertyToID("_ReflectionTex");

        private enum ReflectionNormal
        {
            Up,
            Down,
            Left,
            Right,
            Forward,
            Back
        }

        #region Serialized
        [SerializeField] private ReflectionNormal reflectionNormal = ReflectionNormal.Forward;
        [SerializeField] private LayerMask reflectionMask = -1;
        [SerializeField] private float clipPlaneOffset = 0;
        [SerializeField][Min(1f)] private float farClipPlane = 1000;
        [SerializeField][Range(0.01f, 1f)] private float renderTextureScale = 1f;
        [SerializeField] private bool showHiddenCamera = false;
        #endregion

        private Camera m_reflectionCamera;
        private Renderer m_renderer;
        private Material m_material;

        private int ScaledWidth => Mathf.Max(1, (int)(Screen.width * renderTextureScale));
        private int ScaledHeight => Mathf.Max(1, (int)(Screen.height * renderTextureScale));

        private bool HasScreenSizeChanged => m_reflectionCamera.targetTexture.width != ScaledWidth ||
                                             m_reflectionCamera.targetTexture.height != ScaledHeight;

        private Vector3 ReflectionNormalVector
        {
            get
            {
                return reflectionNormal switch
                {
                    ReflectionNormal.Up => transform.up,
                    ReflectionNormal.Down => -transform.up,
                    ReflectionNormal.Left => -transform.right,
                    ReflectionNormal.Right => transform.right,
                    ReflectionNormal.Forward => transform.forward,
                    ReflectionNormal.Back => -transform.forward,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }

        private void Reset()
        {
            if (m_reflectionCamera)
            {
                if (Application.isPlaying)
                    Destroy(m_reflectionCamera.gameObject);
                else
                    DestroyImmediate(m_reflectionCamera.gameObject);
                m_reflectionCamera = null;
            }
        }

        private void Start()
        {
            Init();
        }

        private void OnValidate()
        {
            reflectionMask &= ~HelperFunctions.MirrorCullLayerMask;
        }

        private void Init()
        {
            if (!m_reflectionCamera)
            {
                var cameraObject = new GameObject($"PlanarReflectionCam_{GetInstanceID()}", typeof(UniversalAdditionalCameraData));
                cameraObject.transform.SetParent(transform);
                cameraObject.transform.localPosition = Vector3.zero;
                cameraObject.transform.localRotation = Quaternion.identity;

                m_reflectionCamera = cameraObject.GetComponent<Camera>();
                m_reflectionCamera.enabled = false; // We will render the camera manually.
                m_renderer = GetComponent<Renderer>();
                m_material = m_renderer.material;
            }

            var oldRenderTexture = m_reflectionCamera.targetTexture;
            var renderTexture = new RenderTexture(ScaledWidth, ScaledHeight, 16)
            {
                name = $"PlanarReflectionRT_{GetInstanceID()}",
                useMipMap = false,
                autoGenerateMips = false,
                anisoLevel = 0,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            m_reflectionCamera.targetTexture = renderTexture;
            m_reflectionCamera.gameObject.hideFlags = showHiddenCamera ? HideFlags.NotEditable | HideFlags.DontSave : HideFlags.HideAndDontSave;

            if (oldRenderTexture)
            {
                oldRenderTexture.Release();
            }

            if (m_material)
            {
                if (m_material.HasProperty(ReflectionTex))
                {
                    m_material.SetTexture(ReflectionTex, m_reflectionCamera.targetTexture);
                }
                else
                {
                    Debug.LogError("Material does not have _ReflectionTex property.", this);
                }
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (!PlayerCharacter.LocalCharacter) return;
            if (cam == PlayerCharacter.LocalCharacter.Camera)
            {
                Render(); // Render the reflection before the player's camera renders.
            }
            else if (cam == m_reflectionCamera)
            {
                GL.invertCulling = true;
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam == m_reflectionCamera)
                GL.invertCulling = false;
        }

        private void Render()
        {
            // Reinitialize if needed
            if (!m_reflectionCamera || !m_reflectionCamera.targetTexture || HasScreenSizeChanged)
            {
                Init();
            }

            if (!m_reflectionCamera)
            {
                Debug.LogError("Reflection camera is missing.", this);
                return;
            }

            if (!CameraUtils.VisibleFromCamera(PlayerCharacter.LocalCharacter.Camera, m_renderer))
                return;

            UpdateCameraSettings(PlayerCharacter.LocalCharacter.Camera, m_reflectionCamera);
            UpdateReflection(PlayerCharacter.LocalCharacter.Camera, m_reflectionCamera);
            m_reflectionCamera.Render();
        }

        private void UpdateCameraSettings(Camera src, Camera dest)
        {
            dest.clearFlags = src.clearFlags;
            dest.backgroundColor = src.backgroundColor;
            dest.cullingMask = reflectionMask;
            dest.orthographic = src.orthographic;
            dest.fieldOfView = src.fieldOfView;
            dest.aspect = src.aspect;
            dest.orthographicSize = src.orthographicSize;
            dest.nearClipPlane = src.nearClipPlane;
            if (farClipPlane <= src.nearClipPlane)
                farClipPlane = src.nearClipPlane + 0.01f;
            dest.farClipPlane = farClipPlane;
            dest.renderingPath = src.renderingPath;
            dest.allowHDR = src.allowHDR;
            dest.allowMSAA = src.allowMSAA;
            dest.allowDynamicResolution = src.allowDynamicResolution;
        }

        private void UpdateReflection(Camera playerCam, Camera reflectionCamera)
        {
            try
            {
                Vector3 normal = ReflectionNormalVector;
                Vector3 pos = transform.position;
                float d = -Vector3.Dot(normal, pos);
                Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

                Matrix4x4 reflection = Matrix4x4.zero;
                CalculateReflectionMatrix(ref reflection, reflectionPlane);

                reflectionCamera.worldToCameraMatrix = playerCam.worldToCameraMatrix * reflection;
                Vector4 clipPlane = CameraSpacePlane(reflectionCamera, pos, normal, 1.0f);
                reflectionCamera.projectionMatrix = playerCam.CalculateObliqueMatrix(clipPlane);
            } catch (Exception e)
            {
                Debug.LogWarning($"Error updating planar reflection: {e.Message}", this);
            }
        }

        private Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            Vector3 offsetPos = pos + normal * clipPlaneOffset;
            Matrix4x4 m = cam.worldToCameraMatrix;
            Vector3 cpos = m.MultiplyPoint(offsetPos);
            Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = (1F - 2F * plane[0] * plane[0]);
            reflectionMat.m01 = (-2F * plane[0] * plane[1]);
            reflectionMat.m02 = (-2F * plane[0] * plane[2]);
            reflectionMat.m03 = (-2F * plane[3] * plane[0]);

            reflectionMat.m10 = (-2F * plane[1] * plane[0]);
            reflectionMat.m11 = (1F - 2F * plane[1] * plane[1]);
            reflectionMat.m12 = (-2F * plane[1] * plane[2]);
            reflectionMat.m13 = (-2F * plane[3] * plane[1]);

            reflectionMat.m20 = (-2F * plane[2] * plane[0]);
            reflectionMat.m21 = (-2F * plane[2] * plane[1]);
            reflectionMat.m22 = (1F - 2F * plane[2] * plane[2]);
            reflectionMat.m23 = (-2F * plane[3] * plane[2]);

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }
    }
}
