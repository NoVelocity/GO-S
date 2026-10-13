using System;
using UnityEngine;

[RequireComponent(typeof(RobotMover))]
public class RobotTiker : MonoBehaviour
{
    private RobotMover robotMover;
    public Transform hui;

    public Vector3 realRobotPosition;
    public Quaternion realRobotRotation;

    private void Awake()
    {
        robotMover = GetComponent<RobotMover>();
    }
    
    private void Start()
    {
        robotMover.GoToNahui(hui.position);
    }

    private void Update()
    {
        robotMover.UpdateRobotPositionFromIRL(realRobotPosition, realRobotRotation);
    }
}