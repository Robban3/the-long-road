using UnityEngine;
namespace UnluckSoftware
{


	public class CameraFreeMove : MonoBehaviour
	{
		[Tooltip("Movement speed")]
		public float moveSpeed = 5f;
		[Tooltip("Mouse sensitivity")]
		public float mouseSensitivity = 100f;

		private float xRotation = 0f;

		void Start()
		{
			xRotation = transform.localRotation.eulerAngles.x;

			if (xRotation > 180f)
				xRotation -= 360f;

			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;

			Cursor.SetCursor(null, new Vector2(Screen.width / 2, Screen.height / 2), CursorMode.Auto);
		}

		void Update()
		{
			HandleMouseLook();
			HandleMovement();
		}

		void HandleMouseLook()
		{
			float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
			float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

			transform.Rotate(Vector3.up * mouseX);

			xRotation -= mouseY;
			xRotation = Mathf.Clamp(xRotation, -90f, 90f);
			transform.localRotation = Quaternion.Euler(xRotation, transform.localRotation.eulerAngles.y, 0f);
		}

		void HandleMovement()
		{
			float horizontal = Input.GetAxis("Horizontal");
			float vertical = Input.GetAxis("Vertical");

			Vector3 horizontalMovement = transform.right * horizontal + transform.forward * vertical;
			horizontalMovement *= moveSpeed * Time.deltaTime;

			float verticalMovement = 0f;
			if (Input.GetKey(KeyCode.Q))
				verticalMovement = -moveSpeed * Time.deltaTime;
			else if (Input.GetKey(KeyCode.E))
				verticalMovement = moveSpeed * Time.deltaTime;

			Vector3 movement = horizontalMovement + transform.up * verticalMovement;
			transform.Translate(movement, Space.World);
		}
	}
}