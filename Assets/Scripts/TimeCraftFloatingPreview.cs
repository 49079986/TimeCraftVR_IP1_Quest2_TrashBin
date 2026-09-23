using UnityEngine;

namespace TimeCraft.IP1
{
    public sealed class TimeCraftFloatingPreview : MonoBehaviour
    {
        private TextMesh display;
        private string clipName;
        private string letter;
        private float time;
        private MeshFilter meshFilter;
        private Mesh originalMesh;
        private Vector3 originalScale;
        private Rigidbody physicsBody;
        private static Mesh sphereMesh;

        public string DisplayName => clipName;
        public bool IsCrumpled { get; private set; }
        public bool IsRecycled { get; private set; }

        public void Initialize(TextMesh textDisplay, string sourceName, string sourceLetter)
        {
            display = textDisplay;
            clipName = sourceName;
            letter = sourceLetter;
            meshFilter = GetComponent<MeshFilter>();
            originalMesh = meshFilter != null ? meshFilter.sharedMesh : null;
            originalScale = transform.localScale;
            UpdateText();
        }

        private void Update()
        {
            time = (time + Time.deltaTime) % 5f;
            UpdateText();
        }

        private void UpdateText()
        {
            if (display == null)
            {
                return;
            }
            display.text = letter + "  PLAYING\n" + clipName + "\n00:" + Mathf.FloorToInt(time).ToString("00");
        }

        public bool Crumple()
        {
            if (IsRecycled)
            {
                return false;
            }
            if (!IsCrumpled)
            {
                IsCrumpled = true;
                if (meshFilter != null)
                {
                    meshFilter.sharedMesh = GetSphereMesh();
                }
                transform.localScale = Vector3.one * 0.42f;
                if (display != null)
                {
                    display.gameObject.SetActive(false);
                }
            }
            BeginHold();
            return true;
        }

        public void BeginHold()
        {
            if (!IsCrumpled)
            {
                return;
            }
            EnsurePhysicsBody();
            if (!physicsBody.isKinematic)
            {
                physicsBody.linearVelocity = Vector3.zero;
                physicsBody.angularVelocity = Vector3.zero;
            }
            physicsBody.useGravity = false;
            physicsBody.isKinematic = true;
        }

        public void HoldAt(Vector3 position, Quaternion rotation)
        {
            if (IsCrumpled && !IsRecycled)
            {
                transform.SetPositionAndRotation(position, rotation);
            }
        }

        public void Throw(Vector3 velocity)
        {
            if (!IsCrumpled || IsRecycled)
            {
                return;
            }
            EnsurePhysicsBody();
            physicsBody.isKinematic = false;
            physicsBody.useGravity = true;
            physicsBody.linearVelocity = Vector3.ClampMagnitude(velocity, 9f);
            physicsBody.angularVelocity = new Vector3(5f, 7f, 4f);
        }

        public void Recycle()
        {
            IsRecycled = true;
            gameObject.SetActive(false);
        }

        private void EnsurePhysicsBody()
        {
            if (physicsBody == null)
            {
                physicsBody = GetComponent<Rigidbody>();
                if (physicsBody == null)
                {
                    physicsBody = gameObject.AddComponent<Rigidbody>();
                }
                physicsBody.mass = 0.28f;
                physicsBody.linearDamping = 0.15f;
                physicsBody.angularDamping = 0.12f;
                physicsBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        private static Mesh GetSphereMesh()
        {
            if (sphereMesh != null)
            {
                return sphereMesh;
            }
            var temporary = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temporary);
            return sphereMesh;
        }
    }
}
