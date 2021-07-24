import os
import json
import bge
import fs
#import json
import bpy
import time
#from queue import Queue, Empty
from threading  import Thread
import mmap
import tempfile
import platform

# if(platform.system() == 'Windows'):
# 	import msvcrt
# 	msvcrt.setmode(3, os.O_BINARY)

# else:
# 	import fcntl
# 	fl = fcntl.fcntl(3, fcntl.F_GETFL)
# 	fl |= os.O_SYNC
# 	fcntl.fcntl(3, fcntl.F_SETFL, fl)

tempdir = tempfile.gettempdir() + '\\' if platform.system() == 'Windows' else '/dev/shm/' 



class Server(object):
	def __init__(self):
		#self.changed = False
		self.process = None
		#self.port = 9999
		self.viewport_texture = None

		self.fd3 = os.fdopen(3,'w')

		self.image_file = open(tempdir + 'test', "rb+")
		self.image_file_size = os.fstat(self.image_file.fileno()).st_size
		print(str(self.image_file_size),flush=True)
		#self.image_file.write(bytearray(1920*1080*4))
		#self.image_file.flush()
		self.image_mmap = mmap.mmap(self.image_file.fileno(),1920*1080*4,prot=mmap.PROT_WRITE)


		#socket = "tcp://127.0.0.1:"
		#self.context = zmq.Context()
		#self.socket = self.context.socket(zmq.REP)


		#self.

		# while self.port > 9000:
		# 	try:
		# 		self.socket.bind(socket + str(self.port))
		# 	except zmq.error.ZMQError:
		# 		print('fail',flush=True)
		# 		self.port -= 1
		# 	else:
		# 		break;
				
		#print("|init|"+socket + str(self.port),flush=True)

	# API : Render the next frame
	def next_frame(self,params):
		start = time.time_ns()
		bge.logic.NextFrame()
		#self.socket.send(b"OK")
		os.write(3, (0).to_bytes(4, byteorder='little'))
		#os.fsync(3)
		print('Rendered next frame ' + str((time.time_ns() - start)/1000000),flush=True)

	# API : Quit the game
	def quit(self,params):
		try:
			bge.logic.endGame()
		except Exception as e:
			print(e,flush=True)
		
	# API : Set render resolution
	def set_resolution(self,params):
		

		print('set resolution to ' + str(params[0]) + ' ' + str(params[1]),flush=True)
		bge.render.setWindowSize(params[0], params[1])
		bpy.context.scene.game_settings.resolution_x = params[0]
		bpy.context.scene.game_settings.resolution_y = params[1]

		#self.image_mmap.resize(params[0] * params[1] * 4) # TODO : Does this resize the file as well?
		if(params[0] * params[1] * 4 > self.image_file_size):
			self.image_file.write(bytearray(params[0] * params[1] * 4))
			self.image_file.flush()
		self.image_mmap = mmap.mmap(self.image_file.fileno(),params[0] * params[1] * 4,flags=mmap.MAP_SHARED,prot=mmap.PROT_WRITE)

		print('Loading image',flush=True)

		self.viewport_texture = bge.texture.ImageViewport(flip=False, alpha=True, scale=False, whole=True, depth=False, zbuff=False)
		#viewport_texture.refresh()
		#self.socket.send(b"OK")
		viewport_texture = self.viewport_texture


		#global image_mmap
		#image_mmap = mmap.mmap(image_file, viewport_texture.size[0] * viewport_texture.size[1] * 4, tagname=None, prot=mmap.PROT_WRITE)
		
		os.write(3, (4).to_bytes(4, byteorder='little'))
		os.write(3, viewport_texture.size[0].to_bytes(2, byteorder='little'))
		os.write(3, viewport_texture.size[1].to_bytes(2, byteorder='little'))
		#os.fsync(3)
		
		print("change",flush=True)

	# API : Write the render to an mmap and return the width/height directly. 
	def fetch_image(self,params):
		start = time.time_ns()
		self.update_texture()

		viewport_texture = self.viewport_texture
		
		try:
			os.write(3, (4).to_bytes(4, byteorder='little'))
			os.write(3, viewport_texture.size[0].to_bytes(2, byteorder='little'))
			os.write(3, viewport_texture.size[1].to_bytes(2, byteorder='little'))
			#os.fsync(3)
		except Exception as e:
			print(e,flush=True)
		print('Updated texture2 ' + str((time.time_ns() - start)/1000000),flush=True)
	
	# Fetch the final render from gpu and store as bytearray under self.image_mmap
	def update_texture(self):
		if self.viewport_texture == None:
			return

		try:
			self.viewport_texture.refresh(self.image_mmap,'RGBA') # Defined in /home/sven/Desktop/hobby_projects/upbge_cultivation/upbge-master/upbge/source/gameengine/VideoTexture/ImageBase.cpp unde Image_Refresh
		except Exception as e:
			print(e,flush=True)
		
	# API : A demo function for changing the emission color in the demo main.blend
	def set_light_color(self,params):
		bpy.data.materials["GlowingCube"].node_tree.nodes["Emission"].inputs[0].default_value = (params[0],params[1],params[2], 1)
		os.write(3, (0).to_bytes(4, byteorder='little'))
		# scene = bge.logic.getCurrentScene()
		# glowing_cube = scene.objects["GlowingCube"]
		# material = glowing_cube.material_slots[0]
		# material.diffuse_color

	# Call each time message queue should be checked (as often as possible)
	def update(self):
		#start = time.time_ns()
		lines = input().split("\r\n",)#sys.stdin.buffer.s.read(1).split("\r\n")
		for line in lines:
			if line == "":
				break
			start = time.time_ns()
			message = json.loads(line)
			latency_in = round(time.time()*1000) - message['time']
			print('Latency in is ' + str(latency_in))
			try:
				getattr(self, message['method'])(message['params'])
			except Exception as e:
				print(e,flush=True)
			#print("Processin " + str(message['method']) + " " + str((time.time_ns() - start)/1000000) + "\n",flush=True)


	
if __name__ == '__main__':
	print("inited")




