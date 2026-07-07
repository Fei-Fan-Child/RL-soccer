using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LPCG
{
    public class LP_TrafficLight : MonoBehaviour
    {

        private float countTime = 0;
        private int step = 0;

        [System.Serializable]
        public class TrafficLightState
        {
            public int status = 0; // (1 and 4 = RED) , (2 = Yellow) , (3 = Green) 

            public GameObject status_GR;
            public GameObject status_RG;
            public GameObject status_YR;
            public GameObject status_RY;
            public GameObject status_RR;

            public GameObject stop_GR;
            public GameObject stop_RG;

        }


        public TrafficLightState tState;
        private Vector3 stop_GR_Default;
        private Vector3 stop_RG_Default;

        // Use this for initialization
        void Start()
        {

            countTime = 0;
            step = 0;

            stop_GR_Default = tState.stop_GR.transform.localPosition;
            stop_RG_Default = tState.stop_RG.transform.localPosition;

            tState.status = (Random.Range(1, 8) < 4) ? 13 : 31;
            EnabledObjects(tState.status);

            InvokeRepeating("Semaforo", Random.Range(0, 10), 1);

        }


        private void Semaforo()
        {
            countTime += 1;

            if (step == 0)
            {

                if (countTime > 15)
                {
                    countTime = 0;
                    step = 1;

                    if (tState.status == 13)
                        tState.status = 12;
                    else if (tState.status == 31)
                        tState.status = 21;

                    EnabledObjects(tState.status);

                }

            }
            else if (step == 1)
            {

                if (countTime >= 3)
                {
                    countTime = 0;
                    step = 2;

                    if (tState.status == 12)
                        tState.status = 41;
                    else if (tState.status == 21)
                        tState.status = 14;
                    EnabledObjects(tState.status);

                }

            }
            else if (step == 2)
            {

                if (countTime >= 7)
                {
                    countTime = 0;
                    step = 0;

                    if (tState.status == 14)
                        tState.status = 13;
                    else if (tState.status == 41)
                        tState.status = 31;

                    EnabledObjects(tState.status);
                }

            }


        }


        void EnabledObjects(int habilita)
        {

            tState.status_RY.SetActive(habilita == 12);
            tState.status_YR.SetActive(habilita == 21);
            tState.status_RG.SetActive(habilita == 13);
            tState.status_GR.SetActive(habilita == 31);
            tState.status_RR.SetActive(habilita == 11 || habilita == 14 || habilita == 41);


            tState.stop_RG.transform.localPosition = (habilita != 31) ? stop_RG_Default : stop_RG_Default + new Vector3(0, -2, 0);
            tState.stop_GR.transform.localPosition = (habilita != 13) ? stop_GR_Default : stop_GR_Default + new Vector3(0, -2, 0);

            /*
            tState.stop_RG.SetActive(habilita != 31);
            tState.stop_GR.SetActive(habilita != 13);
            */

        }



    }
}