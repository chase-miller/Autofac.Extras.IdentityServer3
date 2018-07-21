using System;
using System.Linq;
using Autofac.Core;
using Autofac.Core.Lifetime;
using Autofac.Extras.IdentityServer3.Core;
using Autofac.Integration.Owin;
using IdentityServer3.Core.Configuration;
using IdentityServer3.Core.Services;
using Microsoft.Owin;
using Owin;

namespace Autofac.Extras.IdentityServer3.Extensions
{
    public static class TypeRegistrationExtensions
    {
        public const string CustomDisposeLifetimescopeKey = "IdServerAutofacIntegrationCore.CustomDisposeLifetimescopeKey";
        public const string CustomDisposeLifetimescopeRegisteredKey = CustomDisposeLifetimescopeKey + "Registered";

        public static Options WithTypeRegistrationHandler(this Options options)
        {
            return options
                .WithRegistrationHandler(
                    context => context.ResolvedType != null,
                    (factory, context) => factory.RegisterAsAutofacResolvable(context.ResolvedType, context: context)
                );
        }

        public static Options ResolvingByCastingToTypedService(this Options options)
        {
            return options.UsingTypeResolution(ResolvingByCastingToTypedService);
        }

        /// <summary>
        /// Casts the provided <see cref="Autofac.Core.Service"/> as a <see cref="Autofac.Core.TypedService"/> and returns the <see cref="Autofac.Core.TypedService.ServiceType"/>.
        /// </summary>
        /// <param name="serviceGrouping"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool ResolvingByCastingToTypedService(IGrouping<Service, IComponentRegistration> serviceGrouping, out Type type)
        {
            type = null;

            var asTypedService = serviceGrouping.Key as TypedService;
            if (asTypedService == null)
                return false;

            type = asTypedService.ServiceType;
            return type != null;
        }

        /// <summary>
        /// Resolve with autofac, or if autofac doesn't exist in the owin context for whatever reason use the provided instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="factory"></param>
        /// <param name="instance"></param>
        public static void RegisterAsAutofacResolvableOrUse<T>(this IdentityServerServiceFactory factory, T instance, RegistrationContext context = null) where T : class
        {
            factory.RegisterAsAutofacResolvable<T>(resolveWithOwinContextFunc:
                owinContext =>
                {
                    var autofac = OwinContextExtensions.GetAutofacLifetimeScope(owinContext);
                    return autofac != null
                        ? autofac.Resolve<T>()
                        : instance;
                },
                context: context);
        }

        public static CustomIdServerRegistration CreateRegistration(Type type, Func<ILifetimeScope, object> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, object> resolveWithOwinContextFunc = null, string name = null, IContainer container = null, RegistrationContext context = null)
        {
            var mode = context?.ConvertMode() ?? RegistrationMode.InstancePerUse; // play it safe with InstancePerUse
            if (mode == RegistrationMode.Singleton)
            {
                return new CustomIdServerRegistration(
                    type,
                    // ReSharper disable once PossibleNullReferenceException
                    context.SingletonLifetimeScope.Resolve(type),
                    name)
                {
                    Mode = RegistrationMode.Singleton
                };
            }

            return new CustomIdServerRegistration(
                type,
                dr => ResolveUsingAutofac(dr, type, resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc, container ?? context?.Container),
                name)
            {
                Mode = mode
            };
        }

        public static Registration<T> CreateRegistration<T>(Func<ILifetimeScope, T> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, T> resolveWithOwinContextFunc = null, string name = null, IContainer container = null, RegistrationContext context = null, bool checkForMiddleware = true) where T : class
        {
            var mode = context?.ConvertMode() ?? RegistrationMode.InstancePerUse; // play it safe with InstancePerUse
            if (mode == RegistrationMode.Singleton)
                // ReSharper disable once PossibleNullReferenceException
                return new Registration<T>(ResolutionExtensions.Resolve<T>((IComponentContext) context.SingletonLifetimeScope), name)
                {
                    Mode = RegistrationMode.Singleton
                };

            return new Registration<T>(
                dr => dr.ResolveUsingAutofac<T>(resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc, container ?? context?.Container, checkForMiddleware))
            {
                Mode = mode
            };
        }

        public static T ResolveUsingAutofac<T>(this IDependencyResolver dr, Func<ILifetimeScope, T> resolveWithAutofacFunc = null, Func<IOwinContext, T> resolveWithOwinContextFunc = null, IContainer container = null, bool checkForMiddleware = true)
        {
            var owinContext = dr.Resolve<IOwinContext>();

            if (resolveWithOwinContextFunc != null)
                return resolveWithOwinContextFunc(owinContext);

            var lifetimeScope = owinContext.GetLifetimeScopeHelper(typeof(T), container, checkForMiddleware);

            var resolved = resolveWithAutofacFunc != null
                ? resolveWithAutofacFunc(lifetimeScope)
                : lifetimeScope.Resolve<T>();
            return resolved;
        }

        public static object ResolveUsingAutofac(this IDependencyResolver dr, Type type, Func<ILifetimeScope, object> resolveWithAutofacFunc = null, Func<IOwinContext, object> resolveWithOwinContextFunc = null, IContainer container = null, bool checkForMiddleware = true)
        {
            var owinContext = dr.Resolve<IOwinContext>();

            if (resolveWithOwinContextFunc != null)
                return resolveWithOwinContextFunc(owinContext);

            var lifetimeScope = owinContext.GetLifetimeScopeHelper(type, container, checkForMiddleware);

            var resolved = resolveWithAutofacFunc != null
                ? resolveWithAutofacFunc(lifetimeScope)
                : lifetimeScope.Resolve(type);
            return resolved;
        }

