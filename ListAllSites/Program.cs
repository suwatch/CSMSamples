using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace ListAllSites
{
    static class Program
    {
        public const string AADUrl = "https://login.windows.net/";
        public const string CSMUrl = "https://management.azure.com/";
        public const string CSMApiVersion = "2014-01-01";
        public const string AzureToolClientId = "1950a258-227b-4e31-a9cf-717495945fc2";
        public static Dictionary<TokenCacheKey, string> TokenCache = new Dictionary<TokenCacheKey, string>();

        static void Main(string[] args)
        {
            try
            {
                Console.Write("Getting tenant-less token ... ");
                var authResult = GetAuthorizationResult(tokenCache: TokenCache);
                Console.WriteLine("done!");

                var tenants = GetTokenForTenants(authResult).Result;

                foreach (var tenant in tenants)
                {
                    Console.Write("List subscriptions for tenant {0} ... ", tenant.Key);
                    var subscriptions = GetSubscriptions(tenant.Value.AccessToken).Result;
                    Console.WriteLine("{0} subscriptions found!", subscriptions.Length);

                    foreach (var subscription in subscriptions)
                    {
                        Console.WriteLine("Subscription: {0} ({1})", subscription.displayName, subscription.subscriptionId);
                    }
                    Console.WriteLine();

                    foreach (var subscription in subscriptions)
                    {
                        Console.Write("List sites for subscription {0} ({1}) ... ", subscription.displayName, subscription.subscriptionId);
                        var sites = GetSites(tenant.Value.AccessToken, subscription.subscriptionId).Result;
                        Console.WriteLine("{0} sites found!", sites.Length);

                        foreach (var site in sites)
                        {
                            Console.WriteLine("Site: {0} ({1})", site.name, site.location);
                        }

                        Console.WriteLine();
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        static async Task<IDictionary<string, AuthenticationResult>> GetTokenForTenants(AuthenticationResult authResult)
        {
            var tenants = await GetTenants(authResult.AccessToken);
            Console.WriteLine("User {0} has {1} tenants", authResult.UserInfo.UserId, tenants.Length);

            Dictionary<string, AuthenticationResult> results = new Dictionary<string, AuthenticationResult>();
            foreach (var tenant in tenants)
            {
                Console.Write("Getting token for tenant {0} ... ", tenant.tenantId);
                results[tenant.tenantId] = GetAuthorizationResult(tenantId: tenant.tenantId, tokenCache: TokenCache, userId: authResult.UserInfo.UserId);
                Console.WriteLine("done!");
            }

            return results;
        }

        private static async Task<TenantInfo[]> GetTenants(string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                var url = string.Format("{0}/tenants?api-version={1}", CSMUrl, CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    var result = await response.Content.ReadAsAsync<TenantsResult>();
                    return result.value;
                }
            }
        }

        private static async Task<SubscriptionInfo[]> GetSubscriptions(string token)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                var url = string.Format("{0}/subscriptions?api-version={1}", CSMUrl, CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    var result = await response.Content.ReadAsAsync<SubscriptionsResult>();
                    return result.value;
                }
            }
        }

        private static async Task<SiteInfo[]> GetSites(string token, string subscriptionId)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);

                var url = string.Format("{0}/subscriptions/{1}/providers/Microsoft.Web/sites?api-version={2}", CSMUrl, subscriptionId, CSMApiVersion);
                using (var response = await client.GetAsync(url))
                {
                    return await response.Content.ReadAsAsync<SiteInfo[]>();
                }
            }
        }

        private static AuthenticationResult GetAuthorizationResult(string tenantId = "common", IDictionary<TokenCacheKey, string> tokenCache = null, string userId = null)
        {
            AuthenticationResult result = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var context = new AuthenticationContext(authority: AADUrl + tenantId, validateAuthority: true, tokenCacheStore: tokenCache);

                    if (!string.IsNullOrEmpty(userId))
                    {
                        result = context.AcquireToken(
                            resource: "https://management.core.windows.net/",
                            clientId: AzureToolClientId,
                            redirectUri: new Uri("urn:ietf:wg:oauth:2.0:oob"),
                            userId: userId);
                    }
                    else
                    {
                        result = context.AcquireToken(
                            resource: "https://management.core.windows.net/",
                            clientId: AzureToolClientId,
                            redirectUri: new Uri("urn:ietf:wg:oauth:2.0:oob"),
                            promptBehavior: PromptBehavior.Always);
                    }
                }
                catch (Exception threadEx)
                {
                    Console.WriteLine(threadEx.Message);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Name = "AcquireTokenThread";
            thread.Start();
            thread.Join();

            return result;
        }

        public class TenantsResult
        {
            public TenantInfo[] value { get; set; }
        }

        public class TenantInfo
        {
            public string id { get; set; }
            public string tenantId { get; set; }
        }

        public class SubscriptionsResult
        {
            public SubscriptionInfo[] value { get; set; }
        }

        public class SubscriptionInfo
        {
            public string id { get; set; }
            public string subscriptionId { get; set; }
            public string displayName { get; set; }
            public string state { get; set; }
        }

        public class SitesResult
        {
            public SiteInfo[] value { get; set; }
        }

        public class SiteInfo
        {
            public string id { get; set; }
            public string name { get; set; }
            public string location { get; set; }
        }
    }
}
