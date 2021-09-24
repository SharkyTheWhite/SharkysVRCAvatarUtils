using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
namespace SharkyTheWhite.VrcAvatarUtils.UploadCamPicture
{
    /// <summary>
    /// This Component is used on a Plane object (Unity build-in). The plane should be assigned a Thumbnail Image
    /// as texture using the default Unlit Shader. When starting the VRChat SDK Upload process, this Component will
    /// find the thumbnail camera and snap it this Plane so the Texture covers the Thumbnail.
    /// 
    /// Only one of these Components should exist in a Scene to not cause ambiguities.
    /// </summary>
    [AddComponentMenu("Sharky's VRC Avatar Utils/Align Upload Camera")]
    public class AlignUploadCam : MonoBehaviour
    {
        [Header("Adjust Thumbnail")]
        [Range(0, 99)]
        [Tooltip(
            "Zoom into your picture. 0 means show as much of picture as possible, 50 means show half of the maximum and so on.")]
        public float zoomIn = 0.0f;

        [Range(-100, 100)] [Tooltip("Move the image up (positive) or down (negative). 0 means centered.")]
        public float shiftVertical = 0.0f;

        [Range(-100, 100)] [Tooltip("Move the image right (positive) or left (negative). 0 means centered.")]
        public float shiftHorizontal = 0.0f;

        // Images are 256x192 in thumbnail size (so 4:3)
        private const float ThumbnailAspectRatio = 4.0f / 3.0f;

        private const string CameraGameObjectName = "/VRCCam";


        private Camera _vrcCam = null;
        private Text _thumbnailLabel = null;
        private float _zoomInLast, _shiftVerticalLast, _shiftHorizontalLast;
        private bool _weHaveControl = false;

        void Start()
        {
            // Clear previous state
            _vrcCam = null;
            _thumbnailLabel = null;
            _zoomInLast = zoomIn;
            _shiftVerticalLast = shiftVertical;
            _shiftHorizontalLast = shiftHorizontal;
            _weHaveControl = false;

            // Warn user if they created another VRCCam themselves by accident
            if (null != GameObject.Find(CameraGameObjectName))
            {
                Debug.LogError(
                    $"There was already was an Object called {CameraGameObjectName} in your scene " +
                    "before starting the build. Remove or rename it, else it will conflict with the Camera " +
                    "used by the builder!");
            }

            // Warn user if there are more than one active instances of our behaviour
            var siblings = FindObjectsOfType<AlignUploadCam>()
                .Count(sibling => sibling.isActiveAndEnabled);
            if (siblings > 1)
            {
                Debug.LogError(
                    $"There are {siblings} objects with the Align Upload Cam component enabled. " +
                    "This will cause issues since they compete for control of the VRCCam object.", this);
            }
        }

