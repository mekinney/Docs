---
title: Filters | Microsoft Docs
author: ardalis
description: Learn how *Filters* work and how to use them in ASP.NET Core MVC.
keywords: ASP.NET Core, filters, mvc filters, action filters, authorization filters, resource filters, result filters, exception filters
ms.author: tdykstra
manager: wpickett
ms.date: 12/12/2016
ms.topic: article
ms.assetid: 531bda08-aa5b-4471-8f08-96add22c8683
ms.technology: aspnet
ms.prod: aspnet-core
uid: mvc/controllers/filters
---
# Filters

By [Tom Dykstra](https://github.com/tdykstra/) and [Steve Smith](http://ardalis.com)

*Filters* in ASP.NET Core MVC allow you to run code before or after certain stages in the request processing pipeline. Built-in filters handle tasks such as authorization (preventing access to resources a user isn't authorized for), ensuring that all requests use HTTPS, and response caching (short-circuiting the request pipeline to return a cached response). You can create custom filters for work specific to your application, such as error handling.

[View or download sample from GitHub](https://github.com/aspnet/Docs/tree/master/aspnetcore/mvc/controllers/filters/sample).

## How do filters work?

Filters run within the *MVC action invocation pipeline*, sometimes referred to as the *filter pipeline*.  The filter pipeline runs after MVC selects the action to execute.

![image](filters/_static/filter-pipeline-1.png)

### Filter Types

Each filter type is executed at a different stage in the filter pipeline.

* [Authorization filters](#authorization-filters) run first and are used to determine whether the current user is authorized for the current request. They can short-circuit the pipeline if a request is unauthorized. 

* [Resource filters](#resource-filters) are the first to handle a request after authorization.  They can run code before the rest of the filter pipeline, and after the rest of the pipeline has completed. They're useful to implement caching or otherwise short-circuit the filter pipeline for performance reasons.

* [Action filters](#action-filters) can run code immediately before and after an individual action method is called. They can be used to manipulate the arguments passed into an action and the result returned from the action.

* [Exception filters](#exception-filters) come after Action filters in the pipeline and are used to apply global policies to unhandled exceptions that have occurred up to that point. 

* [Result filters](#result-filters) can run code immediately before and after the execution of individual action results. They run only when the action method has executed successfully and are useful for logic that must surround view or formatter execution.

The following diagram shows how these filter types interact in the filter pipeline.

![image](filters/_static/filter-pipeline-2.png)

### Implementation

Filters support both synchronous and asynchronous implementations through different interface definitions. 

Synchronous filters that can run code both before and after their pipeline stage define On*Stage*Executing and On*Stage*Executed methods. For example, `OnActionExecuting` is called before the action method is called, and `OnActionExecuted` is called after the action method returns.

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/SampleActionFilter.cs?highlight=6,8,13)]

Asynchronous filters define a single On*Stage*ExecutionAsync method. This method takes a *FilterType*ExecutionDelegate delegate which executes the filter's pipeline stage. For example, `ActionExecutionDelegate` calls the action method, and you can execute code before and after you call it.

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/SampleAsyncActionFilter.cs?highlight=6,8-10,13)]

You can implement interfaces for multiple filter stages in a single class. For example, the [ActionFilterAttribute](https://docs.microsoft.com/aspnet/core/api/microsoft.aspnetcore.mvc.filters.actionfilterattribute) abstract class implements both `IActionFilter` and `IResultFilter`, as well as their async equivalents.

> [!NOTE]
> Implement **either** the synchronous or the async version of a filter interface, not both. The framework checks first to see if the filter implements the async interface, and if so, it calls that. If not, it calls the synchronous interface's method(s). If you were to implement both interfaces on one class, only the async method would be called. When using abstract classes like [ActionFilterAttribute](https://docs.microsoft.com/aspnet/core/api/microsoft.aspnetcore.mvc.filters.actionfilterattribute) you would override only the synchronous methods or the async method for each filter type.

### Built-in filter attributes

The framework includes built-in attribute-based filters that you can subclass and customize. For example, the following Result filter adds a header to the response.

<a name=add-header-attribute></a>

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/AddHeaderAttribute.cs?highlight=5,16)]

Attributes allow filters to accept arguments, as shown in the example above. You would add this attribute to a controller or action method and specify the name and value of the HTTP header:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Controllers/SampleController.cs?name=snippet_AddHeader&highlight=1)]

The result of the `Index` action is shown below - the response headers are displayed on the bottom right.

![image](filters/_static/add-header.png)

Several of the filter interfaces have corresponding attributes that can be used as base classes for custom implementations.

Filter attributes:

* `ActionFilterAttribute`
* `ExceptionFilterAttribute`
* `ResultFilterAttribute`
* `FormatFilterAttribute`
* `ServiceFilterAttribute`
* `TypeFilterAttribute`

`TypeFilterAttribute` and `ServiceFilterAttribute` are explained [later in this article](#dependency-injection).

## Filter scopes

A filter can be added to the pipeline at one of three *scopes*. You can add a filter to a particular action method or to a controller class by using an attribute. Or you can register a filter globally by adding it to the `MvcOptions.Filters` collection in the `ConfigureServices` method in the `Startup` class:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Startup.cs?name=snippet_ConfigureServices&highlight=5-7)]

