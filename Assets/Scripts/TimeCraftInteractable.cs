using UnityEngine;

namespace TimeCraft.IP1
{
    public enum InteractableKind
    {
        MediaClip,
        Effect
    }

    public sealed class TimeCraftInteractable : MonoBehaviour
    {
        public InteractableKind Kind;
        public string DisplayName;
        public string EffectName;
        public int SequenceIndex = -1;
        public float Speed = 1f;
        public bool IsTrimmed;
        public bool EffectApplied;
        public bool IsCrumpled { get; private set; }
        public bool IsRecycled { get; private set; }
        public bool IsPaperSheet { get; private set; }
        public Material NormalMaterial => normalMaterial;
        public Material SelectedMaterial => selectedMaterial;
        public Material AppliedMaterial => appliedMaterial;
        public Vector3 BaseScale => baseScale;

        [SerializeField] private Renderer cachedRenderer;
        [SerializeField] private Material normalMaterial;
        [SerializeField] private Material selectedMaterial;
        [SerializeField] private Material appliedMaterial;
        [SerializeField] private Vector3 baseScale;
        [SerializeField] private TextMesh label;
        [SerializeField] private GameObject[] paperContent;
        [SerializeField] private GameObject trimPieceLeft;
        [SerializeField] private GameObject trimPieceRight;
        private MeshFilter cachedMeshFilter;
        private Mesh originalMesh;
        private Rigidbody physicsBody;
        private static Mesh sphereMesh;

        private void Awake()
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            if (cachedMeshFilter == null)
            {
                cachedMeshFilter = GetComponent<MeshFilter>();
            }

            if (originalMesh == null && cachedMeshFilter != null)
            {
                originalMesh = cachedMeshFilter.sharedMesh;
            }
        }

        public void Initialize(
            InteractableKind kind,
            string displayName,
            Material normal,
            Material selected,
            Material applied,
            Vector3 scale,
            bool isPaperSheet = false,
            bool createDefaultLabel = true)
        {
            Kind = kind;
            DisplayName = displayName;
            EffectName = displayName;
            normalMaterial = normal;
            selectedMaterial = selected;
            appliedMaterial = applied;
            baseScale = scale;
            IsPaperSheet = isPaperSheet;

            cachedRenderer = GetComponent<Renderer>();
            cachedRenderer.sharedMaterial = normalMaterial;
            cachedMeshFilter = GetComponent<MeshFilter>();
            originalMesh = cachedMeshFilter != null ? cachedMeshFilter.sharedMesh : null;

            label = createDefaultLabel ? CreateLabel(displayName) : null;
            UpdateVisual(false);
        }

        public void SetPaperContent(GameObject[] content)
        {
            paperContent = content;
            SetPaperContentVisible(!IsCrumpled);
        }

        public void InheritEditableProperties(TimeCraftInteractable source)
        {
            Speed = source.Speed;
            EffectApplied = source.EffectApplied;
            EffectName = source.EffectName;
            IsTrimmed = false;
            UpdateVisual(false);
        }

        public void SetSelected(bool selected)
        {
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponent<Renderer>();
            }

