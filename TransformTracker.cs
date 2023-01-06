using GameManagement;
using ReplayEditor;
using System.Collections.Generic;
using UnityEngine;
using UnityModManagerNet;

namespace walking_mod
{
    class TransformTimeTracker
    {
        public List<float> time = new List<float>();
        public List<Vector3> position = new List<Vector3>();
        public List<Quaternion> rotation = new List<Quaternion>();

        public void pushState(float time, Vector3 position, Quaternion rotation)
        {
            this.time.Add(time);
            this.position.Add(position);
            this.rotation.Add(rotation);
        }

        public void Shift()
        {
            this.time.RemoveAt(0);
            this.position.RemoveAt(0);
            this.rotation.RemoveAt(0);
        }
    }

    class TransformTracker : MonoBehaviour
    {
        public TransformTimeTracker tracker;
        public float nextRecordTime;
        public float spf = 24f;
        public int BufferFrameCount;

        public void Start()
        {
            tracker = new TransformTimeTracker();
            BufferFrameCount = Mathf.RoundToInt(ReplaySettings.Instance.FPS * ReplaySettings.Instance.MaxRecordedTime);
        }

        public void FixedUpdate()
        {
            if (GameStateMachine.Instance.CurrentState.GetType() == typeof(ReplayState))
            {
                int index = getFrame();
                if (index >= 0 && tracker.position[index] != null)
                {
                    UnityModManager.Logger.Log("Has frame");
                    transform.position = Vector3.Lerp(transform.position, tracker.position[index], ReplayEditorController.Instance.playbackController.TimeScale);
                    transform.rotation = Quaternion.Slerp(transform.rotation, tracker.rotation[index], ReplayEditorController.Instance.playbackController.TimeScale);
                }
            }

            if (GameStateMachine.Instance.CurrentState.GetType() == typeof(PlayState))
            {
                tracker.pushState(PlayTime.time, transform.position, transform.rotation);

                if (tracker.time.Count >= BufferFrameCount)
                {
                    tracker.Shift();
                }
            }
        }

        public int getFrame()
        {
            for (int i = tracker.time.Count - 1; i >= 0; i--)
            {
                if (tracker.time[i] <= ReplayEditorController.Instance.playbackController.CurrentTime) return i;
            }
            return -1;
        }
    }
}
