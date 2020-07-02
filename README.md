## AWS X-Ray .NET Agent

The AWS X-Ray .NET Agent is a drop-in solution that enables the propagation of X-Ray traces within your web applications. This includes automatic tracing for AWS X-Ray SDK supported frameworks and libraries. The agent enables you to use the X-Ray SDK out of box, and requires no code changes to enable the basic propagation of traces. See the compatibility chart below for the current feature parity between the AWS X-Ray .NET SDK and the AWS X-Ray .NET Agent.

See the [Sample App](https://github.com/aws-samples/aws-xray-dotnet-webapp) for a demonstration on how to use the agent.

## Compatibility Chart

| **Feature**	| **X-Ray SDK(.NET)** | **X-Ray SDK(.NET Core)** | **X-Ray Agent(.NET)** | **X-Ray Agent(.NET Core)**|
| ----------- | ----------- | ----------- | ----------- | ----------- |
| [AWS](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-sdkclients.html) | ✔ | ✔ | ✔ | ✔ |
| [Incoming Http](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-messagehandler.html) | ✔ | ✔ | ✔ | ✔ |
| [HttpClient](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-httpclients.html) | ✔ | ✔ | ✔ | ✔ |
| [HttpWebRequest](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-httpclients.html) | ✔ | ✔ | ✔ | ✔ |
| [System.Data.SqlClient](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-sqlqueries.html) | ✔ | ✔ | ✔ | ✔ |
| [Microsoft.Data.SqlClient](https://docs.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient) | ❌ | ❌ |    ❌ |  ✔ |
| [EntityFramework](https://docs.microsoft.com/en-us/ef/) | ❌ |✔ (EF Core)| ✔ (EF 6)| ✔ (EF Core)| 
| [Local Sampling](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-configuration.html#xray-sdk-dotnet-configuration-sampling) | ✔ | ✔ | ✔ | ✔ |
| [Dynamic Sampling](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-configuration.html#xray-sdk-dotnet-configuration-sampling) | ✔ | ✔ | ✔ | ✔ |
| [Multithreaded Execution](https://github.com/aws/aws-xray-sdk-dotnet/tree/master#multithreaded-execution-net-and-net-core--nuget) | ✔ | ✔ | ✔ | ✔ |
| [Plugins](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-configuration.html#xray-sdk-dotnet-configuration-plugins) | ✔ | ✔ | ✔ | ✔ |
| [Custom Subsegment](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet-subsegments.html) | ✔ | ✔ | ✔ | ✔ |

## Prerequisites

If you're running an Asp.Net Core application, you need to install the latest version of [Visual C++ Redistributable ](https://support.microsoft.com/en-us/help/2977003/the-latest-supported-visual-c-downloads)

## Configuration

AWS X-Ray .Net Agent will register the [configuration items](https://github.com/aws/aws-xray-sdk-dotnet/tree/master#configuration) as AWS X-Ray .NET SDK.

Besides, AWS X-Ray .Net Agent will register the following configuration items. 
```
{
    "ServiceName" : "DefaultService",
    "DaemonAddress" : "127.0.0.1:2000",
    "TraceHttpRequests" : "true",
    "TraceAWSRequests" : "true",
    "TraceSqlRequests" : "true",
    "TraceEFRequests" : "true"
}
```
You can customize the service name of your application, the daemon address and specify which request to trace through `appsettings.json` file (Asp.Net Core) or `web.config` file (Asp.Net).

If you don't provide these configuration items, the default values shown above will be applied by AWS X-Ray .NET Agent. 

Note:

* .Net Agent doesn't provide configuration item to disable tracing incoming Http request. If you want to disable tracing incoming request, you may set `DisableXRayTracing` as `true`.

* AWS request will trigger Http outgoing handler, so if you want to disable tracing AWS request, you have to disable both AWS handler and Http outgoing handler.

* Similiar situation happens to Entity Framework request, which triggers both Entity Framework handler and Sql handler, therefore, if you want to disable tracing Entity Framework request, remember to disable Sql handler as well.

## Installation

The AWS X-Ray Auto-Instrumentation SDK for .NET (.netframework 4.5 and above) and .NET Core (.netstandard 2.0 and above) is in the form of Nuget package. You can install the package from [Nuget](https://www.nuget.org/) gallery or from Visual Studio editor. Search `AWSXRayRecorder.AutoInstrumentation`.

### Automatic Instrumentation

#### Internet Information Services (IIS)

##### Asp.Net Core

1. Import `AWSXRayRecorder.AutoInstrumentation` Nuget package into your project and **rebuild**. 
2. Download and run AWS X-Ray .NET Agent Installer.
3. Restart IIS and launch your application.
```
iisreset
```

##### Asp.Net

1. Download and run AWS X-Ray .NET Agent Installer.
3. Restart IIS and launch your application.
```
iisreset
```

#### Others (Not IIS)

##### Asp.Net Core

1. Import `AWSXRayRecorder.AutoInstrumentation` Nuget package into your project and **rebuild**.
2. Download and install AWS X-Ray .NET Agent Installer.
3. Launch your application as follows.
```
SET CORECLR_PROFILER = {AE47A175-390A-4F13-84CB-7169CEBF064A}
SET CORECLR_ENABLE_PROFILING = 1

dotnet YourApplication.dll
```
Note:

* **Do not set environment variables globally into the system variables as profiler will try to instrument all .NET processes running on the instance with AWS X-Ray tracing SDK.** 

##### Asp.Net

1. Import `AWSXRayRecorder.AutoInstrumentation` Nuget package into your project and **rebuild**.
2. Add the following snippet into the `web.config` file.
```
<system.webServer>
 <modules> 
  <add name="AWSXRayTracingModule" type="Amazon.XRay.Recorder.AutoInstrumentation.AspNetAutoInstrumentationModule,AWSXRayRecorder.AutoInstrumentation,Version=2.9.0.0,Culture=neutral,PublicKeyToken=d427001f96b0d0b6" /> 
 </modules>
</system.webServer>
```
3. Launch your application

### Manual Instrumentation

#### Asp.Net Core

Instead of using profiler, you may choose to manually instrument AWS X-Ray SDK into your Asp.Net Core application.

1. Import [AWSXRayRecorder.AutoInstrumentation]() Nuget package into your project.

2. Add the following method into any method in `startup.cs` or `program.cs` file
```
Amazon.XRay.Recorder.AutoInstrumentation.Initialize.AddXRay();
```

## Development

### Minimum Requirements

For building packages locally, you need to have Visual Studio 2019 installed with workloads **.NET desktop development** and **Desktop development with C++**.

## Getting Help

Please use these community resources for getting help.

* If you think you may have found a bug or need assistance, please open an [issue](https://github.com/aws/aws-xray-dotnet-agent/issues/new).
* Open a support ticket with [AWS Support](http://docs.aws.amazon.com/awssupport/latest/user/getting-started.html).
* Ask a question in the [AWS X-Ray Forum](https://forums.aws.amazon.com/forum.jspa?forumID=241&start=0).
* For contributing guidelines refer to [CONTRIBUTING.md](https://github.com/aws/aws-xray-dotnet-agent/blob/master/CONTRIBUTING.md).

## Documentation

The [developer guide](https://docs.aws.amazon.com/xray/latest/devguide/xray-sdk-dotnet.html) provides guidance on using the AWS X-Ray DotNet Agent. Please refer to the [Sample App](https://github.com/aws-samples/aws-xray-dotnet-webapp) for an example.

## License

The AWS X-Ray SDK DotNet Agent is licensed under the Apache 2.0 License. See LICENSE and NOTICE.txt for more information.