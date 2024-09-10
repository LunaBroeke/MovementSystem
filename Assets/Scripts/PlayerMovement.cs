using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 10f;          // Movement force for forward/backward movement
    public float maxSpeed = 5f;            // Max allowed speed
    public float sideMoveSpeed = 7f;       // Speed for sideways movement
    public float rotationSpeed = 100f;     // Speed for rotating the player
    public float jumpForce = 5f;           // Jump force
    public float groundCheckDistance = 1.2f;   // Distance for ground raycast check
    public float gravity = 9.81f;
    public float gravityDamp = 6;
    public LayerMask groundLayer;          // Layer mask for ground detection (optional)

    private bool isGrounded = true;        // Check if player is grounded
    private Rigidbody rb;

    public Transform cameraTransform;      // Reference to the camera's transform

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;  // Automatically use the main camera if not set
        }
    }

    void FixedUpdate()
    {
        // Ground check using raycasting
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance, groundLayer);

        // Movement input
        float moveHorizontal = Input.GetAxis("Horizontal");   // Sideways movement (A/D or Left/Right arrows)
        float moveVertical = Input.GetAxis("Vertical");       // Forward/Backward movement (W/S or Up/Down arrows)

        // Rotation input
        float rotateInput = Input.GetAxis("Mouse X");          // Rotation input

        // Calculate camera forward and right directions
        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // Ignore the Y component for horizontal movement
        cameraForward.y = 0;
        cameraRight.y = 0;

        cameraForward.Normalize();
        cameraRight.Normalize();

        // Movement direction based on camera's forward and right
        Vector3 movement = (cameraForward * moveVertical * moveSpeed) + (cameraRight * moveHorizontal * sideMoveSpeed);
        Gravity();

        // Apply movement force
        rb.AddForce(movement, ForceMode.Acceleration);

        // Limit the velocity to the max speed
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }

        // Apply rotation
        transform.Rotate(0, rotateInput * rotationSpeed * Time.fixedDeltaTime, 0);
    }
    public float jumpCooldown = 0.2f;
    private float jCooldown;
    void Update()
    {
        jCooldown -= Time.deltaTime;
        if (jCooldown > 0) { isGrounded = false; }
        // Jump functionality
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            jCooldown = jumpCooldown;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        if (Input.GetKeyDown(KeyCode.LeftAlt)) { CursorCheck(); }
        if (transform.position.y < -10) { transform.position = new Vector3(0, 2, 0); rb.velocity = Vector3.zero; }
    }

    void CursorCheck()
    {
        switch (Cursor.lockState)
        {
            case CursorLockMode.None:
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                break;
            case CursorLockMode.Locked:
                Cursor.lockState = CursorLockMode.None; 
                Cursor.visible = true;
                break;
            case CursorLockMode.Confined:
                break;
        }
    }

    void Gravity()
    {
        Vector3 grav = new Vector3();

        if (rb.velocity.y < 1.5 && rb.velocity.y > -0.8) { grav = new Vector3(0, -gravityDamp, 0); rb.AddForce(grav, ForceMode.Acceleration); }
        else { grav = new Vector3(0, -gravity, 0); rb.AddForce(grav, ForceMode.Acceleration); }
    }

    // Debugging the raycast in the scene view
    void OnDrawGizmosSelected()
    {
        // Draw a line in the scene view to visualize the ground check ray
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * groundCheckDistance);
    }
}
