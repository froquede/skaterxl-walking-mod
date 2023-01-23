using GameManagement;
using ReplayEditor;
using System.Collections.Generic;
using UnityEngine;

namespace walking_mod
{
    class AudioSourceTimeTracker
    {
        public List<float> time = new List<float>();
        public List<bool> play_state = new List<bool>();

        public void pushState(float time, bool isPlaying)
        {
            this.time.Add(time);
            this.play_state.Add(isPlaying);
        }

        public void Shift()
        {
            this.time.RemoveAt(0);
            this.play_state.RemoveAt(0);
        }
    }

    class AudioSourceTracker : MonoBehaviour
    {
        public AudioSourceTimeTracker tracker;
        public float nextRecordTime;
        public float spf = 24f;
        public AudioSource audio_source;
        public int BufferFrameCount;

        public void Start()
        {
            tracker = new AudioSourceTimeTracker();
            audio_source = GetComponent<AudioSource>();
            BufferFrameCount = Mathf.RoundToInt(ReplaySettings.Instance.FPS * ReplaySettings.Instance.MaxRecordedTime);
            ResetAudio();
        }

        public void Update()
        {
            if (GameStateMachine.Instance.CurrentState.GetType() == typeof(ReplayState))
            {
                int index = getFrame();
                if (tracker.play_state[index] && !audio_source.isPlaying)
                {
                    audio_source.Play(0);
                    audio_source.pitch = ReplayEditorController.Instance.playbackController.TimeScale;
                }

                if (!tracker.play_state[index] && audio_source.isPlaying)
                {
                    audio_source.Stop();
                }
            }

            if (GameStateMachine.Instance.CurrentState.GetType() == typeof(PlayState))
            {
                tracker.pushState(PlayTime.time, audio_source.isPlaying);

                if (tracker.time.Count >= BufferFrameCount)
                {
                    tracker.Shift();
                }
            }
        }

        float last_time, last_anim_time;
        public void ResetAudio()
        {

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
