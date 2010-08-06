using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace LogJoint.LogMedia
{
	public interface IFileSystemWatcher: IDisposable
	{
		string Path { get; set; }
		event FileSystemEventHandler Created;
		event FileSystemEventHandler Changed;
		event RenamedEventHandler Renamed;
		bool EnableRaisingEvents { get; set; }
	};

	public interface IFileSystem
	{
		Stream OpenFile(string fileName);
		string[] GetFiles(string path, string searchPattern);
		IFileSystemWatcher CreateWatcher();
	};

	public interface IFileStreamInfo
	{
		DateTime LastWriteTime { get; }
		bool IsDeleted { get; }
	};

	class FileSystemImpl : IFileSystem
	{
		class Watcher : FileSystemWatcher, IFileSystemWatcher
		{
		};

		class StreamImpl : FileStream, IFileStreamInfo
		{
			public StreamImpl(string fileName)
				:
				base(fileName, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.ReadWrite)
			{
				this.fileName = fileName;
				this.lastTimeFileWasReopened = Environment.TickCount;
			}

			protected override void Dispose(bool disposing)
			{
				disposed = true;
				base.Dispose(disposing);
			}

			#region IFileStreamInfo Members

			public DateTime LastWriteTime
			{
				get 
				{
					if (disposed)
					{
						throw new ObjectDisposedException(GetType().Name);
					}

					// Try to detect the time via file handle. It is faster than File.GetLastWriteTime()
					long created, modified, accessed;
					if (GetFileTime(this.SafeFileHandle, out created, out modified, out accessed))
					{
						return DateTime.FromFileTime(modified);
					}

					// This is default implementation
					return File.GetLastWriteTime(fileName);
				}
			}

			public readonly long DeletionDetectionLatency = 3 * 1000;

			public bool IsDeleted
			{
				get 
				{
					if (disposed)
					{
						throw new ObjectDisposedException(GetType().Name);
					}

					if (!isOnNTFSDrive.HasValue)
					{
						isOnNTFSDrive = IsOnNTFSVolume(fileName);
					}

					// First, try quick but platform-dependent way
					if (isOnNTFSDrive.Value)
					{
						BY_HANDLE_FILE_INFORMATION info;
						// GetFileInformationByHandle is not guaranteed to work ok on all file systems.
						// Call it only if we are on NTFS drive. 
						if (GetFileInformationByHandle(this.SafeFileHandle, out info))
						{
							return info.nNumberOfLinks == 0;
						}
					}

					long ticks = Environment.TickCount;

					// Use slow but decent way otherwise. Do it not more than once per DeletionDetectionLatency.
					if (ticks - lastTimeFileWasReopened > DeletionDetectionLatency)
					{
						try
						{
							(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)).Dispose();
						}
						catch (UnauthorizedAccessException)
						{
							// Opening of a file that has been deleted while being open results in error 5: access denied.
							return true;
						}
						lastTimeFileWasReopened = ticks;
					}

					return false;
				}
			}

			#endregion

			[System.Runtime.InteropServices.DllImport("kernel32.dll")]
			static extern bool GetFileTime(
				Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
				out long lpCreationTime,
				out long lpLastAccessTime,
				out long lpLastWriteTime
			);

			[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
			public struct BY_HANDLE_FILE_INFORMATION
			{
				public UInt32 dwFileAttributes;
				public UInt64 ftCreationTime;
				public UInt64 ftLastAccessTime;
				public UInt64 ftLastWriteTime;
				public UInt32 dwVolumeSerialNumber;
				public UInt32 nFileSizeHigh;
				public UInt32 nFileSizeLow;
				public UInt32 nNumberOfLinks;
				public UInt32 nFileIndexHigh;
				public UInt32 nFileIndexLow;
			};
			[System.Runtime.InteropServices.DllImport("kernel32.dll")]
			public static extern bool GetFileInformationByHandle(
				Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
				out BY_HANDLE_FILE_INFORMATION lpFileInformation
			);

			static bool IsOnNTFSVolume(string path)
			{
				DriveInfo drive;
				try
				{
					drive = new DriveInfo(Path.GetPathRoot(path));
				}
				catch (ArgumentException)
				{
					return false;
				}
				try
				{
					return drive.DriveFormat == "NTFS";
				}
				catch (DriveNotFoundException)
				{
					return false;
				}
			}

			readonly string fileName;
			bool? isOnNTFSDrive;
			bool disposed;
			long lastTimeFileWasReopened;
		};

		public Stream OpenFile(string fileName)
		{
			return new StreamImpl(fileName);
		}
		public string[] GetFiles(string path, string searchPattern)
		{
			return Directory.GetFiles(path, searchPattern);
		}
		public IFileSystemWatcher CreateWatcher()
		{
			return new Watcher();
		}

		public static FileSystemImpl Instance = new FileSystemImpl();
	};
}
