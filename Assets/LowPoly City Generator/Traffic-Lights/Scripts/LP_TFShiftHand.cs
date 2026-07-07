using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LPCG
{
    public class LP_TFShiftHand : MonoBehaviour
    {

        public GameObject rightHandObjects;
        public GameObject leftHandObjects;

        public void RightHand(int active)
        {
            rightHandObjects.SetActive(active == 0);
            leftHandObjects.SetActive(active != 0);
        }


    }
}