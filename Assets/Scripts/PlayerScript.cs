using KitSymes.GTRP;
using UnityEngine;

public partial class PlayerScript : NetworkBehaviour
{
    [SyncVar]
    public int dummy = 0;

    [SyncVarAttribute]
    public int test = 0;

    [SyncVar]
    public byte testByte = 0;
    [SyncVar]
    public bool testBool = false;
    [SyncVar]
    public char testChar = 'b';
    [SyncVar]
    public int testInt = 3;
    [SyncVar]
    public float testFloat = 4.2f;
    [SyncVar]
    public double testDouble = 6.1d;
    [SyncVar]
    public string testString = "no";

    // Start is called before the first frame update
    void Start()
    {
        //DebugSourceGenerator.GetTestText();
    }

    // Update is called once per frame
    void Update()
    {
        if (!IsOwner())
            return;
        if (Input.GetKey(KeyCode.A))
            transform.position -= new Vector3(1.0f, 0.0f) * Time.deltaTime;
        if (Input.GetKey(KeyCode.D))
            transform.position += new Vector3(1.0f, 0.0f) * Time.deltaTime;
    }
}
