using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    /// <summary>
    /// Converts between the user-facing seconds value and the wire value a model's numeric
    /// parameter expects, based on the parameter's "x-unit" / "x-reference-frame-rate".
    /// The UI always works in seconds; this is the single place that knows how to translate
    /// to whatever unit the selected model advertises.
    /// </summary>
    static class ModelUnitConversion
    {
        // Frame counts are quantized to a multiple of this for frame-based motion models.
        const int k_FrameQuantization = 8;

        /// <summary>
        /// Effective reference frame rate for a property: its declared rate, else the legacy
        /// default (30). Guards against a non-positive declared rate.
        /// </summary>
        public static double GetFrameRate(SchemaProperty prop)
        {
            var rate = prop?.ReferenceFrameRate ?? ModelConstants.Units.DefaultFrameRate;
            return rate > 0 ? rate : ModelConstants.Units.DefaultFrameRate;
        }

        /// <summary>
        /// True when the property is expressed in seconds.
        /// </summary>
        public static bool IsSecondsUnit(SchemaProperty prop) => prop?.Unit == ModelConstants.Units.Seconds;

        /// <summary>
        /// True when the property is (explicitly or by legacy default) expressed in frames.
        /// A null/absent unit is treated as frames to preserve pre-x-unit behavior.
        /// </summary>
        public static bool IsFrameUnit(SchemaProperty prop)
        {
            var unit = prop?.Unit;
            return string.IsNullOrEmpty(unit) || unit == ModelConstants.Units.Frames;
        }

        /// <summary>
        /// Converts a duration in seconds to the integer value the given length-style property
        /// expects on the wire, clamped to the property's minimum/maximum when present.
        /// - "seconds": rounded to the nearest whole second.
        /// - "frames" (or absent unit): seconds * frameRate, quantized to a multiple of 8.
        /// </summary>
        public static int SecondsToWire(SchemaProperty prop, float seconds)
        {
            int value;
            if (IsSecondsUnit(prop))
            {
                value = Mathf.RoundToInt(seconds);
            }
            else // frames, or legacy model list with no x-unit
            {
                var fps = GetFrameRate(prop);
                value = Mathf.RoundToInt((float)(seconds * fps / k_FrameQuantization)) * k_FrameQuantization;
            }

            return Clamp(prop, value);
        }

        /// <summary>
        /// Converts the property's minimum/maximum (expressed in its own unit) back to seconds,
        /// for driving a seconds-based UI control. Returns (fallbackMin, fallbackMax) when the
        /// property or either bound is absent.
        /// </summary>
        public static (float min, float max) WireRangeToSeconds(SchemaProperty prop, float fallbackMin, float fallbackMax)
        {
            if (prop?.Minimum == null || prop.Maximum == null)
                return (fallbackMin, fallbackMax);

            if (IsSecondsUnit(prop))
                return ((float)prop.Minimum.Value, (float)prop.Maximum.Value);

            // frames (or legacy): convert frame bounds to seconds via the reference rate.
            var fps = GetFrameRate(prop);
            return ((float)(prop.Minimum.Value / fps), (float)(prop.Maximum.Value / fps));
        }

        static int Clamp(SchemaProperty prop, int value)
        {
            if (prop?.Minimum != null && value < prop.Minimum.Value)
                value = Mathf.CeilToInt((float)prop.Minimum.Value);
            if (prop?.Maximum != null && value > prop.Maximum.Value)
                value = Mathf.FloorToInt((float)prop.Maximum.Value);
            return value;
        }
    }
}
