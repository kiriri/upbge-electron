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
using Microsoft.Win32.SafeHandles;
using UnityEngine;

using Debug = UnityEngine.Debug;


public class Main : MonoBehaviour
{
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
	const string path = "/dev/shm/test.sock";
	const string mmap_path = "/dev/shm/test";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
	const string path = "\\\\.\\test.sock";
	const string mmap_path = Path.GetTempPath() + "test";
#endif


	// Render Target. Contains Blender render.
	Texture2D texture;

	// Render Target. Blender result is mapped onto this.
	public UnityEngine.UI.RawImage target;

	// Blender Process
	System.Diagnostics.Process process;
	// Blender Function calls return results via this stream.
	BinaryReader output_stream;

	// Pointer to the shared memory location where blender puts the render image
	IntPtr mmap_pointer;

	// List of Promises to blender function call results. They get removed as soon as they are resolved.
	Queue<Promise<byte[]>> promises = new Queue<Promise<byte[]>>();

	// Await for the next blender frame to finish rendering
	Promise<byte[]> next_frame_promise;

	// True once all IPC is properly setup and Blender is ready to go.
	bool initialized = false;

	// Start is called before the first frame update
	void Start()
	{
		texture = new Texture2D(1920, 1080, TextureFormat.RGBA32, false, false);
		target.texture = texture;


		//print("Started " + new Comm(3).Stream);

		InitializeUPBGE();

		// using (NamedPipeServerStream server = new NamedPipeServerStream(path,PipeDirection.Out,1,PipeTransmissionMode.Byte))
		// {
		//     Debug.Log("Waiting for Connection");
		//     server.WaitForConnection();
		//     Debug.Log("Connection Established");

		//     int cnt = 0;

		// 	//string line = Console.ReadLine();
		//         byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes((++cnt).ToString() + ": " + "line");
		//         server.Write(messageBytes, 0, messageBytes.Length);
		// 	Debug.Log("Wrote line");
		// 	// int i = 2;
		//     // while (i>1)
		//     // {
		// 	// 	i--;

		//     // }
		// }
	}

	async void InitializeUPBGE()
	{
		Debug.Log(Path.GetTempPath());
		var fileName = Application.dataPath + "/../../upbge-master/build_linux/bin/blenderplayer";
		var arguments = Application.dataPath + "/../../main.blend";
		var process = this.process = new System.Diagnostics.Process();
		process.StartInfo.FileName = fileName;
		process.StartInfo.Arguments = arguments;

		process.StartInfo.UseShellExecute = false;
		process.StartInfo.RedirectStandardOutput = true;
		process.StartInfo.RedirectStandardInput = true;
		process.StartInfo.RedirectStandardError = true;
		process.OutputDataReceived += (sender, args) => Debug.Log("received output: " + args.Data);
		process.ErrorDataReceived += (sender, args) => Debug.Log("received error: " + args.Data);
		process.Start();
		process.BeginOutputReadLine();


		await Task.Delay(1000);
		ReadOutputs();

		await send_message("set_environment", new[] { "cs" });
		await send_message("set_resolution", new[] { "1920", "1080" });
		next_frame_promise = send_message("next_frame", new string[0]);
		InitializeMMap();
		UpdateImage();

		initialized = true;

	}


	async void Update()
	{
		if(!initialized)
			return;

		var fetch_image_promise = send_message("fetch_image", new string[0]); 
		await next_frame_promise;
		await fetch_image_promise;
		UpdateImage();
		next_frame_promise = send_message("next_frame", new string[0]);

	}

	void OnApplicationQuit()
	{
		process.Kill();
	}

	Promise<byte[]> send_message(string method_name, string[] args)
	{
		var result = new Promise<byte[]>();
		promises.Enqueue(result);
		process.StandardInput.Write("{ \"method\": \"" + method_name + "\", \"params\": [\"" + String.Join("\",\"", args) + "\"],  \"time\":" + DateTime.Now.Millisecond + " }\r\n");
		return result;
	}



	unsafe void InitializeMMap()
	{
		using (var mmf = MemoryMappedFile.CreateFromFile(mmap_path, FileMode.Open, mmap_path))
		{
			using (var accessor = mmf.CreateViewAccessor())
			{
				byte* pointer = null;
				accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
				mmap_pointer = new IntPtr(pointer);			
			}
		}
	}

	void UpdateImage()
	{
		var watch = new Stopwatch();
		watch.Start();

		this.texture.LoadRawTextureData(mmap_pointer, 1920 * 1080 * 4);
		texture.Apply();
		
		//Debug.Log("Done " + watch.ElapsedMilliseconds);
	}

	void ReadOutputs()
	{
		Debug.Log("Path is  " + path);


		// Read and display lines from the file until the end of
		// the file is reached.
		new Thread(() =>
		{
			Thread.CurrentThread.IsBackground = true;

			Debug.Log("Started Thread");

			while (true)
			{
				var file = new FileInfo(path).OpenRead();

				output_stream = new BinaryReader(file);

				var length = output_stream.ReadInt32(); // blocks forever?
				//Debug.Log("Length is " + length);
				byte[] line = output_stream.ReadBytes(length);
				//if (line.Length > 0)
				//	Debug.Log("Output found " + String.Join(",", line)); // 28

				var promise = promises.Dequeue();
				promise.resolve(line);
			}
		}).Start();

	}

}


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