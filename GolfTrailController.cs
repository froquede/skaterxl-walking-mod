using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace walking_mod
{
    public class GolfTrailController : MonoBehaviour
    {
        public LineRenderer lineRenderer;
        public int positionsCount = 20; // Number of positions to store in the line renderer.

        private Vector3[] positions;

        void Start()
        {
            positions = new Vector3[positionsCount];
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("HDRP/Lit"));
            lineRenderer.material.color = new Color32(200, 200, 250, 120);
            lineRenderer.positionCount = positionsCount;
            lineRenderer.startWidth = .03f;
            lineRenderer.endWidth = 0f;
        }

        void Update()
        {
            // Shift the position array to the right and insert the new position at index 0.
            for (int i = positions.Length - 1; i > 0; i--)
            {
                positions[i] = positions[i - 1];
            }
            positions[0] = transform.position;

            // Update the line renderer positions.
            lineRenderer.SetPositions(positions);
        }
    }
}
