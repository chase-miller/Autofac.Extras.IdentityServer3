using System;
using System.Collections.Generic;
using System.Linq;
using Autofac.Extras.IdentityServer3.Core;
using IdentityServer3.Core.Configuration;
using IdentityServer3.Core.Services;

namespace Autofac.Extras.IdentityServer3.Extensions
{
    public static class IdServerExplicitExtensions
    {
        private static readonly IEnumerable<Type> MatchingTypes = new[]
        {
            typeof(IEventService),
            typeof(IUserService),
            typeof(IClaimsProvider),
            typeof(ICorsPolicyService),
            typeof(ICustomRequestValidator),
            typeof(IScopeStore),
            typeof(IAuthenticationSessionValidator),
            typeof(IViewService),
            typeof(IClientStore),
            typeof(IAuthorizationCodeStore),
            typeof(ITokenHandleStore),
            typeof(IRefreshTokenStore),
            typeof(IConsentStore),
            typeof(ITokenService),
            typeof(IRefreshTokenService),
            typeof(IExternalClaimsFilter),
            typeof(ICustomTokenValidator),
            typeof(ICustomTokenResponseGenerator),
            typeof(IConsentService),
            typeof(ITokenSigningService),
            typeof(ISigningKeyService),
            typeof(IRedirectUriValidator),
            typeof(ILocalizationService),
            typeof(IClientPermissionsService),
            typeof(ICustomGrantValidator),
            typeof(ISecretParser),
        };

        public static Options RegisteringIdServerExtensionPointsExplicitly(this Options options)
        {
            return options.WithRegistrationHandler(
                MatchesExtensionPoint,
                RegisterExtensionPointExplicitly
            );
        }

        private static bool MatchesExtensionPoint(RegistrationContext context)
        {
            var resolvedType = context.ResolvedType;

            if (resolvedType == null)
                return false;

            return MatchingTypes.Contains(resolvedType);
        }

        private static void RegisterExtensionPointExplicitly(IdentityServerServiceFactory factory, RegistrationContext context)
        {
            var resolvedType = context.ResolvedType;

            if (resolvedType == null)
                return;

            switch (resolvedType)
            {
                case Type t when t == typeof(IEventService):
                    // IdServer does a bit of an odd thing during initialization where it resolves the <see cref="IEventService"/> outside of an http request. 
                    // See https://github.com/IdentityServer/IdentityServer3/blob/93bc6bc9b536146b9e3fa0bed21d77283d07f788/source/Core/Configuration/AppBuilderExtensions/UseIdentityServerExtension.cs#L113
                    // Hack the registration to support this.
                    factory.EventService = Create<IEventService>(checkForMiddleware: false);
                    break;
                case Type t when t == typeof(IUserService):
                    factory.UserService = Create<IUserService>();
                    break;
                case Type t when t == typeof(IClaimsProvider):
                    factory.ClaimsProvider = Create<IClaimsProvider>();
                    break;
                case Type t when t == typeof(ICorsPolicyService):
                    factory.CorsPolicyService = Create<ICorsPolicyService>();
                    break;
                case Type t when t == typeof(IViewService):
                    factory.ViewService = Create<IViewService>();
                    break;
                case Type t when t == typeof(ICustomRequestValidator):
                    factory.CustomRequestValidator = Create<ICustomRequestValidator>();
                    break;
                case Type t when t == typeof(IScopeStore):
                    factory.ScopeStore = Create<IScopeStore>();
                    break;
                case Type t when t == typeof(IAuthenticationSessionValidator):
                    factory.AuthenticationSessionValidator = Create<IAuthenticationSessionValidator>();
                    break;
                case Type t when t == typeof(IClientStore):
                    factory.ClientStore = Create<IClientStore>();
                    break;
                case Type t when t == typeof(IAuthorizationCodeStore):
                    factory.AuthorizationCodeStore = Create<IAuthorizationCodeStore>();
                    break;
                case Type t when t == typeof(ITokenHandleStore):
                    factory.TokenHandleStore = Create<ITokenHandleStore>();
                    break;
                case Type t when t == typeof(IRefreshTokenStore):
                    factory.RefreshTokenStore = Create<IRefreshTokenStore>();
                    break;
                case Type t when t == typeof(IConsentStore):
                    factory.ConsentStore = Create<IConsentStore>();
                    break;
                case Type t when t == typeof(ITokenService):
                    factory.TokenService = Create<ITokenService>();
                    break;
                case Type t when t == typeof(IRefreshTokenService):
                    factory.RefreshTokenService = Create<IRefreshTokenService>();
                    break;
                case Type t when t == typeof(IExternalClaimsFilter):
                    factory.ExternalClaimsFilter = Create<IExternalClaimsFilter>();
                    break;
                case Type t when t == typeof(ICustomTokenValidator):
                    factory.CustomTokenValidator = Create<ICustomTokenValidator>();
                    break;
                case Type t when t == typeof(ICustomTokenResponseGenerator):
                    factory.CustomTokenResponseGenerator = Create<ICustomTokenResponseGenerator>();
                    break;
                case Type t when t == typeof(IConsentService):
                    factory.ConsentService = Create<IConsentService>();
                    break;
                case Type t when t == typeof(ITokenSigningService):
                    factory.TokenSigningService = Create<ITokenSigningService>();
                    break;
                case Type t when t == typeof(ISigningKeyService):
                    factory.SigningKeyService = Create<ISigningKeyService>();
                    break;
                case Type t when t == typeof(IRedirectUriValidator):
                    factory.RedirectUriValidator = Create<IRedirectUriValidator>();
                    break;
                case Type t when t == typeof(ILocalizationService):
                    factory.LocalizationService = Create<ILocalizationService>();
                    break;
                case Type t when t == typeof(IClientPermissionsService):
                    factory.ClientPermissionsService = Create<IClientPermissionsService>();
                    break;
                case Type t when t == typeof(ICustomGrantValidator) || t == typeof(IEnumerable<ICustomGrantValidator>):
                    factory.CustomGrantValidators = new List<Registration<ICustomGrantValidator>>();
                    factory.RegisterAsAutofacResolvable<IEnumerable<ICustomGrantValidator>>(container: context.Container);
                    break;
                case Type t when t == typeof(ISecretParser) || t == typeof(IEnumerable<ISecretParser>):
                    factory.SecretParsers = new List<Registration<ISecretParser>>();
                    factory.RegisterAsAutofacResolvable<IEnumerable<ISecretParser>>(container: context.Container);
                    break;
                case Type t when t == typeof(ISecretValidator) || t == typeof(IEnumerable<ISecretValidator>):
                    factory.SecretValidators = new List<Registration<ISecretValidator>>();
                    factory.RegisterAsAutofacResolvable<IEnumerable<ISecretValidator>>(container: context.Container);
                    break;
                default:
                    break;

                Registration<T> Create<T>(bool checkForMiddleware = true) where T : class
                {
                    return TypeRegistrationExtensions.CreateRegistration<T>(context: context, checkForMiddleware: checkForMiddleware);
                }
            }
        }
    }
}
