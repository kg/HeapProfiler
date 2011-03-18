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
    /* This is the native base address of the heap (in a UMDH dump, this is the heap number) */
    BaseAddress INTEGER UNIQUE NOT NULL
);

CREATE TABLE data.SnapshotHeaps (
    Snapshots_ID INTEGER NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Heaps_ID)
);

CREATE TABLE data.HeapStats (
    Snapshots_ID INTEGER NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    /* The absolute address at which the first known allocation in the heap resided during this snapshot. */
    EstimatedStart INTEGER NOT NULL,
    /* The estimated size of the occupied region of the heap, ignoring free space at the beginning and end. */
    EstimatedSize INTEGER NOT NULL,
    EstimatedFree INTEGER NOT NULL,
    TotalOverhead INTEGER NOT NULL,
    /* The total number of bytes requested via heap allocation, ignoring overhead/fragmentation. */
    TotalRequested INTEGER NOT NULL,
    LargestFreeSpan INTEGER NOT NULL,
    LargestOccupiedSpan INTEGER NOT NULL,
    OccupiedSpans INTEGER NOT NULL,
    EmptySpans INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Heaps_ID)
);

CREATE TABLE data.Modules (
    Modules_ID INTEGER PRIMARY KEY NOT NULL,
    Filename TEXT UNIQUE NOT NULL,
    BaseAddress INTEGER NOT NULL,
    Size INTEGER NOT NULL
);

CREATE TABLE data.SnapshotModules (
    Snapshots_ID INTEGER NOT NULL,
    Modules_ID INTEGER NOT NULL,
    PRIMARY KEY (Snapshots_ID, Modules_ID)
);

CREATE TABLE data.Tracebacks (
    Tracebacks_ID INTEGER PRIMARY KEY NOT NULL,
    /* The unique ID assigned to the traceback by UMDH when the traceback was captured. */
    RuntimeID INTEGER UNIQUE NOT NULL
);

CREATE TABLE data.TracebackFrames (
    Tracebacks_ID INTEGER NOT NULL,
    FrameIndex INTEGER NOT NULL,
    /* The absolute address of this stack frame at the time the traceback was captured. */
    Address INTEGER NOT NULL,
    PRIMARY KEY (Tracebacks_ID, FrameIndex)
);

/* This index exists so that you can map back from a single frame to all of the tracebacks that contain it. */
CREATE INDEX data.idx_TracebackFramesByAddress ON TracebackFrames (Address);

CREATE TABLE data.Allocations (
    Allocations_ID INTEGER PRIMARY KEY NOT NULL,
    Heaps_ID INTEGER NOT NULL,
    Tracebacks_ID INTEGER NOT NULL,
    /* The ID of the first snapshot to contain this allocation. */
    First_Snapshots_ID INTEGER NOT NULL,
    /* The ID of the last snapshot to contain this allocation. */
    Last_Snapshots_ID INTEGER NOT NULL,
    /* The address of the allocation, relative to the base address of the heap containing it. */
    RelativeAddress INTEGER NOT NULL,
    Size INTEGER NOT NULL,
    Overhead INTEGER NOT NULL
);

/* This index makes the initial process of constructing the allocation ranges much cheaper. */
CREATE INDEX data.idx_AllocationByAddress ON Allocations (RelativeAddress);
/* This reduces the cost of getting all of the live allocations within a given time window. */
CREATE INDEX data.idx_AllocationBySnapshotRange ON Allocations (First_Snapshots_ID, Last_Snapshots_ID);
/* This reduces the cost of getting all of the allocations associated with a traceback. */
CREATE INDEX data.idx_AllocationByTraceback ON Allocations (Tracebacks_ID);