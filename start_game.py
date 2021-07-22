#main.py
import bpy
import types
import os
import zmq
import sys
import base64

# dir = os.path.dirname(bpy.data.filepath)
# if not dir in sys.path:
#     sys.path.append(dir )
#     #print(sys.path)

# # import api

# # # this re-loads the "api.py" Text every time
# # # (needs the "normal" import statement above to define the module name)
# # import importlib
# # importlib.reload(api)

bpy.ops.wm.blenderplayer_start()
print(sys.argv, flush=True)
print('init done', flush=True)

