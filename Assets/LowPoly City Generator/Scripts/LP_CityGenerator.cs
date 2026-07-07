using UnityEngine;
using System.Collections;
using System.Collections.Generic;
//using System;
using System.IO;
using System.Linq;

//using UnityEditor;
namespace LPCG
{


    public class LP_CityGenerator : MonoBehaviour
    {

        private int nB = 0;
        private Vector3 center;
        private int residential = 0;
        private bool _residential = false;

        GameObject cityMaker;

        private float distCenter = 300;
        private bool withDowntownArea = true;
        private float downTownSize = 100;

        [HideInInspector]
        public bool withSattelityCity;

        public bool borderFlat;

        [HideInInspector]
        public GameObject[] smallBorder;

        [HideInInspector]
        public GameObject[] largeBorder;

        [HideInInspector]
        public GameObject[] mediumBorder;

        [HideInInspector]
        public GameObject[] verylargeBorder;

        [HideInInspector]
        public GameObject[] smallBorderFlat;

        [HideInInspector]
        public GameObject[] largeBorderFlat;

        [HideInInspector]
        public GameObject[] mediumBorderFlat;

        [HideInInspector]
        public GameObject[] verylargeBorderFlat;


        [HideInInspector]
        public GameObject[] smallBorderWithExitOfCity;

        [HideInInspector]
        public GameObject[] largeBorderWithExitOfCity;

        [HideInInspector]
        public GameObject[] mediumBorderWithExitOfCity;

        [HideInInspector]
        public GameObject[] verylargeBorderWithExitOfCity;




        [HideInInspector]
        public GameObject[] largeBlocks;

        private bool[] _largeBlocks;

        [HideInInspector]
        public GameObject[] HighWay;


        [HideInInspector]
        public GameObject BorderSatelliteCity;

        [HideInInspector]
        public GameObject[] BB;  // Buildings in suburban areas (not in the corner)
        [HideInInspector]
        public GameObject[] BC;  // Down Town Buildings(Not in the corner)
        [HideInInspector]
        public GameObject[] BR;  // Residential buildings in suburban areas (not in the corner)
        [HideInInspector]
        public GameObject[] HS;  // Houses
        [HideInInspector]
        public GameObject[] DC;  // Corner buildings that occupy both sides of the block
        [HideInInspector]
        public GameObject[] EB;  // Corner buildings in suburban areas
        [HideInInspector]
        public GameObject[] EC;  // Down Town Corner Buildings 
        [HideInInspector]
        public GameObject[] MB;  //  Buildings that occupy both sides of the block 
        [HideInInspector]
        public GameObject[] BK;  //  Buildings that occupy an entire block
        [HideInInspector]
        public GameObject[] SB;  //  Large buildings that occupy larger blocks 

        private int[] _BB;
        private int[] _BC;
        private int[] _BR;

        private int[] _HS;

        //private int[] _DC;

        private int[] _EB;
        private int[] _EC;
        private int[] _MB;
        private int[] _BK;
        private int[] _SB;


        private GameObject[] tempArray;
        private int numB;


        

        public void GenerateCity(int size, bool withSatteliteCity = false, bool borderFlat = false)
        {
                        
            if (size == 1)
            {
                // Very Small City
                GenerateStreetsVerySmall(withSatteliteCity, borderFlat);
            }
            else if (size == 2)
            {
                // Small City
                GenerateStreetsSmall(withSatteliteCity, borderFlat);
            }
            else if (size == 3)
            {
                // Medium City
                GenerateStreets(withSatteliteCity, borderFlat);
            }
            else if (size == 4)
            {
                // Large City
                GenerateStreetsBig(withSatteliteCity, borderFlat);
            }


            Transform exitPositipon = GetPosition("ExitCity");

            if (exitPositipon != null)
            {

                GameObject block;

                block = (GameObject)Instantiate(HighWay[Random.Range(0, HighWay.Length)], exitPositipon.position, exitPositipon.rotation, cityMaker.transform);

                if (block)
                {
                    Transform nt = block.transform.Find("ExitBlock");
                    if (nt)
                    {

                            
                        block = (GameObject)Instantiate(BorderSatelliteCity, nt.position, nt.rotation, nt.transform);
                        block.transform.localPosition = new Vector3(-96, 0, -224);
                        block.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        block.transform.SetParent(cityMaker.transform);


                        Instantiate(largeBlocks[Random.Range(0, largeBlocks.Length)], block.transform.position, Quaternion.Euler(0, 0, 0), cityMaker.transform);

                    }
                }

            }


            DayNight dayNight = FindObjectOfType<DayNight>();
            if (dayNight)
                dayNight.ChangeMaterial();


        }