            cachedRenderer.sharedMaterial = selected ? selectedMaterial : (EffectApplied ? appliedMaterial : normalMaterial);
            var pieceMaterial = selected ? selectedMaterial : (EffectApplied ? appliedMaterial : normalMaterial);
            if (trimPieceLeft != null)
            {
                trimPieceLeft.GetComponent<Renderer>().sharedMaterial = pieceMaterial;
            }
            if (trimPieceRight != null)
            {
                trimPieceRight.GetComponent<Renderer>().sharedMaterial = pieceMaterial;
            }
        }

        public void ToggleTrim()
        {
            if (Kind != InteractableKind.MediaClip)
            {
                return;
            }

            IsTrimmed = !IsTrimmed;
            UpdateTrimPieces();
            UpdateVisual(true);
        }

        public void ChangeSpeed(float delta)
        {
            if (Kind != InteractableKind.MediaClip)
            {
                return;
            }

            Speed = Mathf.Clamp(Speed + delta, 0.5f, 2f);
            UpdateVisual(true);
        }

        public void ApplyEffect(string effectName)
        {
            if (Kind != InteractableKind.MediaClip)
            {
                return;
            }

            EffectApplied = true;
            EffectName = effectName;
            UpdateVisual(true);
        }

        public void ResetState(Vector3 position, Quaternion rotation)
        {
            gameObject.SetActive(true);
            IsRecycled = false;
            IsCrumpled = false;
            if (cachedMeshFilter != null && originalMesh != null)
            {
                cachedMeshFilter.sharedMesh = originalMesh;
            }
            if (physicsBody != null)
            {
                if (!physicsBody.isKinematic)
                {
                    physicsBody.linearVelocity = Vector3.zero;
                    physicsBody.angularVelocity = Vector3.zero;
                }
                physicsBody.useGravity = false;
                physicsBody.isKinematic = true;
            }
            transform.SetPositionAndRotation(position, rotation);
            SequenceIndex = -1;
            Speed = 1f;
            IsTrimmed = false;
            EffectApplied = false;
            SetPaperContentVisible(true);
            UpdateTrimPieces();
            UpdateVisual(false);
        }

        public bool Crumple()
        {
            if (Kind != InteractableKind.MediaClip || IsRecycled)
            {
                return false;
            }

            if (!IsCrumpled)
            {
                IsCrumpled = true;
                if (cachedMeshFilter == null)
                {
                    cachedMeshFilter = GetComponent<MeshFilter>();
                }
                if (cachedMeshFilter != null)
                {
                    cachedMeshFilter.sharedMesh = GetSphereMesh();
                }
                transform.localScale = Vector3.one * 0.42f;
                SetPaperContentVisible(false);
                SetTrimPiecesVisible(false);
                if (cachedRenderer != null)
                {
                    cachedRenderer.enabled = true;
                }
                EnsurePhysicsBody();
                if (label != null)
                {
                    label.text = DisplayName + "\nCRUMPLED";
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
            if (!IsCrumpled || IsRecycled)
            {
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
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
            physicsBody.angularVelocity = new Vector3(4f, 7f, 3f);
        }

        public void Recycle()
        {
            IsRecycled = true;
            SetTrimPiecesVisible(false);
            if (physicsBody != null && !physicsBody.isKinematic)
            {
                physicsBody.linearVelocity = Vector3.zero;
                physicsBody.angularVelocity = Vector3.zero;
            }
            gameObject.SetActive(false);
        }

        private void UpdateVisual(bool selected)
        {
            if (IsCrumpled)
            {
                transform.localScale = Vector3.one * 0.42f;
                SetSelected(selected);
                if (label != null)
                {
                    label.text = DisplayName + "\nCRUMPLED";
                }
                return;
            }

            var scale = baseScale;
            if (!IsPaperSheet)
            {
                scale.x *= Mathf.Lerp(1.35f, 0.7f, Mathf.InverseLerp(0.5f, 2f, Speed));
            }

            transform.localScale = scale;
            SetSelected(selected);
            UpdateTrimPieces();

            if (label != null)
            {
                if (Kind == InteractableKind.MediaClip)
                {
                    label.text = DisplayName + "\n" + Speed.ToString("0.0") + "x" + (IsTrimmed ? "\ntrimmed" : "") + (EffectApplied ? "\n" + EffectName : "");
                }
                else
                {
                    label.text = EffectName;
                }
            }
        }

        private void UpdateTrimPieces()
        {
            if (IsPaperSheet || Kind != InteractableKind.MediaClip)
            {
                return;
            }

            if (IsTrimmed && trimPieceLeft == null)
            {
                trimPieceLeft = CreateTrimPiece("Trim Part A", -0.27f);
                trimPieceRight = CreateTrimPiece("Trim Part B", 0.27f);
            }

            var showPieces = IsTrimmed && !IsCrumpled;
            SetTrimPiecesVisible(showPieces);
            if (cachedRenderer != null)
            {
                cachedRenderer.enabled = !showPieces;
            }
            if (label != null)
            {
                label.gameObject.SetActive(!showPieces);
            }
        }

        private GameObject CreateTrimPiece(string pieceName, float localX)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = DisplayName + " " + pieceName;
            piece.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(localX, 0f, 0f)), transform.rotation);
            piece.transform.localScale = new Vector3(transform.lossyScale.x * 0.46f, transform.lossyScale.y, transform.lossyScale.z);
            piece.GetComponent<Renderer>().sharedMaterial = normalMaterial;
            piece.AddComponent<TimeCraftTrimPiece>().Initialize(this, pieceName);
            return piece;
        }

        private void SetTrimPiecesVisible(bool visible)
        {
            if (trimPieceLeft != null)
            {
                trimPieceLeft.SetActive(visible);
            }
            if (trimPieceRight != null)
            {
                trimPieceRight.SetActive(visible);
            }
        }

        private void SetPaperContentVisible(bool visible)
        {
            if (paperContent == null)
            {
                return;
            }

            foreach (var content in paperContent)
            {
                if (content != null)
                {
                    content.SetActive(visible);
                }
            }
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
                physicsBody.mass = 0.35f;
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
            if (Application.isPlaying)
            {
                Destroy(temporary);
            }
            else
            {
                DestroyImmediate(temporary);
            }
            return sphereMesh;
        }

        private TextMesh CreateLabel(string text)
        {
            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(transform);
            labelObject.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);

            var mesh = labelObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 42;
            mesh.characterSize = 0.045f;
            mesh.color = Color.white;
            return mesh;
        }
    }
}
