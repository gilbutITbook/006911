using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Kudan.AR
{
	[DisallowMultipleComponent]
	[AddComponentMenu("Kudan AR/Tracking Methods/Marker Tracking")]
	/// <summary>
	/// The Marker Tracking Method. This method tracks objects using markers for positional data.
	/// </summary>
	public class TrackingMethodMarker : TrackingMethodBase
	{
		/// <summary>
		/// Array containing all sets of trackable data to be loaded into the tracker.
		/// </summary>
		public TrackableData[] _markers;

		/// <summary>
		/// Event triggered in the first frame a trackable is detected.
		/// </summary>
		public MarkerFoundEvent _foundMarkerEvent;

		/// <summary>
		/// Event trigged in the first frame a trackable is not detected after being detected.
		/// </summary>
		public MarkerLostEvent _lostMarkerEvent;

		/// <summary>
		/// Event triggered each frame in which a trackable is detected.
		/// </summary>
		public MarkerUpdateEvent _updateMarkerEvent;

		/// <summary>
		/// Array of all trackables detected in the previous frame.
		/// </summary>
		private Trackable[] _lastDetectedTrackables;
	
		/// <summary>
		/// The name of this tracking method.
		/// </summary>
		/// <value>The name of this tracking method is "Marker"</value>
		public override string Name
		{
			get { return "Marker"; }
		}

		/// <summary>
		/// The ID of this tracking method.
		/// </summary>
		/// <value>The ID of this tracking method is 0.</value>
		public override int TrackingMethodId
		{
			get { return 0; }
		}

		/// <summary>
		/// Initialise this tracking method by loading all markers within the referenced data sets.
		/// </summary>
		public override void Init()
		{
			LoadMarkers();
		}

		/// <summary>
		/// Loads the marker data from the array of trackable data sets.
		/// </summary>
		private void LoadMarkers()
		{
			foreach (TrackableData marker in _markers)
			{
				if (marker != null)
				{
					if (marker.Data == null || marker.Data.Length == 0)
					{
						Debug.LogWarning("[KudanAR] Marker has no data assigned");
					}
					else if (!Plugin.AddTrackableSet(marker.Data, marker.id))
					{
						Debug.LogError("[KudanAR] Error adding trackable " + marker.id);
					}
				}
				else
				{
					Debug.LogWarning("[KudanAR] Null marker in list");
				}
			}
		}

		/// <summary>
		/// Processes the current frame and checks which trackables have been detected this frame.
		/// </summary>
		public override void ProcessFrame()
		{
			ProcessNewTrackables();
		}

		/// <summary>
		/// Stop this tracking method. Any markers currently detected will be lost.
		/// </summary>
        public override void StopTracking()
        {
            base.StopTracking();

            Trackable[] oldtrackables = _lastDetectedTrackables;

            if (oldtrackables != null)
            {
                for (int i = 0; i < oldtrackables.Length; i++)
                {
                    _lostMarkerEvent.Invoke(oldtrackables[i]);
                }
            }

            _lastDetectedTrackables = null;
        }

		/// <summary>
		/// Checks for and processes all trackables detected or lost in the current frame.
		/// </summary>
        private void ProcessNewTrackables()
		{
			Trackable[] newTrackables = Plugin.GetDetectedTrackablesAsArray();
			Trackable[] oldtrackables = _lastDetectedTrackables;

			// Find lost markers
			if (oldtrackables != null)
			{
				for (int i = 0; i < oldtrackables.Length; i++)
				{
					bool found = false;
					for (int j = 0; j < newTrackables.Length; j++)
					{
						if (oldtrackables[i].name == newTrackables[j].name)
						{
							found = true;
							break;
						}
					}

					if (!found)
					{
						_lostMarkerEvent.Invoke(oldtrackables[i]);
					}
				}
			}

			if (newTrackables != null)
			{
				// Find new markers
				for (int j = 0; j < newTrackables.Length; j++)
				{
					bool found = false;
					if (oldtrackables != null)
					{
						for (int i = 0; i < oldtrackables.Length; i++)
						{
							if (oldtrackables[i].name == newTrackables[j].name)
							{
								found = true;
								break;
							}
						}
					}

					if (!found)
					{
						_foundMarkerEvent.Invoke(newTrackables[j]);
					}
				}

				// Find updated markers
				for (int j = 0; j < newTrackables.Length; j++)
				{
					_updateMarkerEvent.Invoke(newTrackables[j]);
				}
			}

			// Point to the new markers
			_lastDetectedTrackables = newTrackables;
		}

		/// <summary>
		/// Draw the debug GUI for Marker tracking.
		/// </summary>
		/// <param name="uiScale">User interface scale.</param>
		public override void DebugGUI(int uiScale)
		{
			// Each actively tracked object
			GUILayout.Label ("Trackable sets loaded: " + Plugin.GetNumTrackables());

			GUILayout.Label ("Marker Recovery Status: " + Plugin.GetMarkerRecoveryStatus());
            
			GUILayout.Label ("Marker Autocrop Status: " + Plugin.GetMarkerAutoCropStatus());
            
			GUILayout.Label ("Marker Extensibility Status: " + Plugin.GetMarkerExtensibilityStatus());

			int numDetected = 0;
			if (_lastDetectedTrackables != null)
			{
				numDetected = _lastDetectedTrackables.Length;

			}
			GUILayout.Label("Detected: " + numDetected);

			if (_kudanTracker.HasActiveTrackingData())
			{
				foreach (Trackable t in _lastDetectedTrackables)
				{
					GUILayout.Label("Found: " + t.name);

					if (Camera.current != null)
					{
						// Draw a label in camera-space at the point of the detected marker
						Vector3 sp = Camera.current.WorldToScreenPoint(t.position);
						sp.y = Screen.height - sp.y;
						GUIContent content = new GUIContent(t.name + "\n" + t.position.ToString() + "\n" + t.width + "x" + t.height);
						Vector2 size = GUI.skin.box.CalcSize(content);
						GUI.Label(new Rect(sp.x / uiScale, sp.y / uiScale, size.x, size.y), content, "box");
					}
				}

				GUILayout.EndVertical();
			}
		}
	}
}
