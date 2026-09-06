using UnityEngine;
namespace UnluckSoftware
{

    public class BakedMeshAnimation : MonoBehaviour
    {
        public new string name;
        public Mesh[] meshes;
        public float playSpeed = 30f;
        [HideInInspector]
        public Renderer rendererComponent;
        public bool randomStartFrame = true;
        public bool loop = true;
        public bool pingPong;
        public bool playOnAwake = true;
        public Transform transformCache;
        public int transitionFrame;
        public int crossfadeFrame;

        // Removed crossfadeWeightAdd property from here
    }
}