﻿using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.AspNetCore.Authentication;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace SignService.Utils
{
    public interface IKeyVaultService
    {
        Task<string> GetAccessTokenAsync();
        Task<X509Certificate2> GetCertificateAsync();
        Task<RSA> ToRSA();
        CertificateInfo CertificateInfo { get; }
    }
    public class KeyVaultService : IKeyVaultService
    {
        readonly KeyVaultClient client;
        X509Certificate2 certificate;
        KeyIdentifier keyIdentifier;
        string validatedToken;
        readonly CertificateInfo certificateInfo;
        readonly IOptionsSnapshot<AzureAdOptions> aadOptions;
        readonly IHttpContextAccessor contextAccessor;

        public KeyVaultService(IOptionsSnapshot<Settings> settings, IOptionsSnapshot<AzureAdOptions> aadOptions, IHttpContextAccessor contextAccessor, ILogger<KeyVaultService> logger)
        {
            async Task<string> Authenticate(string authority, string resource, string scope)
            {
                return await GetAccessTokenAsync().ConfigureAwait(false); 
            }

            client = new KeyVaultClient(Authenticate, new HttpClient());
            
            // This must be here because we add it in the request validation
            var authId = contextAccessor.HttpContext.User.Claims.First(c => c.Type == "authId").Value;
            certificateInfo = settings.Value.UserCertificateInfoMap[authId];
            this.aadOptions = aadOptions;
            this.contextAccessor = contextAccessor;
        }

        public CertificateInfo CertificateInfo => certificateInfo;

        public async Task<string> GetAccessTokenAsync()
        {
            if (validatedToken == null)
            {
                var context = new AuthenticationContext($"{aadOptions.Value.AADInstance}{aadOptions.Value.TenantId}", null); // No token caching
                var credential = new ClientCredential(aadOptions.Value.ClientId, aadOptions.Value.ClientSecret);
                var resource = "https://vault.azure.net";

                // This must be here because we add it in the request validation
                var authType = contextAccessor.HttpContext.User.Claims.First(c => c.Type == "authType").Value;
                var authId = contextAccessor.HttpContext.User.Claims.First(c => c.Type == "authId").Value;

                AuthenticationResult result = null;
                if (authType == "user")
                {
                    var incomingToken = await contextAccessor.HttpContext.GetTokenAsync("access_token");
                    result = await context.AcquireTokenAsync(resource, credential, new UserAssertion(incomingToken));

                }
                else if (authType == "application")
                {
                    result = await context.AcquireTokenAsync(resource, credential).ConfigureAwait(false);
                }

                if (result == null)
                {
                    throw new InvalidOperationException("Authentication to Azure failed.");
                }
                validatedToken = result.AccessToken;
            }

            return validatedToken;
        }

        public async Task<X509Certificate2> GetCertificateAsync()
        {
            if (certificate == null)
            {
                var cert = await client.GetCertificateAsync(certificateInfo.KeyVaultUrl, certificateInfo.KeyVaultCertificateName).ConfigureAwait(false);
                certificate = new X509Certificate2(cert.Cer);
                keyIdentifier = cert.KeyIdentifier;
            }
            return certificate;
        }

        public async Task<RSA> ToRSA()
        {
            await GetCertificateAsync()
                .ConfigureAwait(false);
            return client.ToRSA(keyIdentifier, certificate);
        }
    }
}
