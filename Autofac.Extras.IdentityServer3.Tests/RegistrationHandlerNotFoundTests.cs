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
    class RegistrationHandlerNotFoundTests
    {
        [Test]
        public void IfHandlerNotPresentForIdServerType_ThenThrowException()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new DataProtectorImpl())
                .Keyed<IDataProtector>("someKey"); // keyed isn't supported now, so this should throw

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();

            Action resolveAction = () => factory.ResolveUsingAutofac(container);
            resolveAction.Should().Throw<NoHandlerFoundException>();
        }

        [Test]
        public void IfHandlerNotPresentForNonIdServerType_ThenDontThrow()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeOtherTypeImpl())
                .Keyed<ISomeOtherType>("someKey");

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();

            Action resolveAction = () => factory.ResolveUsingAutofac(container);
            resolveAction.Should().NotThrow();
        }

        [Test]
        public void IfHandlerNotPresentForNonIdServerType2_ThenThrowException()
        {
            var builder = new ContainerBuilder();

            builder.Register(cc => new SomeOtherTypeImpl())
                .Keyed<ISomeOtherType>("someKey"); // keyed isn't supported now, so this should throw

            var container = builder.Build();

            var factory = new IdentityServerServiceFactory();

            Action resolveAction = () => factory.ResolveUsingAutofac(container, 
                options => options
                    .RegisteringAllTypes()
                    .Excluding<ILifetimeScope>()
                    .Excluding<IComponentContext>()
                );
            resolveAction.Should().Throw<NoHandlerFoundException>();
        }
    }
}
