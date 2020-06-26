using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AWS.IoT.FleetProvisioning.Extensions
{
    public static class JsonExtensions
    {
        public static string ToJson(this object obj, bool disableIndent = false, JsonSerializerSettings settings = null)
        {
            var formatting = disableIndent ? Formatting.None : Formatting.Indented;

            settings ??= JsonSerializerHelper.GetSettings();

            return JsonConvert.SerializeObject(obj, formatting, settings);
        }

        public static T FromJson<T>(this string json, JsonSerializerSettings settings = null) where T : new()
        {
            settings ??= JsonSerializerHelper.GetSettings();

            return string.IsNullOrEmpty(json)
                ? (T) (object) null
                : JsonConvert.DeserializeObject<T>(json, settings);
        }
    }

    public static class JsonSerializerHelper
    {
        public static JsonSerializerSettings GetSettings()
        {
            return new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
        }
    }
}