using UnityEngine;
using System.Collections;

public class EsraMovement : MonoBehaviour {
    [Header("Robot Stats")]
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public float climbSpeed = 3f;
    
    // State tracking
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
        float hInput = Input.GetAxisRaw("Horizontal"); // -1 = Left, 0 = Null, 1 = Right
        float vInput = Input.GetAxisRaw("Vertical");   // -1 = Down, 0 = Null, 1 = Up

        // Climbing
        if (currentPole != null && Mathf.Abs(vInput)>0.1f) {
            isClimbing = true;
            rb.useGravity = false;
        }
        
        if (isClimbing) {
            rb.linearVelocity = new Vector3(0, vInput * climbSpeed, 0);
            if (Mathf.Abs(hInput) > 0.1f){
                isClimbing = false;
                rb.useGravity = true;
            }
        }
        else {
            rb.linearVelocity = new Vector3(hInput*moveSpeed, rb.linearVelocity.y, 0);
            if (Input.GetKeyDown(KeyCode.UpArrow) && isGrounded) {
                rb.AddForce(Vector3.up*jumpForce, ForceMode.Impulse);
            }
        }
    }

    // Collisions for Poles/Ground
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

    // Pole Stuff
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