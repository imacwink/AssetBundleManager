using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ST.Manager
{
    public class STDownLoadProgress
    {
		public int mFileIndex = 0;  /*文件索引*/
		public int mTotalFileCount = 0;  /*全部文件总数*/
		public long mDownLoadBytes = 0; /*下载的字节数*/
		public long mTotalBytes = 0; /*总字节数*/
		public decimal mProgress = 0; /*下载进度*/
		public bool mDownloadFinsih = false; /*下载是否结束*/
		public bool mDownload = false;
		public bool mCompressing = false; /*资源解压中*/
		public bool mCompressFinish = false; /*解压完毕*/
	}
}
