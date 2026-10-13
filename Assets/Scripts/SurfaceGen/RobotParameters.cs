using UnityEngine;

namespace SurfaceGen
{
    [CreateAssetMenu(menuName = "SurfaceGen/Robot Parameters")]
    public class RobotParameters : ScriptableObject
    {
        public string id;
        public Vector2 home;
        public int northMarker, southMarker, eastMarker, westMarker;
    }
}