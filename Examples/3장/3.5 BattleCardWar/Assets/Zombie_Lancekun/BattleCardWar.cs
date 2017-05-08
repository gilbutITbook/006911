using System.Collections;
using UnityEngine;
using Vuforia;


public enum eGameState
{
    Ready = 0,
    Battle,
    Result
}


public class BattleCardWar : MonoBehaviour
{
    public TrackingObject obj_lancekun_;
    public TrackingObject obj_zombie_;
    public eGameState game_state_ = eGameState.Ready;
    public string system_message_ = "";



    void OnGUI()
    {

        GUIStyle gui_style = new GUIStyle();
        gui_style.fontSize = 60;
        gui_style.normal.textColor = Color.yellow;
        //0329
        //GUI.Label(new Rect(400, 150, 200, 60), "State : " + game_state_.ToString(), gui_style);


        GUIStyle gui_style_btn = new GUIStyle("Button");
        gui_style_btn.fontSize = 50;

        if (obj_lancekun_.is_detected_ && obj_zombie_.is_detected_ && game_state_ == eGameState.Ready)
        {
            if (GUI.Button(new Rect(500, 500, 350, 150), "Start Battle", gui_style_btn))
            {
                game_state_ = eGameState.Battle;
                system_message_ = "주사위로 선공정하기";
                StartCoroutine(RollTheDices());
            }
        }

        if (game_state_ == eGameState.Ready)
        {
            system_message_ = "[게임 준비중]카드를 인식시켜주세요.";

            

        }
        GUI.Label(new Rect(400, 150, 200, 60), system_message_, gui_style);

        if (game_state_ == eGameState.Result)
        {
            if (GUI.Button(new Rect(500, 500, 350, 150), "Refresh", gui_style_btn))
            {
                game_state_ = eGameState.Ready;

                // 초기화
                obj_lancekun_.obj_text_mesh_.text = obj_lancekun_.name_ + "\n HP:" + obj_lancekun_.hp_;
                obj_zombie_.obj_text_mesh_.text = obj_zombie_.name_ + "\n HP:" + obj_zombie_.hp_;
            }
        }

    }


    IEnumerator RollTheDices()
    {



        int last_lancekun_dice = 0;
        int last_zombie_dice = 0;
        for (int i = 0; i < 30; i++)
        {
            last_lancekun_dice = 1 + Random.Range(0, 6);
            last_zombie_dice = 1 + Random.Range(0, 6);

            obj_lancekun_.obj_text_mesh_.text = "주사위 : " + last_lancekun_dice;
            obj_zombie_.obj_text_mesh_.text = "주사위 : " + last_zombie_dice;
            yield return new WaitForSeconds(0.1f);
        }



        if (last_lancekun_dice > last_zombie_dice)
        {
            system_message_ = "랜스군 선공";
            StartCoroutine(StartBattle(obj_lancekun_, obj_zombie_));
        }
        else if (last_lancekun_dice < last_zombie_dice)
        {
            system_message_ = "좀비군 선공";
            StartCoroutine(StartBattle(obj_zombie_, obj_lancekun_));
        }
        else if (last_lancekun_dice == last_zombie_dice)
        {
            system_message_ = "무승부 - 다시하기";
            StartCoroutine(RollTheDices());
        }
    }



    IEnumerator StartBattle(TrackingObject _first_turn, TrackingObject _second_turn)
    {
        yield return new WaitForSeconds(1.0f);
        game_state_ = eGameState.Result;
        int first_hp = _first_turn.hp_;
        int second_hp = _second_turn.hp_;

        // 체력 정보 갱신
        _first_turn.obj_text_mesh_.text = _first_turn.name_ + "\n HP:" + first_hp;
        _second_turn.obj_text_mesh_.text = _second_turn.name_ + "\n HP:" + second_hp;

        while (true)
        {
            // 선공의 턴 
            _first_turn.obj_animation_.Play("Attack");
            yield return new WaitForSeconds(_first_turn.obj_animation_.GetClip("Attack").length);
            _first_turn.obj_animation_.Play("Idle");
            second_hp -= _first_turn.atk_;


            // 체력 정보 갱신
            _first_turn.obj_text_mesh_.text = _first_turn.name_ + "\n HP:" + first_hp;
            _second_turn.obj_text_mesh_.text = _second_turn.name_ + "\n HP:" + second_hp;
            if (second_hp <= 0)
            {
                // _second_turn 의 패배
                system_message_ = _first_turn.name_ + " 가 승리하였습니다.";
                break;
            }
            yield return new WaitForSeconds(1.0f);

            // 후공의 턴 
            _second_turn.obj_animation_.Play("Attack");
            yield return new WaitForSeconds(_second_turn.obj_animation_.GetClip("Attack").length);
            _second_turn.obj_animation_.Play("Idle");
            first_hp -= _second_turn.atk_;


            // 체력 정보 갱신
            _first_turn.obj_text_mesh_.text = _first_turn.name_ + "\n HP:" + first_hp;
            _second_turn.obj_text_mesh_.text = _second_turn.name_ + "\n HP:" + second_hp;
            if (first_hp <= 0)
            {
                // _first_turn 의 패배
                system_message_ = _second_turn.name_ + " 가 승리하였습니다.";
                break;
            }

            yield return new WaitForSeconds(1.0f);
        }
    }
}
 