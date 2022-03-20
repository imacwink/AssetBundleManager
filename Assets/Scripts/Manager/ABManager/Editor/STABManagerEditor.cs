using UnityEngine;
using UnityEditor;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ST.Manager.Editor
{
	public class STExportPathDef
	{
		public static string s_iOS = "iPhone";
		public static string s_Android = "Android";
		public static string s_StandaloneWindows = "StandaloneWindows";
		public static string s_macOS = "macOS";

		public static string s_SubPath_Boot = "Boot";
	};

	public class STABManagerEditor
    {
        private static string s_strExportPath = "Assets/StreamingAssets/";
        private static BuildAssetBundleOptions s_options = BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.DeterministicAssetBundle;
        private static string s_strTargetExt = ".manifest";
        private static string s_strTargetZipExt = ".bytes";
        private static Dictionary<string, string> s_assetBundleDependDic = new Dictionary<string, string>(); /*文件名，文件MD5名*/

        #region Build

        [MenuItem("Tools/AssetBundle/Build/iOS")]
        public static void ExportIOSResource()
		{
			RenameAll();
			BuildAssetResource(s_strExportPath + STExportPathDef.s_iOS + "/", BuildTarget.iOS);
		}

		[MenuItem("Tools/AssetBundle/Build/Android")]
		public static void ExportAndroidResource()
		{
			RenameAll();
			BuildAssetResource(s_strExportPath + STExportPathDef.s_Android + "/", BuildTarget.Android);
		}

		[MenuItem("Tools/AssetBundle/Build/StandaloneWindows")]
		public static void ExportWPResource()
		{
			RenameAll();
			BuildAssetResource(s_strExportPath + STExportPathDef.s_StandaloneWindows  + "/", BuildTarget.StandaloneWindows);
		}

		[MenuItem("Tools/AssetBundle/Build/macOS")]
		public static void ExportMacOSResource()
		{
			RenameAll();
			BuildAssetResource(s_strExportPath + STExportPathDef.s_macOS + "/", BuildTarget.StandaloneOSX);
		}

		#endregion

		#region Packaging related functions

		public static void BuildAssetResource(string strPath, BuildTarget buildTarget, string strSubPath = "")
		{
			if (Directory.Exists(strPath)) Directory.Delete(strPath, true);
			Directory.CreateDirectory(strPath);

			s_assetBundleDependDic.Clear(); /* Clear Cache*/

			// 导出路径关键文件夹名;
			string strExportPath = GetExportPathByBuildTarget(buildTarget, strSubPath);

			// Gen AssetBundle;
			AssetBundleManifest assetBundleManifest = BuildPipeline.BuildAssetBundles(strPath, s_options, buildTarget);

			// Compress encrypted files（Desc）;
			string strdepAssetName = Path.GetFileNameWithoutExtension(STABManager.s_strAssetBundleDescFile);
			Ionic.Zlib.ZlibStream.CompressFile(strPath + strExportPath, strPath + "/" + strdepAssetName + s_strTargetZipExt);
			File.Delete(strPath + strExportPath);
			Ionic.Zlib.ZlibStream.CompressFile(strPath + strExportPath + s_strTargetExt, strPath + "/" + strdepAssetName + s_strTargetExt + s_strTargetZipExt);
			File.Delete(strPath + strExportPath + s_strTargetExt);

			// Compress encrypted files;
			if (assetBundleManifest != null)
			{
				string[] allAssetBundles = assetBundleManifest.GetAllAssetBundles();
				for (int i = 0; i < allAssetBundles.Length; i++)
				{
					// Debug.Log("Begin Compress : " + allAssetBundles[i]);
					CompressAndEncryptedRes(strPath, allAssetBundles[i]);
				}
			}

			CreateXml(strExportPath);

			AssetDatabase.Refresh();
		}

		private static string GetExportPathByBuildTarget(BuildTarget buildTarget, string strSubPath = "")
		{
			string strExportPath = "";
			switch (buildTarget)
			{
				case BuildTarget.iOS:
					strExportPath = STExportPathDef.s_iOS;
					break;
				case BuildTarget.Android:
					strExportPath = STExportPathDef.s_Android;
					break;
				case BuildTarget.StandaloneWindows:
					strExportPath = STExportPathDef.s_StandaloneWindows;
					break;
				case BuildTarget.StandaloneOSX:
					strExportPath = STExportPathDef.s_macOS;
					break;
				default:
					break;
			}

			if (strSubPath.Length > 0)
			{
				strExportPath = strExportPath + "/" + strSubPath;
			}

			return strExportPath;
		}

		private static void CompressAndEncryptedRes(string strPath, string strResName)
		{
			string strMD5Text = CalcAssetMd5(strPath + strResName);
			s_assetBundleDependDic.Add(strResName, strMD5Text);
			// Debug.Log(strResName + "|" + strMD5Text);
			Ionic.Zlib.ZlibStream.CompressFile(strPath + strResName, strPath + "/" + strMD5Text + s_strTargetZipExt);
			File.Delete(strPath + strResName);

			string strMD5TextManifest = CalcAssetMd5(strPath + strResName + s_strTargetExt);
			s_assetBundleDependDic.Add(strResName + s_strTargetExt, strMD5TextManifest);
			// Debug.Log(strResName + s_strTargetExt + "|" + strMD5TextManifest);
			Ionic.Zlib.ZlibStream.CompressFile(strPath + strResName + s_strTargetExt, strPath + "/" + strMD5TextManifest + s_strTargetZipExt);
			File.Delete(strPath + strResName + s_strTargetExt);
		}

		public static string CalcAssetMd5(string strAssetPath)
		{
			byte[] szAssetBytes = File.ReadAllBytes(strAssetPath);
			MD5 md5Hash = MD5.Create();
			byte[] szMd5Value = md5Hash.ComputeHash(szAssetBytes);

			StringBuilder sBuilder = new StringBuilder();
			for (int i = 0; i < szMd5Value.Length; i++)
			{
				sBuilder.Append(szMd5Value[i].ToString("x2"));
			}

			return sBuilder.ToString();
		}

		public static void CreateXml(string strPath)
		{
			string filepath = Application.dataPath + "/StreamingAssets/" + strPath + @"/assetBundleDepend.xml";

			if (File.Exists(filepath))
				File.Delete(filepath);

			XmlDocument xmlDoc = new XmlDocument();
			XmlElement root = xmlDoc.CreateElement("AssetBundleDepend");

			foreach (var temp in s_assetBundleDependDic)
			{
				XmlElement elm = xmlDoc.CreateElement("FILE");
				elm.SetAttribute("NAME", temp.Key);
				XmlElement value = xmlDoc.CreateElement("VALUE");
				value.InnerText = temp.Value;
				XmlElement value2 = xmlDoc.CreateElement("SIZE");
				string strFileSize = CalcFileSize(Application.dataPath + "/StreamingAssets/" + strPath + @"/" + temp.Value + s_strTargetZipExt).ToString();
				value2.InnerText = strFileSize;
				elm.AppendChild(value);
				elm.AppendChild(value2);
				root.AppendChild(elm);
			}

			xmlDoc.AppendChild(root);
			xmlDoc.Save(filepath);

			// 压缩文件;
			Ionic.Zlib.ZlibStream.CompressFile(filepath, Application.dataPath + "/StreamingAssets/" + strPath + @"/assetBundleDepend" + s_strTargetZipExt);
			File.Delete(filepath);
		}

		public static long CalcFileSize(string strFilePath)
		{
			long lTemp = 0;

			// 判断当前路径所指向的是否为文件;
			if (File.Exists(strFilePath) == false)
			{
				string[] strAllFileNames = Directory.GetFileSystemEntries(strFilePath);
				foreach (string fileName in strAllFileNames)
				{
					lTemp += CalcFileSize(fileName);
				}
			}
			else
			{
				// 定义一个 FileInfo 对象, 使之与 strFilePath 所指向的文件向关联, 以获取其大小;
				FileInfo fileInfo = new FileInfo(strFilePath);
				return fileInfo.Length;
			}
			return lTemp;
		}

		#endregion

		#region Rename AB Name

		[MenuItem("Tools/AssetBundle/Rename/All")]
		public static void RenameAll()
		{
			Rename_Bytes();
			Rename_Texture();
			Rename_Prefab();
			Rename_Sound();
			Rename_Scene();
			Rename_Lua();
		}

		[MenuItem("Tools/AssetBundle/Rename/Bytes")]
		public static void Rename_Bytes()
		{
			SetAssetNameInChildDir("Assets/Artwork/Download/Bytes/", "txt", "asset_bytes");
			SetAssetNameInChildDir("Assets/Artwork/Download/Bytes/", "bytes", "asset_bytes");
		}

		[MenuItem("Tools/AssetBundle/Rename/Texture")]
		public static void Rename_Texture()
		{
			SetAssetNameInChildDir("Assets/Artwork/Download/Texture/", "png", "asset_texture");
			SetAssetNameInChildDir("Assets/Artwork/Download/Texture/", "jpg", "asset_texture");
		}

		[MenuItem("Tools/AssetBundle/Rename/Prefab")]
		public static void Rename_Prefab()
		{
			SetAssetNameInChildDir("Assets/Artwork/Download/Prefab/", "prefab", "asset_prefab");
		}

		[MenuItem("Tools/AssetBundle/Rename/Sound")]
		public static void Rename_Sound()
		{
			SetAssetNameInChildDir("Assets/Artwork/Download/Sound/", "ogg", "asset_sound");
			SetAssetNameInChildDir("Assets/Artwork/Download/Sound/", "mp3", "asset_sound");
		}

		[MenuItem("Tools/AssetBundle/Rename/Scene")]
		public static void Rename_Scene()
		{
			SetAssetNameInChildDir("Assets/Artwork/Download/Scene/", "unity", "asset_scene");
		}

		[MenuItem("Tools/AssetBundle/Rename/Lua")]
		public static void Rename_Lua()
		{
			SetAssetNameInChildDir("Assets/Artwork/Download/Lua/", "lua", "asset_lua");
		}

		public static void SetAssetNameInChildDir(string strDir, string strExt, string assetBundleName)
		{
			string strApplicationDir = Application.dataPath;
			DirectoryInfo dirInfo = new DirectoryInfo(strDir);

			foreach (var fileInfo in dirInfo.GetFiles("*." + strExt, System.IO.SearchOption.AllDirectories))
			{
				string strUsePath = fileInfo.FullName;
				string strFileName = fileInfo.Name;

				strUsePath = strUsePath.Replace("\\", "/");
				strUsePath = strUsePath.Replace(strApplicationDir + "/", "");
				strUsePath = strUsePath.Replace("/" + strFileName, "");

				SetVersionDirAssetName(strUsePath, assetBundleName);
			}
		}

		public static void SetVersionDirAssetName(string versionDir, string assetBundleName)
		{
			var fullPath = Application.dataPath + "/" + versionDir + "/";
			var relativeLen = versionDir.Length + 8; // Assets 长度
			if (Directory.Exists(fullPath))
			{
				EditorUtility.DisplayProgressBar("Set assetName name", "Setting assetName name...", 0f);
				var dir = new DirectoryInfo(fullPath);
				var files = dir.GetFiles("*", SearchOption.AllDirectories);
				for (var i = 0; i < files.Length; ++i)
				{
					var fileInfo = files[i];
					EditorUtility.DisplayProgressBar("Set assetName name", "Setting assetName name...", 1f * i / files.Length);

					if (!fileInfo.Name.EndsWith(".meta"))
					{
						var basePath = fileInfo.FullName.Substring(fullPath.Length - relativeLen).Replace('\\', '/');
						var importer = AssetImporter.GetAtPath(basePath);
						if (importer /*&& importer.assetBundleName != versionDir*/)
						{
							if (fileInfo.Name.Contains(".unity"))
							{
								importer.assetBundleName = assetBundleName + "_" + fileInfo.Name.Replace(".unity", "");
							}
							else
							{
								importer.assetBundleName = assetBundleName; // + "_" + fileInfo.Name.Replace(".unity", "");	
							}
						}
					}
				}
				EditorUtility.ClearProgressBar();
			}
		}
		#endregion
	}
}
