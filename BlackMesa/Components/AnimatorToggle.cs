using System.Collections;
using UnityEngine;

namespace BlackMesa.Components;

public class AnimatorToggle : MonoBehaviour
{
    private Animator animator;

    private void Start()
    {
        animator = GetComponent<Animator>();
        animator.keepAnimatorStateOnDisable = true;
    }

    public void EnableAnimator()
    {
        animator.enabled = true;
    }

    public void CompleteAnimationAndDisableAnimator()
    {
        StartCoroutine(DisableAtEndOfFrame());
    }

    private IEnumerator DisableAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        animator.enabled = false;
    }
}