Filters can be added by type or by instance. If you add an instance, that instance will be used for every request. If you add a type, it will be type-activated, meaning an instance will be created for each request and any constructor dependencies will be populated by [dependency injection](../../fundamentals/dependency-injection.md) (DI). Adding a filter by type is equivalent to `filters.Add(new TypeFilterAttribute(typeof(MyFilter)))`.

## Cancellation and Short Circuiting

You can short-circuit the filter pipeline at any point by setting the `Result` property on the `context` parameter provided to the filter method. For instance, the following Resource filter prevents the rest of the pipeline from executing.

<a name=short-circuiting-resource-filter></a>

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/ShortCircuitingResourceFilterAttribute.cs?highlight=12,13,14,15)]

In the following code, both the `ShortCircuitingResourceFilter` and the `AddHeader` filter target the `SomeResource` action method. However, because the `ShortCircuitingResourceFilter` runs first and short-circuits the rest of the pipeline, the `AddHeader` filter never runs for the `SomeResource` action. This behavior would be the same if both filters were applied at the action method level, provided the `ShortCircuitingResourceFilter` ran first (see [Ordering](#ordering) later in this article).

[!code-csharp[Main](./filters/sample/src/FiltersSample/Controllers/SampleController.cs?name=snippet_AddHeader&highlight=1,9)]

## Dependency Injection

Filters that are implemented as attributes and added directly to controller classes or action methods cannot have constructor dependencies provided by [dependency injection](../../fundamentals/dependency-injection.md) (DI). This is because attributes must have their constructor parameters supplied where they are applied. This is a limitation of how attributes work.

If your filters have dependencies that you need to access from DI, there are several supported approaches. You can apply your filter to a class or action method using one of the following:

* `ServiceFilterAttribute`
* `TypeFilterAttribute`
* `IFilterFactory` implemented on your attribute

> [!NOTE]
> One dependency you might want to get from DI is a logger. However, avoid creating and using filters purely for logging purposes, since the [built-in framework logging features](../../fundamentals/logging.md) may already provide what you need. If you're going to add logging to your filters, it should focus on business domain concerns or behavior specific to your filter, rather than MVC actions or other framework events.

### ServiceFilterAttribute

A `ServiceFilter` retrieves an instance of the filter from DI. You add the filter to the container in `ConfigureServices`, and reference it in a `ServiceFilter` attribute

[!code-csharp[Main](./filters/sample/src/FiltersSample/Startup.cs?name=snippet_ConfigureServices&highlight=10)]

[!code-csharp[Main](../../mvc/controllers/filters/sample/src/FiltersSample/Controllers/HomeController.cs?name=snippet_ServiceFilter&highlight=1)]

Using `ServiceFilter` without registering the filter type results in an exception:

```
System.InvalidOperationException: No service for type
'FiltersSample.Filters.AddHeaderFilterWithDI' has been registered.
```

`ServiceFilterAttribute` implements `IFilterFactory`, which exposes a single method for creating an `IFilter` instance. In the case of `ServiceFilterAttribute`, the `IFilterFactory` interface's `CreateInstance` method is implemented to load the specified type from the services container (DI).

### TypeFilterAttribute

`TypeFilterAttribute` is very similar to `ServiceFilterAttribute` (and also implements `IFilterFactory`), but its type is not resolved directly from the DI container. Instead, it instantiates the type by using `Microsoft.Extensions.DependencyInjection.ObjectFactory`.

Because of this difference, types that are referenced using the `TypeFilterAttribute` do not need to be registered with the container first (but they will still have their dependencies fulfilled by the container). Also, `TypeFilterAttribute` can optionally accept constructor arguments for the type in question. The following example demonstrates how to pass arguments to a type using `TypeFilterAttribute`:

[!code-csharp[Main](../../mvc/controllers/filters/sample/src/FiltersSample/Controllers/HomeController.cs?name=snippet_TypeFilter&highlight=1,2)]

If you have a filter that doesn't require any arguments, but which has constructor dependencies that need to be filled by DI, you can use your own named attribute on classes and methods instead of `[TypeFilter(typeof(FilterType))]`). The following filter shows how this can be implemented:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/SampleActionFilterAttribute.cs?name=snippet_TypeFilterAttribute&highlight=1,3,7)]

This filter can be applied to classes or methods using the `[SampleActionFilter]` syntax, instead of having to use `[TypeFilter]` or `[ServiceFilter]`.

## IFilterFactory

`IFilterFactory` implements `IFilter`. Therefore, an `IFilterFactory` instance can be used as an `IFilter` instance anywhere in the filter pipeline. When the framework prepares to invoke the filter, it attempts to cast it to an `IFilterFactory`. If that cast succeeds, the `CreateInstance` method is called to create the `IFilter` instance that will be invoked. This provides a very flexible design, since the precise filter pipeline does not need to be set explicitly when the application starts.

You can implement `IFilterFactory` on your own attribute implementations as another approach to creating filters:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/AddHeaderWithFactoryAttribute.cs?name=snippet_IFilterFactory&highlight=1,4,5,6,7)]

