using System;
using System.Net.Http;
using Azure.Core.Pipeline;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure.Messaging;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;



namespace AdtEventEndpoint
{
    public static class AdtEventEndpoint
    {
        /// <summary> Azure Digital Twin's relationships events type </summary>
        private static readonly string relationshipEventType = "Microsoft.DigitalTwins.Relationship";
        /// <summary> Azure Digital Twin's create Digital Twin event type </summary>
        private static readonly string createDigitalTwinEventType = "Microsoft.DigitalTwins.Twin.Create";
        /// <summary> Azure Digital Twin's delete Digital Twin event type </summary>
        private static readonly string deleteDigitalTwinEventType = "Microsoft.DigitalTwins.Twin.Delete";

        /// <summary>A HTTP trigger function. It is used by client to be able to connect to SignalR Service.
        /// It uses the SignalRConnectionInfo input binding
        /// to generate and return valid connection information.</summary>
        /// <param name="req">the trigger of the function. Client perform a post request on this function in order to obtain the token.</param>
        /// <param name="connectionInfo">the connection information returned to the client.</param>
        [FunctionName("negotiate")]
        public static SignalRConnectionInfo GetSignalRInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalRConnectionInfo(HubName = "dteventendpointhub")] SignalRConnectionInfo connectionInfo)
        {
            return connectionInfo;
        }

        /// <summary>This Azure function handle the events from the Event Grid topic 
        /// to which it is subscribed in order to receive Azure Digital Twins events.</summary>
        /// <param name="eventGridEvent">the trigger of the function. It is an event from the Event Grid. The specification used is CloudEvent v1.</param>
        /// <param name="signalRConnection">the output binding to the SignalR connection used to send the events.</param>
        [FunctionName("broadcast")]
        public static Task Observe(
            [EventGridTrigger] CloudEvent eventGridEvent,
            [SignalR(HubName = "dteventendpointhub", ConnectionStringSetting = "AzureSignalRConnectionString")] IAsyncCollector<SignalRMessage> signalRConnection,
            ILogger log)
        {
            // Obtain event data and construct the event object to send via SignalR
            JObject eventToClients = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());
            // Add metadata to the event object
            eventToClients.Add("id", eventGridEvent.Subject);
            eventToClients.Add("eventType", eventGridEvent.Type);
            eventToClients.Add("eventDateTime", eventGridEvent.Time);

            // When the event involve the creation or the deletion of a reletionship then get the source and target's model.
            if(eventGridEvent.Type.StartsWith(relationshipEventType)) {
                DigitalTwinsClient client = new DigitalTwinsClient(
                    new Uri(Environment.GetEnvironmentVariable("ADT_SERVICE_URL")),
                    new DefaultAzureCredential(),
                    new DigitalTwinsClientOptions{ Transport = new HttpClientTransport(new HttpClient()) });

                // Get source digital twin's model
                string sourceId = eventToClients["data"]["$sourceId"].ToString();
                BasicDigitalTwin sourceDigitalTwin = client.GetDigitalTwin<BasicDigitalTwin>(sourceId).Value;
                eventToClients["data"]["$sourceModel"] = sourceDigitalTwin.Metadata.ModelId;

                // Get target digital twin's model
                string targetId = eventToClients["data"]["$targetId"].ToString();
                BasicDigitalTwin targetDigitalTwin = client.GetDigitalTwin<BasicDigitalTwin>(targetId).Value;
                eventToClients["data"]["$targetModel"] = targetDigitalTwin.Metadata.ModelId;
            } else if (eventGridEvent.Type.StartsWith(createDigitalTwinEventType) || eventGridEvent.Type.StartsWith(deleteDigitalTwinEventType)) {
                // When the event involves the creation or the deletion of a Digital Twin then include the model
                // of the Digital Twin at the root level of the event itself.
                eventToClients.Add("model", eventToClients["data"]["$metadata"]["$model"]);
            }
            
            log.LogInformation($"New event:\n {eventToClients.ToString()}");

            // Send the event via SignalR
            return signalRConnection.AddAsync(
                new SignalRMessage
                {
                    Target = "newMessage",
                    Arguments = new[] { JsonConvert.SerializeObject(eventToClients) }
                });
        }
    }
}
