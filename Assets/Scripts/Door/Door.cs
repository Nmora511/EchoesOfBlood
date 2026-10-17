using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerClass = Player.Player;

namespace Door
{
    public class Door : MonoBehaviour
    {
        public enum DoorTypeEnum {Entrance, Exit}
        public DoorTypeEnum DoorType;
        public bool IsOpen = false;
        
        private Collider _doorCollider;
        private readonly float _autoWalkDuration = 0.5f;

        private void Start()
        {
            _doorCollider = GetComponent<Collider>();
            if (IsOpen)
            {
                Open();
            }
            else
            {
                Close();
            }
            if (DoorType == DoorTypeEnum.Entrance && PlayerClass.PlayerInstance != null)
            {
                PlayerClass.PlayerInstance.ForceWalk(transform.right, _autoWalkDuration);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && DoorType == DoorTypeEnum.Exit && IsOpen)
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            }
        }

        public void Open()
        {
            IsOpen = true;
            _doorCollider.isTrigger = true;
        }

        public void Close()
        {
            IsOpen = false;
            _doorCollider.isTrigger = false;
        }
    }
}