using Unity.Cinemachine; 
using UnityEngine; 
using UnityEngine.InputSystem; 

[CreateAssetMenu(menuName = "Camera/CM Settings", fileName = "CameraCMSettings")] 
public class CameraCMSettings : ScriptableObject 
{ 
    [Header("Presets")] 
    public float topRadius = 12f; 
    public float topVertical = 75f; 
    public float thirdRadius = 4f; 
    public float thirdVertical = 25f;
    
    [Header("Tuning")] 
    public float transitionSpeed = 6f; 
    public float zoomSpeed = 0.02f; 
    public float minRadius = 2.5f; 
    public float maxRadius = 16f; 
}