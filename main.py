# main.py
# Entry point for the game loop. 
# The Blender World Object should have a custom property called __main__ that refers to this script. This causes bge to defer the render and logic loops to this script.
import bpy
import os
import bge
import sys
import time

# Make sure api script is found by appending this script's folder to sys.path
dir = os.path.dirname(bpy.data.filepath)
if not dir in sys.path:
	sys.path.append(dir )

import api as api

# this re-loads the "api.py" Text every time
# (needs the "normal" import statement above to define the module name)
import importlib
importlib.reload(api)

server = api.Server()
print(sys.argv, flush=True)

# This loop checks for commands from the electron thread.
def LOOP():
	global server
	time.sleep(0.0001) # Check up to 10 times per ms
	server.update()

while(True):
	LOOP()


	