using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa;
public class DamageZone : MonoBehaviour
{

    public int DamageAmount;
    public float DamageTickRate;
    public CauseOfDeath cause;
    private float TimeSincePlayerDamaged = 0f;

    private void OnTriggerStay(Collider other)
    {
        PlayerControllerB victim = other.gameObject.GetComponent<PlayerControllerB>();
        if (!other.gameObject.CompareTag("Player"))
        {
            return;
        }
        if ((TimeSincePlayerDamaged < DamageTickRate))
        {
            TimeSincePlayerDamaged += Time.deltaTime;
            return;
        }
        if (victim != null)
        {
            TimeSincePlayerDamaged = 0f;
            victim.DamagePlayer(DamageAmount, hasDamageSFX: true, callRPC: true, cause);
            //WTOBase.LogToConsole("New health amount: " + victim.health);
        }

    }
}