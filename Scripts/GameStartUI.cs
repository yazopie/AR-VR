using UnityEngine;

public class GameStartUI : MonoBehaviour
{
    public GameObject menuUI;

    public void GameStart()
    {
        menuUI.SetActive(false);
    }
}
