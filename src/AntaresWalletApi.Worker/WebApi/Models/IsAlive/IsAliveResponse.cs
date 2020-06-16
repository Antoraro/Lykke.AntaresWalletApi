using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AntaresWalletApi.Worker.WebApi.Models.IsAlive
{
    public class IsAliveResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("env")]
        public string Env { get; set; }

        [JsonPropertyName("host")]
        public string HostName { get; set; }

        [JsonPropertyName("isDebug")]
        public bool IsDebug { get; set; }

        [JsonPropertyName("startedAt")]
        public DateTime StartedAt { get; set; }

        [JsonPropertyName("issueIndicators")]
        public List<StateIndicator> StateIndicators { get; set; }

        public class StateIndicator
        {
            [JsonPropertyName("type")]
            public string Type { get; set; }

            [JsonPropertyName("value")]
            public string Value { get; set; }
        }
    }
}
