using UnityEngine;
using Vuforia;

public class TrackingObject : MonoBehaviour, ITrackableEventHandler
{
    
    public TextMesh obj_text_mesh_;
    public string name_;
    public int atk_;
    public int def_;
    public int hp_;
    
    public Animation obj_animation_;
    private TrackableBehaviour mTrackableBehaviour;
    public bool is_detected_ = false;

    void Start()
    {
        obj_text_mesh_.text = name_ + "\n HP:" + hp_;

        mTrackableBehaviour = GetComponent<TrackableBehaviour>();
        if (mTrackableBehaviour)
        {
            mTrackableBehaviour.RegisterTrackableEventHandler(this);
        }
    }

    /*
    public void OnTrackableStateChanged(TrackableBehaviour.Status previousStatus, TrackableBehaviour.Status newStatus)
    {
        if (newStatus == TrackableBehaviour.Status.DETECTED || newStatus == TrackableBehaviour.Status.TRACKED)
        {
            is_detected_ = true;
        }
        else
        {
            is_detected_ = false;
        }
    }
    
  
     */

        public void OnTrackableStateChanged(TrackableBehaviour.Status previousStatus, TrackableBehaviour.Status newStatus)
        {
            if (newStatus == TrackableBehaviour.Status.DETECTED || newStatus == TrackableBehaviour.Status.TRACKED)
            {
                is_detected_ = true;
            }
            else
            {
                is_detected_ = false;
            }
        }
    }



