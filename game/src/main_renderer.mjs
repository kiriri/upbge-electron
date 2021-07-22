//const mmap = require('@raygun-nickj/mmap-io');
const { remote, app} = require('electron');
const fs = require('fs');
const { BrowserWindow, process } = remote
const stream = require('stream');
const $ = require('jquery');
const { spawn, exec } = require("child_process");

const mmap = require("mmap-io")
var resolution = [1920, 1080]

const MAX_DATA = 4*1920*1080 * 4 + 32; // initial max data passed between python and nodejs. Currently at least 1 4k image + 32 bytes for some variables

console.log('Entered Main renderer')

// const app_path = remote.app.getAppPath();
// let parent_dir_count = app_path.split("/").length - 1;
// let path_to_system_root = "";
// for (let i = 0; i < parent_dir_count; i++)
// 	path_to_system_root += "../"
// console.log("Root path is ", app_path);


function hex_to_color(str,len= str.length < 6 ? 1 : 2) 
{
	let offset = str[0] == "#" ? 1 : 0;
	let result = [];
	for(; offset < str.length; offset += len)
	{
		result.push(parseInt(str.substr(offset,len), 16))
	}
	return result;
  }



const wait = ms => new Promise(res => remote.getGlobal('setTimeout')(res, ms));



// let cmd = '../upbge-master/build_linux/bin/blenderplayer ../main.blend';
// let blender_process = exec(cmd, {encoding: 'binary', maxBuffer: 5000*1024})
// exec(cmd, {encoding: 'binary', maxBuffer: 5000*1024}, function(error, stdout) {
// 	//fs.writeFileSync('test2.jpg', stdout, 'binary');
//   });

//animation=True,write_still=True
// let blender_process = exec('../upbge-master/build_linux/bin/blenderplayer ../main.blend',{encoding: 'binary', maxBuffer: 5000*1024,stdio:['pipe','pipe','pipe','pipe']},function(error, stdout) {
// 	// 	//fs.writeFileSync('test2.jpg', stdout, 'binary');

// }) //,'-P','../start_game.py','test' ,'--python-console', '-P','../ramdisk_test.py',

let blender_process = spawn('../upbge-master/build_linux/bin/blenderplayer', ['../main.blend'], { stdio: ['pipe', 'pipe', 'pipe', 'pipe'] }) //,'-P','../start_game.py','test' ,'--python-console', '-P','../ramdisk_test.py',
// var fStream = fs.createReadStream('/foo/bar', { highWaterMark: 128 * 1024 });
// blender_process.stdio[3].pipe(fStream);

//blender_process.stdin.setEncoding('utf-8');
//remote.process.stdin.pipe(blender_process.stdin);
/**
 * @type {((index:number,length:number)=>any)[]}
 */
let result_queue = [];
let message_length; // Some messages exceed the buffer size, which causes multiple data events in a row. The first 4 bytes in a data packet define how long the packet will be. This keeps track of that
let remaining_message = 0; // if other than 0, then the current message continues in the next chunk

let max_result_length = MAX_DATA;

let image_mmap = fs.openSync('/dev/shm/test','r+');
let prot = mmap.PROT_READ
let priv = mmap.MAP_SHARED
//let shared_image_buffer = mmap.map(resolution[0]*resolution[1]*4, prot, priv, image_mmap) // not worth figuring out if bottleneck is 100% py



let shared_buffer = new ArrayBuffer(max_result_length) // this is the underlying data in ram. You can define TypedArrays as views on it, and avoid allocating it again and again.
let shared_image_buffer = new ArrayBuffer(max_result_length) 
let shared_buffer_view = new Uint8ClampedArray(shared_buffer) // max data 
let shared_image_buffer_view = new Uint8ClampedArray(shared_image_buffer)


let _buffer_i = 0; // byte offset in shared_buffer. try to keep messages in memory for as long as possible by cycling through the buffer.
function process_message(chunk)
{
	
	let chunk_length = chunk.length;

	
	let chunk_offset = 0;
	// Iterate through all messages in the chunk. Piping strings together several messages into one chunk if they get created soon after one another
	while(chunk_offset < chunk_length)
	{
		// new message at chunk_offset, read header
		if(remaining_message == 0)
		{
			message_length = byteArrayToLong(chunk.subarray(chunk_offset,chunk_offset+4)) + 4;
			remaining_message = message_length;
		}

		let message_length_in_chunk = Math.min(chunk_length - chunk_offset,remaining_message);
		remaining_message -= message_length_in_chunk;
		chunk_offset += message_length_in_chunk;


		// Full message has been read, trigger callback
		if(remaining_message <= 0)
		{
			if (result_queue[0])
			{
				result_queue.shift()(new Uint8ClampedArray(shared_buffer,_buffer_i + chunk_offset-message_length,message_length))
			}
		}
	}

}

