using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Core;
using Autofac.Extras.IdentityServer3.Extensions;
using IdentityServer3.Core.Configuration;
using IdentityServer3.Core.Services;
using Microsoft.Owin;

namespace Autofac.Extras.IdentityServer3.Core
{
    public delegate bool TryResolveType(IGrouping<Service, IComponentRegistration> serviceGrouping, out Type type);

    public delegate void RegistrationAction(IdentityServerServiceFactory factory, RegistrationContext context);

    public static class IdServerAutofacIntegrationCore
    {
        /// <summary>
        /// Consider using another ResolveUsingAutofac()... method as this one does nothing until registering basic items with the <see cref="Options"/>
        /// Creates IdServer factory registrations using the registrations on the provided container. 
        /// Make sure to exclude any registrations that are registered with autofac to be resolved with IdServer as not doing this might result in infinite recursion.
        /// By default nothing will be registered until <see cref="TryResolveType"/> type resolution funcs are added to options.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="container"></param>
        /// <param name="optionsFunc">Provides extension points to use during registration. See <see cref="Options"/> for details.</param>
        /// <param name="throwOnNoRegistrationHandlerFound">A flag indicating whether to throw an exception if no registration handler could be found matching a context.</param>
        public static IdentityServerServiceFactory ResolveUsingAutofacCore(
            this IdentityServerServiceFactory factory, 
            IContainer container,
            Func<Options, Options> optionsFunc = null,
            bool throwOnNoRegistrationHandlerFound = true)
        {
            // Get access to IdServer call context
            factory.Register(new Registration<IOwinContext>(resolver => new OwinContext(resolver.Resolve<OwinEnvironmentService>().Environment)));

            var options = optionsFunc?.Invoke(new Options(factory)) ?? new Options(factory);

            var registrationHandlers = options.RegistrationHandlers
                .Select((registration, index) => (registration: registration, index: index))
                .OrderBy(tuple => tuple.registration.Priority)
                .ThenBy(tuple => tuple.index)
                .Select(tuple => tuple.registration)
                .ToList();

            var typeResolverFuncs = options.TypeResolutionFuncs.AsEnumerable().Reverse().ToList(); // use the last one registered first

            using (var singletonLifetimeScope = container.BeginLifetimeScope())
            {
                var registrationContexts =
                    from registration in container.ComponentRegistry.Registrations
                    from service in registration.Services
                    group registration by service
                    into serviceGrouping
                    select new RegistrationContext(serviceGrouping, ResolveTypeFromService(serviceGrouping, typeResolverFuncs), container, singletonLifetimeScope, options);

                foreach (var registrationContext in registrationContexts)
                {
                    var registrationHandler =
                        registrationHandlers.LastOrDefault(cr => cr.Predicate(registrationContext));

                    if (throwOnNoRegistrationHandlerFound && registrationHandler?.RegistrationAction == null)
                        throw new NoHandlerFoundException(registrationContext);

                    registrationHandler?.RegistrationAction?.Invoke(factory, registrationContext);
                }
            }

            return factory;
        }

        private static Type ResolveTypeFromService(IGrouping<Service, IComponentRegistration> serviceGrouping, List<TryResolveType> typeResolverFuncs)
        {
            foreach (var tryResolve in typeResolverFuncs)
            {
                if (tryResolve(serviceGrouping, out var type))
                    return type;
            }

            return null;
        }
    }

    /// <summary>
    /// Provides methods to use as extension points.
    /// </summary>
    public class Options
    {
        public List<RegistrationHandler> RegistrationHandlers { get; set; } = new List<RegistrationHandler>();

        public readonly List<TryResolveType> TypeResolutionFuncs = new List<TryResolveType>();

        public IdentityServerServiceFactory Factory { get; set; }

        public IDictionary<object, object> Metatadata { get; set; } = new Dictionary<object, object>();

        public Options(IdentityServerServiceFactory factory)
        {
            Factory = factory;
        }

        /// <summary>
        /// Extension point that allows the consumer to perform a specific registration if the provided predicate returns true.
        /// The highest priority (whose predicate returns true) is chosen for execution; if there are multiple of the same priority, then the highest priority that was the last one registered wins. 
        /// If none can be found, the default behavior (registering by type using <see cref="TryResolveType"/> funcs) is executed. 
        /// </summary>
        /// <param name="predicate">If this returns true (and I'm the first eligible), then execute the provided action.</param>
        /// <param name="registrationAction">The registration action to perform. Consider using extension methods such as <see cref="TypeRegistrationExtensions.ResolveUsingAutofac{T}"/></param>
        /// <param name="priority">Defaults to 0 if not provided.</param>
        /// <returns></returns>
        public Options WithRegistrationHandler(Predicate<RegistrationContext> predicate, RegistrationAction registrationAction, int? priority = null)
        {
            RegistrationHandlers.Add(new RegistrationHandler(predicate, registrationAction, priority ?? 0));
            return this;
        }

        /// <summary>
        /// Extension point that allows the consumer to provide a func that, given an autofac service (and its matching IComponentRegistration registrations), resolves to a type.
        /// These are executed in reverse order (based on the order in which this function was called) until one returns true and provides a type.
        /// </summary>
        /// <param name="resolutionFunc"></param>
        /// <returns></returns>
        public Options UsingTypeResolution(TryResolveType resolutionFunc)
        {
            TypeResolutionFuncs.Add(resolutionFunc);
            return this;
        }

        public Options WithMetadata(object key, object val)
        {
            Metatadata.Add(key, val);
            return this;
        }
    }

    public class RegistrationContext
    {
        public RegistrationContext(IGrouping<Service, IComponentRegistration> serviceGrouping, Type type,
            IContainer container, ILifetimeScope singletonLifetimeScope, Options options)
        {
            Container = container;
            AutofacService = serviceGrouping.Key;
            MatchingAutofacRegistrations = serviceGrouping;
            ResolvedType = type;
            SingletonLifetimeScope = singletonLifetimeScope;
            Options = options;
        }

        public Service AutofacService { get; set; }
        public IEnumerable<IComponentRegistration> MatchingAutofacRegistrations { get; set; } = Enumerable.Empty<IComponentRegistration>();
        public Type ResolvedType { get; set; }
        public IContainer Container { get; set; }
        public Options Options { get; set; }

        /// <summary>
        /// A lifetime scope created for the purpose of resolving singletons. This is disposed after all registrations have been (or attempted to have been) handled.
        /// </summary>
        public ILifetimeScope SingletonLifetimeScope { get; set; }

    }

    public class RegistrationHandler
    {
        public RegistrationHandler()
        { }

        public RegistrationHandler(Predicate<RegistrationContext> predicate, RegistrationAction registrationAction, int priority)
        {
            Predicate = predicate;
            RegistrationAction = registrationAction;
            Priority = priority;
        }

        public Predicate<RegistrationContext> Predicate { get; set; }
        public RegistrationAction RegistrationAction { get; set; }
        public int Priority { get; set; }
    }

    public class NoHandlerFoundException : Exception
    {
        public RegistrationContext Context { get; }

        public NoHandlerFoundException(RegistrationContext context) : base("No registration handler found for this context")
        {
            Context = context;
        }
    }
}
