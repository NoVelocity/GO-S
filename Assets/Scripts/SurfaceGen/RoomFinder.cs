using System.Collections.Generic;
using UnityEngine;

namespace SurfaceGen
{
    public class RoomFinder : MonoBehaviour
    {
        public Renderer floor;
        public float cellSize = 0.5f;
        public float checkHeight = 1f;
        public Transform wallsRoot;

        public List<Bounds> DoorSeals { get; } = new();

        public class Room
        {
            public List<Vector2Int> cells = new();
            public Bounds bounds;
            public Vector3 center;
            public float width;
            public float depth;
            public float area;
        }

        public List<Room> rooms = new();

        [ContextMenu("Find Rooms")]
        public void FindRooms()
        {
            rooms.Clear();

            Bounds b = floor.bounds;
            int w = Mathf.CeilToInt(b.size.x / cellSize);
            int h = Mathf.CeilToInt(b.size.z / cellSize);

            bool[,] blocked = new bool[w, h];

            var walls = new List<Bounds>();
            foreach (var r in wallsRoot.GetComponentsInChildren<Renderer>())
                if (r != floor)
                    walls.Add(r.bounds);
            walls.AddRange(DoorSeals);

            float pad = cellSize * 0.5f;

            for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
            {
                float px = b.min.x + (x + 0.5f) * cellSize;
                float pz = b.min.z + (z + 0.5f) * cellSize;

                foreach (var wb in walls)
                {
                    if (px >= wb.min.x - pad && px <= wb.max.x + pad &&
                        pz >= wb.min.z - pad && pz <= wb.max.z + pad)
                    {
                        blocked[x, z] = true;
                        break;
                    }
                }
            }

            bool[,] visited = new bool[w, h];
            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
            {
                if (blocked[x, z] || visited[x, z]) continue;

                var room = new Room();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(new Vector2Int(x, z));
                visited[x, z] = true;

                int minX = x, maxX = x, minZ = z, maxZ = z;

                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    room.cells.Add(c);

                    minX = Mathf.Min(minX, c.x);
                    maxX = Mathf.Max(maxX, c.x);
                    minZ = Mathf.Min(minZ, c.y);
                    maxZ = Mathf.Max(maxZ, c.y);

                    foreach (var d in dirs)
                    {
                        int nx = c.x + d.x, nz = c.y + d.y;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                        if (blocked[nx, nz] || visited[nx, nz]) continue;
                        visited[nx, nz] = true;
                        queue.Enqueue(new Vector2Int(nx, nz));
                    }
                }

                int bboxCells = (maxX - minX + 1) * (maxZ - minZ + 1);
                if (bboxCells != room.cells.Count)
                    Debug.LogWarning($"Room {rooms.Count} isn't a rectangle: " +
                                     $"{room.cells.Count} cells from {bboxCells} in rectangle");

                float x0 = b.min.x + minX * cellSize;
                float x1 = b.min.x + (maxX + 1) * cellSize;
                float z0 = b.min.z + minZ * cellSize;
                float z1 = b.min.z + (maxZ + 1) * cellSize;

                SnapToWalls(ref x0, ref x1, ref z0, ref z1, walls);

                room.bounds = new Bounds(
                    new Vector3((x0 + x1) * 0.5f, b.min.y, (z0 + z1) * 0.5f),
                    new Vector3(x1 - x0, 0f, z1 - z0));
                room.center = room.bounds.center;
                room.width = x1 - x0;
                room.depth = z1 - z0;
                room.area = room.width * room.depth;
                rooms.Add(room);
            }

            Debug.Log($"Rooms: {rooms.Count}");
        }

        void SnapToWalls(ref float x0, ref float x1, ref float z0, ref float z1, List<Bounds> walls)
        {
            const float eps = 0.001f;
            float c = cellSize;

            float left = x0, right = x1, back = z0, front = z1;
            float bestL = float.NegativeInfinity, bestR = float.PositiveInfinity;
            float bestB = float.NegativeInfinity, bestF = float.PositiveInfinity;

            foreach (var wb in walls)
            {
                bool overlapX = wb.max.x > x0 + eps && wb.min.x < x1 - eps;
                bool overlapZ = wb.max.z > z0 + eps && wb.min.z < z1 - eps;
                bool alongZ = wb.size.x < wb.size.z;

                if (alongZ && overlapZ)
                {
                    if (wb.max.x >= x0 - c - eps && wb.max.x <= x0 + eps) bestL = Mathf.Max(bestL, wb.max.x);
                    if (wb.min.x <= x1 + c + eps && wb.min.x >= x1 - eps) bestR = Mathf.Min(bestR, wb.min.x);
                }
                else if (!alongZ && overlapX)
                {
                    if (wb.max.z >= z0 - c - eps && wb.max.z <= z0 + eps) bestB = Mathf.Max(bestB, wb.max.z);
                    if (wb.min.z <= z1 + c + eps && wb.min.z >= z1 - eps) bestF = Mathf.Min(bestF, wb.min.z);
                }
            }

            if (!float.IsInfinity(bestL)) left = bestL;
            if (!float.IsInfinity(bestR)) right = bestR;
            if (!float.IsInfinity(bestB)) back = bestB;
            if (!float.IsInfinity(bestF)) front = bestF;

            x0 = left;
            x1 = right;
            z0 = back;
            z1 = front;
        }

        void OnDrawGizmos()
        {
            if (rooms == null) return;
            Random.InitState(1);
            foreach (var r in rooms)
            {
                Color col = Color.HSVToRGB(Random.value, 0.8f, 1f);
                Gizmos.color = new Color(col.r, col.g, col.b, 0.5f);
                Gizmos.DrawCube(r.bounds.center + Vector3.up * 0.05f,
                    new Vector3(r.width, 0.02f, r.depth));
                Gizmos.color = col;
                Gizmos.DrawWireCube(r.bounds.center + Vector3.up * 0.05f,
                    new Vector3(r.width, 0.02f, r.depth));
            }

            Gizmos.color = Color.cyan;
            foreach (var d in DoorSeals)
                Gizmos.DrawWireCube(d.center, d.size);
        }

        public RoomParameters[] GetRoomsAsRoomParameters(float h)
        {
            RoomParameters[] list = new RoomParameters[rooms.Count];

            int absoluteCounter = 0;

            for (int i = 0; i < rooms.Count; i++)
            {
                RoomParameters r = ScriptableObject.CreateInstance<RoomParameters>();
                r.id = "room" + i;
                for (int j = 0; j < 4; j++)
                {
                    r.markers[j] = absoluteCounter++;
                }

                r.size = new Vector3(rooms[i].width, h, rooms[i].depth);

                list[i] = r;
            }

            return list;
        }
    }
}