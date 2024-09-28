using System;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class ChargingStation : StationBase
{
    public float batterySyncInterval = 0.25f;
    public float batteryChargePerSecond = 0.2f;

    private GrabbableObject itemBeingCharged;

    private float lastBatterySync = 0f;

    protected override void Start()
    {
        base.Start();
    }

    protected override void UpdateInteractabilityWithCapacity()
    {
        var heldItem = GameNetworkManager.Instance.localPlayerController.currentlyHeldObjectServer;

        if (heldItem == null || !heldItem.itemProperties.requiresBattery)
        {
            triggerScript.disabledHoverTip = "Use to charge item batteries";
            triggerScript.interactable = false;
            return;
        }

        if (heldItem.insertedBattery.charge >= 1)
        {
            triggerScript.disabledHoverTip = "Battery is full";
            triggerScript.interactable = false;
            return;
        }

        triggerScript.interactable = true;
    }

    protected override void OnActivatedByPlayer(PlayerControllerB player, ref float capacityInterpolationDelay)
    {
        if (isActiveOnLocalClient)
            capacityInterpolationDelay = batterySyncInterval;

        itemBeingCharged = player.currentlyHeldObjectServer;
    }

    protected override TickResult DoActiveTick()
    {
        TickResult result = TickResult.Continue;

        if (!itemBeingCharged.itemProperties.requiresBattery)
            return TickResult.Deactivate;

        var battery = itemBeingCharged.insertedBattery;

        var chargeAmount = batteryChargePerSecond * Time.deltaTime;
        chargeAmount = Math.Min(chargeAmount, remainingCapacity);
        var newCharge = battery.charge + chargeAmount;

        if (newCharge >= 1)
        {
            newCharge = 1;
            result = TickResult.Deactivate;
        }

        var chargeDelta = battery.charge - newCharge;
        SetBatteryChargeOnLocalClient(itemBeingCharged, newCharge);

        if (Time.time - lastBatterySync >= batterySyncInterval)
            SyncBatteryCharge();

        ConsumeCapacity(chargeAmount);

        return result;
    }

    protected override void OnActiveTickingEnded()
    {
        SyncBatteryCharge();
    }

    private void SyncBatteryCharge()
    {
        lastBatterySync = Time.time;
        SyncBatteryChargeServerRpc(itemBeingCharged, itemBeingCharged.insertedBattery.charge);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SyncBatteryChargeServerRpc(NetworkBehaviourReference item, float charge)
    {
        SyncBatteryChargeClientRpc(item, charge);
    }

    [ClientRpc]
    private void SyncBatteryChargeClientRpc(NetworkBehaviourReference item, float charge)
    {
        if (isActiveOnLocalClient)
            return;
        if (!item.TryGet(out GrabbableObject itemDeref))
            return;
        SetBatteryChargeOnLocalClient(itemDeref, charge);
    }

    private void SetBatteryChargeOnLocalClient(GrabbableObject item, float charge)
    {
        item.insertedBattery.charge = charge;
        item.insertedBattery.empty = charge <= 0;
        item.ChargeBatteries();
    }
}