        public static ILifetimeScope GetLifetimeScopeHelper(this IOwinContext owinContext, Type type, IContainer container, bool checkForMiddleware = true)
        {
            var lifetimeScope = owinContext.GetAutofacLifetimeScope();
            if (lifetimeScope != null)
                return lifetimeScope;

            var middlewareIsRegistered = owinContext.Environment.ContainsKey(CustomDisposeLifetimescopeRegisteredKey);
            if (container != null && (middlewareIsRegistered || !checkForMiddleware))
            {
                // This makes me nervous...especially because we're not disposing the lifetimeScope in the same way as https://github.com/autofac/Autofac.Owin/blob/1e6eab35b59bc3838bbd2f6c7653d41647649b01/src/Autofac.Integration.Owin/AutofacAppBuilderExtensions.cs#L414.
                lifetimeScope = container.BeginLifetimeScope(
                    MatchingScopeLifetimeTags.RequestLifetimeScopeTag,
                    b => b.RegisterInstance(owinContext).As<IOwinContext>());
                owinContext.SetAutofacLifetimeScope(lifetimeScope);
                owinContext.Set(CustomDisposeLifetimescopeKey, true);
                return lifetimeScope;
            }

            if (container != null && !middlewareIsRegistered)
            {
                throw new ApplicationException(
                    $"Could not get autofac lifetime scope from owin context. A container was provided, " +
                    $"but cleanup middleware was not registered. " +
                    $"Be sure to call {nameof(UseIdServerAutofacIntegrationMiddleware)} in startup before {nameof(AutofacAppBuilderExtensions.UseAutofacMiddleware)}.");
            }

            throw new ApplicationException(
                $"Could not get autofac lifetime scope from owin context when trying to resolve {type}.");
        }

        public static void RegisterAsAutofacResolvable(this IdentityServerServiceFactory factory, Type type, Func<ILifetimeScope, object> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, object> resolveWithOwinContextFunc = null, string name = null, IContainer container = null, RegistrationContext context = null)
        {
            factory.Register(CreateRegistration(type, resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc, name, container, context));
        }

        public static void RegisterAsAutofacResolvable<T>(this IdentityServerServiceFactory factory, Func<ILifetimeScope, T> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, T> resolveWithOwinContextFunc = null, string name = null, IContainer container = null, RegistrationContext context = null) where T : class
        {
            factory.Register(CreateRegistration(resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc, name, container, context));
        }

        public static RegistrationMode ConvertMode(this RegistrationContext context)
        {
            if (context == null)
                return RegistrationMode.InstancePerUse; // play it safe with InstancePerUse

            var lifetimes = context.MatchingAutofacRegistrations.Select(r => ConvertFromLifetime(r.Lifetime, r.Sharing)).ToList();
            if (!lifetimes.Any() || lifetimes.Distinct().Count() > 1)
                return RegistrationMode.InstancePerUse; // play it safe with InstancePerUse

            return lifetimes.First();

            RegistrationMode ConvertFromLifetime(IComponentLifetime lifetime, InstanceSharing sharing)
            {
                var instancePerDependency = lifetime is RootScopeLifetime && sharing == InstanceSharing.None;
                if (instancePerDependency)
                    return RegistrationMode.InstancePerUse;

                var singleInstance = lifetime is RootScopeLifetime && sharing == InstanceSharing.Shared;
                if (singleInstance)
                    return RegistrationMode.Singleton;

                var instancePerLifetimeScope = lifetime is CurrentScopeLifetime && sharing == InstanceSharing.Shared;
                if (instancePerLifetimeScope)
                    return RegistrationMode.InstancePerHttpRequest;

                var asMatchingScopeLifetime = lifetime as MatchingScopeLifetime;
                var instancePerRequest = sharing == InstanceSharing.Shared && asMatchingScopeLifetime?.TagsToMatch.All(ttm => ttm == MatchingScopeLifetimeTags.RequestLifetimeScopeTag) == true;
                if (instancePerRequest)
                    return RegistrationMode.InstancePerHttpRequest;

                return RegistrationMode.InstancePerUse; // play it safe with InstancePerUse
            }
        }

        /// <summary>
        /// This should be registered before <see cref="AutofacAppBuilderExtensions.UseAutofacMiddleware"/>
        /// </summary>
        /// <param name="appBuilder"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        public static IAppBuilder UseIdServerAutofacIntegrationMiddleware(this IAppBuilder appBuilder)
        {
            return appBuilder.Use(async (context, next) =>
            {
                try
                {
                    context.Environment.Add(CustomDisposeLifetimescopeRegisteredKey, true);
                    await next();
                }
                finally
                {
                    if (context.Environment.TryGetValue(CustomDisposeLifetimescopeKey, out var shouldDisposeObj))
                    {
                        if (shouldDisposeObj is bool shouldDispose && shouldDispose)
                        {
                            var lifetimeScope = context.GetAutofacLifetimeScope();
                            lifetimeScope?.Dispose(); 
                        }
                    }
                }
            });
        }
    }

    public class CustomIdServerRegistration : Registration<object>
    {
        public CustomIdServerRegistration(Type type, Func<IDependencyResolver, object> factory, string name = null)
        {
            DependencyType = type;
            Factory = factory;
            Type = null; // make sure factory is used

            if (name != null)
                Name = name;
        }

        public CustomIdServerRegistration(Type type, object instance, string name = null)
        {
            DependencyType = type;
            Instance = instance;
            Type = null; // make sure factory is used

            if (name != null)
                Name = name;
        }

        public override Type DependencyType { get; }
    }
}