blender_process.stdio[3].on('data', function (chunk) 
{
	// Consider starting from the beginning of the buffer to make sure enough space is available for potential long multi-chunk messages.
	if(remaining_message == 0 && _buffer_i > shared_buffer_view.length/2)
	{
		_buffer_i = 0; // start from the start again, just in case a large message comes in ( guarantees messages up to bufferlength/2 )
	}
	// Worst case : chunk doesn't fit, because message was longer than have the buffer. Double buffer size and copy old buffer into new one.
	if(_buffer_i + chunk.length >= shared_buffer_view.length)
	{
		max_result_length *= 2;
		let new_buffer = new ArrayBuffer(max_result_length);
		let new_buffer_view = new Uint8ClampedArray(new_buffer)
		new_buffer_view.set(shared_buffer_view);
		shared_buffer = new_buffer;
		shared_buffer_view = new_buffer_view;
	}
	

	shared_buffer_view.set(chunk,_buffer_i);
	process_message(chunk)
	_buffer_i += chunk.length;
});







// blender_process.stdin.write(`import bpy;
// bpy.context.scene.render.filepath = '/dev/shm/render';
// bpy.ops.render.render(animation=True);
// print('done');
// `)



function on_exit(callback)
{
	blender_process.kill("SIGKILL");
}

remote.app.on("will-quit", on_exit);


blender_process.stdout.on('data', (data) => 
{
	console.log(`stdout: ${data}`);
});

blender_process.stderr.on('data', (data) =>
{
	console.error(`stderr: ${data}`);
});




runClient();

const wait_for_image_load = img => new Promise((res, rej) =>
{
	img.onload = () => res();
	img.onerror = rej;

});

const wait_for_animation_frame = () =>
{
	return new Promise((resolve) => window.requestAnimationFrame(resolve));
}


let canvas = document.getElementById('canvas');
const ctx = canvas.getContext('2d');
ctx.imageSmoothingEnabled = false
canvas.width = resolution[0];
canvas.height = resolution[1];
let [width, height] = resolution
/**
 * @type {ImageData}
 */
let image;

/**
 * Render the image from the ImageData "image" variable to the canvas
 */
async function set_image()
{
	ctx.putImageData(image, 0, 0);
}

/**
 * Turn an array of bytes into an integer using little endianess. 
 * @param {byte[]|Uint8Array} byteArray 
 * @returns 
 */
function byteArrayToLong(byteArray) 
{
	var value = 0;
	for (var i = byteArray.length - 1; i >= 0; i--) 
	{
		value = (value * 256) + byteArray[i];
	}

	return value;
};


/**
 * Call a function in blender's api.py script remotely. Returns a promise with a view to the data. This view is volatile and should be copied asap.
 * @param {string} method_name Name of the method in python
 * @param {Array} params array of parameters
 * @returns {Uint8ClampedArray} result in bytes (result exists on shared_buffer object) . Volatile.
 */
async function remote_command(method_name, params)
{
	let promise = new Promise((resolve) =>
	{
		result_queue.push(resolve)
	})

	blender_process.stdin.write(JSON.stringify({ 'method': method_name, params, time:Date.now() }) + '\r\n', (e) => { if (e) console.error(e);  });

	let array = await promise;
	return array;
}

/**
 * Tell Blender to render the next frame.
 */
async function next_frame()
{
	await remote_command('next_frame', [])
}

/**
 * Consider updating the resolution. This reallocates the buffers
 * @param new_width 
 * @param new_height 
 * @param force 
 * @returns 
 */
async function update_resolution(new_width,new_height,force=false)
{
	if(!force && new_width == resolution[0] && new_height == resolution[1])
		return;

	resolution[0] = new_width;
	resolution[1] = new_height;

	canvas.width = resolution[0];
	canvas.height = resolution[1];

	// If buffer too small, create new one.
	if(shared_image_buffer.byteLength < resolution[0] * resolution[1] * 4)
		shared_image_buffer = new ArrayBuffer(resolution[0] * resolution[1] * 4)
	
	// Resize view
	shared_image_buffer_view = new Uint8ClampedArray(shared_image_buffer,0,resolution[0] * resolution[1] * 4);
	image = new ImageData(shared_image_buffer_view, new_width, new_height);
}

