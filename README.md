# Lokad Data Platform Sample

This is Data Platform sample from Lokad - showing principles of storing and 
processing large (GBytes and TBytes) data streams in the cloud. It is based 
on concepts from: Lokad.CQRS, Lokad.Cloud, EventStore, kafka and bitcask.

Data platform is **designed to be run on Windows Azure** (as a Windows Service
on VM or it can be plugged into an Azure Worker Role). However, it can also
run on **file system for local development and testing purposes**.

Some introductory information:

* [Introducing Data Platform](http://abdullin.com/journal/2012/10/20/introducing-lokad-data-platform.html)
* [Scalability targets of Data Platform](http://abdullin.com/journal/2012/10/20/scalability-targets-of-lokad-data-platform.html)

More documentation will be available, once we push through first production deployments internally.

## Core concepts

Data platform currently is a really simple project aiming to demonstrate core
principles of saving, processing and managing large datasets composed of 
millions of messages or data transactions.

Data Platform is represented by:
 
* Storage Engine - set of classes for manipulating message sets in large blobs
  or files. Each event store can be represented as a separate message set.
* Data Platform server - high-throughput server based on Staged Event Driven
  Architecture, which coordinates writes to event stores from various clients.
* Storage Client - client API library which provides convenient access from
  .NET code to push events to event stores or enumerate them. It also provides
  access to store simple key-value documents (which can be used as views in 
  CQRS)

![High-level overview of DataPlatform](https://raw.github.com/Lokad/lokad-data-platform/master/Library/Images/platform-high.png)

### Terminology

**Data Platform Server (Node)** - single instance of data platform server, 
  responsible for coordinating multiple writers. Lokad Data Platform servers
  support multiple event stores. 

**Event Store** - set of events which are logically grouped together (e.g. 
  belong to the same subsystem or subdomain). Single event store can keep 
  multiple event streams for different aggregates). Each event store has
  it's own version (which increments, as new events are added to it).

**Event Stream** - group of events within a single event store, which are 
  identified by the same stream name or key. If you are applying Domain-
  Driven Design with Event Sourcing, then such event stream would represent 
  a single entity.

**Checkpoint** - pointer to some event (either using it's sequential number
  or as direct byte offset in a file). It can be used by event storage to
  record last committed (and flushed) event.

## Credits

Thanks to the authors and maintainers of these projects for letting us stand on the shoulders of giants:

* [EventStore](http://geteventstore.com)
* [Service Stack](http://www.servicestack.net/) (and especially authors of async branch)
* [NLog](http://nlog-project.org/)
* [NUnit](http://www.nunit.org/)

## Samples

See SamplesReadme.md in this folder for more details

## Concerns and intended directions

### Event Storage management

Currently, each event stream is stored into a single file as opposed to splitting 
data into multiple chunks. This was an intentional decision at the first stage of 
the development of DataPlatform, since chunked storage requires some more code 
to handle chunks and their proper loading by both client and server.

This approach is understood as not full production grade. The implementation of
of a proper chunked storage is planned for a later stage.


### Recommendations for writing projections

DataPlatform itself is merely a storage engine, which is capable of high 
message throughputs with concurrent reads/writes. However, the platform has
little value on its own, value is created by the SmartApps, which leverage the functionality and scalability opportunities offered by the platform.

SmartApps are developed by developers wishing to store and process messages
with the help of the platform. While the platform is tuned for high performance
(both on file system and in Azure), the SmartApp can sometimes underperform,
creating the impression that the platform itself is slow. 

Here are some recommendations:

* **Measure performance of the stream reading** in your local environment 
by simply implementing an incremental projection which scans through all 
events in the store (alternatively you can run the projection from Sample 2 
against the appropriate stream container). Then, compare it with the time 
needed to run your projection. Extra time is spent in projection implementation 
code.
* **Batch process multiple events together** to reduce IO cost of reading/writing 
the same view. Sample 2 implements such batching and can be used as a guidance.
* By default stream client will lazily enumerate all events, keeping memory 
consumption to minimum, even while enumerating 100GB streams. Make sure that 
you **avoid unnecessary memory allocations** (e.g. they happen if code converts 
event streams to arrays or lists via the use of ToList or ToArray). Also avoid 
any double enumerations.
* **Measure and optimize memory consumption of the projected views along 
with read-write speeds** of such views. If a large view takes 1GB in memory 
and is saved/read every 100 messages, then processing billions of messages 
can take a lot of time. 

Should you have more questions, please get in touch with Lokad support via contact@lokad.com