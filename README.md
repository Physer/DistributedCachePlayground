# Distributed Cache Playground

## Introduction
This project is showcasing the advantages and disadvantages of using Redis as your caching mechanism including ways to implement locking on these requests.

## Run instructions
In order to run the application you'll need to have Redis running locally.
If you do not use localhost as your connection endpoint, please adjust it accordingly in IHostBuilder configuration section in Program.cs (CreateHostBuilder).

The application leverages [JSONPlaceholder](https://jsonplaceholder.typicode.com/) as a JSON data source

## Application details
### General details
The distributed cache playground project is a .NET 5.0 project written in C#.
It's a console application, making use of the following libraries:
 - StackExchange's Redis library
 - Microsoft's StackExchange extensions
 - Microsoft's Dependency Injection
 - Microsoft's Hosting extensions
 - Microsoft's HTTP extensions for leveraging the .NET HTTP Client
 - Newtonsoft's JSON framework

### Code flow
The application's entrypoint is Program.cs.
This file contains a Main method. This methods executes the console application.

The application sets up its dependencies required for executing the code.
Next up is cleaning the cache of any remaining cache keys from previous runs.

The code does 2 runs. The first run is without any form of locking, the second run uses a Semaphore locking algorithm. ([Microsoft's documentation about Semaphore locking](https://docs.microsoft.com/en-us/dotnet/standard/threading/semaphore-and-semaphoreslim))

For every run, 200 threads in parallel try to retrieve the comments from the Redis cache. If the comments can't be located in the Redis cache, the thread will do an HTTP request to a JSON source with 500 comments and put them in the Redis cache.

## Results
The application outputs the results in a table in your console output.
These results show the amount of threads going to origin, going to cache and the total time elapsed in milliseconds.
```
