using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace fnemailtracker
{
    public class Email
    {
        [JsonProperty(PropertyName ="id")]
        public Guid Id { get; set; }
        public string Subject { get; set; }
        public string EmailBody { get; set; }
    }
}
