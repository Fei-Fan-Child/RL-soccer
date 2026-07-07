using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

namespace LPCG
{
    public class LP_FCityGenerator : EditorWindow
    {

        private LP_CityGenerator cityGenerator;


        private bool rightHand = true;
        private bool heavyTraffic = false;


        public bool withSatteliteCity;
        public bool borderFlat;

        private bool withDowntownArea = true;
        private float downTownSize = 100;


        private LP_TrafficSystem trafficSystem;


        [MenuItem("Window/LowPoly City Generator")]
        static void Init()
        {

            LP_FCityGenerator window = (LP_FCityGenerator)EditorWindow.GetWindow(typeof(LP_FCityGenerator));

            window.Show();

        }



        public void LoadAssets(bool force = false)
        {

            string[] s;

            //BB - Street buildings in suburban areas (not in the corner)
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/BB", "*.prefab");
            if (force || cityGenerator.BB.Length != s.Length)
                cityGenerator.BB = LoadAssets_sub(s);

            //BC - Down Town Buildings(Not in the corner)
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/BC", "*.prefab");
            if (force || cityGenerator.BC.Length != s.Length)
                cityGenerator.BC = LoadAssets_sub(s);

            //BK - Buildings that occupy an entire block
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/BK", "*.prefab");
            if (force || cityGenerator.BK.Length != s.Length)
                cityGenerator.BK = LoadAssets_sub(s);

            //BR - Residential buildings in suburban areas (not in the corner)
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/BR", "*.prefab");
            if (force || cityGenerator.BR.Length != s.Length)
                cityGenerator.BR = LoadAssets_sub(s);

            //HS - Houses
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/HS", "*.prefab");
            if (force || cityGenerator.HS.Length != s.Length)
                cityGenerator.HS = LoadAssets_sub(s);

            //DC - Corner buildings that occupy both sides of the block
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/DC", "*.prefab");
            if (force || cityGenerator.DC.Length != s.Length)
                cityGenerator.DC = LoadAssets_sub(s);

            //EB - Corner buildings in suburban areas
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/EB", "*.prefab");
            if (force || cityGenerator.EB.Length != s.Length)
                cityGenerator.EB = LoadAssets_sub(s);

            //EC - Down Town Corner Buildings 
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/EC", "*.prefab");
            if (force || cityGenerator.EC.Length != s.Length)
                cityGenerator.EC = LoadAssets_sub(s);

            //MB - Buildings that occupy both sides of the block
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/MB", "*.prefab");
            if (force || cityGenerator.MB.Length != s.Length)
                cityGenerator.MB = LoadAssets_sub(s);

            //SB - Large buildings that occupy larger blocks
            s = System.IO.Directory.GetFiles("Assets/LowPoly City Generator/Buildings/Prefabs/SB", "*.prefab");
            if (force || cityGenerator.SB.Length != s.Length)
                cityGenerator.SB = LoadAssets_sub(s);


        }



        private GameObject[] LoadAssets_sub(string[] s)
        {

            int i = s.Length;
            GameObject[] g = new GameObject[i];

            for (int h = 0; h < i; h++)
                g[h] = AssetDatabase.LoadAssetAtPath(s[h], typeof(GameObject)) as GameObject;

            return g;

        }



        private void GenerateCity(int size)
        {
            
            LoadAssets();

            DestroyImmediate(GameObject.Find("CarContainer"));

            cityGenerator.GenerateCity(size, withSatteliteCity, borderFlat);

            /*
            if (trafficSystem)
            {
                InverseCarDirection((trafficLightHand == 1 && japanTrafficLight) ? 2 : trafficLightHand);
                trafficSystem.UpdateAllWayPoints();
            }
            */

            

        }


