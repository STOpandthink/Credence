using UnityEngine;
using System;

public class GUIEx {
	
	public static void LeftAligned(Action callback){
		AlignHorizontally(callback, false, true);
	}
	
	public static void Centered(Action callback){
		AlignHorizontally(callback, true, true);
	}
	
	public static void RightAligned(Action callback){
		AlignHorizontally(callback, true, false);
	}
	
	public static void AlignHorizontally(Action callback, bool leftSpace, bool rightSpace){
		GUILayout.BeginHorizontal();
		if(leftSpace) GUILayout.FlexibleSpace();
		callback();
		if(rightSpace) GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
	}
	
	public static void VCentered(Action callback){
		AlignVertically(callback, true, true);
	}
	
	public static void AlignVertically(Action callback, bool leftSpace, bool rightSpace){
		GUILayout.BeginVertical();
		if(leftSpace) GUILayout.FlexibleSpace();
		callback();
		if(rightSpace) GUILayout.FlexibleSpace();
		GUILayout.EndVertical();
	}
}

