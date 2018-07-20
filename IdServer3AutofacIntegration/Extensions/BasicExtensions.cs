using System;
using Autofac;
using IdentityServer3.Core.Configuration;
using IdServer3AutofacIntegration.Core;

namespace IdServer3AutofacIntegration.Extensions
{
    public static class BasicExtensions
    {
        private static bool _hackExecuted = false;

        /// <summary>
        /// Creates IdServer factory registrations using the registrations on the provided container. 
        /// When evaluating a registration's services for types, <see cref="TypeRegistrationExtensions.ResolvingByCastingToTypedService"/> is used by default.
        /// When handling the registration, <see cref="TypeRegistrationExtensions.WithTypeRegistrationHandler"/> is used by default.
        /// Make sure to exclude any registrations that are registered with autofac to be resolved with IdServer as not doing this might result in infinite recursion. 
        /// If you are using <see cref="AutofacToIdServerExtensions.re{T}"/> this is already taken care of for you.
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="container"></param>
        /// <param name="optionsFunc">Provides extension points to use during registration. See <see cref="Options"/> for details.</param>
        /// <param name="throwOnNoRegistrationHandlerFound">A flag indicating whether to throw an exception if no registration handler could be found matching a context.</param>
        public static void ResolveUsingAutofac(
            this IdentityServerServiceFactory factory,
            IContainer container,
            Func<Options, Options> optionsFunc = null,
            bool throwOnNoRegistrationHandlerFound = false)
        {
            factory.ResolveUsingAutofacCore(
                container, 
                options =>
                {
                    var myOptions = TypeRegistrationExtensions.ResolvingByCastingToTypedService(options)
                            .WithTypeRegistrationHandler()
                            .RegisteringIdServerExtensionPointsExplicitly()
                            .ExcludingIdServerResolvableRegistrations()
                        ;

                    return optionsFunc?.Invoke(myOptions) ?? myOptions;
                },
                throwOnNoRegistrationHandlerFound
            );
        }
    }
}