## Ordering

If there are multiple filters for a particular stage of the pipeline, the default order of execution depends on filter scope:

1. The *before* code of filters applied globally
2. The *before* code of filters applied to controllers
3. The *before* code filters applied to action methods

The *after* code runs in reverse order. 

Here's an example for synchronous Action filters.

| Sequence | Filter scope | Filter method |
|:--------:|:------------:|:-------------:|
| 1 | Global | `OnActionExecuting` |
| 2 | Controller | `OnActionExecuting` |
| 3 | Method | `OnActionExecuting` |
| 4 | Method | `OnActionExecuted` |
| 5 | Controller | `OnActionExecuted` |
| 6 | Global | `OnActionExecuted` |

You can override this default sequence of execution by implementing `IOrderedFilter`. This interface exposes an `Order` property that takes precedence over scope to determine the order of execution. A filter with a lower `Order` value will have its *before* code executed before that of a filter with a higher value of `Order`. A filter with a lower `Order` value will have its *after* code executed after that of a filter with a higher `Order` value. 

You can set the `Order` property by using a constructor parameter:

```csharp
[MyFilter(Name = "Controller Level Attribute", Order=1)]
```

If you have the same 3 Action filters shown in the preceding example but set the `Order` property of the controller and global filters to 1 and 2 respectively, the order of execution would be reversed.

| Sequence | Filter scope | `Order` property | Filter method |
|:--------:|:------------:|:-----------------:|:-------------:|
| 1 | Method | 0 | `OnActionExecuting` |
| 2 | Controller | 1  | `OnActionExecuting` |
| 3 | Global | 2  | `OnActionExecuting` |
| 4 | Global | 2  | `OnActionExecuted` |
| 5 | Controller | 1  | `OnActionExecuted` |
| 6 | Method | 0  | `OnActionExecuted` |

`Order` trumps scope when determining the order in which filters will run. Filters are sorted first by order, then scope is used to break ties. All of the built-in filters implement `IOrderedFilter` and set the default `Order` value to 0, so scope determines order unless you set `Order` to a non-zero value.

