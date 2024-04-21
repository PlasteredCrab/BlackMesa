using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa;

public class SecurityCamera : NetworkBehaviour, INightVisionCamera
{
    [SerializeField]
    private Camera camera;
    [SerializeField]
    private Light nightVisionLight;

    public Camera Camera => camera;

    public Light NightVisionLight => nightVisionLight;

    private void Start()
    {
        camera.targetTexture = new RenderTexture(camera.targetTexture);
        SecurityCameraManager.Instance.AssignSecurityCameraFeed(this);
    }
}
