/*
	Copyright © Carl Emil Carlsen 2025
	http://cec.dk
*/

using UnityEngine;
using Unity.XR.MockHMD;

public class SinglePassStereoSetup : MonoBehaviour
{
	[SerializeField] Vector2Int _eyeResolution = new Vector2Int( 1080, 1080 );
	[SerializeField] float _eyeSeperation = 0.064f;

	public float eyeSeperation => _eyeSeperation;
	public Vector2Int eyeResolution => _eyeResolution;


	void Start()
	{
		MockHMD.SetMirrorViewCrop( 0f );
		MockHMD.SetFoveationMode( false, 0 );

		OnValidate();
	}


	void OnValidate()
	{
		if( Application.isPlaying ) MockHMD.SetEyeResolution( _eyeResolution.x, _eyeResolution.y );
	}
}