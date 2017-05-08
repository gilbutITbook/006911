using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Kudan.AR
{
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Camera))]
	[AddComponentMenu("Kudan AR/Kudan Tracker")]
	public class KudanTracker : MonoBehaviour
	{
        static KudanTracker kudanTracker;

        /// <summary>
        /// Default width component of the camera resolution, in pixels.
        /// </summary>
		private const int DefaultCameraWidth = 640;
		
		/// <summary>
		/// Default height component of the camera resolution, in pixels.
		/// </summary>
		private const int DefaultCameraHeight = 480;

		[Tooltip("The key required to run in the Editor. You can get an Editor key by clicking the 'Get Editor API Key' button below")]
		/// <summary>
		/// The license key used to run the plugin in the Editor. By registering an account on the Kudan website, you can claim one free key for personal use.
		/// NOTE: This key is separate from the API Key checked on iOS and Android builds. Use _APIKey for those platforms.
		/// </summary>
		public string _EditorAPIKey = string.Empty;

		/// <summary>
		/// Reference to the tracker class, which interfaces between Unity and the native frameworks.
		/// </summary>
		protected TrackerBase _trackerPlugin;

		/// <summary>
		/// Array containing all trackables that were detected in the previous frame
		/// </summary>
		protected Trackable[] _lastDetectedTrackables;
		
		[Tooltip("The API License key issued by Kudan. Development keys can be obtained from https://wiki.kudan.eu/Development_License_Keys. Production keys can be bought at https://www.kudan.eu/pricing/")]
		/// <summary>
		/// The API License key used to run the plugin on mobile platforms. Development keys can be obtained from https://wiki.kudan.eu/Development_License_Keys. 
		/// In order to publish an app using the Kudan plugin to the app store, a license is required. Publish keys can be bought at https://www.kudan.eu/pricing/.
		/// </summary>
		public string _APIKey = string.Empty;

		/// <summary>
		/// The default tracking method used by the tracker when the app starts.
		/// </summary>
		public TrackingMethodBase _defaultTrackingMethod;

		/// <summary>
		/// Array of all tracking methods that can be used by the tracker.
		/// </summary>
		public TrackingMethodBase[] _trackingMethods;

		/// <summary>
		/// Setting this to true gets the front-facing camera instead of defaulting to the back-facing camera.
		/// NOTE: This only applies to mobile devices. In the Editor use the WebCamID instead.
		/// </summary>
		public bool _useFrontFacingCameraOnMobile;

		/// <summary>
		/// Whether or not Marker Recovery Mode is enabled.
		/// This feature allows for quick re-detection of a marker after it has been lost.
		/// 
		/// NOTE: The Recovery Mode uses slightly more CPU power while tracking. 
		/// </summary>
		public bool _markerRecoveryMode;

		/// <summary>
		/// Whether or not the markers that are loaded are automatically cropped of the redundant parts of the image e.g. white border around the image in the middle
		/// </summary>
		public bool _markerAutoCrop;

		/// <summary>
		/// Whether the markers should be used with Extensibility turned on. Please note this should only be used with stationary markers. That means that if your marker at all moves, you should not use this mode.
		/// </summary>
		public bool _markerExtensibility;

		[Tooltip("Don't destroy between level loads")]
		/// <summary>
		/// Whether or not to make this tracker persist between scenes.
		/// </summary>
		public bool _makePersistent = true;

		/// <summary>
		/// Whether or not to initialise this tracker when it is loaded.
		/// </summary>
		public bool _startOnEnable = true;

		/// <summary>
		/// Whether or not to apply the projection matrix of the tracker to the camera.
		/// </summary>
		public bool _applyProjection = true;

		[Tooltip("The camera to apply the projection matrix to. If left blank this will use the main camera")]
		/// <summary>
		/// The camera to apply the projection matrix to. If left blank, this will use the main camera.
		/// </summary>
		public Camera _renderingCamera;

		[Tooltip("The renderer to draw the camera texture to")]
		/// <summary>
		/// Reference to the Mesh Renderer component of the object the camera texture is being drawn to.
		/// </summary>
		public Renderer _background;

		/// <summary>
		/// Whether or not to display the debug GUI.
		/// </summary>
		public bool _displayDebugGUI = true;

		[Range(1, 4)]
		/// <summary>
		/// Overall size of the debug GUI on the screen.
		/// </summary>
		public int _debugGUIScale = 1;

		[HideInInspector]
		/// <summary>
		/// The debug shader.
		/// </summary>
		public Shader _debugFlatShader;

		/// <summary>
		/// Gets the interface exposing the Kudan API for those that need scripting control.
		/// </summary>
		public TrackerBase Interface
		{
			get { return _trackerPlugin; }
		}

		/// <summary>
		/// The current tracking method being used by the tracker.
		/// </summary>
		private TrackingMethodBase _currentTrackingMethod;

		/// <summary>
		/// Gets the current tracking method.
		/// </summary>
		public TrackingMethodBase CurrentTrackingMethod
		{
			get { return _currentTrackingMethod; }
		}

        #if UNITY_EDITOR
		/// <summary>
		/// If you have more than one webcam connected to your machine you can change your prefered webcam ID here.
		/// NOTE: This only applies to the Unity Editor. To change the camera used on Mobile devices, change "Use Front Facing Camera On Mobile" instead.
		/// </summary>
		[Range(0, 10)]
		[Tooltip ("If you have more than one webcam you can change your prefered webcam ID here")]
		public int _playModeWebcamID;

		/// <summary>
		/// Checks that the license key required to use the native framework is valid.
		/// </summary>
		private void checkLicenseKeyValidity() 
		{
			bool result = NativeInterface.CheckAPIKeyIsValid(_APIKey.Trim(), PlayerSettings.bundleIdentifier);

			if (result)
            {
				Debug.Log ("[KudanAR] Your Native API Key Is Valid for Bundle ID: " + PlayerSettings.bundleIdentifier);
			}
            else
            {
				Debug.LogError ("[KudanAR] Your Native API Key is INVALID for Bundle ID: "+ PlayerSettings.bundleIdentifier);
			}
		}

		/// <summary>
		/// Checks that the license key required to use the plugin in the Editor is valid.
		/// </summary>
		private void checkEditorLicenseKey() 
		{
			bool result = NativeInterface.SetUnityEditorApiKey (_EditorAPIKey.Trim());

			if (result)
			{
				Debug.Log ("[KudanAR] Editor Play Mode Key is Valid");
			}
			else
			{
				Debug.LogError ("[KudanAR] Editor Play Mode Key is NOT Valid");
			}
		}
        #endif

		/// <summary>
		/// Awake is called once when the script instance is being loaded.
		/// </summary>
		void Awake()
		{
			// If there is no KudanTracker currently in the scene when it loads, make sure that this persists between scenes, then set the static reference of KudanTracker to this object.
			if (kudanTracker == null)
			{
				if (_makePersistent)
				{
					DontDestroyOnLoad(gameObject);
				}
				kudanTracker = this;
			}
			// If KudanTracker already exists in the scene, but this is not it, destroy this gameobject, because there should only be one KudanTracker in a scene at any one time.
			else if (kudanTracker != this)
			{
				Debug.LogError ("There should only be one Kudan Tracker active at once.");
				gameObject.SetActive (false);
				Destroy(gameObject);
			}
		}

		/// <summary>
		/// Start is called only once on the frame the script is enabled.
		/// </summary>
		void Start()
		{
			CreateDebugLineMaterial();

			// Create the platform specific plugin interface
			_trackerPlugin = new Tracker (_background);

			if (_trackerPlugin == null)
			{
				Debug.LogError("[KudanAR] Failed to get tracker plugin");
				this.enabled = false;
				return;
			}
				
			// Initialise plugin
			if (!_trackerPlugin.InitPlugin())
			{
				Debug.LogError("[KudanAR] Error initialising plugin");
				this.enabled = false;
			}
			else
			{
				#if UNITY_EDITOR
				// Check the Editor API Key and validity of the Native API Key
				if (!string.IsNullOrEmpty(_EditorAPIKey))
				{
					checkEditorLicenseKey();
				}
				else
				{
					Debug.LogError("Editor API Key field is Empty");
				}

				if (!string.IsNullOrEmpty(_APIKey))
				{
					checkLicenseKeyValidity();
				}
				else
				{
					Debug.LogWarning("API Key field is Empty");
				}
				#else
				// Set the API key
				if (!string.IsNullOrEmpty(_APIKey))
				{
					_trackerPlugin.SetApiKey (_APIKey, Application.bundleIdentifier);
				}
				else
				{
					Debug.LogError("API Key field is Empty");
				}
				#endif
				
				// Print plugin version
				string version = _trackerPlugin.GetPluginVersion();
				float nativeVersion = _trackerPlugin.GetNativePluginVersion();
				Debug.Log(string.Format("[KudanAR] Initialising Plugin Version {0} (Native Framework Version {1})", version, nativeVersion));

				// Initialise all included tracking methods
				foreach (TrackingMethodBase method in _trackingMethods)
				{
					method.Init();
				}

				_trackerPlugin.SetMarkerRecoveryStatus(_markerRecoveryMode);
				_trackerPlugin.SetMarkerAutoCropStatus (_markerAutoCrop);
				_trackerPlugin.SetMarkerExtensibilityStatus (_markerExtensibility);

				ChangeTrackingMethod(_defaultTrackingMethod);

                if (_trackerPlugin.GetNumCameras() > 0)
                {
                    // Start the camera
                    #if UNITY_EDITOR
                    if (_trackerPlugin.StartInputFromCamera(_playModeWebcamID, DefaultCameraWidth, DefaultCameraHeight))
                    #else
				    int cameraIndex = 0;

					// As rear-facing cameras are always put first in the array on iOS and Android and front-facing cameras at the end, when wanting the front-facing camera, get the camera at the end of the array.
				    if (_useFrontFacingCameraOnMobile)
				    {
					    cameraIndex = _trackerPlugin.GetNumCameras() - 1;
				    }

				    if (_trackerPlugin.StartInputFromCamera(cameraIndex, DefaultCameraWidth, DefaultCameraHeight))
                    #endif
                    {
                        // Start tracking
                        if (_startOnEnable)
                        {
                            _trackerPlugin.StartTracking();
                        }
                    }
                    else
                    {
                        Debug.LogError("[KudanAR] Failed to start camera, is it already in use?");
                    }
                }
                else
                {
                    Debug.LogWarning("No Cameras Detected");
                }
			}
		}

        /// <summary>
		/// Method called when the script becomes enabled and active.
        /// </summary>
        void OnEnable()
		{
			if (_startOnEnable)
			{
				StartTracking();
			}
		}

		/// <summary>
		/// Method called when the script becomes disabled or inactive.
		/// This is also called when the object is destroyed and can be used for any cleanup code.
		/// </summary>
		void OnDisable()
		{
			StopTracking();
		}

		/// <summary>
		/// OnApplicationFocus is called when the application gains or loses focus. 
		/// When the user goes to the home screen or to another app, focus is lost, and this method is called with the argument set to false.
		/// When the user re-opens the app and focus is gained, this method is called with the argument set to true.
		/// 
		/// NOTE: On Android, when the on-screen keyboard is enabled, it causes a OnApplicationFocus( false ) event. 
		/// Additionally, if you press Home at the moment the keyboard is enabled, the OnApplicationFocus() event is not called, but OnApplicationPause() is called instead.
		/// </summary>
		/// 
		/// <param name="focusStatus">True if the app has gained focus, false if it has lost focus.</param>
		void OnApplicationFocus(bool focusStatus)
		{
			if (_trackerPlugin != null)
			{
				_trackerPlugin.OnApplicationFocus(focusStatus);
			}
		}

		/// <summary>
		/// OnApplicationPause is called when the application loses or gains focus.
		/// When the user goes to the home screen or to another app, focus is lost, and this method is called with the argument set to true.
		/// When the user re-opens the app and focus is gained, this method is called with the argument set to false.
		/// 
		/// NOTE: On Android, when the on-screen keyboard is enabled, it causes a OnApplicationFocus( false ) event. 
		/// Additionally, if you press "Home" at the moment the keyboard is enabled, the OnApplicationFocus() event is not called, but OnApplicationPause() is called instead.
		/// </summary>
		/// 
		/// <param name="pauseStatus">True if the app has paused (lost focus), false if it is not paused (gained focus).</param>
		void OnApplicationPause(bool pauseStatus)
		{
			if (_trackerPlugin != null)
			{
				_trackerPlugin.OnApplicationPause(pauseStatus);
			}
		}

		/// <summary>
		/// Adds a single trackable to the tracker from image data, names it with the given string, and applies any settings. This method uses the default values for whether this trackable should use extended tracking or auto-cropping.
		/// </summary>
		/// <returns><c>true</c>, if trackable was successfully added to the tracker, <c>false</c> otherwise.</returns>
		/// <param name="data">The array of image data.</param>
		/// <param name="id">A name applied to the trackable to identify it in the tracker while the app is running.</param>
		public void AddTrackable(byte[] data, string id)
		{
			_trackerPlugin.AddTrackable (data, id, _markerExtensibility, _markerAutoCrop);
		}

		/// <summary>
		/// Adds a single trackable to the tracker from image data, names it with the given string, and sets whether the trackable should utilise extended tracking or auto-cropping. 
		/// </summary>
		/// <returns><c>true</c>, if trackable was successfully added to the tracker, <c>false</c> otherwise.</returns>
		/// <param name="data">The array of image data.</param>
		/// <param name="id">A name applied to the trackable to identify it in the tracker while the app is running.</param>
		/// <param name="extensible">If set to <c>true</c> the loaded trackable will use extended tracking.</param>
		/// <param name="autocrop">If set to <c>true</c> the loaded trackable will use auto-cropping.</param>
		public void AddTrackable(byte[] data, string id, bool extensible, bool autocrop)
		{
			_trackerPlugin.AddTrackable (data, id, extensible, autocrop);
		}

		/// <summary>
		/// Adds a .KARMarker data set to the tracker with a given name.
		/// </summary>
		/// <returns><c>true<c>/c>, if the data set was successfully added to the tracker, <c>false</c> otherwise.</returns>
		/// <param name="pathToFile">The filepath pointing to the data set.</param>
		/// <param name="id">A string used to identify the data set while the app is running.</param>
		public void AddTrackableSet(string pathToFile, string ID)
		{
			_trackerPlugin.AddTrackableSet (pathToFile, ID);
		}

		/// <summary>
		/// Start tracking with this tracker.
		/// </summary>
		public void StartTracking()
		{
			if (_trackerPlugin != null)
			{
				_trackerPlugin.StartTracking();
			}
		}
		
		/// <summary>
		/// Stop tracking with this tracker.
		/// </summary>
		public void StopTracking()
		{
			if (_trackerPlugin != null)
			{
				_trackerPlugin.StopTracking();
			}
			
			// Restore projection matrix
			Camera camera = _renderingCamera;
			if (camera == null) 
			{
				camera = Camera.main;
			}

			if (camera != null)
			{
				camera.ResetProjectionMatrix();
			}
		}
		
		/// <summary>
		/// Changes the current tracking method to the given tracking method.
		/// </summary>
		/// <param name="newTrackingMethod">New tracking method.</param>
		public void ChangeTrackingMethod(TrackingMethodBase newTrackingMethod)
		{
			if (newTrackingMethod != null && _currentTrackingMethod != newTrackingMethod)
			{
				if (_currentTrackingMethod != null)
				{
					_currentTrackingMethod.StopTracking();
				}

				_currentTrackingMethod = newTrackingMethod;
				_currentTrackingMethod.StartTracking();
			}
		}

		/// <summary>
		/// Start ArbiTrack on the current platform.
		/// </summary>
		/// <param name="position">Position to start tracking at.</param>
		/// <param name="orientation">Orientation to start tracking at.</param>
        public void ArbiTrackStart(Vector3 position, Quaternion orientation)
        {
            _trackerPlugin.ArbiTrackStart(position, orientation);
        }

		/// <summary>
		/// Stop ArbiTrack and return to placement mode.
		/// </summary>
		public void ArbiTrackStop()
		{
			_trackerPlugin.ArbiTrackStop ();
		}

		/// <summary>
		/// Checks if arbitrary tracking is currently running.
		/// </summary>
		/// <returns><c>true<c>/c>, if ArbiTrack is running <c>false</c> if not.</returns>
        public bool ArbiTrackIsTracking()
        {
            return _trackerPlugin.ArbiTrackIsTracking();
        }

		/// <summary>
		/// Gets the current position and orientation of the floor, relative to the device.
		/// </summary>
		/// <param name="position">Position of the floor, relative to the camera.</param>
		/// <param name="orientation">Orientation of the floor, relative to the camera.</param>
        public void FloorPlaceGetPose(out Vector3 position, out Quaternion orientation)
        {
            _trackerPlugin.FloorPlaceGetPose(out position, out orientation);
        }

		/// <summary>
		/// Gets the current position and orientation of the markerless driver being tracked.
		/// </summary>
		/// <param name="position">The position of the markerless transform driver.</param>
		/// <param name="orientation">The orientation of the markerless transform driver.</param>
        public void ArbiTrackGetPose(out Vector3 position, out Quaternion orientation)
        {
            _trackerPlugin.ArbiTrackGetPose(out position, out orientation);
        }

		/// <summary>
		/// Method called when this behaviour is scheduled to be destroyed.
		/// OnDestroy will only be called on objects that have previously been active.
		/// </summary>
		void OnDestroy()
		{
			if (_trackerPlugin != null)
			{
				_trackerPlugin.StopInput();
				_trackerPlugin.DeinitPlugin();
				_trackerPlugin = null;
			}

			if (_lineMaterial != null)
			{
				Material.Destroy(_lineMaterial);
				_lineMaterial = null;
			}
		}

		/// <summary>
		/// OnPreRender is called before a camera starts rendering the scene.
		/// This method is only called if the script is enabled and attached to the camera.
		/// </summary>
		void OnPreRender()
		{
			_trackerPlugin.updateCam ();
		}

		/// <summary>
		/// OnPostRender is called after a camera has finished rendering the scene.
		/// This method is only called if the script is enabled and attached to the camera.
		/// </summary>
		void OnPostRender()
		{
			#if UNITY_EDITOR
			#elif UNITY_ANDROID
			if (_trackerPlugin != null)
			{
				_trackerPlugin.PostRender();
			}
			#endif

			if (_displayDebugGUI)
			{
				RenderAxes();
			}
		}

		/// <summary>
		/// Update is called once each frame, if the MonoBehaviour is enabled.
		/// </summary>
		void Update()
		{
			if (_trackerPlugin != null)
			{
				Camera renderingCamera = _renderingCamera;

                if (renderingCamera == null)
                {
                    renderingCamera = Camera.main;
                }

				_trackerPlugin.SetupRenderingCamera(renderingCamera.nearClipPlane, renderingCamera.farClipPlane);
				
				// Update tracking
				_trackerPlugin.UpdateTracking();

                // Apply projection matrix
                if (_applyProjection)
                {
                    renderingCamera.projectionMatrix = _trackerPlugin.GetProjectionMatrix();
                }
                else
                {
                    renderingCamera.ResetProjectionMatrix();
                }

				// Take a copy of the detected trackables
				ProcessNewTrackables();

				_currentTrackingMethod.ProcessFrame();

				// Apply texture to background renderer
				Texture texture = _trackerPlugin.GetTrackingTexture();

				if (_background != null && texture != null)
				{
					_background.material.mainTexture = texture;
				}
			}
		}
			
		/// <summary>
		/// Updates the array of trackables currently being detected.
		/// </summary>
		private void ProcessNewTrackables()
		{
			_lastDetectedTrackables = _trackerPlugin.GetDetectedTrackablesAsArray();
		}

		/// <summary>
		/// Determines whether or not the tracker is currently detecting a trackable.
		/// </summary>
		/// <returns><c>true</c> if the tracker tracked at least one trackable this frame, <c>false</c> otherwise.</returns>
		public bool HasActiveTrackingData()
		{
			return (_trackerPlugin != null && _trackerPlugin.IsTrackingRunning() && _lastDetectedTrackables != null && _lastDetectedTrackables.Length > 0);
		}

		/// <summary>
		/// Sets the position of the floor plane that ArbiTrack uses as a reference for tracking.
		/// </summary>
		/// <param name="floorHeight">How far from the camera the floor is positioned.</param>
		public void SetArbiTrackFloorHeight(float floorHeight)
		{
			_trackerPlugin.SetArbiTrackFloorHeight (floorHeight);
		}

		/// <summary>
		/// Draws wireframe shapes in the editor for debugging purposes.
		/// </summary>
		void OnDrawGizmos()
		{
			// Draw useful debug rendering in Editor
			if (HasActiveTrackingData())
			{
				foreach (Trackable t in _lastDetectedTrackables)
				{
					// Draw circle
					Gizmos.color = Color.cyan;
					Gizmos.DrawSphere(t.position, 10f);

					// Draw line from origin to point (useful if object is offscreen)
					Gizmos.color = Color.cyan;
					Gizmos.DrawLine(Vector3.zero, t.position);
					
					// Draw axes
					Matrix4x4 xform = Matrix4x4.TRS(t.position, t.orientation, Vector3.one * 250f);
					Gizmos.matrix = xform;

					Gizmos.color = Color.red;
					Gizmos.DrawLine(Vector3.zero, Vector3.right);

					Gizmos.color = Color.green;
					Gizmos.DrawLine(Vector3.zero, Vector3.up);

					Gizmos.color = Color.blue;
					Gizmos.DrawLine(Vector3.zero, Vector3.forward);
				}
			}
		}

		/// <summary>
		/// Starts line rendering.
		/// </summary>
		/// <returns><c>true</c>, if line rendering was started, <c>false</c> otherwise.</returns>
		public bool StartLineRendering()
		{
			bool result = false;
			if (_lineMaterial != null)
			{
				_lineMaterial.SetPass(0);
				result = true;
			}
			return result;
		}

		/// <summary>
		/// Renders axes for debugging.
		/// </summary>
		private void RenderAxes()
		{
			if (HasActiveTrackingData() && StartLineRendering())
			{			
				foreach (Trackable t in _lastDetectedTrackables)
				{
					Matrix4x4 xform = Matrix4x4.TRS(t.position, t.orientation, Vector3.one * 250f);

					GL.PushMatrix();

					Matrix4x4 m = GL.GetGPUProjectionMatrix(_trackerPlugin.GetProjectionMatrix(), false);
					m = _trackerPlugin.GetProjectionMatrix();
					GL.LoadProjectionMatrix(m);

					// Draw line from origin to point (useful if object is offscreen)
					GL.Color(Color.cyan);
					GL.Vertex(Vector3.zero);
					GL.Vertex(t.position);		

					GL.Begin(GL.LINES);
					GL.MultMatrix(xform);
					GL.Color(Color.red);
					GL.Vertex(Vector3.zero);
					GL.Vertex(Vector3.right);

					GL.Color(Color.green);
					GL.Vertex(Vector3.zero);
					GL.Vertex(Vector3.up);

					GL.Color(Color.blue);
					GL.Vertex(Vector3.zero);
					GL.Vertex(Vector3.forward);

					GL.End();
					GL.PopMatrix();
				}
			}
		}
		
		/// <summary>
		/// Method used to draw the debug GUI in the upper-left portion of the screen.
		/// </summary>
		void OnGUI()
		{
			// Display debug GUI with tracking information
			if (_displayDebugGUI)
			{
				GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(_debugGUIScale, _debugGUIScale, 1f));
				GUILayout.BeginVertical("box");

				#if UNITY_EDITOR
				GUILayout.Label("KUDAN AR", UnityEditor.EditorStyles.boldLabel);
				#else
				GUILayout.Label("KUDAN AR");
				#endif

				// Tracking status
				if (_trackerPlugin != null && _trackerPlugin.IsTrackingRunning())
				{
					GUI.color = Color.green;
					GUILayout.Label("Tracker is running");
				}
				else
				{
					GUI.color = Color.red;
					GUILayout.Label("Tracker NOT running");
				}
				GUI.color = Color.white;

				// Screen resolution
				GUILayout.Label("Screen: " + Screen.width + "x" + Screen.height);

				// Frame rates
				if (_trackerPlugin != null)
				{
					GUILayout.Label("Camera rate:  " + _trackerPlugin.CameraFrameRate.ToString("F2") + "hz");
					GUILayout.Label("Tracker rate: " + _trackerPlugin.TrackerFrameRate.ToString("F2") + "hz");
					GUILayout.Label("App rate: " + _trackerPlugin.AppFrameRate.ToString("F2") + "hz");
				}

				if (_trackerPlugin != null && _trackerPlugin.IsTrackingRunning())
				{
					// Texture image and resolution
					if (_currentTrackingMethod != null)
					{
						GUILayout.Label("Method: " + _currentTrackingMethod.Name);
						_currentTrackingMethod.DebugGUI(_debugGUIScale);
					}
				}
			}
		}

		/// <summary>
		/// The line material.
		/// </summary>
		private Material _lineMaterial;

		/// <summary>
		/// Creates the debug line material.
		/// </summary>
		private void CreateDebugLineMaterial()
		{
			if (!_lineMaterial && _debugFlatShader != null)
			{
				_lineMaterial = new Material(_debugFlatShader);
				_lineMaterial.hideFlags = HideFlags.HideAndDontSave;
			}
		}

		#region Screenshot methods
		/// <summary>
		/// Takes a screenshot of the camera feed and any projected objects, without any UI.
		/// </summary>
		public void takeScreenshot()
		{
			StartCoroutine (Screenshot ());
		}
		
		IEnumerator Screenshot()
		{
			List<GameObject> uiObjects = FindGameObjectsInUILayer ();

			for (int i = 0; i < uiObjects.Count; i++)
			{
				uiObjects [i].SetActive (false);
			}

			bool wasDebug = false;
			if (_displayDebugGUI) 
			{
				wasDebug = true;
				_displayDebugGUI = false;
			}

			yield return new WaitForEndOfFrame ();

			RenderTexture RT = new RenderTexture (Screen.width, Screen.height, 24);

			GetComponent<Camera> ().targetTexture = RT;

			Texture2D screen = new Texture2D (RT.width, RT.height, TextureFormat.RGB24, false);
			screen.ReadPixels (new Rect (0, 0, RT.width, RT.height), 0, 0);

			byte[] bytes = screen.EncodeToJPG ();

			string filePath = Application.dataPath + "/Screenshot - " + Time.unscaledTime + ".jpg";
			System.IO.File.WriteAllBytes (filePath, bytes);

			Debug.Log ("Saved screenshot at: " + filePath);

			for (int i = 0; i < uiObjects.Count; i++) 
			{
				uiObjects [i].SetActive (true);
			}

			if (wasDebug) 
			{
				_displayDebugGUI = true;
			}

			GetComponent<Camera> ().targetTexture = null;
			Destroy(RT);
		}

		List<GameObject> FindGameObjectsInUILayer()
		{
			GameObject[] goArray = FindObjectsOfType<GameObject>();

			List<GameObject> uiList = new List<GameObject> ();

			for (var i = 0; i < goArray.Length; i++) 
			{
				if (goArray[i].layer == 5)
				{
					uiList.Add(goArray[i]);
				}
			}

			if (uiList.Count == 0) 
			{
				return null;
			}

			return uiList;
		}
		#endregion
	}
};
