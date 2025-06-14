using System;
using UnityEngine;

namespace Game.GameUI
{
    public class Game3Dui : MonoBehaviour
    {
        //Look at camera 

        private void LateUpdate()
        {
            if (Camera.main != null)
            {
                LookAtCamera();
            }
        }

        public void LookAtCamera()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                transform.LookAt(camera.transform);
                transform.Rotate(0, 180, 0);
            }
        }
        
    }
}