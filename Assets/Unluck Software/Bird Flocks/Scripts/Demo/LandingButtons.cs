using UnityEngine;

namespace UnluckSoftware
{
	public class LandingButtons : MonoBehaviour
	{
		[SerializeField]
		[Tooltip("The controller handling all bird landing spots.")]
		private LandingSpotController _landingSpotController;

		[SerializeField]
		[Tooltip("The main flock controller managing bird behavior and count.")]
		private FlockController _flockController;

		[SerializeField]
		[Tooltip("The reference resolution used to calculate the baseline aspect ratio scaling.")]
		private Vector2 designResolution = new Vector2(1920, 1080);

		[SerializeField]
		[Tooltip("Manual modifier to scale the entire UI size up or down.")]
		private float guiScale = 1.0f;

		[SerializeField] private int FPSsamples = 100;

		private string label = "";
		private float count;
		private int FPScount;
		private float FPStotalTime;

		private GUIStyle labelStyle;
		private GUIStyle buttonStyle;
		private GUIStyle sliderStyle;
		private GUIStyle sliderThumbStyle;
		private Texture2D flatBackground;
		private Texture2D flatHoverBackground;
		private Texture2D flatActiveBackground;

		void Start()
		{
			flatBackground = CreateFlatTexture(Color.white);
			flatHoverBackground = CreateFlatTexture(new Color(0.9f, 0.9f, 0.9f));
			flatActiveBackground = CreateFlatTexture(new Color(0.8f, 0.8f, 0.8f));

			labelStyle = new GUIStyle();
			labelStyle.fontSize = 14;
			labelStyle.normal.textColor = Color.white;
			labelStyle.alignment = TextAnchor.MiddleLeft;

			buttonStyle = new GUIStyle();
			buttonStyle.fontSize = 12;
			buttonStyle.normal.textColor = Color.black;
			buttonStyle.hover.textColor = Color.black;
			buttonStyle.active.textColor = Color.black;
			buttonStyle.normal.background = flatBackground;
			buttonStyle.hover.background = flatHoverBackground;
			buttonStyle.active.background = flatActiveBackground;
			buttonStyle.alignment = TextAnchor.MiddleCenter;

			sliderStyle = new GUIStyle();
			sliderStyle.normal.background = CreateFlatTexture(new Color(0.2f, 0.2f, 0.2f, 1.0f));
			sliderStyle.fixedHeight = 4;

			sliderThumbStyle = new GUIStyle();
			sliderThumbStyle.normal.background = flatBackground;
			sliderThumbStyle.fixedWidth = 8;
			sliderThumbStyle.fixedHeight = 12;
		}

		public void Update()
		{
			FPScount -= 1;
			FPStotalTime += Time.deltaTime;

			if (FPScount <= 0)
			{
				float fps = FPSsamples / FPStotalTime;
				label = "FPS: " + (int)fps;
				FPStotalTime = 0f;
				FPScount = FPSsamples;
			}
		}

		public void OnGUI()
		{
			if (_flockController == null) return;

			float baseScale = Screen.width / designResolution.x;
			float finalScale = baseScale * guiScale;

			Matrix4x4 svMat = GUI.matrix;
			GUI.matrix = Matrix4x4.Scale(new Vector3(finalScale, finalScale, 1.0f));

			GUI.Box(new Rect(10, 10, 150, 205), CreateFlatTexture(new Color(0.05f, 0.05f, 0.05f, 1.0f)));

			GUI.Label(new Rect(20, 15, 130, 20), label, labelStyle);
			GUI.Label(new Rect(20, 35, 130, 20), "Landing Spots: " + _landingSpotController.transform.childCount, labelStyle);

			if (GUI.Button(new Rect(20, 60, 130, 22), "Scare All", buttonStyle))
				_landingSpotController.ScareAll();

			if (GUI.Button(new Rect(20, 87, 130, 22), "Land In Reach", buttonStyle))
				_landingSpotController.LandAll();

			if (GUI.Button(new Rect(20, 114, 130, 22), "Land Instant", buttonStyle))
				StartCoroutine(_landingSpotController.InstantLand(0.01f));

			if (GUI.Button(new Rect(20, 141, 130, 22), "Destroy", buttonStyle))
				_flockController.destroyBirds();

			GUI.Label(new Rect(20, 168, 130, 20), "Bird Amount: " + _flockController._childAmount, labelStyle);
			_flockController._childAmount = (int)GUI.HorizontalSlider(new Rect(20, 193, 130, 16), (float)_flockController._childAmount, 0.0f, 2000.0f, sliderStyle, sliderThumbStyle);

			GUI.matrix = svMat;
		}

		private Texture2D CreateFlatTexture(Color color)
		{
			Texture2D texture = new Texture2D(1, 1);
			texture.SetPixel(0, 0, color);
			texture.Apply();
			return texture;
		}
	}
}