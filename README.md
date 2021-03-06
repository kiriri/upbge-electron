
# upbge-electron

`Forward UPBGE renders to Electron or Unity and control Blender from Electron (js) or Unity (c#)`

![Electron UPBGE Demo](.github/upbge-electron-demo.gif?raw=true "UPBGE in Electron Window")

Only works on Windows and X11  

Requires an up-to-date version of Node.js and npm for the electron version.
The Unity version requires .NET 4.x and the unsafe flag.

## How it works (Electron)

First, Electron is started. This creates the Browser Window that the final render is displayed in.
Then the Electron process (see main_renderer.mjs) opens blenderplayer as a child-process. 

4 Pipes are opened. They are stdin, stdout, stderr, and ipc, in that order (mapped to fd 0-3) .
- Stdin is used to send commands from electron to blender. These get sent as utf-8 JSON.
- Stdout and stderr get written to by the blender process, and Electron will forward them to its own console. But don't depend on stderr, python errors often get lost at some point, and I'm not quite sure why.
- Finally, ipc is used by Blender to send data back to Electron. This data is always in response to a command Electron sent via stdin. The ipc data is binary. The first 4 bytes define the length of the data packet. The rest is raw binary data, that must be manually parsed in Electron. All python API-accessible functions must write at the very least an empty message into ipc on completion for electron to recognize that the function call was successful.

This alone is enough for most communication between Electron and Blender. But when it comes to large data packets, such as HD or 4k images, Pipes become a bottleneck. 
This is where Memory Mapped Files come in. These are normal files on the hard-drive, that are mapped into RAM, and then accessed via buffers ( Which are essentially C-style Pointer abstractions ). The Buffer in Node points to the exact same memory region as the Buffer in Python. This allows for instantaneous data exchange between Node and Python. Memory Mapped Files will not write to hard-drive unless explicitly told to, so they shouldn't degrade SSDs, if all works as intended.

This is how we can reach 60 fps. For a simple HD blender scene, Eevie renders take ~4ms, GPU->CPU fetching the pixels takes about ~8ms on PCIe 3 (about 1ms per mb). Using Asynchronicity, we can render the next frame while rendering the old one to the Electron canvas to reach 60+ fps. 

## How it works (Unity)

.NET does not support any pipes other than stdin, stdout and stderr. The ipc pipe used by Electron is therefore replaced with a FIFO on Linux and a Named Pipe on Windows. The FIFO is faster, but does not exist on Windows.

Otherwise the Unity version works the same way the Electron one does.

## How to use

Download a release package for ease of use. Then just enter the "game" folder and run "npm start" to run the Electron version.
To run the Unity version, just open the Unity folder in the Unity Editor, open the example scene, and play.

There's currently no automated game build pipeline, you'll have to copy upbge to your final build's resource folder and potentially edit the paths in-script.

## How to build

See setup_guide.txt for a list of changes that were made to upbge.

To build from source, build upbge by following the normal Blender build instructions, then enter the "game" folder and run "npm i" to install dependencies. If there is an mmap error on Windows, remove the modified version of mmap that ships with this project and reinstall it from npm. On Linux, try running "npm run rebuild-modules" instead. If a python module is missing, copy it from the modified_python/python/lib folder.

## Possible future optimizations

Python :

- Use multiprocessing.Process for true multithreading with shared variables. Maybe something can be offloaded?

I'm open to suggestions.

## Planned features

- Run several instances at once
- Run blender (as opposed to blenderplayer) headerlessly and provide an API for asynchronous tasks, especially still and animation renders.
