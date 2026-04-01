using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Runtime.Serialization;
using JsonSubTypes;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Stj = System.Text.Json;
using StjSer = System.Text.Json.Serialization;

namespace DI.MCP.Server.Models.Analytics;

#region Enums

public enum PredicateOperator
{
    TIME_PERIOD = 0,
    BETWEEN,
    GREATER_THAN,
    INCLUDES,
    EQUALS,
    TIME_SERIES,
    EXCLUDES
}

public enum Period
{
    LAST = 0,
    CURRENT = 1,
    NEXT = 2,
    CUSTOM = 3,
}

public enum Interval
{
    DAY = 0,
    WEEK = 1,
    MONTH = 2,
    QUARTER = 3,
    YEAR = 4,
    RANGE = 5,
}

public enum Projection
{
    BACKWARD = 0,
    FORWARD = 1,
}

public enum Increment
{
    CONSECUTIVE = 0,
    WEEK_OVER_WEEK = 1,
    MONTH_OVER_MONTH = 2,
    QUARTER_OVER_QUARTER = 3,
    YEAR_OVER_YEAR = 4,
}

public enum MetricQueryGroupBy
{
    BASE = 0,
    PROVIDER = 1,
    PRACTICE = 2,
    PRACTICE_PROVIDER = 3,
    ONLY_PROVIDER = 4,
    ONLY_PRACTICE = 5,
    ONLY_PRACTICE_PROVIDER = 6
}

public enum DisplayGroupBy
{
    REGION = 1,
    ENTERPRISE = 2,
    PROCEDURE_CATEGORY = 3,
    PRACTICE = 4,
    TITLE = 5,
}

#endregion

#region Operand Hierarchy

[DataContract]
[JsonConverter(typeof(JsonSubtypes), "operator")]
[JsonSubtypes.KnownSubType(typeof(OperandTimePeriod), PredicateOperator.TIME_PERIOD)]
[JsonSubtypes.KnownSubType(typeof(OperandBetween), PredicateOperator.BETWEEN)]
[JsonSubtypes.KnownSubType(typeof(OperandGreaterThan), PredicateOperator.GREATER_THAN)]
[JsonSubtypes.KnownSubType(typeof(OperandIncludes), PredicateOperator.INCLUDES)]
[JsonSubtypes.KnownSubType(typeof(OperandEquals), PredicateOperator.EQUALS)]
[JsonSubtypes.KnownSubType(typeof(OperandTimeSeries), PredicateOperator.TIME_SERIES)]
[JsonSubtypes.KnownSubType(typeof(OperandExcludes), PredicateOperator.EXCLUDES)]
public class BaseOperand
{
    [DataMember(Name = "operator")]
    [JsonConverter(typeof(StringEnumConverter))]
    [Required(ErrorMessage = "operator is a required field")]
    public PredicateOperator Operator { get; set; }
}

