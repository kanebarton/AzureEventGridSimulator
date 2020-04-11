using System;
using Newtonsoft.Json;

namespace AzureEventGridSimulator.Data
{
    public class UnitTestData
    {
        [JsonProperty(PropertyName = "tenantId", Required = Required.Default)]
        public string TenantId { get; set; }

        [JsonProperty(PropertyName = "id", Required = Required.Default)]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "name", Required = Required.Default)]
        public string Name { get; set; }

        public bool IsUnitTestData() =>    (TenantId?.Equals("unittests", StringComparison.OrdinalIgnoreCase) ?? false)
                                        || (Id?.Equals("00000000000000000000000000000000") ?? false)
                                        || (Name?.Contains("UnitTesting Kiandra TeamUp #", StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
