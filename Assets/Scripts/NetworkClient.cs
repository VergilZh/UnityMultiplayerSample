using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using UnityEngine.UIElements;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    //serverIp: 127.0.0.1
    //hostIp:127.0.0.1
    //18.222.83.238
    public ushort serverPort;
    public GameObject playerPrefab;
    public float UID;
    public GameObject myself;
    

    
    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
        UID = UnityEngine.Random.value;
        
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");
        GameObject playerTest = Instantiate(playerPrefab);
        playerTest.AddComponent<PlayerMovement>();
        PlayerJoinMsg m = new PlayerJoinMsg();
        m.PlayerPos = playerTest.transform.position;
        playerTest.GetComponent<PlayerID>().ID = UID;
        myself = playerTest;
        m.uniqueid = UID;
        SendToServer(JsonUtility.ToJson(m));
        //// Example to send a handshake message:
        // HandshakeMsg m = new HandshakeMsg();
        // m.player.id = m_Connection.InternalId.ToString();
        // SendToServer(JsonUtility.ToJson(m));
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.HANDSHAKE:
            HandshakeMsg hsMsg = JsonUtility.FromJson<HandshakeMsg>(recMsg);
            Debug.Log("Handshake message received!");
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.PLAYER_JOIN:
            PlayerJoinMsg pjMsg = JsonUtility.FromJson<PlayerJoinMsg>(recMsg);
            if(UID != pjMsg.uniqueid)
            {
                Instantiate(playerPrefab);
            }
            Debug.Log("Player join message received!");
            break;
            case Commands.PLAYER_MOVEMONT:
            PlayerMovementMsg pmMsg = JsonUtility.FromJson<PlayerMovementMsg>(recMsg);
            Debug.Log("Player movement update message received!");
                foreach(GameObject p in GameObject.FindGameObjectsWithTag("Player"))
                {
                    if(p != myself)
                    {
                        p.transform.position = pmMsg.PlayerPos;
                    }
                }
            break;

            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        if(myself)
        {
            PlayerMovementMsg m = new PlayerMovementMsg();
            m.PlayerPos = myself.transform.position;
            m.uniqueid = UID;
            SendToServer(JsonUtility.ToJson(m));
        }
        

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }
    }
}