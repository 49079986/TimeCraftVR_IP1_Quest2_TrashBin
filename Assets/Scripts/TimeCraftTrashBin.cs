using UnityEngine;

namespace TimeCraft.IP1
{
    public sealed class TimeCraftTrashBin : MonoBehaviour
    {
        private TimeCraftPrototypeManager manager;

        public void Initialize(TimeCraftPrototypeManager owner)
        {
            manager = owner;
        }

        private void OnTriggerEnter(Collider other)
        {
            var floatingPreview = other.GetComponentInParent<TimeCraftFloatingPreview>();
            if (floatingPreview != null && floatingPreview.IsCrumpled)
            {
                floatingPreview.Recycle();
                if (manager != null)
                {
                    manager.NotifyFloatingPreviewRecycled(floatingPreview);
                }
                return;
            }

            var item = other.GetComponentInParent<TimeCraftInteractable>();
            if (item == null || item.Kind != InteractableKind.MediaClip || !item.IsCrumpled)
            {
                return;
            }

            item.Recycle();
            if (manager != null)
            {
                manager.NotifyRecycled(item);
            }
        }
    }
}
