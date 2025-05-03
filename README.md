# High Definition Render Pipeline (Custom) - Single Pass Stereo Mod

**USE AT YOUR OWN RISK**

This is a hack for instanced single pass stereo with custom views in HDRP for supporting stereoscopic displays and video projections that need user dependent perspective rendering.

In broad strokes, the approach is to add a custom XRPass to XRLayout during HDRenderPipeline.Render() and use OnEndCameraRendering to blit to a SBS texture.

The officially recommended solution is to write and compile your own OpenXR provider ([NOT](https://discussions.unity.com/t/using-unity-xr-sdk-to-build-my-own-ar-plug-in/904304/13) a XR Provider plugin as has been previosly stated). Yes, writing a plugin ... just to do the equivalent of the (now obsolete) camera.SetStereoViewMatrices() and camera.SetStereoProjectionMatrices() methods.

Based on Unity 6000.1.1f1, HDRP 17.1.0 (May 2025).

![HdrpCustomSinglePassStereo](https://github.com/cecarlsen/com.unity.render-pipelines.high-definition.custom-single-pass-stereo/blob/main/GithubImages~/HdrpCustomSinglePassStereo.png)

## How to avoid writing a plugin

- 1) **Modify Scriptable Render Pipeline Core**.
	- Install *High Definition Render Pipeline* using the package manager. This will automatically install *Scriptable Render Pipeline Core*.
		- Close the Unity project and move *com.unity.render-pipelines.core* from /Library/PackageCache to /Packages (removing the @hashcode). Then open the project again and make these edits to the package:
			- */Runtime/XR/XRView.css*
				- make the struct and the constructor public.
			- */Runtime/XR/XRPass.css*
				- Make all fields of XRPassCreateInfo public.
				- Since we don't have a XRDisplaySubsystem:
					- Make the isHDRDisplayOutputActive property always return false.
					- Make the hdrDisplayOutputColorGamut property always return ColorGamut.sRGB.
					- Make the hdrDisplayOutputInformation always return new HDROutputUtils.HDRDisplayInformation( -1, 1000, 0, 160f ). 
				- Make the AssignView() method public.
				- Make the AddView() method public.
			- */Runtime/XR/XRLayout.css*
				- make the AddPass mathod public.
- 2) **Install OffAxisCamera**
	- Get it on ([Asset Store](https://assetstore.unity.com/packages/tools/camera/offaxiscamera-98991)). Alternatively moddify this hack and use your own view calculations.
- 3) **Install MockHMD**
	- Install *XR Plugin Management* using the package manager or in the Project Settings->XR window. Don't enable "Initialize XR on Startup".
	- Install *Mock HMD Loader*. If this is not present in your project, stereo instancing will be stripped from all shaders and the STEREO_INSTANCING_ON keyword will be undefined.
- 4) **Add custom HDRP**
	- Close the Unity project and drop this repo at some path. Then edit your package manifest.json file to point to that path. For example: *"com.unity.render-pipelines.high-definition": "file:../../PackageRepos/com.unity.render-pipelines.high-definition.custom-single-pass-stereo",*
	- Cross your fingers and re-open your project.
- 5) **Setup your scene**
	- Add a OffAxisCamera component to your main camera and give it a reference to a "Window Transform".
	- Add StereoHackEnabler component to an object in the scene.
	- Assign a RenderTexture asset with B10G11R11_UFloatPack32 format to StereoHackEnabler.
	- Render the the texture somehow. For example by assigning it to a RawImage in a Canvas.


## What was changed in HDRP?

- Modified *HDRenderPipeline.cs* and *HDRenderPipeline.RenderGraph.cs*. Changes are marked with "CEC EDIT".
- Added reference to OffAxisCamera in the Unity.RenderPipelines.HighDefinition.Runtime.asmdef.
- Added some files in Runtime/StereoHack/.
- Added this readme.



## Notes

[Here](https://discussions.unity.com/t/custom-single-pass-stereo-matrices-in-hdrp-how) is the Unity Discussions thread that lead to the soltuion.