        void OnGUI()
        {



            GUILayout.Space(10);


            GUILayout.Label("LowPoly City Generator", EditorStyles.boldLabel);



            EditorGUILayout.BeginHorizontal();

            if (!cityGenerator)
                cityGenerator = AssetDatabase.LoadAssetAtPath("Assets/LowPoly City Generator/Generate.prefab", (typeof(LP_CityGenerator))) as LP_CityGenerator;

            //LoadAssets();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.BeginVertical("box");


            GUILayout.Space(5);
            GUILayout.Label(new GUIContent("Generate Streets", "Make City"));

            GUILayout.Space(5);


            GUILayout.BeginHorizontal("box");

            if (GUILayout.Button("Small"))
                GenerateCity(1);


            if (GUILayout.Button("Medium"))
                GenerateCity(2);

            if (GUILayout.Button("Large"))
                GenerateCity(3);

            if (GUILayout.Button("Very Large"))
                GenerateCity(4);


            GUILayout.Space(5);


            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            withSatteliteCity = GUILayout.Toggle(withSatteliteCity, "With Sattelite City", GUILayout.Width(240));

            if (!withSatteliteCity)
            {
                GUILayout.Space(10);
                borderFlat = GUILayout.Toggle(borderFlat, "Border Flat", GUILayout.Width(240));
            }

            GUILayout.Space(10);


            GUILayout.EndVertical();

            GUILayout.Space(10);



            GUILayout.BeginVertical("box");

            GUILayout.Space(5);

            GUILayout.Label(new GUIContent("Buildings", "Make or Clear Buildings"));

            GUILayout.Space(5);

            GUILayout.BeginHorizontal("box");


            GUILayout.Space(5);

            if (GUILayout.Button("Generate Buildings"))
            {

                LoadAssets(true);

                if (!GameObject.Find("Marcador")) return;

                cityGenerator.GenerateAllBuildings(withDowntownArea,  downTownSize);

            }


            if (GUILayout.Button("Clear Buildings"))
            {
                if (!GameObject.Find("Marcador")) return;
                cityGenerator.DestroyBuildings();
            }






            GUILayout.EndHorizontal();

            withDowntownArea = GUILayout.Toggle(withDowntownArea, "With Downtown Area?", GUILayout.Width(240));

            if (withDowntownArea)
            {
                GUILayout.Space(10);
                GUILayout.Label(new GUIContent("DownTown Size:", "DownTown Size"));
                downTownSize = EditorGUILayout.Slider(downTownSize, 50, 200);
                GUILayout.Space(10);
            }


            GUILayout.EndVertical();




            GUILayout.Space(10);



            GUILayout.BeginVertical("box");

            GUILayout.Space(5);

            GUILayout.Label(new GUIContent("Traffic System", "Make or Clear Traffic System"));

            GUILayout.Space(5);


            GUILayout.BeginHorizontal();


            GUILayout.Space(5);

            if (GUILayout.Button("Add Traffic System"))
            {
                AddVehicles(rightHand ? 0 : 1);
            }


            if (GUILayout.Button("Remove Traffic System"))
            {
                if(FindObjectOfType<LP_TrafficSystem>())
                    DestroyImmediate(FindObjectOfType<LP_TrafficSystem>().gameObject);

                if(GameObject.Find("CarContainer"))
                    DestroyImmediate(GameObject.Find("CarContainer"));
            }

            GUILayout.Space(5);




            GUILayout.EndHorizontal();


            GUILayout.BeginVertical();

            GUILayout.Space(5);

            /*
            if (!trafficSystem && FindObjectOfType<LP_TrafficSystem>())
                trafficSystem = FindObjectOfType<LP_TrafficSystem>();

            if (trafficSystem)
                heavyTraffic = trafficSystem.heavyTraffic;
            */

            bool rh = rightHand;
            bool ht = heavyTraffic;

            rightHand = GUILayout.Toggle(rightHand, "Right Hand", GUILayout.Width(240));

            GUILayout.Space(5);



            heavyTraffic = GUILayout.Toggle(heavyTraffic, "Heavy Traffic", GUILayout.Width(240));


            if (rh != rightHand || ht != heavyTraffic)
            {

                if (ht != heavyTraffic)
                    DefineHeavyTraffic(heavyTraffic);

                if (GameObject.Find("CarContainer"))
                    AddVehicles(rightHand ? 0 : 1);
                else
                {
                    if (rh != rightHand)
                        InverseCarDirection(rightHand ? 0 : 1);
                }
            }






            GUILayout.EndVertical();

            GUILayout.EndVertical();


            GUILayout.Space(10);


        }



