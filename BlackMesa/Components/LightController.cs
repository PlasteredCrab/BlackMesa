using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace BlackMesa.Components;

public class LightController : MonoBehaviour
{
    private class LightSwitcherGroup()
    {
        internal List<LightSwitcher> lights = [];
        internal bool wasOn = false;
        internal float timeOffset = 0;
        internal float flickerSpeed = 1;
        internal bool playedFlickerSound = false;
    }

    private enum State
    {
        Idle,
        TurningOff,
        TurningOn,
        Flickering,
    }

    public AnimationCurve flickeringLightEnabledAnimation;
    public int groupCount;
    public float flickerTime = 0.37f;

    private System.Random random;
    private LightSwitcherGroup[] lightSwitcherGroups;
    private State state = State.TurningOn;
    private float animationTime = 0;

    private void PopulateLightGroups()
    {
        if (lightSwitcherGroups != null)
            return;

        random = new System.Random(StartOfRound.Instance.randomMapSeed);

        var allLightSwitchers = LightSwitcher.lightSwitchers.OrderBy(s => s.GetInstanceID()).ToList();
        lightSwitcherGroups = new LightSwitcherGroup[groupCount];

        var lightsPerGroup = allLightSwitchers.Count / groupCount;
        var group = 0;
        while (group < groupCount)
        {
            var groupLights = new List<LightSwitcher>();

            var lightsAdded = 0;
            while (lightsAdded < lightsPerGroup)
            {
                var popIndex = random.Next(0, allLightSwitchers.Count);
                groupLights.Add(allLightSwitchers[popIndex]);
                allLightSwitchers.RemoveAt(popIndex);

                lightsAdded++;
            }

            lightSwitcherGroups[group] = new LightSwitcherGroup() {
                lights = groupLights,
                timeOffset = (float)random.NextDouble() * 0.2f,
            };

            group++;
        }

        lightSwitcherGroups[^1].lights.AddRange(allLightSwitchers);
    }

    public void EnableLights()
    {
        animationTime = 0;
        state = State.TurningOn;
    }

    public void DisableLights()
    {
        animationTime = 0;
        state = State.TurningOff;
    }

    public void FlickerLights()
    {
        PopulateLightGroups();

        foreach (var group in lightSwitcherGroups)
        {
            group.flickerSpeed = 0.6f + (float)random.NextDouble() * 0.8f;
            group.playedFlickerSound = false;
        }

        animationTime = 0;
        state = State.Flickering;
    }

    private void Update()
    {
        if (state == State.Idle)
            return;

        PopulateLightGroups();

        animationTime += Time.deltaTime;
        var allComplete = true;

        for (var i = 0; i < lightSwitcherGroups.Length; i++)
        {
            var group = lightSwitcherGroups[i];
            var on = group.wasOn;
            var sound = true;
            var offsetTime = animationTime - group.timeOffset;
            var stateComplete = false;

            if (state == State.TurningOn)
            {
                on = offsetTime >= 0;
                stateComplete = on;
            }
            else if (state == State.TurningOff)
            {
                on = offsetTime < 0;
                stateComplete = !on;
            }
            else if (state == State.Flickering)
            {
                var time = Mathf.Min((animationTime - group.timeOffset) / flickerTime * group.flickerSpeed, 1);

                if (time >= 0)
                {
                    if (!group.playedFlickerSound)
                    {
                        foreach (var light in group.lights)
                            light.PlayFlickerSound();
                        group.playedFlickerSound = true;
                    }
                    on = flickeringLightEnabledAnimation.Evaluate(time) > 0.5f;
                }

                stateComplete = time >= 1;
                sound = false;
            }

            if (!stateComplete)
                allComplete = false;

            if (on == group.wasOn)
                continue;

            foreach (var light in group.lights)
                light.SwitchLight(on, sound);
            group.wasOn = on;
        }

        if (allComplete)
            state = State.Idle;
    }
}
