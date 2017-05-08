using UnityEngine;
using System.Collections;

namespace Kudan.AR
{
	/// <summary>
	/// Displays an object when using the Markerless Tracking Method, that acts as a preview showing where the object will be placed.
	/// </summary>
	public class TargetNode : MonoBehaviour 
	{
		/// <summary>
		/// Reference to the markerless tracking method.
		/// </summary>
		public TrackingMethodMarkerless markerless;

		/// <summary>
		/// The tracker attached to the camera.
		/// </summary>
		public KudanTracker tracker;

		/// <summary>
		/// An array of objects that are checked to see when they are active.
		/// </summary>
		public GameObject activeObj;

		/// <summary>
		/// The arrow object.
		/// </summary>
		public GameObject target;

		void Update()
		{
			Vector3 pos;
			Quaternion rot;

			target.gameObject.SetActive (false);

			if (tracker.CurrentTrackingMethod == markerless && !activeObj.activeInHierarchy) 
			{
				target.gameObject.SetActive (true);
			}

			if (target.gameObject.activeSelf) 
			{
				tracker.FloorPlaceGetPose (out pos, out rot);

				target.transform.position = pos;
				target.transform.Translate (0, -25, 0);
				target.transform.rotation = rot;
				#if UNITY_IOS
				target.transform.Rotate (0, 0, 180);
				#endif
			}
		}
	}
}