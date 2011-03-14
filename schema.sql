PRAGMA user_version = 1;

CREATE TABLE Snapshots (
    Snapshots_ID INTEGER PRIMARY KEY NOT NULL,
    When DATETIME NOT NULL,
    Filename TEXT NOT NULL
);

CREATE TABLE MemoryStats (
    Snapshots_ID INTEGER PRIMARY KEY NOT NULL,
    NonpagedSystem INTEGER NOT NULL,
    PagedSystem INTEGER NOT NULL,
    Paged INTEGER NOT NULL,
    Private INTEGER NOT NULL,
    Virtual INTEGER NOT NULL,
    WorkingSet INTEGER NOT NULL,
    PeakPaged INTEGER NOT NULL,
    PeakVirtual INTEGER NOT NULL,
    PeakWorking INTEGER NOT NULL
);

CREATE TABLE Heaps (
    Heaps_ID INTEGER PRIMARY KEY NOT NULL
	Offset INTEGER NOT NULL
);

CREATE TABLE SnapshotHeaps (
    Snapshots_ID INTEGER NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Heaps_ID)
);

CREATE TABLE HeapStats (
	Heaps_ID INTEGER NOT NULL,
	Snapshots_ID INTEGER NOT NULL,
	EstimatedSize INTEGER NOT NULL,
	EstimatedFree INTEGER NOT NULL,
	TotalOverhead INTEGER NOT NULL,
	TotalRequested INTEGER NOT NULL,
	LargestFreeSpan INTEGER NOT NULL,
	LargestOccupiedSpan INTEGER NOT NULL,
	OccupiedSpans INTEGER NOT NULL,
	EmptySpans INTEGER NOT NULL
);

CREATE TABLE Modules (
    Modules_ID INTEGER PRIMARY KEY NOT NULL,
    Filename TEXT NOT NULL,
    Offset INTEGER NOT NULL,
    Size INTEGER NOT NULL
);

CREATE TABLE SnapshotModules (
    Snapshots_ID INTEGER NOT NULL,
    Modules_ID INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Modules_ID)
);

CREATE TABLE Tracebacks (
    Tracebacks_ID INTEGER PRIMARY KEY NOT NULL
);

CREATE TABLE TracebackFrames (
    Tracebacks_ID INTEGER NOT NULL,
    Offset INTEGER NOT NULL
);

CREATE TABLE Allocations (
    Allocations_ID INTEGER PRIMARY KEY NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    First_Snapshots_ID INTEGER NOT NULL,
    Last_Snapshots_ID INTEGER NOT NULL,
    Tracebacks_ID INTEGER NOT NULL,
    Offset INTEGER NOT NULL,
    Size INTEGER NOT NULL,
    Overhead INTEGER NOT NULL
);