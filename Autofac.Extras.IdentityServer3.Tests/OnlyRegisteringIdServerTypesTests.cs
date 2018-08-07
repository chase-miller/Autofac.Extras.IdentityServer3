using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autofac.Extras.IdentityServer3.Extensions;
using FluentAssertions;
using IdentityServer3.Core.Configuration;
using IdentityServer3Fake.Models;
using NUnit.Framework;

namespace Autofac.Extras.IdentityServer3.Tests
{
    [TestFixture]
    class OnlyRegisteringIdServerTypesTests
    {
        [Test]
        public void GivenIdServerType_ThenRegister()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new DataProtectorImpl())
                .As<IDataProtector>()
                .InstancePerRequest();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container);

            factory.Registrations.Should()
                .Contain(registration => registration.DependencyType == typeof(IDataProtector));
        }

        [Test]
        public void GivenTypeNotInIdServer3Namespace_DontRegister()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new FakeClaimsProviderImpl())
                .As<IClaimsProvider>()
                .InstancePerRequest();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container);

            factory.Registrations.Should()
                .NotContain(registration => registration.DependencyType == typeof(IClaimsProvider));
        }

        [Test]
        public void GivenIncludeOfExternalType_ThenRegister()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new FakeClaimsProviderImpl())
                .As<IClaimsProvider>()
                .InstancePerRequest();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<IClaimsProvider>()
            );

            factory.Registrations.Should()
                .Contain(registration => registration.DependencyType == typeof(IClaimsProvider));
        }

        [Test]
        public void GivenMixOfValidAndInvalid_ThenAddAsAppropriate()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new FakeClaimsProviderImpl())
                .As<IClaimsProvider>()
                .InstancePerRequest();

            builder.Register(cc => new DataProtectorImpl())
                .As<IDataProtector>()
                .InstancePerRequest();

            builder.Register(cc => new SomeOtherTypeImpl())
                .As<ISomeOtherType>()
                .InstancePerRequest();

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();
            factory.ResolveUsingAutofac(container, options =>
                options
                    .Including<IClaimsProvider>()
            );

            factory.Registrations.Should()
                .Contain(registration => registration.DependencyType == typeof(IClaimsProvider));

            factory.Registrations.Should()
                .Contain(registration => registration.DependencyType == typeof(IDataProtector));

            factory.Registrations.Should()
                .NotContain(registration => registration.DependencyType == typeof(ISomeOtherType));
        }
    }

    interface ISomeOtherType
    { }

    class SomeOtherTypeImpl : ISomeOtherType
    { }

    class DataProtectorImpl : IDataProtector
    {
        public byte[] Protect(byte[] data, string entropy = "")
        {
            throw new NotImplementedException();
        }

        public byte[] Unprotect(byte[] data, string entropy = "")
        {
            throw new NotImplementedException();
        }
    }
}