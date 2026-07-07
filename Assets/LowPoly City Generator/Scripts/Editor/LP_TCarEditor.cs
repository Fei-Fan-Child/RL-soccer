using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LPCG

{

    [CustomEditor(typeof(LP_TrafficCar))]
    public class TCarEditor : Editor
    {

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            LP_TrafficCar TF = (LP_TrafficCar)target;

            if (GUILayout.Button("Generate WheelColliders"))
            {
                if (TF.gameObject.activeInHierarchy)
                    TF.Configure();
                else
                    Debug.LogWarning("Place the object in the hierarchy");
            }

        }


    }
}
