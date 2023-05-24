// VF Save Game View
// https://github.com/jeinselenVF/VF-UnitySaveGameView
// Version 0.2

using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

public class SaveGameView : EditorWindow
{
	private enum MultiSampleList
	{
		_Disabled = 0,
		_2x = 2,
		_4x = 4,
		_8x = 8
	}
	
	// Script variables
	[SerializeField, Tooltip("Location for saving PNG files")]
	private string filePath;
	[SerializeField, Tooltip("PNG file name, dynamic variables: {scene} {camera} {samples} {date} {time}")]
	private string fileName = "{scene} {camera} {samples} {date} {time}";
	private MultiSampleList multiSampleSelected;
	private Camera activeCamera;
	
	// Add panel to the Unity menu system
	[MenuItem("Tools/Vectorform/Save Game View")]
	public static void ShowWindow()
	{
//		EditorWindow.GetWindow(typeof(SaveGameView));
		SaveGameView window = (SaveGameView)EditorWindow.GetWindow(typeof(SaveGameView));
		window.titleContent = new GUIContent("Save Game View");
	}
	
	private void OnGUI()
	{
		GUILayout.Space(10);
		
		// File location
		EditorGUILayout.BeginHorizontal();
		filePath = EditorGUILayout.TextField("File Path", filePath);
		
//		if (filePath.Length <= 0 || !System.IO.Directory.Exists(filePath))
		if (filePath.Length <= 0)
		{
			filePath = Application.dataPath.Replace("Assets", "Renders");
		}
		
		if (GUILayout.Button("browse", GUILayout.MaxWidth(64)))
		{
			filePath = EditorUtility.SaveFolderPanel("File path", filePath, "");
		}
		
		GUI.enabled = Directory.Exists(filePath);
		if (GUILayout.Button("reveal", GUILayout.MaxWidth(64)))
		{
			EditorUtility.RevealInFinder(filePath);
		}
		GUI.enabled = true;
		
		if (filePath.Length > 0 && filePath[filePath.Length - 1] != '/')
		{
			filePath += '/';
		}
		
		EditorGUILayout.EndHorizontal();
		
		// File name
		fileName = EditorGUILayout.TextField("File Name", fileName);
		
		// Multi sampling dropdown menu
		multiSampleSelected = (MultiSampleList)EditorGUILayout.EnumPopup("Anti Aliasing (MSAA)", multiSampleSelected);
		
		GUILayout.Space(10);
		
		if (GUILayout.Button("Save PNG"))
		{
			CaptureGameView();
		}
		
		GUILayout.Space(10);
		
		GUILayout.Label("Scene:  " + (SceneManager.GetActiveScene() != null ? SceneManager.GetActiveScene().name : "None"));
		GUILayout.Label("Camera:  " + (Camera.main != null ? Camera.main.name : "None"));
//		int multiSampleValue = (int)multiSampleSelected;
//		GUILayout.Label("Samples:  " + (multiSampleValue != null ? multiSampleValue.ToString() : "None"));
//		EditorGUILayout.LabelField("Samples:  ", multiSampleValue.ToString());
	}
	
	private void CaptureGameView()
	{
		// Open game window if not in play mode
		if (!EditorApplication.isPlaying)
		{
			EditorApplication.ExecuteMenuItem("Window/General/Game");
		}
		
		// Get the current active camera from the scene view
		Camera activeCamera = Camera.main;
		
		// Check if a camera is present
		if (activeCamera == null)
		{
			Debug.Log("No active camera found in the scene view.");
			return;
		}
		
		// Get the multi sampling value
		int multiSampleValue = (int)multiSampleSelected;
		
		// Get the width/height of the game view
		Vector2 resolution = GetMainGameViewSize();
		int width = (int)resolution.x;
		int height = (int)resolution.y;
		
		// Create an HDR render texture with the same dimensions as the game view
		RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBFloat);
		if (multiSampleValue > 0)
			renderTexture.antiAliasing = multiSampleValue;
		
		// Render the game view to the render texture
		activeCamera.targetTexture = renderTexture;
		activeCamera.Render();
		RenderTexture.active = renderTexture;
		
		// Create an HDR texture with the same dimensions as the game view
		// Read the pixels from the render texture into the texture
		Texture2D screenshotTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
		screenshotTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
		screenshotTexture.Apply();
		
		// Convert linear render data to sRGB through a stupid complicated process where we shuffle everything to SDR textures
		// Replace render texture with 8-bit version (forces sRGB conversion)
		renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
		// Copy the floating point texture to the 8-bit render texture
		Graphics.Blit(screenshotTexture, renderTexture);
		// Replace output texture with 8-bit version (maintains sRGB conversion)
		screenshotTexture = new Texture2D(width, height, TextureFormat.ARGB32, false);
		// Replace contents. Again.
		screenshotTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
		// Amazingly, this works, and is apparently the process required to fix PNG output gamma...what a nightmare
		screenshotTexture.Apply();
		
		// Reset and remove the render texture
		activeCamera.targetTexture = null;
		RenderTexture.active = null;
		DestroyImmediate(renderTexture);
		
		// Create target directory if it doesn't already exist
		try
		{
			if (!System.IO.Directory.Exists(filePath))
			{
					System.IO.Directory.CreateDirectory(filePath);
			}
		}
		catch (IOException ex)
		{
			Debug.Log(ex.Message);
		}
		
		// Process file name and combine with file path
		string nameTemp = fileName;
		nameTemp = nameTemp.Replace("{scene}", SceneManager.GetActiveScene().name);
		nameTemp = nameTemp.Replace("{camera}", activeCamera.name);
		nameTemp = nameTemp.Replace("{samples}", "MSAA" + multiSampleValue);
		nameTemp = nameTemp.Replace("{date}", System.DateTime.Now.ToString("yyyy-MM-dd"));
		nameTemp = nameTemp.Replace("{time}", System.DateTime.Now.ToString("H-mm-ss.f"));
		string finalPath = filePath + "/" + nameTemp + ".png";
		
		// Convert the texture to PNG bytes and remove the source
		byte[] pngData = screenshotTexture.EncodeToPNG();
		DestroyImmediate(screenshotTexture);
		
		// Save the PNG bytes to a file
		File.WriteAllBytes(finalPath, pngData);
		
		// Provide feedback in the console
//		Debug.Log("Game view captured and exported to: " + finalPath);
	}
	
	// Returns game view resolution even if that panel is not focused
	private Vector2 GetMainGameViewSize()
	{
		System.Type T = System.Type.GetType("UnityEditor.GameView,UnityEditor");
		System.Reflection.MethodInfo GetSizeOfMainGameView = T.GetMethod("GetSizeOfMainGameView", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		System.Object Res = GetSizeOfMainGameView.Invoke(null, null);
		return (Vector2)Res;
	}
}
