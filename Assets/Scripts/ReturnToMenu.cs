using UnityEngine;
using UnityEngine.SceneManagement;

public class ReturnToMenu : MonoBehaviour
{
    public void BackToMenu() => SceneManager.LoadScene("MainMenu");
}