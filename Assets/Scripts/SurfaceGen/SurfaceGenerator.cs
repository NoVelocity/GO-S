using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace SurfaceGen
{
    [DefaultExecutionOrder(-100)]
    public class SurfaceGenerator : MonoBehaviour
    {
        [Header("Colors")] public Color surfaceColor;
        public Color outerWallsColor;
        public Color innerWallsColor;

        [Header("Parameters")] public SurfaceParameters localParameters;
        public GameObject wallPrefab;

        public NavMeshSurface navMeshSurface;

        private const float MinPiece = 0.01f;
        private const float ProbeDistance = 1f;
        private const float ProbeSnapDistance = 1f;

        [SerializeField]
        GameObject robotPrefab;

        private struct Opening
        {
            public string name;
            public float start, end, height;
        }

        private struct DoorProbe
        {
            public string name;
            public Vector3 sideA, sideB;
        }

        private readonly List<DoorProbe> doorProbes = new();

        private RoomFinder finder;

        private void Awake()
        {
            finder = GetComponent<RoomFinder>();

            GenerateSurface();

            finder.FindRooms();
            localParameters.roomParameters = finder.GetRoomsAsRoomParameters(localParameters.scale.y);

            if (localParameters.robotParameters.Length > 0)
            {
                foreach (RobotParameters robot in localParameters.robotParameters)
                {
                    Vector3 spawnPos = new Vector3(robot.home.x, 0.5f, robot.home.y);
                    Quaternion spawnRot = robotPrefab.transform.rotation;

                    var newRobot = Instantiate(robotPrefab, spawnPos, spawnRot);

                    var newRobotMover = newRobot.GetComponent<RobotMover>();
                    newRobotMover.robotId = robot.id;

                    var newRobotTicker = newRobot.GetComponent<RobotTicker>();
                    newRobotTicker.realRobotPosition = spawnPos;
                    newRobotTicker.realRobotRotation = spawnRot;
                }
            }
        }

        private void Start()
        {
            RebuildNavMesh();
        }

        public void OnCamerasInit(WebSocketMessageEventArgs e)
        {
            if (e.source is "tracker" or "cameras" && JObject.Parse(e.payload)["task"]?.ToString() == "init")
            {
                string payload =
                    new CamerasConfiguration(localParameters.roomParameters, localParameters.robotParameters)
                        .ToString();

                WebsocketClient.Instance.SendMessage(e.source, payload);
                print(payload);
            }
        }

        void GenerateSurface()
        {
            finder.DoorSeals.Clear();
            doorProbes.Clear();

            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Plane);
            surface.transform.parent = transform;
            surface.transform.localScale = localParameters.scale / 10;
            surface.GetComponent<Renderer>().material.color = surfaceColor;
            finder.floor = surface.GetComponent<Renderer>();

            for (int i = 0; i < 4; i++)
            {
                GameObject outerWall = Instantiate(wallPrefab, transform, true);
                outerWall.name = "outerWall" + i;

                Vector2[] pos =
                {
                    new(0, localParameters.scale.z / 2),
                    new(localParameters.scale.x / 2, 0),
                    new(0, -(localParameters.scale.z / 2)),
                    new(-(localParameters.scale.x / 2), 0)
                };

                outerWall.transform.localScale = new Vector3(
                    i % 2 == 0 ? localParameters.scale.x : localParameters.scale.z,
                    localParameters.scale.y,
                    1
                );
                outerWall.transform.position = new Vector3(
                    pos[i].x,
                    localParameters.scale.y / 2,
                    pos[i].y
                );
                outerWall.transform.Rotate(0, i % 2 != 0 ? 90f : 0, 0);

                outerWall.GetComponent<WallRender>().wallColor = outerWallsColor;
            }

            foreach (var wall in localParameters.wallParameters)
                BuildInnerWall(wall);
        }

        void BuildInnerWall(WallParameters wall)
        {
            float wallHeight = localParameters.scale.y;

            float dx = wall.endPoint.x - wall.startPoint.x;
            float dz = wall.endPoint.y - wall.startPoint.y;
            bool alongZ = Mathf.Abs(dx) < Mathf.Abs(dz);
            float length = alongZ ? Mathf.Abs(dz) : Mathf.Abs(dx);

            if (length < MinPiece)
            {
                Debug.LogWarning($"Wall '{wall.name}' has zero length, skipped", wall);
                return;
            }

            if (Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dz)) > 0.001f)
                Debug.LogWarning($"Wall '{wall.name}' is not axis-aligned, built along {(alongZ ? "Z" : "X")}", wall);

            Vector2 center = wall.startPoint / 2f;
            float line = alongZ ? center.x : center.y;
            float mid = alongZ ? center.y : center.x;
            float wallStart = mid - length / 2f;
            float wallEnd = mid + length / 2f;

            float direction = (alongZ ? dz : dx) >= 0f ? 1f : -1f;

            var openings = CollectOpenings(wall, wallStart, wallEnd, direction);
            Debug.Log($"Wall '{wall.name}': {openings.Count} door opening(s)");

            float cursor = wallStart;
            int index = 0;
            foreach (var o in openings)
            {
                SpawnWallPiece($"{wall.name}_{index}", alongZ, line, cursor, o.start, 0f, wallHeight);

                if (o.height < wallHeight - MinPiece)
                    SpawnWallPiece($"{wall.name}_{index}_lintel", alongZ, line, o.start, o.end, o.height, wallHeight);

                RegisterDoorSeal(alongZ, line, o.start, o.end, wallHeight);
                RegisterDoorProbe(o.name, alongZ, line, (o.start + o.end) / 2f);

                cursor = o.end;
                index++;
            }

            SpawnWallPiece($"{wall.name}_{index}", alongZ, line, cursor, wallEnd, 0f, wallHeight);
        }

        List<Opening> CollectOpenings(WallParameters wall, float wallStart, float wallEnd, float direction)
        {
            var sorted = new List<Opening>();
            if (wall.doors == null) return sorted;

            float startEnd = direction > 0f ? wallStart : wallEnd;

            foreach (var door in wall.doors)
            {
                if (door == null) continue;
                ValidateDoor(wall, door);

                float doorCenter = startEnd + direction * door.offset;
                float want0 = doorCenter - door.width / 2f;
                float want1 = doorCenter + door.width / 2f;
                float a0 = Mathf.Max(wallStart, want0);
                float a1 = Mathf.Min(wallEnd, want1);

                if (a1 - a0 < MinPiece)
                {
                    Debug.LogWarning($"Door '{door.name}' is outside wall '{wall.name}' " +
                                     $"(wall length {wallEnd - wallStart}, door offset {door.offset}), skipped", door);
                    continue;
                }

                if (a0 > want0 + 0.001f || a1 < want1 - 0.001f)
                    Debug.LogWarning($"Door '{door.name}' is clipped by the edge of wall '{wall.name}'", door);

                sorted.Add(new Opening { name = door.name, start = a0, end = a1, height = door.height });
            }

            sorted.Sort((a, b) => a.start.CompareTo(b.start));

            var result = new List<Opening>();
            foreach (var o in sorted)
            {
                if (result.Count > 0 && o.start < result[result.Count - 1].end - 0.001f)
                {
                    Debug.LogWarning($"Doors of wall '{wall.name}' overlap, door '{o.name}' skipped", wall);
                    continue;
                }

                result.Add(o);
            }

            return result;
        }

        void SpawnWallPiece(string pieceName, bool alongZ, float line, float a0, float a1, float y0, float y1)
        {
            float len = a1 - a0;
            if (len < MinPiece || y1 - y0 < MinPiece) return;

            float along = (a0 + a1) / 2f;
            float y = (y0 + y1) / 2f;

            GameObject piece = Instantiate(wallPrefab, transform, true);
            piece.name = pieceName;
            piece.GetComponent<WallRender>().wallColor = innerWallsColor;

            piece.transform.position = alongZ ? new Vector3(line, y, along) : new Vector3(along, y, line);
            piece.transform.localScale = new Vector3(len, y1 - y0, 1);
            piece.transform.rotation = Quaternion.Euler(0, alongZ ? 90f : 0f, 0);
        }

        void RegisterDoorSeal(bool alongZ, float line, float a0, float a1, float wallHeight)
        {
            float along = (a0 + a1) / 2f;
            float len = a1 - a0;

            finder.DoorSeals.Add(alongZ
                ? new Bounds(new Vector3(line, wallHeight / 2f, along), new Vector3(0, wallHeight, len))
                : new Bounds(new Vector3(along, wallHeight / 2f, line), new Vector3(len, wallHeight, 0)));
        }

        void RegisterDoorProbe(string doorName, bool alongZ, float line, float along)
        {
            doorProbes.Add(new DoorProbe
            {
                name = doorName,
                sideA = alongZ
                    ? new Vector3(line - ProbeDistance, 0f, along)
                    : new Vector3(along, 0f, line - ProbeDistance),
                sideB = alongZ
                    ? new Vector3(line + ProbeDistance, 0f, along)
                    : new Vector3(along, 0f, line + ProbeDistance)
            });
        }

        void ValidateDoor(WallParameters wall, DoorParameters door)
        {
            if (navMeshSurface == null) return;

            var agent = NavMesh.GetSettingsByID(navMeshSurface.agentTypeID);

            if (door.width < agent.agentRadius * 3f)
                Debug.LogWarning($"Door '{door.name}' ({wall.name}): width {door.width} is too small for agent " +
                                 $"radius {agent.agentRadius}, NavMesh may not pass through the opening", door);

            if (door.height < localParameters.scale.y && door.height < agent.agentHeight + 0.1f)
                Debug.LogWarning($"Door '{door.name}' ({wall.name}): height {door.height} is not above agent " +
                                 $"height {agent.agentHeight}, there will be no NavMesh under the lintel", door);
        }

        void RebuildNavMesh()
        {
            if (navMeshSurface == null) return;

            navMeshSurface.BuildNavMesh();
            Debug.Log($"NavMesh rebuilt: {NavMesh.CalculateTriangulation().indices.Length / 3} triangles");

            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            {
                if (!agent.enabled || agent.isOnNavMesh) continue;

                if (!agent.Warp(agent.transform.position))
                    Debug.LogWarning($"Agent '{agent.name}' could not be placed on the rebuilt NavMesh " +
                                     "(too far from it?)", agent);
            }

            VerifyDoors();
        }
        
        void VerifyDoors()
        {
            var path = new NavMeshPath();

            foreach (var probe in doorProbes)
            {
                NavMeshHit hitA, hitB;
                bool foundA = NavMesh.SamplePosition(probe.sideA, out hitA, ProbeSnapDistance, NavMesh.AllAreas);
                bool foundB = NavMesh.SamplePosition(probe.sideB, out hitB, ProbeSnapDistance, NavMesh.AllAreas);

                if (!foundA || !foundB)
                {
                    Debug.LogWarning($"Door '{probe.name}': no NavMesh found next to the doorway");
                    continue;
                }

                bool reachable = NavMesh.CalculatePath(hitA.position, hitB.position, NavMesh.AllAreas, path)
                                 && path.status == NavMeshPathStatus.PathComplete;

                if (reachable)
                    Debug.Log($"Door '{probe.name}': NavMesh path through the doorway is OK");
                else
                    Debug.LogWarning($"Door '{probe.name}': NavMesh has NO path through the doorway " +
                                     "(check door width/height and agent settings)");
            }
        }

        private void OnDrawGizmos()
        {
            foreach (var robot in localParameters.robotParameters)
            {
                Color col = Color.HSVToRGB(Math.Abs(robot.home.y / 255), Math.Abs(robot.home.x / 255), 1);
                Gizmos.color = col;
                Gizmos.DrawCube(new Vector3(robot.home.x, 0.5f, robot.home.y), new Vector3(1f, 1f, 1f));
            }
        }
    }
}