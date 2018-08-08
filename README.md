# Autofac.Extras.IdentityServer3
Use your own autofac container with IdentityServer3

## Usage
Install the nuget package - https://www.nuget.org/packages/Autofac.Extras.IdentityServer3/.

In your startup configuration, call a method to have the IdentityServer3 factory use your container:
```csharp
factory.ResolveUsingAutofac(container);
```

Use autofac middleware (from the Autofac.Owin package) before the call to app.UseIdentityServer():
```csharp
app.UseAutofacMiddleware(container);
app.UseIdentityServer(options);
```

### Example

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


            // Make sure this is registered before id server middleware in the pipeline.
            app.UseAutofacMiddleware(container); 
            
            var idSrvOptions = GetIdentityServerOptions(factory);
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
### Middleware ordering
You may need more control over the ordering of your middleware registrations:
1. If any of your middleware depends on IdentityServer3 dependencies (e.g. those created via `builder.RegisterAsIdServerResolvable<IdentityServerOptions>()`). 
2. If you need your code to take control after IdentityServer3.
3. If you need your code to take control in the middle of IdentityServer3's pipeline. 

When doing this, you still need to create and add to the `OwinContext` an autofac `LifetimeScope` before IdentityServer takes control. Do this by calling `app.UseAutofacLifetimeScopeInjector()` **before** `app.UseIdentityServer()`. Then register your middleware after (or inside of via `PluginConfiguration`) IdentityServer3. 

```csharp
// Use autofac lifetime scope injector before registering any of our middleware (and before calling UseIdentityServer()).
app.UseAutofacLifetimeScopeInjector(configuration.AutofacContainer);

var idSrvOptions = GetIdentityServerOptions(factory);

// Register our middleware inside of IdentityServer3's pipeline.
idSrvOptions.PluginConfiguration = (appBuilder, options) =>
    appBuilder.UseAutofacMiddleware(configuration.AutofacContainer);

app.UseIdentityServer(idSrvOptions);
```

For more information on middleware ordering and autofac see https://autofaccn.readthedocs.io/en/latest/integration/owin.html#controlling-middleware-order. 

## How It Works
Calling `factory.ResolveUsingAutofac(container)` will read the registrations contained on the `container` and create corresponding registrations with the factory. By default only types in the `IdentityServer3.*` namespace will be read. Unless registering as a singleton, dependencies are resolved using a factory func that:

1. Resolves the `IOwinContext`. E.g. `dr.Resolve<IOwinContext>()`. 
2. Gets the autofac scope associated with the current IOwinContext using the `IOwinContext.GetAutofacLifetimeScope()` extension method.
3. Resolves the requested service using a `Resolve` method off the lifetime scope. E.g. `scope.Resolve<T>()`. 


Autofac lifetime scopes are matched up with factory scopes as follows: 

| Autofac Scope | IdServer Factory Scope |
| ------  | ---------------- |
| SingleInstance | Singleton |
| InstancePerDependency | InstancePerUse |
| InstancePerRequest | InstancePerHttpRequest |
| InstancePerLifetimeScope | * Throws Exception |
| * **Anything Else** | * Throws Exception |

Each registration will be added to the factory using `factory.Register()` unless a property exists off the factory (e.g. `factory.EventService`) in which case the property will be set. 

### Type Filtering
As mentioned above, by default only types in the `IdentityServer3.*` namespace will be registered with the factory. The rationale is that IdentityServer3 only provides hooks that use types in this namespace. 

"But you're wrong" you say? Use the `Including<T>()` method. 

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    .Including<ISomeTypeIdentityServerUses>()
                    // other handlers...
            );
```

One more thing: dependencies of your `IdentityServer3` extensions (e.g. `IUserService`) need not be added to the factory. Since the registrations added to the factory simply ask our autofac container to resolve, the dependencies only need to be registered with autofac. 

For example:
```csharp
public class IdServerExtensionsModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        // This registration will NOT be added to the factory, but will still be used when resolving the next registration.
        builder.RegisterType<SomeDependency>()
            .As<ISomeDependency>()
            .InstancePerLifetimeScope(); // lifetime scope is valid here because this isn't added to the factory
        
        // This registration will be added to the factory
        builder.Register(cc => new UserService(cc.Resolve<ISomeDependency>())
            .As<IUserService>()
            .InstancePerRequest();
        
        // ...
    }
}
```

#### Falling back to V2 behavior
If you'd rather register all types, including those outside `IdentityServer3.*`, use the `RegisteringAllTypes()` method.

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    .RegisteringAllTypes()
                    // other handlers...
            );
```

### Using Facotry Extension Methods
If you'd like a type to be registered using a factory extension method such as `factory.ConfigureClientStoreCache()`, use the `options.WithRegistrationHandler()` extension. See the [example below](#example-1).

## Extension Points
There are two primary extension points provided off the `Options` object:

1. `WithRegistrationHandler(Predicate<RegistrationContext> predicate, RegistrationAction registrationAction, int? priority = null)` Given a registration context that matches the predicate, how do we want to register that context with the factory?
2. `UsingTypeResolution(TryResolveType resolutionFunc)` - Given a registration context, to what type does the corresponding autofac service resolve? The output of this function will set the `ResolvedType` property on the `RegistrationContext`, which can be used by the above registration handler.

### Example
Note that some of these methods are actually extension methods off of the two extension points above. There are plenty more examples in this repo.

```csharp
factory.ResolveUsingAutofac(container,
                options => options
                    .HandlingClientStoreCache()
                    .RegisteringOperationalServices()
            );
```

And the corresponding extension methods:

```csharp
public static class IdServerAutofacExtensions
    {   
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
        
        public static Options RegisteringOperationalServices(this Options options)
        {
            return options.WithRegistrationHandler(
                context => context.ResolvedType == typeof(EntityFrameworkServiceOptions) && 
                           context.MatchingAutofacRegistrations.Any(r => r.Metadata.ContainsKey("RegisterOperationalServices")),
                (factory, context) => factory.RegisterOperationalServices(context.SingletonLifetimeScope.Resolve<EntityFrameworkServiceOptions>())
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
