using GameNetcodeStuff;
using UnityEngine;

namespace BlackMesa.Components;

public class ChargingStation : StationBase
{
    protected override void Start()
    {
        base.Start();
    }

    protected override void UpdateInteractabilityWithCapacity()
    {
    }

    protected override void OnActivatedByPlayer(PlayerControllerB player, ref float capacityInterpolationDelay)
    {
        BlackMesaInterior.Logger.LogInfo("Activate");
    }

    protected override TickResult DoActiveTick()
    {
        TickResult result = TickResult.Continue;
        BlackMesaInterior.Logger.LogInfo("Charge battery!");
        return result;
    }
}
