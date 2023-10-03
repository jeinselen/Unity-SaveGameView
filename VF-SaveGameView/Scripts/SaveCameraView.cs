// VF Save Camera View
// https://github.com/jeinselenVF/VF-UnitySaveGameView
// Version 0.2

using System.IO;
using System.Collections;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.Experimental.Rendering;

[CustomEditor(typeof(SaveCameraView))]
public class SaveCameraViewEditor : Editor
{
	private SerializedProperty filePathProperty;
	private SerializedProperty fileNameProperty;
	private SerializedProperty multiSampleProperty;
	
	private void OnEnable()
	{
		filePathProperty = serializedObject.FindProperty("filePath");
		fileNameProperty = serializedObject.FindProperty("fileName");
		multiSampleProperty = serializedObject.FindProperty("multiSample");
	}
	
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		
		SaveCameraView saveCameraView = (SaveCameraView)target; // What is this doing?
				
		// File path input field
		EditorGUILayout.BeginHorizontal();
		EditorGUILayout.PropertyField(filePathProperty);
		
//		if (filePathProperty.stringValue.Length <= 0 || !System.IO.Directory.Exists(filePathProperty.stringValue))
		if (filePathProperty.stringValue.Length <= 0)
		{
			filePathProperty.stringValue = Application.dataPath.Replace("Assets", "Renders");
		}
		
		if (GUILayout.Button("browse", GUILayout.MaxWidth(64)))
		{
			filePathProperty.stringValue = EditorUtility.SaveFolderPanel("Render path", filePathProperty.stringValue, "");
		}
		
		GUI.enabled = Directory.Exists(filePathProperty.stringValue);
		if (GUILayout.Button("reveal", GUILayout.MaxWidth(64)))
		{
			EditorUtility.RevealInFinder(filePathProperty.stringValue);
		}
		GUI.enabled = true;
		
		if (filePathProperty.stringValue.Length > 0 && filePathProperty.stringValue[filePathProperty.stringValue.Length - 1] != '/')
		{
			filePathProperty.stringValue += '/';
		}
		
		EditorGUILayout.EndHorizontal();
		// End file path input field
		
		// File name input field
		EditorGUILayout.PropertyField(fileNameProperty);
		
		// Multi sampling dropdown menu
		EditorGUILayout.PropertyField(multiSampleProperty);
//		var randomGunGenerator = target as RandomGunGenerator;
//		randomGunGenerator.gunType = (RandomGunGenerator.GunTypes)EditorGUILayout.EnumPopup("Type Of Gun:", randomGunGenerator.gunType);
		
		serializedObject.ApplyModifiedProperties();
		
		EditorGUILayout.Space();
		
		if (GUILayout.Button("Save PNG"))
		{
			saveCameraView.CaptureCameraView();
		}
	}
}

[ExecuteInEditMode]
public class SaveCameraView : MonoBehaviour
{
	private enum MultiSampleList
	{
		_Disabled = 0,
		_2x = 2,
		_4x = 4,
		_8x = 8
	}
	
	[SerializeField, Tooltip("Location for saving PNG files")]
	private string filePath = "";
	[SerializeField, Tooltip("PNG file name, dynamic variables: {scene} {camera} {date} {time}")]
	private string fileName = "{scene} {camera} {date} {time}";
	[SerializeField, Tooltip("Anti Aliasing (MSAA)")]
	private MultiSampleList multiSample;
	private Camera activeCamera;
	
	private bool hideGUI;
	
	// On component reset
	private void Reset()
	{
		filePath = Application.dataPath.Replace("Assets", "Renders");
		fileName = "{scene} {camera} {date} {time}";
	}
	
	// Triggered from the object inspector GUI
	public void CaptureCameraView()
	{
		if (!gameObject.activeInHierarchy)
		{
			Debug.Log("Selected camera is inactive");
			return;
		}
		
		// Get the multi sampling value
		int multiSampleValue = (int)multiSample;
		
		// Get the local camera component
		activeCamera = GetComponent<Camera>();
		
		// Get the width/height of the game view
		Vector2 resolution = GetMainGameViewSize();
		int width = (int)resolution.x;
		int height = (int)resolution.y;
		
		// Create an HDR render texture with the same dimensions as the game view
		RenderTexture renderTexture = new RenderTexture(width, height, 32, RenderTextureFormat.ARGBFloat);
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