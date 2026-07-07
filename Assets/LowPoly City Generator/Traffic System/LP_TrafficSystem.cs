using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace LPCG
{

    public class LP_TrafficSystem : MonoBehaviour
    {

        [Space(10)]
        [Header("Player")]
        public Transform player = null;


        [Space(10)]
        [Header("Heavy Traffic")]
        public bool heavyTraffic = false;

        [Space(10)]

        [Header("Traffic Light:  0=Right  1=Left")]
        [Range(0, 1)]
        public int trafficLightHand = 0;

        [Space(10)]

        [Header("Config")]

        public int maxVehiclesWithPlayer = 50;

        [Range(100, 200)]
        public float around = 150;

        [Space(10)]

        [Header("Vehicles")]

        public GameObject[] IaCars;

        [HideInInspector]
        public int nVehicles = 0;

        private ArrayList spawnsPoints;

        bool firstTime = true;

        [System.Serializable]
        public class WpData
        {
            public bool[] tsActive;
            public Vector3[] tf01;
            public LP_WaypointsContainer[] tsParent;
            public bool[] tsOneway;
            public bool[] tsOnewayDoubleLine;
            public int[] tsSide;
        }

        //[HideInInspector]
        private WpData wpData = new WpData();

        [System.Serializable]
        public class WpDataSpawn
        {
            public Vector3 position;
            public Quaternion rotation;
            public float locateZ;
            public int side;
            public int node;
            public LP_WaypointsContainer wayScript;
        }

        private List<WpDataSpawn> wpDataSpawn;

        public void UpdateAllWayPoints()
        {

            LP_WaypointsContainer[] tArray = GameObject.FindObjectsOfType<LP_WaypointsContainer>();

            for (int f = 0; f < tArray.Length; f++)
            {
                tArray[f].ResetWay();
                tArray[f].GetWaypoints();
            }

            GetWpData();

            for (int f = 0; f < tArray.Length; f++)
                if (tArray[f].transform.childCount > 1)
                    tArray[f].wpData = wpData;


            for (int f = 0; f < tArray.Length; f++)
                if (tArray[f].transform.childCount > 1)
                    tArray[f].NextWaysCloseOnly();

            for (int f = 0; f < tArray.Length; f++)
                if (tArray[f].transform.childCount > 1)
                    tArray[f].NextWays();


        }



        public void GetWpData()
        {

            LP_WaypointsContainer[] ts = FindObjectsOfType<LP_WaypointsContainer>();

            wpData.tsActive = new bool[ts.Length * 2];
            wpData.tf01 = new Vector3[ts.Length * 2];
            wpData.tsParent = new LP_WaypointsContainer[ts.Length * 2];
            wpData.tsOneway = new bool[ts.Length * 2];
            wpData.tsOnewayDoubleLine = new bool[ts.Length * 2];
            wpData.tsSide = new int[ts.Length * 2];

            int t = -1;

            for (int i = 0; i < ts.Length; i++)
            {

                if (ts[i].waypoints.Count > 1)
                {
                    t++;

                    if (!ts[i].oneway || ts[i].doubleLine)
                    {
                        wpData.tsActive[t] = true;
                        wpData.tf01[t] = ts[i].Node(0, 0);
                        wpData.tsParent[t] = ts[i];
                        wpData.tsSide[t] = 0;
                        wpData.tsOneway[t] = ts[i].oneway;
                        wpData.tsOnewayDoubleLine[t] = ts[i].oneway && ts[i].doubleLine;
                    }
                    else
                        wpData.tsActive[t] = false;

                    t++;
                    wpData.tsActive[t] = true;
                    wpData.tf01[t] = ts[i].Node(1, 0);
                    wpData.tsParent[t] = ts[i];
                    wpData.tsSide[t] = 1;
                    wpData.tsOneway[t] = ts[i].oneway;
                    wpData.tsOnewayDoubleLine[t] = ts[i].oneway && ts[i].doubleLine;

                }
                else
                {
                    t++;
                    wpData.tsActive[t] = false;
                    t++;
                    wpData.tsActive[t] = false;
                }



            }

        }

        public void SetCameraPlayer()
        {

            Debug.LogWarning("Player was not defined in the Traffic System");

            // Tries to find the main camera
            Camera targetCamera = Camera.main;

            // If the main camera is not found, search for any other camera in the scene
            if (targetCamera == null)
            {
                Camera[] cameras = FindObjectsOfType<Camera>();
                if (cameras.Length > 0)
                {
                    player = cameras[0].transform;
                    Debug.Log("No MainCamera found. Assigning another scene camera as Player in the Traffic System.");
                }
                else
                    Debug.LogWarning("No camera found in the scene!");
            }
            else
            {
                player = targetCamera.transform;
                Debug.LogWarning("MainCamera was set as Player in the Traffic System");
            }

        }
        void Start()
        {

            if (!player)
            {
                SetCameraPlayer();

                if (player)
                {
                    GameObject carContainer = GameObject.Find("CarContainer");
                    if (carContainer)
                    {
                        nVehicles = carContainer.transform.childCount;

                        if (nVehicles > maxVehiclesWithPlayer)
                            DestroyImmediate(carContainer);

                    }
                }

            }

            LoadCars(trafficLightHand);

        }

        public void LoadCars(int right_Hand)
        {

            if (maxVehiclesWithPlayer == 0)
            {
                Debug.LogError("You need to set the maximum number of vehicles in the Traffic System");
                return;
            }

            LP_WaypointsContainer[] ts = FindObjectsOfType<LP_WaypointsContainer>();


            int n = ts.Length;
            
            for (int i = 0; i < n; i++)
                if (ts[i].transform.childCount == 0)
                    DestroyImmediate(ts[i].gameObject);  // Destroy Empty 
            

            UpdateAllWayPoints();

            /*
            if (!player)
                if(GameObject.FindGameObjectWithTag("MainCamera"))
                    player = GameObject.FindGameObjectWithTag("MainCamera").transform;
            */

            if (!player)
            {
                Debug.LogWarning("You have not set the player in the Traffic System on Inspector. This drastically decreases performance in big cities");
            }

            GameObject carContainer = GameObject.Find("CarContainer");

            if (carContainer)
            {
                nVehicles = carContainer.transform.childCount;

                if(nVehicles > maxVehiclesWithPlayer)
                    DestroyImmediate(carContainer);

            }

            if (!carContainer)
            {
                carContainer = new GameObject("CarContainer");
                nVehicles = 0;
            }
            else
            {
                nVehicles = carContainer.transform.childCount;
            }


            trafficLightHand = (right_Hand);

            DeffineDirection(right_Hand);

            wpDataSpawn = new List<WpDataSpawn>();

            ts = FindObjectsOfType<LP_WaypointsContainer>();
            n = ts.Length;

            for (int i = 0; i < n; i++)
            {

                if (!ts[i].bloked && ts[i].waypoints.Count > 1)
                {
                    for (int nSide = 0; nSide <= 1; nSide++)
                    {

                        if ((!ts[i].oneway || ts[i].doubleLine) || (nSide == 1 && trafficLightHand != 0) || (nSide == 0 && trafficLightHand == 0))
                        {
                            for (int node = 0; node < ts[i].waypoints.Count - 1; node++)
                            {

                                float dist = Vector3.Distance(ts[i].Node(nSide, node), ts[i].Node(nSide, node + 1));

                                if (dist > 60 && heavyTraffic)
                                {
                                    PlaceSpawnPoint(ts[i], nSide, node, dist / 1.3f);
                                    PlaceSpawnPoint(ts[i], nSide, node, dist / 2.8f);
                                }
                                else if (dist > 16)
                                    PlaceSpawnPoint(ts[i], nSide, node, dist / 2);


                            }

                        }

                    }

                }

            }



            if (!Application.isPlaying)
                firstTime = true;


            if (player && Application.isPlaying)
            {
                InvokeRepeating(nameof(LoadCars2), 0, 5);
            }
            else
                LoadCars2();

        }

        private void PlaceSpawnPoint(LP_WaypointsContainer f, int side, int node, float locate)
        {

            wpDataSpawn.Add(new WpDataSpawn { locateZ = locate, position = f.AvanceNode(side, node, locate), rotation = f.NodeRotation(side, node), side = side, node = node, wayScript = f });

        }


        public void LoadCars2()
        {


            if (!player && !firstTime)
                return;

            if (firstTime && player && nVehicles > 0)
            {

                LP_TrafficCar[] vcles = FindObjectsOfType<LP_TrafficCar>();

                    int nvcles = vcles.Length;

                    for (int i = 0; i < nvcles; i++)
                    {
                        vcles[i].GetComponent<LP_TrafficCar>().distanceToSelfDestroy = around;
                        vcles[i].GetComponent<LP_TrafficCar>().player = player;
                        vcles[i].GetComponent<LP_TrafficCar>().tSystem = this;

                    }

            }

            GameObject CarContainer = GameObject.Find("CarContainer");
            if (CarContainer)
                nVehicles = CarContainer.transform.childCount;
            else
                nVehicles = 0;

            
            if (firstTime && nVehicles > 0)
            {
                firstTime = false;
                return;
            }
            
            if (player && nVehicles >= maxVehiclesWithPlayer)
                return;

            GameObject vehicle;

            int n = wpDataSpawn.Count;

            int _nVehicles = nVehicles;

            bool invert = (Random.Range(1, 20) < 10);

            Transform test = new GameObject("verify").transform;

            for (int j = 0; j < n; j++)
            {

                int i = (invert) ? n - 1 - j : j;
                
                if (player && nVehicles >= maxVehiclesWithPlayer)
                {
                    break;
                }
                else
                {

                    if (player)
                    {
                        float dist = Vector3.Distance(wpDataSpawn[i].position, player.position);

                        if (player && (dist > around || (!firstTime && dist < 80)))
                            continue;

                        if (!firstTime && InTheFieldOfVision(player.position, wpDataSpawn[i].position))
                            continue;

                    }

                    bool go = false;
                    RaycastHit obsRay2;

                    // Check for a vehicle at the spawn point
                    if (firstTime)
                        go = true; //|| !player;
                    else
                        go = !Physics.Linecast(wpDataSpawn[i].wayScript.Node(wpDataSpawn[i].side, wpDataSpawn[i].node + 1) + Vector3.up * 1f,
                                               wpDataSpawn[i].wayScript.Node(wpDataSpawn[i].side, wpDataSpawn[i].node) + Vector3.up * 1f, out obsRay2);


                    if (go)
                    {

                        vehicle = (GameObject)Instantiate(IaCars[Mathf.Clamp(Random.Range(0, IaCars.Length), 0, IaCars.Length - 1)], wpDataSpawn[i].position + Vector3.up * 0.1f, wpDataSpawn[i].rotation); ;
                        vehicle.transform.SetParent(CarContainer.transform);
                        vehicle.GetComponent<LP_TrafficCar>().sideAtual = (wpDataSpawn[i].wayScript.oneway && wpDataSpawn[i].wayScript.doubleLine && wpDataSpawn[i].wayScript.rightHand == 1) ? ((wpDataSpawn[i].side == 1) ? 0 : 1) : wpDataSpawn[i].side;
                        vehicle.GetComponent<LP_TrafficCar>().atualWay = wpDataSpawn[i].wayScript.transform;
                        vehicle.GetComponent<LP_TrafficCar>().atualWayScript = wpDataSpawn[i].wayScript;
                        vehicle.GetComponent<LP_TrafficCar>().currentNode = wpDataSpawn[i].node + 1;

                        if (player)
                        {
                            vehicle.GetComponent<LP_TrafficCar>().distanceToSelfDestroy = around;
                            vehicle.GetComponent<LP_TrafficCar>().player = player;
                            vehicle.GetComponent<LP_TrafficCar>().tSystem = this;
                        }

                        nVehicles++;

                    }


                }




            }

            
            if (Application.isPlaying)
                Destroy(test.gameObject);
            else
                DestroyImmediate(test.gameObject);
            

            if (nVehicles > 0)
            {
                firstTime = false;
            }
            else
            {
                LP_WaypointsContainer[] _ts = FindObjectsOfType<LP_WaypointsContainer>();

                if (_ts.Length == 0)
                {
                    Debug.Log("Need to generate the city again to use the updated traffic system");
                }

            }


        }


        bool InTheFieldOfVision(Vector3 source, Vector3 target)
        {
            RaycastHit obsRay2;

            return (!(Physics.Linecast(source + Vector3.up * 4f, target + Vector3.up * 4f, out obsRay2)) || !(Physics.Linecast(source + Vector3.up * 1f, target + Vector3.up * 1f, out obsRay2)));

        }

        public void DeffineDirection(int hand_Right)
        {

            trafficLightHand = hand_Right;

            //Inverse Traffic-Lights 
            LP_TFShiftHand[] TLs = FindObjectsOfType<LP_TFShiftHand>();
            
            /*
            if (TLs.Length == 0 && GameObject.Find("Traffic-Light-T"))
            {
                Debug.LogError("It is not compatible with the previous traffic system.\nTo use the new system you need to generate the city again");
                UpdateAllWayPoints();
                return;
            }
            */

            for (int i = 0; i < TLs.Length; i++)
                TLs[i].RightHand(trafficLightHand);

            //Inverse Nodes
            LP_WaypointsContainer[] ts = FindObjectsOfType<LP_WaypointsContainer>();
            for (int i = 0; i < ts.Length; i++)
                ts[i].InvertNodesDirection(trafficLightHand);

            GameObject[] roadMark = GameObject.FindObjectsOfType(typeof(GameObject)).Select(g => g as GameObject).Where(g => g.name.Equals("Road-Mark")).ToArray();
            for (int i = 0; i < roadMark.Length; i++)
                if (roadMark[i].transform.Find("RoadMark"))
                    roadMark[i].transform.Find("RoadMark").gameObject.SetActive(trafficLightHand == 0);

            roadMark = GameObject.FindObjectsOfType(typeof(GameObject)).Select(g => g as GameObject).Where(g => g.name.Equals("Road-Mark-Rev")).ToArray();
            for (int i = 0; i < roadMark.Length; i++)
                if (roadMark[i].transform.Find("RoadMarkRev"))
                    roadMark[i].transform.Find("RoadMarkRev").gameObject.SetActive(trafficLightHand != 0);

            UpdateAllWayPoints();

        }


    }

}