[DataContract]
public class OperandTimePeriod : BaseOperand
{
    [JsonProperty("period", NullValueHandling = NullValueHandling.Ignore)]
    [DataMember(Name = "period")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Period? Period { get; set; }

    [JsonProperty("span", NullValueHandling = NullValueHandling.Ignore)]
    [DataMember(Name = "span")]
    public int? Span { get; set; }

    [JsonProperty("interval")]
    [DataMember(Name = "interval")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Interval Interval { get; set; }

    [JsonProperty("startDate", NullValueHandling = NullValueHandling.Ignore)]
    [DataMember(Name = "startDate")]
    public DateTime? StartDate { get; set; }

    [JsonProperty("endDate", NullValueHandling = NullValueHandling.Ignore)]
    [DataMember(Name = "endDate")]
    public DateTime? EndDate { get; set; }
}

[DataContract]
public class OperandTimeSeries : BaseOperand
{
    [JsonProperty("startDate")]
    [DataMember(Name = "startDate")]
    public DateTime? StartDate { get; set; }

    [JsonProperty("endDate", NullValueHandling = NullValueHandling.Ignore)]
    [DataMember(Name = "endDate")]
    public DateTime? EndDate { get; set; }

    [JsonProperty("interval")]
    [DataMember(Name = "interval")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Interval Interval { get; set; }

    [JsonProperty("iterations")]
    [DataMember(Name = "iterations")]
    public int Iterations { get; set; }

    [JsonProperty("projection")]
    [DataMember(Name = "projection")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Projection Projection { get; set; }

    [JsonProperty("increment")]
    [DataMember(Name = "increment")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Increment Increment { get; set; }
}

[DataContract]
public class OperandBetween : BaseOperand
{
    [JsonProperty("minValue")]
    [DataMember(Name = "minValue")]
    public string MinValue { get; set; }

    [JsonProperty("maxValue")]
    [DataMember(Name = "maxValue")]
    public string MaxValue { get; set; }
}

[DataContract]
public class OperandGreaterThan : BaseOperand
{
    [JsonProperty("value")]
    [DataMember(Name = "value")]
    public string Value { get; set; }
}

[DataContract]
public class OperandEquals : BaseOperand
{
    [JsonProperty("value")]
    [DataMember(Name = "value")]
    public string Value { get; set; }
}

[DataContract]
public class OperandIncludes : BaseOperand
{
    [JsonProperty("value")]
    [DataMember(Name = "value")]
    public List<string> Value { get; set; } = new List<string>();
}

[DataContract]
public class OperandExcludes : BaseOperand
{
    [JsonProperty("value")]
    [DataMember(Name = "value")]
    public List<string> Value { get; set; } = new List<string>();
}

#endregion

#region Predicate

[DataContract]
public class BasePredicate
{
    [JsonProperty("propertyId")]
    [DataMember(Name = "propertyId")]
    public string PropertyId { get; set; }

    [JsonProperty("operand")]
    [DataMember(Name = "operand")]
    [StjSer.JsonConverter(typeof(StjBaseOperandConverter))]
    public BaseOperand Operand { get; set; }
}

#endregion

#region Query Models

public class SimpleTrendByData
{
    [JsonProperty("iterations")]
    public int Iterations { get; set; }

    [JsonProperty("increment")]
    [JsonConverter(typeof(StringEnumConverter))]
    public Increment Increment { get; set; }
}

public class SimpleSettingsViewData
{
    [JsonProperty("includeGoals", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeGoals { get; set; }

    [JsonProperty("displayGroupBy", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public DisplayGroupBy? DisplayGroupBy { get; set; }

    [JsonProperty("includeInputPeriod", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeInputPeriod { get; set; }

    [JsonProperty("includeTimers", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeTimers { get; set; }

    [JsonProperty("includeNullRows", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeNullRows { get; set; }

    [JsonProperty("includeVerboseDebug", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeVerboseDebug { get; set; }

    [JsonProperty("includeForceVerboseDebug", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeForceVerboseDebug { get; set; }

    [JsonProperty("includeSubtotalsBy", NullValueHandling = NullValueHandling.Ignore)]
    public string IncludeSubtotalsBy { get; set; }

    [JsonProperty("includeCacheId", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeCacheId { get; set; }

    [JsonProperty("includeCurrentPeriodInTrend", NullValueHandling = NullValueHandling.Ignore)]
    public bool? IncludeCurrentPeriodInTrend { get; set; }

    [JsonProperty("practiceProductionCalculationMethod", NullValueHandling = NullValueHandling.Ignore)]
    public int? PracticeProductionCalculationMethod { get; set; }
}

public class AnalyticsMetricInputQuery
{
    [Required(ErrorMessage = "select is required.")]
    public List<string> Select { get; set; }

    [Required(ErrorMessage = "from is required.")]
    public string From { get; set; }

    [Required(ErrorMessage = "where is required.")]
    public List<BasePredicate> Where { get; set; }

    [JsonProperty("groupBy", NullValueHandling = NullValueHandling.Ignore)]
    [JsonConverter(typeof(StringEnumConverter))]
    public MetricQueryGroupBy? GroupBy { get; set; }

    public SimpleTrendByData TrendBy { get; set; }
    public SimpleSettingsViewData Options { get; set; } = new SimpleSettingsViewData();
    public Guid? RootPracticeId { get; set; }
    public DateTime? CustomDate { get; set; }
    public DateTime? ClientCurrentDate { get; set; }
    public string GrantId { get; set; }

    [JsonProperty("providerPulseType", NullValueHandling = NullValueHandling.Ignore)]
    public string ProviderPulseType { get; set; }
}

#endregion

#region Converters

/// <summary>
/// System.Text.Json polymorphic converter for BaseOperand.
/// Required because the MCP SDK deserializes tool parameters using System.Text.Json,
/// but the actual API models use Newtonsoft.Json JsonSubtypes for polymorphism.
/// This converter bridges the two, dispatching on the "operator" discriminator.
/// </summary>
public class StjBaseOperandConverter : StjSer.JsonConverter<BaseOperand>
{
    private static readonly Stj.JsonSerializerOptions InternalOptions = new(Stj.JsonSerializerDefaults.Web)
    {
        Converters = { new StjFlexibleDateTimeConverter(), new StjSer.JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> ValueOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "INCLUDES", "EXCLUDES", "EQUALS", "NOT_EQUALS"
    };

    public override BaseOperand? Read(ref Stj.Utf8JsonReader reader, Type typeToConvert, Stj.JsonSerializerOptions options)
    {
        using var doc = Stj.JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var op = root.TryGetProperty("operator", out var opProp)
            ? opProp.GetString() ?? ""
            : "";

        if (op.Equals("TIME_PERIOD", StringComparison.OrdinalIgnoreCase))
            return Stj.JsonSerializer.Deserialize<OperandTimePeriod>(root.GetRawText(), InternalOptions);

        if (op.Equals("TIME_SERIES", StringComparison.OrdinalIgnoreCase))
            return Stj.JsonSerializer.Deserialize<OperandTimeSeries>(root.GetRawText(), InternalOptions);

        if (ValueOperators.Contains(op))
            return Stj.JsonSerializer.Deserialize<OperandIncludes>(root.GetRawText(), InternalOptions);

        throw new Stj.JsonException($"Unknown operand operator: '{op}'");
    }

    public override void Write(Stj.Utf8JsonWriter writer, BaseOperand value, Stj.JsonSerializerOptions options)
    {
        Stj.JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}

/// <summary>
/// Newtonsoft.Json flexible DateTime converter that handles multiple date formats from AI tool calls.
/// Accepts: yyyy-MM-dd, MM-dd-yyyy, MM/dd/yyyy, and other common formats.
/// Always serializes as yyyy-MM-dd for downstream API compatibility.
/// </summary>
public class FlexibleDateTimeConverter : Newtonsoft.Json.JsonConverter<DateTime?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "MM-dd-yyyy",
        "MM/dd/yyyy",
        "M-d-yyyy",
        "M/d/yyyy"
    ];

    public override DateTime? ReadJson(JsonReader reader, Type objectType, DateTime? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException("Expected a date string or null.");

        var value = (string?)reader.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        throw new JsonSerializationException($"Unable to parse '{value}' as a DateTime. Expected format: yyyy-MM-dd.");
    }

    public override void WriteJson(JsonWriter writer, DateTime? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(value.Value.ToString("yyyy-MM-dd"));
    }
}

/// <summary>
/// System.Text.Json flexible DateTime converter for MCP SDK compatibility.
/// The MCP SDK uses System.Text.Json internally for tool parameter deserialization,
/// so operand date fields need this converter to handle non-standard date formats.
/// </summary>
internal class StjFlexibleDateTimeConverter : StjSer.JsonConverter<DateTime?>
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "MM-dd-yyyy",
        "MM/dd/yyyy",
        "M-d-yyyy",
        "M/d/yyyy"
    ];

    public override DateTime? Read(ref Stj.Utf8JsonReader reader, Type typeToConvert, Stj.JsonSerializerOptions options)
    {
        if (reader.TokenType == Stj.JsonTokenType.Null)
            return null;

        if (reader.TokenType != Stj.JsonTokenType.String)
            throw new Stj.JsonException("Expected a date string or null.");

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return dt;

        throw new Stj.JsonException($"Unable to parse '{value}' as a DateTime. Expected format: yyyy-MM-dd.");
    }

    public override void Write(Stj.Utf8JsonWriter writer, DateTime? value, Stj.JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
    }
}

#endregion
