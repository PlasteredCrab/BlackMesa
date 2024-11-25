using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Components;

public class AnimatorCuller : MonoBehaviour
{
    private static readonly Dictionary<Animator, AnimatorCuller> cullers = [];

    private Animator animator;

    private void OnEnable()
    {
        animator = GetComponent<Animator>();
        animator.keepAnimatorStateOnDisable = true;
        cullers.Add(animator, this);
    }

    public void CompleteAnimationAndDisableAnimator()
    {
        StartCoroutine(DisableAtEndOfFrame(animator));
    }

    private static IEnumerator DisableAtEndOfFrame(Animator animator)
    {
        yield return new WaitForEndOfFrame();

        var currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.fullPathHash != 0 && currentState.normalizedTime < 1)
            yield break;
        var nextState = animator.GetNextAnimatorStateInfo(0);
        if (nextState.fullPathHash != 0 && nextState.normalizedTime < 1)
            yield break;

        animator.enabled = false;
    }

    internal static void OnAnimationTriggered(Animator animator)
    {
        if (!cullers.TryGetValue(animator, out var culler))
            return;
        if (culler == null)
            return;
        culler.EnableAnimator();
    }

    public void EnableAnimator()
    {
        animator.enabled = true;
    }

    private void OnDisable()
    {
        cullers.Remove(animator);
    }
}
