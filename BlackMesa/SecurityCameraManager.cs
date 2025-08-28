using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine;
using BlackMesa.Components;
using BlackMesa.Utilities;
using UnityEngine.Rendering.HighDefinition;

namespace BlackMesa
{
    internal class SecurityCameraManager : MonoBehaviour
    {
        internal static SecurityCameraManager Instance;

        internal List<SecurityCamera> securityCameras = [];
        internal List<HandheldTVCamera> handheldTVCameras = [];

        public MeshRenderer handheldTVTerminal;
        public List<int> handheldTVMaterialIndices;
        public List<BoxCollider> handheldTVTerminalScreenColliders;

        public MeshRenderer securityFeedTerminal;
        public Material securityFeedMaterial;
        public List<int> securityCameraMaterialIndices;
        public List<BoxCollider> securityFeedTerminalScreenColliders;

        public int camerasToRenderPerFrame = 2;

        int currentHandheldTVIndex;
        int currentSecurityCameraIndex;

        private Camera[] allOtherCameras = [];
        private Plane[][] allOtherCamerasFrustums = [];
        private bool allOtherCameraFrustumsUpdated = false;

        private List<Camera> allControlledCameras = [];
        private List<Bounds> allControlledCameraScreenBounds = [];
        private List<Renderer[]> allControlledCameraScreenRenderers = [];
        private int nextCameraToRender = 0;

        private HashSet<Camera> nightVisionCameraSet = [];
        private List<Light> nightVisionLights = [];

        private const float ActiveTerminalDistance = 15;
        private const float ActiveTerminalDistanceSqr = ActiveTerminalDistance * ActiveTerminalDistance;

        private const float ActiveHandheldDistance = 5;
        private const float ActiveHandheldDistanceSqr = ActiveHandheldDistance * ActiveHandheldDistance;

        private void AddCamera(INightVisionCamera nightVisionCamera, Bounds screenBounds, params Renderer[] screenRenderers)
        {
            nightVisionCameraSet.Add(nightVisionCamera.Camera);
            nightVisionLights.Add(nightVisionCamera.NightVisionLight);

            allControlledCameras.Add(nightVisionCamera.Camera);
            allControlledCameraScreenBounds.Add(screenBounds);
            allControlledCameraScreenRenderers.Add(screenRenderers);

            var hdrpCamera = nightVisionCamera.Camera.GetComponent<HDAdditionalCameraData>();
            hdrpCamera.hasPersistentHistory = true;
        }

        public void AssignSecurityCameraFeed(SecurityCamera securityCamera)
        {
            if (currentSecurityCameraIndex >= securityCameraMaterialIndices.Count)
                return;

            var securityCameraMaterialIndex = securityCameraMaterialIndices[currentSecurityCameraIndex];

            var material = new Material(securityFeedMaterial)
            {
                mainTexture = securityCamera.Camera.targetTexture,
            };
            securityFeedTerminal.SetMaterial(securityCameraMaterialIndex, material);

            Debug.Log("Added security camera to nightvision camera list");
            securityCameras.Add(securityCamera);
            AddCamera(securityCamera, securityFeedTerminalScreenColliders[currentSecurityCameraIndex].bounds);

            currentSecurityCameraIndex++;
        }

        public void AssignHandheldTVFeed(HandheldTVCamera handheldTVCamera, Material material)
        {
            if (currentHandheldTVIndex >= handheldTVMaterialIndices.Count)
                return;

            var materials = handheldTVTerminal.sharedMaterials;
            var index = handheldTVMaterialIndices[currentHandheldTVIndex];
            materials[index] = material;

            handheldTVTerminal.sharedMaterials = materials;

            Debug.Log("Added handheld TV to nightvision camera list");
            handheldTVCameras.Add(handheldTVCamera);
            AddCamera(handheldTVCamera, handheldTVTerminalScreenColliders[currentHandheldTVIndex].bounds, handheldTVCamera.mainObjectRenderer);

            currentHandheldTVIndex++;
        }

        public void Awake()
        {
            Instance = this;
        }

        public void OnEnable()
        {
            //Debug.Log($"Security Camera Manager Enabled");
            RenderPipelineManager.beginCameraRendering += UpdateVisibleLights;
        }

