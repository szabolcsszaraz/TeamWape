using System;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Integrations.Sample.Editor
{
    [FunctionCallRenderer(typeof(SampleTools), nameof(SampleTools.GetWeather))]
    class GetWeatherRenderer : DefaultFunctionCallRenderer
    {
        public override void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
            var weather = result.GetTypedResult<SampleTools.WeatherOutput>();
            var typeText = weather.Type switch {
                SampleTools.WeatherOutput.WeatherType.Sun => "☀️",
                SampleTools.WeatherOutput.WeatherType.Cloud => "☁️",
                SampleTools.WeatherOutput.WeatherType.Rain => "🌧️",
                SampleTools.WeatherOutput.WeatherType.Snow => "❄️",
                _ => throw new ArgumentOutOfRangeException()
            };
            
            Add(new TextField { value = $"{typeText} {weather.Temperature}°C", isReadOnly = true });
        }
    }
}
