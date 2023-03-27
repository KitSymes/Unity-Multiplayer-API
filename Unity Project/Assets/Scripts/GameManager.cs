using KitSymes.GTRP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager : NetworkBehaviour
{
    [SerializeField]
    NetworkObject _ballPrefab;
    Ball _ball;
    [SyncVar("ScoreLChanged")]
    public int scoreL = 0;
    [SyncVar(nameof(ScoreRChanged))]
    public int scoreR = 0;

    [SerializeField]
    private Text _scoreLText;
    [SerializeField]
    private Text _scoreRText;

    public NetworkObject playerL;
    public NetworkObject playerR;

    public bool _gameReady = false;

    public override void OnServerStart()
    {
        NetworkManager.GetInstance().OnPlayerConnect += OnPlayerConnect;
        NetworkManager.GetInstance().OnPlayerDisconnect += OnPlayerDisonnect;
        _ball = Instantiate(_ballPrefab).GetComponent<Ball>();
        NetworkManager.Spawn(_ball.gameObject);
        _ball.SetDirection(new Vector3());
    }

    void OnPlayerConnect(uint id)
    {
        NetworkObject player = NetworkManager.GetInstance().GetPlayer(id);
        if (playerL == null)
        {
            playerL = player;
            Debug.Log("Moving");
            player.transform.position = new Vector3(-7.0f, 0.0f);
        }
        else if (playerR == null)
        {
            playerR = player;
            Debug.Log("Moving");
            player.transform.position = new Vector3(7.0f, 0.0f);
        }

        if (playerL != null && playerR != null)
            _gameReady = true;
    }
    void OnPlayerDisonnect(uint id)
    {
        NetworkObject player = NetworkManager.GetInstance().GetPlayer(id);
        if (playerL == player)
            playerL = null;
        if (playerR == player)
            playerR = null;

        _ball.SetDirection(new Vector3(0.0f, 0.0f));
        _ball.transform.position = new Vector3();
    }

    public void OutOfBounds()
    {
        if (_ball.lastTouched == playerL)
            RScore();
        else if (_ball.lastTouched == playerR)
            LScore();
        else
        {
            _ball.transform.position = new Vector3();
            _ball.SetDirection(new Vector3(1.0f, 0.0f));
            _ball.lastTouched = null;
        }
    }

    public void LScore()
    {
        scoreL++;
        _ball.transform.position = new Vector3();
        _ball.SetDirection(new Vector3(1.0f, 0.0f));
        _ball.lastTouched = null;
    }

    public void RScore()
    {
        scoreR++;
        _ball.transform.position = new Vector3();
        _ball.SetDirection(new Vector3(-1.0f, 0.0f));
        _ball.lastTouched = null;
    }

    void Update()
    {
        if (_gameReady)
        {
            _gameReady = false;
            _ball.SetDirection(new Vector3(-1.0f, 0.0f));

            Debug.Log("Granting Authority");
            playerL.ChangeAuthority(true);
            playerR.ChangeAuthority(true);
        }
    }

    void ScoreLChanged(int prev, int newV)
    {
        _scoreLText.text = "" + newV;
    }

    void ScoreRChanged(int prev, int newV)
    {
        _scoreRText.text = "" + newV;
    }
}
