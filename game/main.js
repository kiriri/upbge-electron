const { app, BrowserWindow } = require('electron')

global.blender_port = parseInt(process.argv[2])


app.disableHardwareAcceleration()

var createWindow = () => {
	app.allowRendererProcessReuse = false;
  const win = new BrowserWindow({
    // width: 800,
    // height: 600,
	
	//transparent:true,
	//skipTaskbar:true,
	//alwaysOnTop: true,
    //frame: false,
	//fullscreen:true,
	
	//resizable: false,
	//parent:BrowserWindow.fromId(parseInt(process.argv[3])),
    webPreferences: {
      nodeIntegration: true,
	  contextIsolation: false, // same as nodeIntegration:true
      enableRemoteModule:true,
    },
  })
  
  
  win.loadFile('index.html')
  win.webContents.openDevTools({mode:"undocked"})
  
  //win.setFullScreen(true);
//   app.dock?.hide()
//   win.setVisibleOnAllWorkspaces(true, {visibleOnFullScreen: true});
//   win.setAlwaysOnTop(true, "floating", 99999);
//   win.setFullScreenable(false);
//   win.maximize()

//   win.focus();
//   win.setAlwaysOnTop(true, "screen-saver",100);


  




}







app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})

// app.on('activate', () => {
//   if (BrowserWindow.getAllWindows().length === 0) {

//     createWindow()
//   }
// })


if(process.platform === "linux") {
	//app.commandLine.appendSwitch('enable-transparent-visuals');
	//app.commandLine.appendSwitch("disable-gpu")
	//
}
//app.disableHardwareAcceleration();

app.on('ready', () => createWindow())//setTimeout(createWindow, 1000));





