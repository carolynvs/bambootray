﻿namespace BambooTray.Domain.Resources
{
    using Newtonsoft.Json;

    public class Projects
    {
        [JsonProperty("project")]
        public Project[] Project { get; set; }
    }
}