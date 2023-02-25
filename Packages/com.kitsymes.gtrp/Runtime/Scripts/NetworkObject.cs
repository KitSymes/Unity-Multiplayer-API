using UnityEngine;

public sealed class NetworkObject : MonoBehaviour
{
    private uint _id;
    private uint _owner;

    void Start()
    {

    }

    void Update()
    {
        
    }

    void Spawn(uint objectID, uint ownerID)
    {
        _id = objectID;
        _owner = ownerID;

    }
}
