using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Rendering;
using UnityEngine;
using DunGen.Graph;
using DunGen;

namespace BlackMesa
{
    internal class SecurityCameraManager : MonoBehaviour
    {
        internal List<INightVisionCamera> nightVisionCameras = new List<INightVisionCamera>();
        int currentNightVisionCameraIndex = 0;

        internal HashSet<Camera> nightVisionCameraSet = new HashSet<Camera>();
        internal List<Light> nightVisionLights = new List<Light>();

        internal List<Camera> lastRenderedCameras = new List<Camera>();

        private static int camerasToRenderPerFrame = 1;

        internal static SecurityCameraManager Instance;

        [SerializeField]
        public MeshRenderer handheldTVTerminal;
        [SerializeField]
        public List<int> handheldTVMaterialIndices;
        int currentHandheldTVIndex;

        [SerializeField]
        public MeshRenderer securityFeedTerminal;
        [SerializeField]
        public List<int> securityCameraMaterialIndices;
        int currentSecurityCameraIndex;

        private void AddNightVisionCamera(INightVisionCamera nightVisionCamera)
        {
            nightVisionCameras.Add(nightVisionCamera);
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

        public void Update()
        {
            if (nightVisionCameras.Count == 0)
            {
                return;
            }

            foreach (var cameraToDisable in lastRenderedCameras)
            {
                cameraToDisable.enabled = false;
            }

            lastRenderedCameras.Clear();

            int i;
            for (i = 1; i <= nightVisionCameras.Count; i++)
            {
                var checkIndex = currentNightVisionCameraIndex + i;
                var nightVisionCamera = nightVisionCameras[checkIndex % nightVisionCameras.Count];

                if (nightVisionCamera is HandheldTVCamera handheldTVCamera)
                {
                    if (!handheldTVCamera.isBeingUsed)
                        continue;

                    if (handheldTVTerminal.isVisible || handheldTVCamera.mainObjectRenderer.isVisible)
                    {
                        lastRenderedCameras.Add(handheldTVCamera.Camera);
                    }
                }
                else if (securityFeedTerminal.isVisible)
                {
                    lastRenderedCameras.Add(nightVisionCamera.Camera);
                }

                if (lastRenderedCameras.Count >= camerasToRenderPerFrame)
                    break;
            }

            foreach (var camera in lastRenderedCameras)
            {
                camera.enabled = true;
            }

            currentNightVisionCameraIndex += i;
            currentNightVisionCameraIndex %= nightVisionCameras.Count;
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
