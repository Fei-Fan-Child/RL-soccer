
using LPCG;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
Just an example of how to switch day and night at runtime
Requires DayNight prefab to be in scene (Hierarchy)
*/

namespace LPCG
{
    public class ShiftAtRuntime : MonoBehaviour
    {
        DayNight dayNight;

        public LP_CityGenerator cityGenerator;

        [Space(10)]
        [Range(70, 130)]
        public float downtownSize = 100;

        private void Start()
        {
            dayNight = FindObjectOfType<DayNight>();

#if !ENABLE_LEGACY_INPUT_MANAGER
            Debug.LogWarning("⚠️ Input System is set to 'New' only. This script uses the old Input Manager. To fix this, go to Edit > Project Settings > Player > Active Input Handling and set it to 'Both'.");
#endif

        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER

            if (Input.GetKeyDown(KeyCode.N))
            {
                if (dayNight)
                {
                    dayNight.isNight = !dayNight.isNight;
                    dayNight.ChangeMaterial();
                }
            }

            if (Input.GetKeyDown(KeyCode.B))
            {
                if (GameObject.Find("LP-City") == null)
                    return;

                if (cityGenerator)
                {
                    cityGenerator.GenerateAllBuildings(true, downtownSize);
                }
                else
                    Debug.Log("CityGenerator not assigned in inspector");

                if (dayNight && dayNight.isNight)
                {
                    dayNight.SetStreetLights(true);
                }
            }
#endif
        }
    }
}
