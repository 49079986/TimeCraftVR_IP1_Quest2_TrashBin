using UnityEngine;

namespace TimeCraft.IP1
{
    public sealed class TimeCraftTrimPiece : MonoBehaviour
    {
        public TimeCraftInteractable Owner { get; private set; }

        public void Initialize(TimeCraftInteractable owner, string pieceName)
        {
            Owner = owner;
            var labelObject = new GameObject(pieceName + " Label");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            var text = labelObject.AddComponent<TextMesh>();
            text.text = pieceName;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 36;
            text.characterSize = 0.04f;
            text.color = Color.white;
        }
    }
}
