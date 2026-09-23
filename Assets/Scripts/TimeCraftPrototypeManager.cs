using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace TimeCraft.IP1
{
    public sealed class TimeCraftPrototypeManager : MonoBehaviour
    {
        private const float DragPlaneY = 0.7f;

        [SerializeField] private List<TimeCraftInteractable> mediaClips = new List<TimeCraftInteractable>();
        [SerializeField] private List<TimeCraftInteractable> effects = new List<TimeCraftInteractable>();
        [SerializeField] private List<Vector3> clipStartPositions = new List<Vector3>();
        [SerializeField] private List<Vector3> effectStartPositions = new List<Vector3>();
        private readonly Vector3[] snapPoints =
        {
            new Vector3(-1.19f, 0.48f, 0.45f),
            new Vector3(0f, 0.48f, 0.45f),
            new Vector3(1.19f, 0.48f, 0.45f)
        };

        [SerializeField] private bool scenePrebuilt;
        [SerializeField] private Camera mainCamera;
        private Font uiFont;
        private TimeCraftInteractable selected;
        private TimeCraftInteractable dragged;
        private TimeCraftTrimPiece draggedTrimPiece;
        private TimeCraftTrimPiece xrDraggedTrimPiece;
        private TimeCraftFloatingPreview selectedFloatingPreview;
        private TimeCraftFloatingPreview draggedFloatingPreview;
        private TimeCraftFloatingPreview xrDraggedFloatingPreview;
        private Vector3 dragOffset;
        private Vector3 dragOriginPosition;
        private Quaternion dragOriginRotation;
        private int dragOriginSequenceIndex = -1;
        private bool suppressSnapUntilClipLeavesTrack;
        private int floatingPreviewCount;
        [SerializeField] private Text statusText;
        [SerializeField] private Text previewText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text metricsText;
        [System.NonSerialized] private bool buildingEditableScene;
        private Vector3 cameraPivot = new Vector3(0f, 0.75f, 0.65f);
        private float cameraYaw;
        private float cameraPitch = 58f;
        private float cameraDistance = 7.2f;
        private const float MinCameraPitch = -85f;
        private const float MaxCameraPitch = 88f;
        private const float SimulatedEyeHeight = 1.65f;
        private bool headsetSimulatorMode;
        private Vector3 simulatorPosition = new Vector3(0f, SimulatedEyeHeight, -2.35f);
        private float sessionTimer;
        private int promptCount;
        private int trimCount;
        private int speedChanges;
        private int effectsApplied;
        private int successfulSelections;
        private int missedSelections;
        private int recycledCount;
        private bool timerRunning = true;
        private bool xrMode;
        private Vector3 desktopThrowVelocity;
        private Vector3 previousDragPosition;
        private float previousDragTime;
        private TimeCraftInteractable instructionPaper;
        private Vector3 instructionPaperStartPosition;
        private Quaternion instructionPaperStartRotation;
        private static readonly Vector3 TrashBinPosition = new Vector3(-3.05f, 0f, -2.15f);
        private TimeCraftPlaybackButton playbackButton;
        private bool sequencePlaying;
        private float sequencePlaybackTime;
        private int playbackClipIndex = -1;
        private TimeCraftTimelineScrubber timelineScrubber;
        private bool draggingScrubber;
        private bool xrScrubbing;
        private TimeCraftInteractable previewedClip;
        private TextMesh worldPreviewText;
        private Collider editingZoneCollider;
        private Material clipSeparatorMaterial;
        private readonly List<GameObject> dynamicClipSeparators = new List<GameObject>();
        private readonly List<TimeCraftInteractable> originalMediaClips = new List<TimeCraftInteractable>();
        private readonly List<Vector3> originalClipPositions = new List<Vector3>();
        private readonly List<TimeCraftInteractable> generatedSplitClips = new List<TimeCraftInteractable>();
        private readonly List<GameObject> floatingPreviewScreens = new List<GameObject>();

        public Camera MainCamera => mainCamera != null ? mainCamera : Camera.main;
        public TimeCraftInteractable SelectedInteractable => selected;
        public bool IsXRMode => xrMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<TimeCraftPrototypeManager>() != null)
            {
                return;
            }

            var manager = new GameObject("TimeCraft IP1 Prototype Manager");
            manager.AddComponent<TimeCraftPrototypeManager>();
        }

        private void Start()
        {
            if (!scenePrebuilt)
            {
                BuildScene();
            }

            EnsureTrashBin();
            EnsureInstructionPaperInteractable();
            EnsureEditingWorkspaceAndPlayback();
            EnsureTimelineScrubber();
            EnsureWorldPreviewDisplay();
            CaptureOriginalMediaClips();

            ResetPrototype();
            SetStatus("Ready. Click the Controls paper, press G to crumple it, then throw it into the red bin under MEDIA.");
        }

#if UNITY_EDITOR
        public void BuildEditableScene()
        {
            buildingEditableScene = true;
            scenePrebuilt = false;
            mediaClips.Clear();
            effects.Clear();
            clipStartPositions.Clear();
            effectStartPositions.Clear();

            BuildScene();

            scenePrebuilt = true;
            buildingEditableScene = false;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
        }
#endif

        private void Update()
        {
            if (timerRunning && timerText != null)
            {
                sessionTimer += Time.deltaTime;
                timerText.text = "Session " + FormatTime(sessionTimer) + " / 05:00";
            }

            if (!xrMode)
            {
                HandleSelectionAndDrag();
                HandleKeyboardShortcuts();
            }
            UpdateSequencePlayback();
            UpdatePreview();
        }

        private void OnGUI()
        {
            if (xrMode)
            {
                return;
            }

            GUI.depth = -100;
            var previousColor = GUI.color;
            var previousBackgroundColor = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.88f);

            GUILayout.BeginArea(new Rect(16f, 16f, 420f, 286f), GUI.skin.box);
            GUILayout.Label("TimeCraft VR - " + (headsetSimulatorMode ? "SIMULATED HEADSET" : "DESKTOP OVERVIEW"));
            GUILayout.Label("F1: switch view mode    RMB drag: look around");
            GUILayout.Label("WASD / arrows: move    Wheel: zoom (overview)");
            GUILayout.Label(headsetSimulatorMode
                ? "Aim with centre reticle; left-drag to grab and move"
                : "Left click / drag clips and effects");
            GUILayout.Label("G: crumple clip    T: trim    Q / E: speed - / +");
            GUILayout.Label("Drag and flick a crumpled clip into the RED bin");
            GUILayout.Label("P: log prompt    R: reset    Space: play / stop sequence");
            if (GUILayout.Button(sequencePlaying ? "STOP SEQUENCE" : "PLAY SEQUENCE", GUILayout.Height(30f)))
            {
                ToggleSequencePlayback();
            }
            GUILayout.Space(6f);
            GUILayout.Label(statusText == null ? "Ready." : statusText.text);
            GUILayout.EndArea();

            if (headsetSimulatorMode)
            {
                var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                GUI.color = dragged == null ? new Color(0.25f, 0.95f, 1f) : new Color(1f, 0.75f, 0.2f);
                GUI.DrawTexture(new Rect(centre.x - 1f, centre.y - 10f, 2f, 20f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(centre.x - 10f, centre.y - 1f, 20f, 2f), Texture2D.whiteTexture);
            }

            GUI.color = previousColor;
            GUI.backgroundColor = previousBackgroundColor;
        }

        private void BuildScene()
        {
            CreateMaterials();
            CreateCameraAndLighting();
            CreateEnvironment();
            CreateMediaClips();
            CreateEffects();
            EnsureTrashBin();
            CreateScreenUi();
        }

        [SerializeField] private Material clipMat;
        [SerializeField] private Material selectedMat;
        [SerializeField] private Material appliedMat;
        [SerializeField] private Material effectMat;
        [SerializeField] private Material shelfMat;
        [SerializeField] private Material snapMat;
        [SerializeField] private Material floorMat;
        [SerializeField] private Material screenMat;
        [SerializeField] private Material trackBedMat;
        [SerializeField] private Material trackAxisMat;

        private void CreateMaterials()
        {
            clipMat = MakeMaterial("Clip Blue", new Color(0.13f, 0.48f, 0.8f));
            selectedMat = MakeMaterial("Selected Cyan", new Color(0.05f, 0.9f, 1f));
            appliedMat = MakeMaterial("Applied Purple", new Color(0.55f, 0.28f, 0.9f));
            effectMat = MakeMaterial("Effect Gold", new Color(0.95f, 0.62f, 0.18f));
            shelfMat = MakeMaterial("Shelf Charcoal", new Color(0.16f, 0.18f, 0.22f));
            snapMat = MakeMaterial("Snap Green", new Color(0.1f, 0.65f, 0.35f, 0.45f));
            floorMat = MakeMaterial("Floor Slate", new Color(0.08f, 0.09f, 0.11f));
            screenMat = MakeMaterial("Preview Black", new Color(0.02f, 0.025f, 0.035f));
            trackBedMat = MakeMaterial("Timeline Track Bed", new Color(0.07f, 0.16f, 0.2f));
            trackAxisMat = MakeMaterial("Timeline Axis Cyan", new Color(0.1f, 0.78f, 0.88f));
        }

        private Material MakeMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            var material = new Material(shader);
            material.name = name;
            material.color = color;
            if (color.a < 1f)
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                if (material.HasProperty("_Mode"))
                {
                    material.SetFloat("_Mode", 3f);
                }
                if (material.HasProperty("_SrcBlend"))
                {
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                }
                if (material.HasProperty("_DstBlend"))
                {
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                }
                if (material.HasProperty("_ZWrite"))
                {
                    material.SetInt("_ZWrite", 0);
                }
                material.renderQueue = 3000;
            }
            else if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
