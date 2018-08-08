using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac.Core;
using Autofac.Extras.IdentityServer3.Core;

namespace Autofac.Extras.IdentityServer3.Extensions
{
    public static class KeyedRegistrationExtensions
    {
        public static Options ResolvingByCastingToKeyedService(this Options options)
        {
            return options.UsingTypeResolution(ResolvingByCastingToKeyedService);
        }

        public static Options BlockingKeyedRegistrations(this Options options)
        {
            return options.WithRegistrationHandler(
                context =>
                {
                    var resolvedType = context.ResolvedType;
                    var service = context.AutofacService as KeyedService;

                    if (resolvedType == null)
                        return false;

                    if (service == null)
                        return false;

                    return true;
                },
                (factory, context) => throw new NoHandlerFoundException("Keyed registrations are not supported", context));
        }

        /// <summary>
        /// Casts the provided <see cref="Autofac.Core.Service"/> as a <see cref="Autofac.Core.TypedService"/> and returns the <see cref="Autofac.Core.TypedService.ServiceType"/>.
        /// </summary>
        /// <param name="serviceGrouping"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private static bool ResolvingByCastingToKeyedService(IGrouping<Service, IComponentRegistration> serviceGrouping, out Type type)
        {
            type = null;

            var asTypedService = serviceGrouping.Key as KeyedService;
            if (asTypedService == null)
                return false;

            type = asTypedService.ServiceType;
            return type != null;
        }
    }
}
