using Newtonsoft.Json.Linq;
using SurfaceGen;
using System;
using UnityEngine;

[RequireComponent(typeof(RobotMover))]
public class RobotTicker : MonoBehaviour
{
    public Vector3 realRobotPosition;
    public Quaternion realRobotRotation;
}