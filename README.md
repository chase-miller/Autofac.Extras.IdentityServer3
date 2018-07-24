# Autofac.Extras.IdentityServer3
Use your own autofac container with IdentityServer3

## Usage
Install the nuget package - https://www.nuget.org/packages/Autofac.Extras.IdentityServer3/.

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

namespace MyIdServer
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Create a ContainerBuilder and register dependencies.
            var builder = new ContainerBuilder();
            builder.RegisterModule(new IdServerExtensionsModule());
            builder.RegisterModule(new SomeOtherModule());

            // Build the container
            var container = builder.Build();
            
            // Create a blank factory. To set a service (e.g. IUserService) register it via autofac. See IdServerExtensionsModule class.
            var factory = new IdentityServerServiceFactory();
            
            // Use the container built above to setup the factory
            factory.ResolveUsingAutofac(container);

            var config = GetHttpConfiguration();
            config.DependencyResolver = new AutofacWebApiDependencyResolver(container);

            var idSrvOptions = GetIdentityServerOptions(factory);

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

And the autofac module:

```csharp
public class IdServerExtensionsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // Don't forget to register all required types.
        builder.RegisterType<UserService>()
            .As<IUserService>()
            .InstancePerRequest();
            
        builder.RegisterType<InMemoryScopeStore>()
            .As<IScopeStore>()
            .InstancePerRequest();
            
        builder.Register(cc => new ClientStore(cc.Resolve<IClientConfigurationDbContext>(), cc.Resolve<IMyDbContext>(), cc.Resolve<ILogger>()))
            .As<IClientStore>()
            .InstancePerRequest();

        // And any other optional ones you may want
        builder.Register(cc =>
                new ClaimsProvider(cc.Resolve<IUserService>(), cc.Resolve<IScopeProcessor>(), Scopes.Get()))
            .As<IClaimsProvider>()
            .InstancePerRequest();
        
        // If your services depend on any IdServer services (that are registered in its internal autofac container), use this extension method to make them accessible.
        builder.RegisterAsIdServerResolvable<IClientConfigurationDbContext>();
    }
}
```

## How It Works
Calling `factory.ResolveUsingAutofac(container)` will read the registrations contained on the `container` and create corresponding registrations with the factory. Unless registering as a singleton, dependencies are resolved using a factory func that:

1. Resolves the `IOwinContext`. E.g. `dr.Resolve<IOwinContext>()`. 
2. Gets the autofac scope associated with the current IOwinContext using the `IOwinContext.GetAutofacLifetimeScope()` extension method.
3. Resolves the requested service using a `Resolve` method off the lifetime scope. E.g. `scope.Resolve<T>()`. 


Autofac lifetime scopes are matched up with factory scopes as follows: 

| Autofac | IdServer Factory |
| ------  | ---------------- |
| SingleInstance | Singleton |
| InstancePerDependency | InstancePerUse |
| InstancePerRequest | InstancePerHttpRequest |
| InstancePerLifetimeScope | InstancePerHttpRequest |

### Performance
The process outlined above may add a bit of overhead when processing a request, but it should be negligible (example statistics are welcomed :-)).

Another note about performance: all autofac registrations you create (for which a handler exists) will create a registration in IdentityServer3. Internally, IdentityServer3 uses autofac. This means that there will be registrations in IdentityServer3 that are never actually used. Aside from the memory consumption of the registration, I'm fairly certain there isn't a cost to these extra registrations as autofac only resolves a registration when needed.

That said, there is an extension method you can call that restrics registrations to only types in the `IdentityServer3` namespace. This is largely untested, and I don't know exactly the results.

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    .RegisteringOnlyIdServerTypes()
                    // other handlers...
            );
```

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


#### Type Resolver
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
