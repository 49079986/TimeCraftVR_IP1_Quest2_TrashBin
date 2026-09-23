using UnityEngine;

namespace TimeCraft.IP1
{
    public sealed class TimeCraftPlaybackButton : MonoBehaviour
    {
        private TimeCraftPrototypeManager manager;
        private TextMesh label;

        public void Initialize(TimeCraftPrototypeManager owner, TextMesh buttonLabel)
        {
            manager = owner;
            label = buttonLabel;
            SetPlaying(false);
        }

        public void Activate()
        {
            if (manager != null)
            {
                manager.ToggleSequencePlayback();
            }
        }

        public void SetPlaying(bool playing)
        {
            if (label != null)
            {
                label.text = playing ? "STOP" : "PLAY";
                label.color = playing ? new Color(1f, 0.45f, 0.3f) : Color.white;
            }
        }
    }
}
