using UnityEngine;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Kudan.AR
{
	public class Tracker : TrackerBase 
	{
		#region Variables
		#if UNITY_EDITOR
		private int _width = 0; ///< Width component of the camera resolution, in pixels.
		private int _height = 0; ///< Height component of the camera resolution, in pixels.
		private List<Trackable> _currentDetected = new List<Trackable> (8); ///< List of all trackables detected in the current frame.

		#elif UNITY_IOS
		private Renderer _background; ///< reference to the Mesh Renderer of the background render object. This is how we access the camera texture.
		private MeshFilter _cameraBackgroundMeshFilter; ///< The Mesh filter component attached to the background render object. This is used to adjust the camera texture depending on the orientation of the device.

		private Texture2D _textureYp; ///< The Yp component of the texture.
		private int _textureYpID; ///< The ID referencing the Yp component.

		private Texture2D _textureCbCr; ///< The Cb and Cr components of the texture.
		private int _textureCbCrID; ///< The ID referencing the Cb and Cr components.

		private float _cameraAspect = 1.0f; ///< The aspect ratio of the camera.

		private ScreenOrientation _prevScreenOrientation; ///< The last orientation the device before the current one. If the current orientation is equal to this, we do not need to update the rotation of the camera texture.
		private Matrix4x4 _projectionRotation = Matrix4x4.identity; ///< The rotation portion of the projection transformation matrix.

		private const int kKudanARRenderEventId = 0x0a736f21;

		#elif UNITY_ANDROID
		private AndroidJavaObject	m_KudanAR_Instance	= null; ///< The single instance of the KudanAR Android framework.
		private AndroidJavaObject	m_ActivityContext	= null; ///< Reference to the Unity player instance.

		private int					m_DeviceIndex		= -1; ///< The index of the camera device to start input from. Rear-facing cameras come first, followed by front-facing cameras.
		private int					m_TextureHandle		= 0; ///< Texture Handle referencing the background camera texture.
		private int					m_Width				= 0; ///< The width component of the resolution, in pixels.
		private int					m_Height			= 0; ///< The height component of the resolution, in pixels.

		private Texture2D			m_InputTexture		= null; ///< The image that input starts from when using StartInputFromImage.
		  
		private bool				m_WasTrackingWhenApplicationPaused = false; ///< Whether or not the tracker was running when the app lost focus.
		private bool				m_ApplicationPaused = false; ///< Whether or not the app has fully lost focus, meaning that the app has actually gone back to the home screen.

		private MeshFilter 			_cameraBackgroundMeshFilter; ///< The Mesh filter component attached to the background render object. This is used to adjust the camera texture depending on the orientation of the device.

		private ScreenOrientation 	_prevScreenOrientation; ///< The last orientation the device was in before the current one. Should always be different to the current orientation if the orientation has changed at least once.
		private Matrix4x4 			_projectionRotation = Matrix4x4.identity; ///< The rotation portion of the projection transformation matrix.

		#endif
		private int _numFramesGrabbed; ///< The total number of frames since the last rate update.
		private int _numFramesTracked; ///< The number of frames that have been tracked since the last rate update. This should always be the same as the number of grabbed frames.
		private int _numFramesProcessed; ///< The number of frames that have been process by the tracker since the last rate update. This should always be the same as the number of tracked frames.
		private int _numFramesRendered; ///< The number of frames that the Unity Player has rendered since the last rate update. This should always be the same as the number of grabbed frames.
		private float _rateTimer; ///< The amount of time it has been since the last rate update.

		private bool receivingInput; ///< Whether or not input was successfully started, either from a camera or from an image.
		#endregion

		#region iOS_Imports
		#if UNITY_IOS
		[DllImport("__Internal")]
		private static extern System.IntPtr GetRenderEventFunc();

		[DllImport("__Internal")]
		private static extern void SetApiKeyNative(string key, string bundleId);

		[DllImport("__Internal")]
		private static extern System.IntPtr GetTextureForPlane(int plane, ref int width, ref int height);

		[DllImport("__Internal")]
		private static extern int GetCaptureDeviceCount();

		[DllImport("__Internal")]
		private static extern bool BeginCaptureSession(int deviceIndex, int targetWidth, int targetHeight);

		[DllImport("__Internal")]
		private static extern void EndCaptureSession();

		[DllImport("__Internal")]
		private static extern void BeginTracking();

		[DllImport("__Internal")]
		private static extern void EndTracking();

		[DllImport("__Internal")]
		private static extern float GetCaptureDeviceRate();

		[DllImport("__Internal")]
		private static extern float GetTrackerRate();

		[DllImport("__Internal")]
		private static extern void GetDetectedTrackable(int index, StringBuilder name, int nameSize, ref int width, ref int height, float[] pos, float[] ori);
		#endif
		#endregion

		/// <summary>
		/// Constructor for the Tracker class. On mobile devices, it needs a reference to the background in order to render to the target texture and display the camera feed.
		/// </summary>
		/// <param name="background">The Renderer component attached to the background GameObject.</param>
		public Tracker(Renderer background)
		{
			#if UNITY_EDITOR
			#else
			_cameraBackgroundMeshFilter = background.GetComponent<MeshFilter> ();

			#if UNITY_IOS
			_background = background;
			SetYpCbCrMaterialOnBackground();
			#endif

			#endif
		}

		/// <summary>
		/// Initialise the plugin.
		/// </summary>
		/// <returns><c>true</c>, if the plugin initialised successfully, <c>false</c> otherwise.</returns>
		public override bool InitPlugin()
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.Init ();

			#elif UNITY_ANDROID
			bool bInited = false;

			if (m_ActivityContext == null)
			{
				AndroidJavaClass activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
				if (activityClass != null)
				{
					m_ActivityContext = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
					AndroidJavaClass kudanArClass = new AndroidJavaClass("eu.kudan.androidar.KudanAR");
					if (kudanArClass != null )
					{
						m_KudanAR_Instance = kudanArClass.CallStatic<AndroidJavaObject>( "getInstance" );
						if ( m_KudanAR_Instance != null )
						{
							// Initialise java bits (camera, openGL render to target bits, etc)
							// Does the initialisation of the Kudan library as well
							bInited = m_KudanAR_Instance.Call<bool>( "Initialise", m_ActivityContext );
						}
					}
				}
			}

			return bInited;
			
			#endif
		}

		/// <summary>
		/// Deinitialise the plugin.
		/// </summary>
		public override void DeinitPlugin()
		{
			#if UNITY_EDITOR || UNITY_IOS
			NativeInterface.Deinit();

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				// De-initialise everything on the java side
				// Also calls Deinit on the Kudan library
				m_KudanAR_Instance.Call( "Deinitialise" );
			}

			#endif
		}

		/// <summary>
		/// Get the native plugin version number. This is the version number of the native framework used for the current platform, not the version of the plugin itself.
		/// </summary>
		/// <returns>The native plugin version number.</returns>
		public override float GetNativePluginVersion()
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.GetPluginVersion();

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				// Get Version from native side
				return m_KudanAR_Instance.Call<float>( "GetPluginVersion" );
			}
			
			else
			{
				return 0.0f;
			}
			#endif
		}

		/// <summary>
		/// Sets the API key to be checked by licensing later.
		/// </summary>
		/// <param name="key">The full API Key, including hypens. The string must be an exact match to what is provided by Kudan.</param>
		/// <param name="bundleId">The Bundle ID of your project. This is ignored. </param>
		public override void SetApiKey (string key, string bundleId)
		{
			#if UNITY_EDITOR
			Debug.LogWarning("The Editor uses the Editor Key, not the Native API Key");

			#elif UNITY_IOS
			SetApiKeyNative(key, bundleId);

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				byte[] aKey = Encoding.UTF8.GetBytes( key );
				m_KudanAR_Instance.Call( "SetApiKey", aKey );
			}
			#endif
		}

		/// <summary>
		/// Gets the total number of cameras connected to the device, including any built-in and any external webcams.
		/// </summary>
		/// <returns>The total number of cameras connected to the device. On most smart devices, this number will be 2.</returns>
		public override int GetNumCameras()
		{
			#if UNITY_EDITOR
			return WebCamTexture.devices.Length;

			#elif UNITY_IOS
			return GetCaptureDeviceCount();
			
			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				return m_KudanAR_Instance.Call<int>( "GetNumberOfCameras" );
			}
			else
			{
				return 0;
			}
			#endif
		}

		/// <summary>
		/// Start input from a static image. This is useful for debugging purposes.
		/// </summary>
		/// <returns><c>true</c>, if input was successfully started from the image, <c>false</c> otherwise.</returns>
		/// <param name="image">The image that will be put into the tracker. If it contains a marker, the marker will be detected.</param>
		public override bool StartInputFromImage(Texture2D image)
		{
			bool allGood = false;

			#if UNITY_EDITOR || UNITY_ANDROID
			// First stop existing input
			bool wasTracking = _isTrackingRunning;
			StopInput ();
			#endif

			#if UNITY_EDITOR

			#elif UNITY_ANDROID
			// Start new input
			m_InputTexture = image;

			m_Width = m_InputTexture.width;
			m_Height = m_InputTexture.height;

			_finalTexture = m_InputTexture;
			m_TextureHandle = 0;

			m_DeviceIndex = -1;
			#endif

			#if UNITY_EDITOR || UNITY_ANDROID
			// Start new input
			if (wasTracking) 
			{
				StartTracking ();
				allGood = true;
			}
            #endif

            receivingInput = true;
			return allGood;
		}

		/// <summary>
		/// Start input from a given camera device.
		/// </summary>
		/// <returns><c>true</c>, if input was successfully started from a camera device, <c>false</c> otherwise.</returns>
		/// <param name="deviceIndex">The index of the camera to get input from. Back-facing devices always come first in the list. 
		/// On a smart device with a back-facing and front-facing camera, the back-facing camera will have index 0, and the front-facing camera will have index 1. 
		/// On a desktop device, the index corresponds to the connected USB webcams, if there are any.</param>
		/// <param name="targetWidth">Width component of the target resolution, in pixels.</param>
		/// <param name="targetHeight">Height component of the target resolution, in pixels.</param>
		public override bool StartInputFromCamera(int deviceIndex, int targetWidth, int targetHeight)
		{
			bool allGood = false;

			#if UNITY_EDITOR || UNITY_ANDROID
			bool wasTracking = _isTrackingRunning;
			int[] resolution = new int[2];

			// Stop existing input
			StopInput ();
			#endif

			#if UNITY_EDITOR
			// Initialise the webcam. Change the argument to specify your webcam ID. Unfortunately this isn't very predictable
			// on OSX so trial and error is required.
			if (NativeInterface.WebCamInit (deviceIndex))
			{
				// Get webcam texture resolution.
				NativeInterface.WebCamGetResolution (resolution);

				_width = resolution [0];
				_height = resolution [1];

				// Create a new texture to hold it.
				_clonedTexture = new Texture2D (_width, _height, TextureFormat.RGBA32, false);

				if (wasTracking)
				{
					StartTracking ();
				}

				allGood = true;
			}
			else
			{
				Debug.Log ("Couldn't open webcam");
			}

            #elif UNITY_ANDROID
			if (deviceIndex < GetNumCameras())
			{
				if ( m_KudanAR_Instance != null )
				{
					m_DeviceIndex = deviceIndex;

					m_KudanAR_Instance.Call( "startCamera", deviceIndex, targetWidth, targetHeight );

					resolution = m_KudanAR_Instance.Call<int[]>( "getCameraResolution" );

					if ( m_Width != resolution[ 0 ] || m_Height != resolution[ 1 ] )
					{
						_finalTexture = null;
						m_TextureHandle = 0;

						m_Width = resolution[ 0 ];
						m_Height = resolution[ 1 ];
					}

					Debug.Log( "[KudanAR] Camera resolution: " + m_Width + " x " + m_Height );

					if (wasTracking)
					{
						StartTracking();
					}

					allGood = true;
				}
			}

            #elif UNITY_IOS
			if (BeginCaptureSession(deviceIndex, targetWidth, targetHeight))
			{
				StartTracking();
				allGood = true;
			}
            #endif
            receivingInput = true;
			return allGood;
		}

		/// <summary>
		/// Stop all input. If tracking is running, this will also stop tracking. To restart input, StartInputFromCamera will need to be used again, followed by StartTracking.
		/// </summary>
		public override void StopInput()
		{
			#if UNITY_EDITOR || UNITY_ANDROID
			if (_isTrackingRunning)
			{
				StopTracking ();
			}
			#endif

			#if UNITY_EDITOR
			NativeInterface.WebCamDeinit ();

            #elif UNITY_IOS
			EndCaptureSession();

            #elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				m_KudanAR_Instance.Call( "stopCamera" );
			}
			_finalTexture = null;
			m_TextureHandle = 0;
            #endif

            receivingInput = false;
		}

		/// <summary>
		/// Adds a single trackable to the tracker from image data, names it with the given string, and applies any settings. 
		/// </summary>
		/// <returns><c>true</c>, if trackable was successfully added to the tracker, <c>false</c> otherwise.</returns>
		/// <param name="data">The array of image data.</param>
		/// <param name="id">A name applied to the trackable to identify it in the tracker while the app is running.</param>
		/// <param name="extensible">If set to <c>true</c> the loaded trackable will use extended tracking.</param>
		/// <param name="autocrop">If set to <c>true</c> the loaded trackable will use auto-cropping.</param>
		public override bool AddTrackable(byte[] data, string id, bool extensible, bool autocrop)
		{
			bool result = false;

			#if UNITY_EDITOR || UNITY_IOS
			//result = NativeInterface.AddTrackable (data, id);

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null) 
			{
				result = m_KudanAR_Instance.Call<bool> ("AddTrackable", data, id);
			}
			#endif

			if (result)
			{
				Trackable trackable = new Trackable ();
				Texture2D image = new Texture2D (2, 2);
				image.LoadImage(data);

				trackable.name = id;
				trackable.width = image.width;
				trackable.height = image.height;

				_trackables.Add (trackable);
			} 

			else 
			{
				Debug.LogError ("Trackable was not added to Tracker");
			}

			return result;
		}

		/// <summary>
		/// Adds a .KARMarker data set to the tracker with a given name.
		/// </summary>
		/// <returns><c>true<c>/c>, if the data set was successfully added to the tracker, <c>false</c> otherwise.</returns>
		/// <param name="data">The array of byte data from the data set.</param>
		/// <param name="id">A string used to identify the data set while the app is running.</param>
		public override bool AddTrackableSet(byte[] data, string id)
		{
			bool result = false;

			#if UNITY_EDITOR || UNITY_IOS
			GCHandle handle = GCHandle.Alloc (data, GCHandleType.Pinned);
			result = NativeInterface.AddTrackableSet (handle.AddrOfPinnedObject (), data.Length);
			handle.Free ();

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null )
			{
				result = m_KudanAR_Instance.Call<bool>( "AddTrackableSet", data, data.Length );
			}
			#endif

			if (result) 
			{
				Trackable trackable = new Trackable ();
				trackable.name = id;
				_trackables.Add (trackable);
			}

			return result;
		}
   
		/// <summary>
		/// Update tracking for the current platform.
		/// </summary>
		public override void UpdateTracking ()
		{
            if (receivingInput)
            {
                #if UNITY_EDITOR
                updateEditorTracking();
                #elif UNITY_IOS
			    updateiOSTracking();
                #elif UNITY_ANDROID
			    updateAndroidTracking();
                #endif
            }
		}

		#region UpdateTracking Methods
		#if UNITY_EDITOR
		/// <summary>
		/// Update Tracking for the Editor platforms.
		/// </summary>
		void updateEditorTracking()
		{
			_numFramesGrabbed++;

			if (_numFramesGrabbed > _numFramesTracked) 
			{
				System.IntPtr intPtr = new System.IntPtr ();
				NativeInterface.ProcessFrame (intPtr, _width, _height, 0);

				// Process the detected markers
				_currentDetected = GetDetected ();

				_numFramesTracked++;
			}

			// Only process the markers if a new tracking has completed
			if (_numFramesProcessed != _numFramesTracked) 
			{
				_numFramesProcessed = _numFramesTracked;

				// Copy the list of detected objects, or make a new list of it's empty
				if (_currentDetected != null) 
				{
					_detected = _currentDetected;
				} 

				else 
				{
					_detected = new List<Trackable> (8);
					_currentDetected = new List<Trackable> (8);
				}

				// Update projection matrix
				float[] projectionFloats = new float[16];

				NativeInterface.GetProjectionMatrix (_cameraNearPlane, _cameraFarPlane, projectionFloats);

				_projectionMatrix = ConvertNativeFloatsToMatrix (projectionFloats, (float)_width / (float)_height);
			}

			// Update our frame rates
			UpdateFrameRates ();
		}

		#elif UNITY_IOS
		/// <summary>
		/// Update Tracking for the iOS platform.
		/// </summary>
		void updateiOSTracking()
		{
			#if UNITY_5_2 || UNITY_5_3_OR_NEWER
			GL.IssuePluginEvent(GetRenderEventFunc(), kKudanARRenderEventId);
			#else
			GL.IssuePluginEvent(kKudanARRenderEventId);
			#endif
			UpdateBackground();

			UpdateFrameRates();

			// Grab the detected trackables.
			_detected = GetDetected();

			// Grab the projection matrix.
			float[] projectionFloats = new float[16];
			NativeInterface.GetProjectionMatrix (_cameraNearPlane, _cameraFarPlane, projectionFloats);
			_projectionMatrix = ConvertNativeFloatsToMatrix (projectionFloats, _cameraAspect);

			UpdateRotation();

			// Transform the projection matrix depending on orientation.
			_projectionMatrix = _projectionMatrix * _projectionRotation;
		}
		
		#elif UNITY_ANDROID
		/// <summary>
		/// Update Tracking for the Android platform.
		/// </summary>
		void updateAndroidTracking()
		{
			UpdateRotation ();

			if ( m_KudanAR_Instance != null )
			{
				// Update projection matrix
				float[] projectionFloats = new float[ 16 ];
				projectionFloats = m_KudanAR_Instance.Call<float[]>( "GetProjectionMatrix", _cameraNearPlane, _cameraFarPlane );

				float fCameraAspectRatio = (float)( m_Width ) / (float)( m_Height );
				_projectionMatrix = ConvertNativeFloatsToMatrix( projectionFloats, fCameraAspectRatio );
				_projectionMatrix = _projectionMatrix * _projectionRotation;

				if ( _isTrackingRunning )
				{
			        // Create a texture if required
			        if ( _finalTexture == null )
			        {
				        int iTextureHandle = m_KudanAR_Instance.Call<int>( "getTextureHandle" );
				        if ( m_TextureHandle != iTextureHandle )
				        {
					        m_TextureHandle = iTextureHandle;
					        _finalTexture = Texture2D.CreateExternalTexture( m_Width, m_Height, TextureFormat.RGBA32, false, false, new System.IntPtr( m_TextureHandle ) );
					        Debug.Log("[KudanAR] m_TextureHandle: " + m_TextureHandle );
				        }
			        }

                    // Update the Java side of things
			        m_KudanAR_Instance.Call( "update" );

                    _detected = GetDetected();
		        }
			}

			// Update our frame rates
			UpdateFrameRates();
		}
		#endif
		#endregion

		/// <summary>
		/// Start the tracker. If tracker is already running, a warning message will be logged instead.
		/// </summary>
		public override void StartTracking()
		{
			if (_isTrackingRunning)
			{
				Debug.LogWarning("[KudanAR] Trying to start tracking when it's already running");
				return;
			}

			_isTrackingRunning = true;

			#if UNITY_EDITOR

			#elif UNITY_IOS
			BeginTracking();

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				m_KudanAR_Instance.Call( "SetTrackingEnabled", _isTrackingRunning );
			}
			#endif
		}

		/// <summary>
		/// Stop the tracker. If tracking is already stopped, a warning message will be logged instead.
		/// </summary>
		public override void StopTracking()
		{
			if (!_isTrackingRunning)
			{
				Debug.LogWarning("[KudanAR] Tracking is already stopped");
				return;
			}

			_isTrackingRunning = false;

			#if UNITY_EDITOR

			#elif UNITY_IOS
			EndTracking ();

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				m_KudanAR_Instance.Call( "SetTrackingEnabled", _isTrackingRunning );
			}
			#endif
		}

		/// <summary>
		/// Enable the given tracking method.
		/// </summary>
		/// <returns><c>true<c>/c>, if the tracking method was enabled successfully, <c>false</c> otherwise.</returns>
		/// <param name="trackingMethodId">ID of the tracking method to enable. Tracking methods are: 0 = Marker, 1 = Markerless. </param>
		public override bool EnableTrackingMethod(int trackingMethodId)
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.EnableTrackingMethod(trackingMethodId);

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				return m_KudanAR_Instance.Call<bool>( "EnableTrackingMethod", trackingMethodId );
			}
			else
			{
				return false;
			}
			#endif
		}

		/// <summary>
		/// Disable the given tracking method.
		/// </summary>
		/// <returns><c>true<c>/c>, if the tracking method was disabled successfully, <c>false</c> otherwise.</returns>
		/// <param name="trackingMethodId">ID of the tracking method to disable. Tracking methods are: 0 = Marker, 1 = Markerless. </param>
		public override bool DisableTrackingMethod(int trackingMethodId)
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.DisableTrackingMethod(trackingMethodId);

			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				return m_KudanAR_Instance.Call<bool>( "DisableTrackingMethod", trackingMethodId );
			}
			else
			{
				return false;
			}
			#endif
		}

		/// <summary>
		/// Returns whether or not Marker Recovery is enabled on the tracker.
		/// </summary>
		/// <returns><c>true<c>/c>, if Marker Recovery is enabled. <c>false</c> if not.</returns>
		public override bool GetMarkerRecoveryStatus() 
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.GetMarkerRecoveryStatus();

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null) 
			{
				return m_KudanAR_Instance.Call<bool> ("GetMarkerRecoveryStatus");
			}
			else
			{
				return false;
			}
			#endif
		}

		/// <summary>
		/// Sets the marker recovery status.
		/// Enabling this feature allows for quicker re-detection if a marker is lost as well as making it easier to re-detect
		/// the marker from shallower angles and greater distances.
		/// This is a feature that we recommend everyone should generally enable.
		/// N.B. Enabling this feature will use a fraction more CPU power.
		/// </summary>
		/// <param name="status">Set to true if Marker Recovery should be enabled, false if not.</param>
		public override void SetMarkerRecoveryStatus (bool status)
		{
			#if UNITY_EDITOR || UNITY_IOS
			NativeInterface.SetMarkerRecoveryStatus(status);

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				m_KudanAR_Instance.Call ( "SetMarkerRecoveryStatus", status);
			}
			#endif
		}

		public override void SetMarkerAutoCropStatus (bool status)
		{
			#if UNITY_EDITOR || UNITY_IOS
			NativeInterface.SetAutoCropMarkers(status);
			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				m_KudanAR_Instance.Call ( "SetAutoCropMarkers", status);
			}
			#endif
		}

		public override bool GetMarkerAutoCropStatus()
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.GetAutoCropMarkerStatus();
			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				return m_KudanAR_Instance.Call<bool> ( "GetAutoCropMarkerStatus" );
			}
			else 
			{
				return false;
			}
			#endif
		}

		public override void SetMarkerExtensibilityStatus(bool status)
		{
			#if UNITY_EDITOR || UNITY_IOS
			NativeInterface.SetExtensibleMarkers(status);
			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				m_KudanAR_Instance.Call ( "SetExtensibleMarkers", status);
			}
			#endif
		}

		public override bool GetMarkerExtensibilityStatus ()
		{
			#if UNITY_EDITOR || UNITY_IOS
			return NativeInterface.GetExtensibleMarkersStatus();
			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				return m_KudanAR_Instance.Call<bool> ( "GetExtensibleMarkersStatus");
			}
			else 
			{
				return false;
			}
			#endif
		}


		/// <summary>
		/// OnApplicationFocus is called when the application gains or loses focus.
		/// When the user goes back to the home screen or to another app, focus is lost, and this method is called with the argument set to false.
		/// When the user re-opens the app and focus is gained, this method is called with the argument set to true.
		/// 
		/// NOTE: On Android, when the on-screen keyboard is enabled, it causes a OnApplicationFocus( false ) event. 
		/// Additionally, if you press Home at the moment the keyboard is enabled, the OnApplicationFocus() event is not called, but OnApplicationPause() is called instead.
		/// </summary>
		/// 
		/// <param name="focusStatus">True if the app has gained focus, false if it has lost focus.</param>
		public override void OnApplicationFocus (bool focusStatus)
		{
			#if UNITY_EDITOR || UNITY_IOS

			#elif UNITY_ANDROID
			if ( focusStatus && m_ApplicationPaused)
			{
				if ( m_DeviceIndex > -1 && m_Width > 0 && m_Height > 0 )
				{
					StartInputFromCamera( m_DeviceIndex, m_Width, m_Height );
				}

				if ( m_WasTrackingWhenApplicationPaused )
				{
					StartTracking();

					m_WasTrackingWhenApplicationPaused = false;
				}

				m_ApplicationPaused = false;
			}
			#endif
		}

		/// <summary>
		/// Method called when the app loses focus. Only needed on Android.
		/// </summary>
		/// <param name="pauseStatus">True if GameObjects have focus, False otherwise.</param>
		public override void OnApplicationPause (bool pauseStatus)
		{
			#if UNITY_EDITOR || UNITY_IOS

			#elif UNITY_ANDROID
			if (pauseStatus) 
			{
				// First stop existing input
				m_WasTrackingWhenApplicationPaused = _isTrackingRunning;
				StopInput ();

				m_ApplicationPaused = true;
			}

			else 
			{
				OnApplicationFocus (true);
			}
			#endif
		}

		/// <summary>
		/// Start ArbiTrack on the current platform.
		/// </summary>
		/// <param name="position">Position to start tracking at.</param>
		/// <param name="orientation">Orientation to start tracking at.</param>
		public override void ArbiTrackStart (Vector3 position, Quaternion orientation)
		{
			#if UNITY_EDITOR
			Debug.LogWarning("ArbiTrack is not supported on this platform");

			#elif UNITY_IOS
			float[] f = new float[7];

			f[0] = position.x;
			f[1] = position.y;
			f[2] = position.z;

			f[3] = orientation.x;
			f[4] = orientation.y;
			f[5] = orientation.z;
			f[6] = orientation.w;

			NativeInterface.ArbiTrackStart(f);

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				m_KudanAR_Instance.Call("ArbiTrackStart", position.x, position.y, position.z, orientation.x, orientation.y, orientation.z, orientation.w);
			}
			#endif
		}

		/// <summary>
		/// Stop ArbiTrack and return to placement mode.
		/// </summary>
		public override void ArbiTrackStop ()
		{
			#if UNITY_EDITOR
			Debug.LogWarning("ArbiTrack is not supported on this platform");

			#elif UNITY_IOS
			NativeInterface.ArbiTrackStop ();

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null) 
			{
				m_KudanAR_Instance.Call ("ArbiTrackStop");
			}
			#endif
		}

		/// <summary>
		/// Checks if arbitrary tracking is currently running.
		/// </summary>
		/// <returns><c>true<c>/c>, if ArbiTrack is running <c>false</c> if not.</returns>
		public override bool ArbiTrackIsTracking ()
		{
			#if UNITY_EDITOR
			return false;

			#elif UNITY_IOS
			return NativeInterface.ArbiTrackIsTracking();

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				return m_KudanAR_Instance.Call<bool>("ArbiTrackIsTracking");
			}
			else
			{
				return false;
			}
			#endif
		}

		/// <summary>
		/// Gets the current position and orientation of the markerless driver being tracked.
		/// </summary>
		/// <param name="position">The position of the markerless transform driver.</param>
		/// <param name="orientation">The orientation of the markerless transform driver.</param>
		public override void ArbiTrackGetPose (out Vector3 position, out Quaternion orientation)
		{
			#if UNITY_EDITOR || UNITY_ANDROID
			position = new Vector3();
			orientation = new Quaternion();
			#endif

			#if UNITY_EDITOR

			#elif UNITY_IOS
			float[] f = new float[7];
			NativeInterface.ArbiTrackGetPose(f);

			position = new Vector3(f[0], f[1], -f[2]);
			orientation = new Quaternion(f[3], f[4], f[5], f[6]);

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				m_KudanAR_Instance.Call ("updateArbi", _floorHeight);

				AndroidJavaObject arbiPosition = m_KudanAR_Instance.Get<AndroidJavaObject>("m_ArbiPosition");
				AndroidJavaObject arbiOrientation = m_KudanAR_Instance.Get<AndroidJavaObject>("m_ArbiOrientation");

				position.x = arbiPosition.Get<float>("x");
				position.y = arbiPosition.Get<float>("y");
				position.z = -arbiPosition.Get<float>("z");


				orientation.x = arbiOrientation.Get<float>("x");
				orientation.y = arbiOrientation.Get<float>("y");
				orientation.z = arbiOrientation.Get<float>("z");
				orientation.w = arbiOrientation.Get<float>("w");
			}
			#endif
		}

		/// <summary>
		/// Gets the current position and orientation of the floor, relative to the device.
		/// </summary>
		/// <param name="position">Position of the floor, relative to the camera.</param>
		/// <param name="orientation">Orientation of the floor, relative to the camera.</param>
		public override void FloorPlaceGetPose (out Vector3 position, out Quaternion orientation)
		{
			#if UNITY_EDITOR || UNITY_ANDROID
			position = new Vector3();
			orientation = new Quaternion();
			#endif

			#if UNITY_EDITOR

			#elif UNITY_IOS
			float[] f = new float[7];
			NativeInterface.FloorPlaceGetPose(f, _floorHeight);

			position = new Vector3(f[0], f[1], f[2]);
			orientation = new Quaternion(f[3], f[4], f[5], f[6]);

			#elif UNITY_ANDROID
			if (m_KudanAR_Instance != null)
			{
				m_KudanAR_Instance.Call ("updateArbi", _floorHeight);

				AndroidJavaObject floorPosition = m_KudanAR_Instance.Get<AndroidJavaObject>("m_FloorPosition");
				AndroidJavaObject floorOrientation = m_KudanAR_Instance.Get<AndroidJavaObject>("m_FloorOrientation");

				position.x = floorPosition.Get<float>("x");
				position.y = floorPosition.Get<float>("y");
				position.z = floorPosition.Get<float>("z");

				orientation.x = floorOrientation.Get<float>("x");
				orientation.y = floorOrientation.Get<float>("y");
				orientation.z = floorOrientation.Get<float>("z");
				orientation.w = floorOrientation.Get<float>("w");
			}
			#endif
		}

		/// <summary>
		/// Method called just after the current frame has been drawn. Used only on the Android platform.
		/// </summary>
		public override void PostRender ()
		{
			#if UNITY_EDITOR || UNITY_IOS

			#elif UNITY_ANDROID
			//Some versions of Unity have a bug on Android where rendering to texture can only happen from OnPostRender
			if ( m_KudanAR_Instance != null )
			{
				GL.InvalidateState();
				m_KudanAR_Instance.Call<bool>( "render" );
				GL.InvalidateState();
			}
			#endif
		}

		/// <summary>
		/// Update the camera being used by the editor plugin. For the native equivalent, see UpdateBackground for iOS, or updateAndroidTracking for Android.
		/// </summary>
		public override void updateCam ()
		{
            if (receivingInput)
            {
                #if UNITY_EDITOR
                System.IntPtr texturePtr = _clonedTexture.GetNativeTexturePtr();
                long textureID = (long)texturePtr;

                NativeInterface.setTextureID(textureID);
                GL.IssuePluginEvent(NativeInterface.GetRenderEventFunc(), 0);

                #else
                #endif
            }
		}

		/// <summary>
		/// Get a list of all trackables detected in the current frame.
		/// </summary>
		/// <returns>A list of all trackables detected in the current frame.</returns>
		private List<Trackable> GetDetected()
		{
			int num = 0;

			#if UNITY_EDITOR || UNITY_IOS
			num = NativeInterface.GetNumberOfDetectedTrackables();
			#elif UNITY_ANDROID
			if ( m_KudanAR_Instance != null )
			{
				num = m_KudanAR_Instance.Call<int>( "GetNumberOfDetectedTrackables" );
			}
			#endif

			List<Trackable> result = new List<Trackable> (num);

			for (int i = 0; i < num; i++)
			{
				Trackable trackable = new Trackable();
				StringBuilder sbName = new StringBuilder(512);

				#if UNITY_EDITOR || UNITY_IOS
				int width = 0;
				int height = 0;
				int trackingMethod = 0;

				#if UNITY_EDITOR
				float[] p = new float[7];
				#elif UNITY_IOS
				float[] pos = new float[3];
				float[] ori = new float[4];
				#endif

				#endif

				#if UNITY_EDITOR
				NativeInterface.GetDetectedTrackable (i, p, ref width, ref height, ref trackingMethod, sbName);
				#elif UNITY_IOS
				GetDetectedTrackable(i, sbName, 512, ref width, ref height, pos, ori);
				#elif UNITY_ANDROID
				AndroidJavaObject thisTrackable = m_KudanAR_Instance.Call<AndroidJavaObject>( "GetTrackable", i );
				AndroidJavaObject thisTrackablePosition = thisTrackable.Get<AndroidJavaObject>( "m_Position" );
				AndroidJavaObject thisTrackableOrientation = thisTrackable.Get<AndroidJavaObject>( "m_Orientation" );
				#endif
				
				#if UNITY_EDITOR || UNITY_IOS
				trackable.name = sbName.ToString();
				trackable.width = width;
				trackable.height = height;
				trackable.trackingMethod = trackingMethod;
				#elif UNITY_ANDROID
				trackable.name = thisTrackable.Get<string>( "m_Name" );
				trackable.width = thisTrackable.Get<int>( "m_Width" );
				trackable.height = thisTrackable.Get<int>( "m_Height" );
				#endif

				#if UNITY_EDITOR
				trackable.position = ConvertNativeFloatsToVector3 (p [0], p [1], p [2]);
				trackable.orientation = ConvertNativeFloatsToQuaternion (p [3], p [4], p [5], p [6]);
				#elif UNITY_IOS
				trackable.position = ConvertNativeFloatsToVector3(pos[0], pos[1], pos[2]);
				trackable.orientation = ConvertNativeFloatsToQuaternion(ori[0], ori[1], ori[2], ori[3]);
				#elif UNITY_ANDROID
				trackable.position = ConvertNativeFloatsToVector3( thisTrackablePosition.Get<float>( "x" ), thisTrackablePosition.Get<float>( "y" ), thisTrackablePosition.Get<float>( "z" ) );
				trackable.orientation = ConvertNativeFloatsToQuaternion( thisTrackableOrientation.Get<float>( "x" ), thisTrackableOrientation.Get<float>( "y" ), thisTrackableOrientation.Get<float>( "z" ), thisTrackableOrientation.Get<float>( "w" ) );
				thisTrackable.Dispose();
				#endif

				result.Add(trackable);
			}

			return result;
		}

		/// <summary>
		/// Convert floats to a 4x4 Matrix.
		/// </summary>
		/// <returns>A 4x4 Matrix created using the given floats.</returns>
		/// <param name="r">An array of 16 native floats.</param>
		/// <param name="cameraAspect">The aspect ratio of the camera.</param>
		public static Matrix4x4 ConvertNativeFloatsToMatrix(float[] r, float cameraAspect)
		{
			Matrix4x4 m = new Matrix4x4 ();
			m.SetRow (0, new Vector4 (r [0], r [1], r [2], r [3]));
			m.SetRow (1, new Vector4 (r [4], r [5], r [6], r [7]));
			m.SetRow (2, new Vector4 (r [8], r [9], r [10], r [11]));
			m.SetRow (3, new Vector4 (r [12], r [13], r [14], r [15]));

			// Scale the aspect ratio based on camera vs screen ratios
			float screenAspect = ((float)Screen.width / (float)Screen.height);
			float scale = cameraAspect / screenAspect;

			if (scale > 1) 
			{
				m.m00 *= scale;
			} 
			else 
			{
				m.m11 /= scale;
			}

			m = m.transpose;

			m.m02 *= -1f;
			m.m12 *= -1f;

			return m;
		}

		/// <summary>
		/// Convert floats to a 3-Dimensional Vector.
		/// </summary>
		/// <returns>A 3-Dimenionsal Vector created using the given floats.</returns>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		/// <param name="z">The z coordinate.</param>
		protected static Vector3 ConvertNativeFloatsToVector3(float x, float y, float z)
		{
			#if UNITY_EDITOR
			return new Vector3 (x, y, -z);
			#elif UNITY_IOS || UNITY_ANDROID
			return new Vector3(-x, -y, -z);
			#endif
		}

		/// <summary>
		/// Convert floats to a Quaternion.
		/// </summary>
		/// <returns>A Quaternioin created using the given floats.</returns>
		/// <param name="x">The x coefficient.</param>
		/// <param name="y">The y coefficient.</param>
		/// <param name="z">The z coefficient.</param>
		/// <param name="w">The w coefficient.</param>
		protected static Quaternion ConvertNativeFloatsToQuaternion(float x, float y, float z, float w)
		{
			#if UNITY_EDITOR
			return new Quaternion (-x, -y, z, w) * Quaternion.AngleAxis (90f, Vector3.forward) * Quaternion.AngleAxis (90f, Vector3.left);
			#elif UNITY_IOS || UNITY_ANDROID
			return new Quaternion(x, y, z, w) * Quaternion.AngleAxis(-90f, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.left);
			#endif
		}

		/// <summary>
		/// Update the camera, tracker and app rates. 
		/// Camera Rate is the rate at which the background camera texture updates refreshes.
		/// Tracker Rate is the rate at which the tracker processes trackables, old and new.
		/// App Rate is the rate at which the Unity player is running, including any models and animations, etc.
		/// Visually, the rates in the Debug GUI update once every second.
		/// </summary>
		private void UpdateFrameRates()
		{
			_rateTimer += Time.deltaTime;
			_numFramesRendered++;
		
			if (_rateTimer >= 1.0f)
			{
				#if UNITY_EDITOR
				_cameraRate = (float)_numFramesGrabbed / _rateTimer;
				_trackerRate = (float)_numFramesTracked  / _rateTimer;

				#elif UNITY_ANDROID
				if ( m_KudanAR_Instance != null )
				{
					_cameraRate = m_KudanAR_Instance.Call<float>( "getCameraDisplayFrameRate" );
					_trackerRate = m_KudanAR_Instance.Call<float>( "getTrackerFrameRate" );
				}

				#elif UNITY_IOS
				_cameraRate = GetCaptureDeviceRate();
				_trackerRate = GetTrackerRate();
				#endif

				_appRate = (float)_numFramesRendered  / _rateTimer;
		
				_rateTimer = 0f;
				_numFramesGrabbed = 0;
				_numFramesTracked = 0;
				_numFramesRendered = 0;
			}
		}

		/// <summary>
		/// Update the projection matrix depending on the orientation of the device. This accounts for Unity's own auto-rotation to keep everything the right way up. Only used on mobile.
		/// </summary>
		public void UpdateRotation()
		{
			#if UNITY_EDITOR

			#elif UNITY_IOS || UNITY_ANDROID
			ScreenOrientation currentOrientation = Screen.orientation;

			if (currentOrientation == _prevScreenOrientation) 
			{
				return;
			}

			_prevScreenOrientation = currentOrientation;

			#if UNITY_IOS
			float projectionScale = 1.0f / _cameraAspect;
			#elif UNITY_ANDROID
			float fCameraAspectRatio = (float)(m_Width) / (float)(m_Height);
			float projectionScale = 1.0f / fCameraAspectRatio;
			#endif

			int[] indices;

			if (currentOrientation == ScreenOrientation.LandscapeLeft) 
			{
				indices = new int[]{ 0, 1, 2, 3 };
				_projectionRotation = Matrix4x4.identity;
			} 
			else if (currentOrientation == ScreenOrientation.Portrait) 
			{
				indices = new int[]{ 2, 3, 1, 0 };
				_projectionRotation = Matrix4x4.TRS (Vector3.zero, Quaternion.AngleAxis (90, Vector3.back), new Vector3 (projectionScale, projectionScale, 1));
			} 
			else if (currentOrientation == ScreenOrientation.LandscapeRight) 
			{
				indices = new int[]{ 1, 0, 3, 2 };
				_projectionRotation = Matrix4x4.TRS (Vector3.zero, Quaternion.AngleAxis (180, Vector3.back), Vector3.one);
			} 
			else if (currentOrientation == ScreenOrientation.PortraitUpsideDown) 
			{
				indices = new int[]{ 3, 2, 0, 1 };
				_projectionRotation = Matrix4x4.TRS (Vector3.zero, Quaternion.AngleAxis (270, Vector3.back), new Vector3 (projectionScale, projectionScale, 1));
			} 
			else 
			{
				return;
			}

			Vector3[] pos = new Vector3[4];

			pos [indices [0]] = new Vector3 (-0.5f, -0.5f, 0.0f);
			pos [indices [1]] = new Vector3 (0.5f, 0.5f, 0.0f);
			pos [indices [2]] = new Vector3 (0.5f, -0.5f, 0.0f);
			pos [indices [3]] = new Vector3 (-0.5f, 0.5f, 0.0f);

			_cameraBackgroundMeshFilter.mesh.vertices = pos;
			#endif
		}

		/// <summary>
		/// Loads the YpCbCr material and applies it to the background object. Only used on iOS.
		/// </summary>
		private void SetYpCbCrMaterialOnBackground()
		{
			#if UNITY_EDITOR

			#elif UNITY_IOS
			Material matYpCbCr = Resources.Load("YpCbCr", typeof(Material)) as Material;
			if (matYpCbCr != null)
			{
				_background.material = matYpCbCr;

				_textureYpID = Shader.PropertyToID("Yp");
				Debug.Log("_textureYpID == " + _textureYpID);

				_textureCbCrID = Shader.PropertyToID("CbCr");
				Debug.Log("_textureCbCrID == " + _textureCbCrID);

				_background.material.SetTextureScale("Yp", new Vector2(1.0f, -1.0f));
				_background.material.SetTextureOffset("Yp", new Vector3(0.0f, 1.0f));
				_background.material.SetTextureScale("CbCr", new Vector2(1.0f, -1.0f));
				_background.material.SetTextureOffset("CbCr", new Vector3(0.0f, 1.0f));
			}
			else
			{
				Debug.LogError("[KudanAR] Failed to load YpCbCr material");
			}
			#endif
		}

		/// <summary>
		/// Update the background camera texture using the Yp and CbCr textures. Only used by iOS.
		/// </summary>
		private void UpdateBackground()
		{
			#if UNITY_EDITOR

			#elif UNITY_IOS
			int width = 0, height = 0;
			System.IntPtr texture = GetTextureForPlane(0, ref width, ref height);
			if (texture != System.IntPtr.Zero)
			{
				if (_textureYp == null)
				{
					Debug.Log("CreateExternalTexture(" + width + ", " + height + ", TextureFormat.Alpha8, false, false, " + texture + ")");
					_textureYp = Texture2D.CreateExternalTexture(width, height, TextureFormat.Alpha8, false, false, texture);
					_cameraAspect = (float)width / (float)height;
					_finalTexture = _textureYp;
				}
				else
				{
					_textureYp.UpdateExternalTexture(texture);
				}
			}

			texture = GetTextureForPlane(1, ref width, ref height);
			if (texture != System.IntPtr.Zero)
			{
				if (_textureCbCr == null)
				{
					Debug.Log("CreateExternalTexture(" + width / 2 + ", " + height + ", TextureFormat.RGBA32, false, false, " + texture + ")");
					_textureCbCr = Texture2D.CreateExternalTexture(width / 2, height, TextureFormat.RGBA32, false, false, texture);
				}
				else
				{
					_textureCbCr.UpdateExternalTexture(texture);
				}
			}

			if (_textureYp != null && _textureCbCr != null)
			{
				_background.material.SetTexture(_textureYpID, _textureYp);
				_background.material.SetTexture(_textureCbCrID, _textureCbCr);
			}
			else
			{
				Debug.LogError("[KudanAR] Failed to create external textures");
			}
			#endif
		}
	}
}
