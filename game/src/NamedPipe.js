/**
 * @typedef {"r+"|"w+"} NamedPipeType
 */

const fs              = require('fs');
const { spawn, fork } = require('child_process');
const net = require('net');
const os = require('os');
const platform = os.platform();
const is_win = platform == 'win32' | platform == 'win64';

export class NamedPipe
{
	/**
	 * @type {string}
	 */
	path;
	/**
	 * @type {NamedPipeType}
	 */
	type;

	/**
	 * @type {import('child_process').ChildProcessWithoutNullStreams}
	 */
	fifo;

	/**
	 * 
	 * @param {string} path 
	 * @param {NamedPipeType} type 
	 * @param {any} opts 
	 */
	constructor(path,type,opts={on_data})
	{
		this.path = path; 
		this.type = type;

		if(is_win)
			path = "\\\\.\\pipe\\" + path; // windows requires a particular "directory" 

		console.log("Mkfifo")

		const client = net.createConnection(path, () => {
			console.log('connected to server!');
		});

		client.on('data', opts.on_data || this.on_data);
		
		client.on('end', () => {
			console.log('disconnected from server');
		});

		// this.fifo   = spawn('mkfifo', [path]);  // Create Pipe B

		// this.fifo.on('exit', (status) => {
		// 	console.log('Created Pipe B');

		// 	fs.open(path,"r",0x666,(err,fd)=>{
		// 		console.error(err);
		// 		console.log("Opened file")
		// 		let fifoRs = fs.createReadStream(null, { fd });
	
		// 		console.log('Ready to write')
	
		// 		fifoRs.on('data', opts.on_data || this.on_data);
		// 	})
			
		// });
	}

	on_data = data => 
	{
		console.log('----- Received packet -----');
		console.log('    Data   : ' + data.toString());
	}
}
