//#undef UNITY_EDITOR
using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mono.Xml;
using System.Xml;
using System.Xml.Serialization;
using System.Net;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ST.Manager
{
	public enum enResType
	{
		enResType_Default,
		enResType_Texture,
		enResType_Bytes,
		enResType_Prefab,
		enResType_Sound,
		enResType_Scene,
	};

	public static class STABManager
    {
        public delegate void onAssetDelegate(Object resultObject, string strErrMsg);
        public delegate void onAssetBundleInitFinishDelegate(bool bOK, string strErrMsg);
        public delegate void onAssetBundleProgressDelegate(STDownLoadProgress stDownLoadProgress);

		private static string s_strAssetBundleExt = ".bytes";
		private static string s_strPreHttpDirectory = "";
		private static string s_strRealHttpDirectory = "";
		private static string s_strStreamAssetDir = Application.streamingAssetsPath;
		private static string s_strPersistUrlPath = "file:///" + Application.persistentDataPath;
		private static string s_strPersistPath = Application.persistentDataPath;
		private static bool s_bInitlized = false;
		private static HashSet<string> s_assetBundleList = new HashSet<string>();
		private static string[] s_allAssetBundlesArray = null;
		private static Dictionary<string, string> s_asset2BundleMap = new Dictionary<string, string>();
		private static AssetBundle s_assetBundle = null;
		private static AssetBundle s_assetBundleScene = null;

		private static Dictionary<string, List<string>> s_assetBundleAsstList;
		private static Dictionary<string, string> s_assetBundleDependency;

		private static Dictionary<string, string> s_assetBundleListNew;
		private static Dictionary<string, string> s_assetBundleListOld;

		private static Dictionary<string, string> s_assetBundleSizeListNew;
		private static Dictionary<string, string> s_assetBundleSizeListOld;

		private static HashSet<string> s_levelList;
		private static HashSet<string> s_preloadBundleList;
		private static Dictionary<string, GameObject> s_directGameObject = new Dictionary<string, GameObject>();
		private static Dictionary<string, Object> s_cachedObject = new Dictionary<string, Object>(); // Cache 过的 Obj;
		private static Dictionary<string, AssetBundle> _cachedAssetBundleMap = new Dictionary<string, AssetBundle>();
		private static Dictionary<string, AssetBundle> s_preloadBundleMap = new Dictionary<string, AssetBundle>();

		public static string s_strAssetBundleDescFile = "assetBundleManifest.bytes";
		public static string s_strAssetBundleDependFile = "assetBundleDepend.bytes";
		private static int s_downloadSize = 1024 * 1;

#if UNITY_EDITOR
	private static Dictionary<string, string> s_assetPathDic = new Dictionary<string, string>();
#endif

		/// <summary>
		/// 根据名字获取获取实例对象.
		/// </summary>
		/// <returns>Object.</returns>
		/// <param name="type">资源类型.</param>
		/// <param name="strAsstName">名字.</param>
		public static Object GetAssetObject(enResType type, string strAsstName = "")
		{
#if UNITY_EDITOR
			if(type != enResType.enResType_Scene)
			{
				// 优先DirectObject
				var directObj = getDirectObject(strAsstName);
				if (directObj != null) return directObj;

				string strPath;
				if(s_assetPathDic != null && s_assetPathDic.ContainsKey(strAsstName))
				{
					if(s_assetPathDic.TryGetValue(strAsstName, out strPath) )
					{
						var retObj = AssetDatabase.LoadAssetAtPath(strPath, typeof(UnityEngine.Object));
						return retObj;
					}
					else
					{
						Debug.LogWarning(string.Format("没有配置资源 : {0}", strAsstName));
						return null;
					}
				}
				else
				{
					Debug.LogWarning(string.Format("没有配置资源 : {0}", strAsstName));
					return null;
				}	
			}
			else
			{
				return null;
			}
#else
			if (type != enResType.enResType_Scene)
			{

				if (strAsstName != null && strAsstName.Length == 0)
				{
					Debug.LogWarning("strAsstName is null");
					return null;
				}

				// 优先DirectObject;
				var directObj = getDirectObject(strAsstName.ToLower());
				if (directObj != null) return directObj;

				// First find in pool;
				Object proto = null;
				if (s_cachedObject.TryGetValue(strAsstName.ToLower(), out proto))
				{
					//Debug.Log("Find Cache AssetObj:" + strAsstName);
					return proto;
				}

				// 先找到它在哪个AssetBundle里;
				string strAssetBundleName;
				if (!s_asset2BundleMap.TryGetValue(strAsstName.ToLower(), out strAssetBundleName))
				{
					Debug.LogError("未找到[" + strAsstName.ToLower() + "]依赖的strAsstName");
					return null;
				}

				Object ret = createAssetFromBundle(strAssetBundleName, strAsstName.ToLower());

				return ret;
			}
			else
			{
				string levelKey = "asset_scene_" + strAsstName.ToLower();
				string realFileName = null;
				if (s_assetBundleListNew.ContainsKey(levelKey))
					s_assetBundleListNew.TryGetValue(levelKey, out realFileName);
				if (realFileName != null && realFileName.Length > 0)
				{
					string filePath = System.IO.Path.Combine(s_strPersistPath, realFileName + s_strAssetBundleExt);
					if (s_assetBundleScene != null)
						s_assetBundleScene.Unload(true);
					s_assetBundleScene = AssetBundle.LoadFromFile(filePath);
					return (Object)s_assetBundleScene;
				}
				return null;
			}
#endif
		}

		/// <summary>
		/// 根据名字获取获取实例对象.
		/// </summary>
		/// <returns>GameObject.</returns>
		/// <param name="strAsstName">名字.</param>
		private static GameObject getDirectObject(string strAssetName)
		{
			GameObject outValue = null;
			if (s_directGameObject.TryGetValue(strAssetName, out outValue))
			{
				return outValue;
			}

			return null;
		}

		/// <summary>
		/// 初始化资源管理器并注册下载回调.
		/// </summary>
		/// <param name="strPreUrlDirectory">下载地址.</param>
		/// <param name="strRealUrlDirectory">下载地址.</param>
		/// <param name="initFinishCallback">结束下载.</param>
		/// <param name="progressCallback">进度.</param>
		public static void Initlize(string strPreUrlDirectory,
			string strRealUrlDirectory,
			onAssetBundleInitFinishDelegate initFinishCallback,
			onAssetBundleProgressDelegate progressCallback)
		{
			if (s_bInitlized)
			{
				STDownLoadProgress downLoadProgress = new STDownLoadProgress();
				downLoadProgress.mDownload = false;
				downLoadProgress.mDownloadFinsih = true;
				progressCallback(downLoadProgress);
				initFinishCallback(true, "");
				return;
			}

			InitResDirectory(strPreUrlDirectory, strRealUrlDirectory);

#if UNITY_EDITOR
			// 标记已初始化;
			s_bInitlized = true;
	
			// bin文件加载;
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Bytes/", "bytes", false);
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Bytes/", "txt", false);

			// Texture文件加载;
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Texture/", "png", false);
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Texture/", "jpg", false);

			// Prefab文件加载;
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Prefab/", "prefab", false);

			// 场景文件加载;
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Scene/", "unity", false);

			// 声音文件加载;
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Sound/", "mp3", false);
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Sound/", "ogg", false);

			// Lua文件加载;
			AddDirBinaryFilesToAsset("Assets/Artwork/Download/Lua/", "lua", false);

			STDownLoadProgress downLoadProgresst = new STDownLoadProgress();
			downLoadProgresst.mDownload = false;
			downLoadProgresst.mDownloadFinsih = true;
			progressCallback(downLoadProgresst); /* progressCallback(100, false); */
			initFinishCallback(true, "");
#else
			Coroutiner.StartCoroutine(AsyInitlizeContext(initFinishCallback, progressCallback));
#endif
		}


		/// <summary>
		/// 初始化下载路径和读取路径;
		/// </summary>
		/// <param name="strPreUrlDirectory">下载地址.</param>
		/// <param name="strRealUrlDirectory">下载地址.</param>
		private static void InitResDirectory(string strPreUrlDirectory, string strRealUrlDirectory)
		{
			s_strPreHttpDirectory = strPreUrlDirectory;
			s_strRealHttpDirectory = strRealUrlDirectory;

#if UNITY_STANDALONE_WIN
			s_strPreHttpDirectory += "/StandaloneWindows/";
			s_strRealHttpDirectory += "/StandaloneWindows/";
			s_strStreamAssetDir += "/StandaloneWindows/";

#elif UNITY_STANDALONE_OSX
			s_strPreHttpDirectory += "/macOS/";
			s_strRealHttpDirectory += "/macOS/";
			s_strStreamAssetDir += "/macOS/";

#elif UNITY_ANDROID
			s_strPreHttpDirectory += "/Android/";
			s_strRealHttpDirectory += "/Android/";
			s_strStreamAssetDir += "/Android/";

#elif UNITY_IPHONE
			s_strPreHttpDirectory += "/iPhone/";
			s_strRealHttpDirectory += "/iPhone/";
			s_strStreamAssetDir = "file://" + Application.dataPath + "/Raw/iPhone/";

#elif UNITY_WP8
			s_strPreHttpDirectory += "/WP8Player/";
			s_strRealHttpDirectory += "/WP8Player/";
			s_strStreamAssetDir += "/WP8Player/";
#elif UNITY_METRO
			s_strPreHttpDirectory += "/MetroPlayer";
			s_strRealHttpDirectory += "/MetroPlayer";
			s_strStreamAssetDir += "/MetroPlayer";
#endif

#if UNITY_EDITOR
			s_strStreamAssetDir = "file:///" + s_strStreamAssetDir;
			s_strPersistUrlPath = "file:///" + Application.dataPath;	
			s_strPersistPath = Application.dataPath;
#endif

			//Debug.Log("PersistPath:" + Application.persistentDataPath + "  StreamAsset Path:" + s_strStreamAssetDir);
		}

		/// <summary>
		/// 开起下载协程，同时初始化上下文数据;
		/// </summary>
		/// <returns>The base context.</returns>
		/// <param name="initFinishCallback">下载结束进度.</param>
		/// <param name="progressCallback">下载进度.</param>
		private static IEnumerator AsyInitlizeContext(onAssetBundleInitFinishDelegate initFinishCallback, onAssetBundleProgressDelegate progressCallback)
		{
			yield return Coroutiner.StartCoroutine(ProcessDownload(initFinishCallback, progressCallback));

			s_bInitlized = true;
		}


		/// <summary>
		/// 初始化下载过程中的内存缓存数据;
		/// </summary>
		private static void InitProcessDownload()
		{
			s_asset2BundleMap = new Dictionary<string, string>();
			s_assetBundleAsstList = new Dictionary<string, List<string>>();
			s_assetBundleDependency = new Dictionary<string, string>();
			s_assetBundleList = new HashSet<string>();
			s_assetBundleListNew = new Dictionary<string, string>();
			s_assetBundleListOld = new Dictionary<string, string>();
			s_assetBundleSizeListNew = new Dictionary<string, string>();
			s_assetBundleSizeListOld = new Dictionary<string, string>();
			s_levelList = new HashSet<string>();
			s_preloadBundleList = new HashSet<string>();
		}

		/// <summary>
		/// Real 下载.
		/// </summary>
		/// <returns>下载协程.</returns>
		/// <param name="initCb">下载结束回调函数.</param>
		/// <param name="progressCb">下载进度回调函数.</param>
		private static IEnumerator ProcessDownload(onAssetBundleInitFinishDelegate initFinishCallback, onAssetBundleProgressDelegate progressCallback)
		{
			InitProcessDownload();

			byte[] szResultObjBytes = null;

			// 组装下载地址;
			double dSecond = (System.DateTime.Now - new System.DateTime(1970, 1, 1).ToLocalTime()).TotalSeconds;
			string strRemoteXmlFile = s_strPreHttpDirectory + "/" + s_strAssetBundleDescFile + "?time=" + dSecond; /* 系统生成的全局描述文件 */
			string strDependXmlName = Path.GetFileNameWithoutExtension(s_strAssetBundleDependFile);
			string strDependXmlFile = s_strPreHttpDirectory + "/" + s_strAssetBundleDependFile + "?time=" + dSecond; /* 自定义的描述文件 */

			// 缓存映射文件;
			CacheXml(s_strPersistPath + "/" + strDependXmlName + ".xml", true);

			// 下载自定义的XML文件;
			using (UnityWebRequest dependXmlWebRequest = UnityWebRequest.Get(strDependXmlFile))
			{
				yield return dependXmlWebRequest.SendWebRequest();

				if (dependXmlWebRequest.isNetworkError || (dependXmlWebRequest.error != null && dependXmlWebRequest.error.Length > 0))
				{
					Debug.LogError("AssetBundleDepend Init Fail:" + dependXmlWebRequest.error);

					STDownLoadProgress downLoadProgressError = new STDownLoadProgress();
					downLoadProgressError.mDownload = false;
					downLoadProgressError.mDownloadFinsih = true;

					initFinishCallback(false, "Get Resource Depend Object Fail:" + dependXmlWebRequest.error);
					yield break;
				}
				else
				{
					//Debug.Log("Load dependXmlWebRequest Success|" + dependXmlWebRequest.downloadHandler.data.Length);
					Ionic.Zlib.ZlibStream.DeCompressBuffToFile(dependXmlWebRequest.downloadHandler.data, s_strPersistPath + "/" + strDependXmlName + ".xml");

					CacheXml(s_strPersistPath + "/" + strDependXmlName + ".xml", false);
				}
			}

			// 下载系统生成的AB描述文件;
			using (UnityWebRequest manifestWebRequest = UnityWebRequest.Get(strRemoteXmlFile))
			{
				yield return manifestWebRequest.SendWebRequest();

				if (manifestWebRequest.isNetworkError || (manifestWebRequest.error != null && manifestWebRequest.error.Length > 0))
				{
					Debug.LogError("AssetBundle Init Fail:" + manifestWebRequest.error);

					STDownLoadProgress downLoadProgressError = new STDownLoadProgress();
					downLoadProgressError.mDownload = false;
					downLoadProgressError.mDownloadFinsih = true;

					progressCallback(downLoadProgressError); /* 出错了 这里显示进度为解压中 */
					initFinishCallback(false, "Get Resource Manifest Object Fail:" + manifestWebRequest.error);
					yield break;
				}
				else
				{
					//Debug.Log("Load manifestWebRequest Success|" + manifestWebRequest.downloadHandler.data.Length);
					szResultObjBytes = manifestWebRequest.downloadHandler.data;
				}

				// 处理整个打好的 AB 依赖数据;
				byte[] decompressResult = Ionic.Zlib.ZlibStream.UncompressBuffer(szResultObjBytes);
				AssetBundle depBundle = AssetBundle.LoadFromMemory(decompressResult);
				string strDepName = Path.GetFileNameWithoutExtension(s_strAssetBundleDescFile);
				AssetBundleManifest assetBundleManifest = depBundle.LoadAsset<AssetBundleManifest>(strDepName);
				string[] allAssetBundles = assetBundleManifest.GetAllAssetBundles();

				for (int i = 0; i < allAssetBundles.Length; i++)
				{
					if (s_assetBundleListNew.ContainsKey(allAssetBundles[i]))
					{
						string realFileName = null;
						s_assetBundleListNew.TryGetValue(allAssetBundles[i], out realFileName);
						s_assetBundleList.Add(realFileName);
						//Debug.Log("allAssetBundles[i] : " + allAssetBundles[i] + " realFileName : " + realFileName);
					}
				}

				depBundle.Unload(true);

				// 清理目录下不需要的文件(变动的文件需要清理掉重新下载);
				HashSet<string> delFileList = new HashSet<string>();
				DirectoryInfo dirInfo = new DirectoryInfo(s_strPersistPath);
				foreach (var fileInfo in dirInfo.GetFiles("*.bytes", System.IO.SearchOption.AllDirectories))
				{
					string strFileNameNoExt = System.IO.Path.GetFileNameWithoutExtension(fileInfo.FullName);
					if ((fileInfo.Length == 0) || !s_assetBundleList.Contains(strFileNameNoExt))
					{
						delFileList.Add(fileInfo.FullName);
					}
				}

				foreach (var item in delFileList) { System.IO.File.Delete(item); }

				int iDownloadCount = allAssetBundles.Length;
				int iLocalCount = 0;
				for (int i = 0; i < iDownloadCount; i++)
				{
					// 获取文件真正的名字;
					string realName = null;
					if (s_assetBundleListNew.ContainsKey(allAssetBundles[i]))
						s_assetBundleListNew.TryGetValue(allAssetBundles[i], out realName);

					string realSize = null;
					if (s_assetBundleSizeListNew.ContainsKey(allAssetBundles[i]))
						s_assetBundleSizeListNew.TryGetValue(allAssetBundles[i], out realSize);

					if (realName == null || (realName != null && realName.Length <= 0)) Debug.LogError("ERROR !!!!");

					// 检查本地是否存在以下文件;
					string strPersistPath = s_strPersistPath + "/" + realName + s_strAssetBundleExt;
					if (System.IO.File.Exists(strPersistPath))
					{
						iLocalCount++;
						AddAsset2BundleMap(strPersistPath, realName); /* 添加映射文件 */
						continue;
					}

					// 检查程序包中(本地)是否存在文件;
					string strLocalPath = System.IO.Path.Combine(s_strStreamAssetDir, realName + s_strAssetBundleExt);
					using (UnityWebRequest abLocalWebRequest = UnityWebRequest.Get(strLocalPath))
					{
						yield return abLocalWebRequest.SendWebRequest();

						if (abLocalWebRequest.isNetworkError || (abLocalWebRequest.error != null && abLocalWebRequest.error.Length > 0))
						{
							string strAssetBundleFile = s_strRealHttpDirectory + "/" + realName + s_strAssetBundleExt;

							// 目前也是为了测试，先暴力处理,不停的尝试，不过好像这样这几用于项目一没有问题;                     
							while (true)
							{
								using (UnityWebRequest abNetWebRequest = UnityWebRequest.Get(strAssetBundleFile))
								{
									abNetWebRequest.SendWebRequest();

									STDownLoadProgress downLoadProgress = new STDownLoadProgress();
									downLoadProgress.mDownload = true;
									downLoadProgress.mFileIndex = iLocalCount;
									downLoadProgress.mTotalFileCount = iDownloadCount;

									while (!abNetWebRequest.isDone)
									{
										downLoadProgress.mDownLoadBytes = (long)(long.Parse(realSize) * abNetWebRequest.downloadProgress);
										downLoadProgress.mTotalBytes = long.Parse(realSize);
										downLoadProgress.mProgress = (decimal)abNetWebRequest.downloadProgress * 100;
										downLoadProgress.mDownloadFinsih = false;
										downLoadProgress.mCompressing = false;
										downLoadProgress.mCompressFinish = false;

										progressCallback(downLoadProgress);

										yield return 1;
									}

									downLoadProgress.mDownLoadBytes = long.Parse(realSize);
									downLoadProgress.mTotalBytes = long.Parse(realSize);
									downLoadProgress.mProgress = 100;
									downLoadProgress.mDownloadFinsih = true;
									downLoadProgress.mCompressing = true;
									downLoadProgress.mCompressFinish = false;

									progressCallback(downLoadProgress);

									if (abNetWebRequest.isNetworkError || (abNetWebRequest.error != null && abNetWebRequest.error.Length > 0))
									{
										yield return new WaitForSeconds(0.1f);
										continue;
									}

									Debug.Log("Download: " + s_strPersistPath + "/" + realName + s_strAssetBundleExt);
									Debug.Log("Size: " + abNetWebRequest.downloadHandler.data.Length);
									Ionic.Zlib.ZlibStream.DeCompressBuffToFile(abNetWebRequest.downloadHandler.data, s_strPersistPath + "/" + realName + s_strAssetBundleExt);

									// 添加映射文件;
									AddAsset2BundleMap(strPersistPath, realName);

									// 解压完;
									STDownLoadProgress downLoadCompress = new STDownLoadProgress();
									downLoadCompress.mDownload = true;
									downLoadCompress.mFileIndex = iLocalCount;
									downLoadCompress.mTotalFileCount = iDownloadCount;
									downLoadCompress.mDownLoadBytes = downLoadProgress.mDownLoadBytes;
									downLoadCompress.mTotalBytes = downLoadProgress.mTotalBytes;
									downLoadCompress.mProgress = 100;
									downLoadCompress.mDownloadFinsih = true;
									downLoadCompress.mCompressing = true;
									downLoadCompress.mCompressFinish = true;
									progressCallback(downLoadCompress);
								}
								break;
							}

							yield break;
						}
						else
						{
							Debug.Log("DeCompressBuffToFile !!!!");

							// 解压完;
							STDownLoadProgress downLoadCompress = new STDownLoadProgress();
							downLoadCompress.mDownload = true;
							downLoadCompress.mDownloadFinsih = true;
							downLoadCompress.mProgress = (decimal)(iLocalCount / (float)iDownloadCount) * 100;
							downLoadCompress.mCompressing = true;
							downLoadCompress.mCompressFinish = false;
							progressCallback(downLoadCompress);

							//Debug.Log("iLocalCount:" + iLocalCount + "/" + iDownloadCount + " Export:" + s_strPersistPath + "/" + realName + s_strAssetBundleExt);
							//Debug.Log("size:" + abLocalWebRequest.downloadHandler.data.Length);
							Ionic.Zlib.ZlibStream.DeCompressBuffToFile(abLocalWebRequest.downloadHandler.data, s_strPersistPath + "/" + realName + s_strAssetBundleExt);

							Debug.Log("DeCompressBuffToFile Success ！！");

							iLocalCount++;

							// 添加映射文件;
							AddAsset2BundleMap(strPersistPath, realName);

							if (iLocalCount == iDownloadCount)
							{
								downLoadCompress.mProgress = 100;
								downLoadCompress.mCompressing = true;
								downLoadCompress.mCompressFinish = true;
								progressCallback(downLoadCompress);
							}
							continue;
						}
					}
				}
				iLocalCount++;
			}

			STDownLoadProgress downLoadProgressLocal = new STDownLoadProgress();
			downLoadProgressLocal.mDownload = false;
			downLoadProgressLocal.mDownloadFinsih = true;
			progressCallback(downLoadProgressLocal);

			initFinishCallback(true, "");
		}

		private static Object createAssetFromBundle(string strAssetBundle, string strAssetName)
		{
			// 准备创建了; 
			float begin = Time.realtimeSinceStartup;
			AssetBundle target = createAssetBundle(strAssetBundle);
			Object retObject = target.LoadAsset(strAssetName);

			//Debug.Log("----CreateBundle|" + strAssetName + "|" + (Time.realtimeSinceStartup - begin));
			//ClearBundle();

			s_cachedObject[strAssetName] = retObject;

			return retObject;
		}

		private static AssetBundle createAssetBundle(string strAssetBundle)
		{
			AssetBundle target = null;
			if (_cachedAssetBundleMap.TryGetValue(strAssetBundle, out target))
			{
				return target;
			}

			if (s_preloadBundleMap.TryGetValue(strAssetBundle, out target))
			{
				return target;
			}

			List<string> assetBundleList = new List<string>();
			assetBundleList.Add(strAssetBundle);

			string strTmpBkAsstBandle = strAssetBundle;
			string strDependenctyName;
			while (s_assetBundleDependency.TryGetValue(strTmpBkAsstBandle, out strDependenctyName))
			{
				assetBundleList.Add(strDependenctyName);
				strTmpBkAsstBandle = strDependenctyName;
			}

			assetBundleList.Reverse();

			foreach (string strLoadAssetBundle in assetBundleList)
			{
				if (_cachedAssetBundleMap.ContainsKey(strLoadAssetBundle))
				{
					continue;
				}

				if (s_preloadBundleMap.ContainsKey(strLoadAssetBundle))
				{
					continue;
				}

				if (!s_assetBundleList.Contains(strLoadAssetBundle))
				{
					Debug.LogError("AssetBundle:" + strLoadAssetBundle + "不存在!!");
					return null;
				}

				//  直接从PersistContentPath 读取;
				//float begin = Time.realtimeSinceStartup;

				string filePath = System.IO.Path.Combine(s_strPersistPath, strLoadAssetBundle + s_strAssetBundleExt);
				AssetBundle depBundle = AssetBundle.LoadFromFile(filePath);

				_cachedAssetBundleMap[strLoadAssetBundle] = depBundle;
				target = depBundle;
				//Debug.Log("LoadAssetBundleInternal|" + strLoadAssetBundle + "|" + (Time.realtimeSinceStartup - begin));
			}

			return target;
		}

		/// <summary>
		/// 主要PC应用的接口，用于PC上资源获取.
		/// </summary>
		/// <param name="strDir">目录.</param>
		/// <param name="strExt">后缀.</param>
		/// <param name="hasChildDir">If set to <c>true</c> has child dir.</param>
		private static void AddDirBinaryFilesToAsset(string strDir, string strExt, bool hasChildDir = false)
		{
#if UNITY_EDITOR
		string strProjectPath = Directory.GetParent(Application.dataPath).FullName;
		strProjectPath = strProjectPath.Replace("\\", "/");
		if(strProjectPath[strProjectPath.Length - 1] != '/')
		{
			strProjectPath = strProjectPath + "/";
		}

		DirectoryInfo dirInfo = new DirectoryInfo (strDir);
		if(hasChildDir)
		{
			foreach (var dirInfoItem in dirInfo.GetDirectories()) 
			{
				string dirName = dirInfoItem.Name;
				if(dirName != null && dirName.Length > 0)
				{
					AddDirBinaryFilesToAsset(strDir + dirName + "/" , strExt, false);
				}
			}
		}
		else
		{
			foreach (var fileInfo in dirInfo.GetFiles("*." + strExt, System.IO.SearchOption.AllDirectories)) 
			{
				string strName = Path.GetFileNameWithoutExtension(fileInfo.Name);
				string strUsePath = fileInfo.FullName;
				strUsePath = strUsePath.Replace("\\", "/");
				strUsePath = strUsePath.Replace(strProjectPath, "");

				if (s_assetPathDic.ContainsKey(strName) )
				{
					Debug.LogError("存在多个相同名字的资源:" + strName);
				}

				s_assetPathDic[strName] = strUsePath;
			}
		}
#endif
		}

        #region
        private static void AddAsset2BundleMap(string strpath, string bundleName)
		{
			//Debug.Log("strpath : " + strpath + " bundleName ：" + bundleName);
			AssetBundle depBundleMap = AssetBundle.LoadFromFile(strpath);
			if (depBundleMap != null)
			{
				string[] allAssetNames = depBundleMap.GetAllAssetNames();
				for (int j = 0; j < allAssetNames.Length; j++)
				{
					string assetName = Path.GetFileNameWithoutExtension(allAssetNames[j]);
					if (assetName != null && assetName.Length > 0)
					{
						if (!s_asset2BundleMap.ContainsKey(assetName))
						{
							s_asset2BundleMap.Add(assetName, bundleName);
						}
						else
						{
                            //string strOldBund = null;
                            //s_asset2BundleMap.TryGetValue(assetName, out strOldBund);
                            //Debug.Log(assetName + "|" + bundleName + "|" + strOldBund);
                        }
					}
				}

				depBundleMap.Unload(true);
			}
		}

		private static void CacheXml(string filepath, bool isOld)
		{
			if (File.Exists(filepath))
			{
				XmlDocument xmlDoc = new XmlDocument();
				xmlDoc.Load(filepath);
				XmlNodeList nodeList = xmlDoc.SelectSingleNode("AssetBundleDepend").ChildNodes;

				// 遍历每一个节点，拿节点的属性以及节点的内容;
				foreach (XmlElement xe in nodeList)
				{
					foreach (XmlElement x1 in xe.ChildNodes)
					{
						string strName = xe.GetAttribute("NAME");
						if (x1.Name == "VALUE")
						{
							string strValue = x1.InnerText;

							if (isOld)
								s_assetBundleListOld.Add(strName, strValue);
							else
								s_assetBundleListNew.Add(strName, strValue);
						}
						else if (x1.Name == "SIZE")
						{
							string strValue = x1.InnerText;

							if (isOld)
								s_assetBundleSizeListOld.Add(strName, strValue);
							else
								s_assetBundleSizeListNew.Add(strName, strValue);
						}
					}
				}

				if (isOld)
					System.IO.File.Delete(filepath);
			}
		}
        #endregion
    }
}