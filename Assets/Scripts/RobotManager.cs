using Newtonsoft.Json.Linq;
using SurfaceGen;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Controls;

[DefaultExecutionOrder(-150)]
public class RobotManager : MonoBehaviour
{
    public static RobotManager Instance { get; private set; }
    
    private SurfaceGenerator surfaceGenerator;
    private RoomFinder roomFinder;
    private WebsocketClient websocketClient;

    public List<GameObject> robots = new List<GameObject>();

    //public Transform targetDestination;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        surfaceGenerator = GameObject.FindGameObjectWithTag("Map").GetComponent<SurfaceGenerator>();
        roomFinder = GameObject.FindGameObjectWithTag("Map").GetComponent<RoomFinder>();
    }

    void Start()
    {
        websocketClient = WebsocketClient.Instance;
    }

    public void ReadCameraPosition(WebSocketMessageEventArgs args)
    {
        print(this.name + ": before if");
        if (args.source != "cameras" || (args.source == "cameras" && args.payload == "{\"task\":\"init\"}")) return;
        JObject coordsJson = JObject.Parse(args.payload);
        print("SHIT: "+args.payload);
        
        float payloadX = coordsJson["x"].ToObject<float>();
        float payloadY = coordsJson["y"].ToObject<float>();

        Vector3 referencePosition = new Vector3(payloadX, 0.5f, payloadY);

        var robotMover = robots[int.Parse(coordsJson["robot_id"].ToString().Replace("robot", ""))-1].GetComponent<RobotMover>();

        int i = 0;
        foreach (RoomParameters room in surfaceGenerator.localParameters.roomParameters)
        {
            if (coordsJson["room_id"].ToString() == room.id)
            {
                Vector3 roomCenter = roomFinder.rooms[i].bounds.center;
                Vector3 globalRobotPos = roomCenter + referencePosition;

                Quaternion robotRot = new Quaternion(0, coordsJson["angle"].ToObject<float>(), 0, 0);

                robotMover.UpdateRobotPositionFromIRL(globalRobotPos, robotRot);

                break;
            }
            else
            {
                i++;
            }
        }
    }
}