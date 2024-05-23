using UnityEngine;

namespace BlackMesa;
public class InstructionManual : GrabbableObject
{
    public int currentPage = 1;

    public Animator clipboardAnimator;

    public AudioClip[] turnPageSFX;

    public AudioSource thisAudio;

    public override void PocketItem()
    {
        if (base.IsOwner && playerHeldBy != null)
        {
            playerHeldBy.equippedUsableItemQE = false;
            isBeingUsed = false;
        }
        base.PocketItem();
    }

    public override void ItemInteractLeftRight(bool right)
    {
        int num = currentPage;
        RequireCooldown();
        if (right)
        {
            currentPage = Mathf.Clamp(currentPage + 1, 1, 4);
        }
        else
        {
            currentPage = Mathf.Clamp(currentPage - 1, 1, 4);
        }
        if (currentPage != num)
        {
            RoundManager.PlayRandomClip(thisAudio, turnPageSFX);
        }
        clipboardAnimator.SetInteger("page", currentPage);
    }

    public override void DiscardItem()
    {
        if (playerHeldBy != null)
        {
            playerHeldBy.equippedUsableItemQE = false;
        }
        isBeingUsed = false;
        base.DiscardItem();
    }

    public override void EquipItem()
    {
        base.EquipItem();
        playerHeldBy.equippedUsableItemQE = true;
        if (base.IsOwner)
        {
            HUDManager.Instance.DisplayTip("To read the manual:", "Press Z to inspect closely. Press Q and E to flip the pages.", isWarning: false, useSave: true, "LCTip_UseManual");
        }
    }
}
