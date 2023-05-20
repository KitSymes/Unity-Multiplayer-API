using UnityEngine;

public class FramerateLimiter : MonoBehaviour
{
    public int targetFrameRate = 300;

    void Start()
    {
        Application.targetFrameRate = targetFrameRate;
    }
}
