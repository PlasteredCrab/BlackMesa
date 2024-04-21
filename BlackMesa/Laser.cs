using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BlackMesa
{
    internal class Laser: MonoBehaviour 
    {
        private LineRenderer lr;
        void Start()
        {
            lr = GetComponent<LineRenderer>();
        }

        void LateUpdate()
        {
            lr.SetPosition(0, transform.position); 
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit))
            {
                if (hit.collider)
                {
                    lr.SetPosition(1, hit.point);
                }
            }
            else
            {
                lr.SetPosition(1, transform.position + (transform.forward * 5000));
            }
        }

    }
}
