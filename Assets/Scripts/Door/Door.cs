using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using PlayerClass = Player.Player;

namespace Door
{
    public class Door : MonoBehaviour
    {
        private static readonly int HasOpened = Animator.StringToHash("hasOpened");
        private static readonly int HasClosed = Animator.StringToHash("hasClosed");
        private static readonly int StartedOpen = Animator.StringToHash("startedOpen");

        public enum DoorTypeEnum {Entrance, Exit}
        public DoorTypeEnum DoorType;
        public bool IsOpen = false;
        
        private Collider _doorCollider;
        private Animator _doorAnimator;
        private readonly float _autoWalkDuration = 0.5f;
        private bool _hasExited = false;
        
        private void Start()
        {
            _doorAnimator = GetComponentInChildren<Animator>();
            _doorCollider = GetComponent<Collider>();
            if (IsOpen)
            {
                _doorAnimator.SetTrigger(StartedOpen);
                StartCoroutine(WaitToCloseDoor());
            }

            Close();
            if (DoorType == DoorTypeEnum.Entrance && PlayerClass.PlayerInstance != null)
            {
                PlayerClass.PlayerInstance.ForceWalk(transform.right, _autoWalkDuration);
            }
        }

        private IEnumerator WaitToCloseDoor()
        {
            yield return new WaitForSeconds(_autoWalkDuration * 40);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player") || DoorType != DoorTypeEnum.Exit || !IsOpen || _hasExited) return;
            
            // Prevents calling LoadNext twice if more than one Player collider enters the trigger.
            _hasExited = true;
            RunManager.Instance.LoadNext();
        }

        public void Open()
        {
            IsOpen = true;
            _doorCollider.isTrigger = true;
            _doorAnimator.SetTrigger(HasOpened);
        }

        public void Close()
        {
            IsOpen = false;
            _doorCollider.isTrigger = false;
            _doorAnimator.SetTrigger(HasClosed);
        }
    }
}