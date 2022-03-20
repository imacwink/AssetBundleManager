```
                          _   ____                  _ _      __  __                                   
       /\                | | |  _ \                | | |    |  \/  |                                  
      /  \   ___ ___  ___| |_| |_) |_   _ _ __   __| | | ___| \  / | __ _ _ __   __ _  __ _  ___ _ __ 
     / /\ \ / __/ __|/ _ \ __|  _ <| | | | '_ \ / _` | |/ _ \ |\/| |/ _` | '_ \ / _` |/ _` |/ _ \ '__|
    / ____ \\__ \__ \  __/ |_| |_) | |_| | | | | (_| | |  __/ |  | | (_| | | | | (_| | (_| |  __/ |   
   /_/    \_\___/___/\___|\__|____/ \__,_|_| |_|\__,_|_|\___|_|  |_|\__,_|_| |_|\__,_|\__, |\___|_|   
                                                                                       __/ |          
                    
```
⭐ Star us on GitHub — it helps!

[![repo-size](https://img.shields.io/github/languages/code-size/imacwink/AssetBundleManager?style=flat)](https://github.com/imacwink/AssetBundleManager/archive/main.zip) [![tag](https://img.shields.io/github/v/tag/imacwink/AssetBundleManager)](https://github.com/imacwink/AssetBundleManager/tags) [![license](https://img.shields.io/github/license/imacwink/AssetBundleManager)](LICENSE) 

## Requirements
 - Unity 4.x or greater（Lower versions can use this WWW API to replace the current UnityWebRequest）. 

## Feature
* Load AssetBundles and assets by sync and async.
* Cache AssetBundles and assets to reduce repeat load time.
* Will not load duplicate AssetBundles.
* Easily unload AssetBundles and assets.
* Allow load or unload the same AssetBundle or Asset at the same time by sync and async.
* Provide editor tools to help you build AssetBundles easily.

## Setup
* Click "Tools/AssetBundle/Build/PlatformXXX" menu item to generate ab assets in StreamingAssets folder.
* Click "Tools/AssetBundle/Rename/XXX" menu item to rename AssetBundleName.

## Usage

* STABManager Initlize.

```c#
void Start()
{
	STABManager.Initlize("http://127.0.0.1"/* real url*/, "http://127.0.0.1"/* real url*/, OnAssetBundleInitFinish, OnAssetBundleProgress);
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
```

* STABManager Use.

```c#
STABManager.GetAssetObject(enResType, AssetBundleName);
```
