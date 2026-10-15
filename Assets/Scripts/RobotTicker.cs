using Newtonsoft.Json.Linq;
using SurfaceGen;
using System;
using UnityEngine;

[RequireComponent(typeof(RobotMover))]
public class RobotTicker : MonoBehaviour
{
    private RobotMover robotMover;
    private SurfaceGenerator surfaceGenerator;
    private RoomFinder roomFinder;
    private WebsocketClient websocketClient;
    public Transform targetDestination;

    public Vector3 realRobotPosition;
    public Quaternion realRobotRotation;

    private void Awake()
    {
        robotMover = GetComponent<RobotMover>();
        surfaceGenerator = GameObject.FindGameObjectWithTag("Map").GetComponent<SurfaceGenerator>();
        roomFinder = GameObject.FindGameObjectWithTag("Map").GetComponent<RoomFinder>();
    }
    
    private void Start()
    {
        if (targetDestination != null)
        {
            robotMover.SetNavMeshDestination(targetDestination.position); //set the target to agent so unity's navmesh starts giving us a direction
        }
        else
        {
            targetDestination = this.transform;
        }
        websocketClient = WebsocketClient.Instance;

        websocketClient.OnMessageReceived.AddListener(ReadCameraPosition);
    }

    public void ReadCameraPosition(WebSocketMessageEventArgs args)
    {
        JObject coordsJson = JObject.Parse(args.payload);

        if (coordsJson["robot_id"].ToString() == robotMover.robotId)
        {
            float payloadX = coordsJson["x"].ToObject<float>();
            float payloadY = coordsJson["y"].ToObject<float>();

            Vector3 referencePosition = new Vector3(payloadX, 0.5f, payloadY);

            int i = 0;
            foreach (RoomParameters room in surfaceGenerator.localParameters.roomParameters)
            {
                if (coordsJson["room_id"].ToString() == room.id)
                {
                    Vector3 roomCenter = roomFinder.rooms[i].bounds.center;
                    Vector3 globalRobotPos = roomCenter + referencePosition;

                    robotMover.UpdateRobotPositionFromIRL(globalRobotPos, realRobotRotation);

                    break;
                }
                else
                {
                    i++;
                }
            }
        }
    }
}