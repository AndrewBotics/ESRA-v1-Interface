using UnityEngine;
using NativeWebSocket;
using System.Collections;
using System.Text;
using System.Threading.Tasks;

public class EsraMovement : MonoBehaviour {
    [Header("Network Settings")]
    public string serverUrl = "ws://127.0.0.1:8765/";
    public Animator esraAnimator;
    public bool useAIControl = true;
    private bool isReconnecting = false;

    [Header("Robot Stats")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public float climbSpeed = 3f;
    public float teleportHeight = 7.5f;
    public float teleportDelay = 0.1f;

    // --- INTERNAL STATE ---
    private WebSocket websocket;
    private Rigidbody rb;
    private CapsuleCollider col;
    
    // AI / Input State
    [HideInInspector] public float inputX; 
    [HideInInspector] public float inputY;
    [HideInInspector] public bool inputJump;
    [HideInInspector] public float inputTeleport;
    private float actionTimer = 0f; // How long to hold the AI key press

    // Physics State
    public bool isGrounded;
    public bool isClimbing;
    public Collider currentPole;
    public Collider currentPlatform;
    public Transform esraVisuals;
    public int layer = 0;

    // Timers for buffers
    private float jumpTimer = 0f;       // Cooldown
    private float jumpBufferTimer = 0f; // Input buffering
    private float teleportTimer = 0f;

    // Other Game Objects
    public GameObject TeleportYellow;
    public GameObject TeleportGreen;

    async void Start() {
        // ESRA Sprite Handling
        esraVisuals = transform.GetChild(0);
        esraAnimator = esraVisuals.GetComponent<Animator>();
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

        InvokeRepeating("SendGameState", 1.0f, 0.5f);

        // Connect once the first time
        await ConnectToBrain();
    }

    private async Task ConnectToBrain() {
        if (websocket != null && websocket.State == WebSocketState.Open) return;

        websocket = new WebSocket(serverUrl);
        
        websocket.OnOpen += () => {
            Debug.Log(">>> Connected to ESRA's Server!");
            isReconnecting = false;
        };
        
        websocket.OnError += (e) => Debug.Log("Error! " + e);
        
        websocket.OnClose += (e) => {
            Debug.LogWarning(">>> Connection closed! Triggering safety stop.");
            SafetyStop();
            
            if (!isReconnecting && Application.isPlaying) {
                StartCoroutine(ReconnectSequence());
            }
        };
        
        websocket.OnMessage += (bytes) => {
            string message = Encoding.UTF8.GetString(bytes);
            ProcessBrainCommand(message);
        };

        try {
            await websocket.Connect();
        } catch (System.Exception e) {
            Debug.LogError(">>> WebSocket Connect() threw: " + e.Message);
            // If the initial connection fails, force the reconnect loop
            if (!isReconnecting && Application.isPlaying) {
                StartCoroutine(ReconnectSequence());
            }
        }
    }

    private IEnumerator ReconnectSequence() {
        isReconnecting = true;
        
        while (websocket == null || websocket.State != WebSocketState.Open) {
            Debug.Log("Attempting to wake ESRA's brain in 3 seconds...");
            yield return new WaitForSeconds(3f);
            
            _ = ConnectToBrain();
            
            yield return new WaitForSeconds(1f); 
        }
    }

    private void SafetyStop() {
        // Clear all AI inputs so she doesn't get stuck doing the last commanded action forever
        inputX = 0;
        inputY = 0;
        inputJump = false;
        inputTeleport = 0;
        actionTimer = 0;
    }

    void Update() {
        #if UNITY_EDITOR || !UNITY_WEBGL
            if (websocket != null && websocket.State == WebSocketState.Open) {
                websocket.DispatchMessageQueue();
            }
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

        // Timers
        if (jumpTimer > 0) jumpTimer -= Time.deltaTime;
        if (jumpBufferTimer > 0) jumpBufferTimer -= Time.deltaTime;
        if (teleportTimer > 0) teleportTimer -= Time.deltaTime;

        // Register Jump Intent (AI or Manual)
        if (inputJump || Input.GetKeyDown(KeyCode.Space)) {
            jumpBufferTimer = 0.2f;
            inputJump = false;
        }

        // Register Teleport Intent (AI or Manual)
        if (layer<2 && (inputTeleport>0 || Input.GetKeyDown(KeyCode.UpArrow))) {
            Vector3 targetPos = transform.position + Vector3.up * teleportHeight;
            TeleportTo(targetPos);
            teleportTimer = 0.5f;
            inputTeleport = 0f;
            layer++;
        }
        if (layer>0 && (inputTeleport<0 || Input.GetKeyDown(KeyCode.DownArrow))) {
            Vector3 targetPos = transform.position + Vector3.down * teleportHeight * 0.7f;
            TeleportTo(targetPos);
            teleportTimer = 0.5f;
            inputTeleport = 0f;
            layer--;
        }


        if (moveX != 0) {
            float facingAngle = (moveX>0) ? 90f : -90f;
            esraVisuals.localRotation = Quaternion.Euler(0, facingAngle, 0);
            if (isGrounded && !isClimbing) PlayAnimation("WalkAnimation");
        }

        // Standard walking
        rb.linearVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, 0);
        
        // Standard jumping
        if (jumpBufferTimer > 0 && isGrounded && jumpTimer <= 0) {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            jumpTimer = 0.2f;
            jumpBufferTimer = 0f;
        }
    }