#if UNITY_EDITOR
            if (buildingEditableScene)
            {
                const string generatedFolder = "Assets/Generated";
                const string materialFolder = "Assets/Generated/Materials";
                if (!UnityEditor.AssetDatabase.IsValidFolder(generatedFolder))
                {
                    UnityEditor.AssetDatabase.CreateFolder("Assets", "Generated");
                }
                if (!UnityEditor.AssetDatabase.IsValidFolder(materialFolder))
                {
                    UnityEditor.AssetDatabase.CreateFolder(generatedFolder, "Materials");
                }

                var assetName = name.Replace(" ", "_").Replace("/", "_");
                var assetPath = materialFolder + "/" + assetName + ".mat";
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (existing != null)
                {
                    UnityEditor.EditorUtility.CopySerialized(material, existing);
                    DestroyImmediate(material);
                    UnityEditor.EditorUtility.SetDirty(existing);
                    return existing;
                }

                UnityEditor.AssetDatabase.CreateAsset(material, assetPath);
            }
#endif
            return material;
        }

        private void CreateCameraAndLighting()
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = new GameObject("Main Camera").AddComponent<Camera>();
                mainCamera.tag = "MainCamera";
            }

            cameraYaw = 0f;
            cameraPitch = 58f;
            cameraDistance = 7.2f;
            ApplyCameraPose();
            mainCamera.fieldOfView = 48f;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.04f, 0.045f, 0.055f);

            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var fillObject = new GameObject("Fill Light");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.intensity = 1.6f;
            fill.range = 8f;
            fillObject.transform.position = new Vector3(0f, 3.8f, -2f);
        }

        private void CreateEnvironment()
        {
            CreateCube("Floor", new Vector3(0f, -0.05f, 0.5f), new Vector3(8.5f, 0.1f, 6.3f), floorMat);
            CreateCube("Left Media Shelf", new Vector3(-3.55f, 0.42f, 0.55f), new Vector3(0.22f, 0.84f, 4.2f), shelfMat);
            CreateCube("Right Effects Shelf", new Vector3(3.55f, 0.42f, 0.55f), new Vector3(0.22f, 0.84f, 4.2f), shelfMat);
            CreateCube("Video Layer", new Vector3(0f, 0.08f, 1.63f), new Vector3(4.85f, 0.08f, 0.16f), MakeMaterial("Video Layer", new Color(0.15f, 0.35f, 0.55f)));
            CreateCube("Text Layer", new Vector3(0f, 0.08f, 1.9f), new Vector3(4.85f, 0.08f, 0.16f), MakeMaterial("Text Layer", new Color(0.36f, 0.26f, 0.56f)));
            CreateCube("Audio Layer", new Vector3(0f, 0.08f, 2.17f), new Vector3(4.85f, 0.08f, 0.16f), MakeMaterial("Audio Layer", new Color(0.15f, 0.45f, 0.3f)));

            CreateCube("Timeline Track Bed", new Vector3(0f, 0.215f, 0.45f), new Vector3(5.35f, 0.08f, 0.92f), trackBedMat);
            CreateCube("Timeline Axis", new Vector3(0f, 0.265f, 0.45f), new Vector3(5.55f, 0.035f, 0.11f), trackAxisMat);

            for (var i = 0; i < snapPoints.Length; i++)
            {
                CreateCube(
                    "Timeline Node " + (i + 1),
                    new Vector3(snapPoints[i].x, 0.275f, snapPoints[i].z),
                    new Vector3(0.06f, 0.04f, 1.02f),
                    trackAxisMat);
                CreateCube("Snap Target " + (i + 1), snapPoints[i] + new Vector3(0f, -0.43f, 0f), new Vector3(1.18f, 0.04f, 0.72f), snapMat);
            }

            CreateText3D("MEDIA", new Vector3(-3.55f, 1.25f, -1.85f), 0.06f, TextAnchor.MiddleCenter);
            CreateText3D("EFFECTS", new Vector3(3.55f, 1.25f, -1.85f), 0.06f, TextAnchor.MiddleCenter);
            CreateText3D("Spatial editing workspace", new Vector3(0f, 0.18f, -0.25f), 0.055f, TextAnchor.MiddleCenter);
            CreateText3D("Video / Text / Audio layers", new Vector3(0f, 0.25f, 2.48f), 0.05f, TextAnchor.MiddleCenter);

            var screen = CreateCube("Floating Preview Screen", new Vector3(0f, 2.15f, 2.8f), new Vector3(4.7f, 2.05f, 0.1f), screenMat);
            screen.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);
            CreateText3D("PREVIEW SCREEN", new Vector3(0f, 3.35f, 2.65f), 0.07f, TextAnchor.MiddleCenter);
            CreateInstructionBoard();
        }

        private void CreateInstructionBoard()
        {
            var board = CreateCube(
                "Instruction Board",
                new Vector3(0f, 2.15f, -2.05f),
                new Vector3(4.55f, 1.8f, 0.08f),
                MakeMaterial("Instruction Board Dark", new Color(0.01f, 0.012f, 0.018f)));
            board.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);

            var title = CreateText3D("Controls", new Vector3(0f, 2.85f, -2.12f), 0.05f, TextAnchor.MiddleCenter);
            title.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            title.color = new Color(0.65f, 0.92f, 1f);

            var body = CreateText3D(
                "F1: overview / simulated headset\nRMB drag: look    WASD: move\nLeft drag: select and move objects\nG: crumple clip, then throw into RED bin\nT: split clip into two parts\nSpace / green button: PLAY / STOP\nR: reset    Q / E: speed - / +",
                new Vector3(0f, 2.05f, -2.16f),
                0.029f,
                TextAnchor.MiddleCenter);
            body.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            body.color = new Color(0.92f, 0.96f, 1f);
        }

        private void CreateMediaClips()
        {
            var names = new[] { "Clip A", "Clip B", "Clip C" };
            for (var i = 0; i < names.Length; i++)
            {
                var position = new Vector3(-3.55f, DragPlaneY, -1.1f + i * 1.0f);
                var clip = CreateCube(names[i], position, new Vector3(1.05f, 0.36f, 0.56f), clipMat).AddComponent<TimeCraftInteractable>();
                clip.Initialize(InteractableKind.MediaClip, names[i], clipMat, selectedMat, appliedMat, new Vector3(1.05f, 0.36f, 0.56f));
                mediaClips.Add(clip);
                clipStartPositions.Add(position);
            }
        }

        private void CreateEffects()
        {
            var names = new[] { "Blur", "Colour", "Echo" };
            for (var i = 0; i < names.Length; i++)
            {
                var position = new Vector3(3.55f, DragPlaneY, -1.1f + i * 1.0f);
                var effect = CreateCube(names[i] + " Effect", position, new Vector3(0.72f, 0.5f, 0.5f), effectMat).AddComponent<TimeCraftInteractable>();
                effect.Initialize(InteractableKind.Effect, names[i], effectMat, selectedMat, effectMat, new Vector3(0.72f, 0.5f, 0.5f));
                effects.Add(effect);
                effectStartPositions.Add(position);
            }
        }

        private GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            return cube;
        }

        private void EnsureTrashBin()
        {
            var existingBin = GameObject.Find("RED TRASH BIN");
            if (existingBin != null)
            {
                existingBin.transform.position = TrashBinPosition;
                var existingLabel = existingBin.transform.Find("TRASH Label");
                if (existingLabel != null)
                {
                    existingLabel.localRotation = Quaternion.identity;
                }

                var existingTrigger = existingBin.GetComponentInChildren<TimeCraftTrashBin>(true);
                if (existingTrigger != null)
                {
                    existingTrigger.Initialize(this);
                }
                return;
            }

            var root = new GameObject("RED TRASH BIN");
            root.transform.position = TrashBinPosition;
            var red = MakeMaterial("Trash Bin Red", new Color(0.88f, 0.035f, 0.045f));

            CreateBinPart(root.transform, "Bin Left", new Vector3(-0.46f, 0.62f, 0f), new Vector3(0.12f, 1.18f, 1.0f), red);
            CreateBinPart(root.transform, "Bin Right", new Vector3(0.46f, 0.62f, 0f), new Vector3(0.12f, 1.18f, 1.0f), red);
            CreateBinPart(root.transform, "Bin Front", new Vector3(0f, 0.62f, -0.46f), new Vector3(0.82f, 1.18f, 0.12f), red);
            CreateBinPart(root.transform, "Bin Back", new Vector3(0f, 0.62f, 0.46f), new Vector3(0.82f, 1.18f, 0.12f), red);
            CreateBinPart(root.transform, "Bin Bottom", new Vector3(0f, 0.08f, 0f), new Vector3(0.82f, 0.14f, 0.82f), shelfMat);

            CreateBinPart(root.transform, "Rim Left", new Vector3(-0.5f, 1.23f, 0f), new Vector3(0.12f, 0.12f, 1.12f), red);
            CreateBinPart(root.transform, "Rim Right", new Vector3(0.5f, 1.23f, 0f), new Vector3(0.12f, 0.12f, 1.12f), red);
            CreateBinPart(root.transform, "Rim Front", new Vector3(0f, 1.23f, -0.5f), new Vector3(0.9f, 0.12f, 0.12f), red);
            CreateBinPart(root.transform, "Rim Back", new Vector3(0f, 1.23f, 0.5f), new Vector3(0.9f, 0.12f, 0.12f), red);

            var triggerObject = new GameObject("Recycle Trigger");
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(0.74f, 1.02f, 0.74f);
            triggerObject.AddComponent<TimeCraftTrashBin>().Initialize(this);

            var labelObject = new GameObject("TRASH Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 1.52f, -0.54f);
            labelObject.transform.localRotation = Quaternion.identity;
            var label = labelObject.AddComponent<TextMesh>();
            label.text = "TRASH";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.045f;
            label.color = new Color(1f, 0.82f, 0.82f);
        }

        private void EnsureInstructionPaperInteractable()
        {
            var board = GameObject.Find("Instruction Board");
            if (board == null)
            {
                return;
            }

            instructionPaper = board.GetComponent<TimeCraftInteractable>();
            var content = FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
                .Where(textMesh => textMesh != null &&
                                   textMesh.gameObject.activeInHierarchy &&
                                   Vector3.Distance(textMesh.transform.position, board.transform.position) < 2.5f)
                .Select(textMesh => textMesh.gameObject)
                .ToArray();

            foreach (var item in content)
            {
                item.transform.SetParent(board.transform, true);
            }

            if (instructionPaper == null)
            {
                instructionPaper = board.AddComponent<TimeCraftInteractable>();
                var boardMaterial = board.GetComponent<Renderer>().sharedMaterial;
                instructionPaper.Initialize(
                    InteractableKind.MediaClip,
                    "Controls Paper",
                    boardMaterial,
                    selectedMat,
                    boardMaterial,
                    board.transform.localScale,
                    true,
                    false);
            }

            instructionPaper.SetPaperContent(content);
            instructionPaperStartPosition = board.transform.position;
            instructionPaperStartRotation = board.transform.rotation;
        }

        private void EnsureEditingWorkspaceAndPlayback()
        {
            var trackBed = GameObject.Find("Timeline Track Bed");
            if (trackBed != null)
            {
                trackBed.transform.position = new Vector3(0f, 0.23f, 0.45f);
                trackBed.transform.localScale = new Vector3(5.35f, 0.12f, 0.92f);
                var renderer = trackBed.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = MakeMaterial("Editing Zone Green", new Color(0.08f, 0.58f, 0.28f));
                }
                editingZoneCollider = trackBed.GetComponent<Collider>();
            }

            clipSeparatorMaterial = MakeMaterial("Clip Separator Black", new Color(0.005f, 0.005f, 0.005f));
            for (var i = 0; i < 3; i++)
            {
                var separator = GameObject.Find("Timeline Node " + (i + 1));
                if (separator == null)
                {
                    continue;
                }
                separator.SetActive(false);
            }

            var buttonObject = GameObject.Find("PLAY SEQUENCE BUTTON");
            TextMesh buttonLabel;
            if (buttonObject == null)
            {
                buttonObject = CreateCube(
                    "PLAY SEQUENCE BUTTON",
                    new Vector3(2.05f, 0.28f, -0.92f),
                    new Vector3(0.72f, 0.2f, 0.68f),
                    MakeMaterial("Play Button Green", new Color(0.08f, 0.72f, 0.3f)));
                playbackButton = buttonObject.AddComponent<TimeCraftPlaybackButton>();

                var labelObject = new GameObject("PLAY Button Label");
                labelObject.transform.SetParent(buttonObject.transform, false);
                labelObject.transform.localPosition = new Vector3(0f, 0.56f, 0f);
                labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                buttonLabel = labelObject.AddComponent<TextMesh>();
                buttonLabel.anchor = TextAnchor.MiddleCenter;
                buttonLabel.alignment = TextAlignment.Center;
                buttonLabel.fontSize = 48;
                buttonLabel.characterSize = 0.12f;
            }
            else
            {
                buttonObject.transform.position = new Vector3(2.05f, 0.28f, -0.92f);
                playbackButton = buttonObject.GetComponent<TimeCraftPlaybackButton>();
                if (playbackButton == null)
                {
                    playbackButton = buttonObject.AddComponent<TimeCraftPlaybackButton>();
                }
                buttonLabel = buttonObject.GetComponentInChildren<TextMesh>(true);
            }

            playbackButton.Initialize(this, buttonLabel);
        }

        private void EnsureTimelineScrubber()
        {
            const float width = 5f;
            var root = GameObject.Find("TIMELINE PROGRESS BAR");
            if (root == null)
            {
                root = new GameObject("TIMELINE PROGRESS BAR");
                root.transform.position = new Vector3(0f, 0.18f, -0.48f);
                timelineScrubber = root.AddComponent<TimeCraftTimelineScrubber>();

                var track = CreateCube("Progress Track", Vector3.zero, new Vector3(width, 0.05f, 0.13f), shelfMat);
                track.transform.SetParent(root.transform, false);

                var fill = CreateCube("Progress Fill", Vector3.zero, new Vector3(0.02f, 0.07f, 0.13f), trackAxisMat);
                fill.transform.SetParent(root.transform, false);

                var handle = CreateCube("Progress Handle", Vector3.zero, new Vector3(0.12f, 0.26f, 0.32f), selectedMat);
                handle.transform.SetParent(root.transform, false);
                timelineScrubber.Initialize(fill.transform, handle.transform, width);
            }
            else
            {
                timelineScrubber = root.GetComponent<TimeCraftTimelineScrubber>();
            }
        }

        private void EnsureWorldPreviewDisplay()
        {
            var existing = GameObject.Find("Sequence Preview Letter");
            if (existing == null)
            {
                existing = new GameObject("Sequence Preview Letter");
                existing.transform.position = new Vector3(0f, 2.15f, 2.72f);
                existing.transform.rotation = Quaternion.identity;
                worldPreviewText = existing.AddComponent<TextMesh>();
                worldPreviewText.anchor = TextAnchor.MiddleCenter;
                worldPreviewText.alignment = TextAlignment.Center;
                worldPreviewText.fontSize = 64;
                worldPreviewText.characterSize = 0.13f;
                worldPreviewText.color = new Color(0.35f, 0.95f, 1f);
            }
            else
            {
                worldPreviewText = existing.GetComponent<TextMesh>();
            }
            UpdateWorldPreview(null, 0f);
        }

        private GameObject CreateBinPart(Transform parent, string name, Vector3 localPosition, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private TextMesh CreateText3D(string text, Vector3 position, float size, TextAnchor anchor)
        {
            var obj = new GameObject(text + " Label");
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            var mesh = obj.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = anchor;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.color = new Color(0.82f, 0.9f, 1f);
            return mesh;
        }

        private void CreateScreenUi()
        {
            var canvasObject = new GameObject("Prototype HUD");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var panel = CreateUiPanel(canvasObject.transform, "Top Panel", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -112f), new Vector2(0f, 112f), new Color(0f, 0f, 0f, 0.48f));
            CreateUiText(panel.transform, "Title", "TimeCraft VR - IP1 No-Headset Usability Build", new Vector2(18f, -12f), new Vector2(650f, 30f), 21, FontStyle.Bold, TextAnchor.UpperLeft);
            timerText = CreateUiText(panel.transform, "Timer", "Session 00:00 / 05:00", new Vector2(-260f, -14f), new Vector2(240f, 28f), 17, FontStyle.Bold, TextAnchor.UpperRight);
            statusText = CreateUiText(panel.transform, "Status", "", new Vector2(18f, -46f), new Vector2(860f, 28f), 16, FontStyle.Normal, TextAnchor.UpperLeft);
            metricsText = CreateUiText(panel.transform, "Metrics", "", new Vector2(18f, -76f), new Vector2(860f, 28f), 14, FontStyle.Normal, TextAnchor.UpperLeft);

            var shortcutsTexture = Resources.Load<Texture2D>("TimeCraft_Shortcuts");
            if (shortcutsTexture != null)
            {
                CreateUiTexture(
                    canvasObject.transform,
                    "Shortcut Texture Board",
                    shortcutsTexture,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 0f),
                    new Vector2(18f, 18f),
                    new Vector2(560f, 290f));
            }
            else
            {
                var help = CreateUiPanel(canvasObject.transform, "Help Panel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(430f, 172f), new Color(0f, 0f, 0f, 0.46f));
                CreateUiText(
                    help.transform,
                    "Controls",
                    "Controls\nF1: overview / simulated headset\nRMB drag: look around\nWASD/arrows: move\nWheel: overview zoom\nLeft drag: select / move object\nG: crumple selected clip\nT: split selected clip\nSpace / green button: PLAY / STOP\nR: reset scene",
                    new Vector2(12f, -10f),
                    new Vector2(400f, 150f),
                    14,
                    FontStyle.Normal,
                    TextAnchor.UpperLeft);
            }

            var preview = CreateUiPanel(canvasObject.transform, "Preview Panel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-438f, 18f), new Vector2(420f, 142f), new Color(0f, 0f, 0f, 0.46f));
            previewText = CreateUiText(preview.transform, "Preview Text", "", new Vector2(12f, -10f), new Vector2(392f, 120f), 14, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        private RectTransform CreateUiPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = panel.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private RectTransform CreateUiTexture(Transform parent, string name, Texture2D texture, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);

            var rect = imageObject.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var rawImage = imageObject.AddComponent<RawImage>();
            rawImage.texture = texture;
            rawImage.color = Color.white;
            return rect;
        }

        private Text CreateUiText(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var textComponent = obj.AddComponent<Text>();
            textComponent.font = GetUiFont();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = anchor;
            textComponent.color = Color.white;
            return textComponent;
        }

        private void HandleSelectionAndDrag()
        {
            if (GetMouseButtonDown(0))
            {
                if (TryBeginScrubberDrag(GetInteractionRay()))
                {
                    draggingScrubber = true;
                    dragged = null;
                    Select(null);
                    return;
                }

                if (TryActivatePlaybackButton(GetInteractionRay()))
                {
                    dragged = null;
                    Select(null);
                    return;
                }

                var floatingPreview = RaycastFloatingPreview();
                if (floatingPreview != null)
                {
                    Select(null);
                    selectedFloatingPreview = floatingPreview;
                    draggedFloatingPreview = floatingPreview;
                    floatingPreview.BeginHold();
                    dragOffset = floatingPreview.transform.position - MouseWorldPoint();
                    previousDragPosition = floatingPreview.transform.position;
                    previousDragTime = Time.unscaledTime;
                    desktopThrowVelocity = Vector3.zero;
                    SetStatus("Selected small preview " + floatingPreview.DisplayName + ". Drag it, or press G to crumple it.");
                    return;
                }

                var trimPiece = RaycastTrimPiece();
                if (trimPiece != null)
                {
                    successfulSelections++;
                    Select(trimPiece.Owner);
                    draggedTrimPiece = trimPiece;
                    dragOffset = trimPiece.transform.position - MouseWorldPoint();
                    previewedClip = trimPiece.Owner;
                    UpdateWorldPreview(trimPiece.Owner, Mathf.Max(0, mediaClips.IndexOf(trimPiece.Owner)) * 5f);
                    SetStatus("Selected " + trimPiece.name + ". This torn piece can move independently.");
                }
                else
                {
                    var hit = RaycastInteractable();
                    if (hit != null)
                    {
                        successfulSelections++;
                        Select(hit);
                        dragged = hit;
                        PrepareClipForDrag(hit);
                        dragged.BeginHold();
                        dragOffset = hit.transform.position - MouseWorldPoint();
                        previousDragPosition = hit.transform.position;
                        previousDragTime = Time.unscaledTime;
                        desktopThrowVelocity = Vector3.zero;
                        SetStatus("Selected " + hit.DisplayName + ". Drag to move, or use keyboard controls.");
                    }
                    else
                    {
                        missedSelections++;
                        Select(null);
                        SetStatus(headsetSimulatorMode
                            ? "Nothing selected. Aim the centre reticle at a clip or effect."
                            : "Nothing selected. Click a clip or effect.");
                    }
                }
            }

            if (draggingScrubber)
            {
                if (GetMouseButton(0))
                {
                    UpdateScrubberFromRay(GetInteractionRay());
                }
                if (GetMouseButtonUp(0))
                {
                    draggingScrubber = false;
                }
                return;
            }

            if (draggedFloatingPreview != null)
            {
                if (GetMouseButton(0))
                {
                    var target = MouseWorldPoint() + dragOffset;
                    target.x = Mathf.Clamp(target.x, -4.2f, 4.2f);
                    target.z = Mathf.Clamp(target.z, -2.2f, 3.2f);
                    draggedFloatingPreview.transform.position = target;
                    var now = Time.unscaledTime;
                    var elapsed = Mathf.Max(0.001f, now - previousDragTime);
                    var velocity = (target - previousDragPosition) / elapsed;
                    desktopThrowVelocity = Vector3.Lerp(desktopThrowVelocity, velocity, 0.45f);
                    previousDragPosition = target;
                    previousDragTime = now;
                }
                if (GetMouseButtonUp(0))
                {
                    if (draggedFloatingPreview.IsCrumpled)
                    {
                        draggedFloatingPreview.Throw(desktopThrowVelocity + Vector3.up * 0.5f);
                        SetStatus("Small preview thrown. It can land elsewhere or be recycled in the red bin.");
                    }
                    draggedFloatingPreview = null;
                }
                return;
            }

            if (draggedTrimPiece != null)
            {
                if (GetMouseButton(0))
                {
                    var target = MouseWorldPoint() + dragOffset;
                    target.y = 0.48f;
                    target.x = Mathf.Clamp(target.x, -3.7f, 3.7f);
                    target.z = Mathf.Clamp(target.z, -1.75f, 2.4f);
                    draggedTrimPiece.transform.position = target;
                    UpdateWorldPreview(draggedTrimPiece.Owner, Mathf.Max(0, mediaClips.IndexOf(draggedTrimPiece.Owner)) * 5f);
                }
                if (GetMouseButtonUp(0))
                {
                    SetStatus(draggedTrimPiece.name + " moved independently.");
                    draggedTrimPiece = null;
                }
                return;
            }

            if (GetMouseButton(0) && dragged != null)
            {
                var target = MouseWorldPoint() + dragOffset;
                target.y = DragPlaneY;
                target.x = Mathf.Clamp(target.x, -3.7f, 3.7f);
                target.z = Mathf.Clamp(target.z, -1.75f, 2.4f);
                dragged.transform.position = target;

                var now = Time.unscaledTime;
                var elapsed = Mathf.Max(0.001f, now - previousDragTime);
                var instantaneous = (target - previousDragPosition) / elapsed;
                desktopThrowVelocity = Vector3.Lerp(desktopThrowVelocity, instantaneous, 0.45f);
                previousDragPosition = target;
                previousDragTime = now;

                if (dragged.Kind == InteractableKind.MediaClip && !dragged.IsPaperSheet && !dragged.IsCrumpled)
                {
                    previewedClip = dragged;
                    sequencePlaybackTime = Mathf.Max(0, mediaClips.IndexOf(dragged)) * 5f;
                    UpdateWorldPreview(dragged, sequencePlaybackTime);
                    if (suppressSnapUntilClipLeavesTrack && !TouchesEditingZone(dragged, target))
                    {
                        suppressSnapUntilClipLeavesTrack = false;
                    }
                    if (!suppressSnapUntilClipLeavesTrack)
                    {
                        TryMagneticSnap(dragged, target);
                    }
                }
            }

            if (GetMouseButtonUp(0) && dragged != null)
            {
                if (TryDropClipAtPreviewScreen(dragged))
                {
                    dragged = null;
                    return;
                }
                if (dragged.Kind == InteractableKind.MediaClip && dragged.IsCrumpled)
                {
                    dragged.Throw(desktopThrowVelocity + Vector3.up * 0.65f);
                    SetStatus(dragged.DisplayName + " thrown. Aim for the red trash bin!");
                }
                else if (dragged.Kind == InteractableKind.MediaClip)
                {
                    TrySnapClip(dragged);
                }
                else
                {
                    TryApplyDraggedEffect(dragged);
                    ReturnEffectToShelf(dragged);
                }

                dragged = null;
            }
        }

        private TimeCraftInteractable RaycastInteractable()
        {
            var ray = GetInteractionRay();
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                return hit.collider.GetComponentInParent<TimeCraftInteractable>();
            }

            return null;
        }

        private TimeCraftTrimPiece RaycastTrimPiece()
        {
            var ray = GetInteractionRay();
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                return hit.collider.GetComponentInParent<TimeCraftTrimPiece>();
            }
            return null;
        }

        private TimeCraftFloatingPreview RaycastFloatingPreview()
        {
            var ray = GetInteractionRay();
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                return hit.collider.GetComponentInParent<TimeCraftFloatingPreview>();
            }
            return null;
        }

        private void PrepareClipForDrag(TimeCraftInteractable item)
        {
            suppressSnapUntilClipLeavesTrack = false;
            dragOriginPosition = item.transform.position;
            dragOriginRotation = item.transform.rotation;
            dragOriginSequenceIndex = item.SequenceIndex;

            if (item.Kind == InteractableKind.MediaClip && !item.IsPaperSheet && item.SequenceIndex >= 0)
            {
                item.SequenceIndex = -1;
                suppressSnapUntilClipLeavesTrack = true;
                ReflowConnectedClips();
            }
        }

        private bool TryDropClipAtPreviewScreen(TimeCraftInteractable clip)
        {
            if (clip == null || clip.Kind != InteractableKind.MediaClip || clip.IsPaperSheet || clip.IsCrumpled ||
                clip.transform.position.z < 1.65f || Mathf.Abs(clip.transform.position.x) > 3.6f)
            {
                return false;
            }

            CreateFloatingClipPreview(clip);
            clip.transform.SetPositionAndRotation(dragOriginPosition, dragOriginRotation);
            clip.SequenceIndex = dragOriginSequenceIndex;
            suppressSnapUntilClipLeavesTrack = false;
            ReflowConnectedClips();
            SetStatus(clip.DisplayName + " returned to its original position; a small preview screen is now playing beside the main screen.");
            return true;
        }

        private void CreateFloatingClipPreview(TimeCraftInteractable clip)
        {
            var side = floatingPreviewCount % 2 == 0 ? 1f : -1f;
            var row = floatingPreviewCount / 2;
            var position = new Vector3(side * 3.15f, 2.55f - row * 0.9f, 2.62f);

            var root = CreateCube(
                "Floating Preview - " + clip.DisplayName,
                position,
                new Vector3(1.35f, 0.72f, 0.07f),
                screenMat);

            var labelObject = new GameObject("Small Preview Text");
            labelObject.transform.position = position + new Vector3(0f, 0f, -0.055f);
            labelObject.transform.rotation = Quaternion.identity;
            labelObject.transform.SetParent(root.transform, true);
            var text = labelObject.AddComponent<TextMesh>();
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.034f;
            text.color = new Color(0.4f, 0.95f, 1f);

            var sourceIndex = Mathf.Max(0, mediaClips.IndexOf(clip));
            var letter = ((char)('A' + sourceIndex)).ToString();
            root.AddComponent<TimeCraftFloatingPreview>().Initialize(text, clip.DisplayName, letter);
            floatingPreviewScreens.Add(root);
            floatingPreviewCount++;
        }

        private Vector3 MouseWorldPoint()
        {
            var ray = GetInteractionRay();
            var plane = new Plane(Vector3.up, new Vector3(0f, DragPlaneY, 0f));
            return plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : Vector3.zero;
        }

        private Ray GetInteractionRay()
        {
            if (headsetSimulatorMode)
            {
                return mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }

            return mainCamera.ScreenPointToRay(GetMousePosition());
        }

        private void Select(TimeCraftInteractable item)
        {
            if (selected != null)
            {
                selected.SetSelected(false);
            }

            selected = item;
            selectedFloatingPreview = null;

            if (selected != null)
            {
                selected.SetSelected(true);
                if (selected.Kind == InteractableKind.MediaClip && !selected.IsPaperSheet)
                {
                    previewedClip = selected;
                    sequencePlaybackTime = Mathf.Max(0, mediaClips.IndexOf(selected)) * 5f;
                    UpdateWorldPreview(selected, sequencePlaybackTime);
                }
            }
        }

        public void SetXRMode(bool active)
        {
            xrMode = active;
            headsetSimulatorMode = false;
            dragged = null;
            draggedFloatingPreview = null;
            xrDraggedFloatingPreview = null;
            Select(null);

            if (active)
            {
                SetStatus("Quest 2 mode ready. Point with either controller and hold Trigger to move an item.");
            }
        }

        public bool BeginXRDrag(Ray ray, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (!Physics.Raycast(ray, out var hit, 12f))
            {
                missedSelections++;
                Select(null);
                SetStatus("Nothing selected. Point the controller ray at a clip or effect.");
                return false;
            }

            var hitPlaybackButton = hit.collider.GetComponentInParent<TimeCraftPlaybackButton>();
            if (hitPlaybackButton != null)
            {
                hitPoint = hit.point;
                hitPlaybackButton.Activate();
                return false;
            }

            var hitScrubber = hit.collider.GetComponentInParent<TimeCraftTimelineScrubber>();
            if (hitScrubber != null)
            {
                xrScrubbing = true;
                hitPoint = hit.point;
                UpdateScrubberFromWorldX(hit.point.x);
                return true;
            }

            var hitFloatingPreview = hit.collider.GetComponentInParent<TimeCraftFloatingPreview>();
            if (hitFloatingPreview != null)
            {
                Select(null);
                selectedFloatingPreview = hitFloatingPreview;
                xrDraggedFloatingPreview = hitFloatingPreview;
                hitFloatingPreview.BeginHold();
                hitPoint = hit.point;
                successfulSelections++;
                SetStatus("Selected small preview " + hitFloatingPreview.DisplayName + ". Move it, or squeeze Grip to crumple it.");
                return true;
            }

            var hitTrimPiece = hit.collider.GetComponentInParent<TimeCraftTrimPiece>();
            if (hitTrimPiece != null)
            {
                xrDraggedTrimPiece = hitTrimPiece;
                hitPoint = hit.point;
                successfulSelections++;
                Select(hitTrimPiece.Owner);
                previewedClip = hitTrimPiece.Owner;
                UpdateWorldPreview(hitTrimPiece.Owner, Mathf.Max(0, mediaClips.IndexOf(hitTrimPiece.Owner)) * 5f);
                SetStatus("Selected " + hitTrimPiece.name + ". Move this torn piece independently.");
                return true;
            }

            var interactable = hit.collider.GetComponentInParent<TimeCraftInteractable>();
            if (interactable == null)
            {
                missedSelections++;
                Select(null);
                return false;
            }

            successfulSelections++;
            Select(interactable);
            dragged = interactable;
            PrepareClipForDrag(interactable);
            hitPoint = hit.point;
            SetStatus("Selected " + interactable.DisplayName + ". Keep holding Trigger to move it.");
            return true;
        }

        public void MoveXRDrag(Vector3 worldPosition)
        {
            if (xrScrubbing)
            {
                UpdateScrubberFromWorldX(worldPosition.x);
                return;
            }

            if (xrDraggedFloatingPreview != null)
            {
                worldPosition.x = Mathf.Clamp(worldPosition.x, -4.2f, 4.2f);
                worldPosition.y = Mathf.Clamp(worldPosition.y, 0.2f, 3.6f);
                worldPosition.z = Mathf.Clamp(worldPosition.z, -2.2f, 3.2f);
                xrDraggedFloatingPreview.transform.position = worldPosition;
                return;
            }

            if (xrDraggedTrimPiece != null)
            {
                worldPosition.y = 0.48f;
                worldPosition.x = Mathf.Clamp(worldPosition.x, -3.7f, 3.7f);
                worldPosition.z = Mathf.Clamp(worldPosition.z, -1.75f, 2.4f);
                xrDraggedTrimPiece.transform.position = worldPosition;
                UpdateWorldPreview(xrDraggedTrimPiece.Owner, Mathf.Max(0, mediaClips.IndexOf(xrDraggedTrimPiece.Owner)) * 5f);
                return;
            }

            if (dragged == null)
            {
                return;
            }

            worldPosition.y = DragPlaneY;
            worldPosition.x = Mathf.Clamp(worldPosition.x, -3.7f, 3.7f);
            worldPosition.z = Mathf.Clamp(worldPosition.z, -1.75f, 2.4f);
            dragged.transform.position = worldPosition;
            if (dragged.Kind == InteractableKind.MediaClip && !dragged.IsPaperSheet && !dragged.IsCrumpled)
            {
                previewedClip = dragged;
                sequencePlaybackTime = Mathf.Max(0, mediaClips.IndexOf(dragged)) * 5f;
                UpdateWorldPreview(dragged, sequencePlaybackTime);
                if (suppressSnapUntilClipLeavesTrack && !TouchesEditingZone(dragged, worldPosition))
                {
                    suppressSnapUntilClipLeavesTrack = false;
                }
                if (!suppressSnapUntilClipLeavesTrack)
                {
                    TryMagneticSnap(dragged, worldPosition);
                }
            }
        }

        public void EndXRDrag()
        {
            if (xrScrubbing)
            {
                xrScrubbing = false;
                return;
            }

            if (xrDraggedFloatingPreview != null)
            {
                SetStatus("Small preview moved. Squeeze Grip to crumple it, then release Grip to throw it.");
                xrDraggedFloatingPreview = null;
                return;
            }

            if (xrDraggedTrimPiece != null)
            {
                SetStatus(xrDraggedTrimPiece.name + " moved independently.");
                xrDraggedTrimPiece = null;
                return;
            }

            if (dragged == null)
            {
                return;
            }

            if (TryDropClipAtPreviewScreen(dragged))
            {
                dragged = null;
                return;
            }

            if (dragged.Kind == InteractableKind.MediaClip)
            {
                TrySnapClip(dragged);
            }
            else
            {
                TryApplyDraggedEffect(dragged);
                ReturnEffectToShelf(dragged);
            }

            dragged = null;
        }

        public bool CrumpleSelected()
        {
            if (selectedFloatingPreview != null && selectedFloatingPreview.Crumple())
            {
                SetStatus(selectedFloatingPreview.DisplayName + " small preview crumpled into a ball. Throw it into the red trash bin or elsewhere.");
                return true;
            }

            if (selected == null || !selected.Crumple())
            {
                SetStatus("Select a media clip first, then squeeze Grip (or press G). Effects cannot be crumpled.");
                return false;
            }

            SetStatus(selected.DisplayName + " crumpled into a ball. Throw it into the red trash bin.");
            return true;
        }

        public void HoldSelectedCrumpled(Vector3 position, Quaternion rotation)
        {
            if (selectedFloatingPreview != null && selectedFloatingPreview.IsCrumpled)
            {
                selectedFloatingPreview.BeginHold();
                selectedFloatingPreview.HoldAt(position, rotation);
                return;
            }

            if (selected != null && selected.IsCrumpled)
            {
                selected.BeginHold();
                selected.HoldAt(position, rotation);
            }
        }

        public void ThrowSelectedCrumpled(Vector3 velocity)
        {
            if (selectedFloatingPreview != null && selectedFloatingPreview.IsCrumpled)
            {
                selectedFloatingPreview.Throw(velocity);
                SetStatus(selectedFloatingPreview.DisplayName + " small preview thrown. It can land elsewhere or be recycled in the red bin.");
                return;
            }

            if (selected == null || !selected.IsCrumpled)
            {
                return;
            }

            selected.Throw(velocity);
            SetStatus(selected.DisplayName + " thrown. Aim for the red trash bin!");
        }

        public void NotifyRecycled(TimeCraftInteractable item)
        {
            recycledCount++;
            if (selected == item)
            {
                selected = null;
            }
            if (dragged == item)
            {
                dragged = null;
            }
            SetStatus(item.DisplayName + " recycled! Press R to restore all clips.");
        }

        public void NotifyFloatingPreviewRecycled(TimeCraftFloatingPreview preview)
        {
            recycledCount++;
            if (selectedFloatingPreview == preview)
            {
                selectedFloatingPreview = null;
            }
            if (draggedFloatingPreview == preview)
            {
                draggedFloatingPreview = null;
            }
            if (xrDraggedFloatingPreview == preview)
            {
                xrDraggedFloatingPreview = null;
            }
            SetStatus(preview.DisplayName + " small preview recycled! Press R to clear and restore the scene.");
        }

        public void ToggleSelectedTrim()
        {
            if (selected == null || selected.Kind != InteractableKind.MediaClip || selected.IsPaperSheet || selected.IsCrumpled)
            {
                return;
            }

            SplitClipIntoIndependentClips(selected);
            trimCount++;
        }

        private void SplitClipIntoIndependentClips(TimeCraftInteractable source)
        {
            var sourceName = source.DisplayName;
            var sourceSequence = source.SequenceIndex;
            var sourcePosition = source.transform.position;
            var sourceRotation = source.transform.rotation;
            var sourceListIndex = Mathf.Max(0, mediaClips.IndexOf(source));
            var partScale = source.BaseScale;
            partScale.x *= 0.5f;

            var left = CreateIndependentSplitClip(source, sourceName + "-1", partScale);
            var right = CreateIndependentSplitClip(source, sourceName + "-2", partScale);
            var halfRenderedWidth = left.transform.localScale.x * 0.5f;
            left.transform.SetPositionAndRotation(sourcePosition + Vector3.left * halfRenderedWidth, sourceRotation);
            right.transform.SetPositionAndRotation(sourcePosition + Vector3.right * halfRenderedWidth, sourceRotation);

            mediaClips.Remove(source);
            mediaClips.Insert(Mathf.Min(sourceListIndex, mediaClips.Count), left);
            mediaClips.Insert(Mathf.Min(sourceListIndex + 1, mediaClips.Count), right);
            generatedSplitClips.Add(left);
            generatedSplitClips.Add(right);

            source.SetSelected(false);
            source.gameObject.SetActive(false);

            if (sourceSequence >= 0)
            {
                foreach (var other in mediaClips.Where(item => item != left && item != right && item.SequenceIndex > sourceSequence))
                {
                    other.SequenceIndex++;
                }
                left.SequenceIndex = sourceSequence;
                right.SequenceIndex = sourceSequence + 1;
                ReflowConnectedClips();
            }

            Select(left);
            previewedClip = left;
            SetStatus(sourceName + " became two independent clips. Both inherited speed, effects, and appearance.");
        }

        private TimeCraftInteractable CreateIndependentSplitClip(
            TimeCraftInteractable source,
            string displayName,
            Vector3 scale)
        {
            var clipObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            clipObject.name = displayName;
            var clip = clipObject.AddComponent<TimeCraftInteractable>();
            clip.Initialize(
                InteractableKind.MediaClip,
                displayName,
                source.NormalMaterial,
                source.SelectedMaterial,
                source.AppliedMaterial,
                scale);
            clip.InheritEditableProperties(source);
            return clip;
        }

        public void ChangeSelectedSpeed(float delta)
        {
            if (selected == null || selected.Kind != InteractableKind.MediaClip)
            {
                return;
            }

            selected.ChangeSpeed(delta);
            speedChanges++;
            SetStatus(selected.DisplayName + (delta < 0f ? " slowed down." : " sped up."));
        }

        public void ToggleTimer()
        {
            timerRunning = !timerRunning;
            SetStatus(timerRunning ? "Timer resumed." : "Timer paused for evaluator note-taking.");
        }

        public void ToggleSequencePlayback()
        {
            if (sequencePlaying)
            {
                sequencePlaying = false;
                playbackButton?.SetPlaying(false);
                SetStatus("Sequence paused at " + FormatTime(sequencePlaybackTime) + ". Press PLAY to continue from here.");
                return;
            }

            foreach (var clip in mediaClips.Where(clip => clip != null && clip.gameObject.activeInHierarchy && clip.SequenceIndex < 0))
            {
                TryMagneticSnap(clip, clip.transform.position);
            }

            var ordered = mediaClips.Where(clip => clip.SequenceIndex >= 0).OrderBy(clip => clip.SequenceIndex).ToList();
            if (ordered.Count == 0)
            {
                SetStatus("Place at least one Clip on the green editing strip before pressing PLAY.");
                return;
            }

            var duration = GetSequenceDuration(ordered);
            if (sequencePlaybackTime < 0f || sequencePlaybackTime >= duration)
            {
                sequencePlaybackTime = 0f;
                timelineScrubber?.SetNormalized(0f);
            }

            sequencePlaying = true;
            playbackClipIndex = GetPlaybackClipIndex(ordered, sequencePlaybackTime);
            playbackButton?.SetPlaying(true);
            SetStatus(sequencePlaybackTime > 0.01f
                ? "Sequence resumed from " + FormatTime(sequencePlaybackTime) + "."
                : "Sequence playback started.");
        }

        private bool TryActivatePlaybackButton(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 100f))
            {
                return false;
            }

            var button = hit.collider.GetComponentInParent<TimeCraftPlaybackButton>();
            if (button == null)
            {
                return false;
            }

            button.Activate();
            return true;
        }

        private bool TryBeginScrubberDrag(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 100f) ||
                hit.collider.GetComponentInParent<TimeCraftTimelineScrubber>() == null)
            {
                return false;
            }

            UpdateScrubberFromWorldX(hit.point.x);
            return true;
        }

        private void UpdateScrubberFromRay(Ray ray)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, 0.2f, 0f));
            if (plane.Raycast(ray, out var distance))
            {
                UpdateScrubberFromWorldX(ray.GetPoint(distance).x);
            }
        }

        private void UpdateScrubberFromWorldX(float worldX)
        {
            var normalized = Mathf.InverseLerp(-2.5f, 2.5f, worldX);
            var ordered = mediaClips.Where(clip => clip.SequenceIndex >= 0).OrderBy(clip => clip.SequenceIndex).ToList();
            if (ordered.Count == 0)
            {
                ordered = mediaClips
                    .Where(clip => clip != null && clip.gameObject.activeInHierarchy && !clip.IsRecycled)
                    .ToList();
            }
            var duration = GetSequenceDuration(ordered);
            sequencePlaybackTime = normalized * duration;
            timelineScrubber?.SetNormalized(normalized);

            if (ordered.Count > 0)
            {
                var activeIndex = GetPlaybackClipIndex(ordered, sequencePlaybackTime);
                activeIndex = Mathf.Clamp(activeIndex, 0, ordered.Count - 1);
                playbackClipIndex = activeIndex;
                previewedClip = ordered[activeIndex];
                UpdateWorldPreview(previewedClip, sequencePlaybackTime);
                SetStatus("Scrubbing " + FormatTime(sequencePlaybackTime) + " - previewing " + previewedClip.DisplayName + ".");
            }
            else
            {
                UpdateWorldPreview(null, sequencePlaybackTime);
            }
        }

        private void UpdateSequencePlayback()
        {
            if (!sequencePlaying)
            {
                return;
            }

            var ordered = mediaClips.Where(clip => clip.SequenceIndex >= 0).OrderBy(clip => clip.SequenceIndex).ToList();
            if (ordered.Count == 0)
            {
                sequencePlaying = false;
                playbackButton?.SetPlaying(false);
                return;
            }

            sequencePlaybackTime += Time.deltaTime;
            var duration = GetSequenceDuration(ordered);
            var activeIndex = GetPlaybackClipIndex(ordered, sequencePlaybackTime);
            timelineScrubber?.SetNormalized(duration <= 0f ? 0f : sequencePlaybackTime / duration);

            if (activeIndex < 0)
            {
                sequencePlaying = false;
                playbackClipIndex = -1;
                timelineScrubber?.SetNormalized(1f);
                playbackButton?.SetPlaying(false);
                SetStatus("Sequence playback finished.");
                return;
            }

            if (playbackClipIndex != activeIndex)
            {
                playbackClipIndex = activeIndex;
                previewedClip = ordered[activeIndex];
                SetStatus("Playing " + ordered[activeIndex].DisplayName + "...");
            }
            UpdateWorldPreview(ordered[activeIndex], sequencePlaybackTime);
        }

        private static float GetSequenceDuration(List<TimeCraftInteractable> ordered)
        {
            return ordered.Sum(clip => 5f / Mathf.Max(0.5f, clip.Speed));
        }

        private static int GetPlaybackClipIndex(List<TimeCraftInteractable> ordered, float time)
        {
            var cursor = 0f;
            for (var i = 0; i < ordered.Count; i++)
            {
                cursor += 5f / Mathf.Max(0.5f, ordered[i].Speed);
                if (time < cursor || (i == ordered.Count - 1 && Mathf.Approximately(time, cursor)))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateWorldPreview(TimeCraftInteractable clip, float time)
        {
            if (worldPreviewText == null)
            {
                return;
            }

            if (clip == null)
            {
                worldPreviewText.text = "READY\n" + FormatTime(time);
                return;
            }

            var sourceIndex = Mathf.Max(0, mediaClips.IndexOf(clip));
            var letter = ((char)('A' + sourceIndex)).ToString();
            worldPreviewText.text = letter + "\n" + FormatTime(time) + "\n" + clip.DisplayName;
        }

        private void TrySnapClip(TimeCraftInteractable clip)
        {
            if (!TryMagneticSnap(clip, clip.transform.position))
            {
                clip.SequenceIndex = -1;
                ReflowConnectedClips();
            }
        }

        private bool TryMagneticSnap(TimeCraftInteractable clip, Vector3 contactPosition)
        {
            if (clip == null || clip.IsPaperSheet || clip.IsCrumpled || !TouchesEditingZone(clip, contactPosition))
            {
                return false;
            }

            var snappedX = ResolveNonOverlappingSnapX(clip, contactPosition.x);
            clip.transform.position = new Vector3(snappedX, 0.48f, 0.45f);
            clip.SequenceIndex = Mathf.Max(0, clip.SequenceIndex);
            ReflowConnectedClips();
            previewedClip = clip;
            SetStatus(clip.DisplayName + " snapped onto the green strip. Nearby clip edges join without overlapping.");
            return true;
        }

        private float ResolveNonOverlappingSnapX(TimeCraftInteractable clip, float desiredX)
        {
            var clipCollider = clip.GetComponent<Collider>();
            if (clipCollider == null)
            {
                return desiredX;
            }

            const float edgeSnapDistance = 0.28f;
            const float separation = 0.012f;
            var halfWidth = clipCollider.bounds.extents.x;
            var minX = editingZoneCollider != null ? editingZoneCollider.bounds.min.x + halfWidth : -2.75f + halfWidth;
            var maxX = editingZoneCollider != null ? editingZoneCollider.bounds.max.x - halfWidth : 2.75f - halfWidth;
            desiredX = Mathf.Clamp(desiredX, minX, maxX);

            var neighbours = mediaClips
                .Where(other => other != null && other != clip && other.SequenceIndex >= 0 &&
                                other.gameObject.activeInHierarchy && !other.IsCrumpled && !other.IsRecycled)
                .Select(other => other.GetComponent<Collider>())
                .Where(otherCollider => otherCollider != null)
                .ToList();

            bool IsFree(float centreX)
            {
                var candidateMin = centreX - halfWidth;
                var candidateMax = centreX + halfWidth;
                return neighbours.All(other => candidateMax <= other.bounds.min.x + 0.001f ||
                                               candidateMin >= other.bounds.max.x - 0.001f);
            }

            var edgeCandidates = new List<float>();
            foreach (var neighbour in neighbours)
            {
                edgeCandidates.Add(neighbour.bounds.min.x - halfWidth - separation);
                edgeCandidates.Add(neighbour.bounds.max.x + halfWidth + separation);
            }

            var validEdges = edgeCandidates
                .Select(value => Mathf.Clamp(value, minX, maxX))
                .Where(IsFree)
                .OrderBy(value => Mathf.Abs(value - desiredX))
                .ToList();
            var nearbyEdge = validEdges.Count > 0 ? validEdges[0] : float.NaN;

            if (IsFree(desiredX))
            {
                return !float.IsNaN(nearbyEdge) && Mathf.Abs(nearbyEdge - desiredX) <= edgeSnapDistance
                    ? nearbyEdge
                    : desiredX;
            }

            return float.IsNaN(nearbyEdge) ? desiredX : nearbyEdge;
        }

        private bool TouchesEditingZone(TimeCraftInteractable clip, Vector3 contactPosition)
        {
            var clipCollider = clip == null ? null : clip.GetComponent<Collider>();
            return editingZoneCollider != null && clipCollider != null
                ? BoundsOverlapXZ(clipCollider.bounds, editingZoneCollider.bounds)
                : contactPosition.x >= -2.75f && contactPosition.x <= 2.75f &&
                  contactPosition.z >= -0.15f && contactPosition.z <= 1.05f;
        }

        private void ReflowConnectedClips()
        {
            var ordered = mediaClips
                .Where(clip => clip != null && clip.SequenceIndex >= 0 && clip.gameObject.activeInHierarchy)
                .OrderBy(clip => clip.transform.position.x)
                .ToList();

            var boundaries = new List<float>();
            for (var i = 0; i < ordered.Count; i++)
            {
                ordered[i].SequenceIndex = i;
                if (i < ordered.Count - 1)
                {
                    var currentBounds = ordered[i].GetComponent<Collider>().bounds;
                    var nextBounds = ordered[i + 1].GetComponent<Collider>().bounds;
                    var gap = nextBounds.min.x - currentBounds.max.x;
                    if (Mathf.Abs(gap) <= 0.08f)
                    {
                        boundaries.Add((currentBounds.max.x + nextBounds.min.x) * 0.5f);
                    }
                }
            }
            UpdateDynamicClipSeparators(boundaries);
        }

        private void UpdateDynamicClipSeparators(List<float> boundaries)
        {
            while (dynamicClipSeparators.Count < boundaries.Count)
            {
                var separator = CreateCube(
                    "Dynamic Clip Separator",
                    Vector3.zero,
                    new Vector3(0.025f, 0.42f, 0.6f),
                    clipSeparatorMaterial);
                var collider = separator.GetComponent<Collider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }
                dynamicClipSeparators.Add(separator);
            }

            for (var i = 0; i < dynamicClipSeparators.Count; i++)
            {
                var active = i < boundaries.Count;
                dynamicClipSeparators[i].SetActive(active);
                if (active)
                {
                    dynamicClipSeparators[i].transform.position = new Vector3(boundaries[i], 0.49f, 0.45f);
                }
            }
        }

        private static bool BoundsOverlapXZ(Bounds first, Bounds second)
        {
            return first.max.x >= second.min.x && first.min.x <= second.max.x &&
                   first.max.z >= second.min.z && first.min.z <= second.max.z;
        }

        private void TryApplyDraggedEffect(TimeCraftInteractable effect)
        {
            var nearestClip = mediaClips
                .Where(clip => clip != null && clip.gameObject.activeInHierarchy)
                .OrderBy(clip => Vector3.Distance(clip.transform.position, effect.transform.position))
                .FirstOrDefault();
            if (nearestClip != null && Vector3.Distance(nearestClip.transform.position, effect.transform.position) < 1.15f)
            {
                nearestClip.ApplyEffect(effect.EffectName);
                effectsApplied++;
                SetStatus(effect.EffectName + " applied to " + nearestClip.DisplayName + ".");
            }
        }

        private void ReturnEffectToShelf(TimeCraftInteractable effect)
        {
            var index = effects.IndexOf(effect);
            if (index >= 0)
            {
                effect.transform.position = effectStartPositions[index];
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (GetKeyDown(KeyCode.F1))
            {
                ToggleHeadsetSimulator();
            }

            HandleCameraControls();

            if (GetKeyDown(KeyCode.R))
            {
                ResetPrototype();
            }

            if (GetKeyDown(KeyCode.Space))
            {
                ToggleSequencePlayback();
            }

            if (GetKeyDown(KeyCode.P))
            {
                promptCount++;
                SetStatus("Evaluator prompt logged. Keep note of what help was given.");
            }

            if (GetKeyDown(KeyCode.G))
            {
                SelectClipForCrumple();
                CrumpleSelected();
            }

            if (selected == null)
            {
                return;
            }

            if (GetKeyDown(KeyCode.T))
            {
                ToggleSelectedTrim();
            }

            if (GetKeyDown(KeyCode.Q))
            {
                selected.ChangeSpeed(-0.5f);
                speedChanges++;
                SetStatus(selected.DisplayName + " slowed down.");
            }

            if (GetKeyDown(KeyCode.E))
            {
                selected.ChangeSpeed(0.5f);
                speedChanges++;
                SetStatus(selected.DisplayName + " sped up.");
            }

            if (GetKeyDown(KeyCode.F) && selected.Kind == InteractableKind.Effect)
            {
                var nearestClip = mediaClips.OrderBy(c => Vector3.Distance(c.transform.position, selected.transform.position)).FirstOrDefault();
                if (nearestClip != null)
                {
                    nearestClip.ApplyEffect(selected.EffectName);
                    effectsApplied++;
                    SetStatus(selected.EffectName + " applied to " + nearestClip.DisplayName + ".");
                }
            }
        }

        private void SelectClipForCrumple()
        {
            if (selectedFloatingPreview != null && !selectedFloatingPreview.IsRecycled)
            {
                return;
            }

            if (selected != null && selected.Kind == InteractableKind.MediaClip && !selected.IsRecycled)
            {
                return;
            }

            var aimedAt = RaycastInteractable();
            if (aimedAt != null && aimedAt.Kind == InteractableKind.MediaClip && !aimedAt.IsRecycled)
            {
                Select(aimedAt);
                successfulSelections++;
                return;
            }

            if (instructionPaper != null && instructionPaper.gameObject.activeInHierarchy && !instructionPaper.IsRecycled)
            {
                Select(instructionPaper);
                successfulSelections++;
                return;
            }

            var availableClip = mediaClips.FirstOrDefault(clip =>
                clip != null && clip.gameObject.activeInHierarchy && !clip.IsRecycled && !clip.IsCrumpled);

            if (availableClip == null)
            {
                availableClip = mediaClips.FirstOrDefault(clip =>
                    clip != null && clip.gameObject.activeInHierarchy && !clip.IsRecycled);
            }

            if (availableClip != null)
            {
                Select(availableClip);
                successfulSelections++;
            }
        }

        private void HandleCameraControls()
        {
            if (mainCamera == null)
            {
                return;
            }

            if (GetMouseButton(1))
            {
                var delta = GetMouseDelta();
                cameraYaw += delta.x * 0.18f;
                cameraPitch = Mathf.Clamp(cameraPitch - delta.y * 0.12f, MinCameraPitch, MaxCameraPitch);
            }

            var move = Vector3.zero;
            if (GetKey(KeyCode.W) || GetKey(KeyCode.UpArrow)) move += Vector3.forward;
            if (GetKey(KeyCode.S) || GetKey(KeyCode.DownArrow)) move += Vector3.back;
            if (GetKey(KeyCode.A) || GetKey(KeyCode.LeftArrow)) move += Vector3.left;
            if (GetKey(KeyCode.D) || GetKey(KeyCode.RightArrow)) move += Vector3.right;

            if (move.sqrMagnitude > 0.001f)
            {
                var yawOnly = Quaternion.Euler(0f, cameraYaw, 0f);
                if (headsetSimulatorMode)
                {
                    simulatorPosition += yawOnly * move.normalized * (2.2f * Time.deltaTime);
                    simulatorPosition.x = Mathf.Clamp(simulatorPosition.x, -3.5f, 3.5f);
                    simulatorPosition.z = Mathf.Clamp(simulatorPosition.z, -2.35f, 3.1f);
                    simulatorPosition.y = SimulatedEyeHeight;
                }
                else
                {
                    cameraPivot += yawOnly * move.normalized * (3.2f * Time.deltaTime);
                    cameraPivot.x = Mathf.Clamp(cameraPivot.x, -3.5f, 3.5f);
                    cameraPivot.z = Mathf.Clamp(cameraPivot.z, -2.2f, 2.8f);
                }
            }

            var scroll = GetScrollDelta();
            if (!headsetSimulatorMode && Mathf.Abs(scroll) > 0.01f)
            {
                cameraDistance = Mathf.Clamp(cameraDistance - scroll * 0.035f, 2.6f, 12f);
            }

            ApplyCameraPose();
        }

        private void ApplyCameraPose()
        {
            if (mainCamera == null)
            {
                return;
            }

            var rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            mainCamera.transform.rotation = rotation;
            mainCamera.transform.position = headsetSimulatorMode
                ? simulatorPosition
                : cameraPivot - rotation * Vector3.forward * cameraDistance;
        }

        private void ToggleHeadsetSimulator()
        {
            headsetSimulatorMode = !headsetSimulatorMode;
            dragged = null;
            Select(null);

            if (headsetSimulatorMode)
            {
                simulatorPosition = new Vector3(0f, SimulatedEyeHeight, -2.35f);
                cameraYaw = 0f;
                cameraPitch = 12f;
                mainCamera.fieldOfView = 75f;
                SetStatus("Simulated headset enabled. Hold RMB to look, move with WASD, and aim with the centre reticle.");
                SetDesktopShortcutVisible(false);
            }
            else
            {
                cameraPivot = new Vector3(0f, 0.75f, 0.65f);
                cameraYaw = 0f;
                cameraPitch = 58f;
                cameraDistance = 7.2f;
                mainCamera.fieldOfView = 48f;
                SetStatus("Desktop overview enabled. Use the mouse cursor to select and drag objects.");
                SetDesktopShortcutVisible(true);
            }

            ApplyCameraPose();
        }

        private static void SetDesktopShortcutVisible(bool visible)
        {
            var allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var shortcut = allTransforms.FirstOrDefault(item => item.name == "Shortcut Texture Board");
            if (shortcut != null)
            {
                shortcut.gameObject.SetActive(visible);
            }
            var helpPanel = allTransforms.FirstOrDefault(item => item.name == "Help Panel");
            if (helpPanel != null)
            {
                helpPanel.gameObject.SetActive(visible);
            }
        }

        private bool GetMouseButtonDown(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasPressedThisFrame : Mouse.current.rightButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        private bool GetMouseButton(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.isPressed : Mouse.current.rightButton.isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        private bool GetMouseButtonUp(int button)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current == null) return false;
            return button == 0 ? Mouse.current.leftButton.wasReleasedThisFrame : Mouse.current.rightButton.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(button);
#endif
        }

        private Vector2 GetMousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
#else
            return Input.mousePosition;
#endif
        }

        private Vector2 GetMouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue();
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 12f;
#endif
        }

        private float GetScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current == null ? 0f : Mouse.current.scroll.ReadValue().y;
