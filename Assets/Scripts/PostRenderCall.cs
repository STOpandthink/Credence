using UnityEngine;
using System.Collections;

public class PostRenderCall : MonoBehaviour {

	void OnPostRender(){
		if(GameScript.singleton != null){
			GameScript.singleton.OnPostRender();
		}
	}
}
