using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public void LoadLaboratorio()    => SceneManager.LoadScene("Laboratório_Manual");
    public void LoadNewton()         => SceneManager.LoadScene("Newton_Aleatório");
    public void LoadRelatividade()   => SceneManager.LoadScene("Relatividade_Geral");
    public void QuitApplication()    => Application.Quit();
}