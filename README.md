Heap Profiler
=============

This is a graphical frontend for the UMDH and GFlags tools included in Microsoft Debugging Tools for Windows, written in C#.
It automates the process of capturing heap allocation snapshots for a running application, and provides viewing and filtering tools for analyzing those snapshots.

Usage
-----

You'll need Windows SDK 7.1, along with the 32-bit version of Microsoft Debugging Tools for Windows. If you have the former, you will automatically be prompted to install the latter.
Run it, select an executable to profile, provide any arguments if needed, and hit Launch. Click 'Capture Now' to capture a snapshot at any time, or turn on 'Auto Capture' to capture a snapshot every 5 seconds. Please note that capturing heap information requires running as administrator (saved captures can be viewed without administrator, though)
Once you have some captures, select two of them from the list and click the 'Diff Snapshots' button to generate a diff of the two snapshots and open it in the viewer. You can also select a single point of time and click 'View Selection' to view the heap at that time.
You can save a collection of captures to a folder via the file menu for access later. The saved captures include symbol information so you can successfully examine them on other machines.
The filtering controls in the various viewer interfaces can be used to filter allocations by function name and/or module name. Just type to filter (the filter control is colored red if the filter is invalid, and yellow if it is valid).

Building
--------

You'll need Visual C# 2010 to compile Heap Profiler (it requires C# 4). You can use either the Express edition of Visual C# or the retail version. The library dependencies are included as git submodules in the ext folder.