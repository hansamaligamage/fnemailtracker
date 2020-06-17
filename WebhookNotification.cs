using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace fnemailtracker
{
    class WebhookNotification
    {
        [JsonProperty("value")]
        public SubscriptionNotification[] Notifications { get; set; }
    }
}
