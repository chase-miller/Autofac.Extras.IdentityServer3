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