        private void AddVehicles(int right_Hand = 1)
        {


            trafficSystem = FindObjectOfType<LP_TrafficSystem>();

            if (!trafficSystem)
            {
                Instantiate((GameObject)AssetDatabase.LoadAssetAtPath("Assets/LowPoly City Generator/Traffic System/Traffic System.prefab", (typeof(GameObject))));
                trafficSystem = FindObjectOfType<LP_TrafficSystem>();

            }

            if (!trafficSystem)
            {
                Debug.LogError("Add the Traffic System.prefab to Hierarchy");
                return;
            }
            else trafficSystem.name = "Traffic System";

            if (trafficSystem)
            {

                if(!trafficSystem.player)
                    trafficSystem.SetCameraPlayer();

                if(GameObject.Find("CarContainer"))
                    DestroyImmediate(GameObject.Find("CarContainer"));

                trafficSystem.LoadCars(right_Hand);
            }

        }


        private void InverseCarDirection(int trafficHand)
        {

            if (FindObjectOfType<LP_TrafficSystem>())
                trafficSystem = FindObjectOfType<LP_TrafficSystem>();

            if (!trafficSystem)
            {
                trafficSystem = AssetDatabase.LoadAssetAtPath("Assets/LowPoly City Generator/Traffic System/Traffic System.prefab", (typeof(LP_TrafficSystem))) as LP_TrafficSystem;
            }

            if (!trafficSystem)
            {
                Debug.LogError("Not Found System.prefab");
                return;
            }

            trafficSystem.DeffineDirection(trafficHand);

            if (GameObject.Find("CarContainer"))
                AddVehicles(trafficHand);

        }

        private void DefineHeavyTraffic(bool heaveTraffic)
        {

            if (FindObjectOfType<LP_TrafficSystem>())
                trafficSystem = FindObjectOfType<LP_TrafficSystem>();

            if (!trafficSystem)
            {
                trafficSystem = AssetDatabase.LoadAssetAtPath("Assets/LowPoly City Generator/Traffic System/Traffic System.prefab", (typeof(LP_TrafficSystem))) as LP_TrafficSystem;
            }

            if (!trafficSystem)
            {
                Debug.LogError("Not Found System.prefab");
                return;
            }

            trafficSystem.heavyTraffic = heaveTraffic;

        }



        private List<GameObject> newObjects = new List<GameObject>();




        private Component[] GetMeshFilters(GameObject objs)
        {
            List<Component> filters = new List<Component>();
            Component[] temp = null;

            temp = objs.GetComponentsInChildren(typeof(MeshFilter));
            for (int y = 0; y < temp.Length; y++)
                filters.Add(temp[y]);

            return filters.ToArray();

        }



        public static List<T> LoadAllPrefabsOfType<T>(string path) where T : MonoBehaviour
        {
            if (path != "")
            {
                if (path.EndsWith("/"))
                {
                    path = path.TrimEnd('/');
                }
            }

            DirectoryInfo dirInfo = new DirectoryInfo(path);
            FileInfo[] fileInf = dirInfo.GetFiles("*.prefab");

            //loop through directory loading the game object and checking if it has the component you want
            List<T> prefabComponents = new List<T>();
            foreach (FileInfo fileInfo in fileInf)
            {
                string fullPath = fileInfo.FullName.Replace(@"\", "/");
                string assetPath = "Assets" + fullPath.Replace(Application.dataPath, "");
                GameObject prefab = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;

                if (prefab != null)
                {
                    T hasT = prefab.GetComponent<T>();
                    if (hasT != null)
                    {
                        prefabComponents.Add(hasT);
                    }
                }
            }
            return prefabComponents;
        }

    }
}