using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
        private static bool hackingEventServiceForStartupAlreadyExecuted = false;

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

        public static CustomIdServerRegistration CreateRegistration(Type type, Func<ILifetimeScope, object> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, object> resolveWithOwinContextFunc = null, string name = null, RegistrationContext context = null)
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
                dr => ResolveUsingAutofac(dr, type, resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc),
                name)
            {
                Mode = mode
            };
        }

        public static Registration<T> CreateRegistration<T>(Func<ILifetimeScope, T> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, T> resolveWithOwinContextFunc = null, string name = null, RegistrationContext context = null) where T : class
        {
            var mode = context.ConvertMode(); 

            if (mode == RegistrationMode.Singleton)
                // ReSharper disable once PossibleNullReferenceException
                return new Registration<T>(ResolutionExtensions.Resolve<T>((IComponentContext) context.SingletonLifetimeScope), name)
                {
                    Mode = RegistrationMode.Singleton
                };

            return new Registration<T>(
                dr => dr.ResolveUsingAutofac<T>(resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc))
            {
                Mode = mode
            };
        }

        public static T ResolveUsingAutofac<T>(this IDependencyResolver dr, Func<ILifetimeScope, T> resolveWithAutofacFunc = null, Func<IOwinContext, T> resolveWithOwinContextFunc = null)
        {
            var owinContext = dr.Resolve<IOwinContext>();

            if (resolveWithOwinContextFunc != null)
                return resolveWithOwinContextFunc(owinContext);

            var lifetimeScope = owinContext.GetLifetimeScopeHelper(typeof(T));

            var resolved = resolveWithAutofacFunc != null
                ? resolveWithAutofacFunc(lifetimeScope)
                : lifetimeScope.Resolve<T>();
            return resolved;
        }

        public static object ResolveUsingAutofac(this IDependencyResolver dr, Type type, Func<ILifetimeScope, object> resolveWithAutofacFunc = null, Func<IOwinContext, object> resolveWithOwinContextFunc = null)
        {
            var owinContext = dr.Resolve<IOwinContext>();

            if (resolveWithOwinContextFunc != null)
                return resolveWithOwinContextFunc(owinContext);

            var lifetimeScope = owinContext.GetLifetimeScopeHelper(type);

            var resolved = resolveWithAutofacFunc != null
                ? resolveWithAutofacFunc(lifetimeScope)
                : lifetimeScope.Resolve(type);
            return resolved;
        }

        public static ILifetimeScope GetLifetimeScopeHelper(this IOwinContext owinContext, Type type)
        {
            var lifetimeScope = owinContext.GetAutofacLifetimeScope();
            if (lifetimeScope != null)
                return lifetimeScope;

            throw new ApplicationException(
                $"Could not get autofac lifetime scope from owin context when trying to resolve {type}. Did you call appBuilder.UseAutofacMiddleware() (or appBuilder.UseAutofacLifetimeScopeInjector()) before appBuilder.UseIdentityServer() in your app's startup?");
        }

        public static void RegisterAsAutofacResolvable(this IdentityServerServiceFactory factory, Type type, Func<ILifetimeScope, object> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, object> resolveWithOwinContextFunc = null, string name = null, RegistrationContext context = null)
        {
            factory.Register(CreateRegistration(type, resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc, name, context));
        }

        public static void RegisterAsAutofacResolvable<T>(this IdentityServerServiceFactory factory, Func<ILifetimeScope, T> resolveWithLifetimeScopeFunc = null, Func<IOwinContext, T> resolveWithOwinContextFunc = null, string name = null, RegistrationContext context = null) where T : class
        {
            factory.Register(CreateRegistration(resolveWithLifetimeScopeFunc, resolveWithOwinContextFunc, name, context));
        }

        private static RegistrationMode ConvertMode(this RegistrationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context), "Cannot determine RegistrationMode of null context is provided");

            if (!context.MatchingAutofacRegistrations.Any())
                throw new ApplicationException("No registrations could be found on the given context.");

            var validLifetimes = context.MatchingAutofacRegistrations
                .Select(r => ConvertFromLifetime(r.Lifetime, r.Sharing))
                .Where(r => r.valid)
                .Select(r => r.mode)
                .OrderBy(mode => mode, RegistrationModeComparer.Instance) // order by "risky-ness" so that we choose the least risky
                .ToList();

            if (!validLifetimes.Any())
                throw new ApplicationException($"No valid lifetimes could be found for the resolved type {context.ResolvedType}. Valid lifetimes include .SingleInstance(), .InstancePerDependency(), and .InstancePerRequest().");

            return validLifetimes.First();
        }

        /// <summary>
        /// This should be registered before <see cref="AutofacAppBuilderExtensions.UseAutofacMiddleware"/>
        /// </summary>
        /// <param name="appBuilder"></param>
        /// <param name="container"></param>
        /// <returns></returns>
        [Obsolete("Make sure app.UseAutofacMiddleware() and/or app.UseAutofacLifetimeScopeInjector() is called before app.UseIdentityServer() and remove the call to this method.")]
        public static IAppBuilder UseIdServerAutofacIntegrationMiddleware(this IAppBuilder appBuilder)
        {
            // no-op TODO - (delete this method on next major version).
            return appBuilder;
        }

        public static Options RegisteringOnlyIdServerTypes(this Options options)
        {
            return options.Excluding(
                context => context?.ResolvedType?.Namespace?.StartsWith($"{nameof(IdentityServer3)}.") != true
            );
        }

        /// <summary>
        /// IdentityServer3 creates a fake owin context at startup to resolve IEventService. Use this to get around the fact that we don't have an autofac lifetime scope added to the owin context.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Options HackingEventServiceForStartup(this Options options)
        {
            return options.WithRegistrationHandlerFor<IEventService>(
                (factory, context) =>
                {
                    factory.EventService = CreateRegistration(resolveWithOwinContextFunc: owinContext =>
                    {
                        var lifetimeScope = owinContext.GetAutofacLifetimeScope();
                        if (lifetimeScope != null)
                            return lifetimeScope.Resolve<IEventService>();

                        if (hackingEventServiceForStartupAlreadyExecuted)
                        {
                            // This line should never be hit.
                            throw new ApplicationException("Lifetimescope could not be found. Did you call appBuilder.UseAutofacMiddleware() (or appBuilder.UseAutofacLifetimeScopeInjector()) before appBuilder.UseIdentityServer() in your app's startup?");
                        }

                        var startupScope = context.Container.BeginLifetimeScope(
                            MatchingScopeLifetimeTags.RequestLifetimeScopeTag,
                            b => b.RegisterInstance(owinContext).As<IOwinContext>()
                        );
                        owinContext.SetAutofacLifetimeScope(startupScope);
                        hackingEventServiceForStartupAlreadyExecuted = true;

                        return startupScope.Resolve<IEventService>();
                    });
                },
                100
            );
        }

        private static (RegistrationMode mode, bool valid) ConvertFromLifetime(IComponentLifetime lifetime, InstanceSharing sharing)
        {
            var instancePerDependency = lifetime is CurrentScopeLifetime && sharing == InstanceSharing.None;
            if (instancePerDependency)
                return (RegistrationMode.InstancePerUse, true);

            var singleInstance = lifetime is RootScopeLifetime && sharing == InstanceSharing.Shared;
            if (singleInstance)
                return (RegistrationMode.Singleton, true);

            var asMatchingScopeLifetime = lifetime as MatchingScopeLifetime;
            var instancePerRequest = sharing == InstanceSharing.Shared && asMatchingScopeLifetime?.TagsToMatch.All(ttm => ttm == MatchingScopeLifetimeTags.RequestLifetimeScopeTag) == true;
            if (instancePerRequest)
                return (RegistrationMode.InstancePerHttpRequest, true);

            var instancePerLifetimeScope = lifetime is CurrentScopeLifetime && sharing == InstanceSharing.Shared;
            if (instancePerLifetimeScope)
                return (default(RegistrationMode), false);

            return (default(RegistrationMode), false);
        }
    }

    internal class RegistrationModeComparer : IComparer<RegistrationMode>
    {
        public static readonly RegistrationModeComparer Instance = new RegistrationModeComparer();

        public int Compare(RegistrationMode x, RegistrationMode y)
        {
            if (x == y) return 0;

            var xInt = AsInt(x);
            var yInt = AsInt(y);

            return xInt > yInt ? 1 : -1;
        }

        private static int AsInt(RegistrationMode x)
        {
            switch (x)
            {
                case RegistrationMode.InstancePerUse: // this is the least risky because it will always use autofac to resolve
                    return 1;
                case RegistrationMode.InstancePerHttpRequest: // this is more risky because it will only use autofac to resolve on a given request
                    return 2;
                case RegistrationMode.Singleton: // this is most risky because it will only use autofac to resolve once
                    return 3;
                default:
                    return 234;                 // I don't know what to think about this one
            }
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