#else
            return Input.mouseScrollDelta.y * 120f;
#endif
        }

        private bool GetKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.A: return keyboard.aKey.wasPressedThisFrame;
                case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
                case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
                case KeyCode.F1: return keyboard.f1Key.wasPressedThisFrame;
                case KeyCode.G: return keyboard.gKey.wasPressedThisFrame;
                case KeyCode.P: return keyboard.pKey.wasPressedThisFrame;
                case KeyCode.Q: return keyboard.qKey.wasPressedThisFrame;
                case KeyCode.R: return keyboard.rKey.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.T: return keyboard.tKey.wasPressedThisFrame;
                default: return false;
            }
#else
            return Input.GetKeyDown(key);
#endif
        }

        private bool GetKey(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            switch (key)
            {
                case KeyCode.A: return keyboard.aKey.isPressed;
                case KeyCode.D: return keyboard.dKey.isPressed;
                case KeyCode.S: return keyboard.sKey.isPressed;
                case KeyCode.W: return keyboard.wKey.isPressed;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey.isPressed;
                case KeyCode.RightArrow: return keyboard.rightArrowKey.isPressed;
                case KeyCode.DownArrow: return keyboard.downArrowKey.isPressed;
                case KeyCode.UpArrow: return keyboard.upArrowKey.isPressed;
                default: return false;
            }
#else
            return Input.GetKey(key);
#endif
        }

        private void CaptureOriginalMediaClips()
        {
            if (originalMediaClips.Count > 0)
            {
                return;
            }

            foreach (var clip in mediaClips)
            {
                if (clip == null)
                {
                    continue;
                }
                originalMediaClips.Add(clip);
                originalClipPositions.Add(clip.transform.position);
            }
        }

        private void ResetPrototype()
        {
            foreach (var previewScreen in floatingPreviewScreens)
            {
                if (previewScreen != null)
                {
                    Destroy(previewScreen);
                }
            }
            floatingPreviewScreens.Clear();
            floatingPreviewCount = 0;
            selectedFloatingPreview = null;
            draggedFloatingPreview = null;
            xrDraggedFloatingPreview = null;

            foreach (var splitClip in generatedSplitClips)
            {
                if (splitClip != null)
                {
                    splitClip.gameObject.SetActive(false);
                    Destroy(splitClip.gameObject);
                }
            }
            generatedSplitClips.Clear();
            mediaClips = new List<TimeCraftInteractable>(originalMediaClips);

            for (var i = 0; i < originalMediaClips.Count; i++)
            {
                originalMediaClips[i].ResetState(originalClipPositions[i], Quaternion.identity);
            }

            UpdateDynamicClipSeparators(new List<float>());

            for (var i = 0; i < effects.Count; i++)
            {
                effects[i].ResetState(effectStartPositions[i], Quaternion.identity);
            }

            if (instructionPaper != null)
            {
                instructionPaper.ResetState(instructionPaperStartPosition, instructionPaperStartRotation);
            }

            sessionTimer = 0f;
            promptCount = 0;
            trimCount = 0;
            speedChanges = 0;
            effectsApplied = 0;
            successfulSelections = 0;
            missedSelections = 0;
            recycledCount = 0;
            timerRunning = true;
            sequencePlaying = false;
            sequencePlaybackTime = 0f;
            playbackClipIndex = -1;
            previewedClip = null;
            draggingScrubber = false;
            xrScrubbing = false;
            timelineScrubber?.SetNormalized(0f);
            playbackButton?.SetPlaying(false);
            UpdateWorldPreview(null, 0f);
            Select(null);
            SetStatus("Scene reset. Start the next 5-minute participant test.");
        }

        private void UpdatePreview()
        {
            if (previewText == null || metricsText == null)
            {
                return;
            }

            var ordered = mediaClips
                .Where(c => c.SequenceIndex >= 0)
                .OrderBy(c => c.SequenceIndex)
                .Select(c => c.DisplayName + " " + c.Speed.ToString("0.0") + "x" + (c.IsTrimmed ? " trimmed" : "") + (c.EffectApplied ? " +" + c.EffectName : ""))
                .ToList();

            var playbackLine = sequencePlaying && playbackClipIndex >= 0 && playbackClipIndex < ordered.Count
                ? "PLAYING: " + ordered[playbackClipIndex] + "\n"
                : string.Empty;

            previewText.text = "Preview state\n" + playbackLine +
                               (ordered.Count == 0 ? "No clips connected yet." : string.Join("  >  ", ordered)) +
                               "\n\nTesting aim: can the participant explain this as spatial video editing?";

            metricsText.text = "Evaluator metrics: selections " + successfulSelections + " | misses " + missedSelections + " | recycled " + recycledCount + " | prompts " + promptCount + " | trims " + trimCount + " | speed " + speedChanges + " | effects " + effectsApplied;
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }

        private string FormatTime(float time)
        {
            var minutes = Mathf.FloorToInt(time / 60f);
            var seconds = Mathf.FloorToInt(time % 60f);
            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        private Font GetUiFont()
        {
            if (uiFont != null)
            {
                return uiFont;
            }

            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null)
            {
                uiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            return uiFont;
        }
    }
}
