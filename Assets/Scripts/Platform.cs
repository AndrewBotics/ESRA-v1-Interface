using UnityEngine;

public class Platform : MonoBehaviour
{
    private Collider platformCollider;
    private Collider playerCollider;
    private Rigidbody playerRb;
    private EsraMovement playerScript;

    private float buffer = 0.2f; 

    void Start() {
        platformCollider = GetComponent<Collider>();
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerCollider = player.GetComponent<Collider>();
        playerRb = player.GetComponent<Rigidbody>();
        playerScript = player.GetComponent<EsraMovement>();
    }

    void Update() {
        if (playerCollider == null || playerRb == null) return;

        float playerFeet = playerCollider.bounds.min.y;
        float platformTop = platformCollider.bounds.max.y;
        
        bool isAbove = playerFeet > (platformTop - buffer);
        bool isFalling = playerRb.linearVelocity.y<=0.1f;
        
        bool wantsToDrop = playerScript.inputY<-0.1f || (Input.GetAxisRaw("Vertical")<-0.1f); 
        bool shouldCollide = isAbove && isFalling && !wantsToDrop;
        Physics.IgnoreCollision(playerCollider, platformCollider, !shouldCollide);
    }
}