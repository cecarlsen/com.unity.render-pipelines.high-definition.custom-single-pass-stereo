# High Definition Render Pipeline (Custom) - Single Pass Stereo Mod

USE AT YOUR OWN RISK.

This is a workaround/hack for setting custom single-pass stereo view-projection (off-axis) matrices in HDRP.

The approach is to overwrite the XRPass in HDCamera with a custom setup. If HDRP sees that camera.xr has two views it's largely fooled to just render single pass stereo, except it raises a few issues that this hack then fixes. Finally, OnEndCameraRendering the target texture is then blitted to a SBS texture.

The officially recommended way to do this is by writing and compiling your own OpenXR provider ([NOT](https://discussions.unity.com/t/using-unity-xr-sdk-to-build-my-own-ar-plug-in/904304/13) a XR Provider plugin as has been previosly stated). Yes, writing a plugin ... just to do the equivalent of the (now obsolete) camera.SetStereoViewMatrices() and camera.SetStereoProjectionMatrices() methods

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
					- Make the isHDRDisplayOutputActive property always return true. Otherwise HDRP will render UI into our target texture.
					- Make the hdrDisplayOutputColorGamut property always return ColorGamut.sRGB.
					- Make the hdrDisplayOutputInformation always return new HDROutputUtils.HDRDisplayInformation( -1, 1000, 0, 160f ). 
				- Make the AssignView() method public.
				- Make the AddView() method public.
- 2) Install OffAxisCamera
	- Get it on ([Asset Store](https://assetstore.unity.com/packages/tools/camera/offaxiscamera-98991)). Alternatively moddify this hack and use your own view calculations.
- Install *XR Plugin Management* using the package manager or in the Project Settings->XR window.
- Close the Unity project and drop this repo at some path. Then edit your package manifest.json file to point to that path. For example: *"com.unity.render-pipelines.high-definition": "file:../../PackageRepos/com.unity.render-pipelines.high-definition.custom-single-pass-stereo",*
- Cross your fingers and re-open your project.
- In your scene:
	- Add a OffAxisCamera component to your main camera and give it a reference to a "Window Transform".
	- Add StereoHackEnabler component to an object in the scene.
	- Assign a RenderTexture asset with B10G11R11_UFloatPack32 format to StereoHackEnabler.
	- Render the the texture somehow. For example by assigning it to a RawImage in a Canvas.


## What was modded in HDRP?

- Runtime/RenderPipeline/Camera/HDCamera.cs xr property was modified, see line 600.
- Runtime/RenderPipeline/HDRenderPipeline.cs was modified, see line 2396.
- Runtime/RenderPipeline/HDRenderPipeline.RenderGraph.cs was modified, see line 263 and 447.
- Runtime/Unity.RenderPipelines.HighDefinition.Runtime.asmdef was mofidied; OffAxisCamera was referenced.
- Runtime/StereoHack/StereoHackEnabler.cs and StereoHackSbsBlit.shader was added.
- This readme was added.


## Known issues

- Colors are not converted correctly. More work has to be done to mirror how XRMirrorView renders the stereo texture array to screen.

## Notes

[Here](https://discussions.unity.com/t/custom-single-pass-stereo-matrices-in-hdrp-how) is the Unity Discussions thread that lead to the soltuion.
