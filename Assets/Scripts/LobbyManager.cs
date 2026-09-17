using UnityEngine;
using UnityEngine.SceneManagement; // [추가] 씬 전환을 위해 필요

public class LobbyManager : MonoBehaviour
{
    public GameObject PlayButton;

    void Start()
    {

    }

    void Update()
    {

    }

    // [추가] Play 버튼의 OnClick()에 연결할 함수
    public void OnPlayButtonClicked()
    {
        SceneManager.LoadScene("Ep.1 Story");
    }
}