# main.py
# Entry point for the game loop. 
# The Blender World Object should have a custom property called __main__ that refers to this script. This causes bge to defer the render and logic loops to this script.
import bpy
import os
import bge
import sys
import time
import json

# Make sure api script is found by appending this script's folder to sys.path
dir = os.path.dirname(bpy.data.filepath)
if not dir in sys.path:
	sys.path.append(dir )

import api as api

# this re-loads the "api.py" Text every time this script gets loaded. Useful for code changes while running the script from within the blender editor.
# (needs the "normal" import statement above to define the module name)
import importlib
importlib.reload(api)

server = api.Server()
print(sys.argv, flush=True)

# Call each time message queue should be checked (as often as possible)
def update():
	global server
	lines = input().split("\r\n",)
	print("Received Input",flush=True)
	print("Received " + str(lines),flush=True)
	for line in lines:
		if line == "":
			break
		message = json.loads(line)
		latency_in = round(time.time()*1000) - message['time']
		#print('Latency in is ' + str(latency_in))
		try:
			getattr(server, message['method'])(message['params'])
		except Exception as e:
			print(e,flush=True)
		#print("Processin " + str(message['method']) + " " + str((time.time_ns() - start)/1000000) + "\n",flush=True)

# This loop checks for commands from the electron thread.
def LOOP():	
	time.sleep(0.0001) # Check up to 10 times per ms
	update()

while(True):
	LOOP()


	