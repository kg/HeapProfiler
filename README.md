Heap Profiler
=============

This is a simple graphical frontend for the UMDH and GFlags tools included in Microsoft Debugging Tools for Windows, written in C#.
It automates the process of capturing heap allocation snapshots for a running application, and provides viewing and filtering tools for analyzing those snapshots.

Usage
-----

You'll need Windows SDK 7.1, along with the 32-bit version of Microsoft Debugging Tools for Windows. If you have the former, you will automatically be prompted to install the latter.
Run it, select an executable to profile, provide any arguments if needed, and hit Launch. Click 'Capture Now' to capture a snapshot at any time, or turn on 'Auto Capture' to capture a snapshot every 5 seconds.
Once you have some captures, select two of them from the list and click the 'Diff Snapshots' button to generate a diff of the two snapshots and open it in the viewer. You can save that diff via the File menu, in order to view it later.

Building
--------

You'll need Visual C# 2008 or 2010 to compile Heap Profiler (it requires C# 3.5).
You'll also need two library dependencies, Squared.Task and Squared.Util. You can get them from the [Fracture project on Google Code]. If you put the 'Fracture' folder next to your 'HeapProfiler' folder the included .sln file should load them right up for you.

  [Fracture project on Google Code]: http://code.google.com/p/fracture/source/checkout