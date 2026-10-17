using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RobotMover))]
public class NavMeshToCommand : MonoBehaviour
{
    RobotMover _robotMover;
    WebsocketClient _client;

    enum CommandToSend { None, Stop, Forward, Backward, RotateLeft, RotateRight }

    CommandToSend currentCommand = CommandToSend.None;

    bool sequenceActive = false;

    float lastChangeTime = -999f;
    float lastSendTime = -999f;
    float minCommandInterval = 0.15f;
    float resendCommandInterval = 0f;

    void Awake()
    {
        _robotMover = GetComponent<RobotMover>();
        StartCoroutine(TryAssignClientRoutine());
    }

    void Update()
    {
        CommandToSend wanted = ReadMoverData();

        if (wanted != currentCommand)
        {
            if (wanted == CommandToSend.Stop || Time.time - lastChangeTime >= minCommandInterval)
            {
                ApplyCommand(wanted);
                currentCommand = wanted;
                lastChangeTime = Time.time;
            }
        }
        else if (resendCommandInterval > 0f && sequenceActive && currentCommand != CommandToSend.Stop && Time.time - lastSendTime >= resendCommandInterval)
        {
            SendMovement(currentCommand);
        }
    }

    CommandToSend ReadMoverData()
    {
        switch(_robotMover.robotCommand)
        {
            case "MOVE_FORWARD":
                return CommandToSend.Forward;

            case "ROTATE":
                bool rightMotorForward = _robotMover.motorRightSpeed > 0f;
                return rightMotorForward ? CommandToSend.RotateRight : CommandToSend.RotateLeft;

            case "STOP":
                return CommandToSend.Stop;

            default: 
                return CommandToSend.None;
        }
    }

    private void ApplyCommand(CommandToSend cmd)
    {
        if (cmd == CommandToSend.Stop)
        {
            if (sequenceActive)
            {
                SendBoth(50);   // stop
                SendBoth(62);   // after command sequence
                sequenceActive = false;
            }
            return;
        }

        if (!sequenceActive)
        {
            SendBoth(61);       // before command sequence
            sequenceActive = true;
        }

        SendMovement(cmd);
    }

    void Send(JObject obj)
    {
        string json = obj.ToString(Formatting.None);

        if (_client != null) { _client.SendMessage("robot", json); }
        lastSendTime = Time.time;
    }

    void SendMove(bool rightBackward, bool leftBackward)
    {
        Send(new JObject { ["task"] = 52, ["isRightMotor"] = true, ["backward"] = rightBackward });
        Send(new JObject { ["task"] = 52, ["isRightMotor"] = false, ["backward"] = leftBackward });
    }

    void SendBoth(int task)
    {
        Send(new JObject { ["task"] = task, ["isRightMotor"] = true });
        Send(new JObject { ["task"] = task, ["isRightMotor"] = false });
    }

    void SendMovement(CommandToSend cmd)
    {
        switch (cmd)
        {
            case CommandToSend.Forward: SendMove(rightBackward: false, leftBackward: false); break;
            case CommandToSend.Backward: SendMove(rightBackward: true, leftBackward: true); break;
            case CommandToSend.RotateLeft: SendMove(rightBackward: true, leftBackward: false); break;
            case CommandToSend.RotateRight: SendMove(rightBackward: false, leftBackward: true); break;
        }
    }

    IEnumerator TryAssignClientRoutine()
    {
        while (_client == null)
        {
            _client = WebsocketClient.Instance;

            if (_client == null)
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
}