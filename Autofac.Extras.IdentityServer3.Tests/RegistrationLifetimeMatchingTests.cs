using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac.Extras.IdentityServer3.Core;
using Autofac.Extras.IdentityServer3.Extensions;
using FluentAssertions;
using IdentityServer3.Core.Configuration;
using NUnit.Framework;

namespace Autofac.Extras.IdentityServer3.Tests
{
    [TestFixture]
    public class RegistrationLifetimeMatchingTests
    {
        [Test]
        public void InstancePerRequestMaps()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerRequest();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options => 
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.InstancePerHttpRequest);
        }

        [Test]
        public void InstancePerDependencyMaps()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerDependency();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.InstancePerUse);
        }

        [Test]
        public void SingleInstanceMaps()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .SingleInstance();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.Singleton);
        }

        [Test]
        public void GivenMultipleScopesRegistered_ThenChooseLeastRisky()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerRequest();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerDependency();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .SingleInstance();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.InstancePerUse);
        }

        [Test]
        public void GivenMultipleScopesRegistered_ThenChooseLeastRisky2()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerRequest();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerDependency();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.InstancePerUse);
        }

        [Test]
        public void GivenMultipleScopesRegistered_ThenChooseLeastRisky3()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerRequest();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .SingleInstance();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.InstancePerHttpRequest);
        }

        [Test]
        public void GivenMultipleScopesRegistered_ThenChooseLeastRisky4()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerDependency();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .SingleInstance();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            var registrations = factory.Registrations.Where(r => r.DependencyType == typeof(ISomeInterface));
            var registration = registrations.Single();

            registration.Mode.Should().Be(RegistrationMode.InstancePerUse);
        }

        [Test]
        public void GivenInvalidScope_ThenThrowException()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerLifetimeScope();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            Action resolveAction = () => factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            resolveAction.Should().Throw<ApplicationException>().Where(e => e.Message.Contains("lifetime"));
        }

        [Test]
        public void GivenInvalidScope_ThenThrowException2()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerMatchingLifetimeScope("sometag");

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            Action resolveAction = () => factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            resolveAction.Should().Throw<ApplicationException>().Where(e => e.Message.Contains("lifetime"));
        }

        [Test]
        public void GivenMixOfValidAndInvalid_ThenThrowException()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerRequest();

            builder.Register(cc => new SomeImpl())
                .As<ISomeInterface>()
                .InstancePerLifetimeScope();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            Action resolveAction = () => factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<ISomeInterface>()
            );

            resolveAction.Should().Throw<ApplicationException>().Where(e => e.Message.Contains("lifetime"));
        }
    }

    public interface ISomeInterface { }
    public class SomeImpl : ISomeInterface { }
}
