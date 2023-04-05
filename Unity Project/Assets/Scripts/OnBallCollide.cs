using UnityEngine;
using UnityEngine.Events;

public class OnBallCollide : MonoBehaviour
{
    public UnityEvent OnCollide;
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ball"))
            OnCollide?.Invoke();
    }
}
