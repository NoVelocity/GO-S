using UnityEngine;

namespace SurfaceGen
{
    [CreateAssetMenu(menuName = "SurfaceGen/Door Parameters")]
    public class DoorParameters : ScriptableObject
    {
        public float offset;
        public float width = 1.5f;
        public float height = 2.4f;
    }
}