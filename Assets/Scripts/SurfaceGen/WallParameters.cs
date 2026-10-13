using UnityEngine;

namespace SurfaceGen
{
    [CreateAssetMenu(menuName = "SurfaceGen/Wall Parameters")]
    public class WallParameters : ScriptableObject
    {
        public Vector2 startPoint;
        public Vector2 endPoint;
        public DoorParameters[] doors;
    }
}