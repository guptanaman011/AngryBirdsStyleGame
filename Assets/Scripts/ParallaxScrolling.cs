using UnityEngine;
using System.Collections;

public class ParallaxScrolling : MonoBehaviour {

	// Use this for initialization
	void Start () {
        mainCamera = Camera.main;
        previousCameraTransform = mainCamera.transform.position;
	}

    Camera mainCamera;
	
	/// <summary>
	/// similar tactics just like the "CameraMove" script
	/// </summary>
	void Update () {
        Vector3 delta = mainCamera.transform.position - previousCameraTransform;
        delta.y = 0; delta.z = 0;
        transform.position += delta / ParallaxFactor;


        previousCameraTransform = mainCamera.transform.position;
	}

    public float ParallaxFactor;

    Vector3 previousCameraTransform;

    ///background graphics found here:
    ///http://opengameart.org/content/hd-multi-layer-parallex-background-samples-of-glitch-game-assets
}
