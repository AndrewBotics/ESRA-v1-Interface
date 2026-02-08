using UnityEngine;

public class EsraMovement : MonoBehaviour {
    [Header("Robot Stats")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public float climbSpeed = 3f;
    
    [Header("AI Control")]
    public bool useAIControl = true;
    
    [HideInInspector] public float inputX; 
    [HideInInspector] public float inputY;
    [HideInInspector] public bool inputJump;

    public Rigidbody rb;
    public CapsuleCollider col;
    public bool isGrounded;
    public bool isClimbing;
    public Collider currentPole;
    public Collider currentPlatform;

    public float jumpTimer = 0f;

    public float jumpBufferTimer = 0f;

    void Start() {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    void Update() {
        float moveX = inputX;
        float moveY = inputY;

        if (Input.GetAxisRaw("Horizontal") != 0) moveX = Input.GetAxisRaw("Horizontal");
        if (Input.GetAxisRaw("Vertical") != 0) moveY = Input.GetAxisRaw("Vertical");

        if (jumpTimer > 0) jumpTimer -= Time.deltaTime;
        if (jumpBufferTimer > 0) jumpBufferTimer -= Time.deltaTime;

        if (inputJump || Input.GetKeyDown(KeyCode.UpArrow)) {
            jumpBufferTimer = 0.2f;
            inputJump = false;
        }

        if (currentPole != null && Mathf.Abs(moveY) > 0.1f) {
            isClimbing = true;
            rb.useGravity = false;
        }
        
        if (isClimbing) {
            rb.linearVelocity = new Vector3(0, moveY * climbSpeed, 0);
            
            if (Mathf.Abs(moveX) > 0.1f) {
                isClimbing = false;
                rb.useGravity = true;
                rb.linearVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, 0); 
            }
        }
        else {
            rb.linearVelocity = new Vector3(moveX * moveSpeed, rb.linearVelocity.y, 0);
            
            if (jumpBufferTimer > 0 && isGrounded && jumpTimer <= 0) {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                jumpTimer = 0.2f;
                jumpBufferTimer = 0f;
            }
        }
    }
    private void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Platform")) {
            isGrounded = true;
            if (collision.gameObject.CompareTag("Platform")) {
                currentPlatform = collision.collider;
            }
        }
    }

    private void OnCollisionExit(Collision collision) {
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Platform")) {
            isGrounded = false;
            currentPlatform = null;
        }
    }

    private void OnTriggerEnter(Collider other) {
        if (other.CompareTag("Pole")) {
            currentPole = other;
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.CompareTag("Pole")) {
            currentPole = null;
            isClimbing = false;
            rb.useGravity = true;
        }
    }
}