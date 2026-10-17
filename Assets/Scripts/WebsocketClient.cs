using NativeWebSocket;
using Newtonsoft.Json.Linq;
using System;
using UnityEngine;
using UnityEngine.Events;

public class WebSocketMessageEventArgs : EventArgs
{
    public string id { get; private set; }
    public string source { get; private set; }
    public string payload { get; private set; }

    // Listeners can set this to reject the message. Leave null to reply "ok".
    public string errorCode { get; set; }

    public WebSocketMessageEventArgs(string source, string payload, string id = null)
    {
        this.source = source;
        this.payload = payload;
        this.id = id;
    }
}

[Serializable]
public class WebSocketMessageEvent : UnityEvent<WebSocketMessageEventArgs> { }
[DefaultExecutionOrder(-175)]
public class WebsocketClient : MonoBehaviour
{
    public static WebsocketClient Instance { get; private set; }

    public WebSocketMessageEvent OnMessageReceived = new WebSocketMessageEvent();

    private WebSocket _websocket;

    [SerializeField] string _url = "ws://127.0.0.1:8765/unity";
    [SerializeField] bool _connectOnStart = true; 

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
    }

    async void Start()
    {
        if (_connectOnStart) 
        {
            await ConnectToServer();
        }
    }

    public async System.Threading.Tasks.Task ConnectToServer() 
    {
        _websocket = new WebSocket(_url);

        _websocket.OnOpen += () => print($"WebSocket connected to:\n {_url}");
        _websocket.OnError += (errorText) => Debug.LogError($"WebSocket error:\n {errorText}");
        _websocket.OnClose += (reasonForStopping) => print($"WebSocket connection closed:\n {reasonForStopping}");

        _websocket.OnMessage += (bytes) =>
        {
            string taskId = null;
            try
            {
                string rawMessage = System.Text.Encoding.UTF8.GetString(bytes);

                JObject inboundJson = JObject.Parse(rawMessage);

                taskId = inboundJson["id"]?.ToString();
                print(taskId);
                print(inboundJson["source"]);

                if (inboundJson["result"] != null && inboundJson["source"] == null)
                {
                    string result = inboundJson["result"].ToString();

                    print($"Result for task {taskId}: {result}");

                    return;
                }

                // Incoming task: needs both id and source
                if (taskId == null || inboundJson["source"] == null)
                {
                    Debug.LogWarning($"Ignoring unexpected frame: {rawMessage}");

                    return;
                }

                string sourceStr = inboundJson["source"].ToString();

                if (inboundJson["payload"] == null)
                {
                    SendResult(taskId, "BAD_PAYLOAD");

                    return;
                }

                string payloadStr = inboundJson["payload"].ToString(Newtonsoft.Json.Formatting.None);
                print($"{sourceStr}: {payloadStr}");

                var args = new WebSocketMessageEventArgs(sourceStr, payloadStr, taskId);
                OnMessageReceived.Invoke(args);

                SendResult(taskId, args.errorCode ?? "ok");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing message: {ex}");

                if (taskId != null) SendResult(taskId, "ERR_PROCESSING");
            }
        };

        await _websocket.Connect();
    }

    public async void SendMessage(string destination, string payload)
    {
        if (_websocket != null && _websocket.State == WebSocketState.Open)
        {
            JObject outboundJson = new JObject();
            outboundJson["destination"] = destination;

            try //if this fails
            {
                outboundJson["payload"] = JToken.Parse(payload);
            }
            catch //do this instead
            {
                outboundJson["payload"] = payload;
            }

            string messageToSend = outboundJson.ToString(Newtonsoft.Json.Formatting.None);
            print(messageToSend);

            await _websocket.SendText(messageToSend);
        }
        else
        {
            Debug.LogError("WebSocket error: \nwebsocket isn't open or doesn't exist");
        }
    }

    private async void SendResult(string taskId, string result)
    {
        if (_websocket == null || _websocket.State != WebSocketState.Open)
        {
            Debug.LogError("WebSocket error: \nwebsocket isn't open, can't send result");
            return;
        }

        JObject reply = new JObject();
        reply["id"] = taskId;
        reply["result"] = result;

        await _websocket.SendText(reply.ToString(Newtonsoft.Json.Formatting.None));
    }

    private void Update()
    {
        if (_websocket != null)
        {
            _websocket.DispatchMessageQueue();
        }
    }

    private async void OnApplicationQuit()
    {
        if (_websocket != null)
        {
            await _websocket.Close();
        }
    }
}