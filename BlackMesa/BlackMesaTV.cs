using Unity.Netcode;
using UnityEngine;
using UnityEngine.Video;

namespace BlackMesa;
public class BlackMesaTV : NetworkBehaviour
{
    public bool tvOn;

    private bool wasTvOnLastFrame;

    public MeshRenderer tvMesh;

    public VideoPlayer video;

    [Space(5f)]
    public VideoClip[] tvClips;

    public AudioClip[] tvAudioClips;

    [Space(5f)]
    private float currentClipTime;

    private int currentClip;

    public Material tvOnMaterial;

    public Material tvOffMaterial;

    public AudioClip switchTVOn;

    public AudioClip switchTVOff;

    public AudioSource tvSFX;

    private float timeSinceTurningOffTV;

    public Light tvLight;

    public void TurnTVOnOff(bool on)
    {
        Debug.Log("TurnTVOnOff");
        tvOn = on;
        if (on)
        {
            tvSFX.clip = tvAudioClips[currentClip];
            tvSFX.time = currentClipTime;
            tvSFX.Play();
            tvSFX.PlayOneShot(switchTVOn);
            WalkieTalkie.TransmitOneShotAudio(tvSFX, switchTVOn);
        }
        else
        {
            tvSFX.Stop();
            tvSFX.PlayOneShot(switchTVOff);
            WalkieTalkie.TransmitOneShotAudio(tvSFX, switchTVOff);
        }
    }

    public void SwitchTVLocalClient()
    {
        Debug.Log("SwitchTVLocalClient");
        if (tvOn)
        {
            TurnOffTVServerRpc();
        }
        else
        {
            TurnOnTVServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void TurnOnTVServerRpc()
    {
        Debug.Log("TurnOnTVServerRpc");
        {
            timeSinceTurningOffTV = 0f;
            if (timeSinceTurningOffTV > 7f)
            {
                TurnOnTVAndSyncClientRpc(currentClip, currentClipTime);
            }
            else
            {
                TurnOnTVClientRpc();
            }
        }
    }
    [ClientRpc]
    public void TurnOnTVClientRpc()
    {
        Debug.Log("TurnOnTVClientRpc");
        TurnTVOnOff(on: true);
    }

    [ClientRpc]
    public void TurnOnTVAndSyncClientRpc(int clipIndex, float clipTime)
    {
        Debug.Log("TurnOnTVAndSyncClientRpc");
        currentClip = clipIndex;
        currentClipTime = clipTime;
        TurnTVOnOff(on: true);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TurnOffTVServerRpc()
    {
        TurnOffTVClientRpc();
    }

    [ClientRpc]
    public void TurnOffTVClientRpc()
    {
        Debug.Log("TurnOffTVClientRpc");
        TurnTVOnOff(on: false);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SyncTVServerRpc()
    {
        SyncTVClientRpc(currentClip, currentClipTime, tvOn);
    }

    [ClientRpc]
    public void SyncTVClientRpc(int clipIndex, float clipTime, bool isOn)
    {
        SyncTimeAndClipWithClients(clipIndex, clipTime, isOn);
    }

    private void SyncTimeAndClipWithClients(int clipIndex, float clipTime, bool isOn)
    {
        currentClip = clipIndex;
        currentClipTime = clipTime;
        tvOn = isOn;
    }

    private void OnEnable()
    {
        Debug.Log("OnEnable");
        video.loopPointReached += TVFinishedClip;
    }

    private void OnDisable()
    {
        Debug.Log("OnDisable");
        video.loopPointReached -= TVFinishedClip;
    }

    private void TVFinishedClip(VideoPlayer source)
    {
        if (tvOn && !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
        {
            currentClip = (currentClip + 1) % tvClips.Length;
            video.clip = tvClips[currentClip];
            video.Play();
            tvSFX.clip = tvAudioClips[currentClip];
            tvSFX.time = 0f;
            tvSFX.Play();
        }
    }

    private void Update()
    {
        //Debug.Log("Update");
        if (NetworkManager.Singleton.ShutdownInProgress || GameNetworkManager.Instance.localPlayerController == null)
        {
            return;
        }
        if (!tvOn)// || !GameNetworkManager.Instance.localPlayerController.isInsideFactory)
        {
            Debug.Log("TV is off");
            if (wasTvOnLastFrame)
            {
                wasTvOnLastFrame = false;
                SetTVScreenMaterial(on: false);
                currentClipTime = (float)video.time;
                video.Stop();
            }
            if (base.IsServer && !tvOn)
            {
                timeSinceTurningOffTV += Time.deltaTime;
            }
            currentClipTime += Time.deltaTime;
            if ((double)currentClipTime > tvClips[currentClip].length)
            {
                currentClip = (currentClip + 1) % tvClips.Length;
                currentClipTime = 0f;
                if (tvOn)
                {
                    tvSFX.clip = tvAudioClips[currentClip];
                    tvSFX.Play();
                }
            }
        }
        else
        {
            if (!wasTvOnLastFrame)
            {
                Debug.Log("TV is on");
                wasTvOnLastFrame = true;
                SetTVScreenMaterial(on: true);
                video.clip = tvClips[currentClip];
                video.time = currentClipTime;
                video.Play();
            }
            currentClipTime = (float)video.time;
        }
    }

    private void SetTVScreenMaterial(bool on)
    {
        Material[] sharedMaterials = tvMesh.sharedMaterials;
        if (on)
        {
            sharedMaterials[1] = tvOnMaterial;
        }
        else
        {
            sharedMaterials[1] = tvOffMaterial;
        }
        tvMesh.sharedMaterials = sharedMaterials;
        tvLight.enabled = on;
    }
}

