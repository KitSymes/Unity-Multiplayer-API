using Mirror;
using UnityEngine;

public class MirrorObjectSceneScript : MonoBehaviour
{
    public GameObject testCubePrefab;
    public int max;
    private int _count = 0;
    private float _timeSince = 2.0f;

    void Start()
    {
        if (!NetworkServer.active)
            Destroy(gameObject);
    }

    void Update()
    {
        if (_count >= max)
            return;

        _timeSince -= Time.deltaTime;
        if (_timeSince <= 0.0f)
        {
            _timeSince = 1.0f;
            GameObject nob = Instantiate(testCubePrefab);
            nob.name = "Instance " + _count;
            nob.transform.position = new Vector3(Random.Range(-5.0f, 5.0f), Random.Range(-5.0f, 5.0f));
            NetworkServer.Spawn(nob);
            _count++;
        }
    }
}
