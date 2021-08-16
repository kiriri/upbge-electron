using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class Main : MonoBehaviour
{ 
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
	const string path = "/dev/shm/testsocket";
	const string mmap_path = "/dev/shm/test.tmp";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
	const string path = "testsocket";
	readonly string mmap_path = Path.GetTempPath() + "test.tmp";
#endif

	Texture2D texture; // Render Target. Contains Blender render.

	public UnityEngine.UI.RawImage target; // Render Target. Blender result is mapped onto this.
	
	System.Diagnostics.Process process; // Blender Process

	BinaryReader output_stream; // Blender Function-calls return results via this stream.

	// List of Promises to blender function call results. They get removed as soon as they are resolved.
	Queue<Promise<byte[]>> promises = new Queue<Promise<byte[]>>();
	
	Promise<byte[]> next_frame_promise; // Await for the next blender frame to finish rendering

	bool initialized = false; // True once all IPC is properly setup and Blender is ready to go.
	bool updating = false; // Semaphore for our async Update() function (presumably Unity's fps is significantly faster than the GPU->CPU + UPBGE->Unity bridge).

	int last_frame_time = 0; // Timestamp in ms of last image update. Used to measure UPBGE-Bridge fps.

	MemoryMappedFile mmf = null; // This Memory Mapped File contains the image and is essentially set from Blender-Internal directly.
	FileStream mm_fs = null; // memory mapped file stream. Needs to be closed on exit.
	IntPtr mmap_pointer; // Pointer to the shared memory location where blender puts the render image

	public int initialWidth = 1920;
	public int initialHeight = 1080;

	int height = -1;
	int width = -1;

	int mmapSize = 0; // in pixels. Mmap may be larger than height*width, but never smaller

	// Start is called before the first frame update
	void Start()
	{

		// Windows is very finicky about shared contexts. Which is why we need to create it from c# with fileShare.ReadWrite, or it'll refuse to share the mmap. And it won't tell you why. So be careful when modifying this part.
		mm_fs = File.Open(mmap_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
		var info = new byte[initialWidth*initialHeight*4];
		mm_fs.Write(info,0,initialWidth*initialHeight*4);
		
		//UpdateResolution(1920,1080);

		InitializeUPBGE();
	}



	// Start UPBGE process, redirect console and error messages, setup mmap and ipc.
	async void InitializeUPBGE()
	{
		#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			var fileName = Application.dataPath + "/../../upbge-master/build_linux/bin/blenderplayer";
		#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			var fileName = Application.dataPath + "/../../upbge-master/build_windows_x64_vc16_Release/bin/Release/blenderplayer.exe";
		#endif
		

		// Initialize UPBGE process:
		var arguments = Application.dataPath + "/../../main.blend";
		var process = this.process = new System.Diagnostics.Process();
		process.EnableRaisingEvents = true;
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.FileName = fileName;
		process.StartInfo.Arguments = arguments;

		// Forward Python output and errors to Unity's console
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardOutput = true; // BUG : Stutters after a while. This is not caused by the Unity Console
		process.StartInfo.RedirectStandardInput = true;
		process.StartInfo.RedirectStandardError = true;
		process.OutputDataReceived += (sender, args) => Debug.Log("P|" + args.Data);
		process.ErrorDataReceived += (sender, args) => Debug.LogError("P|" + args.Data);
		process.Start();
		process.BeginOutputReadLine();

		// Wait to make sure the socketets are working at least.
		await Task.Delay(1000);

		send_message("set_environment", new[] { "cs" });

		// Setup ipc receive Thread
		ReadOutputs();

		byte[] result = await send_message("set_resolution", new[] { initialWidth.ToString(), initialHeight.ToString() });
		var newWidth = BitConverter.ToUInt16(result,0);
		var newHeight = BitConverter.ToUInt16(result,2);
		UpdateResolution(newWidth,newHeight);
		next_frame_promise = send_message("next_frame", new string[0]);

		UpdateImage();

		initialized = true;
	}

	// Called each Unity Frame. A Unity Frame does not necessarily equate an UPBGE frame, though UPBGE's framerate is limited by Unity's.
	async void Update()
	{
		if(!initialized || updating) // Skip this frame. UPBGE is busy or not yet initialized.
		{
			return;
		}
		updating = true; // Semaphore.

		var fetch_image_promise = send_message("fetch_image", new string[0]);
		await next_frame_promise;
		await fetch_image_promise;
		UpdateImage();
		next_frame_promise = send_message("next_frame", new string[0]);
		Debug.Log("Took " + (DateTime.Now.Millisecond - last_frame_time));
		last_frame_time = DateTime.Now.Millisecond;

		updating = false;
	}

	// Cleanup persistent resources and close UPBGE child process.
	void OnApplicationQuit()
	{
		process.Kill();
		this.mm_fs.Dispose();
		this.mmf.Dispose();
	}

	// Call a method in python's api.py
	Promise<byte[]> send_message(string method_name, string[] args)
	{
		var result = new Promise<byte[]>();
		promises.Enqueue(result);
		process.StandardInput.Write("{ \"method\": \"" + method_name + "\", \"params\": [\"" + String.Join("\",\"", args) + "\"],  \"time\":" + DateTime.Now.Millisecond + " }\r\n");
		return result;
	}

	// Set the resolution of both the Texture used by Unity, as well as the variables used to delimit internal buffers etc.
	void UpdateResolution(int newWidth, int newHeight)
	{
		if(newWidth == width && newHeight == height)
			return;

		width = newWidth;
		height = newHeight;

		texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
		target.texture = texture;

		target.rectTransform.sizeDelta = new Vector2(width,height);

		// Update mmap
		if(mmapSize < width * height)
		{
			mmapSize = width * height;
			
			// Python will have expanded the file at this point. We just need to reload it.
			#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
				if(this.mmf != null)
				{
					mmf.Dispose();
				}
				mmf = MemoryMappedFile.CreateFromFile(mm_fs,"testmmap", 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.Inheritable,false );
			#endif

			InitializeMMap();

		}
	}

	// Initialize the Mapped Memory File that stores UPBGE's image data. Unity will handle this image data as a Pointer, which can be passed directly to Unity's C-Internals.
	unsafe void InitializeMMap()
	{
		Debug.Log(mmap_path);
		
		#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			// On Linux, Python creates the file, and cs uses it. On Windows, it's the other way around, which is why the file is created in Start()
			if(this.mmf != null)
			{
				this.mmf.Dispose();
			}

			this.mmf = MemoryMappedFile.CreateFromFile(mmap_path, FileMode.Open, mmap_path);
		#endif
		
		using (var accessor = mmf.CreateViewAccessor())
		{
			byte* pointer = null;
			accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
			mmap_pointer = new IntPtr(pointer);			
		}
	
	}

	// Take MMap data containing UPBGE's render and upload it into our texture. This needs to happen in the main thread.
	void UpdateImage()
	{
		var watch = new Stopwatch();
		watch.Start();

		this.texture.LoadRawTextureData(mmap_pointer, width*height * 4);
		texture.Apply();
		
		Debug.Log("Done " + watch.ElapsedMilliseconds);
	}

	// Receive results of python function calls. This happens in a separate thread, so we can block while waiting for streams to finish.
	void ReadOutputs()
	{
		new Thread(() =>
		{
			Thread.CurrentThread.IsBackground = true;

#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
			var file = new FileInfo(path).OpenRead(); // Linux uses a FIFO, which is more efficient than Windows' Named Pipe, but not available on Windows.
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
			NamedPipeServerStream file = new NamedPipeServerStream(path,PipeDirection.In,1,PipeTransmissionMode.Byte);
			
			Console.WriteLine("Waiting for Connection");
			file.WaitForConnection();
			Debug.Log("Connection Established");
#endif
			output_stream = new BinaryReader(file);
			while (true)
			{
				var length = output_stream.ReadInt32(); // Blocks until the next 4 bytes come in. These describe the length of the next message.
				byte[] data = output_stream.ReadBytes(length); // read the message
				
				var promise = promises.Dequeue();
				promise.resolve(data); // return the data as the result of the promise, which was registered when the function call was initiated in c#.
			}
		}).Start();
	}
}


// Analogous to JS. Await a promise to block a thread. Resolve a promise to unblock the waiting threads. Awaiting resolved Promises continues asap.
public class Promise<T> : System.Object
{
	public TaskCompletionSource<T> result = new TaskCompletionSource<T>();
	
	public TaskAwaiter<T> GetAwaiter()
	{
		var task = result.Task;
		return task.GetAwaiter();
	}

	public void resolve(T value)
	{
		result.SetResult(value);
	}
}