> [!NOTE]
> Every controller that inherits from the `Controller` base class includes `OnActionExecuting` and `OnActionExecuted` methods. These methods wrap the filters that run for a given action:  `OnActionExecuting` is called before any of the filters, and `OnActionExecuted` is called after all of the filters.

## Authorization Filters

*Authorization Filters* control access to action methods and are the first filters to be executed within the filter pipeline. They have only a before method, unlike most filters that support before and after methods. You should only write a custom authorization filter if you are writing your own authorization framework. Note that you should not throw exceptions within authorization filters, since nothing will handle the exception (exception filters won't handle them). Instead, issue a challenge or find another way.

Learn more about [Authorization](../../security/authorization/index.md).

## Resource Filters

*Resource Filters* implement either the `IResourceFilter` or `IAsyncResourceFilter` interface, and their execution wraps most of the filter pipeline (only [Authorization Filters](#authorization-filters) run before them. Resource filters are especially useful if you need to short-circuit most of the work a request is doing. Caching is a typical use for resource filters, since they can avoid the rest of the pipeline if the response is already in the cache.

The [short circuiting resource filter](#short-circuiting-resource-filter) shown earlier is one example of a resource filter. A very naive cache implementation (do not use this in production) that only works with `ContentResult` action results is shown below:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/NaiveCacheResourceFilterAttribute.cs?name=snippet_CacheFilter&highlight=1,2,11,16,17,27,30)]

In `OnResourceExecuting`, if the result is already in the static dictionary cache, the `Result` property is set on `context`, and the action short-circuits and returns with the cached result. In the `OnResourceExecuted` method, if the current request's key isn't already in use, the current `Result` is stored in the cache, to be used by future requests.

## Action Filters

*Action Filters* implement either the `IActionFilter` or `IAsyncActionFilter` interface and their execution surrounds the execution of action methods.

Properties that the `ActionExecutingContext` provides include the following:

* `ActionArguments` - use to manipulate the inputs to the action.
* `Controller` - use to manipulate the controller. 
* `Result` - setting this will short-circuit execution of the action method and subsequent action filters. (Throwing an exception also prevents execution of the action method and subsequent filters, but is treated as a failure instead of successful result.)

Properties that the `ActionExecutedContext` provides include the following:

* `Result` - use to manipulate the result of the action method.
* `Canceled`  - will be true if the action execution was short-circuited by another filter. 
* `Exception` - will be non-null if the action or a subsequent action filter threw an exception. Setting this property to null effectively 'handles' an exception, and `Result` will be executed as if it were returned from the action method normally.

For an `IAsyncActionFilter` a call to `await next()` on the `ActionExecutionDelegate` executes any subsequent action filters and the action method, returning an `ActionExecutedContext`. To short-circuit inside of an `OnActionExecutionAsync`, assign `ActionExecutingContext.Result` to some result instance and do not call the `ActionExectionDelegate`.

## Exception Filters

*Exception Filters* implement either the `IExceptionFilter` or `IAsyncExceptionFilter` interface. The framework provides an abstract `ExceptionFilterAttribute` that you can subclass. Exception filters can provide a single location to implement common error handling policies within an app. 

Exception filters handle unhandled exceptions, including those that occur during controller creation and [model binding](../models/model-binding.md). They are only called when an exception occurs in the pipeline. Because of their location in the pipeline, they won't catch exceptions that occur in Result filters, MVC Result execution, or Resource filter code that runs after pipeline completion.

Exception filters are good for trapping exceptions that occur within MVC actions, but they're not as flexible as error handling middleware. Prefer middleware for the general case, and use filters only where you need to do error handling *differently* based on which MVC action was chosen. One example where you might need a different form of error handling for different actions would be in an app that exposes both API endpoints and actions that return views/HTML. The API endpoints could return error information as JSON, while the view-based actions could return an error page as HTML.

Exception filters do not have two events (for before and after) - they only implement `OnException` (or `OnExceptionAsync`). The `ExceptionContext` includes the `Exception` that occurred. If you set `context.ExceptionHandled` to `true`, the effect is that you've handled the exception, so the request will proceed as if it hadn't occurred (generally returning a 200 OK status). 

The following sample exception filter uses a custom developer error view to display details about exceptions that occur when the application is in development:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/CustomExceptionFilterAttribute.cs?name=snippet_ExceptionFilter)]

## Result Filters

*Result Filters* implement either the `IResultFilter` or `IAsyncResultFilter` interface, and their execution surrounds the execution of action results. Result filters are only executed for successful results - when the action or action filters produce an action result. Result filters are not executed when exception filters handle an exception, unless the exception filter sets `Exception = null`.

> [!NOTE]
> The kind of result being executed depends on the action in question. An MVC action returning a view would include all razor processing as part of the `ViewResult` being executed. An API method might perform some serialization as part of the execution of the result. Learn more about [action results](actions.md)

Result filters are ideal for any logic that needs to directly surround view execution or formatter execution. Result filters can replace or modify the action result that's responsible for producing the response.

The `OnResultExecuting` method runs before the action result is executed, so it can manipulate the action result through `ResultExecutingContext.Result`. An `OnResultExecuting` method can short-circuit execution of the action result and subsequent result filters by setting `ResultExecutingContext.Cancel` to true. If short-circuited, MVC will not modify the response; you should generally write to the response object directly when short-circuiting to avoid generating an empty response. Throwing an exception in an `OnResultExecuting` method will also prevent execution of the action result and subsequent filters, but will be treated as a failure instead of a successful result.

The `OnResultExecuted` method runs after the action result has executed. At this point if no exception was thrown, the response has likely been sent to the client and cannot be changed further. `ResultExecutedContext.Canceled` will be set to true if the action result execution was short-circuited by another filter. `ResultExecutedContext.Exception` will be set to a non-null value if the action result or a subsequent result filter threw an exception. Setting `ResultExecutedContext.Exception` to null effectively 'handles' an exception and will prevent the exception from being rethrown by MVC later in the pipeline. When you're handling an exception in a result filter, consider whether or not it's appropriate to write any data to the response. If the action result throws partway through its execution, and the headers have already been flushed to the client, there's no reliable mechanism to send a failure code.

For an `IAsyncResultFilter` a call to `await next()` on the `ResultExecutionDelegate` executes any subsequent result filters and the action result. To short-circuit, set `ResultExecutingContext.Cancel` to true and do not call the `ResultExectionDelegate`.

You can override the built-in `ResultFilterAttribute` to create result filters. The [AddHeaderAttribute](#add-header-attribute) class shown earlier is an example of a result filter.

>[!TIP]
> If you need to add headers to the response, do so before the action result executes. Otherwise, the response may have been sent to the client, and it will be too late to modify it. For a result filter, this means adding the header in `OnResultExecuting` rather than `OnResultExecuted`.

## Using Middleware as Filters

Filters work like [middleware](../../fundamentals/middleware.md), but the filter pipeline is part of MVC, which means that filters have access to MVC context and constructs. For instance, a filter can detect whether model validation on a request has generated errors. 

In ASP.NET Core 1.1, you can use middleware in the filter pipeline. You might want to do that if you have a middleware component that needs access to MVC route data, or one that should run only for certain controllers or actions.

To use middleware as a filter, create a type with a `Configure` method that specifies the middleware that you want to inject into the filter pipeline. Here's an example that uses the localization middleware to establish the current culture for a request:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Filters/LocalizationPipeline.cs?name=snippet_MiddlewareFilter&highlight=3,20)]

You can then use the `MiddlewareFilterAttribute` to run the middleware for a selected controller or action or globally:

[!code-csharp[Main](./filters/sample/src/FiltersSample/Controllers/HomeController.cs?name=snippet_MiddlewareFilter&highlight=2)]

Middleware filters run at the same stage of the filter pipeline as Resource filters, before and after model binding and the rest of the pipeline.

## Next actions

To experiment with filters, [download, test and modify the sample](https://github.com/aspnet/Docs/tree/master/aspnetcore/mvc/controllers/filters/sample).
