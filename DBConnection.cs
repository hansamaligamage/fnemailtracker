using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace fnemailtracker
{
    public class DBConnection
    {
        public string StorageConnectionString { get; set; }
        public static CosmosClient CreateCosmosClient(ILogger log)
        {
            var endpointUrl = GetEndpointUrl();
            var authorizationKey = GetKey();
            log.LogInformation("Cosmos DB account is created\n");
            return new CosmosClient(endpointUrl, authorizationKey);
        }

        public static async Task CreateDatabaseAsync(CosmosClient cosmosClient, string database, ILogger log)
        {
            // Create a new database
            var cosmosdb = await cosmosClient.CreateDatabaseIfNotExistsAsync(database);
            log.LogInformation("Created Database: {0}\n", database);
        }

        public static async Task CreateContainerAsync(CosmosClient cosmosClient, string database, string container, ILogger log)
        {
            // Create a new container
            var cosmoscontainer = await cosmosClient.GetDatabase(database).CreateContainerIfNotExistsAsync(container, "/Subject");
            log.LogInformation("Created Container: {0}\n", container);
        }

        public static async Task AddItemsToContainerAsync(CosmosClient cosmosClient, string database, string container, Email email, ILogger log)
        {
            var cosmosContainer = cosmosClient.GetContainer(database, container);
            try
            {
                ItemResponse<Email> emailResponse = await cosmosContainer.CreateItemAsync<Email>(email, new PartitionKey(email.Subject));
            }
            catch(Exception ex)
            {
                log.LogInformation("Error " + ex.GetBaseException());
            }
            log.LogInformation("Created item in database with id: {0}\n", email.Id);
        }

        public static string GetEndpointUrl ()
        {
            return Environment.GetEnvironmentVariable("EndPointUrl");
        }

        public static string GetKey ()
        {
            return Environment.GetEnvironmentVariable("Key");
        }
    }
}
