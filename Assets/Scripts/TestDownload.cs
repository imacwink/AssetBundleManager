using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using ST.Manager;

public class TestDownload : MonoBehaviour
{
	public AudioSource audioSource;

    // Start is called before the first frame update
    void Start()
    {
		STABManager.Initlize("http://127.0.0.1", "http://127.0.0.1", OnAssetBundleInitFinish, OnAssetBundleProgress);
	}

    // Update is called once per frame
    void Update()
    { 
    }

	protected void OnAssetBundleInitFinish(bool bOK, string strErrMsg)
	{
		if (bOK)
		{
			Debug.Log("OnAssetBundleInitFinish Succ !!!!");
		}
		else
		{
			Debug.Log("OnAssetBundleInitFinish Failed : " + strErrMsg);
		}
	}

	protected void OnAssetBundleProgress(STDownLoadProgress downLoadProgress)
	{
		Debug.Log("OnAssetBundleProgress : " + downLoadProgress.mProgress);
	}

	public void OnPrefabEvnet()
	{
		GameObject rootObj = GameObject.Find("Root");
		GameObject obj = STABManager.GetAssetObject(enResType.enResType_Prefab, "Cube 1") as GameObject;
		GameObject objInstance = Instantiate(obj, new Vector3(0f, 0f, 0f), new Quaternion());
		objInstance.transform.parent = rootObj.transform;
	}

	public void OnSceneEvnet()
	{
		STABManager.GetAssetObject(enResType.enResType_Scene, "TestDownload");

        SceneManager.LoadScene("TestDownload", LoadSceneMode.Single);
	}

	public void OnSoundEvnet()
	{
		Debug.Log("OnSoundEvnet");
		AudioClip audioClip = STABManager.GetAssetObject(enResType.enResType_Sound, "Test004") as AudioClip;
		audioSource.clip = audioClip;
		audioSource.Play();
	}
}
