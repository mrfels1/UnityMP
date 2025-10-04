using UnityEngine;
using UnityEngine.SceneManagement;

public class SimpleSceneMenu : MonoBehaviour
{
    [SerializeField] private string sceneBase2 = "base2";
    [SerializeField] private string sceneBase3 = "base3";
    [SerializeField] private string sceneBase4 = "base4";

    public void LoadBase2() => Load(sceneBase2);
    public void LoadBase3() => Load(sceneBase3);
    public void LoadBase4() => Load(sceneBase4);
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Load(string sceneName)
    {
        if (!string.IsNullOrEmpty(sceneName))
            SceneManager.LoadScene(sceneName);
    }
}
