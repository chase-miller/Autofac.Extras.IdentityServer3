﻿using System;
using Autofac.Extras.IdentityServer3.Core;

namespace Autofac.Extras.IdentityServer3.Extensions
{
    public static class OptionsExtensions
    {
        /// <summary>
        /// Adds a registration handler that will be executed if the context's ResolvedType equals <typeparam name="T"/>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="registrationAction"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static Options WithRegistrationHandlerFor<T>(this Options options, RegistrationAction registrationAction, int? priority = null) where T : class
        {
            return options.WithRegistrationHandler(
                context => context.ResolvedType != null && context.ResolvedType == typeof(T),
                registrationAction,
                priority
            );
        }

        public static Options UsingInstanceIfNotResolvable<T>(this Options options, T instance, int? priority = null) where T : class
        {
            return options.WithRegistrationHandlerFor<T>(
                (factory, _) => factory.RegisterAsAutofacResolvableOrUse(instance),
                priority
            );
        }

        /// <summary>
        /// Includes a registration that matches by given predicate. Useful when "blanket" exclusions have been performed.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="predicate"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static Options Including(this Options options, Predicate<RegistrationContext> predicate,
            int? priority = null)
        {
            return options.WithRegistrationHandler(
                predicate,
                (factory, registrationContext) => factory.RegisterAsAutofacResolvable(registrationContext.ResolvedType, context: registrationContext),
                priority
            );
        }

        /// <summary>
        /// Includes a registration where the resolved type is T. Useful when "blanket" exclusions have been performed.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="predicate"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public static Options Including<T>(this Options options, int? priority = null) where T : class
        {
            return options.WithRegistrationHandlerFor<T>(
                (factory, registrationContext) => factory.RegisterAsAutofacResolvable<T>(context: registrationContext),
                priority
            );
        }

        public static Options ExcludingAll(this Options options)
        {
            return options.Excluding(
                (RegistrationContext _) => true 
            );
        }

        public static Options Excluding(this Options options, Predicate<RegistrationContext> predicate, int? priority = null)
        {
            return options.WithRegistrationHandler(
                predicate,
                (a, b) => { } // no-op
            );
        }

        public static Options Excluding(this Options options, Predicate<Type> predicate, int? priority = null)
        {
            return options.Excluding(
                context => context.ResolvedType != null && predicate(context.ResolvedType),
                priority
            );
        }

        public static Options Excluding<T>(this Options options, int? priority = null) where T : class
        {
            return options.Excluding(typeof(T), priority);
        }

        public static Options Excluding(this Options options, Type type, int? priority = null)
        {
            return options.Excluding(
                theType => theType == type,
                priority
            );
        }

        public static Options ExcludingTypeAssignableTo<T>(this Options options)
        {
            return options.Excluding(type => typeof(T).IsAssignableFrom(type));
        }
    }
}