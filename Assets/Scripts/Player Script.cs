using Mirror;
using UnityEngine;

namespace QuickStart
{
    public class PlayerScript : NetworkBehaviour
    {
        [Header("Player Info")]
        public TextMesh playerNameText;
        public GameObject floatingInfo;

        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 8f;
        public float crouchSpeed = 2.5f;
        public float jumpSpeed = 8f;
        public float gravity = 20f;
        public float mouseSensitivity = 2f;
        public float maxLookAngle = 90f;
        
        [Header("Crouch Settings")]
        public float standingHeight = 0.9f;
        public float crouchHeight = 0.45f;
        public float crouchTransitionSpeed = 10f;

        private Material playerMaterialClone;
        private float verticalRotation = 0;
        private Camera playerCamera;
        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private bool isCrouching = false;
        private float currentHeight;
        private float cameraStandingHeight = 1.7f;
        private float cameraCrouchHeight = 1.4f;

        [SyncVar(hook = nameof(OnNameChanged))]
        public string playerName;

        [SyncVar(hook = nameof(OnColorChanged))]
        public Color playerColor = Color.white;

        void Start()
        {
            // Get the Character Controller component
            characterController = GetComponent<CharacterController>();
            
            if (characterController == null)
            {
                Debug.LogError("Character Controller component is missing! Please add one to this GameObject.");
            }
            
            currentHeight = standingHeight;
        }

        void OnNameChanged(string _Old, string _New)
        {
            playerNameText.text = playerName;
        }

        void OnColorChanged(Color _Old, Color _New)
        {
            playerNameText.color = _New;
            playerMaterialClone = new Material(GetComponent<Renderer>().material);
            playerMaterialClone.color = _New;
            GetComponent<Renderer>().material = playerMaterialClone;
        }

        public override void OnStartLocalPlayer()
        {
            // Set up camera
            playerCamera = Camera.main;
            playerCamera.transform.SetParent(transform);
            playerCamera.transform.localPosition = new Vector3(0, cameraStandingHeight, 0);
            playerCamera.transform.localRotation = Quaternion.identity;
            
            // Lock and hide cursor for FPS controls
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Hide floating info for local player (they don't need to see their own nametag)
            floatingInfo.SetActive(false);

            string name = "Player" + Random.Range(100, 999);
            Color color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            CmdSetupPlayer(name, color);
        }

        [Command]
        public void CmdSetupPlayer(string _name, Color _col)
        {
            playerName = _name;
            playerColor = _col;
        }

        void Update()
        {
            if (!isLocalPlayer)
            {
                // Make non-local players run this
                floatingInfo.transform.LookAt(Camera.main.transform);
                return;
            }

            // Handle mouse look
            HandleMouseLook();
            
            // Handle crouch
            HandleCrouch();
            
            // Handle movement
            HandleMovement();
            
            // Toggle cursor lock with Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        void HandleMouseLook()
        {
            // Get mouse input
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // Rotate the player body left and right
            transform.Rotate(Vector3.up * mouseX);

            // Rotate the camera up and down
            verticalRotation -= mouseY;
            verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
        }

        void HandleCrouch()
        {
            if (characterController == null) return;

            // Toggle crouch state
            bool wantsToCrouch = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            
            // Can only stand up if there's room above
            if (isCrouching && !wantsToCrouch)
            {
                // Cast from character center upward to check for standing room
                Vector3 rayStart = transform.position + Vector3.up * (currentHeight * 0.5f);
                float additionalHeightNeeded = standingHeight - currentHeight;
                
                if (Physics.Raycast(rayStart, Vector3.up, additionalHeightNeeded + 0.05f))
                {
                    wantsToCrouch = true; // Force stay crouched if no room
                }
            }

            isCrouching = wantsToCrouch;

            // Smoothly transition height
            float targetHeight = isCrouching ? crouchHeight : standingHeight;
            float targetCameraHeight = isCrouching ? cameraCrouchHeight : cameraStandingHeight;
            
            currentHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            characterController.height = currentHeight;
            
            // Adjust camera height
            Vector3 cameraPos = playerCamera.transform.localPosition;
            cameraPos.y = Mathf.Lerp(cameraPos.y, targetCameraHeight, crouchTransitionSpeed * Time.deltaTime);
            playerCamera.transform.localPosition = cameraPos;
            
            // Adjust character controller center
            characterController.center = new Vector3(0, currentHeight / 2, 0);
        }

        void HandleMovement()
        {
            if (characterController == null) return;

            // Get input every frame
            float horizontal = Input.GetAxis("Horizontal"); // A/D keys
            float vertical = Input.GetAxis("Vertical");     // W/S keys

            // Calculate movement direction relative to where the player is facing
            Vector3 horizontalMovement = (transform.forward * vertical + transform.right * horizontal).normalized;

            // Determine current speed based on state
            float currentSpeed = moveSpeed;
            
            if (isCrouching)
            {
                currentSpeed = crouchSpeed;
            }
            else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                currentSpeed = sprintSpeed;
            }

            // Check if the character is on the ground
            if (characterController.isGrounded)
            {
                // Set horizontal movement (this fixes the sticky feeling)
                moveDirection.x = horizontalMovement.x * currentSpeed;
                moveDirection.z = horizontalMovement.z * currentSpeed;

                // Handle jumping (can't jump while crouching)
                if (Input.GetButton("Jump") && !isCrouching)
                {
                    moveDirection.y = jumpSpeed;
                }
                else if (moveDirection.y < 0)
                {
                    moveDirection.y = 0; // Reset downward velocity when grounded
                }
            }
            else
            {
                // In air - apply some air control but don't override existing horizontal momentum completely
                moveDirection.x = Mathf.Lerp(moveDirection.x, horizontalMovement.x * currentSpeed, 2f * Time.deltaTime);
                moveDirection.z = Mathf.Lerp(moveDirection.z, horizontalMovement.z * currentSpeed, 2f * Time.deltaTime);
            }

            // Apply gravity
            moveDirection.y -= gravity * Time.deltaTime;

            // Move the character
            characterController.Move(moveDirection * Time.deltaTime);
        }
    }
}