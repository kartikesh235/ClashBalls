using Fusion;
using UnityEngine;

namespace Game.Managers
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        public float matchDuration = 90f;
        [SerializeField]private GameUIManager mUI;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
        }

        private void Start()
        {
            mUI.Setup(matchDuration);
        }

        public void UpdatePlayerStats(NetworkRunner runner,PlayerRef player, float health, float maxHealth, float stamina, float maxStamina, int score)
        {
            if (player == runner.LocalPlayer)
            {
                mUI.UpdateHealth(health, maxHealth);
                mUI.UpdateStamina(stamina, maxStamina);
                mUI.UpdateScore(score);
            }
        }
    }
}