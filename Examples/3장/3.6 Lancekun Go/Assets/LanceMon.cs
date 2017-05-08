using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LanceMon : MonoBehaviour {


    void OnTriggerEnter(Collider _others )
    {
        if (_others.name == "CatchBall")
        {
            Debug.Log("랜스군이잡혔다!!");
            this.gameObject.SetActive(false);
        }

    }

}
