# Any function that should be available to electron should be defined in here (in the Server class).
# Any such function must take parameters in the form of an array : example(self,params)
# Any such function must write a result to the ipc pipe : os.write(3, (0).to_bytes(4, byteorder='little'))
# The first 4 bytes represent the length of the result message. An empty message is therefore just a 4-byte 0.
import os
import bge
import bpy
import time
import mmap
import tempfile
import platform

tempdir = tempfile.gettempdir() + '\\' if platform.system() == 'Windows' else '/dev/shm/' 



class Server(object):
	def __init__(self):
		self.viewport_texture = None
		self.fd3 = os.fdopen(3,'w')
		self.image_file = open(tempdir + 'test', "rb+")
		self.image_file_size = os.fstat(self.image_file.fileno()).st_size
		print(str(self.image_file_size),flush=True)
		self.image_mmap = mmap.mmap(self.image_file.fileno(),1920*1080*4)

	# API : Render the next frame
	def next_frame(self,params):
		start = time.time_ns()
		bge.logic.NextFrame()
		os.write(3, (0).to_bytes(4, byteorder='little'))
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

		# image grew, which requires writing the larger file to disk again, before loading it back into memory
		if(params[0] * params[1] * 4 > self.image_file_size):
			self.image_file.write(bytearray(params[0] * params[1] * 4))
			self.image_file.flush()
		self.image_mmap = mmap.mmap(self.image_file.fileno(),params[0] * params[1] * 4)

		print('Loading image',flush=True)

		self.viewport_texture = bge.texture.ImageViewport(flip=False, alpha=True, scale=False, whole=True, depth=False, zbuff=False)

		# Write actual resolution as result. This may differ from intended resolution, if the window manager messes stuff up.
		os.write(3, (4).to_bytes(4, byteorder='little'))
		os.write(3, self.viewport_texture.size[0].to_bytes(2, byteorder='little'))
		os.write(3, self.viewport_texture.size[1].to_bytes(2, byteorder='little'))

		print("change",flush=True)

	# API : Write the render to an mmap and return the width/height directly. 
	def fetch_image(self,params):
		start = time.time_ns()
		self.update_texture()
		
		os.write(3, (4).to_bytes(4, byteorder='little'))
		os.write(3, self.viewport_texture.size[0].to_bytes(2, byteorder='little'))
		os.write(3, self.viewport_texture.size[1].to_bytes(2, byteorder='little'))

		print('Updated texture2 ' + str((time.time_ns() - start)/1000000),flush=True)
	
	# Fetch the final render from gpu and store as bytearray under self.image_mmap
	def update_texture(self):
		if self.viewport_texture == None:
			return
		self.viewport_texture.refresh(self.image_mmap,'RGBA') # Defined in /home/sven/Desktop/hobby_projects/upbge_cultivation/upbge-master/upbge/source/gameengine/VideoTexture/ImageBase.cpp unde Image_Refresh
		
	# API : A demo function for changing the emission color in the demo main.blend
	def set_light_color(self,params):
		bpy.data.materials["GlowingCube"].node_tree.nodes["Emission"].inputs[0].default_value = (params[0],params[1],params[2], 1)
		os.write(3, (0).to_bytes(4, byteorder='little'))
		# scene = bge.logic.getCurrentScene()
		# glowing_cube = scene.objects["GlowingCube"]
		# material = glowing_cube.material_slots[0]
		# material.diffuse_color



	
if __name__ == '__main__':
	print("inited")




