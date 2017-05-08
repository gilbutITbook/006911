using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Kudan.AR;

public class Lancekun_Go_Main : MonoBehaviour {

    public LanceMon obj_lance_mon_;
	public CatchBall obj_catchball_;

    public KudanTracker kudan_tracker_;
	// Use this for initialization
	
	
	// Update is called once per frame
	void Update () {
		
	}
    void Start()
    {


    }


    void SpawnLancekun()
    {
        Vector3 floor_position;
        Quaternion floor_orientation;

        kudan_tracker_.FloorPlaceGetPose(out floor_position, out floor_orientation);
        kudan_tracker_.ArbiTrackStart(floor_position, floor_orientation);

        obj_lance_mon_.gameObject.SetActive(true);
    }




    void OnGUI()
    {


        if (GUI.Button(new Rect(0, 300, 100, 50), "랜스군출몰"))
        {

            Debug.Log("랜스군출몰");
            SpawnLancekun();
        }
        if (GUI.Button(new Rect(0, 400, 100, 50), "캐치볼리셋"))
        {

            Debug.Log("캐치볼리셋");
            obj_catchball_.ResetCatchBall();
        }

    }


}