        void Update()
        {
            bool foundNew = false;
            if (_vrcCam == null && _weHaveControl)
            {
                _vrcCam = GameObject.Find(CameraGameObjectName)?.GetComponent<Camera>();
                if (_vrcCam != null)
                {
                    foundNew = true;
                }
            }
            else if (_thumbnailLabel == null)
            {
                // See in VRChat SDK: Assets/VRCSDK/Dependencies/VRChat/Resources/VRCSDKAvatar.prefab
                _thumbnailLabel = GameObject.Find(
                    "/VRCSDK/UI/Canvas/AvatarPanel/Avatar Info Panel/Thumbnail Section/ScreenCaptureImage/Text"
                    )?.GetComponent<Text>();
                // Add Controls Extension to Upload GUI (only once!)
                if (_thumbnailLabel != null && _thumbnailLabel.transform.Find("AlignUploadCamExtension") == null)
                {
                    _thumbnailLabel.text = 
                        "Note: Using the \"Align Upload Camera\" Script from Sharky's VRC Avatar Utils.";
                    _thumbnailLabel.color = new Color(0.5f, 0f, 0.4f);

                    var extension = (GameObject) Instantiate(Resources.Load("AlignUploadCamExtension"), _thumbnailLabel.transform, false);
                    var zoomInSlider = extension.transform.Find("ZoomIn").GetComponentInChildren<Slider>();
                    zoomInSlider.value = zoomIn;
                    zoomInSlider.onValueChanged.AddListener(v => zoomIn = v);
                    var shiftVerticalSlider = extension.transform.Find("ShiftVertical").GetComponentInChildren<Slider>();
                    shiftVerticalSlider.value = shiftVertical;
                    shiftVerticalSlider.onValueChanged.AddListener(v => shiftVertical = v);
                    var shiftHorizontalSlider = extension.transform.Find("ShiftHorizontal").GetComponentInChildren<Slider>();
                    shiftHorizontalSlider.value = shiftHorizontal;
                    shiftHorizontalSlider.onValueChanged.AddListener(v => shiftHorizontal = v);
                    _weHaveControl = true;
                }
            }

            // If cam first found or a parameter has changed, apply or update the alignment
            // ReSharper disable CompareOfFloatsByEqualityOperator
            if (_vrcCam != null && (foundNew || zoomIn != _zoomInLast ||
                                    shiftVertical != _shiftVerticalLast || shiftHorizontal != _shiftHorizontalLast))
                // ReSharper restore CompareOfFloatsByEqualityOperator
            {
                // Calculate the appropriate distance for the object to cover the view (according to the Margin)
                var meshFilter = this.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.mesh != null)
                {
                    var mySize = Vector3.Scale(this.transform.localScale, meshFilter.mesh.bounds.extents);
                    if (mySize.y > 1e-5)
                    {
                        Debug.LogError(
                            $"Use a regular plane mesh for Align Upload Cam, the mesh here is not an x-z-Plane!",
                            meshFilter);
                    }

                    // Calculate width and height of the plane, remove relative margin in percent on all four sides.
                    var vSize = mySize.z * (100.0f - zoomIn) / 100.0f;
                    var hSize = mySize.x * (100.0f - zoomIn) / 100.0f;
                    var imageAspect = vSize / hSize;
                    // Get the FOV in both axes based on the thumbnail aspect ratio used by VRChat
                    var vFov = _vrcCam.fieldOfView;
                    var hFov = Camera.VerticalToHorizontalFieldOfView(vFov, ThumbnailAspectRatio);
                    // Calculate resulting parenting distances for optimal coverage based on both axes
                    var vDist = vSize / Mathf.Tan(vFov * Mathf.Deg2Rad / 2.0f);
                    var hDist = hSize / Mathf.Tan(hFov * Mathf.Deg2Rad / 2.0f);
                    // Choose smallest as final distance so we cover the cameras viewport completely
                    var parentingDistance = vDist < hDist ? vDist : hDist;
                    // Calculate shift range
                    var vShiftRange = mySize.z - vSize;
                    var hShiftRange = mySize.x - hSize;
                    if (imageAspect > ThumbnailAspectRatio)
                    {
                        // Image too wide, add more shifting range
                        hShiftRange += mySize.x - vSize * imageAspect / ThumbnailAspectRatio;
                    }
                    else
                    {
                        // Image fits or too tall, add more shifting range
                        vShiftRange += mySize.z - hSize * ThumbnailAspectRatio / imageAspect;
                    }

                    // Adjust camera clipping to optimize for rendering
                    _vrcCam.nearClipPlane = 0.95f * parentingDistance;
                    _vrcCam.farClipPlane = 1.05f * parentingDistance;
                    // Apply or update offset-parenting Constraint
                    var parent = _vrcCam.gameObject.GetOrAddComponent<ParentConstraint>();
                    if (parent.sourceCount < 1)
                        parent.AddSource(new ConstraintSource { weight = 1.0f, sourceTransform = this.transform });
                    // Rotate cam upright towards image plane
                    parent.SetRotationOffset(0, new Vector3(90.0f, 0.0f, 180.0f));
                    // Apply distance and optional shift as fraction of the max height
                    var vShift = vShiftRange * shiftVertical / 200.0f;
                    var hShift = hShiftRange * shiftHorizontal / 200.0f;
                    parent.SetTranslationOffset(0, new Vector3(hShift, parentingDistance, vShift));
                    parent.locked = true;
                    parent.constraintActive = true;
                    // Remember parameters used to update if changed (better live preview)
                    _zoomInLast = zoomIn;
                }
                else
                {
                    Debug.LogError($"Cannot Align Upload Cam to Object without a regular plane mesh!",
                        this);
                }
            }
        }
    }
}