        private void GetCameraFrustums()
        {
            if (allOtherCameraFrustumsUpdated)
                return;
            allOtherCameraFrustumsUpdated = true;

            if (allOtherCameras.Length != Camera.allCamerasCount)
            {
                allOtherCameras = new Camera[Camera.allCamerasCount];
                allOtherCamerasFrustums = new Plane[allOtherCameras.Length][];
                for (var i = 0; i < allOtherCamerasFrustums.Length; i++)
                    allOtherCamerasFrustums[i] = new Plane[6];
            }

            Camera.GetAllCameras(allOtherCameras);
            for (var i = 0; i < allOtherCameras.Length; i++)
                GeometryUtility.CalculateFrustumPlanes(allOtherCameras[i], allOtherCamerasFrustums[i]);
        }

        public bool IsBoundingBoxVisibleOnOtherCameras(Bounds bounds, float activeDistanceSqr)
        {
            for (var i = 0; i < allOtherCameras.Length; i++)
            {
                var camera = allOtherCameras[i];

                // Skip if the camera can't see the default layer.
                if ((camera.cullingMask & 1) == 0)
                    continue;
                if (camera is null || nightVisionCameraSet.Contains(camera))
                    continue;
                if ((bounds.center - camera.transform.position).sqrMagnitude > activeDistanceSqr)
                    continue;

                if (GeometryUtility.TestPlanesAABB(allOtherCamerasFrustums[i], bounds))
                    return true;
            }

            return false;
        }

        private bool ShouldRenderCamera(int cameraIndex)
        {
            if (cameraIndex < 0 || cameraIndex >= allControlledCameras.Count)
                return false;

            var camera = allControlledCameras[cameraIndex];

            if (camera == null)
                return false;
            if (!camera.gameObject.activeInHierarchy)
                return false;

            var bounds = allControlledCameraScreenBounds[cameraIndex];
            GetCameraFrustums();
            if (IsBoundingBoxVisibleOnOtherCameras(bounds, ActiveTerminalDistanceSqr))
                return true;

            var renderers = allControlledCameraScreenRenderers[cameraIndex];
            foreach (var renderer in renderers)
            {
                if (!renderer.isVisible)
                    continue;
                if (IsBoundingBoxVisibleOnOtherCameras(renderer.bounds, ActiveHandheldDistanceSqr))
                    return true;
            }

            return false;
        }

        private void LateUpdate()
        {
            if (allControlledCameras.Count == 0)
                return;

            allOtherCameraFrustumsUpdated = false;

            foreach (var camera in allControlledCameras)
            {
                if (camera == null)
                    continue;
                camera.enabled = false;
            }

            var stopIndex = (nextCameraToRender + allControlledCameras.Count - 1) % allControlledCameras.Count;
            var camerasRendered = 0;
            while (camerasRendered < camerasToRenderPerFrame)
            {
                if (ShouldRenderCamera(nextCameraToRender))
                {
                    allControlledCameras[nextCameraToRender].enabled = true;
                    camerasRendered++;
                }
                nextCameraToRender = (nextCameraToRender + 1) % allControlledCameras.Count;
                if (nextCameraToRender == stopIndex)
                    break;
            }

            // Enable the camera the local player is holding so that they don't experience a low
            // frame rate on their camera.
            var currentPlayer = GameNetworkManager.Instance?.localPlayerController;
            if (currentPlayer != null)
            {
                if (currentPlayer.spectatedPlayerScript != null)
                    currentPlayer = currentPlayer.spectatedPlayerScript;
                var heldItem = currentPlayer.currentlyHeldObjectServer;

                foreach (var handheld in handheldTVCameras)
                {
                    if (handheld == null || handheld.Camera == null)
                        continue;
                    if (handheld != heldItem)
                        continue;
                    handheld.Camera.enabled = true;
                    break;
                }
            }

            // Allow CullFactory to be aware that these will likely be visible during the render pass.
            SetNightVisionVisible(true);
        }

        public void UpdateVisibleLights(ScriptableRenderContext _, Camera camera)
        {
            SetNightVisionVisible(nightVisionCameraSet.Contains(camera));
        }

        private void SetNightVisionVisible(bool visible)
        {
            foreach (var nightVisionLight in nightVisionLights)
            {
                if (nightVisionLight == null)
                    continue;
                nightVisionLight.enabled = visible;
            }
        }

        public void OnDisable()
        {
            //Debug.Log($"Security Camera Manager Disabled");
            RenderPipelineManager.beginCameraRendering -= UpdateVisibleLights;
        }
    }
}
