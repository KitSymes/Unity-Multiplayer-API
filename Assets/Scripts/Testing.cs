using KitSymes.GTRP;
using KitSymes.GTRP.MonoBehaviours;
using UnityEngine;

public class Testing : MonoBehaviour
{
    public NetworkObject cubePrefab;
    public NetworkObject spherePrefab;
    public NetworkObject capsulePrefab;

    int _cube = 0;
    int _sphere = 0;
    int _capsule = 0;

    public void SpawnCube()
    {
        GameObject t = Instantiate(cubePrefab.gameObject);
        t.transform.position = new Vector3(0.0f, _cube);
        NetworkManager.Spawn(t);

        _cube++;
    }

    public void SpawnSphere()
    {
        GameObject t = Instantiate(spherePrefab.gameObject);
        t.transform.position = new Vector3(1.0f, _sphere);
        NetworkManager.Spawn(t);

        _sphere++;
    }

    public void SpawnCapsule()
    {
        GameObject t = Instantiate(capsulePrefab.gameObject);
        t.transform.position = new Vector3(2.0f, _capsule);
        NetworkManager.Spawn(t);

        _capsule++;
    }
}
