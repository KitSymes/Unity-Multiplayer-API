using UnityEngine;
using UnityEngine.Events;

public class OnBallCollide : MonoBehaviour
{
    public UnityEvent OnCollide;
    void OnCollisionEnter(Collision collision)
    {
        Debug.Log("collision enter");
        if (collision.gameObject.CompareTag("Ball"))
            OnCollide?.Invoke();
    }
}
