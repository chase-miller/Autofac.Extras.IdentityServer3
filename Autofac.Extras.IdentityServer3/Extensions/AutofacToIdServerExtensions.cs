using System.Linq;
using Autofac.Builder;
using Autofac.Extras.IdentityServer3.Core;
using IdentityServer3.Core.Extensions;
using Microsoft.Owin;

namespace Autofac.Extras.IdentityServer3.Extensions
{
    public static class AutofacToIdServerExtensions
    {
        public const string IdServerResolvableKey = "IdServerResolvableKey";

        /// <summary>
        /// Prevents an infinite recursion (stack overflow) issue where id server asks autofac who asks id server who asks autofac...
        /// </summary>
        /// <param name="options"></param>
        /// <param name="predicate"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public static Options ExcludingIdServerResolvableRegistrations(this Options options)
        {
            return options.Excluding(
                context => context.MatchingAutofacRegistrations.Any(
                    componentRegistration => componentRegistration.Metadata.ContainsKey(IdServerResolvableKey)),
                1000 // run with a high priority as this will prevent stackoverflow exceptions
            );
        }

        /// <summary>
        /// Registers a type with autofac that uses IdServer to resolve.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IRegistrationBuilder<T, SimpleActivatorData, SingleRegistrationStyle> RegisterAsIdServerResolvable<T>(
            this ContainerBuilder builder)
        {
            return builder.Register(cc =>
                {
                    var owinContext = cc.Resolve<IOwinContext>();
                    return owinContext.Environment.ResolveDependency<T>();
                })
                .As<T>()
                .WithMetadata(IdServerResolvableKey, true)
                .InstancePerRequest();
        }
    }
}
