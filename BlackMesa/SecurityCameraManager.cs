using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Rendering;
using UnityEngine;
using DunGen.Graph;
using DunGen;
using System.Linq;

namespace BlackMesa
{
    internal class SecurityCameraManager : MonoBehaviour
    {
        internal List<SecurityCamera> securityCameras = new List<SecurityCamera>();
        internal List<HandheldTVCamera> handheldTVCameras = new List<HandheldTVCamera>();

        internal HashSet<Camera> nightVisionCameraSet = new HashSet<Camera>();
        internal List<Light> nightVisionLights = new List<Light>();

        internal static SecurityCameraManager Instance;

        [SerializeField]
        public MeshRenderer handheldTVTerminal;
        [SerializeField]
        public List<int> handheldTVMaterialIndices;
        [SerializeField]
        public List<BoxCollider> handheldTVTerminalScreenColliders;
        private Bounds[] handheldTVTerminalScreenBounds;
        int currentHandheldTVIndex;

        [SerializeField]
        public MeshRenderer securityFeedTerminal;
        [SerializeField]
        public List<int> securityCameraMaterialIndices;
        [SerializeField]
        public List<BoxCollider> securityFeedTerminalScreenColliders;
        private Bounds[] securityFeedTerminalScreenBounds;
        int currentSecurityCameraIndex;

        private Camera[] allCameras = [];
        private readonly Plane[] frustumPlanes = new Plane[6];

        private const float ActiveTerminalDistance = 15;
        private const float ActiveTerminalDistanceSqr = ActiveTerminalDistance * ActiveTerminalDistance;

        private void Start()
        {
            securityFeedTerminalScreenBounds = new Bounds[securityFeedTerminalScreenColliders.Count];
            for (var i = 0; i < securityFeedTerminalScreenColliders.Count; i++)
                securityFeedTerminalScreenBounds[i] = securityFeedTerminalScreenColliders[i].bounds;

            handheldTVTerminalScreenBounds = new Bounds[handheldTVTerminalScreenColliders.Count];
            for (var i = 0; i < handheldTVTerminalScreenColliders.Count; i++)
                handheldTVTerminalScreenBounds[i] = handheldTVTerminalScreenColliders[i].bounds;
        }

        private void AddNightVisionCamera(INightVisionCamera nightVisionCamera)
        {
            nightVisionCameraSet.Add(nightVisionCamera.Camera);
            nightVisionLights.Add(nightVisionCamera.NightVisionLight);
        }

        public void AssignSecurityCameraFeed(SecurityCamera securityCamera)
        {
            if (currentSecurityCameraIndex >= securityCameraMaterialIndices.Count)
            {
                return;
            }

            var securityCameraMaterialIndex = securityCameraMaterialIndices[currentSecurityCameraIndex];
            securityFeedTerminal.sharedMaterials[securityCameraMaterialIndex].mainTexture = securityCamera.Camera.targetTexture;
            currentSecurityCameraIndex++;

            Debug.Log("Added security camera to nightvision camera list");
            securityCameras.Add(securityCamera);
            AddNightVisionCamera(securityCamera);
        }

        public void AssignHandheldTVFeed(HandheldTVCamera handheldTVCamera, Material targetMaterial)
        {
            if (currentHandheldTVIndex >= handheldTVMaterialIndices.Count)
            {
                return;
            }

            var materials = handheldTVTerminal.sharedMaterials;
            var index = handheldTVMaterialIndices[currentHandheldTVIndex];
            materials[index] = targetMaterial;

            handheldTVTerminal.sharedMaterials = materials;
            currentHandheldTVIndex++;

            Debug.Log("Added handheld TV to nightvision camera list");
            handheldTVCameras.Add(handheldTVCamera);
            AddNightVisionCamera(handheldTVCamera);
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

        public bool IsBoundingBoxVisibleOnOtherCameras(Bounds bounds)
        {
            if (allCameras.Length != Camera.allCamerasCount)
                allCameras = new Camera[Camera.allCamerasCount];
            Camera.GetAllCameras(allCameras);

            foreach (var camera in allCameras)
            {
                // Skip if the camera can't see the default layer.
                if ((camera.cullingMask & 1) == 0)
                    continue;
                if (camera is null || nightVisionCameraSet.Contains(camera))
                    continue;
                if ((bounds.center - camera.transform.position).sqrMagnitude > ActiveTerminalDistanceSqr)
                    continue;

                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    return true;
            }

            return false;
        }

        public void Update()
        {
            for (var i = 0; i < securityCameras.Count; i++)
            {
                var securityCamera = securityCameras[i];
                bool enabled = securityFeedTerminal.isVisible;

                if (enabled && i < securityFeedTerminalScreenBounds.Length)
                    enabled = IsBoundingBoxVisibleOnOtherCameras(securityFeedTerminalScreenBounds[i]);

                securityCamera.Camera.enabled = enabled;
            }

            for (var i = 0; i < handheldTVCameras.Count; i++)
            {
                var handheldTVCamera = handheldTVCameras[i];
                var enabled = handheldTVCamera.isBeingUsed;

                if (enabled)
                {
                    if (handheldTVCamera.mainObjectRenderer.isVisible)
                    {
                        enabled = true;
                    }
                    else if (handheldTVTerminal.isVisible)
                    {
                        if (i < handheldTVTerminalScreenBounds.Length)
                            enabled = IsBoundingBoxVisibleOnOtherCameras(handheldTVTerminalScreenBounds[i]);
                    }
                }

                handheldTVCamera.Camera.enabled = enabled;
            }
        }

        public void UpdateVisibleLights(ScriptableRenderContext _, Camera camera)
        {
            var nightVisionVisible = nightVisionCameraSet.Contains(camera);

            foreach (var nightVisionLight in nightVisionLights)
            {
                if (nightVisionLight == null)
                    continue;
                nightVisionLight.enabled = nightVisionVisible;
            }
        }

        public void OnDisable()
        {
            //Debug.Log($"Security Camera Manager Disabled");
            RenderPipelineManager.beginCameraRendering -= UpdateVisibleLights;
        }
    }
}
