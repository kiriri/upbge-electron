# Any function that should be available to electron should be defined in here (in the Server class).
# Any such function must take parameters in the form of an array : example(self,params)
# Any such function must write a result to the ipc pipe : self.send((0).to_bytes(4, byteorder='little'))
# The first 4 bytes represent the length of the result message. An empty message is therefore just a 4-byte 0.
import os
import bge
import bpy
import time
import mmap
import tempfile
import platform

is_win = platform.system() == 'Windows'

if is_win:
	import win32file

tempdir = tempfile.gettempdir() + '\\' if is_win else '/dev/shm/' 


# Path of output named pipe (/fifo)
path = "test.sock"


class Server(object):
	def __init__(self):

		print(os.fstat(3),flush=True)

		# Create a FIFO named path
		# with the specified mode
		# using os.mkfifo() method
		# if not os.path.exists(path):
		# 	os.mkfifo(path, 0o600)
		# try:
		# 	self.fifo_fd = os.open(path,os.O_WRONLY)
		# except Exception as e:
		# 	print(e,flush=True)

		self.viewport_texture = None
		# self.fd3 = os.fdopen(3,'w')
		self.image_file = open(tempdir + 'test', "rb+")
		self.image_file_size = os.fstat(self.image_file.fileno()).st_size
		print(str(self.image_file_size),flush=True)
		self.image_mmap = mmap.mmap(self.image_file.fileno(),1920*1080*4)

		self.fd = -1
		self.environment = "?" # "js" | "cs" :: Used to setup the output communication correctly. 

	def init_named(self):
		try:
			print("Initing named ",flush=True)
			global path
			if is_win:
				print("WIN ",flush=True)

				path = "\\\\.\\pipe\\" + path
				# Keep file in memory, else it gets closed automatically
				self.file = win32file.CreateFile(
					path, 
				win32file.GENERIC_READ | win32file.GENERIC_WRITE, 
				0, 
				None, 
				win32file.OPEN_EXISTING, 
				0, 
				None)
				self.fd = self.file.__int__
			else:
				print("LIN ",flush=True)
				path = "/dev/shm/"+path
				if not os.path.exists(path):
					print('calling mkfifo on ' + path, flush=True)
					os.mkfifo(path, 0o600)
					print('called mkfifo on ' + path, flush=True)
				try:
					self.fd = os.open(path,os.O_WRONLY)
					print("Opened ",flush=True)
				except Exception as e:
					print(e,flush=True)
		except Exception as e:
			print(e,flush=True)

	# Send a message back to the parent process. 
	# Usually, this should happen via the fd3 pipe. But under .NET, this seems to be impossible which is why we have to use named pipes in Unity instead.
	def send(self,message):
		global path
		if self.fd < 0:
			if self.environment == "cs": # C# Cant use fd3 even if it is opened automatically by the os.
				self.init_named()
			else:
				try:
					os.write(3,message)
					self.fd = 3
					return
				except:
					self.init_named()
		
		os.write(self.fd,message)
				

	def set_environment(self,params):
		self.environment = params[0]
		self.send((0).to_bytes(4, byteorder='little'))

	# API : Render the next frame
	def next_frame(self,params):
		start = time.time_ns()
		bge.logic.NextFrame()
		self.send((0).to_bytes(4, byteorder='little'))
		#print('Rendered next frame ' + str((time.time_ns() - start)/1000000),flush=True)

	# API : Quit the game
	def quit(self,params):
		try:
			bge.logic.endGame()
		except Exception as e:
			print(e,flush=True)
		
	# API : Set render resolution
	def set_resolution(self,params):
		print('set resolution to ' + str(params[0]) + ' ' + str(params[1]),flush=True)
		params[0] = int(params[0])
		params[1] = int(params[1])
		bge.render.setWindowSize(params[0], params[1])
		bpy.context.scene.game_settings.resolution_x = params[0]
		bpy.context.scene.game_settings.resolution_y = params[1]

		# image grew, which requires writing the larger file to disk again, before loading it back into memory
		if(params[0] * params[1] * 4 > self.image_file_size):
			self.image_file.write(bytearray(params[0] * params[1] * 4))
			self.image_file.flush()
		self.image_mmap = mmap.mmap(self.image_file.fileno(),params[0] * params[1] * 4)

		print('Loading image',flush=True)

		self.viewport_texture = bge.texture.ImageViewport(flip=False, alpha=True, scale=False, whole=True, depth=False, zbuff=False)

		# Write actual resolution as result. This may differ from intended resolution, if the window manager messes stuff up.
		self.send((4).to_bytes(4, byteorder='little'))
		self.send(self.viewport_texture.size[0].to_bytes(2, byteorder='little'))
		self.send(self.viewport_texture.size[1].to_bytes(2, byteorder='little'))

		print("change",flush=True)

	# API : Write the render to an mmap and return the width/height directly. 
	def fetch_image(self,params):
		start = time.time_ns()
		self.update_texture()
		
		self.send((4).to_bytes(4, byteorder='little'))
		self.send(self.viewport_texture.size[0].to_bytes(2, byteorder='little'))
		self.send(self.viewport_texture.size[1].to_bytes(2, byteorder='little'))

		#print('Updated texture2 ' + str((time.time_ns() - start)/1000000),flush=True)
	
	# Fetch the final render from gpu and store as bytearray under self.image_mmap
	def update_texture(self):
		if self.viewport_texture == None:
			return
		self.viewport_texture.refresh(self.image_mmap,'RGBA') # Defined in /home/sven/Desktop/hobby_projects/upbge_cultivation/upbge-master/upbge/source/gameengine/VideoTexture/ImageBase.cpp unde Image_Refresh
		
	# API : A demo function for changing the emission color in the demo main.blend
	def set_light_color(self,params):
		bpy.data.materials["GlowingCube"].node_tree.nodes["Emission"].inputs[0].default_value = (params[0],params[1],params[2], 1)
		self.send((0).to_bytes(4, byteorder='little'))
		# scene = bge.logic.getCurrentScene()
		# glowing_cube = scene.objects["GlowingCube"]
		# material = glowing_cube.material_slots[0]
		# material.diffuse_color
	
if __name__ == '__main__':
	print("inited")




