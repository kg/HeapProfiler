PRAGMA data.user_version = 1;

CREATE TABLE data.Meta (
    Name TEXT PRIMARY KEY NOT NULL,
    Value VARIANT
);

CREATE TABLE data.Snapshots (
    Snapshots_ID INTEGER PRIMARY KEY NOT NULL,
    Timestamp UTCTICKS NOT NULL
);

CREATE TABLE data.MemoryStats (
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

CREATE TABLE data.Heaps (
    Heaps_ID INTEGER PRIMARY KEY NOT NULL,
    BaseAddress INTEGER NOT NULL
);

CREATE TABLE data.SnapshotHeaps (
    Snapshots_ID INTEGER NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Heaps_ID)
);

CREATE TABLE data.HeapStats (
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

CREATE TABLE data.Modules (
    Modules_ID INTEGER PRIMARY KEY NOT NULL,
    Filename TEXT NOT NULL,
    BaseAddress INTEGER NOT NULL,
    Size INTEGER NOT NULL
);

CREATE TABLE data.SnapshotModules (
    Snapshots_ID INTEGER NOT NULL,
    Modules_ID INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Modules_ID)
);

CREATE TABLE data.Tracebacks (
    Tracebacks_ID INTEGER PRIMARY KEY NOT NULL
);

CREATE TABLE data.TracebackFrames (
    Tracebacks_ID INTEGER NOT NULL,
    Address INTEGER NOT NULL
);

CREATE TABLE data.Allocations (
    Allocations_ID INTEGER PRIMARY KEY NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    Snapshots_ID INTEGER NOT NULL,
    Tracebacks_ID INTEGER NOT NULL,
    Address INTEGER NOT NULL,
    Size INTEGER NOT NULL,
    Overhead INTEGER NOT NULL
);