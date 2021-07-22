# upbge-electron
Forward UPBGE renders to Electron and control Blender from Electron (js)

See setup_guide.txt for some hints on how to recompile UPBGE properly (and headerlessly), and a list of changes that were made.

Possible future optimizations : 
Python : 
- Use multiprocessing.Process for true multithreading with shared variables. Maybe something can be offloaded?