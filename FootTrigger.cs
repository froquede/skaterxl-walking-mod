using Dreamteck.Splines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    class FootTrigger : MonoBehaviour
    {

        void OnTriggerEnter(Collider collider)
        {
            SplineComputer spline = collider.gameObject.GetComponent<SplineComputer>();
            if(spline == null) spline = collider.gameObject.GetComponentInParent<SplineComputer>();
            if(spline != null) Main.walking_go.doGrind(spline);
        }

        void OnTriggerStay(Collider collider)
        {
            if(collider.gameObject.name != "FakeSkater")
            {
                UnityModManager.Logger.Log(collider.gameObject.name);
                SplineComputer spline = collider.gameObject.GetComponent<SplineComputer>();
                if (spline == null) spline = collider.gameObject.GetComponentInParent<SplineComputer>();
                if (spline != null) Main.walking_go.doGrind(spline);
            }
        }
    }
}
