using UnityEngine;
using NativeWebSocket;
using System.Text;

public class EsraLLMConnection : MonoBehaviour {
    WebSocket websocket;
    public string serverUrl = "ws://localhost:8765";
    
    public EsraMovement characterController; 

    public float actionTimer = 0f;

    async void Start() {
        characterController = GetComponent<EsraMovement>();
        websocket = new WebSocket(serverUrl);

        websocket.OnOpen += () => Debug.Log("Connected to ESRA Brain!");
        websocket.OnError += (e) => Debug.Log("Error! " + e);
        websocket.OnClose += (e) => Debug.Log("Connection closed!");

        websocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            ProcessBrainCommand(message);
        };

        await websocket.Connect();
        
        InvokeRepeating("SendGameState", 1.0f, 0.5f);
    }

    void Update() {
        #if !UNITY_WEBGL || UNITY_EDITOR
            websocket.DispatchMessageQueue();
        #endif

        if (actionTimer > 0) {
            actionTimer -= Time.deltaTime;
            if (actionTimer <= 0) {
                characterController.inputX = 0;
                characterController.inputY = 0;
            }
        }
    }

    void SendGameState() {
    if (websocket.State == WebSocketState.Open) {
        string groundedStatus = characterController.isGrounded ? "true" : "false";
        string json = $"{{\"event\": \"request_decision\", \"is_grounded\": {groundedStatus}}}";
        
        websocket.SendText(json);
    }
}

    void ProcessBrainCommand(string json) {
        if (json.Contains("JUMP")) {
            characterController.inputJump = true;
        }
        
        if (json.Contains("WALK_RIGHT")) {
            characterController.inputX = 1f;
            actionTimer = 1.0f; // Hold key for 1 sec
        }
        else if (json.Contains("WALK_LEFT")) {
            characterController.inputX = -1f;
            actionTimer = 1.0f;
        }
        else if (json.Contains("STOP")) {
            characterController.inputX = 0;
            characterController.inputY = 0;
            actionTimer = 0;
        }
        
        if (json.Contains("DOWN")) {
            characterController.inputY = -1f;
            actionTimer = 0.5f; 
        }
    }

    private async void OnApplicationQuit()
    {
        await websocket.Close();
    }
}