let fetch_image_request = undefined;

/**
 * Request and wait for the next Blender Frame, then read the image and display it.
 */
async function updateImage()
{
	let start = performance.now();
	
	// Render the first image directly because none has been queued up asynchronously before
	if(fetch_image_request == undefined)
		fetch_image_request = remote_command('fetch_image', []);
	
	let result = await fetch_image_request // Fetch Image requests return the resolution of the image (Eg incase of monitor resolution change, which may cause a smaller result than expected)
	let res = new Uint16Array(shared_buffer,result.byteOffset + 4,2); // 2 shorts after the header are width/height
	await update_resolution(res[0],res[1]); // If nothing changed, this will continue right away
	
	// run the next commands immediately, but don't wait for them to finish. Python runs on a different thread, and we want to maximize parallel work.
	remote_command('next_frame', [])
	fetch_image_request = remote_command('fetch_image', []);
	
	// fetching the image should always be faster than calculating the next frame and writing it to the file in python. So we're not even gonna check for race conditions. If they ever become an issue, we can work with 2 files instead, and swap between them
	fs.read(image_mmap,shared_image_buffer_view,0,shared_image_buffer_view.length,0
	,(err,bytesRead,buff)=>
	{
		set_image()
	})

	console.log(performance.now() - start , 'is what js takes')
}

let fps = 0;

async function update_loop()
{
	next_frame();

	while (true)
	{
		var t1 = performance.now()

		await updateImage();

		var delta = (performance.now() - t1) / 1000;
		fps = fps * 0.98 + delta * 0.02;
		$(`#fps`).html(1/fps);
	}
}


async function runClient(address)
{
	await wait(1000)

	let result = await remote_command('set_resolution', resolution);
	let res = new Uint16Array(shared_buffer,result.byteOffset + 4,2);
	console.log(result,res)
	await update_resolution(res[0],res[1],true);

	update_loop();
}

$('#resize').click(() =>
{
	remote_command('set_resolution', [window.innerWidth,window.innerHeight]);
	
})

$(`#color`).on("input",function (v){
	
	console.log(hex_to_color(this.value));
	remote_command('set_light_color', hex_to_color(this.value).map(v=>v/255));
})



/**
 * src : https://stackoverflow.com/questions/50620821/uint8array-to-image-in-javascript
 */
//  const header_size = 70;
// function set_image_header()
// {


// 	let [width, height] = resolution
// 	image_size = resolution[0] * resolution[1] * 4;

// 	// File Header

// 	// BM magic number.
// 	view.setUint16(0, 0x424D, false);
// 	// File size.
// 	view.setUint32(2, arr.length, true);
// 	// Offset to image data.
// 	view.setUint32(10, header_size, true);

// 	// BITMAPINFOHEADER

// 	// Size of BITMAPINFOHEADER
// 	view.setUint32(14, 40, true);
// 	// Width
// 	view.setInt32(18, width, true);
// 	// Height (signed because negative values flip
// 	// the image vertically).
// 	view.setInt32(22, height, true);
// 	// Number of colour planes (colours stored as
// 	// separate images; must be 1).
// 	view.setUint16(26, 1, true);
// 	// Bits per pixel.
// 	view.setUint16(28, 32, true);
// 	// Compression method, 6 = BI_ALPHABITFIELDS
// 	view.setUint32(30, 6, true);
// 	// Image size in bytes.
// 	view.setUint32(34, image_size, true);
// 	// Horizontal resolution, pixels per metre.
// 	// This will be unused in this situation.
// 	view.setInt32(38, 10000, true);
// 	// Vertical resolution, pixels per metre.
// 	view.setInt32(42, 10000, true);
// 	// Number of colours. 0 = all
// 	view.setUint32(46, 0, true);
// 	// Number of important colours. 0 = all
// 	view.setUint32(50, 0, true);

// 	// Colour table. Because we used BI_ALPHABITFIELDS
// 	// this specifies the R, G, B and A bitmasks.

// 	// Red
// 	view.setUint32(54, 0x000000FF, true);
// 	// Green
// 	view.setUint32(58, 0x0000FF00, true);
// 	// Blue
// 	view.setUint32(62, 0x00FF0000, true);
// 	// Alpha
// 	view.setUint32(66, 0xFF000000, true);
// }
