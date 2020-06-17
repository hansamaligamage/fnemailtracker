using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace fnemailtracker
{
    class SubscriptionNotification
    {
        [JsonProperty("clientState")]
        public string ClientState { get; set; }
        [JsonProperty("resource")]
        public string Resource { get; set; }
        [JsonProperty("subscriptionId")]
        public string SubscriptionId { get; set; }
        [JsonProperty("changeType")]
        public string ChangeType { get; set; }
    }
}
