
# Distributed Cache Playground

## Introduction
The Distributed Cache Playground showcases different implementations and their performance for retrieving data concurrently through a caching implementation.

## Application details

### Project information
The project is a console application built using .NET 5.0 in C#.
There are two console applications present in the solution:

 - DCP.Application
 - DCP.Bootstrapper

In order to simulate the event of multiple instances hitting multiple threads at the same time, a bootstrapper application is able to kickstart multiple console applications.

### Dependency information
One of the caches used in this application is Redis. In order to run the application, a working instance of Redis is required. The application defaults to localhost. If you are running Redis at a different endpoint, adjust the code where necessary.

**Used libraries and frameworks:**
 - Microsoft's StackExchange extensions
 - Microsoft's Dependency Injection
 - Microsoft's Hosting extensions
 - Microsoft's HTTP extensions for leveraging the .NET HTTP Client
 - Newtonsoft's JSON framework

**External services:**
 - The application leverages [JSONPlaceholder](https://jsonplaceholder.typicode.com/) as a JSON data source
 - Redis

### References
 - [Microsoft's documentation about Semaphore locking](https://docs.microsoft.com/en-us/dotnet/standard/threading/semaphore-and-semaphoreslim)
 - [Redis.io](https://redis.io/)
 - [Redlock.net by Sam Cook](https://github.com/samcook/RedLock.net)
 - [JSONPlaceholder](https://jsonplaceholder.typicode.com/)

## How to run the application

 1. Clone or download the source code
 2. Make sure Redis is running
 3. Restore the dependencies and build the solution
 4. Run the **DCP.Bootstrapper** application

## Benchmarking
A benchmark is done by retrieving 500 comments from a cache implementation. If there is no cached data, a request is done to a live JSON data source.

When starting the DCP.Bootstrapper application, it presents you with options to configure your current run.
The following options are available:
 - In-memory caching using a Semaphore Slim lock
 - Redis without any locking
 - Redis using a Semaphore Slim lock
 - Redis using Redlock as distributed locking through Redis itself

After choosing the desired option, you can select the number of instances to run. Enter the amount of desired instances here. Note that more instances consume more resources.

Every instance will open a new console window running the selected option.
One instance fires off 200 threads in parallel and every thread does the following:

 1. Try to retrieve the cached comments from a cache
 2. If the comments are not present in the cache, retrieve them from the origin.
 3. Place the retrieved comments (500 items) in the specified cache

Depending on your chosen option, locking might be applied over multiple threads.

## Results
Every instance will show the amount of threads going to origin, going to cache and the total time elapsed in milliseconds in the console window.
```