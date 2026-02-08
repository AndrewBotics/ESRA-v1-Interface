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

    private Rigidbody rb;
    private CapsuleCollider col;
    private bool isGrounded;
    private bool isClimbing;
    private Collider currentPole;
    private Collider currentPlatform;

    void Start() {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<CapsuleCollider>();
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
    }

    void Update() {
        if (!useAIControl) {
            inputX = Input.GetAxisRaw("Horizontal");
            inputY = Input.GetAxisRaw("Vertical");
            inputJump = Input.GetKeyDown(KeyCode.UpArrow);
        }
        if (currentPole != null && Mathf.Abs(inputY) > 0.1f) {
            isClimbing = true;
            rb.useGravity = false;
        }
        
        if (isClimbing) {
            rb.linearVelocity = new Vector3(0, inputY * climbSpeed, 0);
            if (Mathf.Abs(inputX) > 0.1f) {
                isClimbing = false;
                rb.useGravity = true;
                rb.linearVelocity = new Vector3(inputX * moveSpeed, rb.linearVelocity.y, 0); 
            }
        }
        else {
            rb.linearVelocity = new Vector3(inputX * moveSpeed, rb.linearVelocity.y, 0);
            
            if (inputJump && isGrounded) {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
                inputJump = false;
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