using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using UnityEngine;


public class Main : MonoBehaviour
{
#if UNITY_STANDALONE_LINUX || UNITY_EDITOR_LINUX
	const string path = "/dev/shm/test.sock";
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
	const string path = "\\\\.\\test.sock";
#endif


	System.Diagnostics.Process process;
	BinaryReader output_stream;

	// Start is called before the first frame update
	void Start()
	{


		//print("Started " + new Comm(3).Stream);

		StartCoroutine(WaitForConnection());

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

	void OnApplicationQuit()
	{
		process.Kill();
	}

	void send_message(string method_name, string[] args)
	{
		process.StandardInput.Write("{ \"method\": \"" + method_name + "\", \"params\": [\"" + String.Join("\",\"", args) + "\"],  \"time\":" + DateTime.Now.Millisecond + " }\r\n");
	}

	IEnumerator WaitForConnection()
	{
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


		yield return new WaitForSeconds(2);
		ReadOutputs2();

		send_message("set_environment", new[] { "cs" });
		send_message("set_resolution", new[] { "1920","1080" });


		//StartCoroutine(ReadOutputs());


	}

	IEnumerator ReadOutputs()
	{
		Debug.Log("Path is  " + path);
		var file = new FileInfo(path).OpenRead();

		output_stream = new BinaryReader(file);
		yield return new WaitForSeconds(0.1f);

		byte[] line;
		var length = output_stream.ReadInt32(); // blocks forever?
		Debug.Log(length);
		// Read and display lines from the file until the end of
		// the file is reached.
		while (true)
		{
			//if(output_stream.DataAvailable)
			line = output_stream.ReadBytes(int.MaxValue);
			if (line.Length > 0)
				Debug.Log("Output found " + String.Join(",", line)); // 28
			yield return new WaitForSeconds(0.1f);
		}
	}

	void ReadOutputs2()
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
				Debug.Log("Length is " + length);
				byte[] line = output_stream.ReadBytes(length);
				if (line.Length > 0)
					Debug.Log("Output found " + String.Join(",", line)); // 28
			}
		}).Start();
		
	}

	/**
	* Call a function in blender's api.py script remotely. Returns a promise with a view to the data. This view is volatile and should be copied asap.
	* @param {string} method_name Name of the method in python
	* @param {Array} params array of parameters
	* @returns {Uint8ClampedArray} result in bytes (result exists on shared_buffer object) . Volatile.
	*/
	// async Buffer remote_command(string method_name, string[] args)
	// {
	// 	let promise = new Promise((resolve) =>
	// 	{
	// 		result_queue.push(resolve)
	// 	})

	// 	blender_process.stdin.write(JSON.stringify({ 'method': method_name, args, time:Date.now() }) + '\r\n', (e) => { if (e) console.error(e);  });

	// 	let array = await promise;
	// 	return array;
	// }
}


class Comm : IDisposable
{
	// [DllImport("MSVCRT.DLL", CallingConvention = CallingConvention.Cdecl)]
	// extern static IntPtr _get_osfhandle(int fd);

	[DllImport("libc.so.6")]
	extern static int fileno(int fd); // BUG : fd is File*, which we don't have access to
									  //private static extern int getpid ();

	public readonly Stream Stream;

	public Comm(int fd)
	{
		//var handle = getpid();
		var handle = fileno(fd);
		// if (handle == IntPtr.Zero || handle == (IntPtr)(-1) || handle == (IntPtr)(-2))
		// {
		//     throw new ApplicationException("invalid handle");
		// }

		// var fileHandle = new SafeFileHandle(handle, true);
		// Stream = new FileStream(fileHandle, FileAccess.ReadWrite);
	}

	public void Dispose()
	{
		Stream.Dispose();
	}
}