    private void SendGameState() {
        if (websocket!=null && websocket.State == WebSocketState.Open) {
            string groundedStatus = isGrounded ? "true" : "false";
            string json = $"{{\"event\": \"request_decision\", \"is_grounded\": {groundedStatus}}}";
            websocket.SendText(json);
        }
    }

    private void ProcessBrainCommand(string json) {
        float duration = 2.0f;
        if (json.Contains("\"duration\":")) {
            string[] parts = json.Split(new string[]{"\"duration\":"}, System.StringSplitOptions.None);
            if (parts.Length > 1) {
                float.TryParse(parts[1].TrimEnd('}').Trim(), out duration);
            }
        }

        if (json.Contains("JUMP")) inputJump = true;
        if (json.Contains("WALK_RIGHT")) {
            inputX = 1f;
            actionTimer = duration;
        }
        else if (json.Contains("WALK_LEFT")) {
            inputX = -1f;
            actionTimer = duration;
        }
        else if (json.Contains("STOP")) {
            inputX = 0;
            inputY = 0;
            actionTimer = 0;
        }
        else if (json.Contains("FACE_FRONT")){
            esraVisuals.localRotation = Quaternion.Euler(0, 180f, 0);
            PlayAnimation("WaveAnimation");
        }
        else if (json.Contains("TELEPORT_UP")){
            inputTeleport = 1f;
            actionTimer = duration;
        }
        else if (json.Contains("TELEPORT_DOWN")){
            inputTeleport = -1f;
            actionTimer = duration;
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

    public void PlayAnimation(string newState, bool forceRestart = false){
        if (esraAnimator == null) {
            Debug.LogWarning(">>> Animator Missing!");
            return;
        }
        
        AnimatorStateInfo currentInfo = esraAnimator.GetCurrentAnimatorStateInfo(0);

        if (currentInfo.IsName(newState)){
            if (!forceRestart && currentInfo.normalizedTime<1.0f) return;
        }
        
        esraAnimator.Play(newState, 0, 0f);
    }

    public void Glitch(Vector3 position){
        Instantiate(TeleportGreen, position, Quaternion.identity);
        Instantiate(TeleportYellow, position, Quaternion.identity);
    }

    public void TeleportTo(Vector3 destination){
        StartCoroutine(TeleportSequence(destination));
    }
    
    public IEnumerator TeleportSequence(Vector3 destination){
        Glitch(transform.position);
        yield return new WaitForSeconds(teleportDelay);
        transform.position = destination;
        Glitch(transform.position);
    }
}