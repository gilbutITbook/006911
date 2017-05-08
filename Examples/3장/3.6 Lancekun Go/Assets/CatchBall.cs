using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CatchBall : MonoBehaviour {

    bool is_drag_now_ = false;
	float catch_ball_distance_;
	public float catch_ball_throw_speed_ = 120;
	public float catch_ball_arch_speed_ = 100;
	public float catch_ball_speed_ = 1000;


    void OnMouseDown()
    {
        catch_ball_distance_ = Vector3.Distance(transform.position, Camera.main.transform.position);
        is_drag_now_ = true;
    }


    void OnMouseUp()
    {
        GetComponent<Rigidbody>().useGravity = true;
        GetComponent<Rigidbody>().velocity += transform.forward * catch_ball_throw_speed_;
        GetComponent<Rigidbody>().velocity += transform.up * catch_ball_arch_speed_;
        is_drag_now_ = false;
    }


    void Update()
    {
        if (is_drag_now_)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            Vector3 ray_point = ray.GetPoint(catch_ball_distance_);
            transform.position = Vector3.Lerp(transform.position, ray_point, catch_ball_speed_ * Time.deltaTime);
        }
    }
    public void ResetCatchBall()
    {
        GetComponent<Rigidbody>().useGravity = false;
        GetComponent<Rigidbody>().velocity = Vector3.zero;
        transform.position = new Vector3(0, -60, -150);
    }


}
