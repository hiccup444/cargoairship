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
        public float mouseSensitivity = 2f;
        public float maxLookAngle = 90f;

        private Material playerMaterialClone;
        private float verticalRotation = 0;
        private Camera playerCamera;

        [SyncVar(hook = nameof(OnNameChanged))]
        public string playerName;

        [SyncVar(hook = nameof(OnColorChanged))]
        public Color playerColor = Color.white;

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
            playerCamera.transform.localPosition = new Vector3(0, 0.6f, 0); // Slightly higher for better view
            playerCamera.transform.localRotation = Quaternion.identity;
            
            // Lock and hide cursor for FPS controls
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            floatingInfo.transform.localPosition = new Vector3(0, -0.3f, 0.6f);
            floatingInfo.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            string name = "Player" + Random.Range(100, 999);
            Color color = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            CmdSetupPlayer(name, color);
        }

        [Command]
        public void CmdSetupPlayer(string _name, Color _col)
        {
            // player info sent to server, then server updates sync vars which handles it on all clients
            playerName = _name;
            playerColor = _col;
        }

        void Update()
        {
            if (!isLocalPlayer)
            {
                // make non-local players run this
                floatingInfo.transform.LookAt(Camera.main.transform);
                return;
            }

            // Handle mouse look
            HandleMouseLook();
            
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

        void HandleMovement()
        {
            // Get input
            float horizontal = Input.GetAxis("Horizontal"); // A/D keys
            float vertical = Input.GetAxis("Vertical");     // W/S keys

            // Calculate movement direction relative to where the player is facing
            Vector3 direction = (transform.forward * vertical + transform.right * horizontal).normalized;
            
            // Apply movement
            Vector3 movement = direction * moveSpeed * Time.deltaTime;
            transform.Translate(movement, Space.World);
        }
    }
}