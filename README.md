# High Definition Render Pipeline (Custom) - Single Pass Stereo Mod

USE AT YOUR OWN RISK.

This is a workaround/hack for setting custom single-pass stereo view-projection (off-axis) matrices in HDRP. The approach is to use MockHMD to enable single-pass stereo, and then overwrite the view data it provides.

The proper way to do this is by writing and compiling your own XR Provider plugin. [Here](https://docs.unity3d.com/6000.1/Documentation/Manual/xrsdk-provider-setup.html) is the documentation and [here](https://discussions.unity.com/t/using-unity-xr-sdk-to-build-my-own-ar-plug-in/904304/12) is a much needed hint.

Based on HDRP 17.1.0 (April 2025).

![HdrpCustomSinglePassStereo](https://github.com/cecarlsen/com.unity.render-pipelines.high-definition.custom-single-pass-stereo/blob/main/GithubImages~/HdrpCustomSinglePassStereo.png)

## How to avoid writing a plugin

- Install *High Definition Render Pipeline* using the package manager. This will automatically install *Scriptable Render Pipeline Core*.
- Close the Unity project and move *com.unity.render-pipelines.core* from /Library/PackageCache to /Packages (removing the @hashcode). Then open the project again.
- Make these edits to *Scriptable Render Pipeline Core*:
	- In /Runtime/XR/XRView.css make the class and the constructor public.
	- In /Runtime/XR/XRPass.css make the AssignView() method public.
- Install OffAxisCamera ([Asset Store](https://assetstore.unity.com/packages/tools/camera/offaxiscamera-98991)).
- Install *XR Plugin Management* using the package manager or in the Project Settings->XR window.
- Go to Project Settings->XR and install Mock HMD Loader. Then enable Initialize XR on Startup and choose SinglePass Instanced under MockHMD.
- Go to Project Settings->Quality->HDRP->XR and disable Occlusion Mesh.
- In your scene:
	- Add a OffAxisCamera component to your main camera and give it a reference to a "Window Transform".
	- Add SinglePassStereoSetup to an object and set a per eye resolution.
- Close the Unity project and drop this repo at some path. Then edit your package manifest.json file to point to that path. For example: *"com.unity.render-pipelines.high-definition": "file:../../PackageRepos/com.unity.render-pipelines.high-definition.custom-single-pass-stereo",*
- Cross your fingers and re-open your project.


## What was modded in HDRP?

- Runtime/RenderPipeline/Camera/HDCamera.cs was modified, see line 600.
- Runtime/SinglePassStereoSetup.cs was added.


## Known issues

- Setting a target RenderTexture on your camera will disable stereo rendering. I have no idea why.


## Notes

First I tried to access XRPass.AssignView() and XRView using reflection, but I couldn't find a way to not generate a ton of garbage – hence the SRP Core edits.

[Here](https://discussions.unity.com/t/custom-single-pass-stereo-matrices-in-hdrp-how) is the Unity Discussions thread that lead to the soltuion.
