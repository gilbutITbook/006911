using UnityEngine;
using System.Collections;

public class BulletMaker : MonoBehaviour
{
    //0330 prefab_bullt_  -> prefab_bullet_  수정
    public GameObject prefab_bullet_; // 총알 프리팹
	private float spawn_time_ = 0.0f;// 총알 생성 간격

	public int max_bullets_count_ = 5;// 최대 총알수
	public int bullets_count_ = 0;    // 현재 총알수

	void Start()
	{
		// 기존 화면에 있던 오브젝트 비활성화
		prefab_bullet_.SetActive(false);

		// 유니티 화면이 꺼지지 않게 설정
#if UNITY_ANDROID
		Screen.sleepTimeout = SleepTimeout.NeverSleep;
#elif UNITY_IOS
		iPhoneSettings.screenCanDarken=false;
#endif
	}

	void Update()
	{
		// 1초 마다 총알 생성
		spawn_time_ += Time.deltaTime;

		if (spawn_time_ > 1.0f && max_bullets_count_ >= bullets_count_)
		{
			spawn_time_ = 0.0f;
			GameObject obj_bullet = GameObject.Instantiate(prefab_bullet_, this.transform) as GameObject;
			obj_bullet.SetActive(true);
			obj_bullet.transform.localPosition = new Vector3(Random.RandomRange(-2.0f, 2.0f), Random.RandomRange(0.5f, 3.0f), 30.0f);

			bullets_count_++;
		}

		// 총알 움직여주기
		for (int i = 0; i < transform.childCount; i++)
		{
			Transform child_bullet = transform.GetChild(i);
			child_bullet.Translate(new Vector3(0, 0, 1));
			if (child_bullet.localPosition.z < -5.0f) // 범위를 벗어난 총알은 위치를 재배정해준다.
				child_bullet.transform.localPosition = new Vector3(Random.RandomRange(-2.0f, 2.0f), Random.RandomRange(0.5f, 3.0f), 30.0f);
		}
	}
}