        private void GenerateStreetsVerySmall(bool withSatteliteCity = false, bool borderFlat = false)
        {

            if (!cityMaker)
                cityMaker = GameObject.Find("LP-City");

            if (cityMaker)
                DestroyImmediate(cityMaker);

            cityMaker = new GameObject("LP-City");

            GameObject block;

            distCenter = 60;
            int nb = 0;

            int le = largeBlocks.Length;
            nb = Random.Range(0, le);

            block = (GameObject)Instantiate(largeBlocks[nb], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);


            center = new Vector3(0, 0, 0);

            if(withSatteliteCity)
                block = (GameObject)Instantiate(smallBorderWithExitOfCity[Random.Range(0, smallBorderWithExitOfCity.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else if (borderFlat)
                block = (GameObject)Instantiate(smallBorderFlat[Random.Range(0, smallBorderFlat.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else
                block = (GameObject)Instantiate(smallBorder[Random.Range(0, smallBorder.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);

            block.transform.SetParent(cityMaker.transform);
        }

        private void GenerateStreetsSmall(bool withSatteliteCity = false, bool borderFlat = false)
        {

            if (!cityMaker)
                cityMaker = GameObject.Find("LP-City");

            if (cityMaker)
                DestroyImmediate(cityMaker);

            cityMaker = new GameObject("LP-City");



            distCenter = 100;
            int nb = 0;

            int le = largeBlocks.Length;
            _largeBlocks = new bool[largeBlocks.Length];

            //Position and Rotation
            Vector3[] ps = new Vector3[3];

            int[] rt = new int[3];

            float s = Random.Range(0, 6f);

            if (s < 3)
            {
                ps[1] = new Vector3(0, 0, 0); rt[1] = 0;
                ps[2] = new Vector3(0, 0, 192); rt[2] = 0;
            }
            else
            {
                ps[1] = new Vector3(-96, 0, 96); rt[1] = 90;
                ps[2] = new Vector3(96, 0, 96); rt[2] = 90;
            }


            for (int qt = 1; qt < 3; qt++)
            {

                for (int lp = 0; lp < 100; lp++)
                {
                    nb = Random.Range(0, le);
                    if (!_largeBlocks[nb]) break;
                }
                _largeBlocks[nb] = true;

                Instantiate(largeBlocks[nb], ps[qt], Quaternion.Euler(0, rt[qt], 0), cityMaker.transform);

            }


            center = ps[Random.Range(1, 2)];

            GameObject block;

            if (withSatteliteCity)
                block = (GameObject)Instantiate(mediumBorderWithExitOfCity [Random.Range(0, mediumBorderWithExitOfCity.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else if (borderFlat)
                block = (GameObject)Instantiate(mediumBorderFlat[Random.Range(0, mediumBorderFlat.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else
                block = (GameObject)Instantiate(mediumBorder[Random.Range(0, mediumBorder.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);



            block.transform.SetParent(cityMaker.transform);

        }


        private void GenerateStreets(bool withSatteliteCity = false, bool borderFlat = false)
        {

            if (!cityMaker)
                cityMaker = GameObject.Find("LP-City");

            if (cityMaker)
                DestroyImmediate(cityMaker);

            cityMaker = new GameObject("LP-City");

            distCenter = 150;

            int nb = 0;

            int le = largeBlocks.Length;
            _largeBlocks = new bool[largeBlocks.Length];

            //Position and Rotation
            Vector3[] ps = new Vector3[4];

            int[] rt = new int[4];

            float s = Random.Range(0, 4f);



            if (s < 2)
            {

                ps[1] = new Vector3(0, 0, 0); rt[1] = 0;
                ps[2] = new Vector3(0, 0, 192); rt[2] = 0;
                ps[3] = new Vector3(288, 0, 96); rt[3] = 90;
                

            }
            else if (s < 3)
            {

                ps[1] = new Vector3(288, 0, 96); rt[1] = 90;
                ps[2] = new Vector3(-96, 0, 96); rt[2] = 90;
                ps[3] = new Vector3(96, 0, 96); rt[3] = 90;
                

            }
            else //if (s < 4)
            {

                ps[1] = new Vector3(192, 0, 0); rt[1] = 0;
                ps[2] = new Vector3(192, 0, 192); rt[2] = 0;
                ps[3] = new Vector3(-96, 0, 96); rt[3] = 90;


            }
    


            for (int qt = 1; qt < 4; qt++)
            {

                for (int lp = 0; lp < 100; lp++)
                {
                    nb = Random.Range(0, le);
                    if (!_largeBlocks[nb]) break;
                }
                _largeBlocks[nb] = true;

                Instantiate(largeBlocks[nb], ps[qt], Quaternion.Euler(0, rt[qt], 0), cityMaker.transform);

            }

            center = ps[Random.Range(1, 3)];

            GameObject block; 

            if (withSatteliteCity)
                block = (GameObject)Instantiate(largeBorderWithExitOfCity[Random.Range(0, largeBorderWithExitOfCity.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else if (borderFlat)
                block = (GameObject)Instantiate(largeBorderFlat[Random.Range(0, largeBorderFlat.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else
                block = (GameObject)Instantiate(largeBorder[Random.Range(0, largeBorder.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);

            block.transform.SetParent(cityMaker.transform);

        }



        private void GenerateStreetsBig(bool withSatteliteCity = false, bool borderFlat = false)
        {

            if (!cityMaker)
                cityMaker = GameObject.Find("LP-City");

            if (cityMaker)
                DestroyImmediate(cityMaker);

            cityMaker = new GameObject("LP-City");

            distCenter = 200;
            int nb = 0;

            int le = largeBlocks.Length;
            _largeBlocks = new bool[largeBlocks.Length];

            //Position and Rotation
            Vector3[] ps = new Vector3[7];

            int[] rt = new int[7];

            float s = Random.Range(0, 6f);

            if (s < 3)
            {

                ps[1] = new Vector3(0, 0, 0); rt[1] = 0;
                ps[2] = new Vector3(0, 0, 192); rt[2] = 0;
                ps[3] = new Vector3(288, 0, 96); rt[3] = 90;
                ps[4] = new Vector3(-288, 0, 96); rt[4] = 90;
                ps[5] = new Vector3(-192, 0, 384); rt[5] = 0;
                ps[6] = new Vector3(192, 0, 384); rt[6] = 0;


            }
            else if (s < 3)
            {

                ps[1] = new Vector3(-288, 0, 96); rt[1] = 90;
                ps[2] = new Vector3(-96, 0, 96); rt[2] = 90;
                ps[3] = new Vector3(96, 0, 96); rt[3] = 90;
                ps[4] = new Vector3(288, 0, 96); rt[4] = 90;
                ps[5] = new Vector3(-192, 0, 384); rt[5] = 0;
                ps[6] = new Vector3(192, 0, 384); rt[6] = 0;

            }
            else if (s < 4)
            {

                ps[1] = new Vector3(-192, 0, 192); rt[1] = 0;
                ps[2] = new Vector3(-192, 0, 0); rt[2] = 0;
                ps[3] = new Vector3(96, 0, 96); rt[3] = 90;
                ps[4] = new Vector3(288, 0, 96); rt[4] = 90;
                ps[5] = new Vector3(-192, 0, 384); rt[5] = 0;
                ps[6] = new Vector3(192, 0, 384); rt[6] = 0;


            }
            else
            {

                ps[1] = new Vector3(-288, 0, 96); rt[1] = 90;
                ps[2] = new Vector3(192, 0, 0); rt[2] = 0;
                ps[3] = new Vector3(-96, 0, 96); rt[3] = 90;
                ps[4] = new Vector3(288, 0, 288); rt[4] = 90;
                ps[5] = new Vector3(-192, 0, 384); rt[5] = 0;
                ps[6] = new Vector3(96, 0, 288); rt[6] = 90;

            }


            for (int qt = 1; qt < 7; qt++)
            {

                for (int lp = 0; lp < 100; lp++)
                {
                    nb = Random.Range(0, le);
                    if (!_largeBlocks[nb]) break;
                }
                _largeBlocks[nb] = true;

                Instantiate(largeBlocks[nb], ps[qt], Quaternion.Euler(0, rt[qt], 0), cityMaker.transform);

            }

            center = ps[Random.Range(1, 6)];

            GameObject block; // = (GameObject)Instantiate(verylargeBorder[Random.Range(0, verylargeBorder.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);

            if (withSatteliteCity)
                block = (GameObject)Instantiate(verylargeBorderWithExitOfCity[Random.Range(0, verylargeBorderWithExitOfCity.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else if (borderFlat)
                block = (GameObject)Instantiate(verylargeBorderFlat[Random.Range(0, verylargeBorderFlat.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);
            else
                block = (GameObject)Instantiate(verylargeBorder[Random.Range(0, verylargeBorder.Length)], new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0), cityMaker.transform);


            block.transform.SetParent(cityMaker.transform);

        }


        private Transform GetPosition(string objName)
        {

            if (GameObject.Find(objName))
                return GameObject.Find("ExitCity").transform;
            else
                return null;

        }

        private Transform GetTransformPosition(Transform t, string objName)
        {
            return t.Find(objName);

        }

        private GameObject pB;

        public void GenerateAllBuildings(bool _withDowntownArea, float _downTownSize)
        {

            //distCenter = 160;


            withDowntownArea = _withDowntownArea;

            downTownSize = _downTownSize;

            GameObject[] tArray = SearchUtility.FindAllObjectsByName("Marcador"); 
            
         

            //Debug.Log("tArray.Length: " + tArray.Length);

            center = tArray[Random.Range(0, tArray.Length - 1)].transform.position;


            _BB = new int[BB.Length];
            _BC = new int[BC.Length];
            _BR = new int[BR.Length];
            
            _HS = new int[HS.Length];

            //_DC = new int[DC.Length];

            _EB = new int[EB.Length];
            _EC = new int[EC.Length];
            _MB = new int[MB.Length];
            _BK = new int[BK.Length];
            _SB = new int[SB.Length];

            residential = 0;

            DestroyBuildings();

            GameObject pB = new GameObject();

            nB = 0;

            CreateBuildingsInSuperBlocks();
            CreateBuildingsInBlocks();
            CreateBuildingsInLines();
            CreateBuildingsInDouble();

            Debug.ClearDeveloperConsole();
            Debug.Log(nB + " buildings were created");


            DestroyImmediate(pB);

            DayNight dayNight = FindObjectOfType<DayNight>();
            if (dayNight && dayNight.isNight)
            {
                dayNight.ChangeMaterial();
                dayNight.SetStreetLights(true);
            }

        }



        public void CreateBuildingsInLines()
        {

            bool forceResidential = false;

            tempArray = SearchUtility.FindAllObjectsByName("Marcador");

            foreach (GameObject lines in tempArray)
            {

                forceResidential = lines.transform.parent.name == "residential";

                if (forceResidential)
                    _residential = false;
                else
                    _residential = (residential < 15 && Vector3.Distance(center, lines.transform.position) > 400 && Random.Range(0, 100) < 30);

                foreach (Transform child in lines.transform)
                {

                    if (child.name == "E")
                        CreateBuildingsInCorners(child.gameObject);
                    else
                        CreateBuildingsInLine(child.gameObject, 90f, forceResidential);

                }

                _residential = false;


            }

        }

        public void CreateBuildingsInCorners(GameObject child)
        {

            GameObject pBuilding;

            pB = null;
            int numB = -1;
            int t = 0;
            float pWidth = 0;
            float wComprimento;

            float pScale;
            float remainingMeters;
            GameObject newMarcador;

            float distancia = Vector3.Distance(center, child.transform.position);

            int lp;
            lp = 0;

            float _distCenter = distCenter * (Mathf.Clamp(downTownSize, 50, 200) / 100);

            while (t < 30)
            {

                t++;

                if (distancia < _distCenter && withDowntownArea)
                {

                    do
                    {
                        lp++;
                        numB = Random.Range(0, EC.Length);
                        if (_EC[numB] == 0) break;
                        if (lp > 100 && _EC[numB] <= 1) break;
                        if (lp > 150 && _EC[numB] <= 2) break;
                        if (lp > 200 && _EC[numB] <= 3) break;
                        if (lp > 250) break;
                    } while (lp < 300);


                    pWidth = GetWith(EC[numB]);

                    if (pWidth <= 0) { Debug.LogWarning("Error: EC: " + numB); _EC[numB] += 1; }
                    else
                    if (pWidth <= 20.05f)
                    {
                        _EC[numB] += 1;
                        pB = EC[numB];
                        break;
                    }

                }
                else
                {



                    do
                    {
                        lp++;
                        numB = Random.Range(0, EB.Length);
                        if (_EB[numB] == 0) break;
                        if (lp > 100 && _EB[numB] <= 1) break;
                        if (lp > 150 && _EB[numB] <= 2) break;
                        if (lp > 200 && _EB[numB] <= 2) break;
                        if (lp > 250) break;
                    } while (lp < 300);


                    pWidth = GetWith(EB[numB]);


                    if (pWidth <= 0) { Debug.LogWarning("Error: EB: " + numB); _EB[numB] += 1; }
                    else
                    if (pWidth <= 20.05f)
                    {
                        _EB[numB] += 1;
                        pB = EB[numB];
                        break;
                    }


                }

            }


            pBuilding = (GameObject)Instantiate(pB, new Vector3(0, 0, 0), Quaternion.Euler(0, 0, 0));
            pBuilding.name = pBuilding.name;
            pBuilding.transform.SetParent(child.transform);
            pBuilding.transform.localPosition = new Vector3(-(pWidth * 0.5f), 0, 0);
            pBuilding.transform.localRotation = Quaternion.Euler(0, 0, 0);

            nB++;

            // Check space behind the corner building -------------------------------------------------------------------------------------------------------------------
            wComprimento = GetHeight(pB);
            if (wComprimento < 16)
            {

                newMarcador = new GameObject("Marcador");

                newMarcador.transform.SetParent(child.transform);
                newMarcador.transform.localPosition = new Vector3(0, 0, -20);
                newMarcador.transform.localRotation = Quaternion.Euler(0, 0, 0);
                newMarcador.name = (20 - wComprimento).ToString();
                CreateBuildingsInLine(newMarcador, 90);

            }
            else
            {
                remainingMeters = 20 - wComprimento;
                pScale = 1 + (remainingMeters / wComprimento);
                pBuilding.transform.localScale = new Vector3(1, 1, pScale);

            }


            // Check space on the corner building -------------------------------------------------------------------------------------------------------------------


            if (pWidth < 16f)
            {

                newMarcador = new GameObject("Marcador");


                newMarcador.transform.SetParent(child.transform);
                newMarcador.transform.localPosition = new Vector3(-pWidth, 0, 0);
                newMarcador.transform.localRotation = Quaternion.Euler(0, 270, 0);
                newMarcador.name = (20 - pWidth).ToString();
                CreateBuildingsInLine(newMarcador, 90);

            }
            else
            {

                remainingMeters = 20 - pWidth;
                pScale = 1 + (remainingMeters / pWidth);
                pBuilding.transform.localScale = new Vector3(pScale, 1, 1);

            }

        }

        int RandRotation()
        {
            int r = 0;
            int i = Random.Range(0, 4);
            if (i == 3) r = 180;
            else if (i == 2) r = 90;
            else if (i == 1) r = 270;
            else r = 0;

            return r;


        }

        public void CreateBuildingsInBlocks()
        {

            int numB = 0;

            tempArray = SearchUtility.FindAllObjectsByName("Blocks"); 
        

            foreach (GameObject bks in tempArray)
            {

                foreach (Transform bk in bks.transform)
                {

                    if (Random.Range(0, 20) > 5)
                    {

                        int lp = 0;
                        do
                        {
                            lp++;
                            numB = Random.Range(0, BK.Length);
                            if (_BK[numB] == 0) break;
                            if (lp > 125 && _BK[numB] <= 1) break;
                            if (lp > 150 && _BK[numB] <= 2) break;
                            if (lp > 200 && _BK[numB] <= 3) break;
                            if (lp > 250) break;
                        } while (lp < 300);

                        _BK[numB] += 1;

                        Instantiate(BK[numB], bk.position, bk.rotation, bk);
                        nB++;

                    }
                    else
                    {

                        for (int i = 1; i <= 4; i++)
                        {
                            GameObject nc = new GameObject("E");
                            nc.transform.SetParent(bk);
                            if (i == 1)
                            {
                                nc.transform.localPosition = new Vector3(-20, 0, -20);
                                nc.transform.localRotation = Quaternion.Euler(0, 180, 0);
                            }
                            if (i == 2)
                            {
                                nc.transform.localPosition = new Vector3(-20, 0, 20);
                                nc.transform.localRotation = Quaternion.Euler(0, 270, 0);
                            }
                            if (i == 3)
                            {
                                nc.transform.localPosition = new Vector3(20, 0, 20);
                                nc.transform.localRotation = Quaternion.Euler(0, 0, 0);
                            }
                            if (i == 4)
                            {
                                nc.transform.localPosition = new Vector3(20, 0, -20);
                                nc.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            }
                            CreateBuildingsInCorners(nc);

                        }
                    }


                }

            }

        }

        public void CreateBuildingsInSuperBlocks()
        {

            int numB = 0;

            tempArray = SearchUtility.FindAllObjectsByName("SuperBlocks"); 
            

            foreach (GameObject bks in tempArray)
            {

                foreach (Transform bk in bks.transform)
                {


                    int lp = 0;
                    do
                    {
                        lp++;
                        numB = Random.Range(0, SB.Length);
                        if (_SB[numB] == 0) break;
                        if (lp > 125 && _SB[numB] <= 1) break;
                        if (lp > 150 && _SB[numB] <= 2) break;
                        if (lp > 200 && _SB[numB] <= 3) break;
                        if (lp > 250) break;
                    } while (lp < 300);

                    _SB[numB] += 1;

                    Instantiate(SB[numB], bk.position, bk.rotation, bk);
                    nB++;



                }

            }

        }

        private void CreateBuildingsInLine(GameObject line, float angulo, bool forceResidential = false)
        {

            int index = -1;
            GameObject[] pBuilding;
            pBuilding = new GameObject[50];

            float limit;

            int dotIndex = line.name.IndexOf('.');

            if (dotIndex != -1)
                limit = float.Parse(line.name.Split('.')[0]) + float.Parse(line.name.Split('.')[1]) / float.Parse("1" + "0000000".Substring(0, line.name.Split('.')[1].Length));
            else
                limit = float.Parse(line.name);

            float init = 0;
            float pWidth = 0;


            int tt = 0;
            int t;

            int lp;


            float distancia = Vector3.Distance(center, line.transform.position);

            float _distCenter = distCenter * (Mathf.Clamp(downTownSize, 50, 200) / 100);


            while (tt < 100)
            {

                tt++;
                t = 0;


                lp = 0;
                while (t < 200 && init <= limit - 4)
                {

                    t++;

                    if (distancia < _distCenter && withDowntownArea && !forceResidential)
                    {

                        do
                        {
                            lp++;
                            numB = Random.Range(0, BC.Length);
                            if (_BC[numB] == 0) break;
                            if (lp > 125 && _BC[numB] <= 1) break;
                            if (lp > 150 && _BC[numB] <= 2) break;
                            if (lp > 200 && _BC[numB] <= 3) break;
                            if (lp > 250) break;
                        } while (lp < 300);

                        pWidth = GetWith(BC[numB]);

                        if (pWidth > 0)
                            if ((init + pWidth) <= (limit + 4))
                            {
                                pB = BC[numB];
                                _BC[numB] += 1;
                                break;
                            }

                    }
                    else if (forceResidential)
                    {

                        do
                        {
                            lp++;
                            numB = Random.Range(0, HS.Length);
                            if (_HS[numB] == 0) break;
                            if (lp > 100 && _HS[numB] <= 1) break;
                            if (lp > 150 && _HS[numB] <= 2) break;
                            if (lp > 200 && _HS[numB] <= 3) break;
                            if (lp > 250) break;
                        } while (lp < 300);

                        pWidth = GetWith(HS[numB]);

                        if (pWidth <= 0) { Debug.LogWarning("Error: BR: " + numB); _HS[numB] += 1; }
                        else
                        if ((init + pWidth) <= (limit + 4))
                        {
                            pB = HS[numB];
                            _HS[numB] += 1;
                            break;
                        }
                    }
                    else if (_residential)
                    {

                        do
                        {
                            lp++;
                            numB = Random.Range(0, BR.Length);
                            if (_BR[numB] == 0) break;
                            if (lp > 100 && _BR[numB] <= 1) break;
                            if (lp > 150 && _BR[numB] <= 2) break;
                            if (lp > 200 && _BR[numB] <= 3) break;
                            if (lp > 250) break;
                        } while (lp < 300);

                        pWidth = GetWith(BR[numB]);

                        if (pWidth <= 0) { Debug.LogWarning("Error: BR: " + numB); _BR[numB] += 1; }
                        else
                        if ((init + pWidth) <= (limit + 4))
                        {
                            pB = BR[numB];
                            _BR[numB] += 1;
                            residential += 1;
                            break;
                        }
                    }
                    else
                    {

                        do
                        {
                            lp++;
                            numB = Random.Range(0, BB.Length);
                            if (_BB[numB] == 0) break;
                            if (lp > 100 && _BB[numB] <= 1) break;
                            if (lp > 150 && _BB[numB] <= 2) break;
                            if (lp > 200 && _BB[numB] <= 3) break;
                            if (lp > 250) break;
                        } while (lp < 300);

                        pWidth = GetWith(BB[numB]);

                        if (pWidth <= 0) { Debug.LogWarning("Error: BB: " + numB); _BB[numB] += 1; }
                        if ((init + pWidth) <= (limit + 4))
                        {
                            pB = BB[numB];
                            _BB[numB] += 1;
                            break;
                        }

                    }


                }


                if (t >= 200 || init > limit - 4)
                {
                    // Did not find one that fits in the remaining space

                    AdjustsWidth(pBuilding, index + 1, limit - init, 0);
                    break;

                }
                else
                {
                    Debug.Log(pB.name);

                    index++;

                    pBuilding[index] = (GameObject)Instantiate(pB, new Vector3(0, 0, init + (pWidth * 0.5f)), Quaternion.Euler(0, angulo, 0));
                    nB++;

                    pBuilding[index].name = pBuilding[index].name;
                    pBuilding[index].transform.SetParent(line.transform);

                    pBuilding[index].transform.localPosition = new Vector3(0, 0, init + (pWidth * 0.5f));
                    pBuilding[index].transform.localRotation = Quaternion.Euler(0, angulo, 0);

                    init += pWidth;

                    if (init > limit - 6)
                    {

                        AdjustsWidth(pBuilding, index + 1, limit - init, 0);

                    }

                }


            }



        }


        private void CreateBuildingsInDoubleLine(GameObject line)
        {

            int index = -1;
            GameObject[] pBuilding;
            pBuilding = new GameObject[20];

            float limit;
            limit = float.Parse(line.name);

            float init = 0;
            float pWidth = 0;

            int tt = 0;
            int t;
            int lp;

            while (tt < 100)
            {

                tt++;
                t = 0;

                lp = 0;

                while (t < 200 && init <= limit - 4)
                {

                    t++;

                    do
                    {
                        lp++;
                        numB = Random.Range(0, MB.Length);
                        if (_MB[numB] == 0) break;
                        if (lp > 100 && _MB[numB] <= 1) break;
                        if (lp > 150 && _MB[numB] <= 2) break;
                        if (lp > 200) break;
                    } while (lp < 300);

                    pWidth = GetWith(MB[numB]);


                    if (pWidth <= 0) { Debug.LogWarning("Error: MB: " + numB); _MB[numB] += 1; }
                    else
                    if ((init + pWidth) <= (limit + 4))
                    {
                        _MB[numB] += 1;
                        break;
                    }

                }

                if (t >= 200 || init > limit - 4)
                {
                    AdjustsWidth(pBuilding, index + 1, (limit - init), 0);
                    break;

                }
                else
                {

                    index++;

                    pBuilding[index] = (GameObject)Instantiate(MB[numB], new Vector3(0, 0, 0), Quaternion.Euler(0, 90, 0), line.transform);
                    nB++;

                    pBuilding[index].name = "building";
                    pBuilding[index].transform.SetParent(line.transform);
                    pBuilding[index].transform.localPosition = new Vector3(0, 0, (init + (pWidth * 0.5f)));
                    pBuilding[index].transform.localRotation = Quaternion.Euler(0, 90, 0);

                    init += pWidth;

                    if (init > limit - 6)
                    {
                        AdjustsWidth(pBuilding, index + 1, (limit - init), 0);
                    }

                }


            }

        }

        private void CreateBuildingsInDouble()
        {
            float limit;

            tempArray = SearchUtility.FindAllObjectsByName("Double");

            GameObject DB;
            GameObject mc2;
            GameObject mc;


            foreach (GameObject dbCross in tempArray)
            {

                foreach (Transform line in dbCross.transform)
                {


                    limit = float.Parse(line.name);

                    if (Random.Range(0, 15) <= 10)
                    {
                        //Bloks

                        float wl;
                        float wl2;

                        do
                        {
                            numB = Random.Range(0, DC.Length);
                            wl = GetHeight(DC[numB]);
                        } while (wl > limit / 2);

                        GameObject e = (GameObject)Instantiate(DC[numB], line.transform.position, line.transform.rotation, line.transform);
                        nB++;

                        do
                        {
                            numB = Random.Range(0, DC.Length);
                            wl2 = GetHeight(DC[numB]);
                        } while (wl2 > limit - (wl + 16));

                        e = (GameObject)Instantiate(DC[numB], line.transform.position, line.rotation, line.transform);
                        e.transform.SetParent(line.transform);
                        e.transform.localPosition = new Vector3(0, 0, -limit);
                        e.transform.localRotation = Quaternion.Euler(0, 180, 0);

                        DB = new GameObject("" + ((limit - wl - wl2)));
                        DB.transform.SetParent(line.transform);
                        DB.transform.localPosition = new Vector3(0, 0, -(limit - wl2));
                        DB.transform.localRotation = Quaternion.Euler(0, 0, 0);

                        DB.name = "" + ((limit - wl - wl2));

                        CreateBuildingsInDoubleLine(DB);

                    }
                    else
                    {
                        //Lines and Corners

                        mc = new GameObject("Marcador");
                        mc.transform.SetParent(line);
                        mc.transform.localPosition = new Vector3(0, 0, 0);
                        mc.transform.localRotation = Quaternion.Euler(0, 0, 0);


                        for (int i = 1; i <= 4; i++)
                        {
                            mc2 = new GameObject("E");
                            mc2.transform.SetParent(mc.transform);

                            if (i == 1)
                            {
                                mc2.transform.localPosition = new Vector3(20, 0, -limit);
                                mc2.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            }
                            if (i == 2)
                            {
                                mc2.transform.localPosition = new Vector3(20, 0, 0);
                                mc2.transform.localRotation = Quaternion.Euler(0, 0, 0);
                            }
                            if (i == 3)
                            {
                                mc2.transform.localPosition = new Vector3(-20, 0, 0);
                                mc2.transform.localRotation = Quaternion.Euler(0, 270, 0);
                            }
                            if (i == 4)
                            {
                                mc2.transform.localPosition = new Vector3(-20, 0, -limit);
                                mc2.transform.localRotation = Quaternion.Euler(0, 180, 0);
                            }

                            CreateBuildingsInCorners(mc2);

                        }

                        mc2 = new GameObject("" + (limit - 40));
                        mc2.transform.SetParent(mc.transform);
                        mc2.transform.localPosition = new Vector3(-20, 0.001f, -20);
                        mc2.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        CreateBuildingsInLine(mc2, 90f);

                        mc2 = new GameObject("" + (limit - 40));
                        mc2.transform.SetParent(mc.transform);
                        mc2.transform.localPosition = new Vector3(20, 0.001f, -(limit - 20));
                        mc2.transform.localRotation = Quaternion.Euler(0, 0, 0);
                        CreateBuildingsInLine(mc2, 90f);

                    }




                }



            }


        }


        private void AdjustsWidth(GameObject[] tBuildings, int quantity, float remainingMeters, float init)
        {

            if (remainingMeters == 0)
                return;

            float ajuste = remainingMeters / quantity;

            float zInit = init;
            float pWidth;
            float pScale;
            float gw;


            for (int i = 0; i < quantity; i++)
            {

                gw = GetWith(tBuildings[i]);
                if (gw > 0)
                {
                    pScale = 1 + (ajuste / gw);
                    pWidth = gw + ajuste;

                    tBuildings[i].transform.localPosition = new Vector3(tBuildings[i].transform.localPosition.x, tBuildings[i].transform.localPosition.y, zInit + (pWidth * 0.5f));
                    tBuildings[i].transform.localScale = new Vector3(pScale, 1, 1);
                    zInit += pWidth;
                }

            }

        }


        private float GetWith(GameObject building)
        {

            if (!building)
                return 0;


            if (building.transform.GetComponent<MeshFilter>() != null)
            {

                if (building.transform.GetComponent<MeshFilter>().sharedMesh == null)
                {
                    Debug.LogError("Error:  " + building.name + " does not have a mesh renderer at the root. The prefab must be the floor/base mesh. I nside it you place the building. More info: https://youtu.be/kVrWir_WjNY");
                    //return 0;
                }


                return building.transform.GetComponent<MeshFilter>().sharedMesh.bounds.size.x;

            }
            else
            {
                Debug.LogError("Error:  " + building.name + " does not have a mesh renderer at the root. The prefab must be the floor/base mesh. I nside it you place the building. More info: https://youtu.be/kVrWir_WjNY");
                return 0;
            }
        }

        private float GetHeight(GameObject building)
        {

            if (building.GetComponent<MeshFilter>() != null)
                return building.GetComponent<MeshFilter>().sharedMesh.bounds.size.z;
            else
            {
                Debug.LogError("Error:  " + building.name + " does not have a mesh renderer at the root. The prefab must be the floor/base mesh. I nside it you place the building. More info: https://youtu.be/kVrWir_WjNY");
                return 0;
            }

        }





        public void DestroyBuildings()
        {

            DestryObjetcs("Marcador");
            DestryObjetcs("Blocks");
            DestryObjetcs("SuperBlocks");
            DestryObjetcs("Double");

        }


        private void DestryObjetcs(string tag)
        {
            tempArray = SearchUtility.FindAllObjectsByName(tag);

            foreach (GameObject objt in tempArray)
                foreach (Transform child in objt.transform)
                    for (int k = child.childCount - 1; k >= 0; k--)
                        DestroyImmediate(child.GetChild(k).gameObject);


        }







    }

}