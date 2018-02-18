using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ModWindow : MonoBehaviour 
{
    [SerializeField, HideInInspector]
    int screenWidth;
    [SerializeField, HideInInspector]
    int screenHeight;

    //pull resolution from player settings and store it for use at runtime
#if UNITY_EDITOR
    void OnValidate()
    {
        screenWidth = PlayerSettings.defaultScreenWidth;
        screenHeight = PlayerSettings.defaultWebScreenHeight;
    }
#endif

    void Awake() 
    {
        Screen.SetResolution(screenWidth, screenHeight, false);
	}
	
	void Update()
    {
		
	}
}
