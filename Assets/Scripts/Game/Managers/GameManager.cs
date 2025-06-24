using Fusion;
using UnityEngine;

namespace Game.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        public float matchDuration = 90f;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

      
        
    }
}