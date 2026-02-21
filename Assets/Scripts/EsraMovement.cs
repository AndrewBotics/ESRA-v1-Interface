using UnityEngine;
using NativeWebSocket;
using System.Text;

public class EsraMovement : MonoBehaviour {
    [Header("Network Settings")]
    public string serverUrl = "ws://127.0.0.1:8765/";
    
    public bool useAIControl = true;

    [Header("Robot Stats")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public float climbSpeed = 3f;

    // --- INTERNAL STATE ---
    private WebSocket websocket;
    private Rigidbody rb;
    private CapsuleCollider col;
    
    // AI / Input State
    [HideInInspector] public float inputX; 
    [HideInInspector] public float inputY;
    [HideInInspector] public bool inputJump;
    private float actionTimer = 0f; // How long to hold the AI key press

    // Physics State
    public bool isGrounded;
    public bool isClimbing;
    public Collider currentPole;
    public Collider currentPlatform;

    // Timers for buffers
    private float jumpTimer = 0f;       // Cooldown
    private float jumpBufferTimer = 0f; // Input buffering

    async void Start() {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

        // Web Socket connections
        websocket = new WebSocket(serverUrl);
        websocket.OnOpen += () => Debug.Log("Connected to ESRA's Server!");
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
                inputX = 0;
                inputY = 0;
            }
        }

        float moveX = inputX;
        float moveY = inputY;

        // Pressing keys overrides AI to help ESRA
        if (Input.GetAxisRaw("Horizontal") != 0) moveX = Input.GetAxisRaw("Horizontal");
        if (Input.GetAxisRaw("Vertical") != 0) moveY = Input.GetAxisRaw("Vertical");

        // Jump Timers
        if (jumpTimer > 0) jumpTimer -= Time.deltaTime;
        if (jumpBufferTimer > 0) jumpBufferTimer -= Time.deltaTime;

        // Register Jump Intent (AI or Manual)
        if (inputJump || Input.GetKeyDown(KeyCode.UpArrow)) {
            jumpBufferTimer = 0.2f;
            inputJump = false;
        }

        if (moveX != 0) {
            float facingAngle = (moveX > 0) ? 90f : -90f;
            if (transform.childCount > 0) {
                transform.GetChild(0).localRotation = Quaternion.Euler(0, facingAngle, 0);
            }
        }

        // Climbing stuff
        if (currentPole != null && Mathf.Abs(moveY) > 0.1f) {
            isClimbing = true;
            rb.useGravity = false;
        }
        
        if (isClimbing) {
            rb.linearVelocity = new Vector3(0, moveY * climbSpeed, 0);
            
            // Jump off pole
            if (Mathf.Abs(moveX) > 0.1f) {
                isClimbing = false;
                rb.useGravity = true;
                rb.linearVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, 0); 
            }
        }
        else {
            // Standard walking
            rb.linearVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, 0);
            
            // Standard jumping
            if (jumpBufferTimer > 0 && isGrounded && jumpTimer <= 0) {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                jumpTimer = 0.2f;
                jumpBufferTimer = 0f;
            }
        }
    }

    void SendGameState() {
        if (websocket.State == WebSocketState.Open) {
            string groundedStatus = isGrounded ? "true" : "false";
            string json = $"{{\"event\": \"request_decision\", \"is_grounded\": {groundedStatus}}}";
            websocket.SendText(json);
        }
    }

    void ProcessBrainCommand(string json) {
        if (json.Contains("JUMP")) inputJump = true;
        
        if (json.Contains("WALK_RIGHT")) {
            inputX = 1f;
            actionTimer = 1.0f;
        }
        else if (json.Contains("WALK_LEFT")) {
            inputX = -1f;
            actionTimer = 1.0f;
        }
        else if (json.Contains("STOP")) {
            inputX = 0;
            inputY = 0;
            actionTimer = 0;
        }
        
        if (json.Contains("DOWN")) {
            inputY = -1f;
            actionTimer = 0.5f; 
        }
    }

    private async void OnApplicationQuit() {
        if (websocket != null) await websocket.Close();
    }

    // --- COLLISION TRIGGERS ---
    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Platform")) {
            isGrounded = true;
            if (collision.gameObject.CompareTag("Platform")) currentPlatform = collision.collider;
        }
    }

    private void OnCollisionExit(Collision collision) {
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Platform")) {
            isGrounded = false;
            currentPlatform = null;
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Pole")) currentPole = other;
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Pole")) {
            currentPole = null;
            isClimbing = false;
            rb.useGravity = true;
        }
    }
}