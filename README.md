# idserver3-autofac-integration
Use your own autofac container with IdServer3

## Usage
In your startup configuration, call a method to have the IdentityServer3 factory use your container:
```csharp
factory.ResolveUsingAutofac(container);
```

Then register some middleware:
```csharp
app.UseIdServerAutofacIntegrationMiddleware(); 
```

### More Complete Example

```csharp
[assembly: OwinStartup(typeof(Startup))]

namespace FL.IDM.IdentityGateway
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Create a ContainerBuilder and register dependencies.
            var builder = new ContainerBuilder();
            builder.RegisterModule(new IdServerExtensionsModule(logger, authenticationEventsLogger, efConfig));

            // Build the container
            var container = builder.Build();
            
            // Create a blank factory
            var factory = new IdentityServerServiceFactory();
            
            // Use the container built above to setup the factory
            factory.ResolveUsingAutofac(container);

            var idSrvOptions = GetIdentityServerOptions(factory, dataProtector);

            var config = GetHttpConfiguration();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            // Run any registered middleware within IdServer's context
            idSrvOptions.PluginConfiguration = (appBuilder, options) =>
            {
                appBuilder.UseAutofacMiddleware(container);
            };

            // Make sure this is run before autofac middleware and before id server middleware in the pipeline
            app.UseIdServerAutofacIntegrationMiddleware(); 
            app.UseIdentityServer(idSrvOptions);

            // Hook up webapi autofac stuff
            app.UseAutofacWebApi(config);
            app.UseWebApi(config);
        }
      }
  }

```

## How It Works
Calling `factory.ResolveUsingAutofac(container)` will read the registrations contained on the `container` and create corresponding registrations with the factory. Unless registering as a singleton, dependencies are resolved using a factory func that:

1. Resolves the `IOwinContext`. E.g. `dr.Resolve<IOwinContext>()`. 
2. Gets the autofac scope associated with the current IOwinContext using the `IOwinContext.GetAutofacLifetimeScope()` extension method.
3. Resolves the requested service using a `Resolve` method off the lifetime scope. E.g. `scope.Resolve<T>()`. 

This may add a bit of overhead when processing a request but should be negligible (example statistics are welcomed :-)). 

Autofac lifetime scopes are matched up with factory scopes as follows: 

| Autofac | IdServer Factory |
| ------  | ---------------- |
| SingleInstance | Singleton |
| InstancePerDependency | InstancePerUse |
| InstancePerRequest | InstancePerHttpRequest |
| InstancePerLifetimeScope | InstancePerHttpRequest |

## Extension Points
There are two primary extension points provided off the `Options` object:

1. `WithRegistrationHandler(Predicate<RegistrationContext> predicate, RegistrationAction registrationAction, int? priority = null)` Given a registration context that matches the predicate, how do we want to register that context with the factory?
2. `UsingTypeResolution(TryResolveType resolutionFunc)` - Given a registration context, to what type does the corresponding autofac service resolve? The output of this function will set the `ResolvedType` property on the `RegistrationContext`, which can be used by the above registration handler.

### Example
Note that some of these methods are actually extension methods off of the two extension points above. There are plenty more examples in this repo.

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    .ExcludingControllers()
                    .ExcludingOwinMiddleware()
                    .HandlingClientStoreCache()
                    .UsingInstanceIfNotResolvable<ILogger>(logger)
            );
```

And the corresponding extension methods:

```csharp
public static class IdServerAutofacExtensions
    {
        public static Options ExcludingControllers(this Options options)
        {
            return options.ExcludingTypeAssignableTo<ApiController>();
        }

        public static Options ExcludingOwinMiddleware(this Options options)
        {
            return options.ExcludingTypeAssignableTo<OwinMiddleware>();
        }
    
        public static Options HandlingClientStoreCache(this Options options)
        {
            return options.WithRegistrationHandlerFor<ICache<Client>>(
                (serviceFactory, context) =>
                {
                    var registration = new Registration<ICache<Client>>(dr => dr.ResolveUsingAutofac<ICache<Client>>(container: context.Container));
                    serviceFactory.ClientStore = new Registration<IClientStore>(dr => dr.ResolveUsingAutofac<IClientStore>(container: context.Container));
                    serviceFactory.ConfigureClientStoreCache(registration);
                }
            );
        }
    }
```

### Ordering
Both extension points can be chained. In other words, multiple items can be registered (see example below). But only one handler is chosen and only one type resolver is used to determine the type. So which one is chosen?


#### Registration Handler
The last item registered whose predicate returns true wins. Unless a higher priority item exists whose predicate returns true (in which case the higher priority wins).

Given an `ILogger` registered with autofac, when evaluating which handler to choose:

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    // Order 3 - Despite the predicate returning true, there are other eligible handlers registered after (and at a higher priority)
                    .WithRegistrationHandler(context => context.ResolvedType == typeof(ILogger), (factory, context) => MyHandle(factory, context))
                    // Order 1 - Wins because of priority (and predicate returns true)
                    .WithRegistrationHandler(context => context.ResolvedType == typeof(ILogger), (factory, context) => MyHandle(factory, context), 15)
                    // Order 2 - Would have been executed because it's last registered, but the second one has a higher priority (0 if none specified).
                    .WithRegistrationHandler(context => context.ResolvedType == typeof(ILogger), (factory, context) => MyHandle(factory, context))
                    // Not eligible - predicate returns false given an ILogger.
                    .WithRegistrationHandler(context => context.ResolvedType == typeof(SomeOtherType), (factory, context) => MyHandle(factory, context))
            );
```


### Type Resolver
The last item registered whose resolver returns true wins.

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    .UsingTypeResolution((IGrouping<Service, IComponentRegistration> grouping, out Type type) =>
                    {
                        type = typeof(object);
                        return true;
                    })
                    // Wins because it's the last registered that returns true
                    .UsingTypeResolution((IGrouping<Service, IComponentRegistration> grouping, out Type type) =>
                    {
                        type = typeof(object);
                        return true;
                    })
                    // Would have won but returns false
                    .UsingTypeResolution((IGrouping<Service, IComponentRegistration> grouping, out Type type) =>
                    {
                        type = typeof(object);
                        return false;
                    })
            );
```
